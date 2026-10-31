# Patch notes

What changed on `main`, written for the next agent or developer. **Newest first.** Each entry says what a merge changed in behavior, contracts, data or shared UI pieces, which older assumptions it overturns, and what it left open. For the full story, follow the spec link.

**Adding an entry:** when you merge a feature or a behavior-changing fix, add an entry at the top in the same PR. Keep it to what someone working nearby needs to know. Use the date of the merge and link the PR.

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
