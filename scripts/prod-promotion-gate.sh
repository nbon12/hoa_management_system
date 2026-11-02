#!/usr/bin/env bash
# Automated gates for promoting a commit to Prod (spec 010 FR-026 as amended 2026-11-02, decision D2;
# docs/handoffs/2026-11-02-zero-touch-operations.md §5). Nothing calls this yet: Prod doesn't exist. The
# Prod promotion workflow runs it once infra/environments/prod is added (see the commented job in
# .github/workflows/infra-apply.yml). No person approves a Prod promotion; these checks do.
#
# Modes (each exits 0 to pass, 1 to refuse, 2 on bad input; a refusal names its gate):
#   switch  The Prod kill switch. Passes only when PROD_OPS_AUTOMATED is exactly "on" (case-sensitive,
#           unlike an Actions `==` expression). Unset or any other value refuses.
#   soak    Staging soaked the same commit: STAGING_SHA must equal SHA, and STAGING_APPLIED_AT (RFC 3339
#           UTC) must be at least SOAK_HOURS (24) ago.
#   plan    The Prod plan never deletes or replaces a stateful resource. PLAN_FILE holds one
#           "<actions> <address>" line per resource change, as `tofu show -json` gives them (actions joined
#           with "+", e.g. "delete+create"). Any change whose actions include "delete" on an address of
#           a stateful type refuses.
#
# Env (optional): GITHUB_STEP_SUMMARY, AUDIT_FILE (refusals are appended for the ops-log report).
# Output is gate names, types and addresses only (public Actions logs).
set -euo pipefail

MODE="${1:-}"
STATEFUL_TYPES=(neon_project neon_branch neon_database neon_role neon_endpoint cloudflare_r2_bucket
  google_secret_manager_secret google_storage_bucket)

record() {
  for f in "${AUDIT_FILE:-}" "${GITHUB_STEP_SUMMARY:-}"; do
    if [ -n "$f" ]; then echo "- $*" >>"$f"; fi
  done
}
refuse() { echo "::error::Prod gate $1 refused: $2"; record "**Prod gate $1 refused:** $2"; exit 1; }
pass() { echo "$*"; record "$*"; }

case "$MODE" in
  switch)
    [ "${PROD_OPS_AUTOMATED:-}" = on ] \
      || refuse switch "repo variable PROD_OPS_AUTOMATED is '${PROD_OPS_AUTOMATED:-<unset>}', not 'on'; Prod promotion is off."
    pass "Prod gate switch passed: PROD_OPS_AUTOMATED=on." ;;

  soak)
    : "${SHA:?}" "${STAGING_SHA:?}" "${STAGING_APPLIED_AT:?}"
    SOAK_HOURS="${SOAK_HOURS:-24}"
    [[ "$SOAK_HOURS" =~ ^[0-9]+$ ]] || { echo "::error::SOAK_HOURS must be a whole number." >&2; exit 2; }
    [ "$STAGING_SHA" = "$SHA" ] || refuse soak "Staging runs ${STAGING_SHA:0:12}, not ${SHA:0:12}."
    if ! applied="$(jq -rn --arg t "$STAGING_APPLIED_AT" '$t | sub("\\.[0-9]+Z$"; "Z") | fromdateiso8601' 2>/dev/null)"; then
      echo "::error::STAGING_APPLIED_AT must be an RFC 3339 UTC time." >&2; exit 2
    fi
    hours=$(( ( $(date -u +%s) - applied ) / 3600 ))
    [ "$hours" -ge "$SOAK_HOURS" ] || refuse soak "Staging has run ${SHA:0:12} for ${hours} h, under ${SOAK_HOURS} h."
    pass "Prod gate soak passed: Staging has run ${SHA:0:12} for ${hours} h." ;;

  plan)
    : "${PLAN_FILE:?}"
    [ -f "$PLAN_FILE" ] || refuse plan "no plan file, so the Prod change is unknown."
    bad=0 total=0
    while read -r actions address; do
      [ -n "${actions:-}" ] || continue
      total=$(( total + 1 ))
      [[ "+${actions}+" == *+delete+* ]] || continue
      base="${address%%\[*}"; rest="${base%.*}"; type="${rest##*.}" # [module.x.]<type>.<name>[key] → <type>
      for t in "${STATEFUL_TYPES[@]}"; do
        if [ "$type" = "$t" ]; then
          echo "::error::Prod gate plan: '${actions}' on stateful ${address}."
          bad=$(( bad + 1 ))
        fi
      done
    done <"$PLAN_FILE"
    [ "$bad" -eq 0 ] || refuse plan "${bad} planned change(s) would delete or replace a stateful resource."
    pass "Prod gate plan passed: ${total} change(s), none deletes or replaces a stateful resource." ;;

  *) echo "::error::usage: $0 switch|soak|plan" >&2; exit 2 ;;
esac
