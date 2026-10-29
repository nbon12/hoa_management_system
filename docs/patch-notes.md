# Patch notes

What changed on `main`, written for the next agent or developer. **Newest first.** Each entry says what a merge changed in behavior, contracts, data or shared UI pieces, which older assumptions it overturns, and what it left open. For the full story, follow the spec link.

**Adding an entry:** when you merge a feature or a behavior-changing fix, add an entry at the top in the same PR. Keep it to what someone working nearby needs to know. Use the date of the merge and link the PR.

---

## 2026-10-29 · Time zone dropdown and plain lapse-rule wording · [#247](https://github.com/nbon12/hoa_management_system/pull/247)

Frontend only, no API or data change. It touches the architectural review form shared by Community settings › Architectural review and Company defaults (030 briefs 07 and 08).

**Changes earlier assumptions**

- **Time zone is picked, not typed.** The field is a searchable dropdown of IANA names (`app-combo-picker` in its new `dropdown` mode). Clicking it or its ▾ button lists every zone, with the common US zones first. Typing filters by name, city, generic name ("Eastern") or offset ("UTC-5"). Text that isn't chosen from the list is discarded on blur, so the form can no longer send a mistyped zone.
  - The list comes from `Intl.supportedValuesOf('timeZone')`, plus `UTC`. Chrome lists 19 renamed zones by their old names (`Asia/Calcutta`, `Europe/Kiev`, `America/Indianapolis`, …). `RENAMED_TIME_ZONES` in `shared/time-zones.ts` maps them to the current names, so every browser saves the same name. A saved zone that isn't in the list still shows, first.
  - The server is unchanged: `ArcRuleValidator` still accepts any zone `TimeZoneInfo` resolves.
- **Lapse rule wording.** These are UI labels only. The enum and API values are unchanged.
  - The field is now "After the review period ends without a decision". It was "When the review period ends without a decision".
  - The options are now "Auto approve" (`DeemedApproved`), "Auto reject" (`DeemedDenied`) and "No automatic action" (`FlagOverdueOnly`), in that order. Spec 027 calls them "deemed approved", "deemed denied" and "flag overdue only".
  - A helper under the field explains what each one does.
  - The behavior is spec 027 FR-027, as before: `ArcSweepService` reaches the decision once the due date passes, and a Community Manager records it.

**Shared frontend pieces you can reuse**

- `ComboPickerComponent` has new opt-in inputs:
  - `dropdown`: click or ▾ to list every option.
  - `helperFirst`: the helper goes above the field.
  - `inputId`: the field's id, for error-summary links.
  - `settingId`: sets `data-setting-id`, for settings search.
- `PickerOption.keywords` holds search words that aren't shown.
- The active option now scrolls into view as you arrow through a long list.
- `timeZoneOptions(current)` in `shared/time-zones.ts` gives the time zone list.

**Left open**

- `lapseRuleCitation` still has no field in the Architectural review form (031 T024).

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
