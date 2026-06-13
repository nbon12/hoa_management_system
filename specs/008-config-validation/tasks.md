---

description: "Task list for startup configuration validation"
---

# Tasks: Startup Configuration Validation

**Input**: Design documents from `/specs/008-config-validation/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included. The spec explicitly requires automated tests (US3) and the testing
constitution makes them part of the completion gate. Within each story, tests are written
**first** (red → green).

**Organization**: Grouped by user story. This is a cohesive cross-cutting feature, so the
shared validation engine lives in Foundational; per-story phases layer behavior on top.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1–US4 from spec.md

## Path Conventions

Web app: backend `HOAManagementCompany/`, tests `HOAManagementCompany.Tests/`, frontend
`neko-hoa/src/`. Existing project — only config-validation files are listed.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Dependency and folder scaffolding. (Auth0, migrations, Docker, Serilog, Sentry,
CI gates already exist project-wide — not repeated here.)

- [ ] T001 Verify and, if absent, add an explicit `<PackageReference Include="FluentValidation" />` (version-aligned with the one FastEndpoints resolves — confirm via `dotnet list HOAManagementCompany package --include-transitive`) to `HOAManagementCompany/HOAManagementCompany.csproj` (research R2)
- [ ] T002 [P] Create backend folder `HOAManagementCompany/Infrastructure/Configuration/` and test folders `HOAManagementCompany.Tests/Unit/Configuration/` and `HOAManagementCompany.Tests/Integration/Configuration/`
- [ ] T003 [P] Create frontend folder `neko-hoa/src/app/core/config/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The reusable validation engine every backend story builds on.

**⚠️ CRITICAL**: No backend user story (US1–US3) can begin until this phase is complete. US4
(frontend) is independent of this phase.

- [ ] T004 Implement generic `FluentValidateOptions<T> : IValidateOptions<T>` in `HOAManagementCompany/Infrastructure/Configuration/FluentValidateOptions.cs` — resolves `IValidator<T>`, maps failures to `ValidateOptionsResult.Fail(...)`, MUST NOT include raw values in messages (FR-019); wrap the file in a Repowise marker region (`domain=configuration`)
- [ ] T005 Implement `AddValidatedOptions<TOptions, TValidator>(section)` extension in `HOAManagementCompany/Infrastructure/Configuration/OptionsValidationExtensions.cs` — registers `IValidator<TOptions>`, binds the section, adds the adapter, calls `ValidateOnStart()` (research R1, contracts/backend-config-validation.md); add REPOWISE marker region (depends on T004)

**Checkpoint**: Validation engine ready — US1–US3 can proceed.

---

## Phase 3: User Story 1 - Backend refuses to start on invalid configuration (Priority: P1) 🎯 MVP

**Goal**: A misconfigured backend fails to start with a clear `Section:Field` error in the
startup logs, instead of failing later mid-request.

**Independent Test**: Boot the host with a deliberately invalid value (e.g. sample ratio 1.5,
or `Percentage` fee + `AllCards` scope) and confirm startup throws `OptionsValidationException`
naming the field; boot with valid config and confirm it starts.

### Tests for User Story 1 (write first — red) ⚠️

- [ ] T006 [P] [US1] Startup fail-fast integration tests in `HOAManagementCompany.Tests/Integration/Configuration/StartupValidationTests.cs` using `WebApplicationFactory<Program>` + `WithWebHostBuilder(...ConfigureAppConfiguration...)`, asserting `OptionsValidationException` on host start for: out-of-range `Observability:TraceSampleRatio`, missing `Stripe:SecretKey`, `Payments` Percentage+AllCards, missing `Storage` section, and partially-configured `Twilio`/`SendGrid` (FR-014; research R4 — no PostgreSQL needed)

### Implementation for User Story 1

- [ ] T007 [P] [US1] Implement `StripeOptionsValidator` in `HOAManagementCompany/Infrastructure/Configuration/StripeOptionsValidator.cs` — `SecretKey`/`PublishableKey`/`WebhookSigningSecret` non-empty, `WebhookToleranceSeconds > 0` (data-model.md; FR-004, FR-008)
- [ ] T008 [P] [US1] Implement `PaymentsOptionsValidator` in `HOAManagementCompany/Infrastructure/Configuration/PaymentsOptionsValidator.cs` — `CardFeeType ∈ {Flat,Percentage}`, `CardScope ∈ {AllCards,CreditOnly}`, **Percentage ⇒ CreditOnly**, fee values ≥ 0, `VariableNoticeLeadDays ≥ 0`, `ReconcilePendingAchAfterHours > 0` (FR-006, FR-007)
- [ ] T009 [P] [US1] Implement `ObservabilityOptionsValidator` in `HOAManagementCompany/Infrastructure/Configuration/ObservabilityOptionsValidator.cs` — `TraceSampleRatio`/`SentryTraceSampleRatio ∈ [0,1]`, `OtlpProtocol == "http/protobuf"`, `OtlpEndpoint` absolute URI, `TelemetryProxyMaxBodyBytes > 0` (FR-009, FR-010)
- [ ] T010 [P] [US1] Implement `StorageOptionsValidator` in `HOAManagementCompany/Infrastructure/Configuration/StorageOptionsValidator.cs` — `ServiceUrl`/`AccessKey`/`SecretKey`/`BucketName` non-empty, `ServiceUrl` (and `PublicServiceUrl` if set) absolute URI (FR-011)
- [ ] T011 [P] [US1] Implement `JobsOptionsValidator` in `HOAManagementCompany/Infrastructure/Configuration/JobsOptionsValidator.cs` — `SchedulerSharedSecret` non-empty (FR-004)
- [ ] T012 [P] [US1] Implement `TwilioOptionsValidator` in `HOAManagementCompany/Infrastructure/Configuration/TwilioOptionsValidator.cs` — all-empty is valid; if any field set, require `AccountSid` + `FromNumber` + a usable auth pair (ApiKeySid+ApiKeySecret OR AuthToken), mirroring `TwilioOptions.IsConfigured` (FR-012)
- [ ] T013 [P] [US1] Implement `SendGridOptionsValidator` in `HOAManagementCompany/Infrastructure/Configuration/SendGridOptionsValidator.cs` — all-empty is valid; if any field set, require `ApiKey` + valid `FromEmail` (FR-012)
- [ ] T014 [US1] Wire all seven via `AddValidatedOptions<…>(section)` in `HOAManagementCompany/Program.cs`, replacing the plain `Configure<T>` calls; remove the null-forgiving `GetSection("Storage").Get<StorageOptions>()!` and resolve `IOptions<StorageOptions>` for the `IAmazonS3` singleton instead (FR-011; research R7) (depends on T004, T005, T007–T013)
- [ ] T015 [US1] Refresh the existing Repowise `domain=bootstrap` marker region in `HOAManagementCompany/Program.cs` to note that options are now validated at startup
- [ ] T016 [US1] Run `StartupValidationTests` to green; manually confirm a bad value produces a `Section:Field` message and that **no secret value** appears in the output (FR-002, FR-019)

**Checkpoint**: Backend fails fast on invalid config — US1 independently testable.

---

## Phase 4: User Story 2 - Local and CI workflows stay frictionless without real credentials (Priority: P1)

**Goal**: With strict validation in every environment, Development and CI/Test still start
using non-functional placeholder secret values (never real credentials).

**Independent Test**: Boot in Development/Test with placeholder secrets and confirm success;
remove a required secret entirely and confirm it now fails in that same environment.

### Tests for User Story 2 (write first — red) ⚠️

- [ ] T017 [P] [US2] Add a valid-start test to `HOAManagementCompany.Tests/Integration/Configuration/StartupValidationTests.cs` asserting that a Test configuration with placeholder secrets starts successfully (host builds + serves), and a companion case asserting failure when a placeholder is removed (FR-016, SC-003)

### Implementation for User Story 2

- [ ] T018 [P] [US2] Add non-functional placeholder secret values (e.g. `Stripe:SecretKey=sk_test_placeholder`, `Storage:AccessKey/SecretKey`, `Jobs:SchedulerSharedSecret`) to `HOAManagementCompany/appsettings.Development.json`, and document them in `HOAManagementCompany/appsettings.Secrets.json.example` (research R5; never real credentials)
- [ ] T019 [US2] Inject placeholder secrets into the Test host via `ConfigureAppConfiguration` (in-memory collection) in `HOAManagementCompany.Tests/Fixtures/IntegrationTestBase.cs` (or a shared `TestConfig` helper) so every existing integration test continues to boot under strict validation (depends on T014)

**Checkpoint**: Dev + full existing test suite boot cleanly under strict validation.

---

## Phase 5: User Story 3 - Configuration rules are guarded by automated tests (Priority: P2)

**Goal**: Every validation rule — including previously comment-only business rules — is
covered by direct unit tests, locking in the contract.

**Independent Test**: Run the `Unit/Configuration` suite and confirm each validator has
passing and failing cases for every rule.

### Tests for User Story 3 (these ARE the deliverable) ⚠️

- [ ] T020 [P] [US3] `PaymentsOptionsValidatorTests` (xUnit **Theory**) in `HOAManagementCompany.Tests/Unit/Configuration/PaymentsOptionsValidatorTests.cs` — enum values, `Percentage`+`AllCards` rejected, fee values ≥ 0, lead-days/reconcile boundaries (FR-006, FR-007, FR-015)
- [ ] T021 [P] [US3] `StripeOptionsValidatorTests` in `HOAManagementCompany.Tests/Unit/Configuration/StripeOptionsValidatorTests.cs` — required fields present/absent, `WebhookToleranceSeconds` 0/negative/positive (FR-004, FR-008)
- [ ] T022 [P] [US3] `ObservabilityOptionsValidatorTests` (Theory) in `HOAManagementCompany.Tests/Unit/Configuration/ObservabilityOptionsValidatorTests.cs` — ratios `0`, `1` accepted and `-0.01`, `1.01` rejected; protocol equality; endpoint absolute-URI cases (FR-009, FR-010)
- [ ] T023 [P] [US3] `StorageOptionsValidatorTests` in `HOAManagementCompany.Tests/Unit/Configuration/StorageOptionsValidatorTests.cs` — required-field permutations incl. empty section (FR-011)
- [ ] T024 [P] [US3] `JobsOptionsValidatorTests` in `HOAManagementCompany.Tests/Unit/Configuration/JobsOptionsValidatorTests.cs` — secret present/absent (FR-004)
- [ ] T025 [P] [US3] `TwilioSendGridOptionsValidatorTests` (Theory) in `HOAManagementCompany.Tests/Unit/Configuration/TwilioSendGridOptionsValidatorTests.cs` — all-empty valid, partial-config rejected across auth permutations, valid full configs accepted (FR-012; complements existing `TwilioOptionsTests`)
- [ ] T026 [US3] Confirm ≥ 95% coverage on `Infrastructure/Configuration/**` via the Codecov gate; add cases for any uncovered branch (SC-004, constitution coverage gate)

**Checkpoint**: Every documented rule is regression-guarded.

---

## Phase 6: User Story 4 - Frontend fails loudly on missing required configuration (Priority: P3)

**Goal**: A production build missing the publishable key or API base URL halts bootstrap and
renders a full-page error, instead of failing later in the browser.

**Independent Test**: Build with `production: true` and an empty `stripePublishableKey`; confirm
the app does not load and shows a full-page error naming the missing value; with values present,
confirm normal boot.

### Tests for User Story 4 (write first — red) ⚠️

- [ ] T027 [P] [US4] Jasmine/Karma unit tests in `neko-hoa/src/app/core/config/runtime-config.validator.spec.ts` — `findMissingRequiredConfig` returns `[]` for complete production config and any non-production config, and the missing key names when production values are empty; `renderConfigError` injects a perceivable DOM message containing the missing key name(s) (F1–F5)

### Implementation for User Story 4

- [ ] T028 [P] [US4] Implement `findMissingRequiredConfig(cfg)` in `neko-hoa/src/app/core/config/runtime-config.validator.ts` — enforces only when `production === true`; checks `apiBaseUrl` and `stripePublishableKey` (FR-017, FR-018); wrap the file in a Repowise marker region (`domain=configuration`)
- [ ] T029 [P] [US4] Implement `renderConfigError(missing)` in `neko-hoa/src/app/core/config/config-error.render.ts` — injects a minimal static full-page error naming the missing value(s) into the app root
- [ ] T030 [US4] Wire the guard **before** `bootstrapApplication` in `neko-hoa/src/main.ts` — on missing config, call `renderConfigError` and skip bootstrap; otherwise bootstrap normally (research R6; depends on T028, T029)
- [ ] T031 [US4] Run guard tests to green and `npm run lint`; confirm dev build (`environment.development.ts`) is never blocked

**Checkpoint**: Frontend halts loudly on missing production config; dev unaffected.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T032 [P] Repowise: run the project's Repowise workflow; ensure marker regions on all new config files are populated and `repowise` marker validation passes (constitution CI/CD)
- [ ] T033 Run `quickstart.md` validation end-to-end: bad-config boot fails fast with a clear message; clean boot succeeds; frontend prod-missing-key shows full-page error
- [ ] T034 Verify the Sonar PR scan passes and Codecov reports ≥ 95% on changed/added files (constitution)
- [ ] T035 Confirm the PR is a focused cross-cutting slice (config validation only) per constitution governance

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup. BLOCKS US1–US3 (backend). Does NOT block US4.
- **US1 (Phase 3)**: Depends on Foundational.
- **US2 (Phase 4)**: Depends on US1 wiring (T014).
- **US3 (Phase 5)**: Depends on the validators existing (US1 T007–T013); the unit tests target them directly.
- **US4 (Phase 6)**: Independent of the backend — depends only on Setup T003.
- **Polish (Phase 7)**: After all desired stories complete.

### User Story Dependencies

- **US1 (P1)** 🎯 MVP — the core fail-fast behavior; depends only on Foundational.
- **US2 (P1)** — depends on US1 (needs the wired validators to prove placeholders satisfy them).
- **US3 (P2)** — depends on US1 (hardens coverage of the validators US1 created).
- **US4 (P3)** — fully independent; can be built in parallel with the entire backend track.

### Within Each User Story

- Tests first (red) → implementation (green).
- Foundational bridge/extension before any validator wiring.
- Validators (T007–T013) before `Program.cs` wiring (T014).

### Parallel Opportunities

- Setup T002, T003 in parallel.
- All seven validators T007–T013 in parallel (separate files), after Foundational.
- All US3 unit-test files T020–T025 in parallel.
- US4 (T027–T031) runs in parallel with the entire backend track.

---

## Parallel Example: User Story 1

```bash
# After Foundational (T004, T005), launch all seven validators together:
Task: "StripeOptionsValidator in Infrastructure/Configuration/StripeOptionsValidator.cs"
Task: "PaymentsOptionsValidator in Infrastructure/Configuration/PaymentsOptionsValidator.cs"
Task: "ObservabilityOptionsValidator in Infrastructure/Configuration/ObservabilityOptionsValidator.cs"
Task: "StorageOptionsValidator in Infrastructure/Configuration/StorageOptionsValidator.cs"
Task: "JobsOptionsValidator in Infrastructure/Configuration/JobsOptionsValidator.cs"
Task: "TwilioOptionsValidator in Infrastructure/Configuration/TwilioOptionsValidator.cs"
Task: "SendGridOptionsValidator in Infrastructure/Configuration/SendGridOptionsValidator.cs"
# Then T014 wires them in Program.cs (sequential — single file).
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US1.
2. **STOP and VALIDATE**: bad config fails fast; valid config starts. This is the core value.

### Incremental Delivery

1. Foundational ready → US1 (MVP, fail-fast) → US2 (placeholders keep CI/dev green) → US3
   (lock rules with tests) → US4 (frontend guard).
2. US4 can be delivered any time after Setup, independent of the backend track.

### Parallel Team Strategy

- Developer A: backend track (Foundational → US1 → US2 → US3).
- Developer B: US4 frontend guard, fully in parallel.

---

## Notes

- [P] = different files, no incomplete-task dependencies.
- Validation is **uniform across environments** (clarified 2026-06-13); dev/CI satisfy
  secret-presence with placeholders (US2), never real credentials.
- Error messages name `Section:Field` and never echo secret values (FR-019).
- Startup-failure tests need no PostgreSQL — validation throws before any DB use (research R4).
- Commit after each task or logical group.
