# Tasks: Login Dashboard with Violations Summary

**Input**: Design documents from `/specs/001-dashboard-violations-summary/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Path conventions**: Backend = `HOAManagementCompany/` at repo root; Frontend = `frontend/` at repository root (Angular app).

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, Angular app in `frontend/`, and structure required for the feature.

- [x] T001 Create Angular application in repository root directory `frontend/` (e.g. `ng new frontend` at repo root or scaffold so `frontend/` contains the Angular app and build artifacts)
- [x] T002 [P] Configure Angular app in `frontend/` for API base URL and auth (e.g. environment or config pointing at backend; ensure `frontend/` is at repo root)
- [x] T003 [P] Add `frontend/` to solution or repo build/docs so it is the canonical frontend directory at repo root per project convention
- [x] T004 Verify backend `HOAManagementCompany` runs and existing auth (Identity/Okta) is configured for post-login redirect target

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data model and APIs that all user stories depend on. No user story work can begin until this phase is complete.

- [x] T005 Create Property entity with Id, OwnerUserId (FK to Identity), DisplayName (and optional audit fields) in `HOAManagementCompany/Models/Property.cs`
- [x] T006 Add PropertyId (required FK to Property) to Violation model in `HOAManagementCompany/Models/Violation.cs`
- [x] T007 Register Property in `HOAManagementCompany/EntityFramework/ApplicationDbContext.cs` (DbSet, relationships, query filters if auditable)
- [x] T008 Add EF Core migration for Property table and Violation.PropertyId in `HOAManagementCompany/Migrations/`
- [x] T009 Implement DashboardService (or extend existing service) to return dashboard summary (openViolationCount for current user's properties, placeholders for other boxes) in `HOAManagementCompany/Services/DashboardService.cs`
- [x] T010 Implement GET dashboard summary endpoint (e.g. `GET /api/dashboard/summary`) scoped to current user in `HOAManagementCompany/Controllers/DashboardController.cs` (or equivalent)
- [x] T011 Add method in ViolationService (or dedicated service) to get open violations count for current user's properties in `HOAManagementCompany/Services/ViolationService.cs`
- [x] T012 Add method to get paginated open violations for current user's properties (limit/offset) in `HOAManagementCompany/Services/ViolationService.cs`
- [x] T013 Implement GET My Violations endpoint (e.g. `GET /api/violations/mine?limit=&offset=`) with totalCount and items, scoped to current user in `HOAManagementCompany/Controllers/` (e.g. ViolationsController or new controller)
- [x] T014 Ensure all dashboard and violations endpoints require authentication and use current user id only (no user id in path/query); document or add integration test for FR-010 data isolation

**Checkpoint**: Foundation ready — dashboard summary and my-violations APIs exist and are scoped by current user; Property and Violation.PropertyId in place.

---

## Phase 3: User Story 1 – View Dashboard After Login (Priority: P1) — MVP

**Goal**: After login, user lands on Dashboard with four summary boxes in a 2×2 grid (Current Balance, Violations, Work Orders, Architecture Requests). Dashboard is the default post-login destination.

**Independent Test**: Log in and confirm the dashboard is displayed with four visible summary boxes and is the default destination after login.

- [x] T015 [US1] Add dashboard route and component in `frontend/src/app/` (e.g. `frontend/src/app/pages/dashboard/` or equivalent) that renders a 2×2 grid with four boxes labeled Current Balance, Violations, Work Orders, Architecture Requests
- [x] T016 [US1] Configure post-login redirect to Dashboard (default landing page) in `frontend/` (e.g. auth callback or app routing in `frontend/src/app/`)
- [x] T017 [US1] Call GET dashboard summary from Angular dashboard component and display four boxes (Violations box may show 0 or placeholder until US2); ensure layout matches FR-002 (top row: Current Balance, Violations; bottom row: Work Orders, Architecture Requests)
- [x] T018 [US1] Ensure dashboard route is protected (auth required) and only accessible after login in `frontend/src/app/`

**Checkpoint**: User Story 1 complete — login leads to Dashboard with four boxes; independently testable.

---

## Phase 4: User Story 2 – See My Violation Count and Open My Violations (Priority: P1)

**Goal**: Violations box shows open violation count; loading state while fetching; count is clickable and navigates to My Violations page; My Violations page lists open violations for the user's properties (paginated).

**Independent Test**: Log in, see violation count in box, click count, confirm My Violations page opens with correct list (or empty); confirm loading indicator shows until count is available.

- [x] T019 [US2] In dashboard component in `frontend/`, show loading indicator (spinner or skeleton) in Violations box until dashboard summary response is received; keep box non-clickable until count is loaded (FR-007)
- [x] T020 [US2] Display openViolationCount from dashboard summary in Violations box; when count is zero show 0 or "no violations" (FR-003, FR-006)
- [x] T021 [US2] Make violation count in Violations box clickable and navigate to My Violations page/route in `frontend/src/app/` (FR-004)
- [x] T022 [US2] Add My Violations route and page component in `frontend/src/app/` (e.g. `frontend/src/app/pages/my-violations/`) that calls GET `/api/violations/mine` with limit/offset
- [x] T023 [US2] Render paginated list of open violations on My Violations page (items, totalCount); support limit/offset per contract in `frontend/src/app/`
- [x] T024 [US2] On dashboard summary API failure, show message "Failed to load violation count" in Violations box and leave rest of dashboard usable (FR-008) in `frontend/`
- [x] T025 [US2] Ensure My Violations page is auth-protected and shows only current user's open violations (no cross-user data)

**Checkpoint**: User Story 2 complete — violation count, loading state, click-through, and My Violations list work; independently testable.

---

## Phase 5: User Story 3 – Other Dashboard Boxes (Priority: P2)

**Goal**: Current Balance, Work Orders, and Architecture Requests boxes show placeholder text only and are not linked to any page or action.

**Independent Test**: Confirm all four boxes are present in 2×2 grid, Violations box is the only one with a link; other three show placeholder text only and are not clickable.

- [x] T026 [US3] Set Current Balance, Work Orders, and Architecture Requests boxes to display placeholder text only (no live data) in `frontend/src/app/` dashboard component (FR-009)
- [x] T027 [US3] Ensure Current Balance, Work Orders, and Architecture Requests boxes have no links or click handlers in `frontend/` (FR-009)
- [x] T028 [US3] Verify 2×2 grid order: top row Current Balance, Violations; bottom row Work Orders, Architecture Requests in `frontend/` (FR-002)

**Checkpoint**: User Story 3 complete — placeholder boxes and layout verified.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Error handling, responsiveness, and validation across the feature.

- [x] T029 Add or verify global exception handling returns user-friendly message "Failed to load violation count" for dashboard/summary and violations/mine failures in `HOAManagementCompany/` (FR-008)
- [x] T030 [P] Verify Dashboard and My Violations UI are responsive (iPhone, tablet, desktop) per constitution in `frontend/`
- [x] T031 [P] Run full test suite: `PLAYWRIGHT_HEADLESS=true dotnet test --verbosity normal` from repo root; fix any regressions
- [x] T032 Validate quickstart: build, run backend, run frontend, login → dashboard → My Violations per `specs/001-dashboard-violations-summary/quickstart.md`
- [x] T033 Add unit tests for DashboardService and ViolationService methods (open count, paginated list) and new controller actions in `HOAManagementCompany.Tests/` (per constitution: all functions must have unit tests)
- [x] T034 [P] Add Angular component unit tests for dashboard and my-violations components in `frontend/` (per constitution: component unit tests required)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start with T001 (Angular in `frontend/`).
- **Phase 2 (Foundational)**: Depends on Phase 1 (frontend directory exists; backend unchanged). Blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2 (dashboard summary API, auth). Delivers MVP.
- **Phase 4 (US2)**: Depends on Phase 2 and Phase 3 (dashboard page exists). Can start after T015–T018 if Violations box already exists.
- **Phase 5 (US3)**: Depends on Phase 3/4 (dashboard with four boxes). Placeholder and layout verification.
- **Phase 6 (Polish)**: Depends on Phase 5 complete.

### User Story Dependencies

- **US1 (P1)**: After Foundational — no dependency on US2/US3. Delivers dashboard and post-login redirect.
- **US2 (P1)**: After Foundational and US1 dashboard page — adds violation count, loading, link, and My Violations page.
- **US3 (P2)**: After US1/US2 — ensures other three boxes are placeholders only.

### Parallel Opportunities

- T002, T003 can run in parallel after T001.
- T005, T006 can run in parallel (different files).
- T009–T013 can be parallelized by different developers (different services/controllers).
- T026, T027, T028 (US3) can be done in parallel.
- T030, T031, T034 (Polish) can run in parallel.

---

## Parallel Example: Phase 2 (Foundational)

```text
# After T005–T008 (Property + migration):
T009 DashboardService
T011 ViolationService count method
T012 ViolationService list method

# Then:
T010 DashboardController
T013 ViolationsController (mine)
T014 Auth/data isolation verification
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (Angular in `frontend/`).
2. Complete Phase 2: Foundational (Property, Violation.PropertyId, dashboard summary and my-violations APIs).
3. Complete Phase 3: User Story 1 (Dashboard page, 2×2 grid, post-login redirect).
4. **STOP and VALIDATE**: Log in and confirm dashboard with four boxes is the default destination.
5. Deploy or demo if ready.

### Incremental Delivery

1. Phase 1 + 2 → APIs and data model ready.
2. Phase 3 (US1) → Test independently → MVP (dashboard after login).
3. Phase 4 (US2) → Violation count, loading, My Violations page → Test independently.
4. Phase 5 (US3) → Placeholder boxes verified.
5. Phase 6 → Polish and tests.

### Suggested MVP Scope

- **MVP**: Phase 1 + Phase 2 + Phase 3 (User Story 1). Delivers: Angular app in `frontend/`, dashboard as default after login, four summary boxes in 2×2 grid. Violations box can show 0 or placeholder until US2.

---

## Notes

- Frontend lives in **`frontend/`** at the repository root; Angular setup is T001.
- [P] = parallelizable; [USn] = task belongs to that user story.
- Each user story phase is independently testable per Independent Test in spec.
- Commit after each task or logical group.
- Data isolation (FR-010): all backend queries use current user only; no user id in URLs.
- Existing violations: may be erased before migration; no production DB (early development). See data-model.md.
