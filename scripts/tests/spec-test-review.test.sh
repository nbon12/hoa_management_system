#!/usr/bin/env bash
# Tests for scripts/spec-test-review.sh against a stubbed gh (no network) and throwaway git repos with a
# local bare remote. Each case seeds GitHub state or a working tree, runs one subcommand, and checks its exit
# code, the gh calls it made, what it pushed and what it wrote.
# Run: bash scripts/tests/spec-test-review.test.sh
# shellcheck disable=SC2015 # `check && ok || bad` is the assertion idiom; ok cannot fail
# shellcheck disable=SC2016 # backticks in fixtures are Markdown, not command substitution
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCRIPT="$ROOT/scripts/spec-test-review.sh"
STUB_DIR="$(mktemp -d)"
export STUB_DIR
trap 'rm -rf "$STUB_DIR"' EXIT
mkdir -p "$STUB_DIR/bin"
pass=0 fail=0

# ── Stub ──────────────────────────────────────────────────────────────────────────────────────────────
# gh api: a GET runs its --jq over a fixture picked by path (comments.json, issues.json). A write logs
# "=== METHOD PATH" and its fields (a body=@file field as the file's text) to bodies and prints an html_url.
# Every call logs "METHOD PATH" to calls.
cat >"$STUB_DIR/bin/gh" <<'EOF'
#!/usr/bin/env bash
[ "$1" = api ] || { echo "stub gh: unexpected $*" >&2; exit 1; }
shift
method=GET path="" jqexpr="" fields=()
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2 ;;
    --paginate) shift ;;
    --jq) jqexpr="$2"; shift 2 ;;
    -F | -f) fields+=("$2"); shift 2 ;;
    *) path="$1"; shift ;;
  esac
done
echo "$method $path" >>"$STUB_DIR/calls"
if [ "$method" != GET ]; then
  {
    echo "=== $method $path"
    for f in "${fields[@]}"; do
      case "$f" in body=@*) cat "${f#body=@}"; echo ;; *) echo "$f" ;; esac
    done
  } >>"$STUB_DIR/bodies"
  case "$method $path" in
    "POST repos/o/r/issues") echo "https://github.com/o/r/issues/99" ;;
    "PATCH repos/o/r/issues/"[0-9]*) echo "https://github.com/o/r/issues/${path##*/}" ;;
    *) echo "https://github.com/o/r/pull/7#issuecomment-1" ;;
  esac
  exit 0
fi
case "$path" in
  */issues/*/comments) fixture=comments.json ;;
  */issues\?*) fixture=issues.json ;;
  *) fixture=empty.json ;;
esac
jq -r "$jqexpr" "$STUB_DIR/$fixture"
EOF
chmod +x "$STUB_DIR/bin/gh"

ok() { pass=$((pass + 1)); echo "  ok   $1"; }
bad() { fail=$((fail + 1)); echo "  FAIL $1"; [ -n "${2:-}" ] && echo "       $2"; }

BOT='github-actions[bot]'
MARK='<!-- spec-test-review -->'
reset() {
  rm -f "$STUB_DIR"/{calls,bodies,out,ghout}
  touch "$STUB_DIR/calls" "$STUB_DIR/bodies" "$STUB_DIR/ghout"
  echo '[]' >"$STUB_DIR/comments.json"
  echo '[]' >"$STUB_DIR/issues.json"
  echo '[]' >"$STUB_DIR/empty.json"
  printf '### Blocking findings\n- **US3-AS6**: asserts 409 but not `APPLICATION_CLOSED`.\n' >"$STUB_DIR/report.md"
}
comments() { jq -n "$1" >"$STUB_DIR/comments.json"; }
issues() { jq -n "$1" >"$STUB_DIR/issues.json"; }
called() { grep -qxF "$1" "$STUB_DIR/calls"; }
not_called() { ! grep -qE "$1" "$STUB_DIR/calls"; }
bodies_have() { grep -qF -- "$1" "$STUB_DIR/bodies"; }
calls() { tr '\n' ';' <"$STUB_DIR/calls"; }

# ── scope ─────────────────────────────────────────────────────────────────────────────────────────────
scope_is() { # expected name paths...
  local want="$1" name="$2" got; shift 2
  got="$(printf '%s\n' "$@" | bash "$SCRIPT" scope)"
  [ "$got" = "$want" ] && ok "scope: $name" || bad "scope: $name" "want $want, got $got"
}
echo "scope"
scope_is skip "spec and patch notes only" specs/031-arc-review-refinements/spec.md docs/patch-notes.md
scope_is skip "Markdown anywhere" README.md neko-hoa/README.md .claude/commands/speckit.plan.md
scope_is skip "non-Markdown spec assets" specs/006-stripe-payments/design/screenshots/pay.png
scope_is skip "wireframes" "wireframes/NekoHOA Wireframes _standalone_.html"
scope_is skip "no paths" ""
scope_is review "backend code" HOAManagementCompany/Features/Arc/CastVote.cs
scope_is review "a test only" neko-hoa/src/app/features/arc/arc-table.component.spec.ts
scope_is review "spec plus code" specs/031-arc-review-refinements/tasks.md HOAManagementCompany/Domain/Entities/Vote.cs
scope_is review "a workflow" .github/workflows/test.yml

# ── guard and push: a repo whose feature branch is on a bare remote ──────────────────────────────────
REPO_DIR="$STUB_DIR/repo" REMOTE="$STUB_DIR/remote.git"
mkrepo() {
  rm -rf "$REPO_DIR" "$REMOTE"
  git -c init.defaultBranch=main init -q --bare "$REMOTE"
  git -c init.defaultBranch=main init -q "$REPO_DIR"
  (
    cd "$REPO_DIR" || exit 1
    git config user.email t@example.com && git config user.name t
    mkdir -p HOAManagementCompany.Tests HOAManagementCompany neko-hoa/src/app .claude
    echo 'Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);' >HOAManagementCompany.Tests/VoteTests.cs
    echo 'class VoteService {}' >HOAManagementCompany/VoteService.cs
    echo 'it("shows the badge", () => {});' >neko-hoa/src/app/badge.spec.ts
    echo '{}' >.claude/settings.json && echo '# rules' >CLAUDE.md
    git add -A && git commit -qm init && git push -q "$REMOTE" HEAD:refs/heads/feature
  )
  START="$(git -C "$REPO_DIR" rev-parse HEAD)"
}
edit() { printf '%s\n' "$2" >>"$REPO_DIR/$1"; } # path line
run_guard() { (cd "$REPO_DIR" && bash "$SCRIPT" guard) >"$STUB_DIR/out" 2>&1; }
out_is() { [ "$(cat "$STUB_DIR/out")" = "$1" ]; }

echo "guard"
mkrepo; edit HOAManagementCompany.Tests/VoteTests.cs 'Assert.Equal(ArcErrorCodes.ApplicationClosed, code);'
run_guard; rc=$?
[ "$rc" = 0 ] && out_is HOAManagementCompany.Tests/VoteTests.cs && ok "a changed test is allowed" \
  || bad "a changed test is allowed" "rc=$rc $(cat "$STUB_DIR/out")"
mkrepo; edit HOAManagementCompany/VoteService.cs '// tweak'; edit HOAManagementCompany.Tests/VoteTests.cs '// x'
run_guard; rc=$?
[ "$rc" = 1 ] && grep -q 'outside test code: HOAManagementCompany/VoteService.cs' "$STUB_DIR/out" \
  && ok "a production change is refused" || bad "a production change is refused" "rc=$rc $(cat "$STUB_DIR/out")"
mkrepo; edit .claude/settings.json '{"x":1}'; edit CLAUDE.md 'main rules'; edit HOAManagementCompany.Tests/VoteTests.cs '// x'
run_guard; rc=$?
[ "$rc" = 0 ] && out_is HOAManagementCompany.Tests/VoteTests.cs && ok "restored agent config is ignored, not committed" \
  || bad "restored agent config is ignored, not committed" "rc=$rc $(cat "$STUB_DIR/out")"
mkrepo; edit HOAManagementCompany.Tests/VoteTests.cs '// x'
mkdir -p "$REPO_DIR/HOAManagementCompany.Tests/TestResults" && echo '<trx/>' >"$REPO_DIR/HOAManagementCompany.Tests/TestResults/run.trx"
run_guard; rc=$?
[ "$rc" = 0 ] && out_is HOAManagementCompany.Tests/VoteTests.cs && ok "test-run output is ignored, not committed" \
  || bad "test-run output is ignored, not committed" "rc=$rc $(cat "$STUB_DIR/out")"
mkrepo; printf 'it("hides undo", () => { expect(1).toBe(1); });\n' >"$REPO_DIR/neko-hoa/src/app/undo.spec.ts"
run_guard; rc=$?
[ "$rc" = 0 ] && out_is neko-hoa/src/app/undo.spec.ts && ok "a new spec file is allowed" \
  || bad "a new spec file is allowed" "rc=$rc $(cat "$STUB_DIR/out")"
mkrepo; edit HOAManagementCompany.Tests/VoteTests.cs '[Fact(Skip = "flaky")]'
run_guard; rc=$?
[ "$rc" = 1 ] && grep -q 'skipped or focused' "$STUB_DIR/out" && ok "an xUnit Skip is refused" \
  || bad "an xUnit Skip is refused" "rc=$rc $(cat "$STUB_DIR/out")"
mkrepo; printf 'it.only("hides undo", () => {});\n' >"$REPO_DIR/neko-hoa/src/app/undo.spec.ts"
run_guard; rc=$?
[ "$rc" = 1 ] && ok "a focused test in a new file is refused" || bad "a focused test in a new file is refused" "rc=$rc"
mkrepo; edit neko-hoa/src/app/badge.spec.ts 'xit("pending", () => {});'
run_guard; rc=$?
[ "$rc" = 1 ] && ok "xit is refused" || bad "xit is refused" "rc=$rc"
mkrepo; run_guard; rc=$?
[ "$rc" = 0 ] && out_is "" && ok "no changes: nothing to commit" || bad "no changes: nothing to commit" "rc=$rc"

run_push() { # FIX_OUTPUT [HEAD_SHA]
  (cd "$REPO_DIR" && PATH="$STUB_DIR/bin:$PATH" GITHUB_OUTPUT="$STUB_DIR/ghout" REPO=o/r PR=7 HEAD_REF=feature \
    HEAD_SHA="${2:-$START}" ATTEMPT=1 MAX_FIX_ATTEMPTS=3 MERGE_TOKEN=t PUSH_URL="$REMOTE" FIX_OUTPUT="$1" \
    bash "$SCRIPT" push) >"$STUB_DIR/out" 2>&1
}
remote_head() { git -C "$REMOTE" rev-parse refs/heads/feature; }
remote_msg() { git -C "$REMOTE" log -1 --format=%B refs/heads/feature; }
remote_files() { git -C "$REMOTE" show --name-only --format= refs/heads/feature | tr '\n' ' '; }
ghout_has() { grep -qF -- "$1" "$STUB_DIR/ghout"; }
FIXED='{"fixed":["US3-AS6"],"disputes":[],"unresolved":[],"summary":"Restored the APPLICATION_CLOSED assertion."}'

echo "push"
reset; mkrepo; edit HOAManagementCompany.Tests/VoteTests.cs 'Assert.Equal(ArcErrorCodes.ApplicationClosed, code);'
edit .claude/settings.json '{"restored":true}'
run_push "$FIXED"; rc=$?
[ "$rc" = 0 ] && [ "$(remote_head)" != "$START" ] && ghout_has pushed=true \
  && ok "a fix is committed and pushed" || bad "a fix is committed and pushed" "rc=$rc $(cat "$STUB_DIR/out")"
[ "$(remote_files)" = "HOAManagementCompany.Tests/VoteTests.cs " ] && ok "only the test is committed, not restored config" \
  || bad "only the test is committed, not restored config" "$(remote_files)"
remote_msg | grep -qx 'Spec-Test-Review-Fix: 1' && remote_msg | grep -q 'Fixed: US3-AS6' \
  && ok "the commit carries the attempt trailer" || bad "the commit carries the attempt trailer" "$(remote_msg)"
called "POST repos/o/r/issues/7/comments" && bodies_have '**Fixed:** US3-AS6' \
  && ok "the attempt is announced on the PR" || bad "the attempt is announced on the PR" "$(calls)"

reset; mkrepo
run_push '{"fixed":[],"disputes":[{"id":"US3-AS4","evidence":"VoteTests.cs:120 casts after the lock and asserts APPLICATION_DECIDED."}],"unresolved":[],"summary":"One finding is wrong."}'
[ "$(remote_head)" != "$START" ] && [ -z "$(remote_files)" ] && ghout_has pushed=true \
  && bodies_have '- **US3-AS4**: VoteTests.cs:120 casts after the lock' \
  && ok "a dispute alone pushes an empty commit to start the next review" \
  || bad "a dispute alone pushes an empty commit to start the next review" "$(cat "$STUB_DIR/out")"

reset; mkrepo
run_push '{"fixed":[],"disputes":[],"unresolved":[{"id":"US3-AS2","reason":"The faithful test fails: the decision is not withdrawn."}],"summary":"x"}'
[ "$(remote_head)" = "$START" ] && ghout_has pushed=false && ghout_has 'US3-AS2**: The faithful test fails' \
  && not_called '^POST' && ok "nothing fixed or disputed: no push, the reasons go on" \
  || bad "nothing fixed or disputed: no push, the reasons go on" "$(cat "$STUB_DIR/out")"

reset; mkrepo; edit HOAManagementCompany/VoteService.cs '// changed'
run_push "$FIXED"
[ "$(remote_head)" = "$START" ] && ghout_has pushed=false && ghout_has 'was not pushed: the fix changed files outside test code' \
  && ok "a guard failure pushes nothing and says why" || bad "a guard failure pushes nothing and says why" "$(cat "$STUB_DIR/ghout")"

reset; mkrepo; edit HOAManagementCompany.Tests/VoteTests.cs '// fix'
(cd "$REPO_DIR" && git commit -qam "author pushed" && git push -q "$REMOTE" HEAD:refs/heads/feature && git reset -q --hard "$START")
moved="$(remote_head)"; edit HOAManagementCompany.Tests/VoteTests.cs '// fix'
run_push "$FIXED"
[ "$(remote_head)" = "$moved" ] && ghout_has superseded=true && not_called '^POST' \
  && ok "a branch that moved on is left to its own run" || bad "a branch that moved on is left to its own run" "$(cat "$STUB_DIR/out")"

reset; mkrepo
run_push ""
[ "$(remote_head)" != "$START" ] && ghout_has pushed=true && remote_msg | grep -q 'did not finish' \
  && ok "a crashed attempt pushes an empty commit so the next pass retries" \
  || bad "a crashed attempt pushes an empty commit so the next pass retries" "$(cat "$STUB_DIR/out")"

# ── post ──────────────────────────────────────────────────────────────────────────────────────────────
run_post() { # verdict
  PATH="$STUB_DIR/bin:$PATH" REPO=o/r PR=7 VERDICT="$1" REPORT_FILE="$STUB_DIR/report.md" \
    HEAD_SHA=abc1234def5678 RUN_URL=https://github.com/o/r/actions/runs/42 \
    bash "$SCRIPT" post >"$STUB_DIR/out" 2>&1
}
echo "post"
reset
run_post findings; rc=$?
[ "$rc" = 0 ] && called "POST repos/o/r/issues/7/comments" \
  && ok "findings with no earlier report: creates the comment" \
  || bad "findings with no earlier report: creates the comment" "rc=$rc calls=$(calls)"
sed -n 2p "$STUB_DIR/bodies" | grep -qxF "$MARK" && ok "body starts with the marker" || bad "body starts with the marker"
bodies_have '## Spec test review: ❌ Findings' && bodies_have 'asserts 409 but not `APPLICATION_CLOSED`' \
  && ok "body has the verdict heading and the report" || bad "body has the verdict heading and the report"
bodies_have 'Commit abc1234 · [run](https://github.com/o/r/actions/runs/42)' \
  && ok "footer names the short commit and links the run" || bad "footer names the short commit and links the run"

reset
comments "[{id: 111, user: {login: \"$BOT\"}, body: \"$MARK\nold\"}, {id: 222, user: {login: \"nbon12\"}, body: \"hi\"}]"
run_post pass
called "PATCH repos/o/r/issues/comments/111" && not_called '^POST' \
  && ok "an earlier report is edited in place" || bad "an earlier report is edited in place" "$(calls)"
bodies_have '## Spec test review: ✅ Pass' && ok "pass heading" || bad "pass heading"

reset
comments "[{id: 333, user: {login: \"nbon12\"}, body: \"quoting $MARK in a reply\"}]"
run_post findings
called "POST repos/o/r/issues/7/comments" && not_called '^PATCH' \
  && ok "a person's comment containing the marker is not taken over" \
  || bad "a person's comment containing the marker is not taken over" "$(calls)"

reset
run_post not_applicable; rc=$?
[ "$rc" = 0 ] && not_called '^(POST|PATCH)' && grep -q 'nothing posted' "$STUB_DIR/out" \
  && ok "not applicable with no earlier report: posts nothing" \
  || bad "not applicable with no earlier report: posts nothing" "rc=$rc $(cat "$STUB_DIR/out")"

reset
run_post maybe; rc=$?
[ "$rc" = 2 ] && [ ! -s "$STUB_DIR/calls" ] && ok "an unknown verdict fails before calling GitHub" \
  || bad "an unknown verdict fails before calling GitHub" "rc=$rc"

reset
head -c 70000 /dev/zero | tr '\0' 'x' >"$STUB_DIR/report.md"
run_post findings
size="$(wc -c <"$STUB_DIR/bodies" | tr -d ' ')"
[ "$size" -le 65536 ] && bodies_have 'The report was cut to fit a comment' \
  && ok "an oversized report is cut to fit a comment ($size bytes)" \
  || bad "an oversized report is cut to fit a comment" "size=$size"

# ── decide ────────────────────────────────────────────────────────────────────────────────────────────
DEFERRED_7='{number: 77, body: "<!-- spec-test-review-deferred: #7 -->\nold"}'
DEFERRED_8='{number: 78, body: "<!-- spec-test-review-deferred: #8 -->\nother PR"}'
A_PR='{number: 7, body: "<!-- spec-test-review-deferred: #7 -->", pull_request: {url: "x"}}'
run_decide() { # VAR=value...
  env PATH="$STUB_DIR/bin:$PATH" REPO=o/r PR=7 PR_TITLE='test(031): weaken a test' HEAD_SHA=abc1234def5678 \
    RUN_URL=https://github.com/o/r/actions/runs/42 REPORT_FILE="$STUB_DIR/report.md" ATTEMPT=2 MAX_FIX_ATTEMPTS=3 \
    REVIEW_RESULT=success VERDICT=findings PUSHED=false SUPERSEDED=false DRAFT=false FORK=false DEPENDABOT=false \
    UNRESOLVED='- **US3-AS2**: the faithful test fails.' "$@" bash "$SCRIPT" decide >"$STUB_DIR/out" 2>&1
}
echo "decide"
reset; issues "[$DEFERRED_7, $DEFERRED_8, $A_PR]"
run_decide VERDICT=pass; rc=$?
[ "$rc" = 0 ] && called "PATCH repos/o/r/issues/77" && called "POST repos/o/r/issues/77/comments" \
  && not_called 'issues/78' && called "POST repos/o/r/issues/7/comments" \
  && ok "a pass posts the report and closes this PR's follow-up issue only" \
  || bad "a pass posts the report and closes this PR's follow-up issue only" "rc=$rc $(calls)"
bodies_have 'state=closed' && ok "the follow-up issue is closed" || bad "the follow-up issue is closed"

reset
run_decide PUSHED=true; rc=$?
[ "$rc" = 1 ] && bodies_have 'Auto-fix attempt 2 of 3 pushed a commit' && not_called '^POST repos/o/r/issues$' \
  && ok "findings with a fix pushed: fail this commit, no issue" \
  || bad "findings with a fix pushed: fail this commit, no issue" "rc=$rc $(calls)"

reset
run_decide; rc=$?
[ "$rc" = 0 ] && called "POST repos/o/r/issues" && bodies_have 'title=Spec test gaps deferred from #7: test(031): weaken a test' \
  && bodies_have '<!-- spec-test-review-deferred: #7 -->' && bodies_have '- **US3-AS2**: the faithful test fails.' \
  && ok "the last pass files a follow-up issue and passes" \
  || bad "the last pass files a follow-up issue and passes" "rc=$rc $(calls) $(cat "$STUB_DIR/out")"
bodies_have '## Spec test review: ⚠️ Deferred' && bodies_have '[The follow-up issue](https://github.com/o/r/issues/99)' \
  && ok "the report links the follow-up issue" || bad "the report links the follow-up issue"

reset; issues "[$DEFERRED_7]"
run_decide; rc=$?
[ "$rc" = 0 ] && called "PATCH repos/o/r/issues/77" && not_called '^POST repos/o/r/issues$' \
  && ok "a later last pass updates the same follow-up issue" \
  || bad "a later last pass updates the same follow-up issue" "rc=$rc $(calls)"

reset
run_decide SUPERSEDED=true; rc=$?
[ "$rc" = 1 ] && not_called '^(POST|PATCH)' && ok "a superseded commit fails quietly" \
  || bad "a superseded commit fails quietly" "rc=$rc $(calls)"

reset
run_decide REVIEW_RESULT=failure VERDICT=; rc=$?
[ "$rc" = 0 ] && called "POST repos/o/r/issues" && bodies_have "The review couldn't run on this commit, twice" \
  && ok "a review that crashed twice is filed and passes" \
  || bad "a review that crashed twice is filed and passes" "rc=$rc $(calls)"

reset
run_decide REVIEW_RESULT=skipped VERDICT= DRAFT=true; rc=$?
[ "$rc" = 0 ] && [ ! -s "$STUB_DIR/calls" ] && ok "a draft passes without a review" || bad "a draft passes without a review" "rc=$rc"

reset
run_decide REVIEW_RESULT=skipped VERDICT= FORK=true; rc=$?
[ "$rc" = 1 ] && ok "a fork PR fails: it can't be reviewed" || bad "a fork PR fails: it can't be reviewed" "rc=$rc"

reset
run_decide VERDICT=; rc=$?
[ "$rc" = 0 ] && [ ! -s "$STUB_DIR/calls" ] && ok "a docs-only PR passes without a comment" \
  || bad "a docs-only PR passes without a comment" "rc=$rc $(calls)"

echo
echo "$pass passed, $fail failed"
[ "$fail" -eq 0 ]
