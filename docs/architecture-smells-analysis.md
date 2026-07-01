# Architecture Smells Analysis

Date: 2026-07-01 · Analyzed at commit `e2fcb44` · Scope: backend (`HOAManagementCompany`), frontend (`neko-hoa`), tests (`HOAManagementCompany.Tests`), infrastructure (`infra/`, `.github/`, Docker/compose).

This is a static architecture review focused on structural smells — layering violations, misplaced responsibilities, duplication, god classes, and drift surfaces — not a bug hunt or security review. Every finding cites file-level evidence. A "Non-smells" section at the end lists areas that were checked and found healthy, including one flagged hotspot that turned out to be a false positive.

---

## Top findings (ranked)

| # | Finding | Area | Severity |
|---|---------|------|----------|
| 1 | Webhook processing writes are non-atomic, breaking the ledger invariant enforced everywhere else (double-post risk on retry) | Backend/Payments | **High** |
| 2 | `DomainException` lives in `Features/Auth`, making Auth an implicit shared kernel for 11+ files across all features | Backend | **High** |
| 3 | Infrastructure layer depends on Features (8 files), inverting the dependency arrow | Backend | **High** |
| 4 | Frontend/backend contract is hand-duplicated with no codegen; `core/models/index.ts` is a god barrel with dead, drifted types | Contract | **High** |
| 5 | Cross-cutting concerns copy-pasted: claim parsing inline in 24 places, `DomainException` catch blocks hand-written in 12 endpoints | Backend | **High** |
| 6 | Test/dev concerns compiled into the production binary: `Seed/`, `testdata/sample.pdf`, anonymous config-gated `/e2e/cleanup` endpoint | Backend | **High** |
| 7 | God components: `one-time.component.ts` (341 lines) and `recurring.component.ts` (329) each mix presentation, forms, API orchestration, and Stripe Elements | Frontend | **High** |
| 8 | `test.yml` is a 367-line CI+CD monolith (7 jobs: test → scan → build → publish → deploy → promote → notify) | CI | **High** |
| 9 | Settle-payment orchestration triplicated across `ConfirmPaymentEndpoint`, `RecurringDraftService`, `WebhookProcessor` | Backend/Payments | Medium |
| 10 | Anemic domain model: 26 entities with zero methods; all invariants live in services | Backend | Medium |
| 11 | Inconsistent persistence boundary: 8 endpoints query `ApplicationDbContext` directly while other slices use services | Backend | Medium |
| 12 | `environment` vs `pr-environment` OpenTofu modules are parallel near-duplicates (~350 diff lines across cloud_run/secrets/neon) | Infra | Medium |
| 13 | Single test project mixes unit + integration + perf tiers; session-global god fixture (`TestDataSeeder`, 260 lines, magic IDs) | Tests | Medium |
| 14 | No shared HTTP/error layer in the frontend: 6 services hand-roll URLs, no error interceptor, per-component try/catch duplication | Frontend | Medium |
| 15 | Orphaned root `/Dockerfile` that nothing builds, subtly divergent from the real one | Infra | Medium |

---

## Backend (`HOAManagementCompany`)

### 1. Layering violation: Infrastructure → Features (High)

Eight Infrastructure files import `HOAManagementCompany.Features.Payments`:

- `Infrastructure/Payments/StripeGateway.cs:3`, `Infrastructure/Payments/Alerts/TwilioSmsProvider.cs:2`, `SendGridEmailProvider.cs:2`
- `Infrastructure/Configuration/{Stripe,Payments,Twilio,SendGrid,Jobs}OptionsValidator.cs`

Root cause: the options types (`StripeOptions`, `PaymentsOptions`, `JobsOptions`, …) are defined under `Features/Payments/PaymentOptions.cs`, while their validators and the SDK adapters that consume them live in Infrastructure. Options/config are cross-cutting and belong in a shared layer (or alongside the validators in `Infrastructure/Configuration`), not inside a feature slice.

`Domain/` is clean — zero references to Infrastructure or Features.

### 2. `DomainException` misplaced in `Features/Auth` (High)

The application's core business-error type — `DomainException(code, message, statusCode)` — is defined at the bottom of `Features/Auth/AuthService.cs:206`. Every other feature must `using HOAManagementCompany.Features.Auth` just to throw or catch it: 11+ files across Property, Community, Payments (e.g. `Features/Property/PropertyEndpoint.cs:2`, `Features/Community/CommunityService.cs:2`, `Features/Community/Documents/DocumentDownloadEndpoint.cs:2`). This turns the Auth slice into an accidental shared kernel and is the single largest source of cross-feature coupling. It belongs in `Domain/`.

### 3. Duplicated cross-cutting concerns (High)

- **Claims parsing:** `Guid.Parse(User.FindFirst("propertyId")!.Value)` appears inline in **24 places across 23 files** (e.g. `Features/Property/PropertyEndpoint.cs:17`, `Features/Dashboard/DashboardEndpoint.cs:16-17`). No `ClaimsPrincipal` extension or `CurrentUser` abstraction exists; the `!` means a missing claim throws `NullReferenceException` rather than a clean 401/403.
- **Error handling is split into two parallel paths:** `Features/Common/GlobalExceptionHandler.cs` maps only unhandled exceptions to a generic 500 and does *not* understand `DomainException`. So 12 endpoints hand-write `catch (DomainException ex)` + `Response.WriteAsJsonAsync(new { code, message })`; any endpoint that forgets the catch surfaces business errors as 500s.
- **Validation is the counter-example done right:** centralized FastEndpoints `Validator<T>` classes (11) with a global 422 response builder.

### 4. Anemic domain model (Medium)

All 26 entities in `Domain/Entities` are pure data bags — public `{ get; set; }` on every property, zero methods (`Property.cs` 30 props, `PaymentTransaction.cs` 28, `LedgerEntry.cs` 15). Every invariant (fee calculation, allocation, ledger posting rules, status transitions, refund accumulation) lives in `Features/Payments/Services/*` and other services — a transaction-script architecture. This is a deliberate trade-off some teams accept, but combined with the mixed persistence boundary (below) it means invariants are only as safe as each call site.

### 5. Inconsistent persistence boundary (Medium)

`ApplicationDbContext` is referenced by 24 files under `Features/`. Property, Community, Dashboard, and Auth route through a `*Service` seam, but **8 endpoints query EF directly in the handler** (`Payments/OneTime/PaymentOptionsEndpoint.cs`, `ConfirmPaymentEndpoint.cs`, `TransactionsEndpoint.cs`, `ReceiptEndpoint.cs`, `Recurring/SetupIntentEndpoint.cs`, `Alerts/AlertPreferencesEndpoints.cs`, `Webhooks/StripeWebhookEndpoint.cs`, `DevTools/E2ECleanupEndpoint.cs`). Two different conventions with no rule deciding which applies. On the positive side, EF entities are never returned from endpoints — response DTOs are mapped consistently, though by ~22 hand-written `new XDto { … }` projection sites with no shared mapping layer.

### 6. `Program.cs` trending toward a god composition root (Medium)

420 lines inlining Serilog, Sentry/OTel, DbContext + traced NpgsqlDataSource, Identity + JWT, 9 options blocks, S3/MinIO client, ~55 lines of rate-limiting policy (`Program.cs:224-278` plus a mid-file static helper at `:279`), ~30 lines of CORS logic, and ~25 service registrations (`:333-360`). Startup migrations/seeding are the one extracted piece (`Infrastructure/Configuration/StartupTasks.cs`). Grouping registrations into per-area `AddXxx()` extension methods would restore readability.

### 7. Test/dev concerns shipped in the production assembly (High)

- `Seed/` (7 files, ~800 lines of seeders) compiles into the production binary; execution is gated only by a config flag (`StartupTasks.cs:53,58`), with the "off in Production" invariant asserted by a comment in `appsettings.json:31`, not an `IsProduction()` guard.
- `Features/DevTools/E2ECleanupEndpoint.cs` — an **anonymous** `DELETE /e2e/cleanup` that bulk-deletes users matching `e2e+%@test.dev`. The code comment (`:14-17`) records that the `IsDevelopment()` environment guard was deliberately removed; the only barrier in Production is one unset config key (`DevTools:E2ECleanupEnabled`). The blast radius is limited to test-pattern emails, but a destructive anonymous endpoint with no environment backstop is a fragile design.
- `testdata/sample.pdf` ships to the production image (`HOAManagementCompany.csproj:65` copies `testdata\**\*` with no `CopyToPublishDirectory="Never"`), even though `Infrastructure/Storage/TestDataFiles.cs` already has an embedded fallback.

### 8. Dead Blazor remnant (Low)

`Components/Pages/` contains exactly one file — a misfiled `.code-workspace`. No `.razor` files exist. The directory is vestigial scaffolding and misleading; remove it.

---

## Payments subsystem (deep dive)

### 9. Webhook writes are non-atomic — the subsystem's one real consistency hole (High)

The two interactive write paths enforce a clear invariant — transaction row + ledger entry + receipt commit atomically inside one execution-strategy transaction, with `LedgerService.AppendAsync` joining the ambient transaction (`ConfirmPaymentEndpoint.cs:89-109`, `RecurringDraftService.cs:184-198`, `LedgerService.cs:27-31`).

The **webhook path silently drops that invariant**: `WebhookProcessor.cs` (214 lines) contains five separate `SaveChangesAsync` calls and zero `BeginTransaction` (verified by grep). With no ambient transaction, each `ledger.AddPaymentAsync` / `AddCompensatingChargeAsync` opens and commits its *own* transaction, and the `txn.Status` update commits separately afterward. Because failed webhooks are re-run by `ReconciliationService.RetryPendingWebhooksAsync`, a crash between the ledger commit and the status commit re-executes the handler:

- `HandleFailedAsync` (`:97-108`): ACH return appends a `Reversal` + NSF fee (committed), then sets `Status=Returned` in a separate save → crash between → **retry double-posts the reversal and NSF fee**.
- `HandleRefundAsync` (`:128-139`): refund ledger row commits before `CumulativeRefundedAmount` persists → retry recomputes the same delta → **duplicate refund entry**.

The idempotency machinery at ingress is otherwise well-built (durable `WebhookEventInbox` persisted before the 2xx ack, dedupe on `StripeEventId`) — the hole is purely in handler-level atomicity.

### 10. Settle-payment orchestration triplicated (Medium)

The "record a settled payment" sequence (build `PaymentTransaction` ~15 fields → transaction → save → `ledger.AddPaymentAsync` → receipt → commit) is copy-pasted three times: `OneTime/ConfirmPaymentEndpoint.cs:67-109`, `Recurring/RecurringDraftService.cs:153-198`, and `Webhooks/WebhookProcessor.cs:73-89` (`SettleSucceededAsync`). Extracting a shared `PaymentRecorder`/settlement writer would remove the duplication — and, since the two interactive copies are the atomic ones, reusing it from the webhook path would fix finding #9 at the same time.

### 11. Stripe SDK leaks past the gateway abstraction for events (Medium)

Charge/intent/vault flows go cleanly through `IStripeGateway` (with an in-memory fake for tests). But the inbound-event surface bypasses it: `WebhookProcessor.cs:10`, `StripeWebhookEndpoint.cs:9`, and `Jobs/ReconciliationService.cs:7` all `using Stripe;` and destructure raw `Stripe.Event` / `PaymentIntent` / `Charge` / `Dispute` objects. `IStripeGateway.ConstructEvent` returns the raw SDK type (`IStripeGateway.cs:82`), so there is no gateway-neutral event model — unit-testing handlers requires constructing real Stripe SDK objects.

### 12. Smaller payments smells (Low–Medium)

- `LedgerService.RecomputeBalancesAsync` (`:119-134`) rewrites running balances **without acquiring the per-property advisory lock** every write path uses — can race a concurrent append. (Medium)
- Advisory-lock key derives from `propertyId.GetHashCode()` XOR one byte (`LedgerService.cs:139`) — collision-prone but fail-safe (over-serializes). (Low)
- Money-policy scatter: `* 100m` cents conversion duplicated (`CreateIntentEndpoint.cs:35`, `RecurringDraftService.cs:134`), `"usd"` hardcoded twice, `MidpointRounding.AwayFromZero` repeated in 3 files. Internally decimals are used consistently, cents only at the Stripe boundary — the pattern is right, the constants just aren't centralized. (Low)
- The ~7-line scheduler-secret check is duplicated verbatim between `Jobs/RunDraftsEndpoint.cs:35-43` and `Jobs/ReconcileEndpoint.cs:28-36`. (Low)
- Stringly-typed outbox kind→channel map in `OutboxDispatcher.ChannelForKind` (`:98-103`) must stay in sync with kind strings emitted in two other files. (Low)

---

## Frontend (`neko-hoa`)

### 13. `core/models/index.ts` — god barrel with dead, drifted types (High)

A single 176-line barrel exports 21 types covering every domain in the app, imported by 18 files. Worse, it has already drifted: `RecurringPayment` (17 fields) and `DraftEntry` have **zero usages outside the file** — the running code uses parallel definitions `RecurringInfo` and `DraftRow` declared inside `payments.service.ts` (lines 73–85, 112–119), which itself defines 14 more interfaces. Storybook stories add a third hand-written copy of the same payment DTO shapes. Three competing sources of truth for one domain, none generated from the backend contract.

### 14. God components in payments (High)

`features/payments/one-time/one-time.component.ts` (341 lines) and `recurring/recurring.component.ts` (329 lines) — also the two highest-churn frontend files — each combine all four concerns in one class: giant inline templates with inline styles (41 and 25 `style="…"` attributes respectively; no `.html`/`.scss` split), direct API orchestration, Stripe Elements lifecycle (`injectStripe`, `confirmPayment`/`confirmSetup`, clientSecret handling), and a hand-rolled 4-step wizard state machine with inline validation and embedded mandate text. Eight more components exceed 160 lines.

### 15. Missing shared layers (Medium)

- **No `shared/` layer at all** — no shared UI primitives, pipes, or directives, which is why presentation gets inlined per component.
- **No API-client abstraction:** all 6 services repeat `private base = environment.apiBaseUrl` and hand-build URLs; each carries its own private `Api* → model` mappers.
- **No error interceptor / global `ErrorHandler`:** the only interceptor is `auth.interceptor.ts` (bearer + 401 refresh — good). Error handling is re-implemented per component as try/catch → `error.set(...)` (17 sites in `recurring`, 12 in `one-time`).
- **No shared state/caching:** services are stateless pass-throughs; `currentBalance` is independently fetched and mapped by dashboard, one-time, and statement components. `PaymentsService.getBalance()` (`:171-181`) still hardcodes `monthlyAssessment: 250` — stub logic in a core service.
- Domain services (`payments`, `community`, `property`, `dashboard`) are parked in `core/services/` rather than co-located with their features.

### Frontend positives

Fully standalone components (zero `@NgModule`, consistent paradigm), clean route-level lazy feature folders, and **zero cross-feature imports** — feature isolation is genuinely good.

---

## Contract, tests, and delivery

### 16. Hand-duplicated frontend/backend contract (High)

The backend emits an OpenAPI document (FastEndpoints.Swagger, wired in `Program.cs:317-318, 409-410`), but nothing consumes it: no nswag/openapi-generator/orval anywhere in `neko-hoa`. Every TypeScript interface is hand-mirrored from C# DTOs, which is exactly how the dead/drifted types in finding #13 arose. Generating the client types from the existing OpenAPI doc would eliminate the entire drift surface.

### 17. Test architecture: one assembly, four tiers, one god fixture (Medium)

- Single test project mixes Unit (88 facts / 15 files), Integration (150 facts / 45 files), Performance, Startup, and Sandbox tiers. The csproj unconditionally references Testcontainers (PostgreSQL + MinIO), so there is no container-free fast feedback loop.
- `Fixtures/TestDataSeeder.cs` (260 lines) seeds ~15 entity types in one method; tests across unrelated domains couple to its hardcoded magic GUIDs (e.g. `aaaaaaaa-0000-0000-0000-000000000001` in `LedgerServiceTests.cs:18`). Any seed reshuffle breaks the whole suite. Session-global `TestDatabaseFixture` makes all 150 integration tests depend on both containers and the seeder.
- Positive: core services are tested directly through DI (e.g. `LedgerServiceTests` resolves `LedgerService`), not only via HTTP — though still on the container stack.

### 18. CI/CD monolith and script logic (High / Medium)

- `.github/workflows/test.yml` (367 lines, 7 jobs) is named "CI" but spans test → Sonar/Codecov → frontend → sandbox integration → Trivy gate → Docker publish → dev deploy with health gates, E2E, traffic promotion, and failure webhooks. PR-feedback and release pipelines are fused; any CI tweak risks the deploy path.
- Setup blocks are copy-pasted across 9 workflows (`actions/checkout` ×15, `google-github-actions/auth` ×5); a composite action exists for PR-env tofu init but `infra-plan.yml`/`infra-apply.yml` inline their own tofu setup instead (plus a commented-out dead duplicate at `infra-apply.yml:87-93`).
- `.github/scripts/check-repowise-health-gates.py` (241 lines) implements real gate logic (threshold evaluation, JSON scraping from mixed stdout, /tmp caching, enforce/warn modes) with no tests and hidden env-var switches — business logic living in CI glue.

### 19. IaC: duplicated PR-environment module (Medium)

`infra/modules/environment` (~598 lines) and `infra/modules/pr-environment` (~407 lines) are parallel implementations of the same concerns — the Cloud Run service (152 diff lines), secrets wiring (93), and Neon branch logic (103) are each maintained twice. Extracting shared inner modules would remove the copy-paste. By contrast, dev vs staging roots are genuinely DRY (<15% divergence, same `modules/environment`).

### 20. Docker/local-dev drift (Medium)

- Root `/Dockerfile` is built by nothing (compose and CI both point at `HOAManagementCompany/Dockerfile`) and differs only by *missing* `ENV ASPNETCORE_ENVIRONMENT=Production` — a dead drift trap. Delete it.
- `run_local_development.sh` starts only the `db` service, not `minio` or `aspire-dashboard`, so local dev runs without object storage or a telemetry sink while tests and compose always provide them. The compose `api` service also duplicates the env-var contract inline (`docker-compose.yaml:65-81`) alongside `.env.example`.

---

## Non-smells (checked and healthy)

- **`LedgerService.cs` — the repo's flagged "worst-health" file — is a false positive.** 142 lines, 5 cohesive public methods, single responsibility (append-only ledger + balance). Its score reflects high fan-in (5 dependents) and the one raw-SQL advisory-lock line, not poor structure. Do not prioritize rewriting it; the real complexity concentration is `WebhookProcessor.cs`.
- Alerts are properly event-driven via a transactional outbox (`AlertService` enqueues rows on the caller's transaction; `OutboxDispatcher` delivers post-ack) with a clean feature/infrastructure split and no duplicated composition.
- Background jobs are thin HTTP endpoints triggered by Cloud Scheduler with constant-time secret comparison, delegating to services — no duplicated logic, no in-process scheduler.
- Config validation (spec 008) is fully implemented and centralized (11 FluentValidation validators + generic `FluentValidateOptions` adapter) with unit and integration coverage. No secrets committed.
- Domain layer has zero dependencies on Infrastructure or Features; EF entities never leak into API responses; DI constructors are lean (max 2 dependencies).
- Frontend has no cross-feature imports and a consistent standalone-component paradigm.

---

## Recommended remediation order

Highest leverage first — several fixes collapse multiple findings:

1. **Extract an atomic `PaymentRecorder` and use it from `WebhookProcessor`** — fixes the double-post consistency hole (#9) and the triplication (#10) in one change.
2. **Move `DomainException` to `Domain/` and options types out of `Features/Payments`** — resolves both High layering findings (#2, #3) with mechanical moves.
3. **Teach `GlobalExceptionHandler` about `DomainException` and add a `ClaimsPrincipal.PropertyId()` extension** — deletes 12 hand-written catch blocks and 24 inline claim parses (#5).
4. **Generate the Angular client types from the existing OpenAPI doc** — eliminates the god barrel's drift surface (#13, #16); delete the dead `RecurringPayment`/`DraftEntry` types either way.
5. **Split `test.yml` into CI and CD workflows; extract shared setup into composite actions** (#18).
6. **Add an environment backstop (`IsProduction()` hard-disable) to seeding and `/e2e/cleanup`, and exclude `testdata/` from publish** (#7).
7. Decompose the two payment god components behind a shared Stripe-elements wrapper + error interceptor (#14, #15); split the OpenTofu PR-environment module (#19); delete the dead root `Dockerfile` and Blazor `Components/Pages` remnant (#20, #8).
