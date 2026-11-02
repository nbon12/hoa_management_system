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

# ── soak: "Staging applied with the same commit and green for at least 24 h"
t "soak: the same commit on Staging for 25 h passes"; f0=$fail
gate soak SHA="$SHA" STAGING_SHA="$SHA" STAGING_APPLIED_AT="$DAY_AGO"
exit_is 0; out_has "Prod gate soak passed"
dump_on_fail "$f0"

t "soak: under 24 h, or another commit on Staging, refuses"; f0=$fail
gate soak SHA="$SHA" STAGING_SHA="$SHA" STAGING_APPLIED_AT="$HOUR_AGO"
exit_is 1; out_has "Prod gate soak refused: Staging has run 0123456789ab for 1 h, under 24 h."
gate soak SHA="$SHA" STAGING_SHA=fedcba9876543210fedcba9876543210fedcba98 STAGING_APPLIED_AT="$DAY_AGO"
exit_is 1; out_has "Prod gate soak refused: Staging runs fedcba987654, not 0123456789ab."
dump_on_fail "$f0"

t "soak: bad input is rejected"; f0=$fail
gate soak SHA="$SHA" STAGING_SHA="$SHA" STAGING_APPLIED_AT=yesterday
exit_is 2
gate soak SHA="$SHA" STAGING_SHA="$SHA" STAGING_APPLIED_AT="$DAY_AGO" SOAK_HOURS=a-day
exit_is 2
dump_on_fail "$f0"

# ── plan: "no destroy or replace on stateful resources (Neon project or branch, R2 buckets, secrets)"
t "plan: creates, updates and deletes of stateless resources pass"; f0=$fail
printf '%s\n' "update module.environment.google_cloud_run_v2_service.api" \
  "create module.environment.google_secret_manager_secret.jwt" \
  "delete module.environment.google_cloud_run_v2_service_iam_member.invoker" \
  "delete+create module.environment.google_cloud_scheduler_job.arc_sweep" >"$TMP/plan"
gate plan PLAN_FILE="$TMP/plan"
exit_is 0; out_has "Prod gate plan passed: 4 change(s)"
dump_on_fail "$f0"

t "plan: a delete or replace of any stateful type refuses"; f0=$fail
for line in "delete module.environment.neon_branch.main" "delete+create neon_project.prod" \
  "create+delete module.environment.cloudflare_r2_bucket.documents" \
  "delete module.environment.google_secret_manager_secret.db_connection" \
  "delete module.environment.neon_role.app" "delete module.environment.neon_database.app" \
  'delete module.environment.google_secret_manager_secret.s["stripe.key"]' \
  "delete google_storage_bucket.tfstate"; do
  echo "$line" >"$TMP/plan"
  gate plan PLAN_FILE="$TMP/plan"
  exit_is 1; out_has "Prod gate plan refused: 1 planned change(s) would delete or replace a stateful resource."
done
dump_on_fail "$f0"

t "plan: a missing plan file refuses; an empty one passes"; f0=$fail
gate plan PLAN_FILE="$TMP/none"
exit_is 1; out_has "no plan file"
: >"$TMP/plan"; gate plan PLAN_FILE="$TMP/plan"
exit_is 0
dump_on_fail "$f0"

t "bad mode is rejected"; f0=$fail
gate apply
exit_is 2
dump_on_fail "$f0"

echo "prod promotion gate tests: ${pass} passed, ${fail} failed"
[ "$fail" -eq 0 ]
