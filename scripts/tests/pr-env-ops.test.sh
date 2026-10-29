#!/usr/bin/env bash
# Tests for scripts/pr-env-clear-stale-lock.sh and scripts/pr-env-audit.sh against stubbed curl, gcloud and
# gh (no network, no cloud). Same harness as pr-env-reap.test.sh: each case writes the HTTP routes the stub
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
method=GET out=/dev/stdout url="" auth=no
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2 ;;
    -o) out="$2"; shift 2 ;;
    -w) shift 2 ;;
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
printf '%s' "$code"
EOF
cat >"$STUB_DIR/bin/gcloud" <<'EOF'
#!/usr/bin/env bash
echo "gcloud $*" >>"$STUB_DIR/calls"
case "$1 $2" in
  "auth print-access-token") [ -f "$STUB_DIR/gcloud_noauth" ] && exit 1; echo gcp-test-token ;;
esac
EOF
# gh: `run list --workflow <w> --jq <expr>` applies the real --jq to $STUB_DIR/runs_<w> (default []), so
# the script's filter is tested too; fails when gh_fail exists. `api repos/.../pulls/<n>` prints
# $STUB_DIR/pr_<n> (default "closed").
cat >"$STUB_DIR/bin/gh" <<'EOF'
#!/usr/bin/env bash
echo "gh $*" >>"$STUB_DIR/calls"
[ -f "$STUB_DIR/gh_fail" ] && exit 1
case "$1 $2" in
  "run list")
    w="" q="."
    while [ $# -gt 0 ]; do
      case "$1" in --workflow) w="$2"; shift 2 ;; --jq) q="$2"; shift 2 ;; *) shift ;; esac
    done
    jq -r "$q" "$STUB_DIR/runs_${w}" 2>/dev/null || jq -rn "[] | $q" ;;
  api*) n="${2##*/}"; cat "$STUB_DIR/pr_${n}" 2>/dev/null || echo closed ;;
esac
EOF
chmod +x "$STUB_DIR/bin/"*

# ── Harness ───────────────────────────────────────────────────────────────────────────────────────────
route() { printf '%s\t%s\t%s\t%s\n' "$@" >>"$STUB_DIR/routes"; }
reset() {
  rm -f "$STUB_DIR"/{routes,calls,argv,gh_fail,gcloud_noauth} "$STUB_DIR"/pr_* "$STUB_DIR"/runs_*
  touch "$STUB_DIR/routes" "$STUB_DIR/calls" "$STUB_DIR/argv"
}
run() {
  local script="$1"; shift
  env -i PATH="$STUB_DIR/bin:$PATH" HOME="$HOME" TMPDIR="${TMPDIR:-/tmp}" STUB_DIR="$STUB_DIR" \
    REPO=nbon12/hoa_management_system "$@" bash "$ROOT/scripts/${script}" >"$STUB_DIR/out" 2>&1
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
  run pr-env-audit.sh PROJECT=nekohoa-dev REGION=us-central1 NEON_PROJECT_ID=super-water-18090867 \
    CF_ACCOUNT=acct NEON_API_KEY=neon-test-token CF_TOKEN=cf-test-token GH_TOKEN=gh-test-token
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

t "audit: tokens go through a file descriptor and every call is authenticated"; f0=$fail
orphans_217; audit
grep -qE 'gcp-test-token|neon-test-token|cf-test-token' "$STUB_DIR/argv" && bad "a token reached curl's argv" || ok
grep -q 'auth=no' "$STUB_DIR/calls" && bad "a call went out without a token" || ok
dump_on_fail "$f0"

echo "pr-env ops tests: ${pass} passed, ${fail} failed"
[ "$fail" -eq 0 ]
