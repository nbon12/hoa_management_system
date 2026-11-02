#!/usr/bin/env bash
# Policy gate for unattended pr-env repairs (docs/handoffs/2026-11-02-zero-touch-operations.md §2; issue
# #295). It replaces the `ops` Environment's required reviewer: instead of a person clicking Approve, every
# write must pass checks that a machine can verify against live state. A refusal changes nothing and
# exits 1, so the run shows red.
#
# Modes:
#   preflight   P1 kill switch, P3 rate limit, P4 no live run for this PR, P5 PR closed, P8 dispatcher has
#               write access. Run before any credential is minted.
#   check-plan  P7: every planned deletion is one of this PR's per-PR names, and there are not too many.
#               Run on a dry run's output, before the real run.
#
# Callers (CALLER):
#   repair  pr-env-repair.yml. All checks that apply to ACTION and DRY_RUN.
#   sweep   pr-env-sweep.yml's "Reap orphans of closed PRs". P1, P4 and P5 only (it runs on a schedule, so
#           there is no dispatcher, and it has its own per-run cap), and P5 also needs the PR to have
#           closed at least CLOSED_MIN_HOURS ago so a teardown in progress is not raced.
#
# Env, preflight: CALLER ACTION PR_NUMBER DRY_RUN REPO; GH_TOKEN for gh; OPS_AUTOMATION (the repo
#                 variable; anything but "on" refuses). CALLER=repair also needs ACTOR and RUN_ID (this
#                 run's id, which P3 and P4 leave out).
# Env, check-plan: ACTION (reap|destroy) PR_NUMBER PLAN_FILE.
#   reap:    PLAN_FILE holds pr-env-reap.sh's ITEMS_OUT lines ("would delete: <what>").
#   destroy: PLAN_FILE holds "<actions> <address>" per planned resource change ("delete module.x.y").
# Env, optional: MAX_WRITES_PER_DAY (3), MAX_NAMES (10), CLOSED_MIN_HOURS (24), AUDIT_FILE (refusals and
#                passes are appended there for the run's PR / ops-log comment), GITHUB_STEP_SUMMARY.
# Exit: 0 pass; 1 refused; 2 bad input. Output is names and PR numbers only (public Actions logs).
set -euo pipefail

MODE="${1:-}"
case "$MODE" in preflight | check-plan) ;; *) echo "::error::usage: $0 preflight|check-plan" >&2; exit 2 ;; esac
: "${PR_NUMBER:?}" "${ACTION:?}"
[[ "$PR_NUMBER" =~ ^[1-9][0-9]*$ ]] || { echo "::error::PR_NUMBER must be a positive integer." >&2; exit 2; }
MAX_WRITES_PER_DAY="${MAX_WRITES_PER_DAY:-3}"
MAX_NAMES="${MAX_NAMES:-10}"
CLOSED_MIN_HOURS="${CLOSED_MIN_HOURS:-24}"
for v in MAX_WRITES_PER_DAY MAX_NAMES CLOSED_MIN_HOURS; do
  [[ "${!v}" =~ ^[0-9]+$ ]] || { echo "::error::${v} must be a whole number." >&2; exit 2; }
done

note() {
  echo "$*"
  if [ -n "${AUDIT_FILE:-}" ]; then echo "- $*" >>"$AUDIT_FILE"; fi
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then echo "- $*" >>"$GITHUB_STEP_SUMMARY"; fi
}
refuse() {
  echo "::error::Policy gate $1 refused: $2"
  if [ -n "${AUDIT_FILE:-}" ]; then echo "- **Policy gate $1 refused:** $2" >>"$AUDIT_FILE"; fi
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then echo "- **Policy gate $1 refused:** $2" >>"$GITHUB_STEP_SUMMARY"; fi
  exit 1
}
# Seconds since an RFC 3339 UTC timestamp (fractional seconds dropped; portable, no GNU date).
age_s() { echo $(( $(date -u +%s) - $(jq -rn --arg t "$1" '$t | sub("\\.[0-9]+Z$"; "Z") | fromdateiso8601') )); }

# ── P7: the plan names only this PR's per-PR resources ─────────────────────────────────────────────────
if [ "$MODE" = check-plan ]; then
  : "${PLAN_FILE:?}"
  [ -f "$PLAN_FILE" ] || refuse P7 "the dry run left no plan file, so what the real run would delete is unknown."
  n="$PR_NUMBER"
  bad=0 counted=0 total=0
  case "$ACTION" in
    reap)
      # One pattern per kind pr-env-reap.sh deletes. Pages deployments and state-object versions are
      # name-checked but not counted: a busy PR has one per push, and both are scoped by PR already.
      while IFS= read -r line; do
        [ -n "$line" ] || continue
        item="${line#would delete: }"; item="${item#deleted: }"
        total=$(( total + 1 ))
        if [[ "$item" =~ ^Cloud\ Run\ service\ nekohoa-api-pr-${n}$ ]] \
          || [[ "$item" =~ ^R2\ bucket\ nekohoa-pr-${n}-documents\ \(emptied\ first\)$ ]] \
          || [[ "$item" =~ ^secret\ pr-${n}-[a-z0-9-]+$ ]] \
          || [[ "$item" =~ ^Neon\ branch\ pr-${n}\ \([A-Za-z0-9-]+\)$ ]] \
          || [[ "$item" =~ ^Stripe\ webhook\ endpoint\ we_[A-Za-z0-9]+\ \(host\ nekohoa-api-pr-${n}-[A-Za-z0-9.-]+\)$ ]]; then
          counted=$(( counted + 1 ))
        elif [[ "$item" =~ ^Pages\ deployment\ [A-Za-z0-9-]+\ \(branch\ pr-${n}\)$ ]] \
          || [[ "$item" =~ ^gs://nekohoa-dev-tfstate/state/pr/${n}/[A-Za-z0-9._-]+#[0-9]+$ ]]; then
          :
        else
          echo "::error::Policy gate P7: '${item}' is not one of PR #${n}'s per-PR names."
          bad=$(( bad + 1 ))
        fi
      done <"$PLAN_FILE"
      [ "$bad" -eq 0 ] || refuse P7 "${bad} planned deletion(s) are not PR #${n}'s per-PR resources."
      [ "$counted" -le "$MAX_NAMES" ] || refuse P7 "the reap would delete ${counted} resources, more than ${MAX_NAMES}."
      note "Policy gate P7 passed: ${total} planned deletion(s), all PR #${n}'s (${counted} counted against the cap of ${MAX_NAMES})." ;;
    destroy)
      # A destroy plan may only delete, and only inside the per-PR module (infra/environments/pr/main.tf).
      while read -r actions address; do
        [ -n "${actions:-}" ] || continue
        total=$(( total + 1 ))
        if [ "$actions" != delete ] || [[ "$address" != module.pr_environment.* ]]; then
          echo "::error::Policy gate P7: planned '${actions}' on '${address}' is not a delete inside module.pr_environment."
          bad=$(( bad + 1 ))
        fi
      done <"$PLAN_FILE"
      [ "$bad" -eq 0 ] || refuse P7 "${bad} planned change(s) are outside module.pr_environment or are not deletes."
      note "Policy gate P7 passed: the destroy plan deletes ${total} address(es), all inside module.pr_environment." ;;
    *) echo "::error::check-plan applies to reap and destroy, not ${ACTION}." >&2; exit 2 ;;
  esac
  exit 0
fi

# ── preflight ────────────────────────────────────────────────────────────────────────────────────────────
: "${CALLER:?}" "${REPO:?}" "${DRY_RUN:?}"
[[ "$CALLER" =~ ^(repair|sweep)$ ]] || { echo "::error::CALLER must be repair or sweep." >&2; exit 2; }
[[ "$DRY_RUN" =~ ^(true|false)$ ]] || { echo "::error::DRY_RUN must be true or false." >&2; exit 2; }
[[ "$ACTION" =~ ^(unlock|destroy|reap)$ ]] || { echo "::error::ACTION must be unlock, destroy or reap." >&2; exit 2; }
if [ "$CALLER" = repair ]; then
  : "${ACTOR:?}" "${RUN_ID:?}"
  [[ "$RUN_ID" =~ ^[0-9]+$ ]] || { echo "::error::RUN_ID must be digits." >&2; exit 2; }
fi
write=false; [ "$DRY_RUN" = false ] && write=true

# P1. Kill switch. Checked on every run the gate sees, including a destroy dry run, which still mints
# the deployer's credentials.
[ "${OPS_AUTOMATION:-}" = on ] || refuse P1 "repo variable OPS_AUTOMATION is '${OPS_AUTOMATION:-<unset>}', not 'on' (the kill switch)."
note "Policy gate P1 passed: OPS_AUTOMATION=on."

# P8. The dispatcher can write to the repo (workflow_dispatch already needs that; this keeps it explicit).
if [ "$CALLER" = repair ]; then
  perm="$(gh api "repos/${REPO}/collaborators/${ACTOR}/permission" --jq .permission 2>/dev/null || echo unknown)"
  case "$perm" in
    admin | maintain | write) note "Policy gate P8 passed: ${ACTOR} has ${perm} access." ;;
    *) refuse P8 "${ACTOR} has '${perm}' access to ${REPO}, not write." ;;
  esac
fi

# P5. Destroy and reap act only on a closed (or merged) PR. An open PR's leftovers are reaped by its own
# next pr-env run (ONLY_UNTRACKED), so nothing here ever needs to touch one.
if [ "$ACTION" = destroy ] || [ "$ACTION" = reap ]; then
  if ! pr="$(gh api "repos/${REPO}/pulls/${PR_NUMBER}" --jq '[.state, (.closed_at // "")] | @tsv' 2>/dev/null)"; then
    refuse P5 "PR #${PR_NUMBER}'s state could not be read."
  fi
  IFS=$'\t' read -r state closed_at <<<"$pr"
  [ "$state" = closed ] || refuse P5 "PR #${PR_NUMBER} is '${state}', not closed or merged."
  if [ "$CALLER" = sweep ]; then
    [ -n "$closed_at" ] || refuse P5 "PR #${PR_NUMBER} has no closed_at time."
    hours=$(( $(age_s "$closed_at") / 3600 ))
    [ "$hours" -ge "$CLOSED_MIN_HOURS" ] \
      || refuse P5 "PR #${PR_NUMBER} closed ${hours} h ago, under ${CLOSED_MIN_HOURS} h; its teardown may still be running."
  fi
  note "Policy gate P5 passed: PR #${PR_NUMBER} is closed."
fi

# P4. No queued or in-progress pr-env, pr-env-teardown or other pr-env-repair run for this PR. A run is
# matched by the "(pr-<n>)" its run-name ends in; an unfinished run without that tag, or a run list that
# cannot be read, counts as this PR's (the same rule as pr-env-clear-stale-lock.sh CHECK_LIVE_RUNS).
if [ "$write" = true ] || [ "$CALLER" = sweep ]; then
  live=0
  for w in pr-env.yml pr-env-teardown.yml pr-env-repair.yml; do
    if ! runs="$(gh run list --repo "$REPO" --workflow "$w" --limit 50 --json databaseId,status,displayTitle \
      --jq '.[] | select(.status != "completed") | [(.databaseId | tostring), .displayTitle] | @tsv' 2>/dev/null)"; then
      echo "  ${w}: the run list could not be read; counting it as live."
      live=$(( live + 1 )); continue
    fi
    while IFS=$'\t' read -r id title; do
      [ -n "$id" ] || continue
      [ "$id" = "${RUN_ID:-}" ] && continue # this run
      if [[ "$title" =~ \(pr-([0-9]+)\)$ ]] && [ "${BASH_REMATCH[1]}" != "$PR_NUMBER" ]; then continue; fi
      echo "  live: ${w} run ${id} \"${title}\""
      live=$(( live + 1 ))
    done <<<"$runs"
  done
  [ "$live" -eq 0 ] || refuse P4 "${live} pr-env, teardown or repair run(s) that may be PR #${PR_NUMBER}'s are unfinished."
  note "Policy gate P4 passed: no other unfinished run for PR #${PR_NUMBER}."
fi

# P3. At most MAX_WRITES_PER_DAY write dispatches of pr-env-repair in 24 h, counting refused ones too: a
# session retrying into refusals is what this limit exists to stop. Runs are matched by run-name
# "pr-env-repair <unlock|destroy|reap> dry_run=false (pr-<n>)".
if [ "$CALLER" = repair ] && [ "$write" = true ]; then
  since="$(jq -rn 'now - 86400 | todate')"
  if ! recent="$(gh run list --repo "$REPO" --workflow pr-env-repair.yml --limit 100 \
    --json databaseId,displayTitle,createdAt 2>/dev/null)"; then
    refuse P3 "the pr-env-repair run list could not be read, so the daily limit cannot be checked."
  fi
  count="$(jq -r --arg since "$since" --arg me "$RUN_ID" '[.[]
    | select((.databaseId | tostring) != $me)
    | select(.createdAt >= $since)
    | select(.displayTitle | test("^pr-env-repair (unlock|destroy|reap) dry_run=false \\(pr-[0-9]+\\)$"))] | length' <<<"$recent")"
  [ "$count" -lt "$MAX_WRITES_PER_DAY" ] \
    || refuse P3 "${count} pr-env-repair write run(s) in the last 24 h already; the limit is ${MAX_WRITES_PER_DAY} (decision D3)."
  note "Policy gate P3 passed: ${count} other write run(s) in the last 24 h (limit ${MAX_WRITES_PER_DAY})."
fi

note "Policy gate passed: ${ACTION} on PR #${PR_NUMBER} (dry_run=${DRY_RUN}, caller ${CALLER})."
