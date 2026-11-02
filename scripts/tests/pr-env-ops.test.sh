#!/usr/bin/env bash
# Tests for scripts/pr-env-clear-stale-lock.sh, scripts/pr-env-audit.sh, scripts/pr-env-policy-gate.sh and
# scripts/stripe-webhook-register.sh against stubbed curl, gcloud and gh (no network, no cloud). Same harness as pr-env-reap.test.sh: each case writes the HTTP routes the stub
# serves, runs a script, and checks its exit code, its output and the calls it made.
# Run: bash scripts/tests/pr-env-ops.test.sh
# shellcheck disable=SC2015 # `check && ok || bad` is the assertion idiom; ok cannot fail
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
STUB_DIR="$(mktemp -d)"
trap 'rm -rf "$STUB_DIR"' EXIT
mkdir -p "$STUB_DIR/bin"
pass=0 fail=0

# ── Stubs ─────────────────────────────────────────────────────────────────────────────────────────────
# curl: serves the first route in $STUB_DIR/routes whose method matches and whose regex matches the URL;
# unmatched → 404 {}. Logs "curl METHOD URL auth=yes|no" to calls and its raw argv to argv.
cat >"$STUB_DIR/bin/curl" <<'EOF'
#!/usr/bin/env bash
echo "curl $*" >>"$STUB_DIR/argv"
method=GET out=/dev/stdout url="" auth=no w=no
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2 ;;
    -o) out="$2"; shift 2 ;;
    -w) w=yes; shift 2 ;;
    -H) if [[ "$2" == @* ]] && grep -q '^Authorization: Bearer ' "${2#@}"; then auth=yes; fi; shift 2 ;;
    http*) url="$1"; shift ;;
    *) shift ;;
  esac
done
echo "curl ${method} ${url} auth=${auth}" >>"$STUB_DIR/calls"
code=404 body='{}'
while IFS=$'\t' read -r m re c b; do
  if [ "$m" = "$method" ] && [[ "$url" =~ $re ]]; then code="$c" body="$b"; break; fi
done <"$STUB_DIR/routes"
printf '%s' "$body" >"$out"
# The status code is printed only for -w (scripts that read it); otherwise curl prints just the body.
if [ "$w" = yes ]; then printf '%s' "$code"; fi

EOF
cat >"$STUB_DIR/bin/gcloud" <<'EOF'
#!/usr/bin/env bash
echo "gcloud $*" >>"$STUB_DIR/calls"
case "$1 $2" in
  "auth print-access-token") [ -f "$STUB_DIR/gcloud_noauth" ] && exit 1; echo gcp-test-token ;;
esac
EOF
# gh: `run list --workflow <w> --jq <expr>` applies the real --jq to $STUB_DIR/runs_<w> (default []), so
# the script's filter is tested too; fails when gh_fail exists (or runs_fail, for `run list` only).
# `api repos/.../pulls/<n>` applies the real --jq to $STUB_DIR/pr_<n>.json when it exists, else prints
# $STUB_DIR/pr_<n> (default "closed"). `api .../collaborators/<u>/permission` prints $STUB_DIR/perm
# (default "write").
cat >"$STUB_DIR/bin/gh" <<'EOF'
#!/usr/bin/env bash
echo "gh $*" >>"$STUB_DIR/calls"
[ -f "$STUB_DIR/gh_fail" ] && exit 1
q="."
for ((i = 1; i < $#; i++)); do [ "${!i}" = --jq ] && { j=$((i + 1)); q="${!j}"; }; done
case "$1 $2" in
  "run list")
    [ -f "$STUB_DIR/runs_fail" ] && exit 1
    w=""
    while [ $# -gt 0 ]; do case "$1" in --workflow) w="$2"; shift 2 ;; *) shift ;; esac; done
    jq -r "$q" "$STUB_DIR/runs_${w}" 2>/dev/null || jq -rn "[] | $q" ;;
  api*/permission) cat "$STUB_DIR/perm" 2>/dev/null || echo write ;;
  api*)
    n="${2##*/}"
    if [ -f "$STUB_DIR/pr_${n}.json" ]; then jq -r "$q" "$STUB_DIR/pr_${n}.json"
    else cat "$STUB_DIR/pr_${n}" 2>/dev/null || echo closed; fi ;;
esac
EOF
chmod +x "$STUB_DIR/bin/"*

# ── Harness ───────────────────────────────────────────────────────────────────────────────────────────
route() { printf '%s\t%s\t%s\t%s\n' "$@" >>"$STUB_DIR/routes"; }
reset() {
  rm -f "$STUB_DIR"/{routes,calls,argv,gh_fail,gcloud_noauth,perm,plan} "$STUB_DIR"/pr_* "$STUB_DIR"/runs_*
  touch "$STUB_DIR/routes" "$STUB_DIR/calls" "$STUB_DIR/argv"
}
# run SCRIPT [VAR=value ...] [-- ARG ...]: runs SCRIPT with only the given environment, then ARGs.
run() {
  local script="$1" envs=() args=(); shift
  while [ $# -gt 0 ]; do
    if [ "$1" = -- ]; then shift; args=("$@"); break; fi
    envs+=("$1"); shift
  done
  env -i PATH="$STUB_DIR/bin:$PATH" HOME="$HOME" TMPDIR="${TMPDIR:-/tmp}" STUB_DIR="$STUB_DIR" \
    REPO=nbon12/hoa_management_system "${envs[@]}" bash "$ROOT/scripts/${script}" "${args[@]}" >"$STUB_DIR/out" 2>&1
  echo $? >"$STUB_DIR/rc"
}
CASE=""
ok() { pass=$((pass + 1)); }
bad() { fail=$((fail + 1)); echo "not ok - ${CASE}: $*"; }
exit_is() { [ "$(cat "$STUB_DIR/rc")" = "$1" ] && ok || bad "exit $(cat "$STUB_DIR/rc"), expected $1"; }
out_has() { grep -qF -- "$1" "$STUB_DIR/out" && ok || bad "output lacks '$1'"; }
out_lacks() { grep -qF -- "$1" "$STUB_DIR/out" && bad "output has '$1'" || ok; }
called() { grep -qE -- "$1" "$STUB_DIR/calls" && ok || bad "no call matching /$1/"; }
not_called() { grep -qE -- "$1" "$STUB_DIR/calls" && bad "unexpected call matching /$1/" || ok; }
dump_on_fail() { [ "$fail" -eq "$1" ] || { sed 's/^/    | /' "$STUB_DIR/out"; sed 's/^/    > /' "$STUB_DIR/calls"; }; }
t() { CASE="$1"; reset; }

# ── pr-env-clear-stale-lock.sh ────────────────────────────────────────────────────────────────────────
LOCK='https://storage\.googleapis\.com/storage/v1/b/nekohoa-dev-tfstate/o/state%2Fpr%2F217%2Fdefault\.tflock'
OLD='2026-10-26T13:47:34.120Z'
NEW="$(jq -rn 'now - 600 | todate')" # 10 minutes ago
lock_at() {
  route GET "${LOCK}\\?alt=media&generation=1761486454120000\$" 200 '{"ID":"x","Operation":"OperationTypeApply","Who":"runner@fv-az1","Version":"1.8.0","Created":"'"$1"'"}'
  route GET "${LOCK}\$" 200 '{"name":"state/pr/217/default.tflock","generation":"1761486454120000","timeCreated":"'"$1"'"}'
}
DEL="^curl DELETE ${LOCK}\\?ifGenerationMatch=1761486454120000 auth=yes\$"
clear_lock() { run pr-env-clear-stale-lock.sh PR_NUMBER=217 "$@"; }

t "no lock: nothing to do"; f0=$fail
clear_lock
exit_is 0; out_has "PR #217: no state lock."; not_called '^curl DELETE'
dump_on_fail "$f0"

t "a lock under 45 minutes old is kept"; f0=$fail
lock_at "$NEW"; clear_lock
exit_is 0; out_has "may belong to a live run. Keeping it"; out_has '"Operation":"OperationTypeApply"'
not_called '^curl DELETE'
dump_on_fail "$f0"

t "a stale lock is removed, conditional on its generation"; f0=$fail
lock_at "$OLD"; route DELETE "${LOCK}" 204 ''
clear_lock
exit_is 0; called "$DEL"; out_has "removed stale state lock 1761486454120000"
not_called '^gh '
dump_on_fail "$f0"

t "sweep mode keeps a stale lock while this PR's pr-env, teardown or repair run is unfinished"; f0=$fail
lock_at "$OLD"; route DELETE "${LOCK}" 204 ''
echo '[{"status":"in_progress","displayTitle":"feat: thing (pr-217)"},
       {"status":"completed","displayTitle":"feat: thing (pr-217)"}]' >"$STUB_DIR/runs_pr-env.yml"
echo '[{"status":"waiting","displayTitle":"pr-env-repair reap (pr-217)"}]' >"$STUB_DIR/runs_pr-env-repair.yml"
clear_lock CHECK_LIVE_RUNS=true
exit_is 0; out_has "2 pr-env, teardown or repair run(s) that may be this PR's are unfinished"
not_called '^curl DELETE'
called '^gh run list --repo nbon12/hoa_management_system --workflow pr-env\.yml '
called '^gh run list .* --workflow pr-env-teardown\.yml '
called '^gh run list .* --workflow pr-env-repair\.yml '
dump_on_fail "$f0"

t "sweep mode ignores other PRs' unfinished runs and finished ones"; f0=$fail
lock_at "$OLD"; route DELETE "${LOCK}" 204 ''
echo '[{"status":"in_progress","displayTitle":"fix (pr-217) in the title (pr-247)"},
       {"status":"completed","displayTitle":"feat: thing (pr-217)"}]' >"$STUB_DIR/runs_pr-env.yml"
echo '[{"status":"queued","displayTitle":"pr-env-teardown (pr-2170)"}]' >"$STUB_DIR/runs_pr-env-teardown.yml"
echo '[{"status":"waiting","displayTitle":"pr-env-repair reap (pr-74)"},
       {"status":"in_progress","displayTitle":"pr-env-repair verify (pr-0)"}]' >"$STUB_DIR/runs_pr-env-repair.yml"
clear_lock CHECK_LIVE_RUNS=true
exit_is 0; called "$DEL"; out_lacks "are unfinished"
dump_on_fail "$f0"

t "sweep mode counts an unfinished run without a PR tag as live"; f0=$fail
lock_at "$OLD"; route DELETE "${LOCK}" 204 ''
echo '[{"status":"waiting","displayTitle":"pr-env-repair"}]' >"$STUB_DIR/runs_pr-env-repair.yml"
clear_lock CHECK_LIVE_RUNS=true
exit_is 0; out_has "1 pr-env, teardown or repair run(s) that may be this PR's are unfinished"
not_called '^curl DELETE'
dump_on_fail "$f0"

t "sweep mode treats an unreadable run list as live"; f0=$fail
lock_at "$OLD"; route DELETE "${LOCK}" 204 ''; touch "$STUB_DIR/gh_fail"
clear_lock CHECK_LIVE_RUNS=true
exit_is 0; out_has "3 pr-env, teardown or repair run(s) that may be this PR's are unfinished"
not_called '^curl DELETE'
dump_on_fail "$f0"

t "sweep mode removes a stale lock when no run is live"; f0=$fail
lock_at "$OLD"; route DELETE "${LOCK}" 204 ''
clear_lock CHECK_LIVE_RUNS=true
exit_is 0; called "$DEL"
dump_on_fail "$f0"

t "a lock replaced between the check and the delete is left alone"; f0=$fail
lock_at "$OLD"; route DELETE "${LOCK}" 412 '{"error":{"code":412}}'
clear_lock
exit_is 0; out_has "was released or replaced while checking"
dump_on_fail "$f0"

t "a failed lookup or delete is an error"; f0=$fail
route GET "${LOCK}\$" 403 '{"error":{"code":403}}'
clear_lock
exit_is 1; out_has "returned HTTP 403"; not_called '^curl DELETE'
reset; lock_at "$OLD"; route DELETE "${LOCK}" 500 '{}'
clear_lock
exit_is 1; out_has "Removing state lock 1761486454120000 returned HTTP 500"
reset; touch "$STUB_DIR/gcloud_noauth"
clear_lock
exit_is 1; not_called '^curl '
dump_on_fail "$f0"

t "lock: bad inputs are rejected"; f0=$fail
clear_lock PR_NUMBER='217;rm'
exit_is 2
clear_lock CHECK_LIVE_RUNS=maybe
exit_is 2
clear_lock STALE_AFTER_MIN=soon
exit_is 2
dump_on_fail "$f0"

# ── pr-env-audit.sh ───────────────────────────────────────────────────────────────────────────────────
GCS='https://storage\.googleapis\.com/storage/v1/b/nekohoa-dev-tfstate/o\?prefix=state/pr/&delimiter=/'
RUN='https://run\.googleapis\.com/v2/projects/nekohoa-dev/locations/us-central1/services\?pageSize=100'
SM='https://secretmanager\.googleapis\.com/v1/projects/nekohoa-dev/secrets\?pageSize=250'
NEON='https://console\.neon\.tech/api/v2/projects/super-water-18090867/branches'
R2='https://api\.cloudflare\.com/client/v4/accounts/acct/r2/buckets\?per_page=1000'
svc() { printf '{"name":"projects/nekohoa-dev/locations/us-central1/services/%s"}' "$1"; }
sec() { printf '{"name":"projects/123/secrets/%s"}' "$1"; }
# Dev's own resources, which share the name stems and must never be reported.
dev_only() {
  route GET "${GCS}\$" 200 '{"prefixes":["state/pr/240/"]}'
  route GET "${RUN}\$" 200 '{"services":['"$(svc nekohoa-api-dev),$(svc nekohoa-api-pr-240)"']}'
  route GET "${SM}\$" 200 '{"secrets":['"$(sec dev-db-connection),$(sec pr-240-db-connection)"']}'
  route GET "${NEON}\$" 200 '{"branches":[{"name":"dev"},{"name":"pr-base"},{"name":"pr-240"}]}'
  route GET "${R2}\$" 200 '{"result":{"buckets":[{"name":"nekohoa-dev-documents"},{"name":"nekohoa-pr-240-documents"}]}}'
}
# PR 217 as the incident left it (no state prefix), next to PR 240's tracked environment.
orphans_217() {
  route GET "${GCS}\$" 200 '{"prefixes":["state/pr/240/"]}'
  route GET "${RUN}\$" 200 '{"services":['"$(svc nekohoa-api-dev),$(svc nekohoa-api-pr-217),$(svc nekohoa-api-pr-240)"']}'
  route GET "${SM}\$" 200 '{"secrets":['"$(sec pr-217-db-connection)"'],"nextPageToken":"p2"}'
  route GET "${SM}&pageToken=p2\$" 200 '{"secrets":['"$(sec pr-217-stripe-webhook),$(sec pr-240-db-connection)"']}'
  route GET "${NEON}\$" 200 '{"branches":[{"name":"pr-base"},{"name":"pr-217"},{"name":"pr-240"}]}'
  route GET "${R2}\$" 200 '{"result":{"buckets":[{"name":"nekohoa-pr-217-documents"}]}}'
}
audit() {
  local extra=()
  if [ -n "${audit_out:-}" ]; then rm -f "$STUB_DIR/orphans"; extra=(ORPHANS_OUT="$STUB_DIR/orphans"); fi
  run pr-env-audit.sh PROJECT=nekohoa-dev REGION=us-central1 NEON_PROJECT_ID=super-water-18090867 \
    CF_ACCOUNT=acct NEON_API_KEY=neon-test-token CF_TOKEN=cf-test-token GH_TOKEN=gh-test-token "${extra[@]}"
}

t "audit: tracked environments and Dev's own resources are not orphans"; f0=$fail
dev_only; audit
exit_is 0; out_has "No orphans"; out_has "State prefixes: 240"; out_lacks "== PR"
not_called '^gh api'
dump_on_fail "$f0"

t "audit: a closed PR's untracked resources fail the run and name the reap"; f0=$fail
orphans_217; audit
exit_is 1
out_has "== PR #217 (closed): no state/pr/217 prefix"
out_has "  Cloud Run service nekohoa-api-pr-217"
out_has "  secret pr-217-db-connection"
out_has "  secret pr-217-stripe-webhook" # second page of secrets
out_has "  Neon branch pr-217"
out_has "  R2 bucket nekohoa-pr-217-documents"
out_has "PR #217 is closed and has 5 untracked per-PR resource(s). Reap them: gh workflow run pr-env-repair.yml -f action=reap -f pr_number=217"
out_lacks "PR #240"
called '^gh api repos/nbon12/hoa_management_system/pulls/217 '
dump_on_fail "$f0"

t "audit: an open PR's untracked resources are a warning only"; f0=$fail
orphans_217; echo open >"$STUB_DIR/pr_217"; audit
exit_is 0; out_has "::warning::PR #217 is open and has 5 untracked per-PR resource(s)"
dump_on_fail "$f0"

t "audit: a PR whose state cannot be read counts as closed"; f0=$fail
orphans_217; touch "$STUB_DIR/gh_fail"; audit
exit_is 1; out_has "PR #217 is unknown"
dump_on_fail "$f0"

t "audit: a listing that fails stops the audit"; f0=$fail
route GET "${RUN}\$" 403 '{"error":{"code":403}}'
dev_only; audit
exit_is 1; out_has "returned HTTP 403"; out_lacks "No orphans"
dump_on_fail "$f0"

t "audit: an empty page ends pagination"; f0=$fail
route GET "${R2}\$" 200 '{"result":{"buckets":[{"name":"nekohoa-pr-217-documents"}]},"result_info":{"cursor":"c1"}}'
route GET "${R2}&cursor=c1\$" 200 '{"result":{"buckets":[]},"result_info":{"cursor":"c2"}}'
dev_only; audit
exit_is 1; out_has "R2 bucket nekohoa-pr-217-documents"; not_called 'cursor=c2'
dump_on_fail "$f0"

t "audit: ORPHANS_OUT lists the closed PRs with orphans, one per line"; f0=$fail
orphans_217; audit_out=1 audit
exit_is 1; [ "$(cat "$STUB_DIR/orphans")" = 217 ] && ok || bad "orphans file is '$(cat "$STUB_DIR/orphans")', expected 217"
reset; orphans_217; echo open >"$STUB_DIR/pr_217"; audit_out=1 audit
exit_is 0; [ -f "$STUB_DIR/orphans" ] && [ ! -s "$STUB_DIR/orphans" ] && ok || bad "an open PR's orphans were listed"
dump_on_fail "$f0"

t "audit: tokens go through a file descriptor and every call is authenticated"; f0=$fail
orphans_217; audit
grep -qE 'gcp-test-token|neon-test-token|cf-test-token' "$STUB_DIR/argv" && bad "a token reached curl's argv" || ok
grep -q 'auth=no' "$STUB_DIR/calls" && bad "a call went out without a token" || ok
dump_on_fail "$f0"

# ── pr-env-policy-gate.sh ─────────────────────────────────────────────────────────────────────────────
# A write as pr-env-repair.yml dispatches it; this run is 900. Later arguments override earlier ones.
gate() {
  run pr-env-policy-gate.sh CALLER=repair ACTION=reap PR_NUMBER=217 DRY_RUN=false OPS_AUTOMATION=on \
    ACTOR=nbon12 RUN_ID=900 GH_TOKEN=gh-test-token "$@" -- preflight
}
HOUR_AGO="$(jq -rn 'now - 3600 | todate')"
DAY_AGO="$(jq -rn 'now - 90000 | todate')" # 25 h
# write_runs N: N earlier pr-env-repair write runs in the last 24 h, plus ones that must not count.
write_runs() {
  local i out='[{"databaseId":900,"status":"in_progress","displayTitle":"pr-env-repair reap dry_run=false (pr-217)","createdAt":"'"$HOUR_AGO"'"},
    {"databaseId":801,"status":"completed","displayTitle":"pr-env-repair reap dry_run=false (pr-74)","createdAt":"'"$DAY_AGO"'"},
    {"databaseId":802,"status":"completed","displayTitle":"pr-env-repair diagnose dry_run=true (pr-74)","createdAt":"'"$HOUR_AGO"'"},
    {"databaseId":803,"status":"completed","displayTitle":"pr-env-repair destroy dry_run=true (pr-74)","createdAt":"'"$HOUR_AGO"'"}'
  for ((i = 1; i <= $1; i++)); do
    out+=',{"databaseId":'"$((700 + i))"',"status":"completed","displayTitle":"pr-env-repair unlock dry_run=false (pr-'"$i"')","createdAt":"'"$HOUR_AGO"'"}'
  done
  echo "${out}]" >"$STUB_DIR/runs_pr-env-repair.yml"
}

t "gate: a write on a closed PR with nothing live passes every check"; f0=$fail
write_runs 2; gate
exit_is 0
out_has "Policy gate P1 passed"; out_has "P8 passed: nbon12 has write access"; out_has "P5 passed: PR #217 is closed"
out_has "P4 passed"; out_has "P3 passed: 2 other write run(s) in the last 24 h (limit 3)"
called '^gh api repos/nbon12/hoa_management_system/collaborators/nbon12/permission '
dump_on_fail "$f0"

t "gate Z3/P1: the kill switch off, or unset, refuses before anything is read"; f0=$fail
gate OPS_AUTOMATION=off
exit_is 1; out_has "Policy gate P1 refused: repo variable OPS_AUTOMATION is 'off'"; not_called '^gh '
gate OPS_AUTOMATION=
exit_is 1; out_has "OPS_AUTOMATION is '<unset>'"
dump_on_fail "$f0"

t "gate Z2/P5: an open PR is refused for reap and destroy"; f0=$fail
echo '{"state":"open","closed_at":null}' >"$STUB_DIR/pr_217.json"
gate
exit_is 1; out_has "Policy gate P5 refused: PR #217 is 'open', not closed or merged."
gate ACTION=destroy
exit_is 1; out_has "P5 refused"
gate ACTION=destroy DRY_RUN=true
exit_is 1; out_has "P5 refused"
dump_on_fail "$f0"

t "gate: unlock does not need a closed PR"; f0=$fail
echo '{"state":"open","closed_at":null}' >"$STUB_DIR/pr_217.json"
gate ACTION=unlock
exit_is 0; out_lacks "P5"; not_called '^gh api repos/.*/pulls/'
dump_on_fail "$f0"

t "gate: a PR whose state cannot be read is refused"; f0=$fail
echo 'not json' >"$STUB_DIR/pr_217.json"
gate
exit_is 1; out_has "P5 refused: PR #217's state could not be read."
dump_on_fail "$f0"

t "gate Z4/P3: a fourth write in 24 h is refused, counting refused runs, not dry runs or older runs"; f0=$fail
write_runs 3; gate
exit_is 1; out_has "Policy gate P3 refused: 3 pr-env-repair write run(s) in the last 24 h already; the limit is 3"
write_runs 3; gate MAX_WRITES_PER_DAY=4
exit_is 0
dump_on_fail "$f0"

t "gate P4: an unreadable run list counts as live and refuses"; f0=$fail
touch "$STUB_DIR/runs_fail"; gate ACTION=unlock
exit_is 1; out_has "Policy gate P4 refused"
dump_on_fail "$f0"

t "gate P4: this PR's unfinished pr-env, teardown or repair run refuses; other PRs' and this run don't"; f0=$fail
write_runs 0
echo '[{"databaseId":11,"status":"queued","displayTitle":"pr-env-teardown (pr-217)"}]' >"$STUB_DIR/runs_pr-env-teardown.yml"
gate
exit_is 1; out_has "live: pr-env-teardown.yml run 11"; out_has "Policy gate P4 refused: 1 pr-env, teardown or repair run(s)"
reset; write_runs 0
echo '[{"databaseId":12,"status":"in_progress","displayTitle":"feat (pr-2170)"},{"databaseId":13,"status":"completed","displayTitle":"x (pr-217)"}]' >"$STUB_DIR/runs_pr-env.yml"
gate
exit_is 0; out_has "P4 passed"
reset; echo '[{"databaseId":14,"status":"waiting","displayTitle":"pr-env-repair"}]' >"$STUB_DIR/runs_pr-env-repair.yml"
gate
exit_is 1; out_has "P4 refused" # untagged counts as live
dump_on_fail "$f0"

t "gate P8: a dispatcher without write access is refused"; f0=$fail
echo read >"$STUB_DIR/perm"; gate
exit_is 1; out_has "Policy gate P8 refused: nbon12 has 'read' access"
echo maintain >"$STUB_DIR/perm"; gate
exit_is 0
dump_on_fail "$f0"

t "gate: a destroy dry run checks P1, P8 and P5 only"; f0=$fail
write_runs 5
echo '[{"databaseId":11,"status":"queued","displayTitle":"pr-env-teardown (pr-217)"}]' >"$STUB_DIR/runs_pr-env-teardown.yml"
gate ACTION=destroy DRY_RUN=true
exit_is 0; out_lacks "P3"; out_lacks "P4"; not_called '^gh run list'
dump_on_fail "$f0"

t "gate sweep: P1, P4 and P5 with 24 h since close; no P3 or P8"; f0=$fail
echo '{"state":"closed","closed_at":"'"$DAY_AGO"'"}' >"$STUB_DIR/pr_217.json"
write_runs 5 # five writes today, but none unfinished: the sweep has no daily write limit
jq '[.[] | select(.databaseId != 900)]' "$STUB_DIR/runs_pr-env-repair.yml" >"$STUB_DIR/r" && mv "$STUB_DIR/r" "$STUB_DIR/runs_pr-env-repair.yml"
run pr-env-policy-gate.sh CALLER=sweep ACTION=reap PR_NUMBER=217 DRY_RUN=false OPS_AUTOMATION=on GH_TOKEN=x -- preflight
exit_is 0; out_has "P5 passed"; out_has "P4 passed"; out_lacks "P3"; out_lacks "P8"
echo '{"state":"closed","closed_at":"'"$HOUR_AGO"'"}' >"$STUB_DIR/pr_217.json"
run pr-env-policy-gate.sh CALLER=sweep ACTION=reap PR_NUMBER=217 DRY_RUN=false OPS_AUTOMATION=on GH_TOKEN=x -- preflight
exit_is 1; out_has "P5 refused: PR #217 closed 1 h ago, under 24 h"
dump_on_fail "$f0"

t "gate: bad inputs are rejected"; f0=$fail
gate PR_NUMBER=0; exit_is 2
gate PR_NUMBER='217;rm'; exit_is 2
gate ACTION=diagnose; exit_is 2
gate CALLER=other; exit_is 2
gate RUN_ID=abc; exit_is 2
run pr-env-policy-gate.sh PR_NUMBER=217 ACTION=reap -- bogus; exit_is 2
dump_on_fail "$f0"

# P7: the reap plan is pr-env-reap.sh's ITEMS_OUT from a dry run.
plan() { run pr-env-policy-gate.sh ACTION="${ACTION:-reap}" PR_NUMBER=217 PLAN_FILE="$STUB_DIR/plan" -- check-plan; }
per_pr_plan() {
  cat >"$STUB_DIR/plan" <<'P'
would delete: Cloud Run service nekohoa-api-pr-217
would delete: R2 bucket nekohoa-pr-217-documents (emptied first)
would delete: secret pr-217-db-connection
would delete: secret pr-217-stripe-webhook
would delete: Neon branch pr-217 (br-pr217)
would delete: Stripe webhook endpoint we_1AbC (host nekohoa-api-pr-217-abc123-uc.a.run.app)
would delete: Pages deployment 0b5e-77aa (branch pr-217)
would delete: gs://nekohoa-dev-tfstate/state/pr/217/default.tfstate#1761486454120000
P
}

t "plan P7: every per-PR name is accepted"; f0=$fail
per_pr_plan; plan
exit_is 0; out_has "Policy gate P7 passed: 8 planned deletion(s), all PR #217's (6 counted against the cap of 10)."
: >"$STUB_DIR/plan"; plan
exit_is 0; out_has "0 planned deletion(s)"
dump_on_fail "$f0"

t "plan P7: shared Dev names and another PR's names are refused"; f0=$fail
for item in "Cloud Run service nekohoa-api-dev" "secret dev-jwt-secret" "Neon branch main (br-main)" \
  "Neon branch pr-base (br-base)" "R2 bucket nekohoa-dev-documents (emptied first)" "secret pr-2170-db-connection" \
  "Cloud Run service nekohoa-api-pr-2170" "Stripe webhook endpoint we_1 (host nekohoa-api-dev-abc.run.app)" \
  "Pages deployment abc (branch main)" "gs://nekohoa-dev-tfstate/state/dev/default.tfstate#1" \
  "gs://nekohoa-dev-tfstate/state/pr/2170/default.tfstate#1"; do
  per_pr_plan; echo "would delete: ${item}" >>"$STUB_DIR/plan"; plan
  exit_is 1; out_has "'${item}' is not one of PR #217's per-PR names"; out_has "Policy gate P7 refused: 1 planned deletion(s)"
done
dump_on_fail "$f0"

t "plan P7: more than MAX_NAMES resources is refused; Pages and state versions don't count"; f0=$fail
per_pr_plan
for i in 1 2 3 4 5; do echo "would delete: secret pr-217-extra-${i}" >>"$STUB_DIR/plan"; done
plan
exit_is 1; out_has "Policy gate P7 refused: the reap would delete 11 resources, more than 10."
per_pr_plan
for i in $(seq 1 30); do echo "would delete: Pages deployment d${i} (branch pr-217)" >>"$STUB_DIR/plan"; done
plan
exit_is 0
dump_on_fail "$f0"

t "plan P7: a missing plan file is a refusal"; f0=$fail
plan
exit_is 1; out_has "the dry run left no plan file"
dump_on_fail "$f0"

t "plan P7 destroy: only deletes inside module.pr_environment"; f0=$fail
printf '%s\n' "delete module.pr_environment.google_cloud_run_v2_service.api" \
  "delete module.pr_environment.neon_branch.pr" >"$STUB_DIR/plan"
ACTION=destroy plan
exit_is 0; out_has "the destroy plan deletes 2 address(es)"
echo "delete google_secret_manager_secret.dev_jwt" >>"$STUB_DIR/plan"; ACTION=destroy plan
exit_is 1; out_has "planned 'delete' on 'google_secret_manager_secret.dev_jwt'"
printf '%s\n' "delete+create module.pr_environment.neon_branch.pr" >"$STUB_DIR/plan"; ACTION=destroy plan
exit_is 1; out_has "Policy gate P7 refused: 1 planned change(s)"
dump_on_fail "$f0"

t "gate: AUDIT_FILE gets the passes and the refusal"; f0=$fail
echo '{"state":"open","closed_at":null}' >"$STUB_DIR/pr_217.json"
gate AUDIT_FILE="$STUB_DIR/audit"
exit_is 1; grep -qF "**Policy gate P5 refused:**" "$STUB_DIR/audit" && ok || bad "refusal not in AUDIT_FILE"
grep -qF "Policy gate P1 passed" "$STUB_DIR/audit" && ok || bad "pass not in AUDIT_FILE"
rm -f "$STUB_DIR/audit"
dump_on_fail "$f0"

# ── stripe-webhook-register.sh ────────────────────────────────────────────────────────────────────────
# The stub picks the method from -X only, so the create call (curl -d …) is routed as GET.
t "stripe webhook: subscribes to every event the app handles"; f0=$fail
route GET 'api\.stripe\.com/v1/webhook_endpoints\?limit=100$' 200 '{"data":[]}'
route GET 'api\.stripe\.com/v1/webhook_endpoints$' 200 '{"secret":"whsec_test"}'
run stripe-webhook-register.sh STRIPE_SECRET_KEY_TEST=sk_test_x PR_NUMBER=217 \
  API_URL=https://pr-217.example.run.app GCP_PROJECT_ID=nekohoa-dev
exit_is 0
for e in payment_intent.succeeded payment_intent.payment_failed charge.refunded \
  charge.dispute.created charge.dispute.closed; do
  grep -qF -- "enabled_events[]=${e}" "$STUB_DIR/argv" && ok || bad "no enabled_events[]=${e}"
done
grep -qF 'enabled_events[]=charge.refund.updated' "$STUB_DIR/argv" && bad "subscribed charge.refund.updated" || ok
called 'gcloud secrets versions add pr-217-stripe-webhook'
dump_on_fail "$f0"

echo "pr-env ops tests: ${pass} passed, ${fail} failed"
[ "$fail" -eq 0 ]
