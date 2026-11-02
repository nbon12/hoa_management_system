# Merge policy: when a session merges its own PR

Source: `docs/handoffs/2026-11-02-zero-touch-operations.md` §3 (issue #295). Decision D1 is still open
there. Until nbon12 answers it, sessions follow the recommended answer, which is what this file says.

A session **squash-merges its own PR**, or turns on auto-merge with `mcp__github__enable_pr_auto_merge`,
when every rule below holds on the PR's current head commit. "Its own PR" means a PR this session, or the
session or routine that handed it the work, opened. Never merge someone else's PR under this policy.

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

## Guarded paths

These are the code that enforces the guardrails, or that moves money or identity:

```
.github/workflows/**          scripts/pr-env-*           .routines/**
.specify/memory/constitution.md                          CLAUDE.md
infra/**                      HOAManagementCompany/Features/Auth/**
HOAManagementCompany/Features/Payments/**                **/Migrations/**
```

## Independent automated review (guarded paths)

1. **Start a separate reviewer session.** Use `create_session` with this repo as the source, titled
   "Review <owner>/<repo>#<n>". Give it only the PR link and tell it to read the diff from the PR.
   Don't give it your own summary or conclusions. Its prompt: run the `code-review` skill at `high`
   against that PR with `--comment`, so its findings are posted on the PR, and then end its turn.
2. **Fix every confirmed blocking finding.** Push the fixes, and answer each finding on the PR: fixed, or
   why it isn't a problem. If you make more than a small, targeted fix, have a new reviewer session check
   it again.
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
