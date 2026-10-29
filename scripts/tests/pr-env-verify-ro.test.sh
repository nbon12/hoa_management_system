#!/usr/bin/env bash
# Tests for scripts/pr-env-verify-ro.sh against stubbed curl and gcloud (no network, no cloud). Each case
# writes the HTTP routes the stub serves, runs the script, and checks its exit code and output.
# Run: bash scripts/tests/pr-env-verify-ro.test.sh
# shellcheck disable=SC2015 # `check && ok || bad` is the assertion idiom; ok cannot fail
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCRIPT="$ROOT/scripts/pr-env-verify-ro.sh"
STUB_DIR="$(mktemp -d)"
trap 'rm -rf "$STUB_DIR"' EXIT
mkdir -p "$STUB_DIR/bin"
pass=0 fail=0

# ── Stubs ─────────────────────────────────────────────────────────────────────────────────────────────
# curl: serves the first route in $STUB_DIR/routes whose method matches, whose regex matches the URL (with
# the --get query) and whose optional fifth column matches the request body; unmatched → 404 {}. Logs
# "curl METHOD URL auth=yes|no" to calls and its raw argv to argv.
cat >"$STUB_DIR/bin/curl" <<'EOF'
#!/usr/bin/env bash
echo "curl $*" >>"$STUB_DIR/argv"
method=GET out=/dev/stdout url="" query="" data="" get=no auth=no
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2 ;;
    -o) out="$2"; shift 2 ;;
    -w) shift 2 ;;
    -H) if [[ "$2" == @* ]] && grep -q '^Authorization: Bearer ' "${2#@}"; then auth=yes; fi; shift 2 ;;
    --get) get=yes; shift ;;
    -d) data="${data:+${data}&}$2"; shift 2 ;;
    http*) url="$1"; shift ;;
    *) shift ;;
  esac
done
full="$url"; [ "$get" = yes ] && full="${url}${data:+?${data}}" && data=""
echo "curl ${method} ${full} auth=${auth}" >>"$STUB_DIR/calls"
code=404 body='{}'
while IFS=$'\t' read -r m re c b dre; do
  if [ "$m" = "$method" ] && [[ "$full" =~ $re ]] && [[ "$data" =~ ${dre:-.*} ]]; then code="$c" body="$b"; break; fi
done <"$STUB_DIR/routes"
printf '%s' "$body" >"$out"
printf '%s' "$code"
EOF
cat >"$STUB_DIR/bin/gcloud" <<'EOF'
#!/usr/bin/env bash
echo "gcloud $*" >>"$STUB_DIR/argv"
case "$1 $2" in
  "auth print-access-token") [ -f "$STUB_DIR/gcloud_noauth" ] && exit 1; echo gcp-test-token ;;
  *) echo "gcloud $*" >>"$STUB_DIR/calls" ;;
esac
EOF
chmod +x "$STUB_DIR/bin/"*

# ── Fixtures ──────────────────────────────────────────────────────────────────────────────────────────
RUN='https://run\.googleapis\.com/v2/projects/nekohoa-dev/locations/us-central1'
SM='https://secretmanager\.googleapis\.com/v1/projects/nekohoa-dev'
LOG='https://logging\.googleapis\.com/v2/entries:list'
GCS='https://storage\.googleapis\.com/storage/v1/b/nekohoa-dev-tfstate/o'
SCHED='https://cloudscheduler\.googleapis\.com/v1/projects/nekohoa-dev/locations/us-central1/jobs/nekohoa-arc-sweep-dev'
CF='https://api\.cloudflare\.com/client/v4'
STRIPE='https://api\.stripe\.com/v1'
NEON='https://console\.neon\.tech/api/v2/projects/super-water-18090867'
DENIED='{"error":{"code":403,"status":"PERMISSION_DENIED"}}'

# route METHOD URL_REGEX STATUS BODY [REQUEST_BODY_REGEX]. The first match wins: a case adds its overrides
# before the correctly scoped defaults below.
route() { printf '%s\t%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$4" "${5:-}" >>"$STUB_DIR/routes.case"; }
reset() {
  rm -f "$STUB_DIR"/{routes.case,calls,argv,gcloud_noauth}
  touch "$STUB_DIR/routes.case" "$STUB_DIR/calls" "$STUB_DIR/argv"
}
# Every identity scoped as the handoff specifies: reads work, writes and secret reads are refused.
base_routes() {
  cat "$STUB_DIR/routes.case" >"$STUB_DIR/routes"
  {
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${RUN}/services\\?pageSize=1\$" 200 '{"services":[]}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${SM}/secrets\\?pageSize=1\$" 200 '{"secrets":[]}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' POST "${LOG}\$" 200 '{"entries":[]}' 'cloud_run_revision'
    printf '%s\t%s\t%s\t%s\t%s\n' POST "${LOG}\$" 200 '{"entries":[{"protoPayload":{"methodName":"google.cloud.scheduler.v1.CloudScheduler.CreateJob","request":{"job":{"name":"j"}}}}]}' 'cloudscheduler'
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${GCS}\\?prefix=state/&maxResults=1\$" 200 '{"kind":"storage#objects"}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${SM}/secrets/dev-jwt-secret/versions/latest:access\$" 403 "$DENIED" ''
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${GCS}/state%2Fdev%2Fdefault\\.tfstate\\?alt=media\$" 403 "$DENIED" ''
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${SCHED}\$" 403 "$DENIED" ''
    printf '%s\t%s\t%s\t%s\t%s\n' POST "${SM}/secrets\\?secretId=agent-ro-probe\$" 403 "$DENIED" ''
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${CF}/user/tokens/verify\$" 200 '{"success":true,"result":{"status":"active","expires_on":"2027-01-25T00:00:00Z"}}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${CF}/accounts/acct/r2/buckets\\?per_page=1\$" 200 '{"success":true,"result":{"buckets":[]}}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${CF}/accounts/acct/pages/projects/nekohoa-dev/deployments\\?per_page=1\$" 200 '{"success":true,"result":[]}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' DELETE "${CF}/accounts/acct/r2/buckets/agent-ro-probe\$" 403 '{"success":false,"errors":[{"code":10000,"message":"Authentication error"}]}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${STRIPE}/webhook_endpoints\\?limit=1\$" 200 '{"data":[]}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${STRIPE}/customers\\?limit=1\$" 403 '{"error":{"type":"invalid_request_error"}}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' DELETE "${STRIPE}/webhook_endpoints/we_agentprobe\$" 403 '{"error":{"type":"invalid_request_error"}}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' GET "${NEON}/branches\$" 200 '{"branches":[]}' ''
    printf '%s\t%s\t%s\t%s\t%s\n' DELETE "${NEON}/branches/br-agent-probe\$" 403 '{"message":"forbidden"}' ''
  } >>"$STUB_DIR/routes"
}
run_verify() {
  base_routes
  env -i PATH="$STUB_DIR/bin:$PATH" HOME="$HOME" TMPDIR="${TMPDIR:-/tmp}" STUB_DIR="$STUB_DIR" \
    PROJECT=nekohoa-dev REGION=us-central1 PREFIX=dev CF_ACCOUNT=acct CF_TOKEN=cf-test-token \
    STRIPE_KEY=rk_test_stub-key "$@" bash "$SCRIPT" >"$STUB_DIR/out" 2>&1
  echo $? >"$STUB_DIR/rc"
}

# ── Assertions ────────────────────────────────────────────────────────────────────────────────────────
CASE=""
ok() { pass=$((pass + 1)); }
bad() { fail=$((fail + 1)); echo "not ok - ${CASE}: $*"; }
exit_is() { [ "$(cat "$STUB_DIR/rc")" = "$1" ] && ok || bad "exit $(cat "$STUB_DIR/rc"), expected $1"; }
out_has() { grep -qF -- "$1" "$STUB_DIR/out" && ok || bad "output lacks '$1'"; }
out_lacks() { grep -qF -- "$1" "$STUB_DIR/out" && bad "output contains '$1'" || ok; }
calls_count() { [ "$(grep -cE -- "$1" "$STUB_DIR/calls")" = "$2" ] && ok || bad "expected $2 call(s) matching /$1/, got $(grep -cE -- "$1" "$STUB_DIR/calls")"; }
dump_on_fail() { [ "$fail" -eq "$1" ] || { sed 's/^/    | /' "$STUB_DIR/out"; sed 's/^/    > /' "$STUB_DIR/calls"; }; }
t() { CASE="$1"; reset; }

# ── Cases ─────────────────────────────────────────────────────────────────────────────────────────────
t "correctly scoped identities pass every check"; f0=$fail
run_verify
exit_is 0
out_has "PASS  list Cloud Run services (HTTP 200)"
out_has "PASS  read the value of secret dev-jwt-secret is refused (HTTP 403)"
out_has "PASS  read state/dev/default.tfstate is refused (HTTP 403)"
out_has "PASS  describe Cloud Scheduler job nekohoa-arc-sweep-dev is refused (HTTP 403)"
out_has "PASS  create secret agent-ro-probe is refused (HTTP 403)"
out_has "PASS  no request headers in 1 Cloud Scheduler audit-log entries (400 days)"
out_has "PASS  token is active (expires 2027-01-25T00:00:00Z)"
out_has "PASS  delete R2 bucket agent-ro-probe is refused (HTTP 403)"
out_has "PASS  key is a restricted test-mode key (rk_test_ prefix)"
out_has "PASS  list customers is refused (HTTP 403)"
out_has "PASS  delete webhook endpoint we_agentprobe is refused (HTTP 403)"
out_has "not checked  Neon: no read-only Neon key (handoff O6)"
out_has "none failed"
out_lacks "FAIL"
dump_on_fail "$f0"

t "tokens never reach curl's argv, and every call carries a token"; f0=$fail
run_verify
grep -qE 'gcp-test-token|cf-test-token|rk_test_stub-key' "$STUB_DIR/argv" && bad "a token is in argv" || ok
grep -q 'auth=no' "$STUB_DIR/calls" && bad "a call went out without a token" || ok
dump_on_fail "$f0"

t "a readable secret value fails, and the value is never printed"; f0=$fail
route GET "${SM}/secrets/dev-jwt-secret/versions/latest:access\$" 200 '{"payload":{"data":"U0VDUkVULVZBTFVF"}}'
run_verify
exit_is 1
out_has "FAIL  read the value of secret dev-jwt-secret was NOT refused (HTTP 200)"
out_lacks "U0VDUkVULVZBTFVF"
dump_on_fail "$f0"

t "a created probe secret fails and says how to delete it"; f0=$fail
route POST "${SM}/secrets\\?secretId=agent-ro-probe\$" 200 '{"name":"projects/p/secrets/agent-ro-probe"}'
run_verify
exit_is 1
out_has "FAIL  create secret agent-ro-probe was NOT refused (HTTP 200)"
out_has "gcloud secrets delete agent-ro-probe --project nekohoa-dev"
dump_on_fail "$f0"

t "scheduler headers in audit logs fail across pages, without printing values"; f0=$fail
route POST "${LOG}\$" 200 '{"entries":[{"protoPayload":{"request":{"job":{"httpTarget":{"headers":{"X-Scheduler-Secret":"s3cr3t-value"}}}}}}]}' 'pageToken.*p2'
route POST "${LOG}\$" 200 '{"entries":[{"protoPayload":{"request":{}}}],"nextPageToken":"p2"}' 'cloudscheduler'
run_verify
exit_is 1
out_has "FAIL  2 header field path(s) in Cloud Scheduler audit logs"
out_lacks "s3cr3t-value"
calls_count "^curl POST ${LOG} " 3
dump_on_fail "$f0"

t "a missing probe target is inconclusive, not a failure"; f0=$fail
route GET "${SM}/secrets/dev-jwt-secret/versions/latest:access\$" 404 '{"error":{"code":404}}'
route GET "${SCHED}\$" 404 '{"error":{"code":404}}'
run_verify
exit_is 0
out_has "inconclusive  read the value of secret dev-jwt-secret: HTTP 404"
out_has "inconclusive  describe Cloud Scheduler job nekohoa-arc-sweep-dev: HTTP 404"
dump_on_fail "$f0"

t "an R2 delete that gets to 'bucket does not exist' counts as over-scoped"; f0=$fail
route DELETE "${CF}/accounts/acct/r2/buckets/agent-ro-probe\$" 404 '{"success":false,"errors":[{"code":10006,"message":"The specified bucket does not exist."}]}'
run_verify
exit_is 1
out_has "FAIL  delete R2 bucket agent-ro-probe was NOT refused"
dump_on_fail "$f0"

t "an account-owned Cloudflare token verifies at the account endpoint"; f0=$fail
route GET "${CF}/user/tokens/verify\$" 401 '{"success":false,"errors":[{"code":1000,"message":"Invalid API Token"}]}'
route GET "${CF}/accounts/acct/tokens/verify\$" 200 '{"success":true,"result":{"status":"active","expires_on":"2027-01-25T00:00:00Z"}}'
run_verify
exit_is 0
out_has "PASS  token is active (expires 2027-01-25T00:00:00Z)"
dump_on_fail "$f0"

t "a Stripe secret key, or a 404 on delete, fails without printing the key"; f0=$fail
route DELETE "${STRIPE}/webhook_endpoints/we_agentprobe\$" 404 '{"error":{"type":"invalid_request_error","code":"resource_missing"}}'
run_verify STRIPE_KEY=sk_test_stub-secret
exit_is 1
out_has "FAIL  key is not a restricted test-mode key"
out_has "FAIL  delete webhook endpoint we_agentprobe was NOT refused (HTTP 404)"
out_lacks "sk_test_stub-secret"
dump_on_fail "$f0"

t "a Neon key is checked when present, and a 404 on delete fails"; f0=$fail
route DELETE "${NEON}/branches/br-agent-probe\$" 404 '{"message":"not found"}'
run_verify NEON_API_KEY=neon-test-token NEON_PROJECT_ID=super-water-18090867
exit_is 1
out_has "PASS  list branches (HTTP 200)"
out_has "FAIL  delete branch br-agent-probe was NOT refused (HTTP 404)"
dump_on_fail "$f0"

t "without credentials every provider is not checked and nothing is called"; f0=$fail
touch "$STUB_DIR/gcloud_noauth"
run_verify CF_TOKEN= STRIPE_KEY=
exit_is 0
out_has "not checked  GCP: gcloud has no usable credential"
out_has "not checked  Cloudflare: no CF_TOKEN"
out_has "not checked  Stripe: no STRIPE_KEY"
[ -s "$STUB_DIR/calls" ] && bad "a call was made" || ok
dump_on_fail "$f0"

t "required settings are enforced"; f0=$fail
run_verify PROJECT=
[ "$(cat "$STUB_DIR/rc")" != 0 ] && ok || bad "ran without PROJECT"
dump_on_fail "$f0"

echo "pr-env-verify-ro tests: ${pass} passed, ${fail} failed"
[ "$fail" -eq 0 ]
