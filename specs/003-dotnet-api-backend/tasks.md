# Tasks: Implement .NET Backend for NekoHOA API

**Input**: Design documents from `/specs/003-dotnet-api-backend/`  
**Branch**: `003-dotnet-api-backend` | **Date**: 2026-05-24  
**Prerequisites**: plan.md ✅ · spec.md ✅ · research.md ✅ · data-model.md ✅ · contracts/ ✅ · quickstart.md ✅

**Tests**: The spec's quality gates explicitly require integration tests (happy path, validation failure, and auth failure for every endpoint handler). Tests are written **first** — ensure they fail before implementing the corresponding endpoint/service.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story?] Description — file path`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks in this phase)
- **[Story]**: Which user story this task belongs to (US1–US6)
- Tests listed first within each story (write first, fail first, then implement)

---

## Phase 1: Setup (Project Scaffold)

**Purpose**: Replace the existing Blazor project with a clean `dotnet new webapi` scaffold, install all packages, and wire up infrastructure services.

- [ ] T001 Delete all files inside `HOAManagementCompany/` except `.csproj` and `Migrations/`; scaffold a fresh `dotnet new webapi` layout (`Program.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`) — `HOAManagementCompany/`
- [ ] T002 Replace `HOAManagementCompany/HOAManagementCompany.csproj` with updated package references: `FastEndpoints`, `FastEndpoints.Swagger`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `AWSSDK.S3`, `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Sentry.AspNetCore` — `HOAManagementCompany/HOAManagementCompany.csproj`
- [ ] T003 [P] Update `HOAManagementCompany.Tests/HOAManagementCompany.Tests.csproj` with `xUnit`, `Testcontainers`, `Testcontainers.PostgreSql`, `Testcontainers.Minio`, `Microsoft.AspNetCore.Mvc.Testing` packages — `HOAManagementCompany.Tests/HOAManagementCompany.Tests.csproj`
- [ ] T004 [P] Update `docker-compose.yaml` to add `postgres:17` service (port 5432, db/user/password: `nekohoa`) and `minio/minio:latest` service (ports 9000/9001, credentials: `minioadmin/minioadmin`) — `docker-compose.yaml`
- [ ] T005 [P] Update `neko-hoa/api/openapi.yaml` to add `refreshToken` field to `AuthResponse`, add `accountNumber` to `RegisterRequest`, add `properties` array to `CurrentUser`, and add `POST /auth/refresh` + `POST /auth/switch-property` endpoint definitions per `contracts/auth-contract-additions.md` — `neko-hoa/api/openapi.yaml`
- [ ] T085 [P] Add multi-stage `Dockerfile` to the API project: `mcr.microsoft.com/dotnet/sdk:9.0` build stage → `mcr.microsoft.com/dotnet/aspnet:9.0` runtime stage; expose port 8080; set `ASPNETCORE_ENVIRONMENT=Production` default — `HOAManagementCompany/Dockerfile`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: All shared domain entities, DbContext, migrations, middleware, and test infrastructure must be in place before any user story can be implemented.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T006 Create all Domain enum files: `LedgerEntryType`, `ViolationStatus`, `ViolationCategory`, `RecurringAmountType`, `PaymentMethod`, `DraftStatus`, `AnnouncementCategory`, `EventCategory`, `DocumentCategory` — `HOAManagementCompany/Domain/Enums/`
- [ ] T007 [P] Create `ApplicationUser` entity (extends `IdentityUser`, adds `FirstName`, `LastName`) — `HOAManagementCompany/Domain/Entities/ApplicationUser.cs`
- [ ] T008 [P] Create `UserProperty` entity (UserId FK, PropertyId FK, LinkedAt; unique index on UserId+PropertyId) — `HOAManagementCompany/Domain/Entities/UserProperty.cs`
- [ ] T009 [P] Create `RefreshToken` entity (UserId FK, TokenHash, ExpiresAt, CreatedAt, RevokedAt; index on TokenHash) — `HOAManagementCompany/Domain/Entities/RefreshToken.cs`
- [ ] T010 [P] Create `Property` entity (AccountNumber unique, CommunityId, address fields, assessment fields, FinanceChargeRate) — `HOAManagementCompany/Domain/Entities/Property.cs`
- [ ] T011 [P] Create `Owner` entity (PropertyId FK 1:1, contact fields, notification preference booleans, MailingAddress) — `HOAManagementCompany/Domain/Entities/Owner.cs`
- [ ] T012 [P] Create `AddressHistory`, `DirectoryField`, `LedgerEntry`, `RecurringPayment`, `DraftEntry` entities per data-model.md — `HOAManagementCompany/Domain/Entities/`
- [ ] T013 [P] Create `Announcement`, `Poll`, `PollOption`, `PollVote`, `Violation`, `CalendarEvent`, `EventRsvp`, `HoaDocument`, `CommunityExpense` entities per data-model.md — `HOAManagementCompany/Domain/Entities/`
- [ ] T014 Create `ApplicationDbContext` extending `IdentityDbContext<ApplicationUser>`, registering all entity sets with EF Core fluent configuration (indexes, unique constraints, cascade rules) — `HOAManagementCompany/Infrastructure/Persistence/ApplicationDbContext.cs`
- [ ] T015 Create `DesignTimeDbContextFactory` for EF Core CLI tooling — `HOAManagementCompany/Infrastructure/Persistence/DesignTimeDbContextFactory.cs`
- [ ] T016 Generate initial EF Core migration `20260524_InitialSchema` and verify it produces SQL for all tables; apply to local dev database — `HOAManagementCompany/Infrastructure/Persistence/Migrations/`
- [ ] T017 Implement `IDocumentStorage` interface and `S3DocumentStorage` class (AWSSDK.S3 against MinIO/R2 with path-style flag, pre-signed URL with 5-min expiry) — `HOAManagementCompany/Infrastructure/Storage/`
- [ ] T018 Implement global exception handler mapping unhandled exceptions to `{ code, message }` JSON (no stack trace in production; verbose in development) — `HOAManagementCompany/Features/Common/GlobalExceptionHandler.cs`
- [ ] T019 Write `Program.cs`: register FastEndpoints (`RoutePrefix = "api/v1"`), JWT Bearer auth, ASP.NET Core Identity (`UserManager` only), EF Core (`AddDbContext` + `AddDbContextFactory`), `IDocumentStorage`, Serilog + `app.UseSerilogRequestLogging()` with correlation ID enrichment, Sentry, rate limiter (10 req/min auth, 20 req/min payments), CORS (`localhost:4200`), health checks at `/health`, Swashbuckle (dev only at `/swagger`), and global exception handler — **does not include `--seed` flag logic** (that is T068) — `HOAManagementCompany/Program.cs`
- [ ] T020 Set `appsettings.json` (production defaults, no secrets) and `appsettings.Development.json` (ConnectionStrings, Jwt, Storage/MinIO, Sentry DSN placeholder) — `HOAManagementCompany/appsettings.json` + `HOAManagementCompany/appsettings.Development.json`
- [ ] T021 [P] Create `TestDatabaseFixture` (xUnit `IAsyncLifetime`) spinning up `PostgreSqlContainer` + `MinioContainer`, applying migrations, exposing `DbContext` and `MinioEndpoint` — `HOAManagementCompany.Tests/Fixtures/TestDatabaseFixture.cs`
- [ ] T022 [P] Create `IntegrationTestBase` with `BeginIsolatedAsync()` / `RollbackAsync()` transaction helpers and `HttpClient` factory wired to `WebApplicationFactory` — `HOAManagementCompany.Tests/Fixtures/IntegrationTestBase.cs`
- [ ] T023 [P] Create test data factories for `UserFactory`, `PropertyFactory`, `OwnerFactory` — `HOAManagementCompany.Tests/Factories/`
- [ ] T024 [P] Create `.github/workflows/repowise.yml` Repowise PR documentation workflow — `.github/workflows/repowise.yml`
- [ ] T025 [P] Add Repowise marker region placeholders to `README.md` (project overview, tech stack, quick-start summary sections) — `README.md`
- [ ] T086 [P] Create `.github/workflows/test.yml` CI workflow: checkout, `dotnet restore`, `dotnet build --no-restore`, `dotnet test --collect:"XPlat Code Coverage"`, publish coverage to Codecov (`codecov/codecov-action`), fail if Codecov threshold < 80% on changed files — `.github/workflows/test.yml`
- [ ] T087 [P] Configure SonarQube scan in `test.yml` CI: add `SonarScanner.MSBuild` begin/end steps with `sonar.projectKey`, `sonar.host.url`, `SONAR_TOKEN` secret; enforce zero new blocker/critical issues gate — `.github/workflows/test.yml`

**Checkpoint**: All entities, DbContext, migrations, middleware, test fixtures, and GitHub workflows in place — user story implementation can begin.

---

## Phase 3: User Story 1 — Resident Authentication (Priority: P1) 🎯 MVP

**Goal**: Register, login, logout, token refresh, profile retrieval, and property switching. All other stories depend on this.

**Independent Test**: `POST /auth/register` → `POST /auth/login` → `GET /auth/me` → `POST /auth/logout` — verify JWT pair issued, profile returned, refresh token revoked on logout, subsequent calls rejected.

### Tests — write first, must fail before implementing

- [ ] T026 [P] [US1] Write xUnit Theory integration tests for `POST /auth/register`: happy path (201 + token pair), EMAIL_TAKEN (409), ACCOUNT_NOT_FOUND (422), ACCOUNT_ALREADY_CLAIMED (422), weak password (422) — `HOAManagementCompany.Tests/Integration/Auth/RegisterTests.cs`
- [ ] T027 [P] [US1] Write xUnit Theory integration tests for `POST /auth/login` + `POST /auth/logout` + `GET /auth/me`: valid credentials (200 + tokens), INVALID_CREDENTIALS (401), missing token (401), logout invalidates refresh token (204) — `HOAManagementCompany.Tests/Integration/Auth/LoginLogoutTests.cs`
- [ ] T028 [P] [US1] Write xUnit Theory integration tests for `POST /auth/refresh` + `POST /auth/switch-property`: valid refresh (200 + rotated pair), INVALID_REFRESH_TOKEN (401), PROPERTY_ACCESS_DENIED (403), switch to unlinked property (403) — `HOAManagementCompany.Tests/Integration/Auth/RefreshSwitchTests.cs`

### Implementation

- [ ] T029 [US1] Implement `AuthService`: `RegisterAsync` (hash password via `UserManager`, create `UserProperty`, issue token pair), `LoginAsync` (verify credentials, issue token pair), `LogoutAsync` (delete refresh token), `RefreshAsync` (validate hash, rotate token), `SwitchPropertyAsync` (verify `UserProperty` link, re-issue token), `CreateTokenPairAsync` (build JWT with `propertyId`/`communityId` claims, generate + hash refresh token) — `HOAManagementCompany/Features/Auth/AuthService.cs`
- [ ] T030 [P] [US1] Implement `RegisterEndpoint` + `RegisterValidator` (email format, password ≥8 chars, accountNumber required) — `HOAManagementCompany/Features/Auth/RegisterEndpoint.cs`
- [ ] T031 [P] [US1] Implement `LoginEndpoint` + `LoginValidator` — `HOAManagementCompany/Features/Auth/LoginEndpoint.cs`
- [ ] T032 [P] [US1] Implement `LogoutEndpoint` (requires Bearer auth; deletes refresh token from DB) — `HOAManagementCompany/Features/Auth/LogoutEndpoint.cs`
- [ ] T033 [P] [US1] Implement `MeEndpoint` (returns `CurrentUser` with linked `properties` array) — `HOAManagementCompany/Features/Auth/MeEndpoint.cs`
- [ ] T034 [P] [US1] Implement `RefreshEndpoint` + `RefreshValidator` (refreshToken required) — `HOAManagementCompany/Features/Auth/RefreshEndpoint.cs`
- [ ] T035 [P] [US1] Implement `SwitchPropertyEndpoint` + `SwitchPropertyValidator` (propertyId required, UUID format) — `HOAManagementCompany/Features/Auth/SwitchPropertyEndpoint.cs`
- [ ] T036 [US1] Add Serilog audit log events for login, registration, logout, and failed login attempts (never log passwords or tokens) — `HOAManagementCompany/Features/Auth/AuthService.cs`

**Checkpoint**: Auth round-trip fully functional. All other user stories can now be implemented.

---

## Phase 4: User Story 2 — Resident Dashboard (Priority: P2)

**Goal**: Single aggregated `GET /dashboard` response covering balance, violations, documents, events, announcement, and expense chart data.

**Independent Test**: Seed representative data for one resident; call `GET /dashboard`; verify all required fields (`currentBalance`, `openViolations`, `pinnedAnnouncement`, `thisWeekEvents`, `recentActivity`, `communityExpenses`) are present and correctly populated.

### Tests — write first, must fail before implementing

- [ ] T037 [P] [US2] Write xUnit Theory integration tests for `GET /dashboard`: full payload with seeded data, missing pinned announcement (null), no events this week (empty array), unauthenticated (401) — `HOAManagementCompany.Tests/Integration/Dashboard/DashboardTests.cs`

### Implementation

- [ ] T038 [US2] Implement `DashboardService`: query `currentBalance` (latest `LedgerEntry.RunningBalance`), `balanceDueDate` (next occurrence of `Property.AssessmentDueDay` from today: current month if day has not yet passed, next month otherwise, formatted `YYYY-MM-DD`), `openViolations` count, `documentCount` + `newDocumentsThisMonth`, `pinnedAnnouncement` (first pinned for community), `thisWeekEvents` + `nextEvent`, `recentActivity` (5 most recent ledger entries), `communityExpenses` — all scoped by `propertyId`/`communityId` JWT claims — `HOAManagementCompany/Features/Dashboard/DashboardService.cs`
- [ ] T039 [US2] Implement `DashboardEndpoint` (GET, requires Bearer auth, operationId `GetDashboard`) — `HOAManagementCompany/Features/Dashboard/DashboardEndpoint.cs`

**Checkpoint**: Dashboard endpoint returns correct aggregated data for seeded residents.

---

## Phase 5: User Story 3 — Payment Management (Priority: P2)

**Goal**: Paginated + filtered ledger, one-time ACH/card payments, recurring payment CRUD, and draft history.

**Independent Test**: Authenticate as seeded resident → `GET /payments/ledger` (paginated, filtered) → `POST /payments/one-time` (ACH then card) → `PUT /payments/recurring` → `DELETE /payments/recurring` → `GET /payments/drafts`.

### Tests — write first, must fail before implementing

- [ ] T040 [P] [US3] Write xUnit Theory integration tests for `GET /payments/ledger`: no-filter (paginated), date range filter, type filter, search filter, page validation (page=0 → 422), pageSize > 200 → 422, unauthenticated (401) — `HOAManagementCompany.Tests/Integration/Payments/LedgerTests.cs`
- [ ] T041 [P] [US3] Write xUnit Theory integration tests for `POST /payments/one-time`: ACH happy path (200 + confirmationNumber), card happy path ($1.95 fee added server-side), missing routingNumber (422), missing cardNumber (422), amount ≤ 0 (422), method enum invalid (422) — `HOAManagementCompany.Tests/Integration/Payments/OneTimePaymentTests.cs`
- [ ] T042 [P] [US3] Write xUnit Theory integration tests for `GET|PUT|DELETE /payments/recurring` + `GET /payments/drafts`: upsert happy path, fixedAmount missing when amountType=fixed (422), draftDay > 28 (422), soft-cancel (204, record retained), drafts returned up to 12 months — `HOAManagementCompany.Tests/Integration/Payments/RecurringDraftTests.cs`

### Implementation

- [ ] T043 [US3] Implement `PaymentService`: `GetLedgerAsync` (paginated, filtered, full-text search on description+documentNumber), `SubmitOneTimePaymentAsync` (simulate ACH/card, add $1.95 card fee server-side, return confirmation), `GetRecurringAsync`, `UpsertRecurringAsync` (mask sensitive fields before persist), `CancelRecurringAsync` (set inactive), `GetDraftsAsync` (12-month window) — `HOAManagementCompany/Features/Payments/PaymentService.cs`
- [ ] T044 [P] [US3] Implement `LedgerEndpoint` + `LedgerValidator` (page ≥1, pageSize 1–200, date format, enum type) — `HOAManagementCompany/Features/Payments/LedgerEndpoint.cs`
- [ ] T045 [P] [US3] Implement `OneTimePaymentEndpoint` + `OneTimePaymentValidator` (ACH: routingNumber 9 digits, accountType enum; card: cardExpiry MM/YY format; amount > 0) — `HOAManagementCompany/Features/Payments/OneTimePaymentEndpoint.cs`
- [ ] T046 [P] [US3] Implement `RecurringGetEndpoint`, `RecurringUpsertEndpoint` + `RecurringValidator` (draftDay 1–28, fixedAmount required when amountType=fixed), `RecurringDeleteEndpoint` (soft-cancel) — `HOAManagementCompany/Features/Payments/Recurring/`
- [ ] T047 [P] [US3] Implement `DraftsEndpoint` — `HOAManagementCompany/Features/Payments/DraftsEndpoint.cs`
- [ ] T048 [US3] Add rate limiter policy `"payments"` to `OneTimePaymentEndpoint` and `RecurringUpsertEndpoint`; verify card numbers and routing numbers are never logged — `HOAManagementCompany/Features/Payments/`

**Checkpoint**: Full payment workflow functional; ACH/card validated; $1.95 fee enforced server-side; recurring config persisted.

---

## Phase 6: User Story 4 — Property and Owner Information (Priority: P3)

**Goal**: Property record retrieval, owner contact CRUD, mailing address history, and directory field visibility toggling.

**Independent Test**: Authenticate → `GET /property` → `GET /property/owner` → `PATCH /property/owner` → `GET /property/address-history` → `GET /property/directory-fields` → `PATCH /property/directory-fields/phone` → verify all return correct data; `PATCH /property/directory-fields/unknown` → 404.

### Tests — write first, must fail before implementing

- [ ] T049 [P] [US4] Write xUnit Theory integration tests for `GET /property` + `GET|PATCH /property/owner`: happy path, invalid email in PATCH (422), address history appended on mailing address change, unauthenticated (401) — `HOAManagementCompany.Tests/Integration/Property/PropertyOwnerTests.cs`
- [ ] T050 [P] [US4] Write xUnit Theory integration tests for `GET /property/address-history`, `GET /property/directory-fields`, `PATCH /property/directory-fields/{key}`: happy paths, unknown key → 404, cross-resident isolation → 403 — `HOAManagementCompany.Tests/Integration/Property/DirectoryAddressTests.cs`

### Implementation

- [ ] T051 [US4] Implement `PropertyService`: `GetPropertyAsync`, `GetOwnerAsync`, `PatchOwnerAsync` (partial update; append `AddressHistory` row when mailing address changes), `GetAddressHistoryAsync`, `GetDirectoryFieldsAsync`, `PatchDirectoryFieldAsync` (404 on unknown key) — `HOAManagementCompany/Features/Property/PropertyService.cs`
- [ ] T052 [P] [US4] Implement `PropertyEndpoint` — `HOAManagementCompany/Features/Property/PropertyEndpoint.cs`
- [ ] T053 [P] [US4] Implement `OwnerGetEndpoint` + `OwnerPatchEndpoint` + `OwnerPatchValidator` (email format if supplied) — `HOAManagementCompany/Features/Property/`
- [ ] T054 [P] [US4] Implement `AddressHistoryEndpoint` — `HOAManagementCompany/Features/Property/AddressHistoryEndpoint.cs`
- [ ] T055 [P] [US4] Implement `DirectoryFieldsEndpoint` + `DirectoryFieldPatchEndpoint` + `DirectoryFieldValidator` (returns 404 for unknown key) — `HOAManagementCompany/Features/Property/`

**Checkpoint**: Property and owner info fully functional; address history appended correctly; directory fields toggle correctly.

---

## Phase 7: User Story 5 — Community Features (Priority: P3)

**Goal**: Announcements, active poll with voting, violations, calendar events with RSVP, and HOA documents with pre-signed download URLs.

**Independent Test**: Authenticate → `GET /community/announcements` (filtered) → `GET /community/announcements/{id}` → `GET /community/poll` → `POST /community/poll/{id}/vote` (then 409 on re-vote) → `GET /community/violations` → `GET /community/events` → `POST /community/events/{id}/rsvp` → `GET /community/documents` → `GET /community/documents/{id}/download` (resolve pre-signed URL).

### Tests — write first, must fail before implementing

- [ ] T056 [P] [US5] Write xUnit Theory integration tests for announcements: list (reverse chron, pagination), category filter, pinned filter, single by id (200), unknown id (404), unauthenticated (401) — `HOAManagementCompany.Tests/Integration/Community/AnnouncementTests.cs`
- [ ] T057 [P] [US5] Write xUnit Theory integration tests for poll: active poll returned (200), no active poll (204), vote recorded + percentages recalculated (200), duplicate vote (409), invalid optionIndex (422) — `HOAManagementCompany.Tests/Integration/Community/PollTests.cs`
- [ ] T058 [P] [US5] Write xUnit Theory integration tests for violations + events + RSVP: violations scoped to property, status/category filters, events sorted by date, date range filter, RSVP recorded (204), unknown event (404) — `HOAManagementCompany.Tests/Integration/Community/ViolationsEventsTests.cs`
- [ ] T059 [P] [US5] Write xUnit integration tests for documents: list (pinned-first), category/search filter, download URL returned with valid expiresAt, unknown id (404) — `HOAManagementCompany.Tests/Integration/Community/DocumentTests.cs`

### Implementation

- [ ] T060 [US5] Implement `CommunityService`: `GetAnnouncementsAsync` (paginated, category/pinned filter), `GetAnnouncementAsync` (404 on unknown), `GetViolationsAsync` (property-scoped, status/category filter), `GetEventsAsync` (community-scoped, date range/category filter), `RsvpEventAsync` (upsert EventRsvp, 404 on unknown event), `GetDocumentsAsync` (pinned-first, category/search filter), `GetDocumentDownloadUrlAsync` (pre-signed URL via `IDocumentStorage`, 404 on unknown) — `HOAManagementCompany/Features/Community/CommunityService.cs`
- [ ] T061 [US5] Implement `PollService`: `GetActivePollAsync` (204 if none), `VoteAsync` (record `PollVote`, recalculate `PollOption.Percentage` + `Poll.TotalVotes`, 409 on duplicate) — `HOAManagementCompany/Features/Community/PollService.cs`
- [ ] T062 [P] [US5] Implement `AnnouncementsListEndpoint` + `AnnouncementGetEndpoint` — `HOAManagementCompany/Features/Community/Announcements/`
- [ ] T063 [P] [US5] Implement `PollGetEndpoint` + `PollVoteEndpoint` + `PollVoteValidator` (optionIndex ≥ 0) — `HOAManagementCompany/Features/Community/Poll/`
- [ ] T064 [P] [US5] Implement `ViolationsEndpoint` (property-scoped, status + category filters, pagination) — `HOAManagementCompany/Features/Community/Violations/ViolationsEndpoint.cs`
- [ ] T065 [P] [US5] Implement `EventsEndpoint` + `EventRsvpEndpoint` — `HOAManagementCompany/Features/Community/Events/`
- [ ] T066 [P] [US5] Implement `DocumentsEndpoint` + `DocumentDownloadEndpoint` (calls `IDocumentStorage.GetPreSignedUrlAsync`, returns `{ url, expiresAt }`) — `HOAManagementCompany/Features/Community/Documents/`

**Checkpoint**: All 30 API endpoints implemented and returning correct data.

---

## Phase 8: User Story 6 — Local Development Seed Data (Priority: P1)

**Goal**: `dotnet run -- --seed` populates a fresh database with realistic data for all domains, uploads placeholder files to MinIO, and is idempotent and development-restricted.

**Independent Test**: Run `dotnet run -- --seed` against fresh DB → login as `resident@nekohoa.dev` / `Password1!` → call representative endpoint from each domain → verify meaningful non-empty responses; run seed again → no duplicate records created; run with `ASPNETCORE_ENVIRONMENT=Production` → error returned.

### Tests — write first, must fail before implementing

- [ ] T067 [US6] Write integration test for `DatabaseSeeder`: verifies seed completes on fresh DB, all test accounts login, `GET /dashboard` returns non-empty data, `GET /payments/ledger` returns ≥12 entries, `GET /community/poll` returns active poll; re-run is idempotent; running with non-Development env returns error — `HOAManagementCompany.Tests/Integration/Seed/SeederTests.cs`

### Implementation

- [ ] T068 [US6] **Amend** `Program.cs` (skeleton written in T019) to add the `--seed` CLI flag guard: if flag present + env is Development, build host, resolve `DatabaseSeeder`, call `SeedAsync()`, exit 0; if flag present + env is not Development, write error to `Console.Error`, exit 1 — this task only adds these lines; all other middleware registrations remain from T019 — `HOAManagementCompany/Program.cs`
- [ ] T069 [US6] Implement `DatabaseSeeder` orchestrator: applies migrations, checks for existing seed marker (`resident@nekohoa.dev`), calls sub-seeders in dependency order, handles errors — `HOAManagementCompany/Seed/DatabaseSeeder.cs`
- [ ] T070 [P] [US6] Implement `AuthSeeder`: create `resident@nekohoa.dev` + `resident2@nekohoa.dev` via `UserManager`, create `Property` records for `SAKURA` community, create `UserProperty` links — `HOAManagementCompany/Seed/AuthSeeder.cs`
- [ ] T071 [US6] Implement `PropertySeeder`: create `Owner`, `AddressHistory` (created + 1 change event), and 4 `DirectoryField` records (`name`, `email`, `phone`, `address`) for primary resident — depends on `AuthSeeder` (T070) completing first to obtain the seeded `ApplicationUser.Id` — `HOAManagementCompany/Seed/PropertySeeder.cs`
- [ ] T072 [P] [US6] Implement `PaymentSeeder`: create 14 `LedgerEntry` rows (12 RegularAssessment + 2 Payment + 1 LateFee + 1 FinanceCharge), 1 `RecurringPayment` (ACH, active), and `DraftEntry` rows with paid/scheduled/failed statuses — `HOAManagementCompany/Seed/PaymentSeeder.cs`
- [ ] T073 [P] [US6] Implement `CommunitySeeder`: create 5+ `Announcement` rows (all 4 categories, 1 pinned), 1 active `Poll` + 3 `PollOption` rows + `PollVote` rows (non-zero totals), 4+ `Violation` rows (multiple categories, open + closed), 7 `CalendarEvent` rows (past/current week/future, 1 RSVP-enabled), 4 `CommunityExpense` rows — `HOAManagementCompany/Seed/CommunitySeeder.cs`
- [ ] T074 [P] [US6] Implement `StorageSeeder`: for each `HoaDocument` created, upload a small placeholder text file to MinIO bucket (`hoa-documents`) using `S3DocumentStorage`; create 5+ `HoaDocument` rows (all 5 categories, 1 pinned) — `HOAManagementCompany/Seed/StorageSeeder.cs`

**Checkpoint**: `dotnet run -- --seed` completes < 30 s, all endpoints return meaningful data, re-run is clean.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [ ] T075 Verify all 30 endpoints appear in Swagger UI at `http://localhost:5000/swagger` with correct `operationId` values matching `openapi.yaml`; confirm Swagger is disabled when `ASPNETCORE_ENVIRONMENT=Production`
- [ ] T076 [P] Security audit: confirm card numbers, CVV, and routing numbers never appear in Serilog output or Sentry payloads; verify payment endpoints return `429` when rate limit exceeded
- [ ] T077 [P] Sentry review: verify `TracesSampleRate` set; environment/release tags populated; request body data for payment fields stripped before send in `BeforeSend` callback — `HOAManagementCompany/Program.cs`
- [ ] T078 [P] Cross-resident isolation audit: write regression tests verifying `resident2@nekohoa.dev` cannot access `resident@nekohoa.dev` property, ledger, violations, or owner data — `HOAManagementCompany.Tests/Integration/Auth/TenantIsolationTests.cs`
- [ ] T079 [P] Review all xUnit tests use `[Theory]` with `[InlineData]`/`[MemberData]` for data-varied cases (validation, pagination boundaries, auth failure combinations); no copy-paste `[Fact]` repetitions for the same behavior
- [ ] T080 [P] Add Repowise marker regions to `HOAManagementCompany/Program.cs`, `HOAManagementCompany/Domain/Entities/ApplicationUser.cs`, and `HOAManagementCompany/Infrastructure/Persistence/ApplicationDbContext.cs` — per plan.md Repowise section
- [ ] T081 [P] Run `quickstart.md` end-to-end validation: `docker compose up -d` → `dotnet run -- --seed` → `dotnet run` → curl auth/dashboard/ledger/download verification script
- [ ] T082 Verify GitHub Actions `test.yml` passes end-to-end: all xUnit integration tests green, Testcontainers spin up without error — `.github/workflows/test.yml`
- [ ] T083 [P] Verify SonarQube scan passes with zero new blocker/critical issues and Codecov reports ≥ 95% line coverage on changed files — no code changes expected; fix any violations surfaced — `.github/workflows/test.yml`
- [ ] T084 [P] Run Repowise GitHub Actions workflow (`repowise.yml`), review generated marker region outputs, commit refreshed documentation back to the branch — `.github/workflows/repowise.yml`
- [ ] T088 [P] Write xUnit performance smoke test: `GET /api/v1/dashboard` and `GET /api/v1/payments/ledger` must each complete in < 200 ms (p50) over 20 sequential requests against Testcontainers DB with seeded data — `HOAManagementCompany.Tests/Performance/DashboardPerformanceTests.cs`
- [ ] T089 [P] Add Docker Hub image push job to `test.yml`: on merge to `main`, build Dockerfile (T085), tag `nekohoa/api:latest` + `nekohoa/api:{git-sha}`, push using `DOCKER_HUB_USERNAME` / `DOCKER_HUB_TOKEN` secrets — `.github/workflows/test.yml`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user stories**
- **Phase 3 (US1 Auth)**: Depends on Phase 2 — no user story dependencies
- **Phase 4 (US2 Dashboard)**: Depends on Phase 2 + Phase 3 (needs valid Bearer token to test)
- **Phase 5 (US3 Payments)**: Depends on Phase 2 + Phase 3 — can run in parallel with Phase 4
- **Phase 6 (US4 Property)**: Depends on Phase 2 + Phase 3 — can run in parallel with Phases 4–5
- **Phase 7 (US5 Community)**: Depends on Phase 2 + Phase 3 — can run in parallel with Phases 4–6
- **Phase 8 (US6 Seeder)**: Depends on Phases 3–7 being complete (seeder covers all domains)
- **Phase 9 (Polish)**: Depends on all prior phases

### Parallel Opportunities (once Foundational complete)

- **Phases 4, 5, 6, 7** can be worked in parallel by different developers; each is independently testable after Phase 3.
- Within Phase 3: T030–T035 (endpoint files) can all be worked simultaneously after T029 (AuthService).
- Within Phase 5: T044–T047 can be worked simultaneously after T043 (PaymentService).
- Within Phase 6: T052–T055 can be worked simultaneously after T051 (PropertyService).
- Within Phase 7: T062–T066 can be worked simultaneously after T060+T061 (services).
- Within Phase 8: T070–T074 (sub-seeders) can be worked simultaneously after T069 (orchestrator).

```bash
# Example: once Phase 3 (Auth) is done, launch these in parallel:
Task: "Dashboard service + endpoint"     # Phase 4
Task: "Payment service + endpoints"      # Phase 5
Task: "Property service + endpoints"     # Phase 6
Task: "Community service + endpoints"    # Phase 7
```

---

## Implementation Strategy

### MVP (Phase 1 + 2 + 3 only)

1. Complete Phase 1: Scaffold
2. Complete Phase 2: Foundational (CRITICAL — blocks everything)
3. Complete Phase 3: Auth endpoints
4. **STOP and VALIDATE**: register → login → /me → logout works end-to-end
5. Frontend can now be wired to real auth endpoints

### Incremental Delivery

| After Phase | What works |
|-------------|-----------|
| 3 (Auth) | Full auth flow; all other endpoints return 401 (correct) |
| 4 (Dashboard) | Frontend home screen loads from real API |
| 5 (Payments) | Ledger, one-time payments, recurring config |
| 6 (Property) | Property info, owner edits, directory |
| 7 (Community) | Announcements, poll, violations, events, documents |
| 8 (Seeder) | Developer onboarding: fresh DB → seeded → all endpoints testable |

---

## Notes

- `[P]` = different files, safe to start in parallel once phase prerequisites are met
- `[USn]` maps directly to User Story n in `spec.md`
- Test tasks listed first within each story — write tests, confirm they **fail**, then implement
- Commit after each logical group (one endpoint + its test = one commit)
- Stop at each phase checkpoint to validate the story independently before moving forward
- Sensitive fields: card numbers, CVV, routing numbers must never appear in logs or Sentry events — this is enforced by T076 but should be coded correctly from T043/T045
