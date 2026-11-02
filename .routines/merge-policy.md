# Merge policy: when a session merges its own PR

Source: `docs/handoffs/2026-11-02-zero-touch-operations.md` §3 (issue #295). Decision D1 is still open
there. Until nbon12 answers it, sessions follow the recommended answer, which is what this file says.
Since #311, the independent review runs in a subagent rather than the handoff's separate session (see
`docs/patch-notes.md`).

A session's own PR is merged by `.github/workflows/auto-merge.yml` once the session marks it ready (see
[Auto-merge](#auto-merge)), unless it touches a guarded path: then the session **squash-merges it**
itself, when every rule below holds on the PR's current head commit. Either way, every rule below must
hold first. "Its own PR" means a PR this session, or the session or routine that handed it the work,
opened. Never merge someone else's PR under this policy.

## The rules

1. **Checks are green.** Every check run is complete, and none failed or was cancelled.
   - `pr-env` (the per-PR environment) is the one exception. Re-run it once. If it fails again for a
     provisioning reason unrelated to the change, say so in the PR, as
     `.routines/nightly-repo-hygiene.md` §6 does, and it doesn't block the merge.
2. **Reviews are settled.** No review thread is unresolved, and no review has requested changes.
3. **The PR is ready.** It isn't a draft, and its description lists what was verified: the tests run and
   links to the CI runs.
4. **Patch notes are in.** The PR includes a `docs/patch-notes.md` entry whenever `CLAUDE.md` requires
   one, which is for features and behavior-changing fixes.
5. **Guarded paths get an independent review.** If the PR touches a guarded path (below), it has passed
   the independent automated review described below.

## Auto-merge

`.github/workflows/auto-merge.yml` squash-merges a PR that nbon12 opened (every session's PR is) once
every check on it has passed, not only the required ones, and every conversation is resolved. It checks
rule 1, rule 5, and the parts of rules 2 and 3 that GitHub can see: no review requesting changes, not a
draft. It can't check the rest, so:

- **Open your PR as a draft** (`gh pr create --draft`), and mark it ready (`gh pr ready`) only once
  rules 1–4 hold. Marking it ready is the decision to merge.
- **A failed check keeps the PR open,** `pr-env` included. If `pr-env` failed for a provisioning reason
  (rule 1's exception), re-run it. If it fails the same way again, say so in the PR and merge it
  yourself.
- **To stop a merge,** add the `hold` label; removing it lets auto-merge carry on.
- **A PR touching a guarded path** isn't auto-merged. Follow the independent review below, then merge it
  yourself.

## Guarded paths

These are the code that enforces the guardrails, or that moves money or identity: all of `.github/`,
`scripts/pr-env-*`, `.routines/`, the constitution, any `CLAUDE.md`, `infra/`, Auth, Payments and
migrations. The exact list is `GUARDED_PATHS` in `.github/workflows/auto-merge.yml`, which keeps these
PRs out of auto-merge. Change the list there, and only there.

## Independent automated review (guarded paths)

1. **Start an independent reviewer.** Spawn a fresh subagent (the `Agent` tool, `general-purpose`) and
   pass it the `model` this session runs on, so the review never falls to a cheaper default. This
   policy, linked from `CLAUDE.md`, is nbon12's standing request for that subagent: don't ask first. It
   runs in any turn with no click. If it fails to start or errors, retry it once.
   - **Fallback, only if the retry fails too:** a separate session titled "Review <owner>/<repo>#<n>".
     In a cloud or routine session, `create_session` starts one with no click. In the desktop app,
     `start_session` starts one only on nbon12's own message. From any other turn it becomes a card
     nbon12 has to click, so don't leave it at that. Say on the PR that the merge is waiting for a
     reviewer and why, then start the reviewer yourself in the next turn nbon12 starts.

   Either way, give the reviewer only the PR link and tell it to read the diff from the PR. Don't give it
   your own summary or conclusions. Its prompt: run the `code-review` skill at `high` against that PR
   with `--comment`, so its findings are posted on the PR, and then end its turn.
2. **Fix every confirmed blocking finding.** Push the fixes, and answer each finding on the PR: fixed, or
   why it isn't a problem. If you make more than a small, targeted fix, have a new reviewer check it
   again.
3. **Merge, then notify.** When rules 1–4 hold again, merge. Then send nbon12 one push notification
   (`PushNotification`), such as "merged #N touching guarded paths: <paths>", so they can review it after
   the fact.

**Constitution exception:** `.specify/memory/constitution.md` §13 requires amendments to be "reviewed and
approved like any architectural decision". A PR that changes the constitution stays **owner-merged**
until nbon12 amends §13. Leave it open, ready, and say so on the PR and its issue.

## Never, under any policy

- Skip, disable or quarantine a test to get green.
- Merge with a red required check.
- Force-push `main`.
- Approve a GitHub deployment or a review on your own PR.

## Still the owner's

- Creating or rotating root credentials, accounts and billing.
- GitHub settings that need admin: Environments, rulesets and secrets.
- Product and spec decisions, and constitution amendments.
- Anything on nbon12's own computer.

Everything else should end with a notification, not a question.
