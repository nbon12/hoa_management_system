# Tasks: Board Member Experience — Overall Design

**Input**: Design documents from `/specs/025-board-overall-design/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/board-access.md

**Tests**: Test tasks are included and are part of the completion gate. Per the constitution
(spec.md → Constitution Requirements: *Quality gates* / *Frontend testing*), the community-scope
resolver, membership evaluation, and migration are 95%-coverage-critical; backend uses
xUnit + Testcontainers.PostgreSQL, frontend uses Jasmine/Karma + Angular Testing Library +
Playwright + Cypress + Storybook.

**Test-first (red→green)**: Per the Spec Kit Testing Constitution §2.4, each `*Tests*` task MUST be
authored and made to **fail (red)** *before* its paired implementation task begins, and
implementation proceeds in red→green cycles. Task IDs mark phase/grouping, not strict authoring
order; where a test task is listed after its implementation, the test is still written first. The
red→green pairs are: **T028 before T017** (community-scope resolver — the blocking primitive is
test-first even though its suite is grouped under US2), **T020 before T019** (board-mode endpoint),
**T033 before T032** (`/me/communities`), **T046 before T044 & T045** (membership admin), and
**T052 before T051** (board metrics endpoint). Non-compiling tests may be temporarily commented
with a clear restore path (§2.4).

**Organization**: Tasks are grouped by user story. Priority order from spec.md is
US1 (P1) · US2 (P1) · US3 (P1) · US5 (P1) · US4 (P2).

## Path Conventions (Option 2 — web app, existing repo shape)

- Backend: `HOAManagementCompany/` (Domain, Features/Board, Infrastructure/Persistence)
- Backend tests: `HOAManagementCompany.Tests/Integration/Board/`
- Frontend: `neko-hoa/src/app/` (features/board, core/services, core/guards, shell)

---

## Phase 1: Setup

**Purpose**: Create the new feature folders the rest of the work lands in. No new package for either project (plan.md).

- [x] T001 Create backend feature folder `HOAManagementCompany/Features/Board/` and test folder `HOAManagementCompany.Tests/Integration/Board/`
- [x] T002 [P] Create frontend feature folder `neko-hoa/src/app/features/board/` with subfolders `mode-toggle/`, `community-home/`, `membership-admin/`, `metrics/`

---

## Phase 2: Foundational (BLOCKING — must complete before any user story)

**Purpose**: The community tenant boundary, membership model, migration, and shared authorization
primitive that every user story depends on. Nothing in Phases 3+ can proceed until this phase is green.

### Enums (data-model.md → New entities)

- [x] T003 [P] Create `CommunityRole` enum (`Resident`, `BoardMember`, `CommunityManager`, `Accountant`; no `CommitteeMember`) in `HOAManagementCompany/Domain/Enums/CommunityRole.cs`
- [x] T004 [P] Create `MembershipStatus` enum (`Active`, `Inactive`) in `HOAManagementCompany/Domain/Enums/MembershipStatus.cs`
- [x] T005 [P] Create `CommunityStatus` enum (`Active`, `Inactive`) in `HOAManagementCompany/Domain/Enums/CommunityStatus.cs`
- [x] T006 [P] Create `UserMode` enum (`Resident`, `Board`) in `HOAManagementCompany/Domain/Enums/UserMode.cs`

### Entities (data-model.md → New / Modified entities)

- [x] T007 Create `Community` entity (Id GUID PK, LegalName, CommunityName, County?, FormationDate?, ManagementStartDate?, Description?, Status, ParentCommunityId? self-ref, CreatedAt; navs ParentCommunity/SubCommunities/Properties/Memberships) in `HOAManagementCompany/Domain/Entities/Community.cs`
- [x] T008 Create `CommunityMembership` entity (Id GUID PK, UserId string FK, CommunityId GUID FK, Role, Status, StartDate, EndDate?, CreatedAt) in `HOAManagementCompany/Domain/Entities/CommunityMembership.cs`
- [x] T009 Modify `Property` — remove `CommunityId`(string) & `CommunityName`(string), add `CommunityId`(GUID FK) + `Community` navigation in `HOAManagementCompany/Domain/Entities/Property.cs`
- [x] T010 [P] Modify **all remaining community-scoped entities** (FR-006) — replace the loose `CommunityId`(string) with `CommunityId`(GUID FK) + `Community` navigation on `Violation`, `HoaDocument`, `Poll`, `CalendarEvent`, `HoaPaymentConfig`, `Announcement`, and `CommunityExpense` (`HOAManagementCompany/Domain/Entities/*.cs`)
- [x] T011 Modify `ApplicationUser` — add `Memberships` collection nav + `LastActiveMode` (`UserMode`, default `Resident`), and update the existing Repowise marker comment (lines ~5-7) in `HOAManagementCompany/Domain/Entities/ApplicationUser.cs`

### Persistence config + migration (data-model.md → Migration sequencing, research.md R5)

- [x] T012 Add `DbSet<Community>` and `DbSet<CommunityMembership>` and entity configuration in `ApplicationDbContext` (`HOAManagementCompany/Infrastructure/Persistence/`): unique index on `Community.CommunityName`; unique index on `CommunityMembership (UserId, CommunityId, Role)`; Property/Violation → Community FKs; self-referencing `Community.ParentCommunityId`
- [x] T013 Update every consumer of the old string `CommunityId` for the Guid FK (FR-004/FR-006): change `ClaimsPrincipalExtensions.RequireCommunityId()` to return `Guid`; update `DashboardService`, `CommunityService`, `PollService`, `PaymentConfigService` filters to compare `CommunityId` (Guid); derive community name via `property.Community.CommunityName` in `PropertyService`/`PropertyDto`/`OwnerDto` (FR-004); and update the `Seed/*` seeders to create a `Community` row and reference its Guid
- [x] T014 Create single EF Core migration `AddCommunityAndMembership` in `HOAManagementCompany/Infrastructure/Persistence/Migrations/` following data-model.md's 5-step sequence: create tables → add nullable GUID FK columns on **all eight** community-scoped tables (Property, Violation, HoaDocument, Poll, CalendarEvent, HoaPaymentConfig, Announcement, CommunityExpense) → **idempotent** backfill `Community` rows from distinct `(Property.CommunityId, Property.CommunityName)` pairs and set each table's GUID FK from the `Community` whose `CommunityName` = the old string handle → drop old string columns + `Property.CommunityName`, make FKs non-null, add FK constraints + unique index → add `ApplicationUser.LastActiveMode`. Reversible down-migration re-derives the string columns (FR-005)
- [x] T015 Migration integration test (Testcontainers.PostgreSQL) in `HOAManagementCompany.Tests/Integration/Board/CommunityBackfillMigrationTests.cs`: seed a multi-community dataset, run up-migration, assert no lost property associations and correct Violation FK mapping, run down-migration, assert reversibility

### Shared authorization primitive (research.md R4, FR-012/FR-014)

- [x] T016 [P] Define `CommunityCapability` enum (e.g. `ViewAssociationData`, `ManageMemberships`) and `ICommunityScopeResolver` interface (`CanAccessAsync(user, communityId, capability)`) in `HOAManagementCompany/Features/Board/ICommunityScopeResolver.cs`
- [x] T017 Implement `CommunityScopeResolver` in `HOAManagementCompany/Features/Board/CommunityScopeResolver.cs` — evaluates the effective-permission rule at read time (membership `Status == Active`, `EndDate` null-or-future UTC, owning `Community.Status == Active`, role satisfies capability; never from client mode). Decides scope **strictly per `Community` row** — MUST NOT traverse `Community.ParentCommunityId` (master membership grants no access to sub-associations, and vice versa; Clarifications 2026-08-23). Grants the capability when **any** of the caller's active membership rows in the target community confers it (**union** across role rows — a plain "allow if any grants it" check, no role-precedence table). Plus DI registration in `Program.cs`
- [x] T018 Modify backend `AuthService` (`HOAManagementCompany/Features/Auth/AuthService.cs`) — `communityId` claim now carries the `Community.Id` GUID (research.md R2) and add support for minting a token reflecting `LastActiveMode` (research.md R7)

**Checkpoint**: Entities, migration, and resolver exist and are tested. User stories may now begin (parallelizable where independent).

---

## Phase 3: User Story 1 — Enter and leave board mode (Priority: P1) 🎯 MVP

**Goal**: A user with an active non-resident membership sees an "Enter board mode" control, switches into board mode (distinct banner appears), and can switch back; last-used mode persists.

**Independent Test**: Sign in as a user with an active board membership → control present → enter board mode → shell changes + banner appears → switch back → resident view intact; sign out/in returns to last mode.

- [x] T019 [US1] Implement `BoardModeEndpoint` (`POST /api/v1/auth/board-mode`) in `HOAManagementCompany/Features/Board/BoardModeEndpoint.cs` — mirrors `SwitchPropertyEndpoint`: persist `ApplicationUser.LastActiveMode`, re-mint token pair + rotate refresh cookie, server-verifies ≥1 active non-resident membership before allowing `Board` (FR-014/FR-020), return `communityId` when exactly one membership; `403 NO_ACTIVE_MEMBERSHIP` otherwise (contracts/board-access.md)
- [x] T020 [US1] Backend test in `HOAManagementCompany.Tests/Integration/Board/BoardModeEndpointTests.cs` — Theory over {has active non-resident membership, resident-only, expired term}: allow → 200 + mode persisted (+ communityId when single), deny → 403 NO_ACTIVE_MEMBERSHIP (Acceptance Scenarios 1,2,5); and assert a fresh login/refresh boots into the persisted `LastActiveMode` (Acceptance Scenario 4)
- [x] T021 [US1] Extend frontend `AuthService` (`neko-hoa/src/app/core/services/auth.service.ts`) — decode membership/role + mode from the JWT and add `switchMode(mode)` calling the board-mode endpoint, persisting returned mode/communityId
- [x] T022 [P] [US1] Create mode-toggle component in `neko-hoa/src/app/features/board/mode-toggle/` — rendered in the top-bar account-controls cluster to the left of alerts/avatar, and only when the user holds ≥1 active non-resident membership (FR-019/FR-020)
- [x] T023 [P] [US1] Create board-mode banner component in `neko-hoa/src/app/features/board/` — visually distinct (dark ink on violet fill per spec Accessibility note), states association-wide data is shown, carries no control (FR-021)
- [x] T024 [US1] Wire the mode-toggle + banner into `shell.component` and top bar; land the user according to the board-mode response
- [x] T025 [P] [US1] Karma unit test for the mode service/toggle visibility rule (present with membership, absent without) in `neko-hoa/src/app/.../mode-toggle.spec.ts`
- [ ] T026 [P] [US1] Playwright test for the enter/leave board-mode journey (control → banner → back) under `neko-hoa/` e2e, including sign-out → sign-in returns to the last-used board mode (Acceptance Scenario 4)
- [ ] T027 [P] [US1] Storybook story for the board-mode banner (visual regression)

**Checkpoint**: US1 independently demonstrable — a board member can enter and leave board mode.

---

## Phase 4: User Story 2 — See only my own community's data (Priority: P1)

**Goal**: All board-mode access is community-scoped server-side; out-of-scope requests fail closed and non-disclosing; association-wide data access is audited.

**Independent Test**: With two seeded communities and a board member in one, every board endpoint returns only in-scope records and returns `403 FORBIDDEN` (never 404) for an out-of-scope community id; resident endpoints do not widen.

- [x] T028 [US2] Resolver behavior test suite in `HOAManagementCompany.Tests/Integration/Board/CommunityScopeResolverTests.cs` — Theory varying role × membership status × target community against a seeded two-community dataset, including expired terms and role-mismatch denials (spec Quality gates). MUST include the two Clarifications-2026-08-23 boundary cases: (a) **master/sub non-cascade** — a member of the master is denied scope over a sub-association and a member of a sub is denied scope over the master (both directions, per FR-012); (b) **multi-role union** — a user holding two roles in one community receives the union of both capability sets (allow if any active row grants it)
- [x] T029 [US2] Implement fail-closed, non-disclosing denial: out-of-scope community and non-existent community both return identical `403 FORBIDDEN` body via the `DomainException` pattern (FR-016); assert in test that no 404 distinguishes them
- [x] T030 [US2] Add audit logging (FR-017) — emit a structured Serilog sensitive-event (actor, community, resource, UTC timestamp) when a user accesses association-wide personal/financial data; wire into the resolver/endpoint path. **No queryable audit table is introduced** (Clarifications 2026-08-23); the US2 test asserts the structured event is emitted with all four fields
- [x] T031 [US2] Regression assertion that resident-scoped endpoints retain "own properties only" semantics and do NOT widen for a user who also holds a board membership (FR-015) — add to `HOAManagementCompany.Tests/Integration/Board/ResidentScopeUnchangedTests.cs`

**Checkpoint**: The security boundary is proven independently of the UI.

---

## Phase 5: User Story 3 — Navigate the board shell (Priority: P1)

**Goal**: A single-community user lands directly on Community Home; nav is derived from role; "My Communities" appears only for 2+ communities; role-forbidden sections are shown disabled with a lock and direct navigation is refused.

**Independent Test**: Sign in as each of board/manager/accountant against a seeded community → rendered nav matches the role's permitted set and landing route is correct; direct-nav to a forbidden route is refused and redirected.

- [x] T032 [US3] Implement `MyCommunitiesEndpoint` (`GET /api/v1/me/communities`) in `HOAManagementCompany/Features/Board/MyCommunitiesEndpoint.cs` — returns only communities where the caller holds an active membership, summary fields `{ id, communityName, role, status }`, with `limit`(default 25/max 100)/`offset` pagination returning `{ items, total, limit, offset }` (contracts/board-access.md, constitution §4)
- [x] T033 [US3] Backend test for `/me/communities` in `HOAManagementCompany.Tests/Integration/Board/MyCommunitiesEndpointTests.cs` (active-only, summary shape, cross-community exception scope, and a `limit`/`offset` pagination boundary case)
- [x] T034 [US3] Create `BoardNavigationService` in `neko-hoa/src/app/core/services/board-navigation.service.ts` — derives the grouped nav set from decoded claims (research.md R6, client-side) using the **union** of the user's active roles in the active community (generalizing the wireframe's single-`role` `boardNav()` to a role set; Clarifications 2026-08-23), applies the "My Communities" rule (rendered iff >1 active community membership; FR-025) and renders role-forbidden entries visible-but-disabled with a lock affordance (FR-040/FR-027)
- [x] T035 [P] [US3] Karma test for nav derivation at 0, 1, and 2+ communities, for the disabled/lock rule, and for the **multi-role union** case — a user holding two roles in one community sees the union of both roles' permitted sections (Clarifications 2026-08-23) — in `neko-hoa/src/app/core/services/board-navigation.service.spec.ts`
- [x] T036 [US3] Create `board.guard.ts` in `neko-hoa/src/app/core/guards/` — refuses a route the user's role cannot use and redirects to a permitted page (FR-028), and add board `board/*` routes to `neko-hoa/src/app/app.routes.ts`
- [x] T037 [US3] Refactor `shell.component` (`neko-hoa/src/app/shell/`) — replace the fixed `navGroups` array with the `BoardNavigationService` signal so sibling specs add sections as data (FR-024); land single-community users on Community Home (FR-026), multi-community on the My Communities list
- [x] T038 [P] [US3] Create `community-home` placeholder component in `neko-hoa/src/app/features/board/community-home/` (real content ships in spec 2) as the single-community landing route
- [ ] T039 [P] [US3] Angular Testing Library test for the shell rendering the correct nav per role in `neko-hoa/src/app/shell/shell.component.spec.ts`
- [ ] T040 [P] [US3] Playwright test for role-gated route refusal + redirect
- [ ] T041 [P] [US3] Cypress E2E: sign-in → enter board mode → land on Community Home under `neko-hoa/` e2e
- [ ] T042 [P] [US3] Storybook story for the board shell (visual regression)

**Checkpoint**: The shell contract is settled — sibling specs 2-6 can render into it.

---

## Phase 6: User Story 5 — Grant and edit board membership (Priority: P1)

**Goal**: A community manager can create, edit, and end a `CommunityMembership` in-product; changes take effect on the member's next request without re-auth.

**Independent Test**: As a manager, grant a board membership to a resident → they can enter board mode for that community; set the end date to the past → they lose access on their next request. Non-managers are refused.

- [x] T043 [US5] Implement `MembershipsListEndpoint` (`GET /api/v1/communities/{communityId}/memberships`) in `HOAManagementCompany/Features/Board/MembershipsListEndpoint.cs` — `ICommunityScopeResolver.CanAccessAsync(..., ManageMemberships)` (Community Manager only), `limit`(25/max 100)/`offset` pagination, `{ items, total, limit, offset }` (contracts/board-access.md)
- [x] T044 [US5] Implement `MembershipCreateEndpoint` (`POST .../memberships`) in `HOAManagementCompany/Features/Board/MembershipCreateEndpoint.cs` — `{ userId, role(≠Resident), startDate, endDate? }`, resolver-gated, `201` with created membership; `403 FORBIDDEN` for non-managed community (Scenario 2), `422 VALIDATION_ERROR` for unknown userId / invalid role / endDate<startDate; emit a security-sensitive Serilog event (actor, community, target user, role, UTC) for the grant (FR-042, §7)
- [x] T045 [US5] Implement `MembershipUpdateEndpoint` (`PATCH .../memberships/{membershipId}`) in `HOAManagementCompany/Features/Board/MembershipUpdateEndpoint.cs` — partial `{ role?, status?, endDate? }`, `200`; `404 NOT_FOUND` when the membership does not belong to `communityId`; effect on member's next request (FR-023); reject an edit that would remove or downgrade a community's last active Community Manager (FR-042, §3); emit a security-sensitive Serilog event for the edit/end (FR-042, §7)
- [x] T046 [US5] Backend test suite in `HOAManagementCompany.Tests/Integration/Board/MembershipAdminEndpointsTests.cs` — create makes membership active immediately (Scenario 1), non-manager create/list/patch → 403 (Scenarios 2,5), edit end-date-to-past revokes on next request (Scenario 4), role/end-date edit takes effect next request with no re-auth (Scenario 3); assert create/edit/end each emit the sensitive Serilog event with all fields (FR-042, §7); and a denial test — ending or downgrading the last active Community Manager is refused (FR-042, §3)
- [x] T047 [US5] Create `membership-admin` frontend component in `neko-hoa/src/app/features/board/membership-admin/` — list + create/edit form (assign user, role, status, term), reachable only via the manager-gated nav entry
- [x] T048 [P] [US5] Component test for the membership-admin surface (create/edit/end happy path + manager-gating) in `neko-hoa/src/app/features/board/membership-admin/`

**Checkpoint**: A manager can provision board access in-product with no seed/import — the only real path into the feature.

---

## Phase 7: User Story 4 — Read a metric and understand what it means (Priority: P2)

**Goal**: The registry-driven metric presentation contract — every metric surface renders from one descriptor collection, the help affordance is the right-most column, and clicking it opens a glossary panel scrolled to that term. Ships with an empty registry (spec 2 registers concrete metrics).

**Independent Test**: Render any metric surface from a seeded registry → click a row's help link → the glossary panel opens positioned at the matching definition; adding/removing a descriptor changes only the collection.

- [x] T049 [US4] Define the `MetricDescriptor` record plus `MetricSurface`, `MetricEmphasis`, `MetricValue`, `MetricContext` types (data-model.md) in `HOAManagementCompany/Features/Board/MetricDescriptor.cs`
- [x] T050 [US4] Add the registry mechanism — `IEnumerable<MetricDescriptor>` resolved from DI (FR-030), empty by default (no concrete descriptors), registered in `Program.cs`
- [x] T051 [US4] Implement `BoardMetricsEndpoint` (`GET /api/v1/board/metrics?communityId=&surface=`) in `HOAManagementCompany/Features/Board/BoardMetricsEndpoint.cs` — requires an active membership in `communityId` (any role), filters descriptors per `RequiredCapability` via the resolver (FR-035), resolves each value and renders an explicit unavailable state for a single failing descriptor without blanking siblings (FR-036); `limit`(default 25/max 100)/`offset` pagination returning `{ items, total, limit, offset }` (constitution §4); empty registry → empty `items`
- [x] T052 [US4] Backend test in `HOAManagementCompany.Tests/Integration/Board/BoardMetricsEndpointTests.cs` — empty registry → empty items (not error), per-descriptor capability filtering, one failing descriptor does not blank the rest, `403` when no membership, and a `limit`/`offset` pagination boundary case
- [x] T053 [P] [US4] Create the generic metric-table component in `neko-hoa/src/app/features/board/metrics/` — data-driven from descriptors, help affordance as the right-most column of every row (FR-032); explicit empty state and per-row unavailable state
- [x] T054 [US4] Create the glossary side panel in `neko-hoa/src/app/features/board/metrics/` — content derived from the same descriptors (FR-034); opening from a row scrolls to and visually distinguishes that entry, moves focus to it, and returns focus on close (FR-033, Accessibility)
- [x] T055 [P] [US4] Component tests for the metric table (help column position, empty state, add/remove descriptor changes only data) and glossary panel (targeted-open + focus management) under `neko-hoa/src/app/features/board/metrics/`
- [ ] T056 [P] [US4] Storybook stories for the metric table, hero statistics, and glossary panel (visual regression)

**Checkpoint**: Adding/retiring a metric is a one-collection change — spec 2 can register concrete metrics with no layout work.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, accessibility verification, spec reconciliation, and full-suite green.

- [ ] T057 [P] Add Repowise marker regions to the new files (`Community.cs`, `CommunityMembership.cs`, `Features/Board/*`) following the existing `domain=entities` / feature-scoped convention (plan.md → Repowise Documentation)
- [ ] T058 [P] Verify WCAG 2.1 AA contrast and keyboard operability for all new controls — mode toggle, board banner (dark ink on violet), sidebar, help affordance, glossary panel (FR-038, SC-008)
- [ ] T059 [P] Reconcile specs 001 and 003 — annotate spec 001's "board members see only their own properties" as scoped to *resident mode* now that board mode widens the model (spec.md → Executable & living spec)
- [ ] T060 Run `quickstart.md` end-to-end and confirm the demonstrable slice (sign in → enter board mode → scoped Community Home) works
- [ ] T061 Run the full backend (`dotnet test`) and frontend (`npm run test:ci`) suites and confirm existing resident dashboard/payments/property tests pass unmodified (SC-006), and static-analysis check that no board endpoint implements its own scope check (SC-003); confirm the existing auth rate-limit policy covers `POST /api/v1/auth/board-mode` (§7)

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → **Phase 2 (Foundational)** must complete before any user story.
- **Foundational blocks everything**: entities + migration (T007–T015) and the resolver (T016–T018) are prerequisites for all of US1–US5.
- **User story order** (by priority): US1 → US2 → US3 → US5 → US4. Once Foundational is done, US1/US2/US3/US5/US4 are largely independent and can be worked in parallel by separate contributors; US2 verifies the resolver primitive, US3 settles the shell contract the sibling specs render into.
- **Polish (Phase 8)** runs after the stories it documents/verifies are complete.
- **Within a story**, `[P]` tasks touch different files and may run concurrently; non-`[P]` tasks in the same story are sequential (shared files or ordering).

## Parallel Execution Examples

- **Foundational enums**: T003, T004, T005, T006 in parallel.
- **US1 UI + tests**: T022, T023 (components) then T025, T026, T027 in parallel after T024 wires them.
- **US3 tests**: T035, T039, T040, T041, T042 in parallel once the service/shell/guard land.
- **Cross-story parallelism**: after the Foundational checkpoint, one contributor takes US1, another US3, another US5, another US4.

## Implementation Strategy

- **MVP = US1** (Phase 3) on top of Foundational: a board member signs in, enters board mode, sees the banner, and returns — the smallest demonstrable board slice.
- **Security-complete increment**: add US2 (resolver proof + audit) and US3 (scoped shell + Community Home landing) — this is the spec's core deliverable and the contract specs 2-6 depend on.
- **Operability increment**: add US5 so memberships are administered in-product (the only real path into the feature).
- **Presentation contract**: add US4 (metric registry + glossary) last as P2 — ships empty for spec 2 to populate.
