#!/usr/bin/env bash
# Remove a per-PR OpenTofu state lock left by a killed run (issue #236; handoff root-cause items 2 and 6).
# A killed `tofu apply` never releases gs://<bucket>/state/pr/<n>/default.tflock, and every later apply or
# destroy for that PR then fails on it until someone removes it by hand.
#
# Callers, each right before `tofu init` (with no default.tfstate, init itself needs the lock):
#   pr-env-tofu-init action          Used by pr-env.yml and pr-env-teardown.yml. The pr-env-<n>
#                                    concurrency group already excludes a live pr-env, teardown or
#                                    pr-env-repair run for this PR. The age floor protects a live sweep
#                                    destroy, which runs outside that group.
#   pr-env-sweep.yml                 CHECK_LIVE_RUNS=true: outside the group, so it also keeps the lock
#                                    while a pr-env, teardown or repair run for this PR is unfinished.
#                                    Those workflows end their run-name in "(pr-<n>)"; an unfinished run
#                                    without that tag counts as live for every PR.
#
# A kept lock is not an error: the caller's -lock-timeout waits for it and fails if it never clears. The
# delete is conditional on the generation that was inspected (the GCS backend's Lock ID), so a lock taken
# after the check is never removed.
#
# Env (required): PR_NUMBER; gcloud logged in (WIF in CI).
# Env (optional): STATE_BUCKET (nekohoa-dev-tfstate), STALE_AFTER_MIN (45), CHECK_LIVE_RUNS (false; needs
#                 REPO and GH_TOKEN for gh).
# Exit: 0 no lock, a stale lock removed, or a lock kept; 1 a lookup or the delete failed; 2 bad input.
set -euo pipefail

: "${PR_NUMBER:?}"
STATE_BUCKET="${STATE_BUCKET:-nekohoa-dev-tfstate}"
STALE_AFTER_MIN="${STALE_AFTER_MIN:-45}"
CHECK_LIVE_RUNS="${CHECK_LIVE_RUNS:-false}"
[[ "$PR_NUMBER" =~ ^[1-9][0-9]*$ ]] || { echo "::error::PR_NUMBER must be a positive integer." >&2; exit 2; }
[[ "$STALE_AFTER_MIN" =~ ^[0-9]+$ ]] || { echo "::error::STALE_AFTER_MIN must be a whole number of minutes." >&2; exit 2; }
[[ "$CHECK_LIVE_RUNS" =~ ^(true|false)$ ]] || { echo "::error::CHECK_LIVE_RUNS must be true or false." >&2; exit 2; }
if [ "$CHECK_LIVE_RUNS" = true ]; then : "${REPO:?REPO is required when CHECK_LIVE_RUNS=true}"; fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
GCP_TOKEN="$(gcloud auth print-access-token 2>/dev/null || true)"
[ -n "$GCP_TOKEN" ] || { echo "::error::gcloud has no usable credential."; exit 1; }
# call METHOD URL [curl args]: prints the HTTP status ("000" if unreachable); the body is in $tmp/body. The
# token reaches curl through a file descriptor, never argv.
call() {
  local method="$1" url="$2" code; shift 2
  code="$(curl -sS -o "$tmp/body" -w '%{http_code}' -X "$method" \
    -H @<(printf 'Authorization: Bearer %s\n' "$GCP_TOKEN") "$url" "$@")" || true
  echo "${code:-000}"
}
# Minutes since an RFC 3339 UTC timestamp (fractional seconds dropped; portable, no GNU date).
age_min() { echo $(( ( $(date -u +%s) - $(jq -rn --arg t "$1" '$t | sub("\\.[0-9]+Z$"; "Z") | fromdateiso8601') ) / 60 )); }

name="state/pr/${PR_NUMBER}/default.tflock"
obj="https://storage.googleapis.com/storage/v1/b/${STATE_BUCKET}/o/${name//\//%2F}"
code="$(call GET "$obj")"
case "$code" in
  404) echo "PR #${PR_NUMBER}: no state lock."; exit 0 ;;
  200) ;;
  *) echo "::error::Looking up gs://${STATE_BUCKET}/${name} returned HTTP ${code}."; exit 1 ;;
esac
gen="$(jq -r .generation "$tmp/body")"
age="$(age_min "$(jq -r .timeCreated "$tmp/body")")"
echo "PR #${PR_NUMBER}: state lock ${gen}, ${age} min old."
# Who holds it, for the log. The lock file names the operation and the runner, never a secret.
if [ "$(call GET "${obj}?alt=media&generation=${gen}")" = 200 ]; then
  jq -c '{Operation, Who, Version, Created}' "$tmp/body" 2>/dev/null || true
fi

if [ "$age" -lt "$STALE_AFTER_MIN" ]; then
  echo "::warning::PR #${PR_NUMBER}: state lock ${gen} is under ${STALE_AFTER_MIN} min old and may belong to a live run. Keeping it; tofu waits for it (-lock-timeout)."
  exit 0
fi

if [ "$CHECK_LIVE_RUNS" = true ]; then
  # Only runs for this PR can hold its lock, so another PR's run (or a repair waiting days for approval)
  # does not block this one (#236). A run is matched by the "(pr-<n>)" its workflow's run-name ends in.
  live=0
  for w in pr-env.yml pr-env-teardown.yml pr-env-repair.yml; do
    if ! titles="$(gh run list --repo "$REPO" --workflow "$w" --limit 50 --json status,displayTitle \
      --jq '.[] | select(.status != "completed") | .displayTitle' 2>/dev/null)"; then
      live=$(( live + 1 )) # unknown counts as live
      continue
    fi
    while IFS= read -r title; do
      [ -n "$title" ] || continue
      if [[ "$title" =~ \(pr-([0-9]+)\)$ ]] && [ "${BASH_REMATCH[1]}" != "$PR_NUMBER" ]; then continue; fi
      live=$(( live + 1 )) # this PR's run, or an untagged one
    done <<<"$titles"
  done
  if [ "$live" -gt 0 ]; then
    echo "::warning::PR #${PR_NUMBER}: keeping state lock ${gen} (${age} min old): ${live} pr-env, teardown or repair run(s) that may be this PR's are unfinished."
    exit 0
  fi
fi

code="$(call DELETE "${obj}?ifGenerationMatch=${gen}")"
case "$code" in
  200 | 204) echo "::warning::PR #${PR_NUMBER}: removed stale state lock ${gen} (${age} min old)." ;;
  404 | 412) echo "PR #${PR_NUMBER}: state lock ${gen} was released or replaced while checking; leaving it." ;;
  *) echo "::error::Removing state lock ${gen} returned HTTP ${code}."; exit 1 ;;
esac
