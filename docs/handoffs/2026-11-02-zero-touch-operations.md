# Handoff — zero-touch operations: replace manual approvals with automated guards

**Date:** 2026-11-02 · **Repo:** `nbon12/hoa_management_system` (PUBLIC) · **Tracking issue:**
[#295](https://github.com/nbon12/hoa_management_system/issues/295) · **Related:**
[#234](https://github.com/nbon12/hoa_management_system/issues/234) (cloud access, Verification 8),
[`docs/handoffs/2026-10-26-agent-cloud-access.md`](2026-10-26-agent-cloud-access.md),
`.routines/done-story-sweep.md`.

Point a fresh session at this file. It is a handoff and runbook, not a Spec Kit spec. Branch the
work off `main`.

**The owner's goal (2026-11-02):** "I don't really want to have to approve things manually, like, at
all." Sessions and scheduled routines should finish routine work end to end. The owner is
**notified** of what happened and **asked** only for what no automation can do: creating accounts,
paying, and product or constitution decisions.

Every gate below was read from the repo on 2026-11-02. The repo's GitHub Environment settings
can't be read through the cloud session's proxy, so their current values are taken from the
2026-10-26 handoff and marked **unverified**.

## Status (2026-11-02)

Read this first. Where it differs from the plan below, this section wins.

**Done in the S1/S2/S4–S6 PR:**
- **S1.** `pr-env-repair.yml` runs `scripts/pr-env-policy-gate.sh` in place of "Require the ops approval
  gate". `preflight` checks P1, P3, P4, P5 and P8 before any credential is minted. `check-plan` runs P7 on a
  dry run, in the same job, before anything is deleted:
  - for `reap`, on `pr-env-reap.sh`'s new `ITEMS_OUT` file;
  - for `destroy`, on the saved destroy plan's actions and addresses.

  `allow_open_pr` is gone. The run-name now carries `dry_run=<bool>`, which P3 counts. The job's last step
  posts the audit trail to the step summary and, for a write, on the PR. On failure it also posts it on
  the ops-log issue. Unit tests are in `scripts/tests/pr-env-ops.test.sh` ("gate …" and "plan P7 …") and
  `pr-env-reap.test.sh` ("ITEMS_OUT …").
- **S2.** `pr-env-audit.sh` writes `ORPHANS_OUT`, one closed PR number per line. `pr-env-sweep.yml` then
  runs these steps:
  1. it audits without failing;
  2. it reaps at most 3 closed PRs through the gate (`CALLER=sweep`: P1, P4, and P5 with 24 h since the
     PR closed), a dry run and P7;
  3. it comments on each reaped PR;
  4. it re-audits, and that step fails the job if orphans remain;
  5. on failure, it comments on the ops-log issue.
- **S3 (follow-up PR, mostly done).** `.routines/merge-policy.md` is added, and
  `.routines/done-story-sweep.md` is updated as in §4 (nbon12 authorized these edits on 2026-11-02). The
  mmo-engine copy is updated in its own PR. **Still open:** the `CLAUDE.md` link to the merge policy.
  The session's auto-mode check refuses `CLAUDE.md` edits as self-modification, so nbon12 adds it or
  authorizes it.
- **S4.** The 2026-10-26 handoff has decision 7, and Verification 8, O10 and decision 5 are marked
  superseded. #234 has a comment.
- **S5.** The ops log is [#296](https://github.com/nbon12/hoa_management_system/issues/296) (label
  `ops-log`). The Done story sweep skips it.
- **S6.** See [S6 result](#s6-result-the-rulesets-unattributed-changes-flag) below.

**Where the implementation differs from the plan, and why:**
- **P1 also covers a `destroy` dry run.** That run still mints the deployer's credentials, so the kill
  switch stops it too. **P5 also covers a `destroy` dry run.** A plan of an open PR's state isn't needed:
  its own next `pr-env` run handles leftovers.
- **P3 counts every write dispatch in 24 h, refused ones included.** A session retrying into refusals is
  what the limit is for. The run itself and dry runs aren't counted. Runs from before this change have the
  old run-name, so they don't count.
- **P7's cap of 10 counts only Cloud Run, R2, secrets, Neon and Stripe.** Pages deployments (one per push)
  and state-object versions (one per apply) are still name-checked against `pr-<n>`, but counting them
  would refuse an ordinary reap of a busy PR. `destroy` has no cap. Every planned change must be a
  `delete` under `module.pr_environment`.
- **Z1 can't use PR `0`.** The `diagnose` job, which `repair` needs, rejects a `pr_number` that isn't a
  positive integer. Use a closed PR with no state lock, such as 217.
- **Run Z1–Z6 in this order: Z6, Z5, Z1, Z2, Z4, Z3.** Z5, Z1 and Z2 use the day's three writes, so Z4 is
  the fourth. Z3 is refused by P1, which runs before P3, so the order doesn't affect it.

### S6 result: the ruleset's unattributed-changes flag

- **Docs not checked.** `docs.github.com` isn't reachable from the cloud session's network policy, so this
  wasn't checked against GitHub's documentation.
- **Third-party reports** (the ginsys/.github#14 and cbusillo/codex-skills#792 PRs, 2026-09) describe the
  key this way:
  - it is a public-preview `pull_request` rule parameter;
  - the API sets it to `true` when a payload leaves it out;
  - it can make a PR wait for a human approval even with 0 required reviews. Their cases are PRs opened by
    a GitHub App bot.
- **Evidence from this repo:** session-opened PRs merged under the current ruleset with no approving
  review. [#282](https://github.com/nbon12/hoa_management_system/pull/282) was opened at 19:52:11 UTC and
  merged at 19:52:17 on 2026-11-01. #281 merged three minutes after it opened. Both were authored as nbon12.
  So the flag doesn't block a session's squash merge today.
- **Still open:** if a merge is ever blocked with the "unattributed changes" reason, the owner sets the key
  to `false` on the `main` ruleset (Settings → Rules → Rulesets). The ruleset can't be read or changed from
  a cloud session.

---

## TL;DR

- **Today there are three places where the owner must click.**
  1. The `ops` Environment's required reviewer, on every `pr-env-repair` run that changes something.
  2. Merges that the issue or handoff doesn't explicitly hand to sessions.
  3. The planned Prod apply gate (spec 010 FR-026). Prod doesn't exist yet, so this one is latent.

  The sweep's "OWNER ONLY" class also exists largely because of 1 and 2.
- **Replace each click with a guard written in code**, which:
  - checks preconditions that a machine can verify;
  - limits what a run can touch to resources named for one closed PR;
  - does a dry run first and aborts on any surprise;
  - has a rate limit and a kill switch;
  - leaves an audit trail.

  The owner gets a notification after the fact, and only when something failed or looked unusual.
- **Do routine repairs before anyone has to ask.** The daily `pr-env-sweep` should reap orphans found
  by name (PR 217's class of leftover) automatically, instead of only reporting them for a manual
  `reap`.
- **Merge policy.** Sessions squash-merge their own PRs when the checks in
  [Merge policy](#3-merge-policy) hold. PRs that touch the guardrails themselves get an independent
  automated review and a notification after the merge, not a human approval (decision D1).
- **#234's Verification 8 becomes moot.** It tested whether a session could approve its own `ops`
  run, and there's no approval left to bypass. Verification Z1–Z6 below replaces it: the guards
  refuse what they should.
- **The owner's one-time work** (O1–O3): turn off the `ops` required reviewer, set one repo
  variable, and answer decisions D1 and D2. After that, nothing routine needs a click.

---

## 1. Inventory: every manual gate today

| # | Gate | Where | What it guards | Replace with |
|---|---|---|---|---|
| G1 | `ops` Environment required reviewer | `.github/workflows/pr-env-repair.yml:141` (`environment: ops`), and the step "Require the ops approval gate" (`:147-157`), which **refuses to run unless a required reviewer exists** | `unlock`, `destroy`, `reap` with `dry_run=false`, and a `destroy` dry run | Policy-gate step ([§2.1](#21-pr-env-repairyml-policy-gate-instead-of-a-reviewer)) |
| G2 | Orphans found by name need a manual `reap` | `scripts/pr-env-audit.sh` header ("Reaping goes through pr-env-repair.yml action=reap"); `pr-env-sweep.yml:149-159` only audits | Leftovers with no state prefix (PR 217) | Auto-reap in the sweep ([§2.2](#22-pr-env-sweepyml-reap-orphans-automatically)) |
| G3 | Merges reserved for the owner | `.routines/done-story-sweep.md` worker message steps 4–5 ("Don't merge a PR unless the issue, its handoff or nbon12 says sessions merge that work") | Every PR a session opens | Merge policy ([§3](#3-merge-policy)) |
| G4 | The sweep's OWNER ONLY class lists "approvals" and "reviews or merges reserved for the owner" | `.routines/done-story-sweep.md` Step 2 | Issues whose remaining work is G1 or G3 | Shrinks automatically once G1 and G3 are gone ([§4](#4-routine-and-doc-updates)) |
| G5 | Prod apply behind a required reviewer | spec 010 FR-026 and clarification Q (`specs/010-dev-env-iac-opentofu/spec.md:27,129,299-301`); the commented `apply-prod` job in `infra-apply.yml:61-80` | Prod infra changes (Prod doesn't exist yet) | Automated promotion gates (decision D2, [§5](#5-prod-later-decision-d2)) |
| G6 | #234 Verification 8 plus task O10 (an operator starts a cloud session to probe self-approval) | handoff 2026-10-26, decision 4 and Verification §8 | That G1 is a real control | Retired. Verification Z1–Z6 replaces it ([§6](#6-verification)) |

**Not changed. These aren't owner approvals of the owner's own work:**
- **Fork policy:** the "Require approval for all external contributors" Actions setting and the
  same-repo guard in `pr-env.yml` (spec 013). They keep strangers' PRs from running with secrets.
- **The ruleset on `main`:** PR required, squash only, linear history, no deletion or force-push.
  It already requires **0** approving reviews. Its `require_extra_approval_for_unattributed_changes:
  true` setting wasn't checked against GitHub's documentation (**unverified**). Check that it never
  blocks a session's merge (task S6).

---

## 2. Infra repair without approval

### Threat model

The gate exists to stop a **misled session** from destroying something. A session could be misled
by a prompt injection in an issue comment, a wrong inference, or a bad input. The gate is not
there to stop an attacker with write access, who could edit the workflow anyway.

So a machine-checked guard does the same job, provided:
- the guard lives in workflow code on `main`, so a dispatch can't change it;
- its preconditions are checked against live state, not taken from the inputs on trust;
- the worst case it allows is bounded: one closed PR's ephemeral resources, which are disposable by
  design (spec 013).

Shared Dev, Staging and Prod resources are never in reach. `pr-env-repair` only knows the per-PR
name patterns: `nekohoa-api-pr-<n>`, `pr-<n>-*`, Neon `pr-<n>`, `nekohoa-pr-<n>-documents`, and
Stripe endpoints with `metadata.pr=<n>`. `scripts/pr-env-reap.sh` already refuses anything without
the `pr-number=<n>` label, or a Neon branch that isn't a child of `pr-base`.

### 2.1 `pr-env-repair.yml`: policy gate instead of a reviewer

Keep `environment: ops`. It still limits runs to `main` and scopes the environment's secrets
(**unverified**: the current branch policy is assumed from task O9). Remove only its required
reviewer (O1). Replace the step "Require the ops approval gate" with a step **"Policy gate"** that
runs before any write and exits non-zero, changing nothing, unless every check holds:

| Check | Applies to | How |
|---|---|---|
| P1. Kill switch | all writes | Repo variable `OPS_AUTOMATION` is `on`. Unset or anything else means refuse. |
| P2. Ref | all | `github.ref == refs/heads/main`. Already in the job `if:`. Keep it. |
| P3. Rate limit | all writes | At most **3** `pr-env-repair` runs with `dry_run=false` in the last 24 h (`gh run list --workflow pr-env-repair.yml --created ">=<now-24h>"`, filtered by run-name). Refuse the 4th. |
| P4. No live run for this PR | all writes | Reuse `scripts/pr-env-clear-stale-lock.sh`'s `CHECK_LIVE_RUNS` logic: no queued or in-progress `pr-env`, `pr-env-teardown` or other `pr-env-repair` run for `pr-<n>`. |
| P5. PR closed | `destroy`, `reap` | `gh pr view <n> --json state` is `CLOSED` or `MERGED`. The automated path **removes `allow_open_pr`**. An open PR's leftovers are reaped by its own next `pr-env` run (`ONLY_UNTRACKED`), so it never needs this. |
| P6. Lock is stale and is the one named | `unlock` | Already in the job: the generation must equal `lock_id`, and it must be at least 45 min old. Keep it. |
| P7. Dry run first, same job | `destroy`, `reap` | Run the action with `DRY_RUN=true`, capture the planned deletions, and check that every name matches the per-PR patterns for `<n>` and there are at most **10** of them. Only then run with `DRY_RUN=false`. For `destroy`, `tofu plan -destroy` must list only addresses under `module.pr_environment` (`infra/environments/pr/main.tf:22`). |
| P8. Dispatcher is a repo collaborator | all | `github.actor` has write permission (`gh api repos/{repo}/collaborators/{actor}/permission`). `workflow_dispatch` already requires write access; this check keeps it explicit if that changes. |

**Audit:** the job writes a summary (action, PR, names deleted, run URL) to `$GITHUB_STEP_SUMMARY`
and comments it on the closed PR. On failure, it also comments on the ops log issue (S5), which
the owner watches. A refused run is a **failure** (exit 1), so it shows up red, never as a silent
skip.

**Rollout order matters.** Ship and merge the policy gate *before* the owner removes the reviewer
(O1). During the overlap, runs still wait for approval and pass the new checks too. If the order
were reversed, the current step 147-157 would refuse every run. That's safe, but repairs would be
blocked.

### 2.2 `pr-env-sweep.yml`: reap orphans automatically

After the existing step "Audit for orphans by name", add a step **"Reap orphans of closed PRs"**.
For each PR number the audit reports as an `::error::` (a closed PR with resources but no state
prefix):
- skip it unless the PR closed more than **24 h** ago, which avoids racing a teardown in progress;
- skip it unless P1 and P4 hold;
- run `scripts/pr-env-reap.sh` with `DRY_RUN=true`, then apply P7's name check, then run it with
  `DRY_RUN=false`;
- reap at most **3** PRs per sweep run, and leave the rest for the next day.

The audit script must emit a parseable list (one PR number per line to a file, or a JSON line).
Today it only logs. The audit step keeps failing the job if orphans remain after the reap step, so
the red run is the notification.

### 2.3 What sessions do

- **Before:** a session dispatches `diagnose`, then `reap` with `dry_run=false`, then tells the
  owner to approve the waiting run.
- **After:** a session dispatches `diagnose`, then `reap` with `dry_run=false`, and the run goes
  ahead if the policy gate passes. The session reads the job summary and reports the result on the
  issue. Update the session instructions in the 2026-10-26 handoff ("How a session uses it (GitHub
  MCP)" under "Ops workflows to add" §1, step 3): replace "Tell the operator which run to approve" with "Read the policy-gate
  result; if it refused, report which check and why".

---

## 3. Merge policy

New file `.routines/merge-policy.md` (also linked from `CLAUDE.md`). A session **squash-merges its
own PR**, or turns on auto-merge with `mcp__github__enable_pr_auto_merge`, when all of these hold
on the current head:

1. Every check run is complete, and none failed or was cancelled. The repo's rule for the per-PR
   environment check (`pr-env`) still applies: re-run it once, and if it fails again for an
   unrelated provisioning reason, say so in the PR (as `.routines/nightly-repo-hygiene.md` §6 does).
2. There are no unresolved review threads and no "changes requested" review.
3. The PR isn't a draft, and its description lists what was verified (tests run, CI links).
4. A patch-notes entry is included when `CLAUDE.md` requires one.
5. If the PR touches **guarded paths**, it has passed an **independent automated review**
   (decision D1).

**Guarded paths** are the code that enforces the guardrails, or that moves money or identity:

```
.github/workflows/**          scripts/pr-env-*           .routines/**
.specify/memory/constitution.md                          CLAUDE.md
infra/**                      HOAManagementCompany/Features/Auth/**
HOAManagementCompany/Features/Payments/**                **/Migrations/**
```

**Independent automated review (recommended answer to D1):**
- **Who reviews:** a *separate* session, started with `create_session` and given only the PR link
  and the diff. It runs the `code-review` skill at `high` and posts its findings on the PR.
- **What happens next:** the authoring session fixes every confirmed blocking finding, then merges.
- **Who gets told:** after the merge, the owner gets one push notification ("merged #N touching
  guarded paths: <paths>"), so they can review it after the fact.
- **The constitution is the exception:** §13 requires amendments to be "reviewed and approved like
  any architectural decision". Constitution changes stay **owner-merged** unless the owner amends
  §13.

**Never, under any policy:**
- skipping, disabling or quarantining a test to get green;
- merging with a red required check;
- force-pushing `main`;
- a session approving a GitHub deployment or review on its own PR.

None of these is needed once G1 is gone.

## 4. Routine and doc updates

- **`.routines/done-story-sweep.md`:**
  - **Step 2:** drop "approvals" and "reviews or merges reserved for the owner" from OWNER ONLY.
    Keep secrets, tokens, account or console settings, decisions, constitution amendments, and
    anything on the owner's computer.
  - **Worker message step 4:** replace "Don't merge a PR unless …" with "Merge under
    `.routines/merge-policy.md`".
  - **Worker message step 5:** remove "approvals" and "reviews or merges".
  - **Worker message step 6:** keep "never approve deployments". It becomes moot rather than
    restrictive.
  - Make the same edits in `nbon12/mmo-engine`'s copy, in its own PR. mmo-engine has no CI, so its
  merge policy rule 1 reads "the build and tests the repo has, run locally and listed in the PR".
- **`docs/handoffs/2026-10-26-agent-cloud-access.md`:**
  - Status: add decision 7, "Approval gate replaced by policy gate (this handoff)".
  - Mark Verification 8, task O10 and decision 5 superseded.
  - Change the acceptance line "Verification 8 passed, including the self-approval check" to "Z1–Z6
    passed".
- **#234:** comment with the change. Its last open items, O7 (optional) and O10, close as
  superseded.
- **`docs/patch-notes.md`:** one entry per merged PR.

## 5. Prod, later (decision D2)

Prod doesn't exist (`infra/environments/prod` is absent), so G5 isn't urgent. Decide before Prod is
built.

**Recommended:** amend spec 010 FR-026 so that Prod promotes **automatically** behind these gates:
- Staging applied with the same commit, and green for at least 24 h;
- the Staging smoke and E2E gates pass (the same Playwright and Cypress gates as Dev);
- `infra-plan` for Prod has no `destroy` or `replace` on stateful resources (Neon project or
  branch, R2 buckets, secrets). Any of those stops the run and notifies the owner instead;
- automatic rollback: Cloud Run traffic goes back to the previous revision if the health or error
  rate gate fails within 15 minutes.

The owner is notified of each Prod promotion.

The alternative is to keep FR-026's required reviewer for Prod only. That's the one remaining click,
and it guards real customer data.

## 6. Verification

Run these after S1–S3 merge and O1–O2 are done, in the order Z6, Z5, Z1, Z2, Z4, Z3 (see Status). Post
pass or fail per line on the tracking issue.
Each one is safe: it targets PR `0` (never existed), a closed PR with nothing left, or a dry run.

| # | Given | When | Then |
|---|---|---|---|
| Z1 | `OPS_AUTOMATION=on`; a closed PR with no state lock, such as 217 (`pr_number=0` fails `diagnose`; see Status) | dispatch `unlock` `dry_run=false` with any `lock_id` | The policy gate passes P1–P5. The job **refuses** (no lock, P6) and exits 1. It changes nothing, and it **didn't wait for approval** (no "Waiting" state in the run). |
| Z2 | an **open** PR `<n>` | dispatch `reap` `dry_run=false` | P5 refuses. The run is red. The summary names P5. |
| Z3 | `OPS_AUTOMATION=off` | dispatch any write | P1 refuses. Set the variable back to `on` afterwards. |
| Z4 | three `dry_run=false` runs in the last 24 h (Z1 can count) | dispatch a fourth | P3 refuses. |
| Z5 | a closed PR whose resources are already gone (for example 217) | dispatch `reap` `dry_run=false` | The dry run finds 0 names, nothing is deleted, the run is green, and a summary is posted on the PR. |
| Z6 | the sweep runs with no orphans | `workflow_dispatch` on `pr-env-sweep` | The audit is green, the reap step reports "nothing to reap", and nothing is deleted. |

Also add unit tests to `scripts/tests/pr-env-ops.test.sh` or `pr-env-reap.test.sh`, which CI runs
in "Ops scripts (shellcheck + tests)": P7's name filter must reject a shared name (`nekohoa-api-dev`,
`dev-jwt-secret`, Neon `main`) and accept per-PR names.

## 7. Tasks

### Session, in order

- [x] **S1.** `pr-env-repair.yml`:
  - replace the step "Require the ops approval gate" with "Policy gate" (P1, P3–P5, P7, P8);
  - drop `allow_open_pr` from the automated path;
  - add the summary and PR comment;
  - add the P7 name-filter unit tests.

  Merge it while the reviewer is still on (G1 overlap).
- [x] **S2.** `scripts/pr-env-audit.sh`: write parseable output. `pr-env-sweep.yml`: add the "Reap
  orphans of closed PRs" step (§2.2).
- [ ] **S3.** Add `.routines/merge-policy.md`, link it from `CLAUDE.md`, and update
  `.routines/done-story-sweep.md` (§4). Do the same in `nbon12/mmo-engine`.
- [x] **S4.** Update the 2026-10-26 handoff (decision 7; supersede V8, O10 and decision 5) and
  comment on #234.
- [x] **S5.** Create an "Ops log" issue (label `ops-log`, excluded from the Done story sweep like
  `sweep-log`). Workflows comment on it on failure.
- [x] **S6.** Check the ruleset's `require_extra_approval_for_unattributed_changes` against GitHub's
  documentation, and confirm a session's squash merge isn't blocked by it. Record the result here.
- [ ] **S7.** After O1 and O2: run Z1–Z6, post the results, and close the tracking issue when they
  all pass.

### Owner, one time

- [ ] **O1.** After S1 merges: Settings → Environments → `ops` → remove **Required reviewers**. Keep
  "Deployment branches: `main` only". (A session can't change Environment settings: the API path is
  blocked through the cloud proxy, and it needs admin.)
- [ ] **O2.** Settings → Secrets and variables → Actions → Variables → add `OPS_AUTOMATION` = `on`.
  Setting it to `off` later is the kill switch.
- [ ] **O3.** Answer D1 and D2 below in a comment on the tracking issue. Until D1 is answered,
  sessions use the recommended answer.

## 8. Decisions for the owner

- **D1. Guarded paths: automated review, or human merge?**
  - **Recommended:** an independent automated review plus a notification after the merge (§3).
  - **Alternative:** guarded-path PRs stay owner-merged. That means about one click per workflow or
    infra PR.
- **D2. Prod: automatic promotion behind gates, or keep one required reviewer?**
  - **Recommended:** automatic (§5), with an amendment to spec 010 FR-026.
- **D3. Limits.** The defaults are 3 writes a day (P3), 10 names per reap (P7), and 3 PRs per sweep
  (§2.2). Raise them if real incidents hit them.

## 9. What stays human, and why

- **Creating or rotating root credentials, accounts and billing:** GCP, Neon, Cloudflare, Stripe,
  Apple or Google developer accounts. Sessions hold no admin credentials by design (016 FR-004; the
  2026-10-26 handoff).
- **GitHub settings that need admin:** Environments, rulesets, secrets. That's O1 and O2 here, once.
- **Product and spec decisions, and constitution amendments (§13).**
- **Anything on the owner's own computer.**

Everything else should end with a notification, not a question.

## 10. Risks and open questions

- **A misled session reaps the wrong PR's environment.** That's bounded to closed PRs' disposable
  resources (P5, P7), and the PR comment records it. Recovery: push to the PR again to
  re-provision. A merged PR has nothing to re-provision, which is the point of the reap.
- **A session weakens a guard through a merged PR.** The guard files are guarded paths, so an
  independent review and an owner notification follow (D1). A merged change is visible in the
  patch notes.
- **GitHub sends no notification for a deployment that needs no approval.** That's fine: the
  summary, the PR comment and the red-run failure email cover it.
- **Unverified:**
  - the `ops` Environment's current settings;
  - the ruleset flag (S6).

