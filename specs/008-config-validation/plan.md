# Implementation Plan: Startup Configuration Validation

**Branch**: `008-config-validation` | **Date**: 2026-06-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/008-config-validation/spec.md`

## Summary

Make misconfiguration a fail-fast, boot-time error instead of a deferred runtime failure.
On the backend, every strongly-typed options group (`StorageOptions`, `ObservabilityOptions`,
`StripeOptions`, `PaymentsOptions`, `JobsOptions`, `TwilioOptions`, `SendGridOptions`) is
bound with `AddOptions<T>().Bind(...).ValidateOnStart()` and validated by a FluentValidation
`AbstractValidator<T>` bridged through a generic `IValidateOptions<T>` adapter. Validation is
**uniform across all environments** (clarified 2026-06-13): structural, business-rule, and
secret-*presence* checks run identically in Development, Test, and Production; local/CI satisfy
presence with non-functional **placeholder** values. On the frontend, a startup guard
validates required runtime config (publishable key, API base URL) and, when missing in a
production build, **halts bootstrap and renders a minimal full-page error**. The backend
surfaces failures in startup logs and fails to start.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend); TypeScript / Angular 17.3 (frontend)  
**Primary Dependencies**: FastEndpoints (bundles **FluentValidation** — already used for
request DTO validators); `Microsoft.Extensions.Options` (`AddOptions`, `ValidateOnStart`,
`IValidateOptions<T>`); Serilog for the startup-failure log. Frontend: Angular bootstrap
(`bootstrapApplication`) + existing `APP_INITIALIZER` in `app.config.ts`.  
**Storage**: N/A — no schema, migration, or persistence changes.  
**Testing**: xUnit for validator unit tests (no DB); `Microsoft.AspNetCore.Mvc.Testing`
(`WebApplicationFactory<Program>`, already used in `IntegrationTestBase`) for startup
fail-fast tests; Jasmine/Karma for the frontend guard.  
**Target Platform**: Linux container on Cloud Run (backend); modern browsers (frontend).  
**Project Type**: web (Angular frontend + .NET backend + xUnit test project).  
**Performance Goals**: Validation runs once at boot; negligible overhead (target < 50 ms
added startup time). No request-path impact.  
**Constraints**: MUST NOT break local/CI startup (placeholder secrets satisfy presence);
error messages MUST NOT echo secret values (FR-019); all validation failures aggregated
(FR-003); frontend guard MUST NOT block non-production builds (FR-018).  
**Scale/Scope**: 7 backend option groups + 1 generic validation bridge + 1 frontend guard.
No new endpoints; no new entities; no new external services.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Technology fit**: ✅ Uses the approved .NET/FastEndpoints stack and FluentValidation
  already in the project; Angular for the frontend guard. No new product technology. Auth0,
  Cloudflare, Cloud Run, Neon, R2/MinIO unaffected.
- **HOA tenancy**: ✅ N/A — application-level configuration, no HOA-scoped rows, no queries.
- **API contracts**: ✅ N/A — no new or changed endpoints, response shapes, or pagination.
  Only observable change: a misconfigured deployment fails to start.
- **Security and operations**: ✅ Reinforces the constitution — secrets remain externalized
  (env vars / secret store), presence is validated without printing values (FR-019), and the
  failure is logged via Serilog. Failing fast shrinks the window of running mis-secured.
- **File storage**: ✅ N/A — no new blob storage. `StorageOptions` validation only checks
  presence of required fields; MinIO/local defaults must satisfy it.
- **Caching/edge**: ✅ N/A — no responses cached.
- **Testing discipline**: ✅ Test-first. Validator unit tests use xUnit **Theories** for the
  data-varied cases (boundary ratios, enum values, fee-policy combinations). Startup
  fail-fast tests reuse the `WebApplicationFactory<Program>` pattern. No DB is required for
  validation-failure tests (failure throws before DB use), so transaction isolation rules are
  not implicated; the existing PostgreSQL/Testcontainers path is untouched.
- **CI/CD and documentation**: ✅ Sonar/Codecov via existing GitHub Actions; ≥95% coverage on
  changed files; Repowise marker regions added/refreshed on the new validation files.

**Result**: PASS — no violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/008-config-validation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (option groups + rules as the "data model")
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (validation contract + frontend config contract)
│   ├── backend-config-validation.md
│   └── frontend-config-guard.md
└── checklists/
    └── requirements.md   # Created by /speckit.specify
```

### Source Code (repository root)

```text
HOAManagementCompany/                      # .NET backend
├── Program.cs                             # MODIFY: replace Configure<T>(...) with
│                                          #   AddOptions<T>().Bind(...).ValidateOnStart();
│                                          #   remove null-forgiving Storage .Get<>()!;
│                                          #   register validators + IValidateOptions bridge.
├── Infrastructure/
│   └── Configuration/                     # NEW: validation home
│       ├── FluentValidateOptions.cs       # NEW: generic IValidateOptions<T> → FluentValidation
│       ├── OptionsValidationExtensions.cs # NEW: AddValidatedOptions<T,TValidator>() helper
│       ├── StorageOptionsValidator.cs     # NEW
│       ├── ObservabilityOptionsValidator.cs # NEW
│       ├── StripeOptionsValidator.cs      # NEW
│       ├── PaymentsOptionsValidator.cs    # NEW (incl. Percentage⇒CreditOnly cross-field rule)
│       ├── JobsOptionsValidator.cs        # NEW
│       ├── TwilioOptionsValidator.cs      # NEW (partial-config guard)
│       └── SendGridOptionsValidator.cs    # NEW (partial-config guard)
├── Infrastructure/Storage/StorageOptions.cs        # (validated; PublicServiceUrl etc.)
├── Infrastructure/Observability/ObservabilityOptions.cs
└── Features/Payments/PaymentOptions.cs    # StripeOptions/PaymentsOptions/Jobs/Twilio/SendGrid

HOAManagementCompany.Tests/                # xUnit test project
├── Unit/Configuration/                    # NEW: per-validator unit tests (Theories)
│   ├── PaymentsOptionsValidatorTests.cs
│   ├── StripeOptionsValidatorTests.cs
│   ├── ObservabilityOptionsValidatorTests.cs
│   ├── StorageOptionsValidatorTests.cs
│   ├── JobsOptionsValidatorTests.cs
│   └── (Twilio/SendGrid handled near existing TwilioOptionsTests)
└── Integration/Configuration/             # NEW: startup fail-fast tests
    └── StartupValidationTests.cs          # boots WebApplicationFactory with bad config,
                                           # asserts OptionsValidationException; + valid Test
                                           # config (placeholder secrets) starts successfully.

neko-hoa/                                  # Angular frontend
├── src/app/core/config/                   # NEW
│   ├── runtime-config.validator.ts        # NEW: pure validation of required env values
│   └── config-error.render.ts             # NEW: minimal full-page error renderer
├── src/app/app.config.ts                  # MODIFY: run guard before/within APP_INITIALIZER
├── src/main.ts                            # MODIFY (if needed): guard before bootstrap
└── src/app/core/config/*.spec.ts          # NEW: Jasmine/Karma unit tests for the guard
```

**Structure Decision**: Web application (Option 2). Backend validation lives in a new
`Infrastructure/Configuration/` folder (one validator per option group + a reusable bridge),
keeping `PaymentOptions.cs` and the other options classes as plain DTOs. Frontend guard lives
in `core/config/`. No new project; existing test project gains `Unit/Configuration/` and
`Integration/Configuration/` folders.

## Repowise Documentation

**Status**: In progress

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `HOAManagementCompany/Infrastructure/Configuration/FluentValidateOptions.cs` | `domain=configuration` | Document the generic FluentValidation → `IValidateOptions<T>` bridge |
| `HOAManagementCompany/Infrastructure/Configuration/OptionsValidationExtensions.cs` | `domain=configuration` | Document the `AddValidatedOptions` registration helper and `ValidateOnStart` wiring |
| `HOAManagementCompany/Program.cs` | `domain=bootstrap` | Refresh existing bootstrap region to note validated options |
| `neko-hoa/src/app/core/config/runtime-config.validator.ts` | `domain=configuration` | Document the frontend boot-time config guard |

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
