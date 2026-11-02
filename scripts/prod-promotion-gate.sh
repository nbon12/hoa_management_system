#!/usr/bin/env bash
# Automated gates for promoting a commit to Prod (spec 010 FR-026 as amended 2026-11-02, decision D2;
# docs/handoffs/2026-11-02-zero-touch-operations.md §5). Nothing calls this yet: Prod doesn't exist. The
# Prod promotion workflow runs it once infra/environments/prod is added (see the sketch at the bottom of
# .github/workflows/infra-apply.yml). No person approves a Prod promotion; these checks do.
#
# Modes (each exits 0 to pass, 1 to refuse, 2 on bad or missing input; a refusal names its gate):
#   switch  The Prod kill switch. Passes only when PROD_OPS_AUTOMATED is exactly "on" (case-sensitive,
#           unlike an Actions `==` expression). Unset or any other value refuses.
#   soak    The candidate commit SHA reached Staging at STAGING_APPLIED_AT (RFC 3339 UTC), at least
#           SOAK_HOURS (24) ago, and STAGING_FAILURES_SINCE (the caller's count of failed Staging apply,
#           smoke or E2E runs since then) is 0. Staging may have moved on to newer commits; those must
#           stay green too, which the failure count covers.
#   plan    PLAN_JSON is `tofu show -json <saved plan>` output. Refuses unless it is a valid plan
#           document, and refuses any managed change whose actions include "delete" on a stateful
#           resource type (read from `.type`, not parsed from the address).
#
# Env (optional): GITHUB_STEP_SUMMARY, AUDIT_FILE (refusals are appended for the ops-log report).
# Output is gate names, types and addresses only (public Actions logs).
set -euo pipefail

MODE="${1:-}"
# Losing one of these can't be undone by re-applying: data, secrets, every image (including the one a
# rollback needs), or the WIF identity CI uses to apply a fix (a deleted pool's ID stays reserved 30 days).
STATEFUL_TYPES='["neon_project","neon_branch","neon_database","neon_role","neon_endpoint",
  "cloudflare_r2_bucket","google_secret_manager_secret","google_storage_bucket",
  "google_artifact_registry_repository","google_iam_workload_identity_pool",
  "google_iam_workload_identity_pool_provider"]'

record() {
  for f in "${AUDIT_FILE:-}" "${GITHUB_STEP_SUMMARY:-}"; do
    if [ -n "$f" ]; then echo "- $*" >>"$f"; fi
  done
}
refuse() { echo "::error::Prod gate $1 refused: $2"; record "**Prod gate $1 refused:** $2"; exit 1; }
pass() { echo "$*"; record "$*"; }
# need VAR...: exit 2 (bad input, not a refusal) when a required variable is unset or empty.
need() { local v; for v in "$@"; do [ -n "${!v:-}" ] || { echo "::error::${v} is required for '${MODE}'." >&2; exit 2; }; done; }

case "$MODE" in
  switch)
    [ "${PROD_OPS_AUTOMATED:-}" = on ] \
      || refuse switch "repo variable PROD_OPS_AUTOMATED is '${PROD_OPS_AUTOMATED:-<unset>}', not 'on'; Prod promotion is off."
    pass "Prod gate switch passed: PROD_OPS_AUTOMATED=on." ;;

  soak)
    need SHA STAGING_APPLIED_AT STAGING_FAILURES_SINCE
    SOAK_HOURS="${SOAK_HOURS:-24}"
    for v in SOAK_HOURS STAGING_FAILURES_SINCE; do
      [[ "${!v}" =~ ^[0-9]+$ ]] || { echo "::error::${v} must be a whole number." >&2; exit 2; }
    done
    if ! applied="$(jq -rn --arg t "$STAGING_APPLIED_AT" '$t | sub("\\.[0-9]+Z$"; "Z") | fromdateiso8601' 2>/dev/null)"; then
      echo "::error::STAGING_APPLIED_AT must be an RFC 3339 UTC time." >&2; exit 2
    fi
    hours=$(( ( $(date -u +%s) - applied ) / 3600 ))
    [ "$hours" -ge "$SOAK_HOURS" ] || refuse soak "${SHA:0:12} reached Staging ${hours} h ago, under ${SOAK_HOURS} h."
    [ "$STAGING_FAILURES_SINCE" -eq 0 ] \
      || refuse soak "${STAGING_FAILURES_SINCE} Staging apply, smoke or E2E run(s) failed since ${SHA:0:12} reached Staging."
    pass "Prod gate soak passed: ${SHA:0:12} reached Staging ${hours} h ago, with no failed Staging run since." ;;

  plan)
    need PLAN_JSON
    [ -f "$PLAN_JSON" ] || refuse plan "no plan file, so the Prod change is unknown."
    jq -e 'type == "object" and has("format_version")' "$PLAN_JSON" >/dev/null 2>&1 \
      || refuse plan "${PLAN_JSON##*/} is not a tofu plan document (an empty or failed 'tofu show -json'?)."
    total="$(jq '[.resource_changes[]? | select(.mode == "managed") | select(.change.actions != ["no-op"])] | length' "$PLAN_JSON")"
    bad="$(jq -r --argjson stateful "$STATEFUL_TYPES" '.resource_changes[]? | select(.mode == "managed")
      | select(.change.actions | index("delete")) | select(.type as $t | $stateful | index($t))
      | "\(.change.actions | join("+")) \(.address)"' "$PLAN_JSON")"
    if [ -n "$bad" ]; then
      while IFS= read -r line; do echo "::error::Prod gate plan: '${line%% *}' on stateful ${line#* }."; done <<<"$bad"
      refuse plan "$(wc -l <<<"$bad" | tr -d ' ') planned change(s) would delete or replace a stateful resource."
    fi
    pass "Prod gate plan passed: ${total} change(s), none deletes or replaces a stateful resource." ;;

  *) echo "::error::usage: $0 switch|soak|plan" >&2; exit 2 ;;
esac
