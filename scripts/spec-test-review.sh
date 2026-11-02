#!/usr/bin/env bash
# Deterministic halves of .github/workflows/spec-test-review.yml (issue #302). Claude does the review; this
# script decides whether a PR needs one and posts the report.
#
#   scope   Reads the PR's changed paths on stdin and prints "review", or "skip" when every path is
#           documentation: Markdown, docs/, specs/ or wireframes/. Spec Kit artifact PRs land before the
#           code, so there are no tests to hold to the spec yet (CLAUDE.md, "Spec Kit workflow: PR watching").
#   post    Writes the report as the PR's one sticky comment, found by its marker and author, so a re-run
#           edits it in place. A not_applicable verdict posts nothing unless a sticky comment already exists
#           (it then says the review no longer applies), so PRs outside any spec get no comment.
#           Env: REPO PR VERDICT (pass|findings|not_applicable) REPORT_FILE HEAD_SHA RUN_URL;
#           BOT_LOGIN (github-actions[bot]: GITHUB_TOKEN's author). Needs gh authenticated (GH_TOKEN).
# shellcheck disable=SC2016 # backticks in the comment text are Markdown, not command substitution
set -euo pipefail

MARKER='<!-- spec-test-review -->'
MAX_BODY=64000 # GitHub refuses comment bodies over 65,536 characters; the rest is heading and footer

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

post() {
  : "${REPO:?}" "${PR:?}" "${VERDICT:?}" "${REPORT_FILE:?}" "${HEAD_SHA:?}" "${RUN_URL:?}"
  local bot="${BOT_LOGIN:-github-actions[bot]}" heading existing body
  case "$VERDICT" in
    pass) heading='✅ Pass: every scenario in scope has a faithful test' ;;
    findings) heading='❌ Findings: some scenarios lack a faithful test' ;;
    not_applicable) heading='➖ Not applicable: this PR implements no Spec Kit scenario' ;;
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
    head -c "$MAX_BODY" "$REPORT_FILE"
    if [ "$(wc -c <"$REPORT_FILE" | tr -d ' ')" -gt "$MAX_BODY" ]; then
      printf '\n\n_The report was cut to fit a comment. The [run summary](%s) has all of it._\n' "$RUN_URL"
    fi
    printf '\n\n---\n<sub>Commit %s · [run](%s) · ' "${HEAD_SHA:0:7}" "$RUN_URL"
    printf 'Procedure: [`.claude/commands/review-spec-tests.md`](https://github.com/%s/blob/main/.claude/commands/review-spec-tests.md). ' "$REPO"
    printf 'This runs again on every push while the PR is ready for review. To dispute a finding, reply on this PR '
    printf 'with its ID (such as `US3-AS4`) and your evidence, then re-run the check. The next review reads the '
    printf 'replies and withdraws any finding the evidence disproves.</sub>\n'
  } >"$body"

  if [ -n "$existing" ]; then
    gh api -X PATCH "repos/$REPO/issues/comments/$existing" -F "body=@$body" --jq .html_url
  else
    gh api -X POST "repos/$REPO/issues/$PR/comments" -F "body=@$body" --jq .html_url
  fi
  rm -f "$body"
}

case "${1:-}" in
  scope) scope ;;
  post) post ;;
  *) echo "usage: $0 scope|post" >&2; exit 2 ;;
esac
