#!/usr/bin/env bash
# Deterministic halves of .github/workflows/spec-test-review.yml (issue #302). Claude reviews and fixes the
# tests; this script decides what runs, guards and pushes the fixes, and turns the result into the check.
#
#   scope   Reads the PR's changed paths on stdin and prints "review", or "skip" when every path is
#           documentation: Markdown, docs/, specs/ or wireframes/. Spec Kit artifact PRs land before the
#           code, so there are no tests to hold to the spec yet (CLAUDE.md, "Spec Kit workflow: PR watching").
#   patch   In the fixer's checkout: writes its changes, minus the agent config claude-code-action
#           restores from main, as a binary patch to $2. The push job applies it to a clean checkout, so
#           nothing the fixer or the tests it ran left in that tree (hooks, git config) reaches the token.
#   guard   Prints the changed paths a fix may commit: test code, minus agent config and test-run output.
#           Fails when the fix changed anything else, skipped or focused a test, or removed more test
#           declarations or assertions than it added.
#   push    In a clean checkout of the PR head: applies the fixer's patch (PATCH_FILE), guards it, commits
#           with a Spec-Test-Review-Fix trailer, posts the report and then the attempt's comment, and
#           pushes, which starts the next review. A crashed attempt pushes an empty commit so the next pass
#           retries. Writes pushed, superseded and unresolved to $GITHUB_OUTPUT. Env: REPO PR HEAD_REF
#           HEAD_SHA ATTEMPT MAX_FIX_ATTEMPTS MERGE_TOKEN FIX_OUTPUT (the fixer's JSON) FIX_RESULT PATCH_FILE
#           REPORT_FILE RUN_URL; PUSH_URL overrides the remote (tests).
#   decide  The Spec test verdict check: exits 0 (pass) or 1 (fail). A pass with findings left is the
#           last one: it files them in a follow-up issue and passes, so the loop always ends green. A later
#           green pass closes that issue. Env: REPO PR PR_TITLE HEAD_SHA RUN_URL REVIEW_RESULT VERDICT
#           REPORT_FILE DRAFT FORK DEPENDABOT FIX_RESULT PUSH_RESULT PUSHED SUPERSEDED UNRESOLVED.
#   post    Upserts the sticky report comment. Env: REPO PR VERDICT (pass|findings|not_applicable|
#           deferred) REPORT_FILE HEAD_SHA RUN_URL [NOTE] [MAX_FIX_ATTEMPTS].
#
# gh must be authenticated (GH_TOKEN). Comments are found by marker and author (BOT_LOGIN,
# github-actions[bot] for GITHUB_TOKEN), so a re-run edits them in place.
# shellcheck disable=SC2016 # backticks in comment text are Markdown, not command substitution
set -euo pipefail

MARKER='<!-- spec-test-review -->'
MAX_BODY=60000 # GitHub refuses comment and issue bodies over 65,536 characters; the rest is framing
# Agent config claude-code-action restores from main on PRs (src/github/operations/restore-config.ts).
CONFIG_PATHS_RE='^(\.claude/|\.claude-pr/|\.mcp\.json$|\.claude\.json$|\.gitmodules$|\.ripgreprc$|CLAUDE\.md$|CLAUDE\.local\.md$|\.husky/)'
CONFIG_EXCLUDES=(':(exclude).claude' ':(exclude).claude-pr' ':(exclude).mcp.json' ':(exclude).claude.json'
  ':(exclude).gitmodules' ':(exclude).ripgreprc' ':(exclude)CLAUDE.md' ':(exclude)CLAUDE.local.md' ':(exclude).husky')
TEST_PATHS_RE='^(HOAManagementCompany\.Tests/|neko-hoa/e2e/|neko-hoa/cypress/|neko-hoa/src/.*\.spec\.ts$)'
BUILD_OUTPUT_RE='(^|/)(TestResults|bin|obj)/' # test-run output that isn't ignored everywhere
# Skipped or focused tests: xUnit (Skip =, Xunit.SkippableFact's Skip.If/IfNot/SkipException), Jasmine,
# Playwright and Cypress.
SKIP_RE='(Fact|Theory)\([^]]*Skip[[:space:]]*=|Skip\.If(Not)?\(|SkipException|(^|[^[:alnum:]_.])(xit|xdescribe|fit|fdescribe|pending)\(|\.(skip|only|fixme)\('
DECL_RE='(^|[[,[:space:]])(Skippable)?(Fact|Theory)[]( ,]|(^|[^[:alnum:]_.])(it|test)\('
ASSERT_RE='Assert\.|expect\(|\.Should\(|\.Verify\('
GIT=(git -c core.hooksPath=/dev/null)

out() { if [ -n "${GITHUB_OUTPUT:-}" ]; then printf '%s\n' "$@" >>"$GITHUB_OUTPUT"; fi; }
out_multi() { # name value
  [ -n "${GITHUB_OUTPUT:-}" ] || return 0
  local delim="EOF_$RANDOM$RANDOM$RANDOM"
  printf '%s<<%s\n%s\n%s\n' "$1" "$delim" "$2" "$delim" >>"$GITHUB_OUTPUT"
}
count() { grep -cE "$1" <<<"$2" || true; } # regex text

scope() {
  local path
  while IFS= read -r path; do
    case "$path" in
      '' | *.md | docs/* | specs/* | wireframes/*) ;;
      *) echo review; return ;;
    esac
  done
  echo skip
}

make_patch() {
  local dest="${1:?usage: patch OUT}"
  # Stage everything, then leave the agent config out when diffing: naming an ignored path (the action
  # ignores .claude-pr/) in `git add`, even to exclude it, is an error.
  "${GIT[@]}" add -A .
  "${GIT[@]}" diff --cached --binary --no-textconv --no-ext-diff -- . "${CONFIG_EXCLUDES[@]}" >"$dest"
}

# Changed and untracked paths, one per line; both sides of a rename.
changed_paths() {
  local entry status
  "${GIT[@]}" status --porcelain=v1 -z --untracked-files=all | while IFS= read -r -d '' entry; do
    status="${entry:0:2}"
    printf '%s\n' "${entry:3}"
    case "$status" in R* | C*) IFS= read -r -d '' entry && printf '%s\n' "$entry" ;; esac
  done
}

guard() {
  local path diff plus="" minus="" hits
  local -a ok=() bad=()
  while IFS= read -r path; do
    if [[ "$path" =~ $CONFIG_PATHS_RE ]] || [[ "$path" =~ $BUILD_OUTPUT_RE ]]; then continue
    elif [[ "$path" =~ $TEST_PATHS_RE ]]; then ok+=("$path")
    else bad+=("$path"); fi
  done < <(changed_paths)
  if [ "${#bad[@]}" -gt 0 ]; then
    echo "the fix changed files outside test code: ${bad[*]}" >&2
    return 1
  fi
  [ "${#ok[@]}" -gt 0 ] || return 0
  for path in "${ok[@]}"; do
    if "${GIT[@]}" ls-files --error-unmatch -- "$path" >/dev/null 2>&1; then
      diff="$("${GIT[@]}" diff -U0 --no-textconv --no-ext-diff HEAD -- "$path")"
      plus+="$(grep '^+' <<<"$diff" | grep -v '^+++' || true)"$'\n'
      minus+="$(grep '^-' <<<"$diff" | grep -v '^---' || true)"$'\n'
    elif [ -f "$path" ]; then
      plus+="$(cat -- "$path")"$'\n'
    fi
  done
  if hits="$(grep -E "$SKIP_RE" <<<"$plus")"; then
    echo "the fix added a skipped or focused test: $(head -n 3 <<<"$hits" | tr '\n' ' ')" >&2
    return 1
  fi
  # Moving a test between files is fine; ending with fewer tests or assertions is not.
  if [ "$(count "$DECL_RE" "$minus")" -gt "$(count "$DECL_RE" "$plus")" ] ||
    [ "$(count "$ASSERT_RE" "$minus")" -gt "$(count "$ASSERT_RE" "$plus")" ]; then
    echo "the fix removed more test declarations or assertions than it added" >&2
    return 1
  fi
  printf '%s\n' "${ok[@]}"
}

push() {
  : "${REPO:?}" "${PR:?}" "${HEAD_REF:?}" "${HEAD_SHA:?}" "${ATTEMPT:?}" "${MAX_FIX_ATTEMPTS:?}" "${REPORT_FILE:?}" "${RUN_URL:?}"
  if [ -z "${PUSH_URL:-}" ] && [ -z "${MERGE_TOKEN:-}" ]; then
    out pushed=false
    out_multi unresolved "- The auto-fix couldn't push: the MERGE_TOKEN secret isn't set."
    return 0
  fi
  local url="${PUSH_URL:-https://x-access-token:${MERGE_TOKEN}@github.com/${REPO}.git}"
  local fixed="" disputes="" unresolved="" summary="" paths="" why="" crashed=no remote body sha id err
  # A failed fix job (FIX_RESULT) counts as a crash too: its patch may be missing even when its JSON isn't.
  if [ "${FIX_RESULT:-success}" = success ] && [ -n "${FIX_OUTPUT:-}" ] && jq -e 'type == "object"' >/dev/null 2>&1 <<<"$FIX_OUTPUT"; then
    fixed="$(jq -r '[.fixed[]?] | join(", ")' <<<"$FIX_OUTPUT")"
    disputes="$(jq -r '.disputes[]? | "- **\(.id)**: \(.evidence)"' <<<"$FIX_OUTPUT")"
    unresolved="$(jq -r '.unresolved[]? | "- **\(.id)**: \(.reason)"' <<<"$FIX_OUTPUT")"
    summary="$(jq -r '.summary // ""' <<<"$FIX_OUTPUT")"
  else
    crashed=yes
  fi
  err="$(mktemp)"
  if [ "$crashed" = no ] && [ -s "${PATCH_FILE:-}" ] && ! "${GIT[@]}" apply --whitespace=nowarn "$PATCH_FILE" 2>"$err"; then
    why="its patch didn't apply ($(head -c 300 "$err" | tr '\n' ' '))"
  elif [ "$crashed" = no ] && ! paths="$(guard 2>"$err")"; then
    why="$(cat "$err")"
  fi
  rm -f "$err"
  # A refused attempt uses up its attempt but not the loop: an empty commit (with any disputes) starts
  # the next pass, which sees why in this attempt's comment.
  if [ -n "$why" ]; then
    why="The changes were not pushed: ${why}. Only test code may change, no test may be skipped or focused, and no test or assertion may be removed."
    paths=""
  fi
  [ -n "$paths" ] || fixed=""
  out_multi unresolved "$unresolved"
  if [ "$crashed" = no ] && [ -z "$why" ] && [ -z "$paths" ] && [ -z "$disputes" ]; then
    echo "spec-test-review: nothing to push; this is the last pass"
    out pushed=false
    return 0
  fi
  remote="$("${GIT[@]}" ls-remote "$url" "refs/heads/$HEAD_REF" | cut -f1)"
  if [ "$remote" != "$HEAD_SHA" ]; then
    echo "spec-test-review: $HEAD_REF moved on (now ${remote:-gone}); its own run reviews it"
    out pushed=false superseded=true
    return 0
  fi

  if [ -n "$paths" ]; then
    local -a staged=()
    local p
    while IFS= read -r p; do staged+=("$p"); done <<<"$paths"
    "${GIT[@]}" add -A -- "${staged[@]}"
  fi
  {
    if [ "$crashed" = yes ]; then
      printf 'test(spec-test-review): retry after auto-fix attempt %s of %s for #%s\n\n' "$ATTEMPT" "$MAX_FIX_ATTEMPTS" "$PR"
      printf 'The attempt did not finish, so this empty commit starts the next pass.\n'
    elif [ -n "$why" ]; then
      printf 'test(spec-test-review): retry after refused auto-fix attempt %s of %s for #%s\n\n%s\n' "$ATTEMPT" "$MAX_FIX_ATTEMPTS" "$PR" "$why"
    else
      printf 'test(spec-test-review): auto-fix attempt %s of %s for #%s\n\n%s\n' "$ATTEMPT" "$MAX_FIX_ATTEMPTS" "$PR" "$summary"
      [ -z "$fixed" ] || printf '\nFixed: %s\n' "$fixed"
    fi
    printf '\nSpec-Test-Review-Fix: %s\n' "$ATTEMPT"
  } | "${GIT[@]}" -c user.name='github-actions[bot]' -c user.email='41898282+github-actions[bot]@users.noreply.github.com' \
    commit -q --allow-empty -F -
  sha="$("${GIT[@]}" rev-parse HEAD)"

  # The report first, then this attempt's comment, then the push: the push starts the next review, and
  # that review reads the replies after the latest report for disputes.
  VERDICT=findings NOTE="Auto-fix attempt $ATTEMPT of $MAX_FIX_ATTEMPTS is pushing ${sha:0:7}. The review runs again on it." post >/dev/null
  body="$(mktemp)"
  {
    printf '**Spec test auto-fix**, attempt %s of %s: commit %s\n\n' "$ATTEMPT" "$MAX_FIX_ATTEMPTS" "${sha:0:7}"
    if [ "$crashed" = yes ]; then
      printf "The attempt didn't finish, so this empty commit starts the next pass.\n"
    else
      [ -z "$why" ] || printf '**Refused.** %s This empty commit starts the next pass.\n\n' "$why"
      [ -z "$summary" ] || printf '%s\n\n' "$summary"
      [ -z "$fixed" ] || printf '**Fixed:** %s\n\n' "$fixed"
      [ -z "$disputes" ] || printf '**Disputed.** The next review weighs this evidence:\n\n%s\n\n' "$disputes"
      [ -z "$unresolved" ] || printf "**Couldn't fix:**\n\n%s\n" "$unresolved"
    fi
  } >"$body"
  id="$(gh api -X POST "repos/$REPO/issues/$PR/comments" -F "body=@$body" --jq .id)"

  if ! "${GIT[@]}" push -q "$url" "HEAD:refs/heads/$HEAD_REF"; then
    printf '\n_The push was refused because %s moved on, so this attempt did not land._\n' "$HEAD_REF" >>"$body"
    gh api -X PATCH "repos/$REPO/issues/comments/$id" -F "body=@$body" >/dev/null
    VERDICT=findings NOTE="Auto-fix attempt $ATTEMPT of $MAX_FIX_ATTEMPTS didn't land: $HEAD_REF moved on, and the new head gets its own review." post >/dev/null
    rm -f "$body"
    echo "spec-test-review: push refused; $HEAD_REF moved on and its own run reviews it"
    out pushed=false superseded=true
    return 0
  fi
  rm -f "$body"
  out pushed=true
}

post() {
  : "${REPO:?}" "${PR:?}" "${VERDICT:?}" "${REPORT_FILE:?}" "${HEAD_SHA:?}" "${RUN_URL:?}"
  local bot="${BOT_LOGIN:-github-actions[bot]}" heading existing body
  case "$VERDICT" in
    pass) heading='✅ Pass: every scenario in scope has a faithful test' ;;
    findings) heading='❌ Findings: some scenarios lack a faithful test' ;;
    not_applicable) heading='➖ Not applicable: this PR implements no Spec Kit scenario' ;;
    deferred) heading='⚠️ Deferred: what is left moved to a follow-up issue' ;;
    *) echo "spec-test-review: unknown verdict '$VERDICT'" >&2; return 2 ;;
  esac

  existing="$(gh api --paginate "repos/$REPO/issues/$PR/comments" \
    --jq ".[] | select(.user.login == \"$bot\" and (.body | contains(\"$MARKER\"))) | .id" | tail -n 1)"
  if [ "$VERDICT" = not_applicable ] && [ -z "$existing" ]; then
    echo "spec-test-review: not applicable and no earlier report; nothing posted"
    return 0
  fi

  body="$(mktemp)"
  {
    printf '%s\n## Spec test review: %s\n\n' "$MARKER" "$heading"
    [ -z "${NOTE:-}" ] || printf '%s\n\n' "$NOTE"
    head -c "$MAX_BODY" "$REPORT_FILE"
    if [ "$(wc -c <"$REPORT_FILE" | tr -d ' ')" -gt "$MAX_BODY" ]; then
      printf '\n\n_The report was cut to fit a comment. The [run summary](%s) has all of it._\n' "$RUN_URL"
    fi
    printf '\n\n---\n<sub>Commit %s · [run](%s) · ' "${HEAD_SHA:0:7}" "$RUN_URL"
    printf 'Procedure: [`.claude/commands/review-spec-tests.md`](https://github.com/%s/blob/main/.claude/commands/review-spec-tests.md). ' "$REPO"
    printf 'This runs on every push while the PR is ready for review. On findings, Claude fixes the tests and pushes, '
    printf 'up to %s times. Whatever is left after the last pass moves to a follow-up issue and the check passes. ' "${MAX_FIX_ATTEMPTS:-3}"
    printf 'To dispute a finding, reply with its ID (such as `US3-AS4`) and your evidence, then push or re-run.</sub>\n'
  } >"$body"

  if [ -n "$existing" ]; then
    gh api -X PATCH "repos/$REPO/issues/comments/$existing" -F "body=@$body" --jq .html_url
  else
    gh api -X POST "repos/$REPO/issues/$PR/comments" -F "body=@$body" --jq .html_url
  fi
  rm -f "$body"
}

deferral_marker() { printf '<!-- spec-test-review-deferred: #%s -->' "$PR"; }
deferral_issues() {
  gh api --paginate "repos/$REPO/issues?state=open&per_page=100" \
    --jq ".[] | select(.pull_request == null and ((.body // \"\") | contains(\"$(deferral_marker)\"))) | .number"
}

# Files what is left in the PR's follow-up issue (creating or updating it) and prints the issue's URL.
# Fails, rather than printing nothing, when the issue couldn't be written. Args: title intro.
defer() {
  local title="$1" intro="$2" existing body url=""
  existing="$(deferral_issues | head -n 1)" || return 1
  body="$(mktemp)"
  {
    printf '%s\n%s\n\nCommit %s · [run](%s)\n\n' "$(deferral_marker)" "$intro" "${HEAD_SHA:0:7}" "$RUN_URL"
    [ -z "${UNRESOLVED:-}" ] || printf "### Why the auto-fix stopped\n\n%s\n\n" "$(head -c 8000 <<<"$UNRESOLVED")"
    printf '### Last review\n\n'
    head -c 50000 "$REPORT_FILE"
  } >"$body"
  if [ -n "$existing" ]; then
    url="$(gh api -X PATCH "repos/$REPO/issues/$existing" -f "title=${title:0:200}" -F "body=@$body" --jq .html_url)" || true
  else
    url="$(gh api -X POST "repos/$REPO/issues" -f "title=${title:0:200}" -F "body=@$body" --jq .html_url)" || true
  fi
  rm -f "$body"
  [ -n "$url" ] || return 1
  printf '%s\n' "$url"
}

undefer() {
  local n
  for n in $(deferral_issues); do
    gh api -X POST "repos/$REPO/issues/$n/comments" \
      -f "body=Resolved: the spec test review of #$PR passed on commit ${HEAD_SHA:0:7} ([run]($RUN_URL))." >/dev/null
    gh api -X PATCH "repos/$REPO/issues/$n" -f state=closed -f state_reason=completed >/dev/null
    echo "spec-test-review: closed #$n"
  done
}

decide() {
  : "${REPO:?}" "${PR:?}" "${HEAD_SHA:?}" "${RUN_URL:?}" "${REVIEW_RESULT:?}" "${REPORT_FILE:?}"
  local url current suffix="${PR_TITLE:+: $PR_TITLE}"
  case "$REVIEW_RESULT" in
    skipped)
      if [ "${DRAFT:-}" = true ] || [ "${DEPENDABOT:-}" = true ]; then
        echo "spec-test-review: not reviewed (draft or Dependabot)"; return 0
      elif [ "${FORK:-}" = true ]; then
        echo "::error::Fork PRs get no secrets, so they can't be reviewed. A maintainer reviews and merges them."
      else
        echo "::error::The review was skipped for an unexpected reason. Re-run the workflow."
      fi
      return 1 ;;
  esac
  # A re-run of an older commit's run must not overwrite the current head's report or touch its issues.
  current="$(gh api "repos/$REPO/pulls/$PR" --jq .head.sha)"
  if [ "$current" != "$HEAD_SHA" ]; then
    echo "spec-test-review: ${HEAD_SHA:0:7} is no longer the PR head (${current:0:7}); leaving its report alone"
    case "${VERDICT:-}" in findings) return 1 ;; *) return 0 ;; esac
  fi
  case "$REVIEW_RESULT" in
    success) ;;
    *)
      # The review crashed twice: an outage, or a missing or expired Claude token. Per constitution
      # section 14 that mustn't block a merge, but it must not pass quietly either: it gets its own issue.
      printf "The review couldn't run on this commit, twice ([run](%s)). Check the Claude secret and the run log, then push or re-run the workflow.\n" \
        "$RUN_URL" >"$REPORT_FILE"
      if ! url="$(defer "Spec test review couldn't run on #$PR$suffix" \
        "The spec test review of #$PR couldn't run, so the check passed without it. Until it runs, #$PR's tests are unreviewed.")"; then
        echo "::error::The review couldn't run, and its follow-up issue couldn't be filed either. Re-run the workflow."
        return 1
      fi
      VERDICT=deferred NOTE="The review couldn't run. [This issue]($url) tracks it, and the check passes." post
      return 0 ;;
  esac
  case "${VERDICT:-}" in
    '') echo "spec-test-review: only documentation changed; nothing to review"; return 0 ;;
    pass | not_applicable) post; undefer; return 0 ;;
    findings) ;;
    *) echo "spec-test-review: unknown verdict '$VERDICT'" >&2; return 2 ;;
  esac
  if [ "${SUPERSEDED:-}" = true ]; then
    echo "spec-test-review: the branch moved on; the next run reviews the new head"; return 1
  fi
  if [ "${PUSHED:-}" = true ]; then
    # The push job already posted the report; the next run owns the comment now.
    echo "::error::Blocking findings on this commit. The auto-fix pushed a commit, and the review runs again on it."
    return 1
  fi
  if [ "${FIX_RESULT:-}" = failure ] || [ "${PUSH_RESULT:-}" = failure ]; then
    UNRESOLVED="${UNRESOLVED:+$UNRESOLVED$'\n'}- The auto-fix job failed ([run]($RUN_URL)), so these findings weren't fixed."
  fi
  if ! url="$(defer "Spec test gaps deferred from #$PR$suffix" \
    "The spec test review of #$PR still had blocking findings after its last pass, so the check passed and they moved here. Each one is an acceptance scenario #$PR touches without a faithful test.")"; then
    echo "::error::This was the last pass, but its follow-up issue couldn't be filed. Re-run the workflow."
    return 1
  fi
  VERDICT=deferred NOTE="This was the last pass, so the check passes. [The follow-up issue]($url) tracks the findings below." post
  return 0
}

case "${1:-}" in
  scope) scope ;;
  patch) make_patch "${2:-}" ;;
  guard) guard ;;
  push) push ;;
  post) post ;;
  decide) decide ;;
  *) echo "usage: $0 scope|patch OUT|guard|push|post|decide" >&2; exit 2 ;;
esac
