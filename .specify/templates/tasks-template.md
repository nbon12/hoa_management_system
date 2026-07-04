---

description: "Task list template for feature implementation"
---

# Tasks: [FEATURE NAME]

**Input**: Design documents from `/specs/[###-feature-name]/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: The examples below include test tasks. Per the constitution, applicable backend
integration tests and frontend unit tests are part of the completion gate; write tests first
where the testing constitution applies.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root
- **Web app**: `backend/src/`, `frontend/src/`
- **Mobile**: `api/src/`, `ios/src/` or `android/src/`
- Paths shown below assume single project - adjust based on plan.md structure

<!-- 
  ============================================================================
  IMPORTANT: The tasks below are SAMPLE TASKS for illustration purposes only.
  
  The /speckit.tasks command MUST replace these with actual tasks based on:
  - User stories from spec.md (with their priorities P1, P2, P3...)
  - Feature requirements from plan.md
  - Entities from data-model.md
  - Endpoints from contracts/
  
  Tasks MUST be organized by user story so each story can be:
  - Implemented independently
  - Tested independently
  - Delivered as an MVP increment
  
  DO NOT keep these sample tasks in the generated tasks.md file.
  ============================================================================
-->

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create project structure per implementation plan
- [ ] T002 Initialize .NET/FastEndpoints backend and Angular frontend dependencies
- [ ] T003 [P] Configure linting and formatting tools
- [ ] T004 [P] Configure Angular unit testing with Jasmine and Karma
- [ ] T005 [P] Configure Angular Testing Library for component tests
- [ ] T006 [P] Configure Playwright for frontend browser tests
- [ ] T007 [P] Configure Cypress for end-to-end tests
- [ ] T008 [P] Configure Storybook for component stories and visual regression testing

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

Examples of foundational tasks (adjust based on your project):

- [ ] T009 Setup strict database migrations framework (PostgreSQL; Neon per env; no manual DB edits)
- [ ] T010 [P] Implement authentication/authorization (in-application ASP.NET Core Identity + JWT bearer; server-side, HOA-scoped)
- [ ] T011 [P] Setup FastEndpoints routing, global exception handler, and consistent error responses
- [ ] T012 Create base models/entities that all stories depend on (shared tables; multi-HOA membership)
- [ ] T013 Configure Docker/Docker Compose for local parity (Postgres service for dev/tests; MinIO if file storage is used)
- [ ] T014 Setup environment configuration (Dev/Staging/Prod + Cloud Run / Cloudflare settings; Swagger disabled in production)
- [ ] T015 Define tenant-boundary conventions (`hoa_id` or `association_id`) for HOA-scoped tables
- [ ] T016 [P] Configure Serilog structured JSON logging, correlation IDs, and health/readiness endpoints
- [ ] T017 [P] Configure secret management and prevent secrets in images/config
- [ ] T018 [P] Configure edge/rate-limit/cache policy defaults for Cloudflare-fronted API routes
- [ ] T019 [P] Configure GitHub Actions Sonar scan required for pull requests
- [ ] T020 [P] Configure GitHub Actions Codecov reporting and 95% relevant-file coverage gate
- [ ] T021 [P] Configure Swashbuckle OpenAPI and Swagger UI at `/swagger` for development only
- [ ] T022 [P] Configure idempotent Cloud Run startup migrations
- [ ] T023 [P] Configure Neon connection limits, pooling, and short-lived DbContext lifetime
- [ ] T024 [P] Configure xUnit and .NET Testcontainers for backend integration tests
- [ ] T025 [P] Configure Sentry in Angular frontend with environment/release tags and sensitive-data filtering
- [ ] T026 [P] Configure Sentry in .NET backend with performance tracing, environment/release tags, and sensitive-data filtering
- [ ] T027 [P] Configure Sentry trace context propagation from frontend requests through backend handling

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - [Title] (Priority: P1) 🎯 MVP

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 1 (OPTIONAL - only if tests requested) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T028 [P] [US1] xUnit Theory contract test for [endpoint] data variations with unique/scoped test data in backend/tests/Contract/[Name]Tests.cs
- [ ] T029 [P] [US1] xUnit Theory/Testcontainers integration test for [user journey], tenant isolation, and authorization data variations with parallel/rerun-safe setup in backend/tests/Integration/[Name]Tests.cs
- [ ] T030 [P] [US1] Angular Testing Library component test for [component] in frontend/src/[path]/[component].spec.ts
- [ ] T031 [P] [US1] Jasmine/Karma unit test for [service/utility] in frontend/src/[path]/[name].spec.ts
- [ ] T032 [P] [US1] Cypress E2E test for [user journey] in frontend/cypress/e2e/[name].cy.ts
- [ ] T033 [P] [US1] Storybook story and visual regression case for [component]

### Implementation for User Story 1

- [ ] T034 [P] [US1] Create [Entity1] model in backend/src/[path]/[Entity1].cs
- [ ] T035 [P] [US1] Create [Entity2] model in backend/src/[path]/[Entity2].cs
- [ ] T036 [US1] Implement [Service] in backend/src/[path]/[Service].cs (depends on T034, T035)
- [ ] T037 [US1] Implement FastEndpoint for [endpoint/feature] in backend/src/[path]/[Endpoint].cs
- [ ] T038 [US1] Add validation, error handling, and consistent response shape
- [ ] T039 [US1] Add Serilog structured logging/audit events for sensitive operations
- [ ] T040 [US1] Verify Swashbuckle documents the endpoint in development Swagger UI
- [ ] T041 [US1] Verify Sentry captures errors/performance spans with trace context and without sensitive payloads

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - [Title] (Priority: P2)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 2 (OPTIONAL - only if tests requested) ⚠️

- [ ] T042 [P] [US2] xUnit Theory contract test for [endpoint] data variations with unique/scoped test data in backend/tests/Contract/[Name]Tests.cs
- [ ] T043 [P] [US2] xUnit Theory/Testcontainers integration test for [user journey] data variations with parallel/rerun-safe setup in backend/tests/Integration/[Name]Tests.cs
- [ ] T044 [P] [US2] Angular Testing Library component test for [component] in frontend/src/[path]/[component].spec.ts
- [ ] T045 [P] [US2] Cypress E2E test for [user journey] in frontend/cypress/e2e/[name].cy.ts

### Implementation for User Story 2

- [ ] T046 [P] [US2] Create [Entity] model in backend/src/[path]/[Entity].cs
- [ ] T047 [US2] Implement [Service] in backend/src/[path]/[Service].cs
- [ ] T048 [US2] Implement FastEndpoint for [endpoint/feature] in backend/src/[path]/[Endpoint].cs
- [ ] T049 [US2] Integrate with User Story 1 components (if needed)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - [Title] (Priority: P3)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 3 (OPTIONAL - only if tests requested) ⚠️

- [ ] T050 [P] [US3] xUnit Theory contract test for [endpoint] data variations with unique/scoped test data in backend/tests/Contract/[Name]Tests.cs
- [ ] T051 [P] [US3] xUnit Theory/Testcontainers integration test for [user journey] data variations with parallel/rerun-safe setup in backend/tests/Integration/[Name]Tests.cs
- [ ] T052 [P] [US3] Angular Testing Library component test for [component] in frontend/src/[path]/[component].spec.ts
- [ ] T053 [P] [US3] Cypress E2E test for [user journey] in frontend/cypress/e2e/[name].cy.ts

### Implementation for User Story 3

- [ ] T054 [P] [US3] Create [Entity] model in backend/src/[path]/[Entity].cs
- [ ] T055 [US3] Implement [Service] in backend/src/[path]/[Service].cs
- [ ] T056 [US3] Implement FastEndpoint for [endpoint/feature] in backend/src/[path]/[Endpoint].cs

**Checkpoint**: All user stories should now be independently functional

---

[Add more user story phases as needed, following the same pattern]

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] TXXX [P] Documentation updates in docs/
- [ ] TXXX Code cleanup and refactoring
- [ ] TXXX Performance optimization across all stories
- [ ] TXXX [P] Additional unit tests (if requested) in tests/unit/
- [ ] TXXX Review backend tests use xUnit Theories for repeated data variations
- [ ] TXXX Review tests for parallel safety, rerun safety, unique data, scoped resources, cleanup, and idempotent setup
- [ ] TXXX Security hardening, rate-limit review, and untrusted-content validation
- [ ] TXXX Sentry review for frontend/backend error capture, performance spans, trace context, release/environment tags, and sensitive-data filtering
- [ ] TXXX Accessibility pass for keyboard access, labels, and validation messages
- [ ] TXXX Verify Jasmine/Karma unit tests, Angular Testing Library component tests, Playwright browser tests, Cypress E2E tests, and Storybook visual regression pass where applicable
- [ ] TXXX API contract review for pagination defaults/max limits, UTC dates, IDs, and cacheability
- [ ] TXXX Migration review for rollback/mitigation and environment seed/reference data
- [ ] TXXX Swagger review: `/swagger` works in development and is disabled in production
- [ ] TXXX Repowise review: PR includes regenerated or confirmed-unchanged Repowise outputs in marker regions; indexed docs match merged code
- [ ] TXXX Neon review: low max connections, pooling enabled, short-lived DbContexts
- [ ] TXXX File storage review: Cloudflare R2 for hosted environments, MinIO for local/tests, Postgres metadata only
- [ ] TXXX Verify Sonar PR scan passes
- [ ] TXXX Verify Codecov reports at least 95% coverage for relevant changed/added files
- [ ] TXXX Confirm pull request scope is a focused vertical slice or justified cross-cutting change
- [ ] TXXX **Before submitting the PR**: bring this feature's `spec.md` AND `tasks.md` up to
      date with the work actually performed, and update any older `spec.md` that drifted from
      the code so it reflects reality. Only `spec.md` must be kept truthful for prior features;
      `tasks.md`/`plan.md`/`research.md` of older, already-merged specs (and this feature's
      `plan.md`/`research.md`) are not required to be refreshed for this gate
- [ ] TXXX Verify the spec stays executable: every mandatory acceptance scenario / functional
      requirement is backed by an automated test that currently passes; no spec claim is left
      unverified
- [ ] TXXX Reconcile cross-spec contradictions: if this feature's tests or acceptance criteria
      directly contradict a former spec, update the superseded spec(s) so the full spec corpus
      is internally consistent and record which spec prevails and why
- [ ] TXXX Run quickstart.md validation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - May integrate with US1 but should be independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - May integrate with US1/US2 but should be independently testable

### Cross-Spec Dependencies

- Per the constitution's Spec Independence & Parallelism principle, this spec MUST NOT
  assume another spec/sub-spec has already been implemented unless that dependency is
  explicitly documented in this spec's `plan.md`. Where this spec was split from a larger
  effort, prefer a split that lets sibling specs proceed in parallel over one that
  sequences them.

### Within Each User Story

- Tests (if included) MUST be written and FAIL before implementation
- Models before services
- Services before endpoints
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel
- Models within a story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together (if tests requested):
Task: "Contract test for [endpoint] in backend/tests/Contract/[Name]Tests.cs"
Task: "Integration test for [user journey] in backend/tests/Integration/[Name]Tests.cs"

# Launch all models for User Story 1 together:
Task: "Create [Entity1] model in backend/src/[path]/[Entity1].cs"
Task: "Create [Entity2] model in backend/src/[path]/[Entity2].cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
