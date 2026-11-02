# Patch notes

What changed on `main`, written for the next agent or developer. **Newest first.** Each entry says what a merge changed in behavior, contracts, data or shared UI pieces, which older assumptions it overturns, and what it left open. For the full story, follow the spec link.

**Adding an entry:** when you merge a feature or a behavior-changing fix, add an entry at the top in the same PR. Keep it to what someone working nearby needs to know. Use the date of the merge and link the PR.

---

## 2026-11-02 · Auto-merge for nbon12's PRs; all CI jobs are required checks · [#306](https://github.com/nbon12/hoa_management_system/pull/306)

Tooling only. No application behavior, API or data change.

**New**

- **Auto-merge** (`.github/workflows/auto-merge.yml`): a PR into `main` that nbon12 opens gets squash auto-merge, so GitHub merges it once the required checks pass. That includes every Claude session's PR, since sessions open PRs as nbon12. Dependabot's and anyone else's PRs are untouched.
- Left manual: drafts, PRs labelled `hold`, and PRs touching a guarded path from `.routines/merge-policy.md` (all of `.github/` counts). A later push, the `hold` label or a switch to draft turns auto-merge back off. The run's summary says why it skipped a PR.
- Like `/merge`, it uses `MERGE_TOKEN`, so the squash push still starts the push-to-main workflows.

**Changes earlier assumptions**

- **Required checks on `main`** are now Build & Test, Frontend (neko-hoa) and Ops scripts (shellcheck + tests), as well as the two Trivy scans. This was a settings change made alongside this PR. Before it, a PR could merge while Build & Test was still running (#299 did).
- **Open a PR as a draft until it is ready to merge.** Auto-merge checks required checks, drafts, `hold` and guarded paths, and none of the merge policy's other rules: resolved review threads, the verification list in the description, patch notes. A ready PR merges as soon as its checks pass.
- A session no longer merges its own non-guarded PR; auto-merge does. Guarded-path PRs still follow the merge policy's independent review.

---

## 2026-11-02 · Constitution 3.2.0: no human gates, human input is feedback after merge · [#300](https://github.com/nbon12/hoa_management_system/pull/300)

Spec: none (constitution amendment). Process and agent guidance only. No application behavior, API or data change.

**Changes earlier assumptions**

- **The maintainer is no longer a step in the pipeline.** New constitution section 14 ("Autonomous Delivery & Human Input", numbered after Governance so section 13 citations stay valid). New and changed specs, plans, tasks, checklists, quickstarts and PR descriptions must not contain:
  - tasks or checklist items assigned to a person;
  - manual verification or QA;
  - "STOP and validate", approval or sign-off checkpoints;
  - "things only a human can do" sections.

  Every check is an automated test or CI check. Existing specs are not rewritten only to remove an earlier human step.
- **Ambiguity is decided, not asked.** Pick the most defensible default, record it under the spec's Assumptions, and continue. No `[NEEDS CLARIFICATION]` markers and no mid-workflow questions. `CLAUDE.md` says this overrides the upstream Spec Kit text that asks the user: the clarification questions in `/speckit.specify` and the "incomplete checklists, proceed anyway?" prompt in `/speckit.implement`.
- **Unless otherwise specified:** when the maintainer themselves runs `/speckit.specify` or `/speckit.clarify` (or says they want to be involved), the agent engages and asks in those two phases. Plan, tasks, analyze, implement, the PR and the merge stay hands-off either way. Commands an agent, scheduled task or routine starts on its own are never maintainer-driven. The exception relaxes only "decide, don't ask": no human steps in artifacts and merge on green still apply.
- **A green PR always merges.** When every required check passes, the agent merges it or enables auto-merge, and nobody is asked to review at that stage. CI/CD or ops controls are automated guards (dry run, rate limit, kill switch, audit trail), not required reviewers. Product features where people act (a board vote, a moderation queue) are unaffected.
- **Exceptions are narrow:** third-party accounts and root credentials, billing, admin-only settings, amending the constitution, and an irreversible action on production data or real money that no guard bounds. Even then the work ships inert and safe by default, the maintainer is told once with exact steps, and nothing blocks a merge.
- **Templates changed.** The spec template's Constitution Requirements and Assumptions text, the plan template's Constitution Check, and the tasks template: "STOP and VALIDATE" is now "VALIDATE … continue when green", "Deploy/Demo" is "Deploy", quickstart validation is an automated check, and the Polish phase has a human-step audit task.
- **Testing constitution 2.1:** "approved in writing" now reads "documented in the plan", so it can't be read as an approval gate.

**Left open**

- Amendments to the constitution stay owner-merged under section 13. This one merged on the owner's explicit approval. The owner can relax the rule by amending section 13.

**Read together with the merge policy.** [`.routines/merge-policy.md`](../.routines/merge-policy.md) (#299, handoff task S3) landed first and agrees with section 14: no human review, and guarded paths get an independent *automated* review. `CLAUDE.md` now links it, which closes the open item in the #299 entry below.

---

## 2026-11-02 · Merge policy: sessions merge their own PRs · [#299](https://github.com/nbon12/hoa_management_system/pull/299)

Handoff: [`docs/handoffs/2026-11-02-zero-touch-operations.md`](handoffs/2026-11-02-zero-touch-operations.md) §3–4 (#295, task S3). Tooling only.

**Changes earlier assumptions**

- **Sessions now squash-merge their own PRs under [`.routines/merge-policy.md`](../.routines/merge-policy.md).** The conditions are:
  - checks are green;
  - no unresolved review threads and no "changes requested" review;
  - the PR is ready, and its description lists what was verified;
  - patch notes are included when required.

  A PR that touches guarded paths also needs an independent automated review by a separate session, and nbon12 gets a push notification after the merge. These paths are workflows, `scripts/pr-env-*`, `.routines/`, `CLAUDE.md`, `infra/`, Auth, Payments and migrations. Constitution changes stay owner-merged.
- **The Done story sweep's OWNER ONLY class no longer includes approvals or "merges reserved for the owner".** Workers merge under the policy. The sweep also skips the `ops-log` issue (#296).

**Left open**

- `CLAUDE.md` doesn't link the merge policy yet. The session's auto-mode check refuses `CLAUDE.md` edits.

---

## 2026-11-02 · Zero-touch ops: a policy gate replaces the `ops` approval, the sweep reaps orphans · [#298](https://github.com/nbon12/hoa_management_system/pull/298)

Handoff: [`docs/handoffs/2026-11-02-zero-touch-operations.md`](handoffs/2026-11-02-zero-touch-operations.md) (#295). Tooling only. No application behavior, API or data change.

**Changes earlier assumptions**

- **`pr-env-repair` writes no longer need a person to approve them.** This takes effect once nbon12 removes the `ops` Environment's required reviewer (handoff task O1). Until then, runs wait for approval *and* pass the new checks. The job now refuses, changes nothing and goes red unless all of these hold (`scripts/pr-env-policy-gate.sh`):
  - **P1:** repo variable `OPS_AUTOMATION` is `on`. This is the kill switch: unset or `off` refuses.
  - **P3:** there have been at most 3 write dispatches in 24 h, refused ones included.
  - **P4:** no other pr-env, teardown or repair run for the PR is unfinished.
  - **P5:** for `destroy` and `reap`, the PR is closed.
  - **P8:** the dispatcher has write access.
  - **P7:** a dry run in the same job plans only this PR's per-PR names (at most 10 resources), or, for `destroy`, only deletes under `module.pr_environment`.
- **The `allow_open_pr` input is removed.** An open PR's leftovers are reaped by its own next `pr-env` run.
- **The run-name is now `pr-env-repair <action> dry_run=<bool> (pr-<n>)`.** It still ends in `(pr-<n>)`. P3 counts the `dry_run=false` ones.
- **`pr-env-sweep` reaps orphans by name on its own.** These are closed PRs' leftovers with no state prefix, the PR 217 class. It acts only once the PR has been closed 24 h, does at most 3 PRs a run, and goes through the same gate and P7 check. The job is red only if orphans remain after the reap.
- **Failures are reported on the ops log issue [#296](https://github.com/nbon12/hoa_management_system/issues/296)** (label `ops-log`). Each repair write is also summarised on its PR.
- **#234's Verification 8 and task O10 are superseded** (decision 7 in the 2026-10-26 handoff). Run the new handoff's Z1–Z6 instead, in the order its Status section gives.

**Shared pieces to reuse**

- `scripts/pr-env-policy-gate.sh preflight|check-plan`: callers `repair` and `sweep`.
- `pr-env-reap.sh` `ITEMS_OUT=<file>` lists what a run would delete or did delete. Stripe and Pages lines now name their host or branch.
- `pr-env-audit.sh` `ORPHANS_OUT=<file>` lists the closed PRs that have orphans.

**Left open**

- Owner tasks O1 (remove the `ops` reviewer, after this merges), O2 (set `OPS_AUTOMATION=on`) and O3 (answer decisions D1 and D2).
- S7: run Z1–Z6 once O1 and O2 are done.

---

## 2026-11-02 · Done story sweep: dispatcher session + watchdog · [#289](https://github.com/nbon12/hoa_management_system/pull/289)

Tooling only. No application behavior, API or data change.

**Changes earlier assumptions**

- **A scheduled task that starts a fresh session never gets the session tools.** These are `list_sessions`, `create_session`, `send_message` and the rest. This holds whether the task was made in the claude.ai UI or from a session (both tested 2026-11-02). Its `claude-code-remote` server has at most `add_repo` and the PR subscribe tools. That is why the "Daily done checker" run on 2026-11-02 did nothing.
- **A session started from another session does have them** (origin `claude_code_mcp_seed`), and so does a scheduled task that messages that session.
- **The `@claude` workflow (`claude.yml`) works as of 2026-11-02.** Before that, every real run failed with "Either ANTHROPIC_API_KEY, CLAUDE_CODE_OAUTH_TOKEN … is required". The first successful run was [41497347693](https://github.com/nbon12/hoa_management_system/actions/runs/41497347693). The sweep doesn't start `@claude` runs. It hands off to sessions, and only counts an `@claude` run already in progress as "agent already on it" (Step 3).

**How the sweep runs now** (`.routines/done-story-sweep.md`, Step 0 picks the mode)

- **Dispatch:** each repo has a long-lived dispatcher session and a daily scheduled task that messages it:
  - "Done story sweep dispatcher: hoa_management_system (scheduled daily; do not archive)", messaged by **"Done story sweep dispatch: hoa_management_system"** at 5:17 AM America/New_York;
  - the mmo-engine pair, messaged at 5:29 AM.

  Each dispatcher runs its own repo's routine: triage, hand off to worker sessions, then post a `Done story sweep report <date>` comment on the open `sweep-log` issue. It sends a push notification only when something new needs nbon12, or a step failed. There is one dispatcher per repo because a session started from a session can't `add_repo` a second repo: auto mode denies it.
- **Watchdog:** the existing **"Daily done checker"** task (6:30 AM, a fresh session, push notifications on) runs the same file. With no session tools, it only checks that today's report exists in both repos, and pushes a notification if one is missing.
- The sweep skips the `sweep-log` issue as a candidate.

**Dispatcher setup / repair** (if the watchdog says the dispatcher didn't run)

1. The usual cause is that the dispatcher session was archived, which disables its scheduled task. From any interactive cloud session, create a new dispatcher session with `create_session`, using that repo as the source and the same title.
2. Point a daily task at it: `create_trigger` with `persistent_session_id` set to the new session, the old task's cron and the old task's prompt. Then delete the old task.
3. To test it, don't use "Run now" (`fire_trigger`). On a task bound to a session, it starts a fresh session with no session tools, and that session only runs the watchdog check (seen 2026-11-02). Instead, create a one-shot task with `run_once_at` and the same `persistent_session_id`, or `send_message` the task's prompt to the session. Then check that today's report appears on the `sweep-log` issue.

**Left open**

- If the dispatcher keeps failing, the watchdog could fall back to posting `@claude` comments in this repo. It doesn't do that today, to avoid duplicate handoffs. mmo-engine has no `claude.yml` at all.

---

## 2026-10-31 · Repo hygiene sweep: dead code, duplicate tests, spec/doc reconciliation, small spec fixes · [#280](https://github.com/nbon12/hoa_management_system/pull/280)

One-time first pass of the planned nightly hygiene routine. No API, data or UI change for residents or staff.

**Changes earlier assumptions**

- **Removed shared pieces. Don't call them; use the replacements.**
  - `ClaimsPrincipalExtensions` (`RequirePropertyId`/`RequireCommunityId`): resident endpoints use `ResidentAccess.RequireAsync` (030).
  - `AllocationService.ApplyPaymentAsync`: use `LedgerService.AddPaymentAsync`.
  - `LedgerService.RecomputeBalancesAsync`: running balances are computed per append, and 006 FR-007d forbids mutating ledger entries.
  - Frontend `BoardService.getMemberships/createMembership/updateMembership` and `ArchitecturalService.getSettings/putSettings`: use `CommunitySettingsService`.
  - `CommunityService.searchDocuments` and `PaymentsService.getBalance`.
  - `scripts/setup-test-db.sh` and `HOAManagementCompany.Tests/run-playwright-tests.sh`: tests always use Testcontainers, and the .NET Playwright tests were deleted in June.
- **Backend OTLP traces and metrics now go to `<OTEL_EXPORTER_OTLP_ENDPOINT>/v1/traces` and `/v1/metrics`.** Before, they went to the bare base URL, which HTTP/protobuf uses verbatim (005 FR-022). Logs and the browser proxy already appended their paths. Set the endpoint to the OTLP *base* URL.
- **Sign-in lockout settings are validated at startup.**
  - `Identity:Lockout:MaxFailedAttempts` (1–100) and `LockoutMinutes` (1–1440) are now `IdentityLockoutOptions`.
  - A bad value fails startup instead of silently weakening lockout. Defaults are unchanged (10 / 30).
- **The Sandbox test tier skips, rather than errors, without provider secrets.**
  - The harness now blanks every Twilio key unless both Twilio test credentials are set.
  - A plain local `dotnet test` with no secrets goes from 10 failures to 8 skipped. This resolves failure 2 of #216.
- **API fixes from spec 003/006:**
  - A missing or invalid token now gets `401 { code: "UNAUTHORIZED", message }`; the body used to be empty.
  - The dashboard `currentBalance` follows ledger `Sequence`, not `EntryDate`, so a backdated payment no longer leaves a stale balance. Same-date ledger rows list by `Sequence`.
  - `GET /community/documents?pinned=` filters, and `GET /community/events?category=A&category=B` returns the union.
- **Payment webhook fixes (006):**
  - A refund that includes the card fee restores only the principal on the owner's ledger (FR-004d).
  - `charge.refund.updated` is no longer cast to `Charge`, so it no longer fails every time.
  - A one-time ACH return no longer sends a failure alert (FR-014c: recurring only).
  - `PUT /payments/recurring` is rate-limited with the `payments` policy.
- **Manual `security-scan` runs** (`workflow_dispatch`) scan the published `sakurapatch/nekohoa-api:latest`, as the 011 quickstart says. PR and push runs are unchanged.
- **Per-PR Stripe webhooks** also subscribe to `charge.dispute.created` and `charge.dispute.closed`. They still skip `charge.refund.updated`: its payload is a Refund object, which `WebhookProcessor` can't read.
- **Spec text now matches what shipped**, with "Amended by PR #…" / "Superseded by specs/NNN" notes. Older specs (005–030) were updated for later specs and for deliberate changes such as Artifact Registry (#242), ARC refinements (031) and SES (026). Stale READMEs were refreshed; the tests README describes Testcontainers.

**Left open**

- Spec gaps too large or too risky for an unattended pass are tracked as `spec-gap` issues: #263–#271 and #273–#279.
  - The most urgent is #275: nothing schedules `run-drafts`/`reconcile` in deployed environments.
- The constitution's Docker Hub mandate versus Artifact Registry is #272.
- Pre-existing: the skipped Cypress test in `alert-preferences.cy.ts` stays parked under #215. Test factories in `HOAManagementCompany.Tests/Factories` are unused but kept, as the constitution's prescribed pattern.

---

## 2026-10-31 · Architecture Police: nightly duplicate-pattern consolidation · [#259](https://github.com/nbon12/hoa_management_system/pull/259)

Tooling only. No application behavior, API or data change.

**What changes for people working nearby**

- The **Architecture Police** scheduled task runs nightly at 4:41 AM America/New_York. It follows `.claude/skills/architecture-police/SKILL.md`.
- Each run consolidates **one** duplicated pattern into a single shared implementation, with no behavior change. It squash-merges a `refactor(architecture-police): …` PR from an `architecture-police/<date>` branch, but only once every check is green.
- Each of those PRs adds its own entry here, naming the canonical piece and the removed variants. Read those entries before re-adding a helper.
- It skips candidates that touch files in other open PRs. A branch opened later may still need a rebase after a night's merge.
- It never changes `infra/**`, `.github/**`, migrations, `specs/` or `.specify/`. Duplicates found there go into the PR description as findings.

**Shared pieces you can reuse**

- `.claude/skills/architecture-police/find_clones.py` is a standard-library clone finder:
  - `python3 -I find_clones.py [--fuzzy] [--min-lines N] PATH…`;
  - `--fuzzy` also matches copies with renamed identifiers;
  - it is useful before adding a helper, to check whether one already exists.

---

## 2026-10-29 · Time zone dropdown and plain lapse-rule wording · [#247](https://github.com/nbon12/hoa_management_system/pull/247)

Frontend only, no API or data change. It touches the architectural review form shared by Community settings › Architectural review and Company defaults (030 briefs 07 and 08).

**Changes earlier assumptions**

- **Time zone is picked, not typed.** The field is a searchable dropdown of IANA names (`app-combo-picker` in its new `dropdown` mode). Clicking it or its ▾ button lists every zone, with the common US zones first. Typing filters by name, city, generic name ("Eastern"), abbreviation ("EST", either season) or offset ("UTC-5", either season). The common US zones also match big US cities that aren't IANA names ("Dallas") and "Arizona", "Alaska" and "Hawaii". Name matches are listed first, and the first match is highlighted, so Enter chooses it. The list opens over the content below it. Text that isn't chosen from the list is discarded on blur, so the form can no longer send a mistyped zone.
  - The list comes from `Intl.supportedValuesOf('timeZone')`, plus `UTC`. Chrome lists 19 renamed zones by their old names (`Asia/Calcutta`, `Europe/Kiev`, `America/Indianapolis`, …). `RENAMED_TIME_ZONES` in `shared/time-zones.ts` maps them to the current names (when the browser resolves the current name), so every browser saves the same name. A saved zone that isn't in the list still shows, first.
  - The server is unchanged: `ArcRuleValidator` still accepts any zone `TimeZoneInfo` resolves.
- **Lapse rule wording.** These are UI labels only. The enum and API values are unchanged.
  - The field is now "After the review period ends without a decision". It was "When the review period ends without a decision".
  - The options are now "Auto approve" (`DeemedApproved`), "Auto reject" (`DeemedDenied`) and "No automatic action" (`FlagOverdueOnly`), in that order. Spec 027 calls them "deemed approved", "deemed denied" and "flag overdue only".
  - A helper under the field explains what each one does, including the exception: under "Majority of votes cast (with a quorum)", a side that leads with a quorum at the due date decides instead.
  - The behavior is spec 027 FR-027, as before: `ArcSweepService` reaches the decision once the due date passes, and a Community Manager records it.

**Shared frontend pieces you can reuse**

- `ComboPickerComponent` has new opt-in inputs:
  - `dropdown`: click or ▾ to list every option. The list is an overlay, and typing highlights the first match.
  - `helperFirst`: the helper goes above the field.
  - `inputId`: the field's id, for error-summary links.
  - `settingId`: sets `data-setting-id`, for settings search.
- `PickerOption.keywords` holds search words that aren't shown. Label matches are listed before keyword-only matches.
- In every mode, the active option now scrolls into view as you arrow through a long list, and a press anywhere in the open list (its padding or scrollbar) keeps focus in the field.
- `timeZoneOptions(current)` in `shared/time-zones.ts` gives the time zone list.

**Left open**

- `lapseRuleCitation` still has no field in the Architectural review form (031 T024).

---

## 2026-10-29 · Fix: ARC settings Save stuck on "Saving…" · [#248](https://github.com/nbon12/hoa_management_system/pull/248)

Spec: [`specs/030-company-administration/contracts/community-settings.md`](../specs/030-company-administration/contracts/community-settings.md) § Architectural review section.

**Changes earlier assumptions**

- `PUT /api/v1/communities/{cid}/architectural-settings` now answers with the **GET body** (`ArcSettingsSectionDto`: the rules plus `companyDefaults`, `differsFromDefaults`, `canEdit` and `lapseRuleCitation`), the same as `POST …/reset`. It used to answer with 027's narrower `ArcSettingsDto`. Every 027 field is still there, so clients that read only those are unaffected.
- `ArcSettingsDto` is no longer sent by any endpoint. `ArcSettingsSection` still builds it in memory, and the `ArcSettingsChanged` log uses it.

**Why**

- Community settings › Architectural review stores the PUT result as its state. With the narrow body, its `summary()`/`matches()` computeds threw on every change-detection pass. Save then stayed on "Saving…" although the write had succeeded.
- Component specs that mock the service can't catch this kind of drift. When a write's result replaces UI state, answer with the GET body, and pin that in a backend integration test.

**Tests**

- `ArcSettingsSectionTests.SavingTheRules_AnswersWithTheGetBody`, run as Company Administrator and as Community Manager. It checks that the PUT body carries the section fields and equals the next GET.

---

## 2026-10-29 · ARC review refinements (031) · [#243](https://github.com/nbon12/hoa_management_system/pull/243)

Spec: [`specs/031-arc-review-refinements/spec.md`](../specs/031-arc-review-refinements/spec.md). Builds on 027 (board ARC review) and 029 (resident ARC requests), and amends both specs.

**Changes earlier assumptions**

- **Votes can change.** 027's "votes are final" no longer holds. A board member can undo and recast a vote until the decision is final. That is one day after the deciding threshold is met, but never past the end of the due date in the application's time zone. A decision reached on or after the due date can change until the end of that day.
  - Lapse decisions and votes-cast decisions made at the due date are final at once.
  - Logic: `ArcVoting` in `Features/Board/Architectural/`.
- **Votes during the grace period.** Votes are accepted while a decision is in its grace period, and they are re-evaluated on every change:
  - losing the threshold reopens the application;
  - a different winning side starts a new grace period;
  - the same winning side keeps the original deadline.
- **Recording the outcome waits.** Recording an outcome during the grace period returns `409 DECISION_NOT_FINAL`.
  - **Tests:** advance `Clock.UtcNow` past the row's `VotesLockAt` before recording.
  - **Comparing instants:** compare DB round-tripped timestamps with a 1 ms tolerance, because Postgres stores microseconds.
- **Recusal waiver.** A board member linked to the property is recused only while someone else can still vote. A one-member board's member votes on their own property. See `ArcQueries.Eligible` and `IsRecusedAsync(userId, communityId, propertyId)`.
- **Board size is fixed at decision.** Final and closed tallies use `ArchitecturalApplications.EligibleVoterCount`, snapshotted when the decision is reached, at withdrawal, or at close if missing. They do not use the current board size.
- **UI wording only.** The `RevisionsNeeded` vote choice is labelled **"Request revisions"**; the enum and API values are unchanged. "Info" / "Request info" is labelled **"Ask question"**.

**New API**

- `DELETE /api/v1/communities/{cid}/architectural-applications/{id}/votes/mine` undoes the caller's vote.
  - Error codes: `NOT_VOTED` (404), `APPLICATION_DECIDED`, `APPLICATION_CLOSED`.
- `POST /api/v1/property/architectural-applications/{id}/comments` posts a homeowner comment.
  - Multipart body: `message` plus up to 5 `files`.
  - Allowed on any request that wasn't withdrawn.
  - Files count toward the application's file limits and carry over to a revision.
- **New board list/detail fields:** `decisionFinal`, `votesLockAt`, `canUndoVote`.
- **New board detail fields:** `ownerComments`, `lapseStatement`. Attachments gain `ownerCommentId`.
- **New resident detail fields:** `comments`, `canComment`.
- `lapseRuleCitation` is an optional field on `PUT …/architectural-settings` and is returned by its GET. It is per-community. It is not a company default, and "Reset to company defaults" leaves it alone.

**Data:** migration `20261027022709_AddArcReviewRefinements` (additive).

- `ArchitecturalApplications`: + `DecisionFinalAt`, `EligibleVoterCount`.
- New table `ArchitecturalOwnerComments`.
- `ArchitecturalAttachments`: + `OwnerCommentId`.
- `CommunityArcSettings`: + `LapseRuleCitation`.

**Shared frontend pieces you can reuse**

- `VoteChoiceComponent` (`app-arc-vote-choice`): pick a choice, then a Submit/Save button appears.
- `FinalMarkComponent` (`app-arc-final-mark`): labelled ✓ / ✕ / ↻ marks.
- Global styles in `styles.scss`:
  - `.vote-choice` (with hover and `aria-pressed` states);
  - `.skeleton` (shimmer; stops under reduced motion);
  - `.final-mark--*`;
  - `.btn--danger`;
  - a hover tint on `.field`.
- On the board ARC page, rows are clickable and the page re-reads its counts from the server after every change. Don't decrement counts locally.

**Seed (Dev)**

- `ArchitecturalSeeder` removes the demo staff's links to properties a non-demo user lives on. This was the cause of the "0/0 board" on the demo resident's requests.
- The seeder adds **ARC-1040**, closed as approved by default.
- The demo community's citation is a placeholder ("Article IX, Section 9.4 of the Declaration of Covenants, Conditions and Restrictions"). Replace it with the real clause.

**Left open**

- No field yet for `lapseRuleCitation` in the Community settings "Architectural review" form. That form is shared with company defaults (030), so the field must appear in the community variant only. Tracked as T024 in `specs/031-arc-review-refinements/tasks.md`.
- The updated Playwright and Cypress ARC specs haven't run against a live stack yet.
- A Claude Design brief for the new UI states is in [`specs/031-arc-review-refinements/design-brief.md`](../specs/031-arc-review-refinements/design-brief.md).

---

## Before 2026-10-29

This file starts with #243. For earlier work, read the feature specs under `specs/` (each one's `spec.md` lists its acceptance scenarios and amendments), the "Recent Changes" list in `CLAUDE.md`, and the incident handoffs under [`docs/handoffs/`](handoffs/).
