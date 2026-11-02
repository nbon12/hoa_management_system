#!/usr/bin/env bash
# Tests for scripts/spec-test-review.sh against a stubbed gh (no network). `scope` cases feed changed paths
# and check the decision; `post` cases seed the PR's existing comments and check which call is made and
# what the comment body says.
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
# gh api: a read runs its --jq over $STUB_DIR/comments.json; a write (-F body=@file) copies the body to
# $STUB_DIR/body. Logs "METHOD PATH" to calls.
cat >"$STUB_DIR/bin/gh" <<'EOF'
#!/usr/bin/env bash
[ "$1" = api ] || { echo "stub gh: unexpected $*" >&2; exit 1; }
shift
method=GET path="" jqexpr="" bodyfile=""
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2 ;;
    --paginate) shift ;;
    --jq) jqexpr="$2"; shift 2 ;;
    -F) bodyfile="${2#body=@}"; shift 2 ;;
    *) path="$1"; shift ;;
  esac
done
echo "$method $path" >>"$STUB_DIR/calls"
if [ -n "$bodyfile" ]; then
  cp "$bodyfile" "$STUB_DIR/body"
  echo "https://github.com/o/r/pull/7#issuecomment-1"
  exit 0
fi
jq -r "$jqexpr" "$STUB_DIR/comments.json"
EOF
chmod +x "$STUB_DIR/bin/gh"

ok() { pass=$((pass + 1)); echo "  ok   $1"; }
bad() { fail=$((fail + 1)); echo "  FAIL $1"; [ -n "${2:-}" ] && echo "       $2"; }

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

# ── post ──────────────────────────────────────────────────────────────────────────────────────────────
BOT='github-actions[bot]'
MARK='<!-- spec-test-review -->'
reset() {
  rm -f "$STUB_DIR"/{calls,body,out}
  touch "$STUB_DIR/calls"
  echo '[]' >"$STUB_DIR/comments.json"
  printf '### Findings\n- **US3-AS4**: asserts 409 but not `APPLICATION_DECIDED`.\n' >"$STUB_DIR/report.md"
}
comments() { jq -n "$1" >"$STUB_DIR/comments.json"; }
run_post() { # verdict
  PATH="$STUB_DIR/bin:$PATH" REPO=o/r PR=7 VERDICT="$1" REPORT_FILE="$STUB_DIR/report.md" \
    HEAD_SHA=abc1234def5678 RUN_URL=https://github.com/o/r/actions/runs/42 \
    bash "$SCRIPT" post >"$STUB_DIR/out" 2>&1
}
called() { grep -qxF "$1" "$STUB_DIR/calls"; }
body_has() { grep -qF -- "$1" "$STUB_DIR/body"; }

echo "post"
reset
run_post findings; rc=$?
[ "$rc" = 0 ] && called "POST repos/o/r/issues/7/comments" \
  && ok "findings with no earlier report: creates the comment" \
  || bad "findings with no earlier report: creates the comment" "rc=$rc calls=$(tr '\n' ';' <"$STUB_DIR/calls")"
head -n 1 "$STUB_DIR/body" | grep -qxF "$MARK" && ok "body starts with the marker" || bad "body starts with the marker"
body_has '## Spec test review: ❌ Findings' && body_has 'asserts 409 but not `APPLICATION_DECIDED`' \
  && ok "body has the verdict heading and the report" || bad "body has the verdict heading and the report"
body_has 'Commit abc1234 · [run](https://github.com/o/r/actions/runs/42)' \
  && ok "footer names the short commit and links the run" || bad "footer names the short commit and links the run"

reset
comments "[{id: 111, user: {login: \"$BOT\"}, body: \"$MARK\nold\"}, {id: 222, user: {login: \"nbon12\"}, body: \"hi\"}]"
run_post pass
called "PATCH repos/o/r/issues/comments/111" && ! grep -q '^POST' "$STUB_DIR/calls" \
  && ok "an earlier report is edited in place" || bad "an earlier report is edited in place" "$(tr '\n' ';' <"$STUB_DIR/calls")"
body_has '## Spec test review: ✅ Pass' && ok "pass heading" || bad "pass heading"

reset
comments "[{id: 333, user: {login: \"nbon12\"}, body: \"quoting $MARK in a reply\"}]"
run_post findings
called "POST repos/o/r/issues/7/comments" && ! grep -q '^PATCH' "$STUB_DIR/calls" \
  && ok "a person's comment containing the marker is not taken over" \
  || bad "a person's comment containing the marker is not taken over" "$(tr '\n' ';' <"$STUB_DIR/calls")"

reset
run_post not_applicable; rc=$?
[ "$rc" = 0 ] && ! grep -qE '^(POST|PATCH)' "$STUB_DIR/calls" && grep -q 'nothing posted' "$STUB_DIR/out" \
  && ok "not applicable with no earlier report: posts nothing" \
  || bad "not applicable with no earlier report: posts nothing" "rc=$rc $(cat "$STUB_DIR/out")"

reset
comments "[{id: 444, user: {login: \"$BOT\"}, body: \"$MARK\n❌\"}]"
run_post not_applicable
called "PATCH repos/o/r/issues/comments/444" && body_has '➖ Not applicable' \
  && ok "not applicable with an earlier report: updates it" || bad "not applicable with an earlier report: updates it"

reset
run_post maybe; rc=$?
[ "$rc" = 2 ] && [ ! -s "$STUB_DIR/calls" ] && ok "an unknown verdict fails before calling GitHub" \
  || bad "an unknown verdict fails before calling GitHub" "rc=$rc"

reset
head -c 70000 /dev/zero | tr '\0' 'x' >"$STUB_DIR/report.md"
run_post findings
size="$(wc -c <"$STUB_DIR/body" | tr -d ' ')"
[ "$size" -le 65536 ] && body_has 'The report was cut to fit a comment' \
  && ok "an oversized report is cut to fit a comment ($size bytes)" \
  || bad "an oversized report is cut to fit a comment" "size=$size"

echo
echo "$pass passed, $fail failed"
[ "$fail" -eq 0 ]
