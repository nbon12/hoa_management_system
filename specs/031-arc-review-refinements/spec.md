# Feature Specification: ARC Review Refinements

**Feature Branch**: `claude/vibrant-euler-8fb60e` (cloud session branch; see the PR). Numbered 031 because `030` is Company Administration.
**Created**: 2026-10-10
**Status**: Draft
**Input**: Product-owner feedback on the merged Architectural Applications work (`027-board-arc-review`, `029-resident-arc-requests`), 15 items: clickable rows, two-step voting with an undo window, a final-decision column, skeleton loading, clearer wording, a fix for the "awaiting your vote" count, recusal and board-size fixes, an auto-approved demo, and a homeowner comment thread.

## Context

027 shipped board review and voting; 029 shipped resident submission. Using them on `dev.nekohoa.com` surfaced friction (voting fires on the first click, rows aren't clickable, panels pop in late), confusing wording ("you voted" next to other people's names, "review still due"), and two bugs:

- **"0/0" board and "no eligible board members"** on the demo resident's requests. Root cause: the dev seed put all five demo board members and the manager on the *same property as the demo resident* (before 030 Company Administration, `AuthSeeder.EnsureBoardUserAsync` linked the board user to the first claimed property and `ArchitecturalSeeder` co-homed the others there), so every board member was recused on that resident's requests. 030 stopped creating those links for new databases (staff sign in without a home) but kept them on databases seeded earlier, such as `dev.nekohoa.com`.
- **"1 awaiting your vote" after voting.** A vote cast from the detail panel refreshed the row but never the header counts.

This spec refines 027 and 029. Where it changes their behavior, those specs carry an "Amended by 031" note.

## Clarifications

### Session 2026-10-10 (from the product owner's request)

- Q: How long can a vote be changed? → A: Until the decision is final: one day after the deciding threshold is met, **except** on the last day to vote, which gets no extra day — the vote can change until the end of that day. A vote with no decision yet can be changed at any time while the application is open.
- Q: How is a vote changed? → A: An "undo" link appears after "you voted …" while the vote is still changeable. Undo removes the vote; the member then picks and submits again.
- Q: When can a board member be recused? → A: Not when recusal would leave nobody to vote — in particular, a single-member board's only member is never recused.
- Q: How many vote slots does a finalized application show? → A: The number of eligible board members when it was decided, even if the board has grown or shrunk since.
- Q: What does an auto-approval look like? → A: Where the vote card would be, a statement says the application was approved by default and cites the clause of the community's governing documents; the final result shows the green ✓ like any approval. The dev seed includes one (ARC-1040).
- Q: Who can comment on an application? → A: The homeowner, on any of their requests that wasn't withdrawn (open, decided or closed — typically to answer "revisions requested"), with text and/or files. The board and manager read the comments in their own section under the details. Board members keep using "Ask question" to talk to the owner.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Faster, safer board navigation and voting (Priority: P1)

As a board member, I open an application by clicking anywhere on its row, here or on Community Home. Voting takes two steps, pick then Submit (Save on the home card), so a stray click never casts a vote. The page shows skeletons while an application loads, and the counts stay right after I vote.

**Independent Test**: On Architectural Applications, click the whitespace of a row and see its panel open, with skeletons until it loads. Pick Deny on a row: nothing is cast until Submit. On Community Home, pick Approve on the card and see Save appear, with nothing pre-selected. Vote from the panel and see the "awaiting your vote" pill update.

**Acceptance Scenarios**:

1. **Given** the applications table, **When** I click the whitespace of a row, **Then** that application opens; a click on a control inside the row keeps that control's own action.
2. **Given** a row I can vote on, **When** I pick Deny, **Then** nothing is cast until I click Submit, which casts Deny.
3. **Given** the "Cast your vote" card, **When** it renders, **Then** Approve, Request revisions and Deny are offered, none is selected, and no Submit button shows until I pick one.
4. **Given** the Community Home "Needs your vote" card, **When** it renders, **Then** no option is pre-selected and no Save button shows; **When** I pick Approve, **Then** Save appears and nothing is cast until I click it.
5. **Given** the vote options on any surface, **When** I hover one, **Then** it shows the app's hover tint (pink background, rose border); a picked option shows lavender with a dot.
6. **Given** finalized decisions, **When** the table renders, **Then** a Final decision column after "Your vote" shows ✓ for approved, ✕ for denied and a yellow ↻ for revisions requested, each with a text label.
7. **Given** one application awaits my vote, **When** I vote on it from the row or the detail panel, **Then** the "awaiting your vote" pill reflects the server's count (it disappears at 0).
8. **Given** the Needs your vote card, **When** I click a row, **Then** I am taken to that application on the Architectural Applications page.
9. **Given** I click an application, **When** its details haven't loaded yet, **Then** two skeletons show in place of the details card and the vote card.

---

### User Story 2 - Clearer wording (Priority: P1)

**Independent Test**: Open an application with three votes and see each listed as "Name — voted …" and a "Board final result" line. Click "Ask question" and see the due-date notice.

**Acceptance Scenarios**:

1. **Given** three board votes, **When** the Details section renders, **Then** it lists "Aaliyah Brooks — voted approve", "Marcus Chen — voted approve", "Bianca Board — voted to request revisions", never "you voted" for someone else.
2. **Given** an application, **When** the Details section renders, **Then** it shows "Board final result:" with one of: "Not yet decided" (no decision, or one still in its grace period), "Application Approved", "Revisions Requested (effectively denied; the resident is asked to revise and resubmit)", "Denied", or, for a lapse decision, "Application Approved (by default — review period lapsed)" / "Denied (by default — review period lapsed)".
3. **Given** any board voting surface, **When** it renders, **Then** the RevisionsNeeded choice reads "Request revisions" (tally label "N request revisions").
4. **Given** an info request, **When** it is composed or shown on the application, **Then** the text reads: "Info requests do not delay the due date of the board vote result. Failure to approve or deny by the due date may result in auto-approval. To request revisions, please select "Request revisions"." (The composer also states "Board vote result due <date>.")
5. **Given** a row or the vote card, **When** it renders, **Then** the question action reads "Ask question" (formerly "Info" / "Request info").

---

### User Story 3 - Change a vote until the decision is final (Priority: P1)

**Independent Test**: Cast the deciding third approval of five, see "undo" with its deadline, undo it and see the application reopen. Let the grace period end and see the undo link replaced by the final ✓, and further votes refused.

**Acceptance Scenarios**:

1. **Given** I voted on an open application that has no decision, **When** I click undo, **Then** my vote is removed, the tally drops by one and I can vote again.
2. **Given** my vote reached the threshold less than a day ago (and before the due date's last day), **When** I undo it, **Then** the decision is withdrawn and the application is open again.
3. **Given** the decision would still hold without my vote, **When** I undo it during the grace period, **Then** the decision stands with its original deadline, and I can recast my vote until then.
4. **Given** the decision's grace period has ended, **When** I try to undo or cast a vote, **Then** the request is refused with `APPLICATION_DECIDED` and no undo link is offered.
5. **Given** I have not voted, **When** I call undo, **Then** it is refused with `NOT_VOTED`.
6. **Given** an application is closed, **When** I try to undo my vote, **Then** it is refused with `APPLICATION_CLOSED`.
7. **Given** a decision is still within its grace period, **When** the manager records the outcome, **Then** it is refused with `DECISION_NOT_FINAL`.
8. **Given** I am a Community Manager without a board seat, **When** I try to undo, **Then** I am refused (403).

---

### User Story 4 - Demo data that shows real states (Priority: P2)

**Independent Test**: After the dev seed runs, sign in as `board@nekohoa.dev`: the demo resident's requests show a 0/5 tally (not 0/0), and ARC-1040 in Closed shows the auto-approval.

**Acceptance Scenarios**:

1. **Given** a dev database where the demo board shares the demo resident's property, **When** the seeder runs, **Then** the demo board members' and manager's links to that property are removed (staff sign in without a home, 030 FR-036a) and the resident keeps hers, so nobody is recused on the resident's requests.
2. **Given** the dev seed, **When** it runs (including on databases seeded earlier), **Then** ARC-1040 exists, closed as approved by default (lapse rule), and the community's lapse-rule citation is set.

---

### User Story 5 - Board size and recusal (Priority: P2)

**Acceptance Scenarios**:

1. **Given** a board with a single member who owns the application's property, **When** the row renders, **Then** the member is not recused, can vote, it counts as awaiting their vote, and the tally reads 0/1 with the rule text "One of one votes decide."
2. **Given** a two-member board where one member owns the property, **When** that member views it, **Then** they are recused and only the other member counts (0/1).
3. **Given** an application was decided by a 3-member board, **When** two more members join, **Then** its final tally still shows 3 slots (3/3), while an open application shows all 5.

---

### User Story 6 - Explain a lapse decision (Priority: P2)

**Acceptance Scenarios**:

1. **Given** an application approved by default because the board did not act by its due date, **When** a board member opens it, **Then** where the vote card would be they see "Approved by default: the board did not approve or deny this application by its due date (<due>). Under <citation>, an application the board does not act on within the <N>-day review period is deemed approved." and the final result shows the green ✓.
2. **Given** a decision reached by votes, **When** it is opened, **Then** no lapse statement is shown.
3. **Given** I am the community manager, **When** I set the governing-document clause through the ARC settings API, **Then** it is saved, returned by the settings GET, and cited on later lapse decisions. *(A field for it in the Community settings "Architectural review" form, which 030 shares with company defaults, is a follow-up.)*

---

### User Story 7 - Homeowner comments (Priority: P2)

**Independent Test**: As a resident with a "revisions requested" request, post "Lowered the fence to 5ft" with a PDF. See it in "Comments to the board". As a board member, open the application and see it under "Homeowner comments" with the file.

**Acceptance Scenarios**:

1. **Given** my request was denied with revisions requested, **When** I post "Lowered the fence to 5ft" with revised-plan.pdf, **Then** the comment and its file appear in my comment section and not among the submitted files.
2. **Given** the owner commented, **When** a board member or the manager opens the application, **Then** the "Homeowner comments" section lists the owner's name, text, time and files, and each file opens through a short-lived link.
3. **Given** I write nothing and attach nothing, **When** I post, **Then** it is refused with `VALIDATION_ERROR`; a comment of only files is accepted.
4. **Given** my request was withdrawn, **When** I post a comment, **Then** it is refused with `APPLICATION_CLOSED`, and no comment form is shown.
5. **Given** another household's request, **When** I post a comment on it, **Then** it is refused with the non-disclosing 403.

---

### User Story 8 - Withdraw at the top (Priority: P3)

**Acceptance Scenarios**:

1. **Given** my request can be withdrawn, **When** the page renders, **Then** "Withdraw request" is a red button at the top right of the page header, not at the bottom.

### Edge Cases

- **Undo leaves the same side winning**: the decision and its original grace deadline stand; the member may recast.
- **Two deciding votes at once**: both are accepted (votes are open during the grace period); exactly one decision is recorded.
- **Decision reached after the due date** (flag-overdue-only communities keep voting open): the vote can change until the end of that day in the community's time zone.
- **Lapse and due-date (votes-cast) decisions** are final immediately; there is no grace period.
- **Rows decided before 031** have no board-size snapshot; they use the current board until closed, and recording the outcome snapshots it.
- **Comment files** count toward the application's file limits and carry over to a revision like any attachment.

## Requirements *(mandatory)*

> **Design references.** No wireframe exists for the new states; `design-brief.md` in this folder is the brief for Claude Design. Implementation uses existing `styles.scss` tokens.

### Board navigation and voting

- **FR-001**: Clicking anywhere on an application row (the board table and the Community Home card) MUST open that application; clicks on controls inside the row MUST keep their own action. The card's row navigates to `/app/board/architectural?open=<id>`.
- **FR-002**: Voting MUST be two-step on every surface: picking Approve / Request revisions / Deny only selects it (none pre-selected), and a Submit (table, "Submit vote" on the card in the panel) or Save (Community Home card) button, shown only after a pick, casts it. Options MUST have a hover background from the app palette.
- **FR-003**: A decision reached by votes MUST record `DecisionFinalAt` = the earlier of (threshold time + 24 hours) and the end of the later of (the due date, the day the threshold was met) in the application's time zone. Lapse and due-date decisions are final at once.
- **FR-004**: Votes and undos MUST be accepted while the application is Open or its decision is not yet final, and refused with `APPLICATION_DECIDED` once final or `APPLICATION_CLOSED` once closed. Every vote change MUST re-run the decision rule under the application's row lock: losing the threshold reopens the application; a different winning side records a new decision and a new grace period; the same side keeps its original grace period.
- **FR-005**: `DELETE …/architectural-applications/{id}/votes/mine` MUST undo the caller's vote (Board Member capability, `board-writes` rate limit, sensitive event `ArcVoteUndone`), returning the updated row, or `NOT_VOTED` (404) when there is no vote.
- **FR-006**: Recording the outcome MUST be refused with `DECISION_NOT_FINAL` while the decision is in its grace period.
- **FR-007**: The table MUST end with a "Final decision" column holding the "undo" link (with "until <time>") while the caller's vote can change, otherwise the final mark for a final decision (✓ approved, ✕ denied, yellow ↻ revisions requested, – withdrawn, each labeled), or "pending" during another member's grace period. List and detail responses carry `decisionFinal`, `votesLockAt` and `canUndoVote`.
- **FR-008**: The detail panel MUST show skeletons for the details card and the vote card from the moment an application is selected until it loads.
- **FR-018**: After any vote, undo or panel change, the page MUST re-read its counts from the server.

### Wording

- **FR-009**: The Details vote list MUST read "<name> — voted approve | voted to request revisions | voted deny".
- **FR-010**: The Details section MUST show "Board final result:" per US2-S2.
- **FR-011**: "Info" / "Request info" MUST read "Ask question".
- **FR-012**: The info-request notice and the info-requested marker MUST use the US2-S4 text.
- **FR-013**: The RevisionsNeeded choice MUST read "Request revisions" on board surfaces; the legal outcome and owner wording ("revisions requested") are unchanged.

### Board size, recusal, lapse

- **FR-014**: A board member linked to the property is recused only while at least one other active board member can vote.
- **FR-015**: Decisions MUST snapshot the eligible-voter count (`EligibleVoterCount`) when reached, at withdrawal and, if missing, at closing; final and closed tallies MUST use it.
- **FR-016**: Community ARC settings MUST hold a lapse-rule citation (≤ 300 characters, default "the community's governing documents"), accepted as an optional `lapseRuleCitation` on `PUT …/architectural-settings` and returned by its GET; it is not a company default and "Reset to company defaults" leaves it alone. Lapse decisions MUST expose the US6-S1 statement (`lapseStatement`), shown in place of the vote card.
- **FR-017**: The dev seed MUST remove demo staff links to any property a non-demo user lives on, and MUST include ARC-1040, approved by default.

### Homeowner comments

- **FR-019**: Text inputs on the board panel MUST show a hover tint signalling they take typing.
- **FR-020**: `POST /property/architectural-applications/{id}/comments` (multipart: `message`, up to five `files`) MUST let the owner of the active property comment on any of their requests that wasn't withdrawn. Files pass the same type sniffing and per-application limits as other uploads, are all checked before any is stored, and are linked by `OwnerCommentId`. Sensitive event `ArcOwnerCommented` (IDs and counts only).
- **FR-021**: Board members and managers MUST see the comments, with author, time and files, in a "Homeowner comments" section under the details. The resident sees them in "Comments to the board" with a form; comment files are not repeated among the submitted files.
- **FR-022**: A comment MUST have text (≤ 2,000 characters) or at least one file, and at most five files.
- **FR-023**: "Withdraw request" MUST be a red (`btn--danger`) button in the request page header's actions.

### Key Entities

- **ArchitecturalApplication** (027): adds `DecisionFinalAt` and `EligibleVoterCount` (both nullable).
- **ArchitecturalOwnerComment** (new): application, author, body (≤ 2,000), created time. Immutable.
- **ArchitecturalAttachment** (027): adds `OwnerCommentId` (nullable, cascade with the comment).
- **CommunityArcSettings** (027): adds `LapseRuleCitation`.

### Constitution Requirements

- **Tenant boundary / authorization**: Undo uses the 027 voting capability through `ICommunityScopeResolver`. Comments go through `ResidentArcScope` (active property only; non-disclosing 403). No endpoint spans communities.
- **Database/runtime**: One additive, forward-only migration `20261027022709_AddArcReviewRefinements` (after 030's `AddCompanyAdministration`).
- **Security and abuse controls**: Comment text is untrusted, length-limited and rendered as text. File checks reuse 029's validator. Rate limits: `board-writes` (undo), `resident-writes` (comments).
- **Observability**: New sensitive events carry IDs and counts only, never comment text or file names.
- **Accessibility**: Choice toggles expose `aria-pressed`; final marks are `role="img"` with a text label; skeletons sit in an `aria-busy` container; the shimmer stops under `prefers-reduced-motion`.
- **Executable & living spec**: every scenario above maps to a test, listed below. 027 and 029 carry "Amended by 031" notes where this spec changes them.

## Scenario → test map

| Scenario | Test |
| --- | --- |
| US1-S1 | `applications-page.component.spec.ts` › "opens an application when its row is clicked (031 item 1)" |
| US1-S2 | `applications-page.component.spec.ts` › "casts a row vote only after Submit (031 item 5)" |
| US1-S3 | `cast-vote-card.component.spec.ts` › "offers three unselected choices, Ask question and the comment box"; "casts Deny with the comment only after Submit vote" |
| US1-S4 | `vote-choice.component.spec.ts` › "pre-selects nothing…", "shows Save after a pick…"; `needs-your-vote-card.component.spec.ts` › "lists what needs my vote…", "approves with Approve then Save…" |
| US1-S5 | `vote-choice.component.spec.ts` › "uses the shared hover-styled choice buttons" |
| US1-S6 | `final-mark.component.spec.ts` › "renders ✓ / ✕ / ↻ with labels"; `applications-page.component.spec.ts` › "shows the final-decision mark instead of undo once final" |
| US1-S7 | `applications-page.component.spec.ts` › "refreshes "awaiting your vote" after a change from the panel"; "votes Approve from the row…" |
| US1-S8 | `needs-your-vote-card.component.spec.ts` › "opens the application when a row is clicked (031 item 2)" |
| US1-S9 | `application-detail-panel.component.spec.ts` › "shows two skeletons while the application loads" |
| US2-S1 | `application-detail-panel.component.spec.ts` › "lists every vote as "— voted …""; `arc-format.spec.ts` › "labels anyone's vote…" |
| US2-S2 | `arc-format.spec.ts` › "labels the board final result"; `application-detail-panel.component.spec.ts` › "shows the board final result", "shows "Not yet decided" with a pending note…" |
| US2-S3 | `arc-format.spec.ts` › "builds the FR-007 tally label…", "formats instants and labels choices"; `tally.component.spec.ts` |
| US2-S4 | `cast-vote-card.component.spec.ts` › "refuses an empty question and shows the due-date notice"; `application-detail-panel.component.spec.ts` › "shows the due-date notice on Ask question and on the info-requested marker"; `e2e/board-architectural.spec.ts` › "Ask question explains…" |
| US2-S5 | `cast-vote-card.component.spec.ts` › "emits the question"; `applications-page.component.spec.ts` › "opens the detail panel from the ID and from Ask question" |
| US3-S1 | `VoteChangeTests.Undo_BeforeDecision_RemovesVote_AndAllowsRecast`; `applications-page.component.spec.ts` › "offers undo while my vote can change…" |
| US3-S2 | `VoteChangeTests.Undo_DuringGrace_BelowThreshold_ReopensApplication` |
| US3-S3 | `VoteChangeTests.Undo_DuringGrace_AboveThreshold_KeepsDecision_AndAllowsRecast` |
| US3-S4 | `VoteChangeTests.AfterGrace_UndoAndVote_AreRefused`; `DecisionOnVoteTests.ThirdApproveOfFive_DecidesApproved_AndRefusesFurtherVotes` |
| US3-S5 | `VoteChangeTests.Undo_WithoutVote_IsRefused` |
| US3-S6 | `VoteChangeTests.Undo_OnClosedApplication_IsRefused` |
| US3-S7 | `VoteChangeTests.RecordOutcome_DuringGrace_IsRefused` |
| US3-S8 | `VoteChangeTests.Undo_ByManager_IsRefused` |
| FR-003 | `VoteChangeTests.DecisionOnLastDay_LocksAtEndOfDueDate`, `DecisionOnDayBeforeDue_GetsFullDay`, `DecisionAfterDueDate_LocksAtEndOfThatDay`; `ArcVotingGraceEndTests` (fixed dates, both DST changes) |
| US4-S1, US4-S2 | `ArchitecturalSeederTests.Seeder_CreatesDemoData_AndIsIdempotent`; `e2e/board-architectural.spec.ts` › "the auto-approved demo application cites the governing documents" |
| US5-S1 | `BoardSizeAndLapseTests.SingleMemberBoard_OwnerIsNotRecused_AndCanVote` |
| US5-S2 | `BoardSizeAndLapseTests.TwoMemberBoard_OwnerIsStillRecused` |
| US5-S3 | `BoardSizeAndLapseTests.FinalDecision_KeepsBoardSizeAtDecision` |
| US6-S1 | `BoardSizeAndLapseTests.LapseApproval_DetailCitesGoverningDocuments`; `application-detail-panel.component.spec.ts` › "explains a lapse approval…" |
| US6-S2 | `BoardSizeAndLapseTests.VoteDecision_HasNoLapseStatement` |
| US6-S3 | `BoardSizeAndLapseTests.Settings_SaveLapseRuleCitation` |
| US7-S1 | `OwnerCommentTests.ResidentComment_WithFile_AppearsInOwnThread`; `request-detail.component.spec.ts` › "shows my comments with their files…", "posts a comment with a file" |
| US7-S2 | `OwnerCommentTests.BoardAndManager_SeeOwnerComments`; `application-detail-panel.component.spec.ts` › "shows the homeowner's comments in their own section" |
| US7-S3 | `OwnerCommentTests.EmptyComment_IsRefused`, `FilesOnlyComment_IsAccepted`; `comment-thread.component.spec.ts` › "disables Post comment until there is text or a file" |
| US7-S4 | `OwnerCommentTests.WithdrawnRequest_RefusesComments`; `request-detail.component.spec.ts` › "hides the comment form when comments are closed" |
| US7-S5 | `OwnerCommentTests.OtherPropertysRequest_IsForbidden` |
| FR-022 | `OwnerCommentTests.TooManyOrInvalidFiles_AreRefused`; `comment-thread.component.spec.ts` › "limits a comment to five files…" |
| US8-S1 | `request-detail.component.spec.ts` › "puts Withdraw request at the top right as a red button" |

## Success Criteria *(mandatory)*

- **SC-001**: No vote is cast by a single click on any surface (every cast needs a pick and a Submit/Save).
- **SC-002**: A board member can change their vote at any time until the decision is final, and never after.
- **SC-003**: No application on a board with at least one member shows a 0-member tally.
- **SC-004**: After any vote or undo, the "awaiting your vote" count matches the server.

## Assumptions

- **Grace period length**: one day (24 hours) after the threshold, per the product owner. Not configurable per community yet.
- **Auto-approval text**: "may result in auto-approval" is shown in every community, as requested, even where the lapse rule is "flag overdue only" or "deemed denied".
- **No notification for comments**: the board isn't emailed when the owner comments; the Notification Settings spec can add it.
- **Desktop-first**, like the rest of the board side.
