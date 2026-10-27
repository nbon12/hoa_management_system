---
description: "Task list for 031-arc-review-refinements"
---

# Tasks: ARC Review Refinements

**Input**: `specs/031-arc-review-refinements/spec.md`. Every scenario's test is listed in the spec's "Scenario → test map".

## Backend (`HOAManagementCompany/`)

- [x] T001 Migration `AddArcReviewRefinements`: `ArchitecturalApplications.DecisionFinalAt`, `.EligibleVoterCount`; `ArchitecturalOwnerComments`; `ArchitecturalAttachments.OwnerCommentId`; `CommunityArcSettings.LapseRuleCitation` (FR-003, FR-015, FR-016, FR-020)
- [x] T002 `ArcVoting`: grace-period end, votes-open check, re-run the decision rule after a vote change (FR-003, FR-004)
- [x] T003 `CastVoteEndpoint` accepts votes during the grace period; `UndoVoteEndpoint` (FR-004, FR-005)
- [x] T004 `RecordOutcomeEndpoint` refuses `DECISION_NOT_FINAL`; snapshots the board size if missing (FR-006, FR-015)
- [x] T005 `ArcQueries`: recusal waiver, snapshot tallies, `decisionFinal` / `votesLockAt` / `canUndoVote`, owner comments, `lapseStatement`; awaiting count waiver in `ApplicationsListEndpoint` (FR-007, FR-014–FR-016)
- [x] T006 ARC settings `lapseRuleCitation` on PUT and GET (FR-016)
- [x] T007 Resident `POST …/comments`, comments and `canComment` in the resident detail, board size at withdrawal (FR-015, FR-020–FR-022)
- [x] T008 `ArchitecturalSeeder`: remove demo staff links to residents' properties; ARC-1040 approved by default (FR-017)

## Backend tests (`HOAManagementCompany.Tests/`)

- [x] T010 `VoteChangeTests` (US3, FR-003)
- [x] T011 `BoardSizeAndLapseTests` (US5, US6)
- [x] T012 `OwnerCommentTests` (US7)
- [x] T013 Update `DecisionOnVoteTests`, `ArcConcurrencyTests`, `RecordOutcomeEndpointTests`, `ArchitecturalSeederTests` for the grace period and seed

## Frontend (`neko-hoa/src/app/`)

- [x] T020 `VoteChoiceComponent` (pick → Submit/Save) and `FinalMarkComponent`; shared `.vote-choice`, `.skeleton`, `.final-mark`, `.btn--danger`, `.field:hover` styles (FR-002, FR-007, FR-008, FR-019, FR-023)
- [x] T021 Applications page: clickable rows, two-step voting, Ask question, undo, Final decision column, count refresh (FR-001, FR-002, FR-007, FR-011, FR-018)
- [x] T022 Detail panel: skeletons, "— voted …", Board final result, pending note, undo, lapse statement, Homeowner comments (FR-008–FR-010, FR-012, FR-016, FR-021)
- [x] T023 Cast-vote card and Needs-your-vote card: two-step voting, wording, clickable rows (FR-001, FR-002, FR-011–FR-013)
- [ ] T024 Community settings "Architectural review" form: lapse-rule citation field (FR-016) — follow-up; the form is shared with company defaults (030)
- [x] T025 Resident request page: `CommentThreadComponent`; red Withdraw at top right (FR-020–FR-023)
- [x] T026 Karma specs, Storybook stories, Playwright and Cypress updates

## Docs

- [x] T030 Amend `027` and `029` spec.md where 031 changes them
- [x] T031 `design-brief.md` for Claude Design
