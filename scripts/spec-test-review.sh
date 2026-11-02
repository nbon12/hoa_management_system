#!/usr/bin/env bash
# Deterministic halves of .github/workflows/spec-test-review.yml (issue #302). Claude reviews and fixes the
# tests; this script decides what runs, guards and pushes the fixes, and turns the result into the check.
#
#   scope   Reads the PR's changed paths on stdin and prints "review", or "skip" when every path is
#           documentation: Markdown, docs/, specs/ or wireframes/. Spec Kit artifact PRs land before the
#           code, so there are no tests to hold to the spec yet (CLAUDE.md, "Spec Kit workflow: PR watching").
#   guard   In the auto-fix checkout: prints the changed paths the fix may commit (test code), ignoring the
#           agent config claude-code-action restores from main and test-run output. Fails when the fix changed anything else
#           or added a skipped or focused test.
#   push    After an auto-fix attempt: guards, posts the attempt's comment, commits with a
#           Spec-Test-Review-Fix trailer and pushes, which starts the next review. A crashed attempt
#           pushes an empty commit so the next pass retries. Writes pushed, superseded and unresolved
#           to $GITHUB_OUTPUT. Env: REPO PR HEAD_REF HEAD_SHA ATTEMPT MAX_FIX_ATTEMPTS MERGE_TOKEN
#           FIX_OUTPUT (the fixer's JSON); PUSH_URL overrides the remote (tests).
#   decide  The Spec test verdict check. Posts the report and exits 0 (pass) or 1 (fail). A pass with
#           findings left is the last one: it files them in a follow-up issue and passes, so the loop
#           always ends green. A later green pass closes that issue. Env: REPO PR PR_TITLE HEAD_SHA
#           RUN_URL REVIEW_RESULT VERDICT REPORT_FILE DRAFT FORK DEPENDABOT PUSHED SUPERSEDED
#           UNRESOLVED ATTEMPT MAX_FIX_ATTEMPTS.
#   post    Upserts the sticky report comment (decide uses it). Env: REPO PR VERDICT (pass|findings|
#           not_applicable|deferred) REPORT_FILE HEAD_SHA RUN_URL [NOTE].
#
# gh must be authenticated (GH_TOKEN). Comments are found by marker and author (BOT_LOGIN,
# github-actions[bot] for GITHUB_TOKEN), so a re-run edits them in place.
# shellcheck disable=SC2016 # backticks in comment text are Markdown, not command substitution
set -euo pipefail

MARKER='<!-- spec-test-review -->'
MAX_BODY=60000 # GitHub refuses comment and issue bodies over 65,536 characters; the rest is framing
# Agent config claude-code-action restores from main on PRs (src/github/operations/restore-config.ts).
CONFIG_PATHS_RE='^(\.claude/|\.claude-pr/|\.mcp\.json$|\.claude\.json$|\.gitmodules$|\.ripgreprc$|CLAUDE\.md$|CLAUDE\.local\.md$|\.husky/)'
TEST_PATHS_RE='^(HOAManagementCompany\.Tests/|neko-hoa/e2e/|neko-hoa/cypress/|neko-hoa/src/.*\.spec\.ts$)'
BUILD_OUTPUT_RE='(^|/)(TestResults|bin|obj)/' # test-run output that isn't ignored everywhere
SKIP_RE='Skip[[:space:]]*=|(^|[^[:alnum:]_.])(xit|xdescribe|fit|fdescribe)\(|\.(skip|only|fixme)\('

out() { if [ -n "${GITHUB_OUTPUT:-}" ]; then printf '%s\n' "$@" >>"$GITHUB_OUTPUT"; fi; }
out_multi() { # name value
  [ -n "${GITHUB_OUTPUT:-}" ] || return 0
  local delim="EOF_$RANDOM$RANDOM$RANDOM"
  printf '%s<<%s\n%s\n%s\n' "$1" "$delim" "$2" "$delim" >>"$GITHUB_OUTPUT"
}

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

# Changed and untracked paths, one per line; both sides of a rename.
changed_paths() {
  local entry status
  git status --porcelain=v1 -z --untracked-files=all | while IFS= read -r -d '' entry; do
    status="${entry:0:2}"
    printf '%s\n' "${entry:3}"
    case "$status" in R* | C*) IFS= read -r -d '' entry && printf '%s\n' "$entry" ;; esac
  done
}

guard() {
  local path added hits
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
  added="$({
    git diff -U0 -- "${ok[@]}" | grep '^+' | grep -v '^+++' || true
    for path in "${ok[@]}"; do
      if [ -f "$path" ] && ! git ls-files --error-unmatch -- "$path" >/dev/null 2>&1; then cat -- "$path"; fi
    done
  })"
  if hits="$(grep -E "$SKIP_RE" <<<"$added")"; then
    echo "the fix added a skipped or focused test: $(head -n 3 <<<"$hits" | tr '\n' ' ')" >&2
    return 1
  fi
  printf '%s\n' "${ok[@]}"
}

push() {
  : "${REPO:?}" "${PR:?}" "${HEAD_REF:?}" "${HEAD_SHA:?}" "${ATTEMPT:?}" "${MAX_FIX_ATTEMPTS:?}"
  local url="${PUSH_URL:-https://x-access-token:${MERGE_TOKEN:?}@github.com/${REPO}.git}"
  local fixed="" disputes="" unresolved="" summary="" paths="" why="" crashed=no remote body sha
  if [ -n "${FIX_OUTPUT:-}" ] && jq -e 'type == "object"' >/dev/null 2>&1 <<<"$FIX_OUTPUT"; then
    fixed="$(jq -r '[.fixed[]?] | join(", ")' <<<"$FIX_OUTPUT")"
    disputes="$(jq -r '.disputes[]? | "- **\(.id)**: \(.evidence)"' <<<"$FIX_OUTPUT")"
    unresolved="$(jq -r '.unresolved[]? | "- **\(.id)**: \(.reason)"' <<<"$FIX_OUTPUT")"
    summary="$(jq -r '.summary // ""' <<<"$FIX_OUTPUT")"
  else
    crashed=yes
  fi
  if [ "$crashed" = no ] && ! paths="$(guard 2>"${TMPDIR:-/tmp}/spec-test-review-guard.$$")"; then
    why="$(cat "${TMPDIR:-/tmp}/spec-test-review-guard.$$")"
    unresolved="${unresolved:+$unresolved$'\n'}- The auto-fix was not pushed: ${why}. Only test code may change, and no test may be skipped or focused."
    paths="" fixed="" disputes=""
  fi
  rm -f "${TMPDIR:-/tmp}/spec-test-review-guard.$$"
  out_multi unresolved "$unresolved"
  if [ "$crashed" = no ] && [ -z "$paths" ] && [ -z "$disputes" ]; then
    echo "spec-test-review: nothing to push; this is the last pass"
    out pushed=false
    return 0
  fi
  remote="$(git ls-remote "$url" "refs/heads/$HEAD_REF" | cut -f1)"
  if [ "$remote" != "$HEAD_SHA" ]; then
    echo "spec-test-review: $HEAD_REF moved on (now ${remote:-gone}); its own run reviews it"
    out pushed=false superseded=true
    return 0
  fi

  if [ -n "$paths" ]; then
    local -a staged=()
    local p
    while IFS= read -r p; do staged+=("$p"); done <<<"$paths"
    git add -A -- "${staged[@]}"
  fi
  {
    if [ "$crashed" = yes ]; then
      printf 'test(spec-test-review): retry after auto-fix attempt %s of %s for #%s\n\n' "$ATTEMPT" "$MAX_FIX_ATTEMPTS" "$PR"
      printf 'The attempt did not finish, so this empty commit starts the next pass.\n'
    else
      printf 'test(spec-test-review): auto-fix attempt %s of %s for #%s\n\n%s\n' "$ATTEMPT" "$MAX_FIX_ATTEMPTS" "$PR" "$summary"
      [ -z "$fixed" ] || printf '\nFixed: %s\n' "$fixed"
    fi
    printf '\nSpec-Test-Review-Fix: %s\n' "$ATTEMPT"
  } | git -c user.name='github-actions[bot]' -c user.email='41898282+github-actions[bot]@users.noreply.github.com' \
    commit -q --allow-empty -F -
  sha="$(git rev-parse HEAD)"

  # Post before pushing: the push starts the next review, which reads this comment for disputes.
  body="$(mktemp)"
  {
    printf '**Spec test auto-fix**, attempt %s of %s: commit %s\n\n' "$ATTEMPT" "$MAX_FIX_ATTEMPTS" "${sha:0:7}"
    if [ "$crashed" = yes ]; then
      printf "The attempt didn't finish, so this empty commit starts the next pass.\n"
    else
      [ -z "$summary" ] || printf '%s\n\n' "$summary"
      [ -z "$fixed" ] || printf '**Fixed:** %s\n\n' "$fixed"
      [ -z "$disputes" ] || printf '**Disputed.** The next review weighs this evidence:\n\n%s\n\n' "$disputes"
      [ -z "$unresolved" ] || printf "**Couldn't fix:**\n\n%s\n" "$unresolved"
    fi
  } >"$body"
  GH_TOKEN="${MERGE_TOKEN:-${GH_TOKEN:-}}" gh api -X POST "repos/$REPO/issues/$PR/comments" -F "body=@$body" --jq .html_url
  rm -f "$body"

  if ! git push -q "$url" "HEAD:refs/heads/$HEAD_REF"; then
    echo "spec-test-review: push refused; $HEAD_REF moved on and its own run reviews it"
    out pushed=false superseded=true
    return 0
  fi
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
defer() {
  local existing body title
  existing="$(deferral_issues | head -n 1)"
  body="$(mktemp)"
  {
    printf '%s\n' "$(deferral_marker)"
    printf 'The spec test review of #%s still had blocking findings after its last pass, so the check passed ' "$PR"
    printf 'and they moved here. Each one is an acceptance scenario #%s touches without a faithful test.\n\n' "$PR"
    printf 'Commit %s · [run](%s)\n\n' "${HEAD_SHA:0:7}" "$RUN_URL"
    [ -z "${UNRESOLVED:-}" ] || printf "### Why the auto-fix stopped\n\n%s\n\n" "$UNRESOLVED"
    printf '### Last review\n\n'
    head -c "$MAX_BODY" "$REPORT_FILE"
  } >"$body"
  if [ -n "$existing" ]; then
    gh api -X PATCH "repos/$REPO/issues/$existing" -F "body=@$body" --jq .html_url
  else
    title="Spec test gaps deferred from #$PR${PR_TITLE:+: $PR_TITLE}"
    gh api -X POST "repos/$REPO/issues" -f "title=${title:0:200}" -F "body=@$body" --jq .html_url
  fi
  rm -f "$body"
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
  local url
  case "$REVIEW_RESULT" in
    skipped)
      if [ "${DRAFT:-}" = true ] || [ "${DEPENDABOT:-}" = true ]; then
        echo "spec-test-review: not reviewed (draft or Dependabot)"; return 0
      fi
      echo "::error::Fork PRs get no secrets, so they can't be reviewed. A maintainer reviews and merges them."
      return 1 ;;
    success) ;;
    *)
      # The review crashed twice. That's a last pass too: track it and pass.
      printf "The review couldn't run on this commit, twice ([run](%s)). Push or re-run the workflow to review it.\n" \
        "$RUN_URL" >"$REPORT_FILE"
      url="$(defer)"
      VERDICT=deferred NOTE="The review couldn't run, so [the follow-up issue]($url) tracks it and the check passes." post
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
    NOTE="Auto-fix attempt ${ATTEMPT:-?} of ${MAX_FIX_ATTEMPTS:-3} pushed a commit. The review runs again on it." post
    echo "::error::Blocking findings on this commit; the auto-fix pushed a commit and the review runs again on it."
    return 1
  fi
  url="$(defer)"
  VERDICT=deferred NOTE="This was the last pass, so the check passes. [The follow-up issue]($url) tracks the findings below." post
  return 0
}

case "${1:-}" in
  scope) scope ;;
  guard) guard ;;
  push) push ;;
  post) post ;;
  decide) decide ;;
  *) echo "usage: $0 scope|guard|push|post|decide" >&2; exit 2 ;;
esac
