#!/usr/bin/env bash
# Name-based orphan audit of the ephemeral per-PR environments (issue #236; handoff "Ops workflows to add"
# §2). Lists every per-PR resource by name and reports each PR that owns some but has no state/pr/<n>
# prefix: no tofu destroy, teardown or sweep will ever reclaim those (PR 217 after the 2026-10-26
# incident). Read-only. Reaping goes through pr-env-repair.yml action=reap.
#
#   Cloud Run service  nekohoa-api-pr-<n>     Secret Manager  pr-<n>-*
#   Neon branch        pr-<n>                 R2 bucket       nekohoa-pr-<n>-documents
#
# Not audited: Stripe test endpoints (draft PR #118 reconciles them by metadata.pr) and Pages deployments
# (free; never in state).
#
# A closed (or unknown) PR's orphans are an ::error:: and make the script exit 1. An open PR's are a
# ::warning:: only: its next pr-env run reaps untracked leftovers before `tofu apply`.
#
# Env (required): PROJECT REGION NEON_PROJECT_ID CF_ACCOUNT REPO; tokens NEON_API_KEY CF_TOKEN, GH_TOKEN
#                 for gh; gcloud logged in (WIF in CI).
# Env (optional): STATE_BUCKET (nekohoa-dev-tfstate).
# Output is names and PR numbers only: this public repo's Actions logs are world-readable. Tokens reach
# curl through a file descriptor, never argv.
set -euo pipefail

: "${PROJECT:?}" "${REGION:?}" "${NEON_PROJECT_ID:?}" "${CF_ACCOUNT:?}" "${REPO:?}" \
  "${NEON_API_KEY:?}" "${CF_TOKEN:?}"
STATE_BUCKET="${STATE_BUCKET:-nekohoa-dev-tfstate}"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
GCP_TOKEN="$(gcloud auth print-access-token 2>/dev/null || true)"
[ -n "$GCP_TOKEN" ] || { echo "::error::gcloud has no usable credential."; exit 1; }

# call VAR METHOD URL [curl args]: prints the HTTP status ("000" if unreachable); the body is in $tmp/body.
call() {
  local var="$1" method="$2" url="$3" code; shift 3
  code="$(curl -sS -o "$tmp/body" -w '%{http_code}' -X "$method" \
    -H @<(printf 'Authorization: Bearer %s\n' "${!var}") "$url" "$@")" || true
  echo "${code:-000}"
}
# list VAR URL JQ_ITEMS JQ_NEXT [PAGE_PARAM]: prints every item JQ_ITEMS selects, following the page token
# JQ_NEXT picks as query parameter PAGE_PARAM (default pageToken). A missing token, an empty page or the
# 100th page ends it. Returns 1 when a page is not HTTP 200.
list() {
  local var="$1" url="$2" items="$3" next="$4" param="${5:-pageToken}" token="" code sep page=0
  sep='?'; [[ "$url" == *\?* ]] && sep='&'
  while [ "$page" -lt 100 ]; do
    page=$(( page + 1 ))
    code="$(call "$var" GET "${url}${token:+${sep}${param}=${token}}")"
    if [ "$code" != 200 ]; then echo "::error::GET ${url%%\?*} returned HTTP ${code}." >&2; return 1; fi
    [ -n "$(jq -r "$items" "$tmp/body")" ] || return 0
    jq -r "$items" "$tmp/body"
    token="$(jq -r "($next) // empty" "$tmp/body")"
    [ -n "$token" ] || return 0
  done
}

GCS="https://storage.googleapis.com/storage/v1/b/${STATE_BUCKET}/o?prefix=state/pr/&delimiter=/"
RUN="https://run.googleapis.com/v2/projects/${PROJECT}/locations/${REGION}/services?pageSize=100"
SM="https://secretmanager.googleapis.com/v1/projects/${PROJECT}/secrets?pageSize=250"
NEON="https://console.neon.tech/api/v2/projects/${NEON_PROJECT_ID}/branches"
R2="https://api.cloudflare.com/client/v4/accounts/${CF_ACCOUNT}/r2/buckets?per_page=1000"

tracked="$(list GCP_TOKEN "$GCS" '.prefixes[]?' .nextPageToken | sed -nE 's#^state/pr/([0-9]+)/$#\1#p' | sort -u)"
# "<pr> <kind> <name>" for every per-PR resource, from the name patterns above.
found="$tmp/found"
{
  list GCP_TOKEN "$RUN" '.services[]?.name | split("/") | last' .nextPageToken \
    | sed -nE 's/^(nekohoa-api-pr-([0-9]+))$/\2 Cloud-Run-service \1/p'
  list GCP_TOKEN "$SM" '.secrets[]?.name | split("/") | last' .nextPageToken \
    | sed -nE 's/^(pr-([0-9]+)-.+)$/\2 secret \1/p'
  # One page, as pr-env-reap.sh reads it: Neon returns every branch of a project this size.
  list NEON_API_KEY "$NEON" '.branches[]?.name' null \
    | sed -nE 's/^(pr-([0-9]+))$/\2 Neon-branch \1/p'
  list CF_TOKEN "$R2" '.result.buckets[]?.name' .result_info.cursor cursor \
    | sed -nE 's/^(nekohoa-pr-([0-9]+)-documents)$/\2 R2-bucket \1/p'
} >"$found"

echo "State prefixes: $(echo "${tracked:-none}" | tr '\n' ' ')"
failed=0 orphans=0 summary=""
prs="$(cut -d' ' -f1 "$found" | sort -un)"
for n in $prs; do
  if grep -qxF "$n" <<<"$tracked"; then continue; fi
  orphans=$(( orphans + 1 ))
  state="$(gh api "repos/${REPO}/pulls/${n}" --jq .state 2>/dev/null || echo unknown)"
  echo "== PR #${n} (${state}): no state/pr/${n} prefix"
  awk -v n="$n" '$1 == n { k = $2; gsub("-", " ", k); print "  " k " " $3 }' "$found"
  count="$(awk -v n="$n" '$1 == n' "$found" | wc -l | tr -d ' ')"
  fix="gh workflow run pr-env-repair.yml -f action=reap -f pr_number=${n}"
  if [ "$state" = open ]; then
    echo "::warning::PR #${n} is open and has ${count} untracked per-PR resource(s); its next pr-env run reaps them."
  else
    echo "::error::PR #${n} is ${state} and has ${count} untracked per-PR resource(s). Reap them: ${fix}"
    failed=1
  fi
  summary+="- PR #${n} (${state}): ${count} untracked resource(s)"$'\n'
done
[ "$orphans" -gt 0 ] || echo "No orphans: every per-PR resource has a state prefix."

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### pr-env orphan audit"
    if [ -z "$summary" ]; then echo "- no orphans"; else printf '%s' "$summary"; fi
  } >>"$GITHUB_STEP_SUMMARY"
fi
exit "$failed"
