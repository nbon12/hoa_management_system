#!/usr/bin/env bash
# Tests for scripts/prod-promotion-gate.sh (spec 010 FR-026 and acceptance scenario 2, as amended
# 2026-11-02). No network: every mode reads only its environment and files.
# Run: bash scripts/tests/prod-promotion-gate.test.sh
# shellcheck disable=SC2015 # `check && ok || bad` is the assertion idiom; ok cannot fail
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
pass=0 fail=0 CASE=""

# gate MODE [VAR=value ...]: runs the gate with only the given environment.
gate() {
  local mode="$1"; shift
  env -i PATH="$PATH" HOME="$HOME" "$@" bash "$ROOT/scripts/prod-promotion-gate.sh" "$mode" >"$TMP/out" 2>&1
  echo $? >"$TMP/rc"
}
ok() { pass=$((pass + 1)); }
bad() { fail=$((fail + 1)); echo "not ok - ${CASE}: $*"; }
exit_is() { [ "$(cat "$TMP/rc")" = "$1" ] && ok || bad "exit $(cat "$TMP/rc"), expected $1"; }
out_has() { grep -qF -- "$1" "$TMP/out" && ok || bad "output lacks '$1'"; }
dump_on_fail() { [ "$fail" -eq "$1" ] || sed 's/^/    | /' "$TMP/out"; }
t() { CASE="$1"; }

SHA=0123456789abcdef0123456789abcdef01234567
DAY_AGO="$(jq -rn 'now - 90000 | todate')"   # 25 h
HOUR_AGO="$(jq -rn 'now - 3600 | todate')"

# ── switch: acceptance scenario 2, "only while PROD_OPS_AUTOMATED is on … otherwise it skips … reports why"
t "switch: exactly 'on' passes"; f0=$fail
gate switch PROD_OPS_AUTOMATED=on
exit_is 0; out_has "Prod gate switch passed"
dump_on_fail "$f0"

t "switch: unset, off, or another case of 'on' refuses and says why"; f0=$fail
gate switch
exit_is 1; out_has "Prod gate switch refused: repo variable PROD_OPS_AUTOMATED is '<unset>', not 'on'"
for v in off ON On true 1 ""; do
  gate switch PROD_OPS_AUTOMATED="$v"
  exit_is 1; out_has "Prod gate switch refused"
done
dump_on_fail "$f0"

t "switch: a refusal is recorded for the ops-log report"; f0=$fail
gate switch PROD_OPS_AUTOMATED=off AUDIT_FILE="$TMP/audit"
exit_is 1; grep -qF "**Prod gate switch refused:**" "$TMP/audit" && ok || bad "refusal not in AUDIT_FILE"
rm -f "$TMP/audit"
dump_on_fail "$f0"

# ── soak: "the candidate reached Staging ≥ 24 h ago and Staging has stayed green since"
t "soak: 25 h on Staging with no failed run since passes, even if Staging moved on"; f0=$fail
gate soak SHA="$SHA" STAGING_APPLIED_AT="$DAY_AGO" STAGING_FAILURES_SINCE=0
exit_is 0; out_has "Prod gate soak passed: 0123456789ab reached Staging 25 h ago, with no failed Staging run since."
dump_on_fail "$f0"

t "soak: under 24 h, or a failed Staging run since, refuses"; f0=$fail
gate soak SHA="$SHA" STAGING_APPLIED_AT="$HOUR_AGO" STAGING_FAILURES_SINCE=0
exit_is 1; out_has "Prod gate soak refused: 0123456789ab reached Staging 1 h ago, under 24 h."
gate soak SHA="$SHA" STAGING_APPLIED_AT="$DAY_AGO" STAGING_FAILURES_SINCE=2
exit_is 1; out_has "Prod gate soak refused: 2 Staging apply, smoke or E2E run(s) failed since 0123456789ab reached Staging."
dump_on_fail "$f0"

t "soak: bad or missing input exits 2, not a refusal"; f0=$fail
gate soak SHA="$SHA" STAGING_APPLIED_AT=yesterday STAGING_FAILURES_SINCE=0
exit_is 2
gate soak SHA="$SHA" STAGING_APPLIED_AT="$DAY_AGO" STAGING_FAILURES_SINCE=0 SOAK_HOURS=a-day
exit_is 2
gate soak SHA="$SHA" STAGING_APPLIED_AT="$DAY_AGO" STAGING_FAILURES_SINCE=some
exit_is 2
gate soak SHA="$SHA" STAGING_APPLIED_AT="$DAY_AGO"
exit_is 2; out_has "STAGING_FAILURES_SINCE is required for 'soak'."
gate soak STAGING_APPLIED_AT="$DAY_AGO" STAGING_FAILURES_SINCE=0
exit_is 2; out_has "SHA is required"
dump_on_fail "$f0"

# ── plan: "no destroy or replace on stateful resources". PLAN_JSON is `tofu show -json` output.
# plan_json "ACTIONS|ADDRESS|TYPE" ...: a plan document with those managed changes.
plan_json() {
  local rc="" sep="" a addr ty
  for c in "$@"; do
    IFS='|' read -r a addr ty <<<"$c"
    rc+="${sep}$(jq -cn --arg a "$a" --arg addr "$addr" --arg ty "$ty" \
      '{address: $addr, mode: "managed", type: $ty, change: {actions: ($a | split("+"))}}')"
    sep=","
  done
  printf '{"format_version":"1.2","resource_changes":[%s]}' "$rc" >"$TMP/plan.json"
}
t "plan: creates, updates, and deletes or replaces of stateless resources pass"; f0=$fail
plan_json "update|module.environment.google_cloud_run_v2_service.api|google_cloud_run_v2_service" \
  "create|module.environment.google_secret_manager_secret.jwt|google_secret_manager_secret" \
  "delete|module.environment.google_cloud_run_v2_service_iam_member.invoker|google_cloud_run_v2_service_iam_member" \
  "delete+create|module.environment.google_cloud_scheduler_job.arc_sweep|google_cloud_scheduler_job" \
  "no-op|module.environment.neon_project.this|neon_project"
gate plan PLAN_JSON="$TMP/plan.json"
exit_is 0; out_has "Prod gate plan passed: 4 change(s)"
dump_on_fail "$f0"

t "plan: a delete or replace of any stateful type refuses, whatever the address looks like"; f0=$fail
for c in "delete|module.environment.neon_branch.main|neon_branch" "delete+create|neon_project.prod|neon_project" \
  "create+delete|module.environment.cloudflare_r2_bucket.documents|cloudflare_r2_bucket" \
  "delete|module.environment.google_secret_manager_secret.db_connection|google_secret_manager_secret" \
  "delete|module.environment.neon_role.app|neon_role" "delete|module.environment.neon_database.app|neon_database" \
  'delete|module.environment.google_secret_manager_secret.s["stripe.key"]|google_secret_manager_secret' \
  'delete|module.environment["prod"].neon_project.main|neon_project' \
  "delete|module.environment.google_artifact_registry_repository.images|google_artifact_registry_repository" \
  "delete|module.environment.google_iam_workload_identity_pool.github|google_iam_workload_identity_pool" \
  "delete|module.environment.google_iam_workload_identity_pool_provider.github|google_iam_workload_identity_pool_provider" \
  "delete|google_storage_bucket.tfstate|google_storage_bucket"; do
  plan_json "update|a.b|google_cloud_run_v2_service" "$c"
  gate plan PLAN_JSON="$TMP/plan.json"
  exit_is 1; out_has "Prod gate plan refused: 1 planned change(s) would delete or replace a stateful resource."
done
dump_on_fail "$f0"

t "plan: a missing, empty or non-plan file refuses; a valid plan with no changes passes"; f0=$fail
gate plan PLAN_JSON="$TMP/none.json"
exit_is 1; out_has "no plan file"
: >"$TMP/plan.json"; gate plan PLAN_JSON="$TMP/plan.json"
exit_is 1; out_has "is not a tofu plan document"
echo '{"errors":["boom"]}' >"$TMP/plan.json"; gate plan PLAN_JSON="$TMP/plan.json"
exit_is 1; out_has "is not a tofu plan document"
echo '{"format_version":"1.2"}' >"$TMP/plan.json"; gate plan PLAN_JSON="$TMP/plan.json"
exit_is 0; out_has "0 change(s)"
gate plan
exit_is 2; out_has "PLAN_JSON is required"
dump_on_fail "$f0"

t "bad mode is rejected"; f0=$fail
gate apply
exit_is 2
dump_on_fail "$f0"

echo "prod promotion gate tests: ${pass} passed, ${fail} failed"
[ "$fail" -eq 0 ]
