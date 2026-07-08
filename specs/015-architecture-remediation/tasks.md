# Tasks: Architecture Remediation — Proper Target Architecture

**Input**: Design documents from `/specs/015-architecture-remediation/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included and written FIRST (red→green) — the testing constitution applies to every backend slice; each user story's acceptance scenarios map to the test tasks below.

**Organization**: Tasks are grouped by user story (US1–US6 = spec priorities P1–P6) so each story is independently implementable, testable, and mergeable. No schema changes anywhere — no migration tasks.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US6)

## Path Conventions

Existing monorepo layout: backend `HOAManagementCompany/`, backend tests `HOAManagementCompany.Tests/` (+ new `HOAManagementCompany.UnitTests/`), frontend `neko-hoa/`, infra `infra/`, CI `.github/workflows/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: The two new tooling containers several stories depend on.

- [X] T001 Create `HOAManagementCompany.UnitTests/HOAManagementCompany.UnitTests.csproj` (net9.0; references `HOAManagementCompany.csproj`, xUnit, `NetArchTest.Rules`; NO Testcontainers/MinIO/Mvc.Testing), add it to `HOAManagementCompany.sln`, and verify `dotnet test HOAManagementCompany.UnitTests` runs (empty) without Docker
- [X] T002 [P] Add `openapi-typescript` devDependency and a placeholder `generate:api-types` npm script to `neko-hoa/package.json` (wired fully in US4)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared-kernel move that US2 (central error mapping) and US5 (architecture rules) both build on.

**⚠️ CRITICAL**: Complete before starting US2 or US5 (US1, US3, US4, US6 do not depend on it but merging it first avoids churn).

- [X] T003 Move `DomainException` from `HOAManagementCompany/Features/Auth/AuthService.cs:206` to new `HOAManagementCompany/Domain/DomainException.cs` (namespace `HOAManagementCompany.Domain`, identical shape: `Code`, `Message`, `StatusCode`); update all `using HOAManagementCompany.Features.Auth` sites that exist only for it (Features/Property: `OwnerGetEndpoint.cs`, `PropertyEndpoint.cs`, `OwnerPatchEndpoint.cs`, `PropertyService.cs`, `DirectoryFieldPatchEndpoint.cs`; Features/Community: `CommunityService.cs`, `PollService.cs`, `AnnouncementGetEndpoint.cs`, `Events/EventRsvpEndpoint.cs`, `Poll/PollVoteEndpoint.cs`, `Documents/DocumentDownloadEndpoint.cs`; plus Auth/Payments raise sites); `dotnet build && dotnet test` green

**Checkpoint**: Foundation ready — user stories can begin.

---

## Phase 3: User Story 1 — Payment records are always correct, even when processing is interrupted (Priority: P1) 🎯 MVP

**Goal**: Provider-event effects (ledger entries, status, cumulative amounts, receipt) commit atomically and idempotently through one shared `PaymentRecorder`; recomputation serializes with appends; historical inconsistencies are detected report-only. (FR-001–FR-005)

**Independent Test**: Interrupt provider-event processing at every intermediate point, retry via the reconcile path, and verify exactly-once ledger effects with status/ledger agreement (`quickstart.md` → WebhookAtomicityTests).

### Tests for User Story 1 (write FIRST — must fail against current code) ⚠️

- [X] T004 [P] [US1] Interrupt-and-retry integration tests in `HOAManagementCompany.Tests/Integration/Payments/WebhookAtomicityTests.cs`: xUnit Theory over event kinds (succeeded, failed, ACH return, refund, dispute) injecting a fault between ledger write and status save, then re-running via retry; assert exactly one ledger entry per business event and status consistent with ledger (spec US1 scenarios 1–2 — the ACH double-reversal/NSF and duplicate-refund cases MUST be covered and MUST fail red)
- [X] T005 [P] [US1] Idempotent redelivery tests in `HOAManagementCompany.Tests/Integration/Payments/WebhookIdempotencyTests.cs`: re-deliver fully-applied refund and terminal-state events; assert no new entries and unchanged `CumulativeRefundedAmount` (spec US1 scenario 3); PLUS a concurrent-delivery Theory — two parallel `ProcessAsync` invocations for the same inbox event — asserting exactly-once ledger effects (spec Edge Case: two workers pick up the same event; exercises the in-transaction row-locked guard from research R1); Theory-based, factory data, per-test isolation
- [X] T006 [P] [US1] Concurrency test in `HOAManagementCompany.Tests/Integration/Payments/LedgerRecomputeLockTests.cs`: run `RecomputeBalancesAsync` concurrently with `AppendAsync` for one property; assert final `RunningBalance` sequence consistent (spec US1 scenario 4 — fails red without the lock)
- [X] T007 [P] [US1] Consistency-detection test in `HOAManagementCompany.Tests/Integration/Payments/LedgerConsistencyDetectionTests.cs`: seed a transaction whose status disagrees with ledger effects; run detection; assert structured finding logged (Serilog test sink) and no data mutated (FR-005)

### Implementation for User Story 1

- [X] T008 [US1] Implement `PaymentRecorder` in `HOAManagementCompany/Features/Payments/Services/PaymentRecorder.cs`: `RecordSettledPaymentAsync` (txn build/update + `LedgerService.AddPaymentAsync` + `ReceiptFactory` receipt) and `RecordCompensatingAsync` (reversal/fee/refund entries + status + cumulative update), each inside one `IExecutionStrategy` + `BeginTransactionAsync` scope with the idempotency guard reading the txn row inside the same transaction (research R1/R2); register `AddScoped` in `HOAManagementCompany/Program.cs`
- [X] T009 [US1] Refactor `HOAManagementCompany/Features/Payments/OneTime/ConfirmPaymentEndpoint.cs:67-109` to delegate its settle sequence to `PaymentRecorder` (behavior identical, retry receipt-reset preserved)
- [X] T010 [US1] Refactor `HOAManagementCompany/Features/Payments/Recurring/RecurringDraftService.cs:153-198` to delegate to `PaymentRecorder` (per-mandate transaction isolation preserved)
- [X] T011 [US1] Refactor `HOAManagementCompany/Features/Payments/Webhooks/WebhookProcessor.cs`: `SettleSucceededAsync`, `HandleFailedAsync` (ACH return), `HandleRefundAsync`, dispute handler all route their writes through `PaymentRecorder` so guard+effects+status commit atomically; remove the five scattered standalone `SaveChangesAsync` write points; T004/T005 go green
- [X] T012 [US1] Add per-property advisory lock acquisition to `RecomputeBalancesAsync` in `HOAManagementCompany/Features/Payments/Ledger/LedgerService.cs:119-134` (reuse `AcquirePropertyLockAsync`); T006 goes green
- [X] T013 [US1] Add `DetectLedgerInconsistenciesAsync` to `HOAManagementCompany/Features/Payments/Jobs/ReconciliationService.cs` (missing/duplicate ledger effects for terminal states; refund-sum mismatch), emit `LedgerInconsistencyFinding` as Serilog warning + Sentry message (no amounts beyond deltas), and invoke from `HOAManagementCompany/Features/Payments/Jobs/ReconcileEndpoint.cs`; T007 goes green. INCLUDES the FR-005 cutover run: after the US1 slice deploys to Dev/Staging, manually trigger reconcile (quickstart § Ledger consistency report) and record the findings (zero or triaged) in the PR before promoting

**Checkpoint**: US1 fully functional — MVP deliverable; full suite green (SC-001, SC-007).

---

## Phase 4: User Story 2 — Consistent, predictable error responses across the whole API (Priority: P2)

**Goal**: One central `DomainException` → `{ code, message }` mapping; one identity-claim accessor; zero per-endpoint error boilerplate. (FR-006–FR-008; `contracts/error-envelope.md`)

**Independent Test**: Every documented business-error condition returns the uniform envelope through the public API; a missing `propertyId` claim yields 403 `MISSING_CLAIM`, not a 500.

### Tests for User Story 2 (write FIRST) ⚠️

- [X] T014 [P] [US2] Error-contract tests in `HOAManagementCompany.Tests/Integration/Common/ErrorContractTests.cs`: xUnit Theory over documented codes (`EMAIL_TAKEN`, `PROPERTY_ACCESS_DENIED`, `INVALID_CREDENTIALS`, poll double-vote, document access denied, …) asserting envelope shape + intended status via HTTP, including one endpoint with no local error handling (fail-safe scenario — red today for uncaught cases returning 500)
- [X] T015 [P] [US2] Missing-claim tests in `HOAManagementCompany.Tests/Integration/Common/MissingClaimTests.cs`: authenticated request without `propertyId` claim to representative endpoints → 403 with `code=MISSING_CLAIM` (red today: NRE→500)

### Implementation for User Story 2

- [X] T016 [US2] Extend `HOAManagementCompany/Features/Common/GlobalExceptionHandler.cs` with a `DomainException` branch writing `{ code, message }` at `ex.StatusCode` (production leak rules unchanged for non-domain exceptions; Sentry capture preserved)
- [X] T017 [P] [US2] Create `HOAManagementCompany/Features/Common/ClaimsPrincipalExtensions.cs` with `GetPropertyId()`/`GetUserId()` throwing `DomainException("MISSING_CLAIM", …, 403)` when absent/invalid
- [X] T018 [US2] Remove all 12 per-endpoint `catch (DomainException)` blocks (Auth Login/Register/Refresh endpoints, `Features/Property/PropertyEndpoint.cs:19-20` and sibling property endpoints, Community endpoints, Payments endpoints) so the central mapping is the single path; T014 green
- [X] T019 [US2] Replace all 24 inline `Guid.Parse(User.FindFirst("propertyId")!.Value)` sites across 23 files (e.g. `Features/Property/PropertyEndpoint.cs:17`, `Features/Dashboard/DashboardEndpoint.cs:16-17`, Payments/Community/Statements endpoints) with the T017 accessor; T015 green; SC-002 counts = 0

**Checkpoint**: US1 + US2 independently deliverable; error envelope contract live.

---

## Phase 5: User Story 3 — Production cannot run test machinery (Priority: P3)

**Goal**: Environment-level backstop (fail-fast boot validation + endpoint guard) that config flags cannot override; test artifacts out of publish output. (FR-009–FR-010)

**Independent Test**: Boot with `ASPNETCORE_ENVIRONMENT=Production` and all test-support flags set → startup refuses; publish output contains no `testdata/`.

### Tests for User Story 3 (write FIRST) ⚠️

- [ ] T020 [P] [US3] Startup-validation tests in `HOAManagementCompany.Tests/Integration/Configuration/ProductionBackstopValidationTests.cs`: Production + `Startup:SeedData=true` → fail-fast; Production + `DevTools:E2ECleanupEnabled=true` → fail-fast; Dev/Test environments with same flags → boot succeeds; PLUS unset/unknown `ASPNETCORE_ENVIRONMENT` with test-support flags set → boot refused (spec Edge Case: ambiguous environment identity defaults to "test machinery disabled"; pins the existing 008 env-name validation as the backstop) (Theory over environments; red today)
- [ ] T021 [P] [US3] Endpoint backstop test in `HOAManagementCompany.Tests/Integration/DevTools/E2ECleanupBackstopTests.cs`: host environment Production with flag set → 404 and a security-relevant Serilog event recorded; Dev with flag set → still works (defense-in-depth path, distinct from T020's boot rejection)

### Implementation for User Story 3

- [ ] T022 [US3] Extend the startup options validation in `HOAManagementCompany/Infrastructure/Configuration/` (e.g. `HostEnvironmentValidator`/startup options validator per research R8) with environment-aware rules rejecting `Startup:SeedData` and `DevTools:E2ECleanupEnabled` in `Production`; T020 green
- [ ] T023 [US3] Add `IHostEnvironment.IsProduction()` guard to `HOAManagementCompany/Features/DevTools/E2ECleanupEndpoint.cs` returning 404 and logging a Serilog security event when invoked with the flag set in Production (keep config-flag gate for non-prod; update the explanatory comment); T021 green
- [ ] T024 [P] [US3] Change `HOAManagementCompany/HOAManagementCompany.csproj:65` to `<Content Include="testdata\**\*" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="Never" />` and verify `dotnet publish -c Release` output contains no `testdata/` (embedded fallback in `Infrastructure/Storage/TestDataFiles.cs` already covers runtime)

**Checkpoint**: Production backstop live and boot-tested (SC-003).

---

## Phase 6: User Story 4 — One source of truth for the client–server contract (Priority: P4)

**Goal**: Angular contract types generated from the OpenAPI document, drift gated in CI, dead/duplicate types removed. (FR-011–FR-012; `contracts/client-type-generation.md`)

**Independent Test**: `npm run generate:api-types` reproduces the committed file byte-identical; a deliberate backend shape change makes the gate fail; grep finds one definition per contract concept.

### Implementation for User Story 4

- [ ] T025 [US4] Implement `--export-openapi <path>` startup flag (pattern of the existing `--seed`) in `HOAManagementCompany/Program.cs` + `HOAManagementCompany/Infrastructure/Configuration/StartupTasks.cs`: generate the NSwag OpenAPI document, write JSON, exit without binding HTTP or touching the database; add a startup test in `HOAManagementCompany.Tests/Startup/` asserting the flag produces a valid non-empty document
- [ ] T026 [US4] Implement `neko-hoa/scripts/generate-api-types.mjs` + finalize `generate:api-types` npm script (run backend export → `openapi-typescript` → `neko-hoa/src/app/core/api/generated-types.ts`); generate and commit the file; add companion doc `neko-hoa/src/app/core/api/README.md` describing the pipeline + drift gate (hosts the Repowise `section=contract` marker region — the generated file itself stays marker-free)
- [ ] T027 [US4] Re-point consumers: type `neko-hoa/src/app/core/services/payments.service.ts` request/response interfaces (the 14 local definitions incl. `RecurringInfo`, `DraftRow`) and the other five services with generated types (thin re-exports allowed via `core/models`); keep hand-written mappers (types-only per Clarifications)
- [ ] T028 [US4] Shrink `neko-hoa/src/app/core/models/index.ts` to app-internal view-models + re-exports; delete dead types `RecurringPayment`, `DraftEntry`, `ISODate`, `LedgerEntryType`; update the 18 importer files; `npm run test:ci` green
- [ ] T029 [P] [US4] Update Storybook fixtures in `neko-hoa/src/app/features/payments/{one-time,recurring,statement}.stories.ts` to type stubs with canonical generated types (no parallel hand-written shapes; FR-012)
- [ ] T030 [US4] Add the CI drift gate (build backend → export → codegen → `git diff --exit-code neko-hoa/src/app/core/api/generated-types.ts`) to the PR-verification pipeline: if US6's T040 split is already underway or merged, implement the gate directly in `.github/workflows/ci.yml` and skip touching `test.yml`; otherwise add it to the frontend job in `.github/workflows/test.yml` and T040 migrates it

**Checkpoint**: Contract drift structurally impossible without CI failure (SC-004).

---

## Phase 7: User Story 5 — The codebase's layers match the intended architecture (Priority: P5)

**Goal**: Dependency arrows enforced by architecture tests; provider SDK confined; single-definition policies; dead artifacts removed. (FR-013–FR-016, FR-021)

**Independent Test**: `dotnet test HOAManagementCompany.UnitTests` — layering rules pass; SC-005 counts all zero.

### Tests for User Story 5 (write FIRST — red on today's violations) ⚠️

- [ ] T031 [P] [US5] Architecture tests in `HOAManagementCompany.UnitTests/Architecture/LayeringTests.cs` (NetArchTest): (a) `Infrastructure.*` ↛ `Features.*` (red: 8 files); (b) `Domain.*` ↛ `Features.*|Infrastructure.*`; (c) `Stripe` namespace only from `Infrastructure.Payments` (red: 3 files); (d) no cross-feature internal imports outside the allow-list (`Features.Common`, `Domain`)
- [ ] T032 [P] [US5] Gateway event-mapping unit tests in `HOAManagementCompany.UnitTests/Payments/ProviderEventMappingTests.cs`: Theory over the kind-mapping table in `contracts/provider-event-model.md` exercising `StripeEventTranslator` (the coverable pure translator from T034 — constructing `Stripe.*` POCOs in tests is fine; the arch rule confines the namespace within the app assembly only), plus `WebhookProcessor` handler tests against hand-built `PaymentProviderEvent` values (no Stripe SDK objects)

### Implementation for User Story 5

- [ ] T033 [US5] Move options classes (`StripeOptions`, `PaymentsOptions`, `JobsOptions`, `TwilioOptions`, `SendGridOptions`) from `HOAManagementCompany/Features/Payments/PaymentOptions.cs` to `HOAManagementCompany/Infrastructure/Configuration/PaymentOptions.cs`; update the 8 Infrastructure importers (5 validators, `StripeGateway.cs`, `TwilioSmsProvider.cs`, `SendGridEmailProvider.cs`) and feature/Program.cs usings; LayeringTests rule (a) green
- [ ] T034 [US5] Create `PaymentProviderEvent` + `PaymentProviderEventKind` in `HOAManagementCompany/Features/Payments/Webhooks/PaymentProviderEvent.cs`; change `IStripeGateway.ConstructEvent` → `ParseEvent(json, signature): PaymentProviderEvent` in `HOAManagementCompany/Infrastructure/Payments/IStripeGateway.cs`; move the `Stripe.Event`/`PaymentIntent`/`Charge`/`Dispute` destructuring and `/100m` conversions into a NEW coverable pure translator `HOAManagementCompany/Infrastructure/Payments/StripeEventTranslator.cs` (NOT `[ExcludeFromCodeCoverage]` — the exclusion stays only on `StripeGateway`'s SDK-I/O methods, so the contract-mapping logic counts toward the 95% gate), called by `StripeGateway.ParseEvent` after signature verification; drop `using Stripe;` from `WebhookProcessor.cs`, `StripeWebhookEndpoint.cs`, `Jobs/ReconciliationService.cs`; update the in-memory test fake; T032 + rule (c) green
- [ ] T035 [P] [US5] Create `HOAManagementCompany/Domain/Payments/MoneyPolicy.cs` (`ToCents`/`FromCents` with `MidpointRounding.AwayFromZero`, `Currency = "usd"`); replace duplicated conversions/constants at `Features/Payments/OneTime/CreateIntentEndpoint.cs:35,46`, `Features/Payments/Recurring/RecurringDraftService.cs:134,140`, `Features/Payments/Services/FeeCalculator.cs:58`, and the gateway `/100m` sites (FR-015); unit tests in `HOAManagementCompany.UnitTests/Payments/MoneyPolicyTests.cs`
- [ ] T036 [P] [US5] Extract the duplicated scheduler-secret check (`Features/Payments/Jobs/RunDraftsEndpoint.cs:35-43` ≡ `ReconcileEndpoint.cs:28-36`) into a shared helper/pre-processor in `HOAManagementCompany/Features/Payments/Jobs/SchedulerAuth.cs` (constant-time compare preserved)
- [ ] T037 [P] [US5] Delete dead artifacts: root `/Dockerfile` (nothing builds it) and `HOAManagementCompany/Components/` (misfiled `.code-workspace` moved to repo root or deleted); verify `docker-compose.yaml` and workflows still reference `HOAManagementCompany/Dockerfile` only (FR-016)

**Checkpoint**: Architecture rules enforced and green in the container-free tier (SC-005).

---

## Phase 8: User Story 6 — Fast, layered feedback and separated delivery pipeline (Priority: P6)

**Goal**: Container-free unit tier; CI/CD split with composite actions; shared IaC core; payment components decomposed with central error presentation. (FR-017–FR-020)

**Independent Test**: `dotnet test HOAManagementCompany.UnitTests` succeeds with Docker unavailable in <60 s; `ci.yml`/`release.yml` independently runnable; both tofu roots validate through the shared module; decomposed components keep Cypress/Playwright green.

### Implementation for User Story 6

- [ ] T038 [US6] Move `HOAManagementCompany.Tests/Unit/**` (88 facts: FeeCalculator, AllocationService, ReceiptFactory, PaymentConfigService, 9 validator suites) into `HOAManagementCompany.UnitTests/`, preserving namespaces per folder convention; remove the now-empty `Unit/` tree; both projects green (FR-017)
- [ ] T039 [P] [US6] Add per-domain `PaymentFactory` to `HOAManagementCompany.Tests/Factories/` (valid `PaymentTransaction`/`LedgerEntry`/`RecurringPayment` construction, no business rules per testing constitution §2.3); convert the US1 test files (T004–T007) to factories instead of `TestDataSeeder` magic IDs; seeder itself stays (bounded scope per research R10)
- [ ] T040 [US6] Split `.github/workflows/test.yml` into `.github/workflows/ci.yml` (PR verification: backend build/test/Sonar/Codecov + frontend unit/build/Storybook/Cypress + T030 drift gate + Trivy scan-only) and `.github/workflows/release.yml` (push-to-main: docker build/push, deploy-dev, health gate, E2E/smoke, traffic promote, failure webhook); retire `test.yml`; all existing gates preserved (FR-018)
- [ ] T041 [P] [US6] Extract composite actions `.github/actions/dotnet-setup/`, `.github/actions/node-setup/`, `.github/actions/gcloud-auth/` and adopt them across the 9 workflows; make `infra-plan.yml`/`infra-apply.yml` use the existing `pr-env-tofu-init` composite and delete the dead commented block at `infra-apply.yml:87-93`
- [ ] T042 [US6] Extract `infra/modules/cloud-run-service/` shared core (Cloud Run service, secrets wiring, Neon branch/database provisioning; variables for name prefixing, TTL labels, scaling, branch-vs-database source per research R12); refactor `infra/modules/environment/` and `infra/modules/pr-environment/` into thin wrappers; `tofu init -backend=false && tofu validate` green for `infra/environments/{dev,staging,pr}` (FR-019)
- [ ] T043 [US6] Add `neko-hoa/src/app/core/interceptors/error.interceptor.ts` normalizing the `{ code, message }` envelope into a typed `ApiError`, registered in `neko-hoa/src/app/app.config.ts` beside the auth interceptor; add shared error presentation component in `neko-hoa/src/app/shared/`; AND establish the shared API-access convention (FR-020's third clause): a `core/api/api-client.ts` helper owning the base address (`environment.apiBaseUrl`) and typed request plumbing, adopted by all six services in `neko-hoa/src/app/core/services/` (removes the 6× hand-rolled `private base = …` + URL construction; hand-written mappers stay per Clarifications); Jasmine specs for the interceptor and helper (FR-020)
- [ ] T044 [US6] Decompose `neko-hoa/src/app/features/payments/one-time/one-time.component.ts` (341 lines, 41 inline styles): extract `StripeElementsHostComponent` (shared, `neko-hoa/src/app/shared/stripe-elements-host/`), a signal-based wizard store, and split template/styles into `.html`/`.scss`; replace the 12 hand-rolled error sites with the central mechanism; component tests via Angular Testing Library; Cypress payments E2E stays green
- [ ] T045 [US6] Decompose `neko-hoa/src/app/features/payments/recurring/recurring.component.ts` (329 lines, 25 inline styles, 17 error sites) the same way, reusing `StripeElementsHostComponent` and the shared error presentation; component tests; Cypress green

**Checkpoint**: All six stories independently functional (SC-006).

---

## Phase 9: Polish & Cross-Cutting Concerns

- [ ] T046 [P] Update `docs/architecture-smells-analysis.md` with a remediation-status column mapping each finding to its landed fix (report stays truthful)
- [ ] T047 [P] Verify Sentry capture end-to-end after the error-mapping change: business errors logged with code, unexpected errors reported with trace context and env/release tags, no sensitive payment data (spec Constitution Requirements → Observability)
- [ ] T048 [P] Accessibility pass on decomposed payment screens and the shared error component: keyboard access, labels, validation messages (WCAG 2.1 AA behavior preserved)
- [ ] T049 Review new/changed tests for Theory usage, parallel safety, rerun safety, unique data, and factory discipline (testing constitution §2.1/2.3/2.5)
- [ ] T050 Verify Sonar PR scan and Codecov ≥95% coverage on relevant changed files for each slice's PR (`PaymentRecorder`, `GlobalExceptionHandler`, `ClaimsPrincipalExtensions`, backstop validators are hotspot-class)
- [ ] T051 Repowise: regenerate/confirm marker regions in the six files listed in plan.md (`PaymentRecorder.cs`, `WebhookProcessor.cs`, `DomainException.cs`, `GlobalExceptionHandler.cs`, `LayeringTests.cs`, and `neko-hoa/src/app/core/api/README.md` — the companion doc; `generated-types.ts` itself stays marker-free because it is byte-exact codegen output guarded by the drift gate) so indexed docs match merged code
- [ ] T052 **Before each slice's PR**: bring this feature's `spec.md` AND `tasks.md` up to date with work actually performed; update any older `spec.md` that drifted (spec 006 payments flows must still read truthfully after the `PaymentRecorder`/neutral-event refactor — reconcile and record which spec prevails if any contradiction emerges)
- [ ] T053 Verify the spec stays executable: every mandatory acceptance scenario (US1–US6) is traceable to a currently-passing automated test (T004–T007, T014–T015, T020–T021, drift gate, T031–T032, tier/pipeline checks); no unverified spec claims
- [ ] T054 Run `specs/015-architecture-remediation/quickstart.md` end-to-end as final validation (container-free tier timing, backstop boot check, drift gate, tofu validate)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none — start immediately. T001 blocks T031/T032/T035/T038; T002 blocks T026.
- **Foundational (Phase 2)**: T003 blocks US2 (T016–T019) and US5 rule (b) going green; other stories unaffected.
- **User Stories (Phases 3–8)**: independent of each other except the explicit seams below; deliver sequentially P1→P6 or in parallel.
- **Polish (Phase 9)**: T046–T054 after the stories they audit; T052/T053 gate every slice's PR.

### Cross-story seams (only ones that exist)

- **US5 T034 touches `WebhookProcessor.cs` after US1 T011 refactors it** — land US1 first (priority order already ensures this); if parallelized, coordinate the file.
- **US4 T030 adds the drift gate to `test.yml`; US6 T040 splits that workflow** — T040 migrates the gate into `ci.yml`.
- **US6 T039 retrofits factories into US1's test files** — requires T004–T007 merged.
- **US6 T043–T045 consume the US2 error envelope** — envelope contract must be live (P2 merged) for the interceptor's typed `ApiError`.

### Within Each User Story

- Tests first (red) → implementation (green) → checkpoint with full suite passing (SC-007 requires green after every slice).

### Parallel Opportunities

- Phase 1: T001 ∥ T002.
- US1 tests T004–T007 all [P]; then T008 → T009/T010/T011 (T009–T011 parallelizable after T008), T012 ∥ T013.
- US2: T014 ∥ T015; T016 ∥ T017; then T018 → T019.
- US3: T020 ∥ T021; T022/T023 sequential-ish, T024 [P] anytime.
- US5: T031 ∥ T032 first; T033, T035, T036, T037 mutually [P]; T034 after T032.
- US6: T038 ∥ T039 ∥ T041; T042 independent; T043 → T044 ∥ T045.
- Different stories can be staffed in parallel after Phase 2, respecting the four seams above.

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together (red first):
Task: "WebhookAtomicityTests.cs — interrupt-and-retry Theories per event kind"
Task: "WebhookIdempotencyTests.cs — redelivery no-op Theories"
Task: "LedgerRecomputeLockTests.cs — recompute vs append race"
Task: "LedgerConsistencyDetectionTests.cs — report-only detection"

# After T008 (PaymentRecorder) lands, parallelize the three call-site refactors:
Task: "ConfirmPaymentEndpoint.cs → delegate to PaymentRecorder"
Task: "RecurringDraftService.cs → delegate to PaymentRecorder"
Task: "WebhookProcessor.cs → atomic handlers via PaymentRecorder"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 (T001–T002) + Phase 2 (T003)
2. Phase 3 complete (T004–T013), full suite green
3. **STOP and VALIDATE**: interrupt-and-retry harness passes; SC-001 met; deploy — homeowner financial correctness is secured before any structural work

### Incremental Delivery

Each story = one focused PR (or a small PR train), independently mergeable with the suite green: P1 (correctness) → P2 (error contract) → P3 (backstop) → P4 (contract types) → P5 (architecture rules) → P6 (delivery structure). Cross-cutting P2/P5 PRs are justified per constitution §12 (documented in plan.md Complexity Tracking).

### Notes

- No migrations, no schema changes anywhere — any task that seems to need one is off-plan.
- Behavior preservation is the default; the only deliberate user-visible changes are the error envelope (US2) and the production backstop (US3).
- Commit after each task or logical group; stop at any checkpoint to validate the story independently.
