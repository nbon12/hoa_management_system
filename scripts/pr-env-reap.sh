#!/usr/bin/env bash
# Reap one PR's ephemeral environment BY EXACT NAME, for resources OpenTofu no longer tracks: a killed
# `tofu apply` (created, never recorded) or a teardown that deleted the state after a failed destroy
# (docs/handoffs/2026-10-26-agent-cloud-access.md, "Ops workflows to add" §1; issue #234).
#
# Callers:
#   pr-env-repair.yml `diagnose`   DRY_RUN=true   read-only tokens: inventory + what a reap would delete
#   pr-env-repair.yml `reap`       DRY_RUN=false  CI's write tokens, only after the `ops` approval
#   pr-env.yml, before tofu apply  ONLY_UNTRACKED=true  deletes only what `tofu state list` does not track
#
# Order: Cloud Run (the only writer to the bucket; holds the branch's connections) → R2 bucket (emptied
# first) → the two pr-<n>-* secrets → Neon branch (its endpoint goes with it) → Stripe test webhook
# endpoints → Pages deployments → the state prefix. Those first four kinds plus what is deleted with them
# (secret versions + IAM, the service's IAM, the Neon endpoint) are every resource in
# infra/modules/pr-environment; a new resource type there must be added here (scripts/tests checks it).
# ONLY_UNTRACKED covers the four OpenTofu-managed kinds only (Stripe and Pages are never in state, so they
# would always look untracked), runs in infra/environments/pr after `tofu init`, and leaves the state alone.
#
# Every guard refuses rather than guesses: Cloud Run and the secrets must carry label pr-number=<n>; the
# Neon branch must be the only one named pr-<n> and a child of pr-base; a Stripe endpoint must have
# metadata.pr=<n> and a nekohoa-api-pr-<n>- host. A full reap also refuses an open PR (unless
# ALLOW_OPEN_PR=true) and a state lock younger than 45 minutes (it may belong to a live run). Anything
# refused or failed makes the script exit 1, and a full reap then keeps the state prefix.
#
# Env (required): PR_NUMBER PROJECT REGION PREFIX NEON_PROJECT_ID NEON_PR_BASE_BRANCH_ID CF_ACCOUNT DRY_RUN
#                 ONLY_UNTRACKED; tokens NEON_API_KEY CF_TOKEN; gcloud logged in (WIF in CI).
# Env (optional): STRIPE_SECRET_KEY_TEST (the Stripe step is skipped with a notice when unset);
#                 REPO + GH_TOKEN, ALLOW_OPEN_PR (open-PR guard of a full reap).
#                 With DRY_RUN=true any token may be read-only or empty; an empty one is "not checked".
# Output is names, ids and states only: this public repo's Actions logs are world-readable. Tokens reach
# curl through a file descriptor, never argv.
# shellcheck disable=SC2329 # the delete helpers below run through remove()
set -euo pipefail

: "${PR_NUMBER:?}" "${PROJECT:?}" "${REGION:?}" "${PREFIX:?}" "${NEON_PROJECT_ID:?}" \
  "${NEON_PR_BASE_BRANCH_ID:?}" "${CF_ACCOUNT:?}" "${DRY_RUN:?}" "${ONLY_UNTRACKED:?}"
[[ "$PR_NUMBER" =~ ^[1-9][0-9]*$ ]] || { echo "::error::PR_NUMBER must be a positive integer." >&2; exit 2; }
for f in DRY_RUN ONLY_UNTRACKED; do
  [[ "${!f}" =~ ^(true|false)$ ]] || { echo "::error::${f} must be true or false." >&2; exit 2; }
done
NEON_API_KEY="${NEON_API_KEY:-}" CF_TOKEN="${CF_TOKEN:-}" STRIPE_SECRET_KEY_TEST="${STRIPE_SECRET_KEY_TEST:-}"
if [ "$DRY_RUN" = false ]; then
  : "${NEON_API_KEY:?NEON_API_KEY is required unless DRY_RUN=true}" "${CF_TOKEN:?CF_TOKEN is required unless DRY_RUN=true}"
fi
FULL=false; [ "$ONLY_UNTRACKED" = false ] && FULL=true

SERVICE="nekohoa-api-pr-${PR_NUMBER}"
BUCKET="nekohoa-pr-${PR_NUMBER}-documents"
BRANCH="pr-${PR_NUMBER}"
STATE_BUCKET="nekohoa-dev-tfstate"
STATE_PREFIX="state/pr/${PR_NUMBER}/"
PAGES_PROJECT="nekohoa-dev"
STALE_LOCK_MIN=45
CF_API="https://api.cloudflare.com/client/v4/accounts/${CF_ACCOUNT}"
NEON_API="https://console.neon.tech/api/v2/projects/${NEON_PROJECT_ID}"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
failed=0
done_items=()

fail() { echo "::error::$*"; failed=1; }
# _hdr VAR: the Authorization header for the token held in VAR; nothing when VAR is empty.
_hdr() { if [ -n "${!1:-}" ]; then printf 'Authorization: Bearer %s\n' "${!1}"; fi; }
# call VAR METHOD URL [curl args]: prints the HTTP status ("000" if unreachable); the body is in $tmp/body.
call() {
  local var="$1" method="$2" url="$3" code; shift 3
  code="$(curl -sS -o "$tmp/body" -w '%{http_code}' -X "$method" -H @<(_hdr "$var") "$url" "$@")" || true
  echo "${code:-000}"
}
# remove DESCRIPTION CMD...: runs CMD, or only reports it under DRY_RUN.
remove() {
  local what="$1"; shift
  if [ "$DRY_RUN" = true ]; then echo "  would delete: ${what}"; done_items+=("would delete: ${what}"); return 0; fi
  echo "  deleting: ${what}"
  if "$@"; then done_items+=("deleted: ${what}"); else fail "deleting ${what} failed"; fi
}
# untracked ADDRESS: true unless ONLY_UNTRACKED and the state tracks module.pr_environment.ADDRESS.
tracked=""
untracked() {
  if [ "$ONLY_UNTRACKED" = true ] && grep -qxF "module.pr_environment.$1" <<<"$tracked"; then
    echo "  tracked by state (module.pr_environment.$1); left to tofu"; return 1
  fi
}
# Minutes since an RFC 3339 UTC timestamp (fractional seconds dropped; portable, no GNU date).
age_min() { echo $(( ( $(date -u +%s) - $(jq -rn --arg t "$1" '$t | sub("\\.[0-9]+Z$"; "Z") | fromdateiso8601') ) / 60 )); }

echo "pr-env reap: PR #${PR_NUMBER}, DRY_RUN=${DRY_RUN}, ONLY_UNTRACKED=${ONLY_UNTRACKED}"

GCP_TOKEN="$(gcloud auth print-access-token 2>/dev/null || true)"
if [ -z "$GCP_TOKEN" ]; then
  if [ "$DRY_RUN" = false ]; then echo "::error::gcloud has no usable credential." >&2; exit 1; fi
  echo "::warning::gcloud has no usable credential, so GCP is not checked."
fi

# ── Preflight (a full, real reap): the PR must be closed, and no live run may hold the state lock ──────
if [ "$FULL" = true ] && [ "$DRY_RUN" = false ] && [ "${ALLOW_OPEN_PR:-false}" != true ]; then
  : "${REPO:?REPO is required for the open-PR guard}"
  pr_state="$(gh api "repos/${REPO}/pulls/${PR_NUMBER}" --jq .state 2>/dev/null || echo unknown)"
  if [ "$pr_state" != closed ]; then
    echo "::error::PR #${PR_NUMBER} is '${pr_state}', not closed. Refusing to reap (ALLOW_OPEN_PR=true overrides)."; exit 1
  fi
fi
if [ "$ONLY_UNTRACKED" = true ]; then
  tracked="$(tofu state list)" || { echo "::error::tofu state list failed; refusing to treat everything as untracked."; exit 1; }
fi

state_objects="[]"
if [ "$FULL" = true ]; then
  echo "== State gs://${STATE_BUCKET}/${STATE_PREFIX}"
  if [ -z "$GCP_TOKEN" ]; then echo "  not checked (no GCP credential)"; else
    code="$(call GCP_TOKEN GET "https://storage.googleapis.com/storage/v1/b/${STATE_BUCKET}/o" --get \
      --data-urlencode "prefix=${STATE_PREFIX}" -d versions=true -d maxResults=1000)"
    if [ "$code" = 200 ]; then
      state_objects="$(jq -c '[.items // [] | .[] | {name, generation, timeCreated, live: (.timeDeleted == null)}]' "$tmp/body")"
      [ "$state_objects" = "[]" ] && echo "  no objects"
      while IFS=$'\t' read -r name gen created live; do
        [ -n "$name" ] || continue
        echo "  ${name}#${gen}  created ${created}  $([ "$live" = true ] && echo live || echo noncurrent)"
        if [ "$live" = true ] && [ "${name##*/}" = default.tflock ]; then
          lock_age="$(age_min "$created")"
          echo "  state lock ${gen} is ${lock_age} min old"
          if [ "$DRY_RUN" = false ] && [ "$lock_age" -lt "$STALE_LOCK_MIN" ]; then
            echo "::error::State lock ${gen} is under ${STALE_LOCK_MIN} min old and may belong to a live run. Refusing to reap."; exit 1
          fi
        fi
      done < <(jq -r '.[] | [.name, .generation, .timeCreated, (.live | tostring)] | @tsv' <<<"$state_objects")
    elif [ "$DRY_RUN" = false ]; then
      echo "::error::Listing the state prefix returned HTTP ${code}, so a live lock cannot be ruled out. Refusing to reap."; exit 1
    else fail "listing the state prefix returned HTTP ${code}"; fi
  fi
fi

# ── 1. Cloud Run service ───────────────────────────────────────────────────────────────────────────────
echo "== Cloud Run service ${SERVICE}"
if [ -z "$GCP_TOKEN" ]; then echo "  not checked (no GCP credential)"; else
  run_url="https://run.googleapis.com/v2/projects/${PROJECT}/locations/${REGION}/services/${SERVICE}"
  code="$(call GCP_TOKEN GET "$run_url")"
  case "$code" in
    404) echo "  absent" ;;
    200)
      label="$(jq -r '.labels["pr-number"] // ""' "$tmp/body")"
      echo "  present: created $(jq -r .createTime "$tmp/body"), label pr-number=${label:-<none>}"
      if [ "$(call GCP_TOKEN GET "${run_url}/revisions")" = 200 ]; then
        jq -r '.revisions[]? | "  revision \(.name | split("/") | last)  created \(.createTime)"' "$tmp/body"
      fi
      if untracked google_cloud_run_v2_service.api; then
        if [ "$label" != "$PR_NUMBER" ]; then fail "${SERVICE} lacks label pr-number=${PR_NUMBER}; not deleting it."
        else remove "Cloud Run service ${SERVICE}" gcloud run services delete "$SERVICE" --region "$REGION" --project "$PROJECT" --quiet
        fi
      fi ;;
    *) fail "looking up Cloud Run service ${SERVICE} returned HTTP ${code}" ;;
  esac
fi

# ── 2. R2 bucket (Cloudflare refuses to delete a non-empty one) ──────────────────────────────────────────
empty_and_delete_bucket() {
  local endpoint ak sk v code
  endpoint="$(gcloud secrets versions access latest --secret="${PREFIX}-storage-service-url" --project="$PROJECT")" || return 1
  ak="$(gcloud secrets versions access latest --secret="${PREFIX}-storage-access-key" --project="$PROJECT")" || return 1
  sk="$(gcloud secrets versions access latest --secret="${PREFIX}-storage-secret-key" --project="$PROJECT")" || return 1
  if [ -n "${GITHUB_ACTIONS:-}" ]; then for v in "$endpoint" "$ak" "$sk"; do echo "::add-mask::${v}"; done; fi
  AWS_ACCESS_KEY_ID="$ak" AWS_SECRET_ACCESS_KEY="$sk" AWS_DEFAULT_REGION=auto \
    aws s3 rm "s3://${BUCKET}" --recursive --only-show-errors --endpoint-url "$endpoint" || return 1
  code="$(call CF_TOKEN DELETE "${CF_API}/r2/buckets/${BUCKET}")"
  [ "$code" = 200 ] && return 0
  jq -r '.errors[]? | "  Cloudflare error \(.code): \(.message)"' "$tmp/body" 2>/dev/null || true
  return 1
}
echo "== R2 bucket ${BUCKET}"
if [ -z "$CF_TOKEN" ]; then echo "  not checked (no Cloudflare credential)"; else
  code="$(call CF_TOKEN GET "${CF_API}/r2/buckets/${BUCKET}")"
  if [ "$code" = 200 ]; then
    echo "  present: created $(jq -r '.result.creation_date // "?"' "$tmp/body") (object count not checked)"
    if untracked cloudflare_r2_bucket.documents; then
      remove "R2 bucket ${BUCKET} (emptied first)" empty_and_delete_bucket
    fi
  elif jq -e '[.errors[]?.code] | index(10006)' "$tmp/body" >/dev/null 2>&1; then echo "  absent"
  else fail "looking up R2 bucket ${BUCKET} returned HTTP ${code}"; fi
fi

# ── 3. Secrets (deleting one removes every version and its accessor binding) ─────────────────────────────
for address in google_secret_manager_secret.db_connection google_secret_manager_secret.stripe_webhook; do
  kind="${address#*.}"
  secret="pr-${PR_NUMBER}-${kind//_/-}"
  echo "== Secret ${secret}"
  if [ -z "$GCP_TOKEN" ]; then echo "  not checked (no GCP credential)"; continue; fi
  code="$(call GCP_TOKEN GET "https://secretmanager.googleapis.com/v1/projects/${PROJECT}/secrets/${secret}")"
  case "$code" in
    404) echo "  absent" ;;
    200)
      label="$(jq -r '.labels["pr-number"] // ""' "$tmp/body")"
      echo "  present: created $(jq -r .createTime "$tmp/body"), label pr-number=${label:-<none>}"
      if untracked "$address"; then
        if [ "$label" != "$PR_NUMBER" ]; then fail "${secret} lacks label pr-number=${PR_NUMBER}; not deleting it."
        else remove "secret ${secret}" gcloud secrets delete "$secret" --project "$PROJECT" --quiet
        fi
      fi ;;
    *) fail "looking up secret ${secret} returned HTTP ${code}" ;;
  esac
done

# ── 4. Neon branch (deleting it also deletes its endpoint, roles and databases) ────────────────────────
delete_neon_branch() {
  local id="$1" code i
  for i in $(seq 1 12); do
    code="$(call NEON_API_KEY DELETE "${NEON_API}/branches/${id}")"
    case "$code" in
      200 | 404) return 0 ;;
      423) echo "  branch busy (HTTP 423), retry ${i}/12 in 10 s"; sleep 10 ;;
      *) echo "  Neon returned HTTP ${code}"; return 1 ;;
    esac
  done
  return 1
}
echo "== Neon branch ${BRANCH}"
if [ -z "$NEON_API_KEY" ]; then echo "  not checked (no Neon credential)"; else
  code="$(call NEON_API_KEY GET "${NEON_API}/branches")"
  if [ "$code" != 200 ]; then fail "listing Neon branches returned HTTP ${code}"; else
    matches="$(jq -c --arg n "$BRANCH" '[.branches[]? | select(.name == $n)
      | {id, parent_id, default: (.default // false), current_state, created_at}]' "$tmp/body")"
    case "$(jq length <<<"$matches")" in
      0) echo "  absent" ;;
      1)
        id="$(jq -r '.[0].id' <<<"$matches")"
        jq -r --arg base "$NEON_PR_BASE_BRANCH_ID" '.[0] | "  present: \(.id), parent is pr-base: \(.parent_id == $base), state \(.current_state), created \(.created_at)"' <<<"$matches"
        if [ "$(call NEON_API_KEY GET "${NEON_API}/branches/${id}/endpoints")" = 200 ]; then
          jq -r '.endpoints[]? | "  endpoint \(.id)  \(.current_state)"' "$tmp/body"
        fi
        if untracked neon_branch.pr; then
          if [ "$(jq -r '.[0].parent_id' <<<"$matches")" != "$NEON_PR_BASE_BRANCH_ID" ] || [ "$(jq -r '.[0].default' <<<"$matches")" = true ]; then
            fail "Neon branch ${BRANCH} (${id}) is not a child of pr-base, or is the default branch; not deleting it."
          else remove "Neon branch ${BRANCH} (${id})" delete_neon_branch "$id"
          fi
        fi ;;
      *) fail "more than one Neon branch is named ${BRANCH}: $(jq -r '[.[].id] | join(", ")' <<<"$matches"); not deleting any." ;;
    esac
  fi
fi

if [ "$FULL" = true ]; then
  # ── 5. Stripe test webhook endpoints (metadata.pr=<n>, URL host must be this PR's Cloud Run) ───────────
  delete_stripe_endpoint() {
    case "$(call STRIPE_SECRET_KEY_TEST DELETE "https://api.stripe.com/v1/webhook_endpoints/$1")" in 200 | 404) ;; *) return 1 ;; esac
  }
  echo "== Stripe test webhook endpoints (metadata.pr=${PR_NUMBER})"
  if [ -z "$STRIPE_SECRET_KEY_TEST" ]; then echo "::notice::Stripe not checked (STRIPE_SECRET_KEY_TEST unset)."; else
    endpoints="[]" ok=true
    page_args=(-d limit=100)
    while :; do
      code="$(call STRIPE_SECRET_KEY_TEST GET "https://api.stripe.com/v1/webhook_endpoints" --get "${page_args[@]}")"
      if [ "$code" != 200 ]; then fail "listing Stripe webhook endpoints returned HTTP ${code}"; ok=false; break; fi
      endpoints="$(jq -c --argjson acc "$endpoints" --arg n "$PR_NUMBER" \
        '$acc + [.data[] | select(.metadata.pr == $n) | {id, host: (.url | split("/")[2])}]' "$tmp/body")"
      [ "$(jq -r .has_more "$tmp/body")" = true ] || break
      page_args=(-d limit=100 -d "starting_after=$(jq -r '.data[-1].id' "$tmp/body")")
    done
    if [ "$ok" = true ]; then
      [ "$endpoints" = "[]" ] && echo "  none"
      while IFS=$'\t' read -r we host; do
        [ -n "$we" ] || continue
        echo "  ${we}  host ${host}"
        if [[ "$host" != "${SERVICE}-"* ]]; then fail "Stripe endpoint ${we} has metadata.pr=${PR_NUMBER} but host ${host}; not deleting it."
        else remove "Stripe webhook endpoint ${we}" delete_stripe_endpoint "$we"
        fi
      done < <(jq -r '.[] | [.id, .host] | @tsv' <<<"$endpoints")
    fi
  fi

  # ── 6. Pages deployments on branch pr-<n> (every page is read before anything is deleted) ─────────────
  delete_pages_deployment() {
    [ "$(call CF_TOKEN DELETE "${CF_API}/pages/projects/${PAGES_PROJECT}/deployments/$1?force=true")" = 200 ]
  }
  echo "== Pages deployments on branch ${BRANCH} (project ${PAGES_PROJECT})"
  if [ -z "$CF_TOKEN" ]; then echo "  not checked (no Cloudflare credential)"; else
    deployments="" page=1 ok=true
    while :; do
      code="$(call CF_TOKEN GET "${CF_API}/pages/projects/${PAGES_PROJECT}/deployments" --get -d env=preview -d "page=${page}")"
      if [ "$code" != 200 ]; then fail "listing Pages deployments returned HTTP ${code}"; ok=false; break; fi
      deployments+="$(jq -r --arg b "$BRANCH" '.result[]? | select(.deployment_trigger.metadata.branch == $b) | .id' "$tmp/body")"$'\n'
      if [ "$page" -ge "$(jq -r '.result_info.total_pages // 1' "$tmp/body")" ] || [ "$page" -ge 100 ]; then break; fi
      page=$(( page + 1 ))
    done
    if [ "$ok" = true ]; then
      [ -z "${deployments//$'\n'/}" ] && echo "  none"
      while read -r dep; do
        [ -n "$dep" ] || continue
        remove "Pages deployment ${dep}" delete_pages_deployment "$dep"
      done <<<"$deployments"
    fi
  fi

  # ── 7. State prefix, every version: only when everything above is gone ──────────────────────────────────
  if [ "$state_objects" != "[]" ]; then
    echo "== State objects under gs://${STATE_BUCKET}/${STATE_PREFIX}"
    if [ "$failed" -ne 0 ]; then echo "  kept: an item above was refused or failed"; else
      while IFS=$'\t' read -r name gen; do
        remove "gs://${STATE_BUCKET}/${name}#${gen}" gcloud storage rm "gs://${STATE_BUCKET}/${name}#${gen}"
      done < <(jq -r '.[] | [.name, .generation] | @tsv' <<<"$state_objects")
    fi
  fi
fi

echo "== Summary"
if [ "${#done_items[@]}" -eq 0 ]; then echo "  nothing to delete"; else printf '  %s\n' "${done_items[@]}"; fi
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### pr-env reap: PR #${PR_NUMBER} (DRY_RUN=${DRY_RUN}, ONLY_UNTRACKED=${ONLY_UNTRACKED})"
    if [ "${#done_items[@]}" -eq 0 ]; then echo "- nothing to delete"; else printf -- '- %s\n' "${done_items[@]}"; fi
    [ "$failed" -eq 0 ] || echo "- **an item was refused or failed: see the log**"
  } >>"$GITHUB_STEP_SUMMARY"
fi
exit "$failed"
