---
description: "Task list for 027-board-arc-review"
---

# Tasks: Board Architectural Review (ARC)

**Input**: Design documents from `specs/027-board-arc-review/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/architectural-applications.md, quickstart.md

**Tests**: Required. Under the spec's "Executable & living spec" clause and CLAUDE.md's NLT rule, every acceptance scenario and Independent Test needs a faithful automated test. Each test task cites the scenarios it covers as `USn-Sm` (story n, scenario m in `spec.md`). Write the tests first and watch them fail before implementing.

**Organization**: Grouped by user story (US1–US6 from spec.md).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: the user story the task belongs to

## Path Conventions

| Short name | Path |
|---|---|
| Backend | `HOAManagementCompany/` |
| Backend tests | `HOAManagementCompany.Tests/` |
| ARC backend folder | `HOAManagementCompany/Features/Board/Architectural/` |
| ARC test folder | `HOAManagementCompany.Tests/Integration/Board/Architectural/` |
| Frontend | `neko-hoa/src/app/` |
| ARC frontend folder | `neko-hoa/src/app/features/board/architectural/` |
| Playwright | `neko-hoa/e2e/` |
| Cypress | `neko-hoa/cypress/e2e/` |

---

## Phase 1: Setup

- [X] T001 Create backend folder `HOAManagementCompany/Features/Board/Architectural/` and test folder `HOAManagementCompany.Tests/Integration/Board/Architectural/`
- [X] T002 [P] Create frontend folder `neko-hoa/src/app/features/board/architectural/`

---

## Phase 2: Foundational (blocking: must finish before any user story)

**Purpose**: data model, authorization, shared query and decision logic, seed data and test harness used by every story.

### Enums and entities (data-model.md)

- [X] T003 [P] Create the 8 enums from data-model.md (`ArcApplicationStatus`, `ArcVoteChoice`, `ArcOutcome`, `ArcDenialWording`, `ArcDecisionSource`, `ArcDecisionRule`, `ArcLapseRule`, `ArcProjectType`), each in its own file `HOAManagementCompany/Domain/Enums/<Name>.cs`
- [X] T004 [P] Create `CommunityArcSettings` entity (fields, defaults and ranges per data-model.md, with a Repowise `domain=entities` marker) in `HOAManagementCompany/Domain/Entities/CommunityArcSettings.cs`
- [X] T005 [P] Create `ArchitecturalApplication` entity: all data-model.md fields, including the snapshot fields `DecisionRule`/`LapseRule`/`TimeZoneId`, `PreviousRevisionId` self-reference and navigations to Property, Community, Attachments, Votes, InfoRequests; with a Repowise marker. File: `HOAManagementCompany/Domain/Entities/ArchitecturalApplication.cs`
- [X] T006 [P] Create `ArchitecturalAttachment`, `ArchitecturalVote` and `ArchitecturalInfoRequest` entities in `HOAManagementCompany/Domain/Entities/ArchitecturalAttachment.cs`, `ArchitecturalVote.cs` and `ArchitecturalInfoRequest.cs`
- [X] T007 Modify `OutboxMessage`: `OwnerId` becomes `Guid?`, add `string? RecipientUserId`, update the doc comment listing the `arc_*` kinds and the Repowise marker. File: `HOAManagementCompany/Domain/Entities/OutboxMessage.cs`. Then fix every compile error from the nullable `OwnerId` in `HOAManagementCompany/Features/Payments/Alerts/AlertService.cs`, `OutboxDispatcher.cs` and `HOAManagementCompany/Features/Payments/Recurring/VariableNoticeService.cs`, without changing behavior.

### Persistence

- [X] T008 Register the DbSets and configure the new entities in `HOAManagementCompany/Infrastructure/Persistence/ApplicationDbContext.cs`:
  - string-converted enums and max lengths per data-model.md;
  - unique `(CommunityId, ApplicationNumber, Revision)`;
  - indexes `(CommunityId, Status, DueDate)`, `(PropertyId)`, `(PreviousRevisionId)`;
  - the three `ArchitecturalApplication` check constraints;
  - unique `ArchitecturalVote (ApplicationId, VoterUserId)`;
  - cascade and restrict rules per data-model.md;
  - `OutboxMessage.RecipientUserId` FK (cascade) and the check `("OwnerId" IS NULL) <> ("RecipientUserId" IS NULL)`.
- [X] T009 Generate the forward-only migration `AddArchitecturalReview` with `dotnet ef migrations add AddArchitecturalReview` into `HOAManagementCompany/Infrastructure/Persistence/Migrations/`. Review it: idempotent at startup, no destructive change, and `OwnerId` altered to nullable without touching existing rows.
- [X] T010 Add a migration test that applies all migrations to a fresh Testcontainers database. It asserts the 5 new tables, the unique indexes and check constraints (inserting a second vote by the same voter fails; an outbox row with both or neither of `OwnerId`/`RecipientUserId` fails) and that existing payment outbox rows survive. File: `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcMigrationTests.cs`

### Authorization (research R1, R2)

- [X] T011 Add `ViewArchitecturalApplications`, `VoteArchitecturalApplications` and `ManageArchitecturalReview` to `CommunityCapability` in `HOAManagementCompany/Features/Board/ICommunityScopeResolver.cs`, and map them in `HOAManagementCompany/Features/Board/CommunityScopeResolver.cs`:
  - View → BoardMember, CommunityManager
  - Vote → BoardMember
  - Manage → CommunityManager
- [X] T012 [P] Add a `[Theory]` resolver test: every role × the 3 new capabilities × active/inactive/ended membership, asserting Accountant and Resident are denied all three. File: `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcCapabilityTests.cs`
- [X] T013 Change `BoardScopeEnforcementStaticAnalysisTests` to scan `SearchOption.AllDirectories`. Add `ArcSweepJobEndpoint.cs` to `AllowList` with the reason "secret-authenticated scheduler job, not a user-facing community resource". The allow-list name check passes once T066 creates the file. File: `HOAManagementCompany.Tests/Integration/Board/BoardScopeEnforcementStaticAnalysisTests.cs`

### Shared logic

- [X] T014 [P] Write `ArcDecisionRulesTheoryTests` first. `[Theory]`/`MemberData` over:
  - both decision rules × eligible counts 3, 4, 5, 6 × vote mixes × due date passed or not × quorum met or not;
  - the wording split (RevisionsNeeded ≥ Deny gives RevisionsRequested, a tie gives RevisionsRequested, Deny > RevisionsNeeded gives Denied);
  - the early-win rule ("can't be overtaken");
  - the rule text for each rule and size: "Three of five votes decide…", "Three of four votes decide…", and "A majority of votes cast decides once three of five members have voted…".

  Pure unit tests in `HOAManagementCompany.Tests/Unit/Architectural/ArcDecisionRulesTheoryTests.cs`.
- [X] T015 Implement the pure, static `ArcDecisionRules` (`Evaluate(rule, eligible, approve, revisionsNeeded, deny, dueDatePassed)` returning a decision with outcome and wording, plus `RuleText(rule, eligible)`) with a Repowise `domain=arc-decision` marker, until T014 passes. File: `HOAManagementCompany/Features/Board/Architectural/ArcDecisionRules.cs`
- [X] T016 Implement `ArcQueries`, the shared read layer, in `HOAManagementCompany/Features/Board/Architectural/ArcQueries.cs`. It provides:
  - eligible voters (active BoardMember memberships minus users linked to the property by `UserProperty`);
  - `IsRecused(userId, application)`;
  - a batched tally for a page of applications (one grouped query);
  - `MyVoteState` (`CanVote`/`Voted`/`Recused`/`NotEligible`);
  - `IsOverdue(application, now)` using the snapshotted `TimeZoneId` (end of `DueDate` local time);
  - `DisplayId` (`ARC-{n}`).
- [X] T017 [P] Create DTOs matching the contract JSON exactly (list item, list response with `counts`, detail, tally, myVote, decision, attachment, vote, info request, revision summary, settings, error codes as constants) in `HOAManagementCompany/Features/Board/Architectural/ArcModels.cs`
- [X] T018 Register `TimeProvider.System` as a singleton and the ARC services (`ArcQueries`, the later `ArcEmailRenderer` and `ArcSweepService`, and `IArcNotificationPreferences` → `AllowAllArcNotificationPreferences`) in `HOAManagementCompany/Program.cs`
- [X] T019 Add the `board-writes` rate-limit policy (fixed window, partitioned by user ID claim falling back to client IP, 30 requests per minute), following the existing `payments` policy, in `HOAManagementCompany/Program.cs`

### Test harness and seed (research R11, R12)

- [X] T020 Create `ArcTestBase : BoardTestBase` in `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcTestBase.cs`. Factory helpers create only their own rows, all keyed by `Guid.NewGuid()`:
  - `CreateBoardAsync(communityId, size)` returns board user IDs;
  - `CreateManagerAsync`, `CreateResidentOwnerAsync(propertyId)`;
  - `CreateApplicationAsync(communityId, …, revision, previousRevisionId, attachments, status, dueDate, decisionRule, lapseRule)`;
  - `UploadAttachmentAsync` (MinIO);
  - `CastVoteViaApiAsync`;
  - a `TestClock : TimeProvider` (hand-written subclass overriding `GetUtcNow()`, so no `Microsoft.Extensions.TimeProvider.Testing` package is added) registered as a WebApplicationFactory override.
- [X] T021 Create the idempotent `ArchitecturalSeeder` in `HOAManagementCompany/Seed/ArchitecturalSeeder.cs` per research R11:
  - ARC settings with defaults;
  - board users `board2@`…`board5@nekohoa.dev` with BoardMember memberships;
  - `manager@nekohoa.dev` with a CommunityManager membership;
  - ARC-1036, 1039, 1041 and 1042 mirroring the wireframe, with placeholder files uploaded through the `StorageSeeder` pattern;
  - existing votes so `board@nekohoa.dev` hasn't voted on ARC-1042;
  - one closed denied v1 ("revisions requested") plus an open v2.

  Insert the rows directly for now (T063 later switches the seeder to `ArcApplicationFactory`). Call it from `HOAManagementCompany/Seed/DatabaseSeeder.cs` after `EnsureBoardUserAsync`.
- [X] T022 [P] Add a seeder test asserting a second run creates no duplicates and the expected applications and memberships exist. File: `HOAManagementCompany.Tests/Integration/Seed/ArchitecturalSeederTests.cs`
- [X] T023 [P] Create a frontend `ArchitecturalService` (typed models mirroring `ArcModels`; methods `list`, `detail`, `attachmentUrl`, `vote`, `requestInfo`, `recordOutcome`, `resendOutcomeEmail`, `getSettings`, `putSettings`, following `board.service.ts`) in `neko-hoa/src/app/core/services/architectural.service.ts`, with a unit spec in `neko-hoa/src/app/core/services/architectural.service.spec.ts` (HttpTestingController: URLs, query params, bodies)

**Checkpoint**: migration applies, the resolver maps the new capabilities, the decision rules pass, and the seed runs.

---

## Phase 3: User Story 1: Review the community's open applications (P1) 🎯 MVP

**Goal**: a board member sees the community's applications with Open/Closed tabs, counts, search, tally and their own vote state.

**Independent Test** (spec US1): seed open and closed applications, sign in as a board member, open Architectural Applications, then check the tabs, counts, columns and search.

### Tests (write first)

- [X] T024 [P] [US1] Create `ApplicationsListEndpointTests` in `HOAManagementCompany.Tests/Integration/Board/Architectural/ApplicationsListEndpointTests.cs`:
  - **US1-S1**: 4 open + 27 closed; default `status` returns exactly the 4 open IDs and `counts.open == 4`, `counts.closed == 27`.
  - **US1-S2**: `status=closed` returns exactly the 27 and none of the open.
  - **US1-S3**: `search=Keystone Park` and `search=Pattyam` each include the matching application and exclude a non-matching one; matching is case-insensitive.
  - **US1-S4**: the row has `displayId == "ARC-1042"`, the address, the owner name, the project, `attachmentCount == 3` (and 0 for one without files), and the due date.
  - **US1-S5**: 2 approve + 3 not voted gives the tally `{approve:2, revisionsNeeded:0, deny:0, notVoted:3, eligible:5}`.
  - **US1-S6**: `counts.awaitingMyVote == 1` for the caller.
  - **US1-S7**: a board member of community A requesting community B, and requesting a nonexistent community, both get 403 with an identical body.
  - Pagination: default 25, `limit=101` gives 422, and `counts` ignore search and paging.
  - `Cache-Control: no-store`.
- [ ] T025 [P] [US1] Create the tally component spec: the accessible label equals "2 approve · 0 revisions needed · 0 deny · 3 not voted" for US1-S5 and "2 approve · 1 revisions needed · 0 deny · 2 not voted" for FR-007; the visible text reads "2/5"; the counts are exposed as text, not color only. File: `neko-hoa/src/app/features/board/architectural/tally.component.spec.ts`
- [ ] T026 [P] [US1] Create the applications-page component spec (Angular Testing Library, mocked `ArchitecturalService`) in `neko-hoa/src/app/features/board/architectural/applications-page.component.spec.ts`:
  - the heading is "Architectural applications";
  - **US1-S1**: tabs read "Open · 4" and "Closed · 27", Open selected, 4 rows;
  - **US1-S2**: clicking Closed requests `status=closed` and renders those rows;
  - **US1-S3**: typing in the box labeled "Search address or owner" requests `search`;
  - **US1-S4**: the column headers are exactly ID, Property, Project, Attachments, Due, Board votes, Your vote, in order, and the attachments cell reads "📎 3 files" or "none";
  - **US1-S6**: the pill reads "1 awaiting your vote" and is hidden at 0;
  - empty-search state names the term.
  - Edge case (board member of several communities): the page requests only the community from `BoardNavigationService.activeCommunityId()`, and switching the active community re-requests the new one.
- [ ] T027 [P] [US1] Create a Playwright spec: **US1-S8**, a Resident-only user navigating to `/app/board/architectural`, is redirected to a permitted page and the table never renders. Also a board member sees the page. File: `neko-hoa/e2e/board-architectural.spec.ts`

### Implementation

- [X] T028 [US1] Implement `ApplicationsListEndpoint` (`GET /communities/{communityId}/architectural-applications`) in `HOAManagementCompany/Features/Board/Architectural/ApplicationsListEndpoint.cs`:
  - `CanAccessAsync(…, ViewArchitecturalApplications)`, then `BoardHttp.NoStore`, with `BoardHttp.ForbiddenAsync` on deny;
  - validate `status`, `search` (≤ 100) and `limit`/`offset` (25/100);
  - `open` means Open + DecisionReached;
  - ILIKE on property address or `OwnerName`;
  - `awaitingMyVote` filter;
  - `counts` computed without search or paging;
  - tally and `myVote` through `ArcQueries`;
  - `overdue` computed per row.
- [ ] T029 [P] [US1] Implement the standalone `TallyComponent` (approve, revisions-needed, deny and not-voted segments; "{approve}/{eligible}" text; aria-label per FR-007 with zero segments kept; `styles.scss` tokens only) in `neko-hoa/src/app/features/board/architectural/tally.component.ts`, plus `tally.stories.ts`
- [ ] T030 [US1] Implement `ApplicationsPageComponent` (header and pill, Open/Closed tabs with counts, debounced search, table with the FR-002 columns, ID shown as `ARC-n` with a "v{n}" badge when revision ≥ 2, owner name under the address, an Overdue marker, paging, empty states) in `neko-hoa/src/app/features/board/architectural/applications-page.component.ts`
- [ ] T031 [US1] Add the route `board/architectural` (`canActivate: [boardGuard]`, `data.requiredRoles: ['BoardMember','CommunityManager']`, lazy `ApplicationsPageComponent`) in `neko-hoa/src/app/app.routes.ts`. Change the "Architectural Applications" nav entry from a stub to `route: '/app/board/architectural'`, `requiredRoles: ['BoardMember','CommunityManager']` in `neko-hoa/src/app/core/services/board-navigation.service.ts`, and update `board-navigation.service.spec.ts` for the entry being live and hidden for Accountant.

**Checkpoint**: US1 is demoable: list, tabs, search, tally and scope denial.

---

## Phase 4: User Story 2: Cast a vote (P1)

**Goal**: Approve / Revisions needed / Deny with an optional comment; one vote per member; recusal; role and closed refusals.

**Independent Test** (spec US2): click Approve on an unvoted row and see the cell, tally and pill update. Vote Deny with a comment from the detail panel and check another board member sees the comment.

### Tests (write first)

- [X] T032 [P] [US2] Create `CastVoteEndpointTests` in `HOAManagementCompany.Tests/Integration/Board/Architectural/CastVoteEndpointTests.cs`:
  - **US2-S1**: an unvoted board member's list row has `myVote.state == "CanVote"`.
  - **US2-S2**: from 2/0/3, `POST votes {choice:"Approve"}` returns 201 with `myVote.state == "Voted"`, choice Approve, and tally 3 approve / 2 not voted.
  - **US2-S3**: after a Deny, the row has `myVote` Voted with choice Deny.
  - **US2-S4**: RevisionsNeeded with comment "Fence must be 5ft max per Guideline 4.2" is stored, the tally's `revisionsNeeded` goes up by 1, and the detail shows the comment.
  - **US2-S5**: Deny with comment "Fence height exceeds the 5ft limit in §4.2", then a second board member's `GET detail` shows that comment with the voter's name.
  - **US2-S6**: Deny without a comment gives 201.
  - **US2-S7**: on a closed application, 409 `APPLICATION_CLOSED` and the tally is unchanged in the DB.
  - **US2-S8**: a board member linked to the property by `UserProperty` sees `myVote.state == "Recused"`, a vote gives 403 `RECUSED`, and they're excluded from `eligible`.
  - **US2-S9**: `[Theory]` with CommunityManager-only and Accountant-only callers gives 403 and no vote row.
  - A second vote gives 409 `ALREADY_VOTED`; a 2,001-character comment gives 422.
  - An `ArcVoteCast` sensitive event is emitted with Actor, Community, Application and UTC time.
  - The endpoint requires the `board-writes` rate limit (exceeding it gives 429).
- [ ] T033 [P] [US2] Add vote-column specs to `applications-page.component.spec.ts`:
  - **US2-S1**: Approve, Revisions needed, Deny and Info buttons render when `CanVote`.
  - **US2-S2**: clicking Approve calls `vote` and the row re-renders "you voted approve" with the new tally.
  - **US2-S3**: "you voted deny" with no buttons.
  - **US2-S4**: the "you voted revisions needed" text and the formal-denial helper note.
  - **US2-S8**: a recused indicator and no buttons.
- [ ] T034 [P] [US2] Create the cast-vote-card component spec: **US2-S5** the comment plus Deny calls `vote({choice:'Deny', comment})`; **US2-S6** an empty comment is allowed; the buttons are ✓ Approve / ↻ Revisions needed / ✕ Deny. File: `neko-hoa/src/app/features/board/architectural/cast-vote-card.component.spec.ts`

### Implementation

- [X] T035 [US2] Implement `CastVoteEndpoint` (`POST …/{applicationId}/votes`) in `HOAManagementCompany/Features/Board/Architectural/CastVoteEndpoint.cs`:
  - `CanAccessAsync(Vote)`; the application must belong to `communityId` (else 403); recusal check;
  - one transaction: `SELECT … FOR UPDATE` on the application row (raw SQL through `FromSqlInterpolated`); status checks (`APPLICATION_CLOSED`/`APPLICATION_DECIDED`); insert the vote (map a unique-violation to 409 `ALREADY_VOTED`);
  - re-count and run `ArcDecisionRules.Evaluate(snapshot rule, eligible, counts, dueDatePassed:false)`; on a decision set `Status=DecisionReached` with outcome, wording, source `Votes` and `DecisionReachedAt`;
  - commit, log `ArcVoteCast`, return the updated list item;
  - `.RequireRateLimiting("board-writes")`.
- [ ] T036 [US2] Add vote actions to the applications page row (Approve / Revisions needed / Deny / Info buttons, voted pills, recused indicator, after-vote helper note for Revisions needed, error toast on 409/403) in `neko-hoa/src/app/features/board/architectural/applications-page.component.ts`
- [ ] T037 [P] [US2] Implement `CastVoteCardComponent` ("Cast your vote" heading, the comment field "Comment to the board · Optional — visible to the board and the manager", 2,000-character counter, ✓ Approve / ↻ Revisions needed / ✕ Deny, a Request info slot used by US4) in `neko-hoa/src/app/features/board/architectural/cast-vote-card.component.ts`

**Checkpoint**: US1 + US2 make a usable voting board (MVP).

---

## Phase 5: User Story 3: Details and attachments (P1)

**Goal**: a detail panel with owner, received date, attachments that open through short-lived links, vote comments, rule text and revision links.

**Independent Test** (spec US3): select an application with three attachments; the panel lists them; one opens in a new tab and its link expires.

### Tests (write first)

- [X] T038 [P] [US3] Create `ApplicationDetailEndpointTests` in `HOAManagementCompany.Tests/Integration/Board/Architectural/ApplicationDetailEndpointTests.cs`:
  - **US3-S1**: ARC-1042 received 2026-05-28 with owner "Praneeth Pattyam" returns the owner name, `receivedDate`, the three attachments with exact `fileName`/`sizeBytes`, and `projectTitle` "Fence replacement — 6ft cedar".
  - **US3-S5**: no attachments gives an empty `attachments` array.
  - **US3-S6**: the serialized list and detail responses contain no `http` URL and no `StorageKey`.
  - `ruleText` for a 5-member majority-of-members board equals "Three of five votes decide. The manager records the outcome and notifies the owner." (FR-013).
  - Votes include comments (FR-019).
  - An `ArcSensitiveAccess` event is emitted with resource `application:{id}`.
  - Cross-community gives 403.
- [X] T039 [P] [US3] Create `AttachmentUrlEndpointTests` in `HOAManagementCompany.Tests/Integration/Board/Architectural/AttachmentUrlEndpointTests.cs`:
  - **US3-S2**: 200 with a `url` that downloads the exact uploaded bytes from MinIO, and `expiresAt` ≤ now + 15 minutes.
  - **US3-S3**: the production URL's `X-Amz-Expires` is ≤ 900. Then, through a test-only `IDocumentStorage` decorator that signs with a 1-second expiry, issue a link, wait 2 seconds, and assert MinIO answers 403 for it.
  - **US3-S4**: a non-member, and an attachment ID belonging to another application, both give 403 with no URL issued.
  - A missing object gives 404 `ATTACHMENT_UNAVAILABLE`.
  - An `ArcSensitiveAccess` event is emitted with resource `attachment:{id}`.
  - **SC-002**: issuing the link and downloading a 2 MB attachment together finish in under 3 seconds against Testcontainers MinIO.
- [ ] T040 [P] [US3] Create the detail-panel component spec in `neko-hoa/src/app/features/board/architectural/application-detail-panel.component.spec.ts`:
  - **US3-S1**: the title is "ARC-1042 · fence replacement", plus owner, received date and three files with sizes.
  - **US3-S2**: clicking a file calls `attachmentUrl` and opens `window.open(url, '_blank', 'noopener')`.
  - **US3-S5**: the "No attachments" text.
  - **US3-S6**: the rendered DOM has no anchor `href` to storage before a click.
  - "Attachment unavailable" shows on 404.
  - The rule text renders.
  - A vote comment of `<script>alert(1)</script>` renders as literal text and adds no element to the DOM (Constitution: user text rendered as text).

### Implementation

- [X] T041 [US3] Implement `ApplicationDetailEndpoint` (`GET …/{applicationId}`) in `HOAManagementCompany/Features/Board/Architectural/ApplicationDetailEndpoint.cs`: the View capability; the application in the community; attachments metadata; votes with voter names and comments; info requests; the revision chain by `(CommunityId, ApplicationNumber)` ordered by `Revision`; `ruleText` through `ArcDecisionRules.RuleText`; decision, conditions or reason; `ownerEmailStatus` read from the latest `arc:{id}:outcome*` outbox row (null when none); the `ArcSensitiveAccess` log; `no-store`.
- [X] T042 [US3] Implement `AttachmentUrlEndpoint` (`GET …/attachments/{attachmentId}/url`) in `HOAManagementCompany/Features/Board/Architectural/AttachmentUrlEndpoint.cs`: the View capability; the attachment must belong to the application in the community; a `HEAD` existence check (add `ExistsAsync` to `IDocumentStorage` and `S3DocumentStorage` in `HOAManagementCompany/Infrastructure/Storage/`, and update every `IDocumentStorage` test double found with `grep -rn ": IDocumentStorage" HOAManagementCompany.Tests`) returning 404 `ATTACHMENT_UNAVAILABLE`; `GetPreSignedUrlAsync`; return `{url, expiresAt = now + 5 min}`; log `ArcSensitiveAccess`.
- [ ] T043 [US3] Implement `ApplicationDetailPanelComponent` (title, owner, received date, attachment list with name, size and ↗ link opening on click, empty and unavailable states, votes and comments list, rule text, embedded `CastVoteCardComponent`, links to earlier revisions) in `neko-hoa/src/app/features/board/architectural/application-detail-panel.component.ts`, plus `application-detail-panel.stories.ts`. Wire row selection in `applications-page.component.ts`, supporting the `?open={id}` query parameter.

**Checkpoint**: all P1 stories are done.

---

## Phase 6: User Story 4: Request more information (P2)

**Goal**: a board member sends a required message that doesn't vote, doesn't move the due date, and warns that the clock keeps running.

**Independent Test** (spec US4): request info with "Please attach a plat survey". The marker appears, the tally is unchanged and the vote buttons stay.

### Tests (write first)

- [X] T044 [P] [US4] Create `InfoRequestEndpointTests` in `HOAManagementCompany.Tests/Integration/Board/Architectural/InfoRequestEndpointTests.cs`:
  - **US4-S2**: "Please attach a plat survey" gives 201; the detail shows the request with sender name; `infoRequested == true` in the list; the tally is unchanged; `myVote.state` is still `CanVote`; `dueDate` is unchanged in the response and the DB (FR-021).
  - **US4-S3**: an empty or whitespace message gives 422 `VALIDATION_ERROR` with a message naming the required field.
  - **US4-S4**: a CommunityManager's `GET detail` shows message, sender and `requestedAt`.
  - A manager or accountant posting gives 403; a closed application gives 409.
- [ ] T045 [P] [US4] Add Request-info specs to `application-detail-panel.component.spec.ts` and `applications-page.component.spec.ts`:
  - **US4-S1**: clicking Info on a row opens the panel with the comment box focused and Request info enabled.
  - **US4-S2**: submitting shows the info-requested marker and the vote buttons stay.
  - **US4-S5**: choosing Request info shows the notice "Questions don't pause the review period (due 06/27/26). To require changes before approval, vote Revisions needed — it counts as a formal denial and invites the owner to resubmit." with the formatted due date.

### Implementation

- [X] T046 [US4] Implement `InfoRequestEndpoint` (`POST …/{applicationId}/info-requests`; Vote capability; Open status only; non-blank message ≤ 2,000; insert; return `{…, dueDate}`; `board-writes`) in `HOAManagementCompany/Features/Board/Architectural/InfoRequestEndpoint.cs`
- [ ] T047 [US4] Add the Request info mode to `CastVoteCardComponent` and the detail panel (Info focuses the comment box; the FR-021 notice with the due date; the marker with message, sender and time) in `neko-hoa/src/app/features/board/architectural/cast-vote-card.component.ts` and `application-detail-panel.component.ts`. Show the "info requested" marker on rows in `applications-page.component.ts`.

---

## Phase 7: User Story 5: Needs your vote on Community Home (P2)

**Goal**: a card on Community Home listing applications awaiting the caller's vote, with inline voting.

**Independent Test** (spec US5): with one unvoted application the card shows "1 open"; approving removes it.

### Tests (write first)

- [X] T048 [P] [US5] Add `awaitingMyVote=true` cases to `ApplicationsListEndpointTests.cs`: **US5-S1** with 1 unvoted + 3 voted, only that one is returned with all FR-032 fields; **US5-S3** after a vote it's no longer returned; **US5-S4** with everything voted, an empty list.
- [ ] T049 [P] [US5] Create the needs-your-vote-card component spec in `neko-hoa/src/app/features/board/architectural/needs-your-vote-card.component.spec.ts`:
  - **US5-S1**: "Needs your vote" heading, "1 open" pill, one row with ID, project, address · owner, 📎 count, tally, due date and the Approve / Revisions needed / Deny buttons.
  - **US5-S2**: the "All architectural applications →" link points to `/app/board/architectural`.
  - **US5-S3**: Approve calls `vote` and the row disappears. This is the one-click path for SC-001.
  - **US5-S4**: an empty-state message.

### Implementation

- [ ] T050 [US5] Implement `NeedsYourVoteCardComponent` (loads `list({awaitingMyVote:true, limit:25})`; rows and buttons per FR-032; the link; empty state; `needs-your-vote-card.stories.ts`) in `neko-hoa/src/app/features/board/architectural/needs-your-vote-card.component.ts`
- [ ] T051 [US5] Render `<app-needs-your-vote-card>` as its own section above the metrics panels, only when the user's roles include BoardMember in the active community, in `neko-hoa/src/app/features/board/community-home/community-home.component.ts`. Leave the rest of the page untouched for spec 2.

---

## Phase 8: User Story 6: Decisions, outcomes, revisions, reminders and lapses (P2)

**Goal**: the decision rules applied on vote and at the due date; the manager records the outcome and the owner is emailed; revisions are linked; reminders; per-community lapse rules and settings.

**Independent Test** (spec US6): on a 5-member board, 3 approve votes reach a decision; the manager records it, the application closes and the approved email is queued. Each lapse rule, run through the sweep, produces its outcome and board emails.

### Tests (write first)

- [X] T052 [P] [US6] Create `DecisionOnVoteTests` (API-level, real database) in `HOAManagementCompany.Tests/Integration/Board/Architectural/DecisionOnVoteTests.cs`:
  - **US6-S1**: majority of members, 5 eligible, 2 approve; a third approve gives `decision.outcome == "Approved"`, `status == DecisionReached`, and a further vote gives 409 `APPLICATION_DECIDED`.
  - **US6-S2**: three deny votes give `outcome == "Denied"`, `wording == "Denied"`.
  - **US6-S3**: 2 approve / 2 deny out of 5 gives no decision and voting stays open.
  - **US6-S4**: votes cast, 5 eligible, 3 approve / 0 deny gives an immediate Approved.
  - **US6-S9**: majority of members, 1 RevisionsNeeded / 1 Deny / 1 Approve; another RevisionsNeeded gives Denied with wording `RevisionsRequested`.
  - Edge case (board size changes mid-vote): a member votes approve and their membership is then ended; their vote still counts in the tally, and `eligible` drops by one. A member added after the application opened can vote on it.
- [X] T053 [P] [US6] Create `ArcConcurrencyTests` in `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcConcurrencyTests.cs` (SC-005, FR-023): 5 eligible, 2 approve; two board members send approve votes concurrently (`Task.WhenAll`). Assert exactly one vote is stored and the other request gets 409 `APPLICATION_DECIDED` (FR-018), the application has one decision (Approved) with `DecisionReachedAt` set once, and the tally shows 3 approve. After the outcome is recorded, exactly one `arc_owner_*` outbox row.
- [X] T054 [P] [US6] Create `RecordOutcomeEndpointTests` in `HOAManagementCompany.Tests/Integration/Board/Architectural/RecordOutcomeEndpointTests.cs`:
  - **US6-S7**: Approved; the manager posts `{}` and gets 200; `status Closed`; `closedAt` set; one outbox row `Kind arc_owner_approved`, `OwnerId` = the property owner, DedupKey `arc:{id}:outcome`; the payload body contains the display ID, address and project and has no conditions section.
  - **US6-S8**: conditions "Fence must be stained to match the existing color" are stored and appear in the approved payload's conditions section.
  - **US6-S10**: Denied/RevisionsRequested with the reason "Lower the fence to 5ft per Guideline 4.2" gives `Kind arc_owner_revisions_requested`; the payload contains the reason, the community's `FormalDisapprovalStatement` and the `/app/property/architectural/{id}/revise` link; it contains no voter names or vote comments; the list's closed row shows the decision wording `RevisionsRequested`.
  - **US6-S11**: 3 Deny + 1 RevisionsNeeded gives `Kind arc_owner_denied` with the reason, the same statement and the same link.
  - Errors:
    - a denial without a reason gives 422;
    - conditions on a denial give 422;
    - `wording` on a vote-decided denial gives 422;
    - an Open application gives 409 `NO_DECISION`;
    - a board member or accountant posting gives 403.
  - An `ArcOutcomeRecorded` event.
  - Owner missing: a property with no `Owner` row, or an owner with a blank email, still closes the application, writes no outbox row, and returns `ownerEmailStatus == "NoOwnerEmail"`. The manager sees that status.
  - Resend: a failed dispatch (no configured provider) gives `ownerEmailStatus Failed`; resend gives 202 and a new row with DedupKey `…:resend:1`; resend when Sent gives 409 `EMAIL_NOT_FAILED`.
- [X] T055 [P] [US6] Create `RevisionHistoryTests` in `HOAManagementCompany.Tests/Integration/Board/Architectural/RevisionHistoryTests.cs`:
  - **US6-S12**: a closed denied v1 and an open v2 of ARC-1042 (`PreviousRevisionId` → v1) give v2 a detail with `revision == 2`, its own received and due dates, empty votes, and `revisions` containing v1 with its decision.
  - v1's detail still shows its votes and comments.
  - v1's attachments are unchanged when the v2 rows sharing a `StorageKey` are deleted from the DB (FR-034).
- [X] T056 [P] [US6] Create `ArcSweepTests` with `TestClock` and the community `TimeZoneId` `America/New_York` in `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcSweepTests.cs`:
  - **US6-S13**: reminder of 7, due 2026-06-27; at 2026-06-20 local time the sweep queues exactly one `arc_board_reminder` per active board member (DedupKey `arc:{id}:reminder:{userId}`, `RecipientUserId` set); a second sweep queues none; a decided application gets none; `ReminderDays = 0` sends none.
  - **US6-S14**: flag overdue only; after the end of the due date the application stays Open with `overdue == true`, voting still works, and one `arc_board_lapsed` per board member.
  - **US6-S15**: deemed approved gives `DecisionReached`, Approved, source `Lapse`, votes refused (409), lapse emails.
  - **US6-S16**: deemed denied gives Denied, source `Lapse`, lapse emails.
  - **US6-S5**: votes cast, 2 approve / 1 deny at the due date gives Approved from votes and no lapse rule applied.
  - **US6-S6**: votes cast, only 2 votes gives no quorum, so the community's lapse rule applies.
  - Time zone: 23:30 New York on the due date isn't overdue, 00:30 the next day is.
  - Idempotency: the second run is all zeros.
  - Opt-out seam: a test double `IArcNotificationPreferences` excluding one user gives that user no row.
- [X] T057 [P] [US6] Create `ArcSweepJobEndpointTests` in `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcSweepJobEndpointTests.cs`: a missing or wrong `X-Scheduler-Secret` gives 401; the correct secret gives 200 with the four counts.
- [X] T058 [P] [US6] Create `ArcSettingsEndpointTests` in `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcSettingsEndpointTests.cs`:
  - **US6-S17**: review period 45 and received 2026-05-01 gives `dueDate` 2026-06-15, created through `ArcApplicationFactory.CreateFromSettings` (implemented in T063; this case fails until then).
  - **US6-S18**: a manager PUT with all five fields gives 200 and they're saved. An application received before the change keeps its old `DueDate`, `DecisionRule` and `LapseRule` snapshots, and one received after uses the new values. A board member or resident PUT gives 403.
  - Validation `[Theory]`: review period 0 and 366, reminder −1 and 31, time zone "Mars/Olympus", blank statement, 1,001-character statement, each giving 422.
  - GET with no row returns the defaults (30 / FlagOverdueOnly / MajorityOfMembers / 7 / America/New_York / default statement) and writes nothing.
  - An `ArcSettingsChanged` event with old and new values.
- [ ] T059 [P] [US6] Create the frontend specs:
  - in `applications-page.component.spec.ts`: "decision reached: approve", "decision reached: denied · revisions requested" and "decision reached: denied" render; the Closed tab shows "Denied · revisions requested"; the "v2" badge and the Overdue marker render.
  - `neko-hoa/src/app/features/board/architectural/record-outcome.component.spec.ts`: the reason field is required for denials, with the prompt "What would need to change for approval?"; conditions are only for approvals; the wording picker appears only for lapse denials; there's a resend button on a failed email.
  - `neko-hoa/src/app/features/board/architectural/arc-settings.component.spec.ts`: field validation messages; save calls `putSettings`.

### Implementation

- [X] T060 [US6] Implement `ArcEmailRenderer` (plain-text subject and body for the 5 kinds per the contract's email table; owner emails include no votes or names; builds `AlertMessage` and `PayloadJson`) in `HOAManagementCompany/Features/Board/Architectural/ArcEmailRenderer.cs`. Also implement `IArcNotificationPreferences` with a default `AllowAllArcNotificationPreferences` in `HOAManagementCompany/Features/Board/Architectural/IArcNotificationPreferences.cs`.
- [X] T061 [US6] Implement `RecordOutcomeEndpoint` (`POST …/outcome`; Manage capability; row lock; requires `DecisionReached`; FR-025 validation; wording only for `Source == Lapse` denials, default RevisionsRequested; set `Closed`, `ClosedAt` and `ClosedByUserId`; if the property has an owner with an email, enqueue the owner outbox row in the same transaction with `OwnerId` = `Property.Owner.Id` (otherwise skip it and report `NoOwnerEmail`) and DedupKey `arc:{id}:outcome`; commit; dispatch through `OutboxDispatcher`; log `ArcOutcomeRecorded`; `board-writes`) in `HOAManagementCompany/Features/Board/Architectural/RecordOutcomeEndpoint.cs`
- [X] T062 [US6] Implement `ResendOutcomeEmailEndpoint` (`POST …/outcome/resend-email`; Manage capability; only when Closed and the email failed, else 409 `EMAIL_NOT_FAILED`; a new outbox row with DedupKey `arc:{id}:outcome:resend:{n}`; dispatch; 202) in `HOAManagementCompany/Features/Board/Architectural/ResendOutcomeEmailEndpoint.cs`
- [X] T063 [US6] Implement `ArcApplicationFactory.CreateFromSettings` (allocates `ApplicationNumber` with `UPDATE "CommunityArcSettings" SET "NextApplicationNumber" = "NextApplicationNumber" + 1 … RETURNING`, computes `DueDate`, snapshots the rules and time zone, and creates a revision with `PreviousRevisionId` and carried-over attachment metadata when given a prior revision; it refuses a revision unless the previous one is Closed and Denied) in `HOAManagementCompany/Features/Board/Architectural/ArcApplicationFactory.cs`. Switch `ArchitecturalSeeder` (T021) from direct inserts to the factory. Add factory tests (revision of an open or approved application is refused; carried-over attachments share the `StorageKey`) in `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcApplicationFactoryTests.cs`.
- [X] T064 [US6] Implement `ArcSettingsGetEndpoint` and `ArcSettingsPutEndpoint` (GET returns defaults without writing when no row exists, under the View capability; the row is created on the first PUT or by `ArcApplicationFactory` on first application; PUT needs the Manage capability, FluentValidation per data-model.md, `TimeZoneInfo.FindSystemTimeZoneById` validation, `ArcSettingsChanged` log with old and new values, `board-writes`) in `HOAManagementCompany/Features/Board/Architectural/ArcSettingsGetEndpoint.cs` and `ArcSettingsPutEndpoint.cs`
- [X] T065 [US6] Implement `ArcSweepService` (`TimeProvider`-driven, with a Repowise `domain=arc-sweep` marker) in `HOAManagementCompany/Features/Board/Architectural/ArcSweepService.cs`:
  1. Reminders per research R6, honoring `IArcNotificationPreferences`, stamping `ReminderSentAt`.
  2. Due-date decisions for the votes-cast rule.
  3. Lapses: apply the snapshotted rule under the row lock, set `LapseProcessedAt`, enqueue `arc_board_lapsed` per recipient.
  4. `OutboxDispatcher.DispatchPendingAsync`.

  Return the counts.
- [X] T066 [US6] Implement `ArcSweepJobEndpoint` (`POST /architectural/jobs/sweep`, `AllowAnonymous`, constant-time `X-Scheduler-Secret` compare against `JobsOptions.SchedulerSharedSecret` as in `Features/Payments/Jobs/RunDraftsEndpoint.cs`, calls `ArcSweepService`) in `HOAManagementCompany/Features/Board/Architectural/ArcSweepJobEndpoint.cs`
- [ ] T067 [US6] Add an hourly `google_cloud_scheduler_job` targeting `${cloud_run_url}/api/v1/architectural/jobs/sweep` with an `X-Scheduler-Secret` header from the existing `scheduler-secret` Secret Manager value. Create `infra/modules/environment/scheduler.tf`, add any needed variables in `infra/modules/environment/variables.tf`, and follow the pinned provider in `versions.tf`. Don't add it to `infra/modules/pr-environment/`. Run `tofu validate` for `infra/environments/dev` and `infra/environments/staging`.
- [ ] T068 [US6] Implement `RecordOutcomeComponent` (manager only, shown in the detail panel when `DecisionReached`: reason field with the prompt, conditions field for approvals, wording picker for lapse denials, submit, email status and Resend) in `neko-hoa/src/app/features/board/architectural/record-outcome.component.ts`. Embed it in `application-detail-panel.component.ts`.
- [ ] T069 [US6] Show decisions in the UI: decision labels, Closed-tab wording, the "v2" badge and revision links in `neko-hoa/src/app/features/board/architectural/applications-page.component.ts` and `application-detail-panel.component.ts`
- [ ] T070 [US6] Implement `ArcSettingsComponent` (manager form for the five settings plus time zone, with validation and save) in `neko-hoa/src/app/features/board/architectural/arc-settings.component.ts`. Add the route `board/arc-settings` (`requiredRoles: ['CommunityManager']`) in `neko-hoa/src/app/app.routes.ts` and an "ARC Settings" nav item (`requiredRoles: ['CommunityManager']`) in `neko-hoa/src/app/core/services/board-navigation.service.ts` with its spec update.

---

## Phase 9: Polish and cross-cutting

- [ ] T071 [P] Add a Cypress journey: sign in as `board@nekohoa.dev` → Enter board mode → Architectural Applications → open ARC-1042 → Approve → "you voted approve". File: `neko-hoa/cypress/e2e/board-architectural.cy.ts`
- [ ] T072 [P] Add a Playwright vote journey (board member votes Revisions needed with a comment and sees the formal-denial note; the info-request notice appears) to `neko-hoa/e2e/board-architectural.spec.ts`
- [ ] T073 [P] Add a performance test (SC-006): a community with 500 applications × 5 votes; the first list page answers in under 2 s against Testcontainers. File: `HOAManagementCompany.Tests/Performance/ArcListPerformanceTests.cs`
- [ ] T074 [P] Add accessibility checks: keyboard reachability of tabs, search, row actions and panel; labeled controls; tally text alternatives (axe through the existing Playwright setup if present, otherwise explicit role and label assertions). File: `neko-hoa/e2e/board-architectural.spec.ts`
- [ ] T075 [P] Check Storybook stories for tally, detail panel and needs-your-vote card in both themes, using tokens only, in `neko-hoa/src/app/features/board/architectural/*.stories.ts`
- [ ] T076 Run the full backend suite (`dotnet test`) and frontend (`cd neko-hoa && npm run test:ci && npm run build`). Confirm the payment outbox tests still pass after T007 and that coverage on new files is ≥ 95%.
- [ ] T077 Walk through `specs/027-board-arc-review/quickstart.md` against a local stack and fix any drift in the quickstart or code
- [ ] T078 NLT audit per CLAUDE.md: for every `USn-Sm` and Independent Test in `spec.md`, confirm the citing test exists, asserts the stated Then, and isn't skipped. Record the mapping table at the end of this file.
- [ ] T079 Keep the spec truthful: update `specs/027-board-arc-review/spec.md` for any implementation-driven change, mark this file's tasks done, and refresh the Repowise marker regions listed in `plan.md`
- [X] T080 [P] Add a telemetry-hygiene test: after votes, info requests, detail views and attachment links, assert that the `ArcVoteCast`/`ArcSensitiveAccess`/`ArcOutcomeRecorded` log events carry only IDs, and that none of the captured log events or OpenTelemetry span attributes (using the existing in-memory exporter or activity listener) contain the comment text, owner name or storage key used in the test. File: `HOAManagementCompany.Tests/Integration/Board/Architectural/ArcTelemetryHygieneTests.cs`

---

## Dependencies and Execution Order

### Phase dependencies

| Phase | Depends on |
|---|---|
| Setup (1) | nothing |
| Foundational (2) | Setup. Blocks every story. |
| US1 (3) | Phase 2 |
| US2 (4) | Phase 2. Its UI tasks (T033, T036) extend the page from US1 (T030), so do US1 first if one person is working. The backend (T032, T035) is independent. |
| US3 (5) | Phase 2. T043 embeds the vote card (T037). |
| US4 (6) | Phase 2. Its UI extends the vote card and panel (T037, T043). |
| US5 (7) | Phase 2 and the vote endpoint (T035) |
| US6 (8) | Phase 2. The decision-on-vote tests (T052, T053) need T035. The UI needs the panel (T043). |
| Polish (9) | All stories |

Within US6: the US6-S17 case in T058 and the seeder switch in T063 both need `ArcApplicationFactory` (T063), so that case stays red until T063 is done.

### Within each story

Tests (fail first) → backend endpoint → frontend component → route and nav wiring.

### Cross-spec dependencies

- **Hard dependency on 025** (merged): `Community`, `CommunityMembership`, `ICommunityScopeResolver`, `BoardHttp`, `boardGuard`, `BoardNavigationService`, `IDocumentStorage`.
- **Sibling: Resident Architectural Application Submission.** Shares the tables from T003–T009 and the `ArcApplicationFactory` (T063). Whichever spec lands first creates them. If the submission spec merges first, rebase this spec's Phase 2 onto its migration instead of generating a duplicate.
- **Sibling: Notification Settings.** Plugs into `IArcNotificationPreferences` (T060). No ordering constraint.
- **Spec 2 (Community Overview & Metrics).** Edits other parts of `community-home.component.ts`. T051 adds its own section only.

---

## Parallel examples

```text
# Phase 2, after T003:
T004, T005, T006 (entities), T012 (capability test), T014 (decision theory), T017 (DTOs), T023 (frontend service)

# US1 tests together:
T024 (list endpoint), T025 (tally spec), T026 (page spec), T027 (Playwright)

# US6 tests together:
T052, T053, T054, T055, T056, T057, T058, T059
```

---

## Implementation strategy

1. **MVP**: Phase 1, Phase 2, then US1 + US2 (a list the board can vote from), then US3 so voters can see the plans.
2. **Increment 2**: US4 (info requests) and US5 (Community Home card).
3. **Increment 3**: US6 in this order:
   1. decision on vote (T052, T053);
   2. outcome and emails (T054, T060–T062, T068);
   3. settings and factory (T058, T063, T064, T070);
   4. sweep and scheduler (T056, T057, T065–T067);
   5. revisions view (T055, T069).
4. **Polish**: then the NLT audit (T078) and telemetry-hygiene test (T080) before opening the PR for review.

## NLT mapping (filled by T078)

| Scenario | Test |
|---|---|
| _to be completed during implementation_ | |
