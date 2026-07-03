# Phase 0 Research: Architecture Remediation

**Feature**: 015-architecture-remediation · **Date**: 2026-07-02

No `NEEDS CLARIFICATION` markers remained in the Technical Context; the open questions were tooling/pattern selections. Each decision below follows the repo's existing conventions first, ecosystem best practice second.

## R1. Atomicity pattern for webhook handlers (FR-001, FR-002)

- **Decision**: Wrap each `WebhookProcessor` handler's writes in the same pattern the interactive flows already use — `IExecutionStrategy.ExecuteAsync` + `BeginTransactionAsync` — via the new shared `PaymentRecorder`. `LedgerService.AppendAsync` already joins an ambient transaction when one exists (`LedgerService.cs:27-31`), so no ledger changes are needed for enlistment. The idempotency guard (terminal-status / cumulative-amount check) moves inside the same transaction as the write it guards, with the row read via `FOR UPDATE`-equivalent tracking query so retry races serialize.
- **Rationale**: Reuses the proven pattern from `ConfirmPaymentEndpoint.cs:89-109` / `RecurringDraftService.cs:184-198`; respects Npgsql retrying-execution-strategy rules (explicit transactions must run inside the strategy delegate). Keeps transactions short — no Stripe calls occur inside webhook handlers (verified: handlers only read `evt` payloads already in memory).
- **Alternatives considered**: `TransactionScope` (rejected: mixes poorly with Npgsql execution strategy and async flow); outbox-style "process exactly once" job queue (rejected: `WebhookEventInbox` + reconcile retry already provides at-least-once redelivery; handler idempotency is the missing piece, not more infrastructure).

## R2. Shared settle path (FR-003)

- **Decision**: New `PaymentRecorder` service in `Features/Payments/Services` exposing `RecordSettledPaymentAsync(...)` (txn upsert + `ledger.AddPaymentAsync` + `ReceiptFactory` receipt, one transaction) and `RecordCompensatingAsync(...)` (reversal/fee/refund + status/cumulative update, one transaction). `ConfirmPaymentEndpoint`, `RecurringDraftService`, and `WebhookProcessor` all delegate to it.
- **Rationale**: Directly removes the triplication (report §6) and gives the webhook path the atomic variant for free. Registered `AddScoped` alongside the existing ~25 registrations.
- **Alternatives considered**: Domain events + handlers (rejected: introduces new machinery the codebase doesn't use; transaction-script services are the accepted style per spec Assumptions).

## R3. Gateway-neutral provider event model (FR-021)

- **Decision**: Introduce `PaymentProviderEvent` (discriminated by `PaymentProviderEventKind` enum: `PaymentSucceeded`, `PaymentFailed`, `AchReturned`, `Refunded`, `DisputeUpdated`) carrying only the fields handlers actually read (intent id, charge id, amounts as `decimal`, failure reason, dispute status, mandate/PM references). `StripeGateway.ConstructEvent` becomes `ParseEvent(json, signature)` returning `PaymentProviderEvent`; the `Stripe.Event` destructuring moves from `WebhookProcessor`/`ReconciliationService` into a coverable pure `StripeEventTranslator` (in `Infrastructure/Payments`, NOT `[ExcludeFromCodeCoverage]` — contract-mapping logic stays inside the 95% coverage gate) invoked by `StripeGateway.ParseEvent` after signature verification; only the SDK-I/O surface of `StripeGateway` keeps the coverage exclusion.
- **Rationale**: The three leaking files (report §2) read a small, stable field set; a neutral DTO makes handlers unit-testable without constructing `Stripe.*` objects and completes the abstraction `IStripeGateway` already provides for outbound calls.
- **Alternatives considered**: Interface-wrapping Stripe types (rejected: leaks shape anyway); full anti-corruption event hierarchy per Stripe event taxonomy (rejected: YAGNI — 5 event kinds are handled today).

## R4. Historical consistency detection (FR-005)

- **Decision**: Add `DetectLedgerInconsistenciesAsync` to the existing `ReconciliationService`, invoked from the existing `ReconcileEndpoint` (Cloud Scheduler cadence) and once at cutover. It flags: transactions in terminal states whose expected ledger effects are missing/duplicated, and `CumulativeRefundedAmount` disagreeing with summed refund entries. Findings emit structured Serilog warnings (event id, txn id, property id — no amounts beyond deltas) and a Sentry message for alerting. Report-only.
- **Rationale**: Matches the clarification (cutover + recurring cadence, logs/alerts); reuses the scheduler/job plumbing; no schema or new endpoint.
- **Alternatives considered**: New table for findings (rejected: report-only per clarification); one-off script (rejected: recurring detection guards future regressions for free).

## R5. Layering enforcement mechanism (FR-013/014/021, SC-005)

- **Decision**: `NetArchTest.Rules` tests in the new `HOAManagementCompany.UnitTests` project: (a) `Infrastructure.*` must not depend on `Features.*`; (b) `Domain.*` must not depend on `Features.*`/`Infrastructure.*`; (c) `Stripe` namespace referenced only from `Infrastructure.Payments`; (d) `Features.X` must not depend on `Features.Y` internals (allow-list: `Features.Common`, shared `Models`). Runs in the PR-verification workflow as ordinary unit tests.
- **Rationale**: Zero new CI plumbing (it's just xUnit); failures point at the offending type; the allow-list encodes the intended architecture explicitly.
- **Alternatives considered**: Roslyn analyzer/banned-API lists (rejected: more build engineering for the same guarantee); ArchUnitNET (viable, but NetArchTest is lighter and sufficient for namespace-level rules).

## R6. Shared-kernel moves (FR-007, FR-013)

- **Decision**: `DomainException` moves to `HOAManagementCompany/Domain/DomainException.cs` (namespace `HOAManagementCompany.Domain`); a `global using HOAManagementCompany.Domain;` eases the 11+ call-site updates. Options classes (`StripeOptions`, `PaymentsOptions`, `JobsOptions`, `TwilioOptions`, `SendGridOptions`) move from `Features/Payments/PaymentOptions.cs` to `Infrastructure/Configuration/` beside their validators (they are config, consumed by Infrastructure adapters and Program.cs — not domain concepts).
- **Rationale**: Domain owns business error semantics; config options belong with the validation layer that 008 already centralized there. Both moves are mechanical namespace changes with no behavior delta.
- **Alternatives considered**: New `Shared/` root folder (rejected: two well-defined homes already exist; a third top-level bucket invites dumping).

## R7. Central error mapping + identity accessor (FR-006, FR-008)

- **Decision**: Extend `GlobalExceptionHandler` with a `DomainException` branch writing `{ code, message }` at `ex.StatusCode`, and delete the 12 per-endpoint catch blocks. Add `Features/Common/ClaimsPrincipalExtensions.cs` with `GetPropertyId()`/`GetUserId()` that throw `DomainException("MISSING_CLAIM", …, 403)` when absent — so the central mapping also converts the current NRE crash into a clean 403. Replace all 24 inline parses.
- **Rationale**: FastEndpoints exceptions flow to ASP.NET Core's `IExceptionHandler` pipeline the handler already sits in; endpoints get fail-safe-by-default behavior (spec US2 scenario 3) with no per-endpoint code.
- **Alternatives considered**: FastEndpoints pre/post-processors per endpoint group (rejected: still opt-in per endpoint — not fail-safe); result-object pattern instead of exceptions (rejected: repo-wide rewrite for no user-visible gain).

## R8. Production backstop (FR-009, FR-010)

- **Decision**: Three layers: (1) `HostEnvironmentValidator` (exists from 008) gains rules rejecting `Startup:SeedData=true` and `DevTools:E2ECleanupEnabled=true` when environment is `Production` — fail-fast at boot; (2) `E2ECleanupEndpoint` re-adds an explicit `env.IsProduction()` guard returning 404 and logging a security-relevant Serilog event when the flag was set (kept in addition to boot validation for defense in depth; `Dev`/`Test` deployed environments keep working, which is why `IsDevelopment()` was originally removed); (3) csproj: `<Content Include="testdata\**\*" ... CopyToPublishDirectory="Never">`.
- **Rationale**: Boot-time rejection matches the constitution's fail-fast configuration rule; the endpoint-level guard covers config drift after boot; publish exclusion follows the pattern already used for test appsettings (csproj:76-77).
- **Alternatives considered**: `#if !RELEASE` compilation exclusion of Seed/DevTools (rejected: Dev/Test deployed environments run Release builds and legitimately use these).

## R9. Client type generation + drift gate (FR-011, FR-012)

- **Decision**: `openapi-typescript` (dev-dependency in `neko-hoa`) generating `src/app/core/api/generated-types.ts` from the backend's OpenAPI JSON. Export the document in CI/local via the API's existing NSwag pipeline: a small `dotnet run -- --export-openapi <path>` startup flag (mirrors the existing `--seed` startup-flag pattern) that writes swagger.json and exits. npm script `generate:api-types` runs export + codegen; CI drift gate = run it, then `git diff --exit-code` on the generated file. `core/models/index.ts` shrinks to app-only view-models and re-exports canonical generated types; dead `RecurringPayment`/`DraftEntry`/`ISODate`/`LedgerEntryType` deleted; `payments.service.ts` local interfaces and story fixtures re-point to generated types.
- **Rationale**: Types-only per clarification — `openapi-typescript` emits pure `.d.ts`-style types with zero runtime, preserving the hand-written services/mappers. The startup-flag export avoids standing up the full app + database in CI just to scrape `/swagger/json`.
- **Alternatives considered**: NSwag TS client / orval (rejected: generate full clients — beyond types-only scope); checking in swagger.json as the source (rejected: two artifacts to drift instead of one gate).

## R10. Test-tier split (FR-017)

- **Decision**: New `HOAManagementCompany.UnitTests` csproj referencing only the app project + xUnit + NetArchTest + FluentAssertions (if already used). Move `Tests/Unit/**` (88 facts — FeeCalculator, AllocationService, ReceiptFactory, PaymentConfigService, 9 validator suites) into it. `HOAManagementCompany.Tests` keeps Integration/Performance/Startup/Sandbox on Testcontainers. Introduce per-domain factories (`PaymentFactory`, extending existing `Factories/`) and migrate new P1 tests to factories; existing seeder-coupled tests are left as-is and migrated opportunistically (full seeder removal is explicitly out of scope).
- **Rationale**: A separate csproj is the only way to get a container-free dependency graph (the current csproj unconditionally pulls Testcontainers/MinIO/Mvc.Testing). Bounded factory adoption avoids a big-bang rewrite of 150 integration tests.
- **Alternatives considered**: Trait-based filtering in one project (rejected: still builds/restores the full integration graph; SC-006's "no container tooling" machine can't even restore MinIO-dependent fixtures cleanly).

## R11. CI/CD split (FR-018)

- **Decision**: Split `test.yml` into `ci.yml` (PR verification: backend build/test/Sonar/Codecov, frontend unit/build/Storybook/Cypress, type-drift gate, Trivy scan-only) and `release.yml` (push-to-main: docker build/push, deploy-dev with health gates, smoke, traffic promotion, failure webhook). Extract repeated setup (checkout+dotnet, checkout+node, gcloud auth) into composite actions under `.github/actions/`, following the existing `pr-env-tofu-init` precedent; `infra-plan.yml`/`infra-apply.yml` adopt the tofu composite and drop the dead commented block.
- **Rationale**: Preserves every existing gate while separating PR feedback from the release path (spec US6); composite actions are the repo's established reuse mechanism.
- **Alternatives considered**: Reusable workflows (`workflow_call`) (viable; composite actions chosen for consistency with the existing pattern and finer-grained reuse).

## R12. IaC shared core module (FR-019)

- **Decision**: Extract `infra/modules/cloud-run-service/` (Cloud Run service + secrets wiring + Neon branch/database provisioning as submodules or one core module with variables for the PR-vs-standard differences: name prefixing, TTL labels, scaling bounds, branch-vs-database source). `environment/` and `pr-environment/` become thin wrappers passing their variant inputs.
- **Rationale**: The three files diverge ~350 lines while modeling the same resources (report §3-infra); a variable-driven core removes double maintenance. Provider versions stay pinned in `versions.tf` per 010.
- **Alternatives considered**: Merging pr-environment into environment with a `is_pr` flag (rejected: conflates lifecycles — PR envs have TTL/sweep semantics; wrapper modules keep those concerns separate).

## R13. Payment component decomposition + frontend shared layers (FR-020)

- **Decision**: Add `core/interceptors/error.interceptor.ts` (registered via `withInterceptors` beside the auth interceptor) normalizing the backend's `{ code, message }` envelope into a typed `ApiError`; components consume a shared error signal/presentation component from a new `shared/` folder. Extract from the two god components: `StripeElementsHostComponent` (mount/confirm lifecycle around ngx-stripe), `PaymentWizardStore` (signal-based step/state machine per flow), and template/style extraction to `.html`/`.scss` files. Services stay in `core/services` (moving them is cosmetic churn; deferred).
- **Rationale**: Targets exactly the four mixed concerns the audit measured (341/329-line components, 41/25 inline styles, 17/12 hand-rolled error sites) with the smallest new-abstraction budget; standalone components + signals match the app's existing idiom.
- **Alternatives considered**: NgRx/state library (rejected: app has zero shared-state needs beyond these flows; signals suffice); full feature-folder re-architecture (rejected: out of proportion to findings).

## Resolution status

All Technical Context items resolved; no outstanding `NEEDS CLARIFICATION`. Ready for Phase 1.
