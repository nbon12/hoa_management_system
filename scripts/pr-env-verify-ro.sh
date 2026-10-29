#!/usr/bin/env bash
# Verify the read-only (Tier 1) identities that pr-env-repair.yml's diagnose job runs as (issue #234;
# docs/handoffs/2026-10-26-agent-cloud-access.md, "Verification" 1-3 and 5-7). It runs where those
# credentials live, the diagnose job, because no session holds them. Every positive check must succeed
# and every negative check must be refused.
#
# Negative probes use names that do not exist, or reads whose response is never printed (and deleted at
# once), so a wrongly scoped key changes nothing and no value reaches the log. A 403 caused by an API
# that is off in the project (SERVICE_DISABLED) proves nothing, so it is "inconclusive", not a pass. The one exception is `secrets create
# agent-ro-probe`: if it succeeds, the key is over-scoped and the operator deletes that secret.
#
# Env (required): PROJECT REGION PREFIX CF_ACCOUNT; gcloud logged in as the read-only SA (WIF in CI).
# Env (optional): CF_TOKEN STRIPE_KEY NEON_API_KEY NEON_PROJECT_ID (a missing token's checks are "not
#                 checked"); STATE_BUCKET (nekohoa-dev-tfstate).
# Exit: 1 when any check FAILs; "inconclusive" (a probe target is missing, so the probe proves nothing)
# does not fail the run. Output is check names and HTTP statuses only: Actions logs are world-readable.
# Tokens reach curl through a file descriptor, never argv.
set -euo pipefail

: "${PROJECT:?}" "${REGION:?}" "${PREFIX:?}" "${CF_ACCOUNT:?}"
STATE_BUCKET="${STATE_BUCKET:-nekohoa-dev-tfstate}"
CF_TOKEN="${CF_TOKEN:-}" STRIPE_KEY="${STRIPE_KEY:-}" NEON_API_KEY="${NEON_API_KEY:-}"
NEON_PROJECT_ID="${NEON_PROJECT_ID:-}"
PAGES_PROJECT="nekohoa-dev"
CF_API="https://api.cloudflare.com/client/v4"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
failed=0 passed=0 skipped=0

_hdr() { if [ -n "${!1:-}" ]; then printf 'Authorization: Bearer %s\n' "${!1}"; fi; }
# call VAR METHOD URL [curl args]: prints the HTTP status ("000" if unreachable); the body is in $tmp/body.
call() {
  local var="$1" method="$2" url="$3" code; shift 3
  code="$(curl -sS -o "$tmp/body" -w '%{http_code}' -X "$method" -H @<(_hdr "$var") "$url" "$@")" || true
  echo "${code:-000}"
}
# probe VAR METHOD URL [curl args]: like call, but the body is never printed: a check that wrongly succeeds
# must not show what it read. Only a 403 body is looked at, for Google's SERVICE_DISABLED: a disabled API
# refuses everyone, so that 403 proves nothing about the credential ("disabled"). The body is deleted at once.
probe() {
  local var="$1" method="$2" url="$3" code; shift 3
  code="$(curl -sS -o "$tmp/probe" -w '%{http_code}' -X "$method" -H @<(_hdr "$var") "$url" "$@")" || true
  if [ "${code:-000}" = 403 ] && grep -q SERVICE_DISABLED "$tmp/probe" 2>/dev/null; then code=disabled; fi
  rm -f "$tmp/probe"
  echo "${code:-000}"
}
pass() { echo "  PASS  $*"; passed=$((passed + 1)); }
bad() { echo "::error::FAIL  $*"; failed=1; }
unsure() { echo "::warning::inconclusive  $*"; }
skip() { echo "  not checked  $*"; skipped=$((skipped + 1)); }
# expect WANT CODE NAME: a positive check passes on WANT (200).
expect() { if [ "$2" = "$1" ]; then pass "$3 (HTTP $2)"; else bad "$3: HTTP $2, expected $1"; fi; }
# denied CODE NAME [MISSING_CODE]: a negative check passes on 401/403. MISSING_CODE (404 by default) means
# the probe's target does not exist, which proves nothing; anything else means the request got through.
denied() {
  case "$1" in
    401 | 403) pass "$2 is refused (HTTP $1)" ;;
    disabled) unsure "$2: HTTP 403 SERVICE_DISABLED (the API is off in this project, so the refusal is unproven)" ;;
    "${3:-404}") unsure "$2: HTTP $1 (the probe target does not exist, so the refusal is unproven)" ;;
    *) bad "$2 was NOT refused (HTTP $1): the credential is over-scoped" ;;
  esac
}

# ── GCP: nekohoa-agent-ro-dev (Verification 2 and 3) ─────────────────────────────────────────────────────
echo "== GCP (read-only service account)"
GCP_TOKEN="$(gcloud auth print-access-token 2>/dev/null || true)"
if [ -z "$GCP_TOKEN" ]; then skip "GCP: gcloud has no usable credential"; else
  RUN="https://run.googleapis.com/v2/projects/${PROJECT}/locations/${REGION}"
  SM="https://secretmanager.googleapis.com/v1/projects/${PROJECT}"
  LOG="https://logging.googleapis.com/v2/entries:list"
  GCS="https://storage.googleapis.com/storage/v1/b/${STATE_BUCKET}/o"

  expect 200 "$(probe GCP_TOKEN GET "${RUN}/services?pageSize=1")" "list Cloud Run services"
  expect 200 "$(probe GCP_TOKEN GET "${SM}/secrets?pageSize=1")" "list secrets (names only)"
  # entries_query FILTER [PAGE_TOKEN]: an entries:list request body for this project.
  entries_query() {
    jq -nc --arg p "projects/${PROJECT}" --arg f "$1" --arg t "${2:-}" \
      '{resourceNames: [$p], filter: $f, pageSize: 1000} + (if $t == "" then {} else {pageToken: $t} end)'
  }
  expect 200 "$(probe GCP_TOKEN POST "$LOG" -H 'Content-Type: application/json' \
    -d "$(entries_query 'resource.type="cloud_run_revision"')")" "read Cloud Logging"
  expect 200 "$(probe GCP_TOKEN GET "$GCS" --get -d prefix=state/ -d maxResults=1)" "list the state bucket"

  denied "$(probe GCP_TOKEN GET "${SM}/secrets/${PREFIX}-jwt-secret/versions/latest:access")" \
    "read the value of secret ${PREFIX}-jwt-secret"
  denied "$(probe GCP_TOKEN GET "${GCS}/state%2F${PREFIX}%2Fdefault.tfstate?alt=media")" \
    "read state/${PREFIX}/default.tfstate"
  denied "$(probe GCP_TOKEN GET \
    "https://cloudscheduler.googleapis.com/v1/projects/${PROJECT}/locations/${REGION}/jobs/nekohoa-arc-sweep-${PREFIX}")" \
    "describe Cloud Scheduler job nekohoa-arc-sweep-${PREFIX}"
  code="$(probe GCP_TOKEN POST "${SM}/secrets?secretId=agent-ro-probe" -H 'Content-Type: application/json' \
    -d '{"replication":{"automatic":{}},"labels":{"agent-ro-probe":"true"}}')"
  denied "$code" "create secret agent-ro-probe" none
  case "$code" in 200 | 409) echo "::error::Secret agent-ro-probe exists. Delete it: gcloud secrets delete agent-ro-probe --project ${PROJECT}" ;; esac

  # The scheduler's shared secret is a request header on its jobs. Admin Activity audit logs (readable with
  # roles/logging.viewer) must not carry it in a request body. Counts field paths only, never values.
  since="$(date -u -d '400 days ago' +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -v-400d +%Y-%m-%dT%H:%M:%SZ)"
  filter="protoPayload.serviceName=\"cloudscheduler.googleapis.com\" AND timestamp>=\"${since}\""
  entries=0 header_paths=0 token="" ok=true
  for _ in 1 2 3 4 5 6 7 8 9 10; do
    code="$(call GCP_TOKEN POST "$LOG" -H 'Content-Type: application/json' -d "$(entries_query "$filter" "$token")")"
    if [ "$code" != 200 ]; then bad "read Cloud Scheduler audit logs: HTTP ${code}"; ok=false; break; fi
    entries=$(( entries + $(jq '.entries // [] | length' "$tmp/body") ))
    header_paths=$(( header_paths + $(jq '[.entries // [] | .[] | paths | map(tostring) | join(".") | ascii_downcase | select(test("headers"))] | length' "$tmp/body") ))
    token="$(jq -r '.nextPageToken // empty' "$tmp/body")"
    [ -n "$token" ] || break
  done
  rm -f "$tmp/body"
  if [ "$ok" = true ]; then
    if [ "$entries" -eq 0 ]; then unsure "no Cloud Scheduler audit-log entries in 400 days (no jobs yet), so there was nothing to inspect"
    elif [ "$header_paths" -eq 0 ]; then pass "no request headers in ${entries} Cloud Scheduler audit-log entries (400 days)"
    else bad "${header_paths} header field path(s) in Cloud Scheduler audit logs: logging.viewer may read the scheduler secret. Rotate it and drop roles/logging.viewer (handoff Verification 3)."
    fi
  fi
fi

# ── Cloudflare: claude-agent-ro (Verification 6) ─────────────────────────────────────────────────────────
echo "== Cloudflare (claude-agent-ro)"
if [ -z "$CF_TOKEN" ]; then skip "Cloudflare: no CF_TOKEN"; else
  # A user token verifies at /user/tokens/verify, an account token at /accounts/<id>/tokens/verify.
  status="" expires=""
  for path in "/user/tokens/verify" "/accounts/${CF_ACCOUNT}/tokens/verify"; do
    if [ "$(call CF_TOKEN GET "${CF_API}${path}")" = 200 ]; then
      status="$(jq -r '.result.status // ""' "$tmp/body")"; expires="$(jq -r '.result.expires_on // "never"' "$tmp/body")"
      break
    fi
  done
  if [ "$status" = active ]; then pass "token is active (expires ${expires})"; else bad "token did not verify as active"; fi
  expect 200 "$(probe CF_TOKEN GET "${CF_API}/accounts/${CF_ACCOUNT}/r2/buckets?per_page=1")" "list R2 buckets"
  expect 200 "$(probe CF_TOKEN GET "${CF_API}/accounts/${CF_ACCOUNT}/pages/projects/${PAGES_PROJECT}/deployments?per_page=1")" \
    "list Pages deployments"
  # Cloudflare answers a refused write with 403 (code 10000). "Bucket does not exist" (10006) means the
  # request passed the permission check, so it counts as over-scoped here.
  code="$(call CF_TOKEN DELETE "${CF_API}/accounts/${CF_ACCOUNT}/r2/buckets/agent-ro-probe")"
  if jq -e '[.errors[]?.code] | index(10006)' "$tmp/body" >/dev/null 2>&1; then code=200; fi
  denied "$code" "delete R2 bucket agent-ro-probe" none
fi

# ── Stripe (test mode): claude-agent-ro (Verification 7) ─────────────────────────────────────────────────
echo "== Stripe test mode (claude-agent-ro)"
if [ -z "$STRIPE_KEY" ]; then skip "Stripe: no STRIPE_KEY"; else
  case "$STRIPE_KEY" in
    rk_test_*) pass "key is a restricted test-mode key (rk_test_ prefix)" ;;
    *) bad "key is not a restricted test-mode key (expected the rk_test_ prefix)" ;;
  esac
  expect 200 "$(probe STRIPE_KEY GET 'https://api.stripe.com/v1/webhook_endpoints?limit=1')" "list webhook endpoints"
  denied "$(probe STRIPE_KEY GET 'https://api.stripe.com/v1/customers?limit=1')" "list customers" none
  # Stripe checks a restricted key's permissions before the object exists, so 404 means write access.
  denied "$(probe STRIPE_KEY DELETE 'https://api.stripe.com/v1/webhook_endpoints/we_agentprobe')" \
    "delete webhook endpoint we_agentprobe" none
fi

# ── Neon (Verification 5) ──────────────────────────────────────────────────────────────────────────────
echo "== Neon"
if [ -z "$NEON_API_KEY" ] || [ -z "$NEON_PROJECT_ID" ]; then skip "Neon: no read-only Neon key (handoff O6)"; else
  NEON="https://console.neon.tech/api/v2/projects/${NEON_PROJECT_ID}"
  expect 200 "$(probe NEON_API_KEY GET "${NEON}/branches")" "list branches"
  denied "$(probe NEON_API_KEY DELETE "${NEON}/branches/br-agent-probe")" "delete branch br-agent-probe" none
fi

echo "== Summary"
echo "  ${passed} passed, ${skipped} not checked$([ "$failed" -eq 0 ] && echo ", none failed" || echo ", FAILURES above")"
exit "$failed"
