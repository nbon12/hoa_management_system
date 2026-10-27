#!/usr/bin/env bash
# Tests for scripts/pr-env-reap.sh against stubbed curl, gcloud, aws, tofu and gh (no network, no cloud).
# Each case writes the HTTP routes the stub serves, runs the script, and checks its exit code, its output
# and the calls it made. Run: bash scripts/tests/pr-env-reap.test.sh
# shellcheck disable=SC2015 # `check && ok || bad` is the assertion idiom; ok cannot fail
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCRIPT="$ROOT/scripts/pr-env-reap.sh"
STUB_DIR="$(mktemp -d)"
trap 'rm -rf "$STUB_DIR"' EXIT
mkdir -p "$STUB_DIR/bin"
pass=0 fail=0

# ── Stubs ─────────────────────────────────────────────────────────────────────────────────────────────
# curl: serves the first route in $STUB_DIR/routes whose method matches and whose regex matches the URL
# (query included); unmatched → 404 {}. Logs "curl METHOD URL auth=yes|no" (auth = a Bearer header came
# through -H @file) to calls, and its raw argv to argv.
cat >"$STUB_DIR/bin/curl" <<'EOF'
#!/usr/bin/env bash
echo "curl $*" >>"$STUB_DIR/argv"
method=GET out=/dev/stdout url="" query="" auth=no
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2 ;;
    -o) out="$2"; shift 2 ;;
    -w) shift 2 ;;
    -H) if [[ "$2" == @* ]] && grep -q '^Authorization: Bearer ' "${2#@}"; then auth=yes; fi; shift 2 ;;
    -d | --data-urlencode) query="${query:+${query}&}$2"; shift 2 ;;
    http*) url="$1"; shift ;;
    *) shift ;;
  esac
done
full="${url}${query:+?${query}}"
echo "curl ${method} ${full} auth=${auth}" >>"$STUB_DIR/calls"
code=404 body='{}'
while IFS=$'\t' read -r m re c b; do
  if [ "$m" = "$method" ] && [[ "$full" =~ $re ]]; then code="$c" body="$b"; break; fi
done <"$STUB_DIR/routes"
printf '%s' "$body" >"$out"
printf '%s' "$code"
EOF
cat >"$STUB_DIR/bin/gcloud" <<'EOF'
#!/usr/bin/env bash
echo "gcloud $*" >>"$STUB_DIR/argv"
case "$1 $2" in
  "auth print-access-token") [ -f "$STUB_DIR/gcloud_noauth" ] && exit 1; echo gcp-test-token ;;
  "secrets versions") echo "value-of-${5#--secret=}" ;;
  *) echo "gcloud $*" >>"$STUB_DIR/calls" ;;
esac
EOF
cat >"$STUB_DIR/bin/aws" <<'EOF'
#!/usr/bin/env bash
echo "aws $* key-in-env=$([ -n "${AWS_SECRET_ACCESS_KEY:-}" ] && echo yes || echo no)" >>"$STUB_DIR/calls"
EOF
cat >"$STUB_DIR/bin/tofu" <<'EOF'
#!/usr/bin/env bash
echo "tofu $*" >>"$STUB_DIR/calls"
[ -f "$STUB_DIR/state_list_fail" ] && exit 1
cat "$STUB_DIR/state_list" 2>/dev/null || true
EOF
cat >"$STUB_DIR/bin/gh" <<'EOF'
#!/usr/bin/env bash
echo "gh $*" >>"$STUB_DIR/calls"
cat "$STUB_DIR/pr_state"
EOF
chmod +x "$STUB_DIR/bin/"*

# ── Fixtures ──────────────────────────────────────────────────────────────────────────────────────────
RUN='https://run\.googleapis\.com/v2/projects/nekohoa-dev/locations/us-central1/services/nekohoa-api-pr-217'
SM='https://secretmanager\.googleapis\.com/v1/projects/nekohoa-dev/secrets'
CF='https://api\.cloudflare\.com/client/v4/accounts/acct'
NEON='https://console\.neon\.tech/api/v2/projects/super-water-18090867'
GCS='https://storage\.googleapis\.com/storage/v1/b/nekohoa-dev-tfstate/o\?prefix=state/pr/217/&'
STRIPE='https://api\.stripe\.com/v1/webhook_endpoints'
OLD='2026-10-26T13:47:34.120Z'
NOT_IN_BRANCHES='{"branches":[{"id":"br-dev","name":"dev","default":true},{"id":"br-base","name":"pr-base","parent_id":"br-dev"}]}'

# route METHOD REGEX STATUS BODY. The first matching route wins, so add a case's overrides before leftovers.
route() { printf '%s\t%s\t%s\t%s\n' "$@" >>"$STUB_DIR/routes.case"; }
reset() {
  rm -f "$STUB_DIR"/{routes.case,calls,argv,state_list,state_list_fail,gcloud_noauth}
  touch "$STUB_DIR/routes.case" "$STUB_DIR/calls" "$STUB_DIR/argv"
  echo closed >"$STUB_DIR/pr_state"
}
# PR 217 as the incident left it: service, bucket, both secrets and the Neon branch, all untracked.
leftovers() {
  route GET "${RUN}\$" 200 '{"labels":{"pr-env":"true","pr-number":"217"},"createTime":"'"$OLD"'"}'
  route GET "${RUN}/revisions\$" 200 '{"revisions":[{"name":"projects/p/locations/l/services/s/revisions/nekohoa-api-pr-217-00001-abc","createTime":"'"$OLD"'"}]}'
  route GET "${CF}/r2/buckets/nekohoa-pr-217-documents\$" 200 '{"success":true,"result":{"name":"nekohoa-pr-217-documents","creation_date":"'"$OLD"'"}}'
  route DELETE "${CF}/r2/buckets/nekohoa-pr-217-documents\$" 200 '{"success":true}'
  route GET "${SM}/pr-217-db-connection\$" 200 '{"labels":{"pr-number":"217"},"createTime":"'"$OLD"'"}'
  route GET "${SM}/pr-217-stripe-webhook\$" 200 '{"labels":{"pr-number":"217"},"createTime":"'"$OLD"'"}'
  route GET "${NEON}/branches\$" 200 '{"branches":[{"id":"br-dev","name":"dev","default":true},{"id":"br-base","name":"pr-base","parent_id":"br-dev"},{"id":"br-pr217","name":"pr-217","parent_id":"br-base","default":false,"current_state":"ready","created_at":"'"$OLD"'"}]}'
  route GET "${NEON}/branches/br-pr217/endpoints\$" 200 '{"endpoints":[{"id":"ep-pr217","current_state":"idle"}]}'
  route DELETE "${NEON}/branches/br-pr217\$" 200 '{}'
}
# Nothing exists anywhere (appended after the case's own routes; the first match wins).
base_routes() {
  cat "$STUB_DIR/routes.case" >"$STUB_DIR/routes"
  {
    printf '%s\t%s\t%s\t%s\n' GET "${CF}/r2/buckets/" 404 '{"success":false,"errors":[{"code":10006,"message":"The specified bucket does not exist."}]}'
    printf '%s\t%s\t%s\t%s\n' GET "${NEON}/branches\$" 200 "$NOT_IN_BRANCHES"
    printf '%s\t%s\t%s\t%s\n' GET "${GCS}" 200 '{"kind":"storage#objects"}'
    printf '%s\t%s\t%s\t%s\n' GET "${STRIPE}\\?" 200 '{"data":[],"has_more":false}'
    printf '%s\t%s\t%s\t%s\n' GET "${CF}/pages/projects/nekohoa-dev/deployments\\?" 200 '{"result":[],"result_info":{"total_pages":1}}'
  } >>"$STUB_DIR/routes"
}
run_reap() {
  base_routes
  env -i PATH="$STUB_DIR/bin:$PATH" HOME="$HOME" TMPDIR="${TMPDIR:-/tmp}" STUB_DIR="$STUB_DIR" \
    PR_NUMBER=217 PROJECT=nekohoa-dev REGION=us-central1 PREFIX=dev NEON_PROJECT_ID=super-water-18090867 \
    NEON_PR_BASE_BRANCH_ID=br-base CF_ACCOUNT=acct NEON_API_KEY=neon-test-token CF_TOKEN=cf-test-token \
    STRIPE_SECRET_KEY_TEST=stripe-test-token REPO=nbon12/hoa_management_system DRY_RUN=true ONLY_UNTRACKED=false \
    "$@" bash "$SCRIPT" >"$STUB_DIR/out" 2>&1
  echo $? >"$STUB_DIR/rc"
}

# ── Assertions ────────────────────────────────────────────────────────────────────────────────────────
CASE=""
ok() { pass=$((pass + 1)); }
bad() { fail=$((fail + 1)); echo "not ok - ${CASE}: $*"; }
exit_is() { [ "$(cat "$STUB_DIR/rc")" = "$1" ] && ok || bad "exit $(cat "$STUB_DIR/rc"), expected $1"; }
out_has() { grep -qF -- "$1" "$STUB_DIR/out" && ok || bad "output lacks '$1'"; }
called() { grep -qE -- "$1" "$STUB_DIR/calls" && ok || bad "no call matching /$1/"; }
not_called() { grep -qE -- "$1" "$STUB_DIR/calls" && bad "unexpected call matching /$1/" || ok; }
nothing_destroyed() {
  not_called '^curl DELETE '
  not_called '^gcloud (run services delete|secrets delete|storage rm)'
  not_called '^aws '
}
# before A B: the first call matching A precedes the first call matching B.
before() {
  local a b
  a="$(grep -nE -- "$1" "$STUB_DIR/calls" | head -1 | cut -d: -f1)"
  b="$(grep -nE -- "$2" "$STUB_DIR/calls" | head -1 | cut -d: -f1)"
  [ -n "$a" ] && [ -n "$b" ] && [ "$a" -lt "$b" ] && ok || bad "/$1/ (line ${a:-none}) is not before /$2/ (line ${b:-none})"
}
dump_on_fail() { [ "$fail" -eq "$1" ] || { sed 's/^/    | /' "$STUB_DIR/out"; sed 's/^/    > /' "$STUB_DIR/calls"; }; }
t() { CASE="$1"; reset; }

# ── Cases ─────────────────────────────────────────────────────────────────────────────────────────────
t "dry run with nothing left reports nothing to delete"; f0=$fail
run_reap
exit_is 0; out_has "nothing to delete"; out_has "== Neon branch pr-217"; nothing_destroyed
dump_on_fail "$f0"

t "dry run lists PR 217's leftovers and deletes nothing"; f0=$fail
leftovers; run_reap
exit_is 0
out_has "would delete: Cloud Run service nekohoa-api-pr-217"
out_has "revision nekohoa-api-pr-217-00001-abc"
out_has "would delete: R2 bucket nekohoa-pr-217-documents (emptied first)"
out_has "would delete: secret pr-217-db-connection"
out_has "would delete: secret pr-217-stripe-webhook"
out_has "would delete: Neon branch pr-217 (br-pr217)"
out_has "endpoint ep-pr217"
nothing_destroyed
dump_on_fail "$f0"

t "dry run without credentials reports every provider not checked"; f0=$fail
touch "$STUB_DIR/gcloud_noauth"
run_reap NEON_API_KEY= CF_TOKEN= STRIPE_SECRET_KEY_TEST=
exit_is 0
out_has "not checked (no GCP credential)"; out_has "not checked (no Cloudflare credential)"
out_has "not checked (no Neon credential)"; out_has "Stripe not checked"
not_called '^curl '
dump_on_fail "$f0"

t "real reap deletes everything in order, state last"; f0=$fail
leftovers
route GET "${STRIPE}\\?" 200 '{"data":[{"id":"we_1","url":"https://nekohoa-api-pr-217-abc-uc.a.run.app/api/v1/payments/webhooks/stripe","metadata":{"pr":"217"}},{"id":"we_dev","url":"https://api.dev.nekohoa.com/x","metadata":{}}],"has_more":false}'
route DELETE "${STRIPE}/we_1\$" 200 '{"deleted":true}'
route GET "${CF}/pages/projects/nekohoa-dev/deployments\\?" 200 '{"result":[{"id":"dep1","deployment_trigger":{"metadata":{"branch":"pr-217"}}},{"id":"dep2","deployment_trigger":{"metadata":{"branch":"pr-2170"}}}],"result_info":{"total_pages":1}}'
route DELETE "${CF}/pages/projects/nekohoa-dev/deployments/dep1\\?force=true\$" 200 '{"success":true}'
route GET "${GCS}" 200 '{"items":[{"name":"state/pr/217/default.tfstate","generation":"11","timeCreated":"'"$OLD"'"},{"name":"state/pr/217/default.tflock","generation":"22","timeCreated":"'"$OLD"'"}]}'
run_reap DRY_RUN=false
exit_is 0
called '^gh api repos/nbon12/hoa_management_system/pulls/217 --jq .state$'
before '^gcloud run services delete nekohoa-api-pr-217 --region us-central1 --project nekohoa-dev --quiet$' '^aws s3 rm s3://nekohoa-pr-217-documents --recursive'
called '^aws s3 rm s3://nekohoa-pr-217-documents --recursive .*key-in-env=yes$'
before '^aws s3 rm' '^curl DELETE .*/r2/buckets/nekohoa-pr-217-documents'
before '^curl DELETE .*/r2/buckets/' '^gcloud secrets delete pr-217-db-connection --project nekohoa-dev --quiet$'
called '^gcloud secrets delete pr-217-stripe-webhook --project nekohoa-dev --quiet$'
before '^gcloud secrets delete' '^curl DELETE .*/branches/br-pr217 '
before '^curl DELETE .*/branches/br-pr217 ' '^curl DELETE .*/webhook_endpoints/we_1 '
not_called 'webhook_endpoints/we_dev'
before '^curl DELETE .*/webhook_endpoints/we_1 ' '^curl DELETE .*/deployments/dep1'
not_called 'deployments/dep2'
before '^curl DELETE .*/deployments/dep1' '^gcloud storage rm gs://nekohoa-dev-tfstate/state/pr/217/default.tfstate#11$'
called '^gcloud storage rm gs://nekohoa-dev-tfstate/state/pr/217/default.tflock#22$'
not_called 'auth=no'
not_called '^curl DELETE .*/branches/br-(dev|base)'
# Credentials never appear on a command line: tokens go through -H @fd, S3 keys through the environment.
grep -qE 'gcp-test-token|neon-test-token|cf-test-token|stripe-test-token|value-of-dev-storage-(access|secret)-key' "$STUB_DIR/argv" \
  && bad "a credential was passed in argv" || ok
dump_on_fail "$f0"

t "a service without label pr-number=217 is refused and the state kept"; f0=$fail
route GET "${RUN}\$" 200 '{"labels":{"pr-number":"999"},"createTime":"'"$OLD"'"}'
leftovers
route GET "${GCS}" 200 '{"items":[{"name":"state/pr/217/default.tfstate","generation":"11","timeCreated":"'"$OLD"'"}]}'
run_reap DRY_RUN=false
exit_is 1; out_has "nekohoa-api-pr-217 lacks label pr-number=217; not deleting it."
not_called '^gcloud run services delete'; not_called '^gcloud storage rm'
out_has "kept: an item above was refused or failed"
dump_on_fail "$f0"

t "a Neon branch not forked from pr-base is refused"; f0=$fail
route GET "${NEON}/branches\$" 200 '{"branches":[{"id":"br-pr217","name":"pr-217","parent_id":"br-dev","default":false}]}'
leftovers
run_reap DRY_RUN=false
exit_is 1; out_has "is not a child of pr-base"; not_called '^curl DELETE .*/branches/'
dump_on_fail "$f0"

t "two Neon branches named pr-217 are refused"; f0=$fail
route GET "${NEON}/branches\$" 200 '{"branches":[{"id":"br-a","name":"pr-217","parent_id":"br-base"},{"id":"br-b","name":"pr-217","parent_id":"br-base"}]}'
run_reap DRY_RUN=false
exit_is 1; out_has "more than one Neon branch is named pr-217: br-a, br-b"; not_called '^curl DELETE .*/branches/'
dump_on_fail "$f0"

t "a Stripe endpoint for PR 217 on another host is refused"; f0=$fail
route GET "${STRIPE}\\?" 200 '{"data":[{"id":"we_x","url":"https://evil.example.com/hook","metadata":{"pr":"217"}}],"has_more":false}'
run_reap DRY_RUN=false
exit_is 1; out_has "Stripe endpoint we_x has metadata.pr=217 but host evil.example.com"; not_called 'webhook_endpoints/we_x'
dump_on_fail "$f0"

t "Stripe listing follows has_more"; f0=$fail
route GET "${STRIPE}\\?limit=100&starting_after=we_9\$" 200 '{"data":[{"id":"we_2","url":"https://nekohoa-api-pr-217-x.a.run.app/h","metadata":{"pr":"217"}}],"has_more":false}'
route GET "${STRIPE}\\?limit=100\$" 200 '{"data":[{"id":"we_9","url":"https://other","metadata":{"pr":"5"}}],"has_more":true}'
run_reap
exit_is 0; out_has "would delete: Stripe webhook endpoint we_2"
dump_on_fail "$f0"

t "an open PR is refused unless ALLOW_OPEN_PR=true"; f0=$fail
leftovers; echo open >"$STUB_DIR/pr_state"
run_reap DRY_RUN=false
exit_is 1; out_has "PR #217 is 'open', not closed. Refusing to reap"; nothing_destroyed
reset; leftovers; echo open >"$STUB_DIR/pr_state"
run_reap DRY_RUN=false ALLOW_OPEN_PR=true
exit_is 0; called '^gcloud run services delete nekohoa-api-pr-217'; not_called '^gh '
dump_on_fail "$f0"

t "a state lock under 45 minutes old stops a real reap before any delete"; f0=$fail
leftovers
fresh="$(jq -rn 'now - 600 | strftime("%Y-%m-%dT%H:%M:%S.000Z")')"
route GET "${GCS}" 200 '{"items":[{"name":"state/pr/217/default.tflock","generation":"33","timeCreated":"'"$fresh"'"}]}'
run_reap DRY_RUN=false
exit_is 1; out_has "State lock 33 is under 45 min old"; nothing_destroyed
reset; leftovers
route GET "${GCS}" 200 '{"items":[{"name":"state/pr/217/default.tflock","generation":"33","timeCreated":"'"$fresh"'"}]}'
run_reap
exit_is 0; out_has "state lock 33 is 10 min old"
dump_on_fail "$f0"

t "a real reap that cannot list the state deletes nothing"; f0=$fail
route GET "${GCS}" 403 '{"error":{"code":403}}'
leftovers
run_reap DRY_RUN=false
exit_is 1; out_has "a live lock cannot be ruled out"; nothing_destroyed
dump_on_fail "$f0"

t "ONLY_UNTRACKED leaves tracked resources, Stripe, Pages and state alone"; f0=$fail
leftovers
printf '%s\n' module.pr_environment.google_cloud_run_v2_service.api \
  module.pr_environment.google_secret_manager_secret.db_connection \
  module.pr_environment.google_secret_manager_secret.stripe_webhook >"$STUB_DIR/state_list"
run_reap DRY_RUN=false ONLY_UNTRACKED=true REPO=
exit_is 0
called '^tofu state list$'
not_called '^gcloud run services delete'; not_called '^gcloud secrets delete'
called '^curl DELETE .*/r2/buckets/nekohoa-pr-217-documents'; called '^curl DELETE .*/branches/br-pr217 '
not_called 'webhook_endpoints'; not_called '/pages/'; not_called 'storage\.googleapis\.com'; not_called '^gh '
not_called '^gcloud storage rm'
dump_on_fail "$f0"

t "ONLY_UNTRACKED fails closed when tofu state list fails"; f0=$fail
leftovers; touch "$STUB_DIR/state_list_fail"
run_reap DRY_RUN=false ONLY_UNTRACKED=true
exit_is 1; out_has "tofu state list failed"; nothing_destroyed
dump_on_fail "$f0"

t "a real reap needs write tokens"; f0=$fail
run_reap DRY_RUN=false CF_TOKEN=
exit_is 1; nothing_destroyed
dump_on_fail "$f0"

t "bad inputs are rejected"; f0=$fail
run_reap PR_NUMBER='217;rm'
exit_is 2
run_reap DRY_RUN=maybe
exit_is 2
dump_on_fail "$f0"

t "the script names every resource the pr-environment module creates"; f0=$fail
# Reaped by name (the ONLY_UNTRACKED addresses) or deleted with one of them.
reaped="google_cloud_run_v2_service.api cloudflare_r2_bucket.documents google_secret_manager_secret.db_connection
google_secret_manager_secret.stripe_webhook neon_branch.pr"
cascaded="google_cloud_run_v2_service_iam_member.public google_secret_manager_secret_version.db_connection
google_secret_manager_secret_version.stripe_webhook_placeholder google_secret_manager_secret_iam_member.runtime_pr_accessor
neon_endpoint.pr"
for r in $reaped; do
  grep -qF "$r" "$SCRIPT" && ok || bad "script does not handle ${r}"
done
while read -r r; do
  case " $(echo "$reaped $cascaded" | tr '\n' ' ') " in
    *" $r "*) ok ;;
    *) bad "infra/modules/pr-environment creates ${r}; add it to scripts/pr-env-reap.sh (or to 'cascaded' here)" ;;
  esac
done < <(sed -nE 's/^resource "([^"]+)" "([^"]+)".*/\1.\2/p' "$ROOT"/infra/modules/pr-environment/*.tf)

echo "pr-env-reap tests: ${pass} passed, ${fail} failed"
[ "$fail" -eq 0 ]
