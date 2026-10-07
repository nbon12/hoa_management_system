# Feature Specification: Board Architectural Review (ARC)

**Feature Branch**: `027-board-arc-review`
**Created**: 2026-10-07
**Status**: Draft
**Input**: User description: "Board Architectural Review (ARC) — Architectural Applications for board members. Spec 3 of 6 in the board effort, building on 025-board-overall-design. Source design: `BoardArchApps` and `NeedsYourVote` in the board wireframe bundle, plus the design handoff README §5."

## Context

Homeowners in an HOA must get board approval before changing the outside of their home: fences, solar panels, repaints, sheds and so on. Each request is an **architectural application**. The board reviews the plans and votes, and the outcome is sent back to the owner. Today the product has nowhere to hold these applications. There is no entity, no board page and no vote record.

This is spec 3 of 6 in the board member effort described in `specs/025-board-overall-design/spec.md`:

| # | Spec | Status |
| --- | --- | --- |
| 1 | Overall design (`025`) | Merged |
| 2 | Community Overview & Metrics | Not started |
| 3 | **Architectural Applications** (this spec) | Draft |
| 4 | Board Approvals | Not started |
| 5 | Accounting | Not started |
| 6 | Reports | Not started |

Two sibling specs come out of this one: **Resident Architectural Application Submission** (how homeowners file applications) and **Notification Settings** (per-user email opt-outs). Neither blocks this spec.

## Grounding: what exists today

Verified against `main` at commit `fb6be6b`:

- **Community scope and roles exist** (spec 025). `Community`, `CommunityMembership` and the `BoardMember` / `CommunityManager` / `Accountant` roles are in place. A single server-side community-scope resolver answers whether a user may act in a community.
- **The board shell exists** and takes its navigation as data. "Architectural Applications" is already listed as a board nav entry (`board-navigation.service.ts`), but it has no page behind it.
- **Community Home is a placeholder** (`features/board/community-home/`). Spec 025 left its real content to later specs.
- **The pre-signed URL primitive exists.** `IDocumentStorage.GetPreSignedUrlAsync` returns short-lived URLs for private objects (spec 025 FR-039).
- **There is no architectural application entity.** Nothing in the backend or frontend models an application, an attachment on one, or a board vote.
- **Demo data:** the seeded user `board@nekohoa.dev` holds an active BoardMember membership in the seeded community.

## Clarifications

### Session 2026-10-07

- Q: How do applications get into the system? → A: Homeowners submit them in the app, but that's a **separate spec** (Resident Architectural Application Submission), with its own mockups. This spec does not build intake. It reads applications created by that spec, and is built and tested against seeded applications, so it doesn't depend on that spec landing first.
- Q: How is the owner told the outcome? → A: When the manager records the outcome, the product emails the owner automatically through the existing transactional email. There are two templates, approved and denied, designed separately.
- Q: What happens when the review period passes without a decision? → A: It follows the association's governing documents, so it is **configurable per community**: flag as overdue only, deemed approved, or deemed denied. The review period length is configurable per community too. Whatever the rule, the community's board is emailed when the deadline lapses. Board members will be able to opt out of that email in a separate Notification Settings spec.
- Q: Can an approval carry conditions? → A: Yes. When recording an approval, the manager may add optional conditions text, which is stored with the outcome and shown to the owner in the approved email. There is no separate "approved with conditions" outcome.
- Q: How is a decision reached: majority of all eligible members, or majority of votes cast with a quorum? → A: Configurable per community, to follow its governing documents. **Majority of members** (the default) or **majority of votes cast with a quorum**, where the quorum is more than half of the eligible members.
- Q: Does "Request info" pause the review period? → A: No. The governing documents reviewed deem *completed* plans approved if the ARB doesn't act within 45 days, and they have no tolling clause. An informal question is not an ARB action, so the clock keeps running. The board's protection is a formal decision within the period. A formal "incomplete submission" notice is left out because its legal footing is uncertain; it can be added in a later spec after counsel review. The board is also emailed before the due date so a decision can go out in time.
- Q: Should "re-submit" be its own outcome? → A: No. Legally it is a **denial** (a formal disapproval of the plans as submitted), and the record always says so. Votes stay Approve / Deny. When recording a denial, the manager picks the **owner-facing wording**: "Revisions requested" (default) or "Denied". Both wordings carry the same formal disapproval statement and a one-click **Revise and resubmit**. Revise and resubmit creates a new revision pre-filled from the previous one, with every field editable and uploaded attachments carried over (removable, and more can be added). The revision has its own received date and full review period, and is linked to earlier revisions. The board UI explains this so board members know Deny is how to ask for changes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Review the community's open applications (Priority: P1)

As a board member in board mode, I open "Architectural Applications" and see every open application for my community in one table. Each row shows its ID, property address and owner, the project, its attachments, its due date, the board's vote tally so far and my own vote. I can switch to closed applications and search by address or owner.

**Why this priority**: The board can't vote on anything it can't see. This list is the core of the feature and is useful even before voting works, because it replaces chasing applications by email.

**Independent Test**: Seed a community with open and closed applications. Sign in as a board member, enter board mode, open Architectural Applications. Confirm the Open tab lists only open applications with the right columns and counts. Confirm Closed lists only closed ones. Confirm search narrows the list.

**Acceptance Scenarios**:

1. **Given** I am an active board member of a community with 4 open and 27 closed applications, **When** I open Architectural Applications, **Then** the Open tab is selected, its label reads "Open · 4", the Closed tab reads "Closed · 27", and the table lists exactly the 4 open applications.
2. **Given** the Open tab is showing, **When** I select the Closed tab, **Then** the table lists exactly the 27 closed applications and none of the open ones.
3. **Given** an open application exists for "711 Keystone Park Dr #29" owned by "Praneeth Pattyam", **When** I type "Keystone Park" or "Pattyam" into the search box, **Then** that application is listed and applications whose address and owner do not match are not.
4. **Given** an open application, **When** the table renders its row, **Then** the row shows the application ID (for example `ARC-1042`), the property address with the owner's name under it, the project description, the attachment count ("📎 3 files", or "none" when there are no attachments), the due date, and the board vote tally.
5. **Given** 2 board members voted approve, 0 voted deny and 3 have not voted, **When** the row renders, **Then** the tally shows 2 approve, 0 deny and 3 not voted, reads "2/5", and its accessible label reads "2 approve · 0 deny · 3 not voted".
6. **Given** 1 open application has no vote from me, **When** the page loads, **Then** the header shows a pill reading "1 awaiting your vote".
7. **Given** I am an active board member of community A only, **When** I request the applications for community B, **Then** the request is refused and does not reveal whether community B exists or has applications.
8. **Given** I hold only a Resident membership, **When** I navigate directly to the Architectural Applications route, **Then** I am refused and redirected to a permitted page.

---

### User Story 2 - Cast a vote on an application (Priority: P1)

As a board member, I vote Approve or Deny on an open application, either straight from its row or from its detail panel. I can add an optional comment that the rest of the board and the manager can see. Once I have voted, my row shows "you voted approve" or "you voted deny" in place of the buttons, and the tally updates.

**Why this priority**: Voting is the decision the board is here to make. Together with Story 1 it is the minimum useful product.

**Independent Test**: As a board member with no vote on an open application, click Approve on its row. Confirm the row now reads "you voted approve", the tally's approve count went up by one, and the "awaiting your vote" count went down by one. Repeat from the detail panel with Deny and a comment, and confirm the comment is visible to another board member.

**Acceptance Scenarios**:

1. **Given** an open application I have not voted on, **When** the row renders, **Then** my vote column shows Approve, Deny and Info buttons.
2. **Given** an open application I have not voted on with a tally of 2 approve / 0 deny / 3 not voted, **When** I click Approve on its row, **Then** my vote is saved, my vote column reads "you voted approve", and the tally reads 3 approve / 0 deny / 2 not voted.
3. **Given** I have already voted deny on an application, **When** the row renders, **Then** my vote column shows "you voted deny" and no vote buttons.
4. **Given** I open an application's detail panel, **When** I enter the comment "Fence height exceeds the 5ft limit in §4.2" and click Deny, **Then** my deny vote is saved with that comment, and another board member of the same community who opens the application sees the comment with my name.
5. **Given** I click Deny without entering a comment, **When** the vote is saved, **Then** it succeeds; the comment is optional.
6. **Given** an application is closed, **When** I try to vote on it, **Then** the vote is refused and the existing tally is unchanged.
7. **Given** an application for a property I own, **When** the row renders, **Then** I am shown as recused instead of being offered vote buttons, and a vote attempt from me is refused.
8. **Given** I hold a Community Manager or Accountant membership but no Board Member membership, **When** I try to vote, **Then** the vote is refused.

---

### User Story 3 - Review an application's details and attachments (Priority: P1)

As a board member, I select an application and see its details panel: owner, date received, and the list of attachments by file name and size. Opening an attachment shows it in a new tab through a short-lived link. The panel also holds the "Cast your vote" card and the decision rule text: "Three of five votes decide. The manager records the outcome and notifies the owner."

**Why this priority**: A board member can't vote responsibly without seeing the plans. Attachments hold homeowner personal information, so they must be served safely.

**Independent Test**: Select an application with three attachments. Confirm the panel shows the owner, received date and all three file names with sizes. Open one and confirm it opens in a new tab and that the link stops working after it expires.

**Acceptance Scenarios**:

1. **Given** application `ARC-1042` was received on 05/28/26 from "Praneeth Pattyam" with attachments `fence-plan.pdf` (1.2 MB), `elevation.jpg` (840 KB) and `plat-survey.pdf` (2.1 MB), **When** I select it, **Then** the detail panel is titled "ARC-1042 · fence replacement" and shows the owner, the received date, and all three files with their sizes.
2. **Given** the detail panel is open, **When** I open `fence-plan.pdf`, **Then** it opens in a new browser tab through a link that expires within 15 minutes of being issued.
3. **Given** an attachment link was issued more than 15 minutes ago, **When** anyone requests it, **Then** the request fails.
4. **Given** I am not a board member of the application's community, **When** I request an attachment link for it, **Then** the request is refused and no link is issued.
5. **Given** the application has no attachments, **When** I select it, **Then** the panel says there are no attachments instead of showing an empty list.
6. **Given** any page in this feature, **When** it renders an attachment, **Then** the page does not contain a durable public object URL for that file.

---

### User Story 4 - Request more information (Priority: P2)

As a board member, when an application is missing something (a survey, a color sample, dimensions), I click Info or "Request info" and write what is needed. The request goes to the manager, who passes it to the owner. It is not a vote: my vote stays open, and the application shows that information was requested.

**Why this priority**: Incomplete applications are common. Without this, board members end up denying applications just to get more detail.

**Independent Test**: As a board member, request info on an open application with the message "Please attach a plat survey". Confirm the application shows an information-requested marker with that message, the tally is unchanged, and my vote buttons are still available.

**Acceptance Scenarios**:

1. **Given** an open application I have not voted on, **When** I click Info on its row, **Then** the detail panel opens with the comment box focused and the "Request info" action available.
2. **Given** the detail panel is open, **When** I enter "Please attach a plat survey" and click Request info, **Then** the application shows an "info requested" marker with my message and name, the vote tally is unchanged, and my Approve and Deny buttons are still available.
3. **Given** I click Request info with an empty message, **When** I submit, **Then** the request is refused with a message saying what information is needed.
4. **Given** a board member requested info, **When** the community manager views the application, **Then** they see the request, its message, who sent it and when.
5. **Given** the detail panel is open, **When** I choose Request info, **Then** I see the notice "Questions don't pause the review period (due <date>). To require changes before approval, vote Deny — the owner can be told 'Revisions requested'." and the info-requested marker shows the unchanged due date.

---

### User Story 5 - See what needs my vote from Community Home (Priority: P2)

As a board member landing on Community Home, I see a "Needs your vote" card listing the open applications I haven't voted on yet, each with Approve and Deny buttons and a link to all architectural applications.

**Why this priority**: It saves a click for the most common board task, but the full list in Story 1 already covers the need.

**Independent Test**: As a board member with one unvoted open application, land on Community Home. Confirm the card lists exactly that application with "1 open". Approve it from the card and confirm it leaves the card.

**Acceptance Scenarios**:

1. **Given** 1 open application lacks my vote and 3 others already have it, **When** I land on Community Home, **Then** the "Needs your vote" card shows "1 open" and lists only that application, with its ID, project, address, owner, attachment count, tally, due date, and Approve and Deny buttons.
2. **Given** the card is showing, **When** I click "All architectural applications →", **Then** I am taken to the Architectural Applications page.
3. **Given** the card lists an application, **When** I click Approve on it, **Then** my vote is saved and the application leaves the card.
4. **Given** I have voted on every open application, **When** I land on Community Home, **Then** the card shows an empty state saying nothing needs my vote.

---

### User Story 6 - Reach a decision (Priority: P2)

When enough board members vote the same way, the application has a board decision. The manager records the outcome, the owner is emailed, and the application moves to Closed. If the review period runs out first, the community's own rule (from its governing documents) decides what happens, and the board is emailed.

**Why this priority**: Without a decision rule, votes pile up with no result. It is P2 because the board can still see and vote without it.

**Independent Test**: In a community with 5 active board members, cast 3 approve votes on one application. Confirm it shows "decision reached: approve" and refuses further votes. Have the manager record the outcome and confirm the application moves to Closed and the owner is sent the approved email. Separately, set each lapse rule on a test community, let an application pass its due date, and confirm the rule's outcome and the board email.

**Acceptance Scenarios**:

1. **Given** a community using the default majority-of-members rule with 5 active, non-recused board members and an application with 2 approve votes, **When** a third board member votes approve, **Then** the application shows "decision reached: approve" and refuses further votes.
2. **Given** the same community, **When** 3 board members vote deny, **Then** the application shows "decision reached: deny".
3. **Given** 2 approve and 2 deny votes out of 5, **When** the row renders, **Then** no decision is shown and voting stays open.
4. **Given** a community using majority of votes cast with 5 eligible members, **When** 3 members have voted 3 approve / 0 deny, **Then** the decision "approve" is reached immediately, because the 2 remaining votes can't overtake it.
5. **Given** a community using majority of votes cast with 5 eligible members and votes of 2 approve / 1 deny, **When** the due date passes with no more votes, **Then** the decision "approve" is reached and the lapse rule does not apply.
6. **Given** a community using majority of votes cast with 5 eligible members and only 2 votes cast, **When** the due date passes, **Then** quorum isn't met, no decision is reached from votes, and the community's lapse rule applies.
7. **Given** an application with a reached decision of approve, **When** the community manager records the outcome, **Then** the application moves to Closed with outcome "approved" and the closing date, and the owner is sent the "application approved" email naming the application ID, property and project.
8. **Given** an application with a reached decision of approve, **When** the manager records the outcome with the conditions "Fence must be stained to match the existing color", **Then** the application closes with outcome "approved" and those conditions, and the owner's approved email shows them in its conditions section. When no conditions are recorded, the email has no conditions section.
9. **Given** an application with a reached decision of deny and board comments, **When** the community manager records the outcome with the reason "Lower the fence to 5ft per Guideline 4.2" and leaves the owner-facing wording at its default, **Then** the application closes with outcome "denied" and wording "revisions requested", the board's Closed tab shows it as "Denied · revisions requested", and the owner is sent the "revisions requested" email containing that reason, the community's formal disapproval statement and a "Revise and resubmit" link. Board-only vote comments are not included.
10. **Given** the same decision, **When** the manager chooses the wording "Denied", **Then** the application closes with outcome "denied" and wording "denied", and the owner is sent the "denied" email with the reason, the same formal disapproval statement and the same "Revise and resubmit" link.
11. **Given** application ARC-1042 was denied and its owner resubmitted a revision, **When** a board member opens the revision, **Then** it is shown as "ARC-1042 · rev 2" with a new received date and due date, no votes, and a link to revision 1 showing its decision, reason and board comments.
12. **Given** a community with a reminder of 7 days and an open application due 06/27/26 with no decision, **When** 06/20/26 arrives, **Then** every active board member is emailed one reminder with the application ID, project, due date, current tally and a link. An application that already has a decision gets no reminder.
13. **Given** a community whose lapse rule is "flag overdue only", **When** an open application passes its due date with no decision, **Then** it is shown as overdue, voting stays open, nothing is decided automatically, and every active board member of the community is emailed that the application is overdue.
14. **Given** a community whose lapse rule is "deemed approved", **When** an open application passes its due date with no decision, **Then** the application shows "decision reached: approved by default (review period lapsed)", refuses further votes, and every active board member of the community is emailed that the application was approved by default.
15. **Given** a community whose lapse rule is "deemed denied", **When** an open application passes its due date with no decision, **Then** the application shows "decision reached: denied by default (review period lapsed)", refuses further votes, and every active board member is emailed that the application was denied by default.
16. **Given** a community with a review period of 45 days, **When** an application received on 05/01/26 is shown, **Then** its due date is 06/15/26.
17. **Given** I am a community manager, **When** I set my community's review period, lapse rule, decision rule, reminder days and formal disapproval statement, **Then** the new values are saved and apply to applications received afterwards. A board member or resident who tries to change them is refused.

---

### Edge Cases

- **Board size changes mid-vote**: The majority and quorum are computed against the active, non-recused board members at the time each vote is counted. A member whose membership ends keeps their already-cast vote; a newly added member can vote on any still-open application.
- **Even-sized board**: Under majority of members, the majority is more than half of eligible members (3 of 4, 4 of 6). A tie never reaches a decision under either rule.
- **Board member owns the property**: They are recused. They count neither toward the denominator nor the tally.
- **Board member votes twice**: The second attempt is refused; a vote can't be changed after it is cast. *(Assumption — see Assumptions.)*
- **Two board members vote at the same moment as the deciding vote**: Exactly one decision is reached, and any vote after the decision is refused.
- **Application with no attachments**: The row shows "none" and the panel shows an empty-state message.
- **Info requested close to the deadline**: The due date doesn't move. The reminder (FR-028a) and lapse emails still go out on schedule.
- **Revision submitted while an earlier revision is still open**: Not possible. Only a closed, denied revision can be revised.
- **Attachment missing from storage**: Opening it shows an "attachment unavailable" message; the rest of the application still renders.
- **Search with no matches**: The table shows an empty state naming the search term.
- **Board member of several communities**: The page only shows applications for the community that is active in board mode.

## Requirements *(mandatory)*

> **Design references.** UI requirements cite the board wireframe bundle at `wireframes/HOA Management CRM v1.0.1/` (`WFb`): `BoardArchApps` (section "4 · Architectural applications") and `NeedsYourVote` (rendered in `BoardHome`) in `wf-board-screens.jsx`, and §5 of `design_handoff_board_experience/README.md`. These are **lofi wireframes**: layout, table columns and copy are intentional; colors and spacing are placeholders and must be mapped to `neko-hoa/src/styles.scss` tokens (spec 025 FR-037). Requirements with no wireframe are marked `[no WFb]`.

### Application list

- **FR-001**: System MUST store architectural applications, each belonging to exactly one property and therefore one community, with: a human-readable ID unique within the community (format `ARC-<number>`), project description, owner, date received, due date, status, and attachments. `[no WFb]`
- **FR-002**: The Architectural Applications page MUST list the active community's applications in a table with these columns, in this order: ID, Property (address with owner name beneath), Project, Attachments, Due, Board votes, Your vote. *[design: `WFb BoardArchApps` table header.]*
- **FR-003**: The page MUST offer Open and Closed tabs, each labeled with its count (for example "Open · 4", "Closed · 27"), with Open selected by default. *[design: `WFb BoardArchApps` tab buttons.]*
- **FR-004**: The page MUST offer a search box, labeled "Search address or owner", that filters the list by case-insensitive partial match on property address or owner name. *[design: `WFb BoardArchApps` search field.]*
- **FR-005**: The page header MUST read "Architectural applications" and show a pill counting the open applications the current user may vote on but has not, reading "N awaiting your vote". The pill is hidden when N is 0. *[design: `WFb BoardArchApps` header pill "1 awaiting your vote".]*
- **FR-006**: The Attachments column MUST show "📎 N file(s)" when attachments exist and "none" otherwise. *[design: `WFb BoardArchApps` attachments cell.]*
- **FR-007**: The Board votes column MUST show a tally of approve, deny and not-voted counts plus "approved/eligible" text (for example "2/5"), with an accessible label of the form "2 approve · 0 deny · 3 not voted". *[design: `WFb Tally`.]*
- **FR-008**: The Your vote column MUST show Approve, Deny and Info actions when the current user may vote and hasn't; "you voted approve" or "you voted deny" once they have; and a recused indicator when they are recused. *[design: `WFb BoardArchApps` "Your vote" cell; recused state is `[no WFb]`.]*
- **FR-009**: Collections MUST be paginated with a default page size of 25 and a maximum of 100. `[no WFb]`

### Detail panel and attachments

- **FR-010**: Selecting an application MUST open a detail panel titled "<ID> · <project>" showing owner, received date and the attachment list. *[design: `WFb BoardArchApps` lower-left card "ARC-1042 · fence replacement".]*
- **FR-011**: Each attachment MUST be listed with file name and size, and MUST open in a new browser tab. *[design: `WFb` "stored in S3, opens in a new tab".]*
- **FR-012**: Attachments MUST be served only through short-lived pre-signed links (spec 025 FR-039) that expire within 15 minutes, issued per request after an authorization check. Durable public object URLs MUST NOT be exposed. *[design: handoff README §5.]*
- **FR-013**: The detail panel MUST show the decision rule text for the community's rule and real board size. For majority of members: "Three of five votes decide. The manager records the outcome and notifies the owner." (for example "Three of four votes decide." with four members). For majority of votes cast: "A majority of votes cast decides once three of five members have voted. The manager records the outcome and notifies the owner." *[design: `WFb BoardArchApps` rule text; dynamic wording is `[no WFb]`.]*

### Voting

- **FR-014**: Only users with an active Board Member membership in the application's community MAY vote. Community Managers and Accountants MUST NOT vote unless they also hold an active Board Member membership there.
- **FR-015**: A vote MUST be either Approve or Deny and MAY carry an optional comment of up to 2,000 characters. *[design: `WFb` "Cast your vote" card — "Comment to the board · Optional".]*
- **FR-016**: Each eligible board member MAY cast at most one vote per application. A vote can't be changed after it is cast.
- **FR-017**: A board member who owns the application's property MUST be recused. They can't vote and don't count toward the eligible total.
- **FR-018**: Votes MUST be refused on applications that are closed or that have already reached a decision.
- **FR-019**: Vote comments MUST be visible to the community's board members and community managers, each with the voter's name and UTC timestamp. They MUST NOT be visible to residents.
- **FR-020**: Voting MUST be available from the row (Approve / Deny), from the detail panel (✓ Approve / ✕ Deny with optional comment), and from the Community Home "Needs your vote" card. *[design: `WFb BoardArchApps` row and "Cast your vote" card; `WFb NeedsYourVote`.]*

### Information requests

- **FR-021**: A board member MAY send a "Request info" message (required, up to 2,000 characters) on an open application. It MUST NOT count as a vote, MUST NOT change the tally, and MUST NOT pause or move the due date. When composing it, the board member MUST be shown that questions don't pause the review period, with the due date and a pointer that Deny (with "Revisions requested" wording) is how to require changes. *[design: `WFb` "Info" row action and "Request info" button.]*
- **FR-022**: An application with an outstanding information request MUST show an "info requested" marker with the message, sender and time to board members and community managers. `[no WFb]`

### Decision and closing

- **FR-023**: An application MUST reach a decision according to its community's decision rule (FR-029). Concurrent votes MUST NOT produce two decisions. *[design: `WFb` "Three of five votes decide."]*
  - **Majority of members** (default): a decision is reached as soon as one side (approve or deny) holds more than half of the eligible board members.
  - **Majority of votes cast with a quorum**: a decision is reached early once quorum is met (more than half of eligible members have voted) and the leading side can no longer be overtaken by the remaining eligible votes. Otherwise, at the due date, if quorum is met and one side leads, that side is the decision. If quorum isn't met or the votes are tied at the due date, the lapse rule (FR-027) applies.
- **FR-024**: Once a decision is reached, the application MUST show "decision reached: approve" or "decision reached: deny" and refuse further votes.
- **FR-025**: A Community Manager MUST be able to record the outcome of an application with a reached decision, which closes it with the outcome and closing date. A denial MUST carry a reason for the owner (required, up to 2,000 characters; the form prompts "What would need to change for approval?") and an owner-facing wording: **Revisions requested** (default) or **Denied**. The wording changes only what the owner reads. The recorded outcome is "denied" either way and the board UI MUST show it as "Denied · revisions requested" or "Denied". An approval MAY carry conditions of approval (optional, up to 2,000 characters). Conditions are not allowed on a denial. *[design: `WFb` "The manager records the outcome…"; manager screen is `[no WFb]`.]*
- **FR-026**: Recording the outcome MUST send the owner an email through the existing transactional email: the "application approved" template for approvals (including the conditions section only when conditions were recorded), and for denials either the "revisions requested" or the "denied" template according to the chosen wording. Both denial templates MUST include the manager's reason, the community's formal disapproval statement (FR-029) and a "Revise and resubmit" link. Board vote comments MUST NOT appear in owner emails. If the email can't be sent, the outcome is still recorded and the failure is logged and shown to the manager so they can resend. *(Templates are being designed separately; see Assumptions.)* `[no WFb]`
- **FR-027**: An open application past its due date without a decision MUST be shown as overdue, and the community's lapse rule MUST then apply: **flag overdue only** (nothing decided automatically), **deemed approved**, or **deemed denied** (the application moves to Decision Reached with an outcome marked "by default (review period lapsed)"). A deemed outcome goes through FR-025/FR-026 like any other decision. The lapse rule MUST apply exactly once per application. `[no WFb]`
- **FR-028a**: A configurable number of days before an open application's due date (per community, default 7, range 0–30, 0 = off), if no decision has been reached, every active board member of the community MUST be emailed one reminder with the application ID, project, due date, current tally and a link. The opt-out from the Notification Settings spec MUST be honored once it exists. `[no WFb]`
- **FR-028**: When an application's review period lapses, every active board member of the community MUST be emailed what happened (overdue, approved by default, or denied by default), with the application ID, property, project and a link to it. In this spec the email goes to all active board members. Opting out is added by the separate Notification Settings spec, which MUST be honored once it exists. `[no WFb]`

### Community ARC settings

- **FR-029**: Each community MUST have an architectural review period in days (default 30, range 1–365), a lapse rule (default "flag overdue only"), a decision rule (default "majority of members"; alternative "majority of votes cast with a quorum"), the pre-deadline reminder days (FR-028a), and a formal disapproval statement used in denial emails (default: "This is a formal disapproval of the plans as submitted under the community's governing documents. You may revise and resubmit."; a manager may cite their own section, e.g. "§12.3 of the Declaration"). An application's due date is its received date plus the community's review period at the time it was received. `[no WFb]`
- **FR-030**: Only a Community Manager of that community MAY change these settings, so they can match the association's governing documents. Each change is logged as a sensitive event with the old and new values. Changes don't alter the due date, lapse rule or decision rule of applications already received. `[no WFb]`

### Revisions

- **FR-034**: After a denial (either wording), the owner MUST be able to revise and resubmit. The result is a new **revision** of the same application (shown as "ARC-1042 · rev 2"), pre-filled from the previous revision. All fields are editable, and previously uploaded attachments carry over without re-uploading, can be removed, and more can be added. Removing an attachment from a revision MUST NOT remove it from earlier revisions, which stay unchanged. The owner-side form belongs to the Resident Architectural Application Submission spec. This spec owns the revision link and what the board sees. `[no WFb]`
- **FR-035**: Each revision is a new submission: its own received date, due date, votes and decision. Nothing carries over from the previous revision's votes. Board members and managers MUST see the revision number and a link to every earlier revision with its decision, reason and board comments. `[no WFb]`

### Intake

- **FR-031**: This spec does not create applications. Homeowner submission is the separate Resident Architectural Application Submission spec. This spec MUST work with applications that spec creates, and is built and tested against seeded applications. `[no WFb]`

### Community Home card

- **FR-032**: Community Home MUST show a "Needs your vote" card listing open applications the current board member may vote on but hasn't, with a count pill ("N open"), and per row: ID, project, address · owner, attachment count, tally, due date, and Approve and Deny buttons. *[design: `WFb NeedsYourVote`.]*
- **FR-033**: The card MUST include an "All architectural applications →" link to the Architectural Applications page and MUST show an empty state when nothing needs the user's vote. *[design: `WFb NeedsYourVote` header link; empty state is `[no WFb]`.]*

### Key Entities

- **Architectural Application**: A homeowner's request to change the exterior of a property. Belongs to one property (and through it, one community). Has a community-unique `ARC-` ID, project description, owner, received date, due date, status (Open, Decision Reached, Closed), a revision number (1 for the first submission) linked to its earlier revisions, and, when closed, the recorded outcome (approved or denied), closing date, and either the optional conditions of approval or the denial reason with its owner-facing wording (revisions requested or denied).
- **Application Attachment**: A file supporting an application (plans, photos, surveys). Holds file name, size, content type and a private storage key. The file itself lives in private object storage; only metadata is stored with the application.
- **Board Vote**: One board member's vote on one application: Approve or Deny, optional comment, voter, and UTC timestamp. At most one per member per application.
- **Community ARC Settings**: Per-community review period (days), lapse rule (flag overdue only, deemed approved, deemed denied), decision rule (majority of members, majority of votes cast with a quorum), pre-deadline reminder days, and the formal disapproval statement. Set by the community manager to match the association's governing documents.
- **Information Request**: A board member's request for more information on an application: message, sender, UTC timestamp. Does not count as a vote.

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: Applications, attachments, votes and info requests are community-scoped through the application's property. Every read and write goes through the spec 025 community-scope resolver. Cross-community access is denied by default and fails closed without revealing whether the community or application exists (025 FR-016). No endpoint in this spec spans communities.
- **Authorization**: Viewing the list, details and attachments requires an active Board Member or Community Manager membership in the community. Voting and requesting info require Board Member. Recording the outcome and changing the community's ARC settings require Community Manager. Every check is server-side from persisted membership; the client's board mode is never an input (025 FR-014).
- **Ownership and moderation**: Votes, comments and info requests belong to the board member who wrote them and can't be edited or deleted once submitted, so the record is an honest history. The application belongs to the owner.
- **API contract**: Uses the existing response and error shapes. Collections take `limit`/`offset` (default 25, max 100). All timestamps are UTC. Entity IDs are GUIDs; the `ARC-` number is a display handle only. No breaking changes to existing endpoints.
- **API implementation and docs**: New endpoints are FastEndpoints. Swagger stays available only in Development/Dev and is disabled in Production.
- **Database/runtime**: New tables are added by forward-only migrations that apply idempotently at startup in Cloud Run. Queries use short-lived DbContexts and respect Neon's low connection limit. A unique constraint enforces one vote per member per application.
- **File storage**: Attachment files live in private object storage (Cloudflare R2 in production, MinIO in local Docker Compose and tests). PostgreSQL holds only attachment metadata and the storage key. Files are reached only through the pre-signed URL primitive.
- **Security and abuse controls**: Comments and info requests are untrusted input: length-limited and rendered as text, never HTML. Vote and info-request endpoints use the existing rate limiting. Each board access to an application's attachments or owner details emits the 025 FR-017 structured sensitive-event (actor, community, resource, UTC timestamp). Each vote and outcome recording is logged as a sensitive event too.
- **Observability**: Errors go to Sentry with environment and release tags. Trace context flows from frontend to backend. Comment text, owner names and storage keys are excluded from telemetry.
- **Accessibility**: All row actions, tabs, the search box and the detail panel are keyboard-reachable and labeled. The tally exposes its counts as text, not color alone. Approve and deny states don't rely on color. Everything meets WCAG 2.1 AA.
- **Quality gates**: 95% coverage on new backend and frontend files. Sonar analysis passes. xUnit integration tests run on Testcontainers PostgreSQL and MinIO, use isolated per-test communities so they're safe in parallel and after prior runs, and use `[Theory]` data for both decision rules (board sizes 3, 4, 5 and 6, with and without a recused member, quorum met and not met at the due date). Serilog sensitive-events are asserted where the spec requires them. Repowise docs are refreshed for the PR. The PR is a focused vertical slice.
- **Frontend testing**: Jasmine/Karma for tally and majority-text logic. Angular Testing Library for the applications table, tabs, search, detail panel and Needs-your-vote card. Playwright for the vote journey and refusal of a Resident on the route. Cypress E2E for sign-in → board mode → Architectural Applications → vote. Storybook visual regression for the table, tally, detail panel and card.
- **Executable & living spec**: Every acceptance scenario and Independent Test above maps to an automated test that runs on demand and passes before merge. This `spec.md` and `tasks.md` are updated before the PR. This spec fills in 025's placeholder Community Home and its "Architectural Applications" nav entry without contradicting 025.
- **Spec independence & parallelism**: Hard dependency on spec 025 (merged) for community scope, roles, the board shell and the pre-signed URL primitive. No dependency on specs 2, 4, 5 or 6. Two sibling specs come out of this one's clarifications, and neither blocks it: **Resident Architectural Application Submission** creates applications (this spec uses seeded ones until it lands), and **Notification Settings** adds the board opt-out for the lapse email (this spec emails all active board members until it lands). They can be built in parallel. The "Needs your vote" card is added to Community Home as its own section, so spec 2 (Community Overview & Metrics) can fill the rest of that page in parallel.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A board member can go from Community Home to a cast vote on an application in 2 clicks or fewer (Approve on the "Needs your vote" card is 1).
- **SC-002**: A board member can open any attachment of an application within 3 seconds of selecting it.
- **SC-003**: 100% of attachment links issued by the product stop working within 15 minutes, and no page or response contains a durable public object URL.
- **SC-004**: 0 votes are accepted from non-board members, recused owners, or on closed or decided applications, across the automated suite.
- **SC-005**: Every application that meets its community's decision rule shows exactly one decision, including when the deciding votes arrive simultaneously.
- **SC-006**: The applications page shows the first page of results within 2 seconds for a community with 500 applications.
- **SC-007**: Every recorded outcome produces exactly one owner email (or a visible, resendable failure), and every lapsed application produces exactly one board email per active board member.
- **SC-008**: 100% of lapsed applications get the outcome their community's configured rule calls for.

## Assumptions

- **Majority rule**: The default rule, "Three of five votes decide", generalizes to a simple majority of active, non-recused board members. The wireframe's five-member board is just the example.
- **Votes are final**: A board member can't change or withdraw a vote. This keeps the record clean and matches how most boards minute decisions. Easy to relax later if wanted.
- **Due date**: The review period defaults to 30 days, matching the wireframe (received 05/28/26, due 06/27/26), and the lapse rule defaults to "flag overdue only" so nothing is decided automatically until a manager sets the community's rule.
- **Owner email templates**: Three owner emails (approved, revisions requested, denied) are being designed separately in Claude Design. Until they arrive, plain-text versions with the same fields are used.
- **No incomplete notice**: A formal "submission incomplete" notice is out of scope. Submissions are kept complete by required fields in the submission spec, and denial with "Revisions requested" covers the rest. It can be added after counsel review.
- **Not legal advice**: The formal disapproval statement and per-community rules let each association follow its own documents. The product doesn't decide what those documents require.
- **Lapse check timing**: Lapses are detected within one hour of the due date passing (end of day in the community's time zone).
- **Manager visibility**: Community Managers can see the list, detail and comments, and record outcomes, but don't vote.
- **Residents** see nothing from this feature in this spec except the outcome email. Their submission and status pages come from the separate submission spec.
- **Desktop-first**, like the rest of the board side (025). Mobile layout is out of scope.
- **Demo data**: The dev seed gets a few sample applications (mirroring the wireframe's ARC-1036–1042) for the seeded community so `board@nekohoa.dev` can try the flow.
