# Implementation Plan: Stage 2 Integration CI — Provider Sandbox Verification

**Branch**: `007-integration-ci-tests` | **Date**: 2026-06-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/007-integration-ci-tests/spec.md`

## Summary

Add a `main`-only CI job that runs the **real** Stripe, SendGrid, and Twilio adapters against each provider's test/sandbox mode, gating the release image so a broken integration cannot ship. The existing test architecture already supports this: the in-memory fakes are injected *only* by `PaymentTestBase`/`AlertTestBase`, so a new sandbox test base deriving from `IntegrationTestBase` (which leaves the real adapters in place) exercises live adapter code with no production-wiring change. Three small production edits are required because two adapters lack a test/sandbox seam today: a SendGrid sandbox-mode flag (the *only* no-deliver guarantee for email), a Twilio test-credential (basic-auth) path, and a credential guardrail that refuses non-test keys. A retry/classification helper distinguishes a real regression (fail) from a provider outage (skip-as-unavailable) so the gate stays trustworthy.

## Technical Context

**Language/Version**: C# / .NET 9.0
**Primary Dependencies**: Stripe.net, SendGrid SDK, Twilio SDK (all already referenced by the backend); xUnit, Testcontainers.PostgreSQL, Microsoft.AspNetCore.Mvc.Testing (test project)
**Storage**: PostgreSQL via Testcontainers for the webhook→persistence path (`WebhookEventInbox`); no schema changes
**Testing**: xUnit integration tests under a new `Sandbox` trait; real adapters against provider test/sandbox endpoints
**Target Platform**: GitHub Actions `ubuntu-latest`, `main` branch push only
**Project Type**: Backend test tier + CI workflow (no frontend, no new endpoints)
**Performance Goals**: Sandbox job completes in < 5 minutes (SC-006)
**Constraints**: Zero real charges/emails/SMS (FR-008/SC-003); no Stripe CLI sidecar (signed-fixture decision, Clarifications Q1); secrets test-scoped, masked, validated non-live (FR-006/009/010)
**Scale/Scope**: ~3 provider surfaces, ~10–14 sandbox tests, 1 new CI job, 3 small production edits, 2–3 new test-harness files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Technology fit**: ✅ GitHub Actions, .NET, Testcontainers/PostgreSQL, Docker are all in-scope. No new technology — Stripe/SendGrid/Twilio SDKs are already referenced; the signed-fixture decision avoids adding the Stripe CLI as a CI dependency.
- **HOA tenancy**: ⚪ N/A — Stage 2 introduces no HOA-scoped data or queries. Test objects are provider-side sandbox artifacts; the only DB writes are `WebhookEventInbox` rows already covered by 006. Justified in Complexity Tracking.
- **API contracts**: ⚪ N/A — no new or changed endpoints. The existing `POST /payments/webhooks/stripe` is *exercised*, not modified.
- **Security and operations**: ✅ Strongly relevant and satisfied — secrets externalized to CI (FR-006), masked (FR-010), validated as test-scoped before any call (FR-009). No production error-leak surface (test tier). Serilog warnings already emitted by adapters on failure.
- **File storage**: ⚪ N/A — no blob storage introduced. (MinIO Testcontainer still spins up via `IntegrationTestBase` but is unused by these tests.)
- **Caching/edge**: ⚪ N/A.
- **Testing discipline**: ✅ Testcontainers PostgreSQL for the persistence path; tests written first (failing sandbox tests precede the adapter seam edits); xUnit Theories used for Twilio magic-number success/failure variation. **Exception noted**: the real adapters remain `[ExcludeFromCodeCoverage]`, so SC-001 ("100% of adapter operations exercised") is tracked behaviorally via a method checklist, not the coverage report — consistent with the existing decision to exclude these thin network wrappers.
- **CI/CD and documentation**: ✅ New `integration-sandbox` GitHub Actions job; Repowise marker regions updated for the two edited adapter files. The existing `test` job is narrowed to exclude the `Sandbox` trait so PRs stay hermetic.

**Result**: PASS. One justified N/A cluster (tenancy/API/storage) tracked below; one documented testing exception (coverage-excluded adapters).

## Project Structure

### Documentation (this feature)

```text
specs/007-integration-ci-tests/
├── plan.md              # This file
├── research.md          # Phase 0 — provider sandbox mechanics + seam decisions
├── data-model.md        # Phase 1 — config/value objects (no DB entities)
├── quickstart.md        # Phase 1 — run Stage 2 locally + CI secret setup
├── contracts/           # Phase 1 — adapter seams, harness contract, CI job contract
│   ├── sendgrid-sandbox.md
│   ├── twilio-test-credentials.md
│   ├── stripe-sandbox-surface.md
│   ├── sandbox-test-harness.md
│   └── ci-integration-sandbox-job.md
└── checklists/
    └── requirements.md  # (from /speckit.specify)
```

### Source Code (repository root)

```text
HOAManagementCompany/                        # production edits (additive, opt-in)
├── Features/Payments/PaymentOptions.cs       # +SendGridOptions.Sandbox; +TwilioOptions.AuthToken
└── Infrastructure/Payments/Alerts/
    ├── SendGridEmailProvider.cs              # honor MailSettings.SandboxMode when Sandbox=true
    └── TwilioSmsProvider.cs                  # basic-auth (AccountSid+AuthToken) path for test creds

HOAManagementCompany.Tests/
├── Fixtures/
│   ├── SandboxIntegrationTestBase.cs         # NEW — keeps real adapters; loads test-mode secrets;
│   │                                         #       skip-if-unconfigured; non-live guardrail
│   └── SandboxResult.cs                      # NEW — retry + outage-vs-regression classifier
└── Integration/Sandbox/                      # NEW — [Trait("Category","Sandbox")]
    ├── StripeSandboxTests.cs                 # FR-011, FR-012, FR-013
    ├── SendGridSandboxTests.cs               # FR-015, FR-016
    └── TwilioSandboxTests.cs                 # FR-017, FR-018

.github/workflows/test.yml                   # narrow `test` to Category!=Sandbox;
                                             # add `integration-sandbox` job; gate docker-push on it
```

**Structure Decision**: Vertical test-tier slice. All new test code lives in `HOAManagementCompany.Tests/Integration/Sandbox/` behind a `Sandbox` trait. Production touches are strictly additive and default-off (sandbox/test-credential behavior is inert unless explicitly configured), so the existing payment-alert behavior is provably unchanged. The CI change is localized to `test.yml`.

## Repowise Documentation

**Status**: In progress

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `Infrastructure/Payments/Alerts/SendGridEmailProvider.cs` | `domain=payments-alerts` | Document the opt-in sandbox-mode no-deliver seam |
| `Infrastructure/Payments/Alerts/TwilioSmsProvider.cs` | `domain=payments-alerts` | Document the test-credential basic-auth path |
| `HOAManagementCompany.Tests/Integration/Sandbox/` | `section=stage2-sandbox` | Document the Stage 2 sandbox test tier and its trait gate |

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, marker validation |
| `integration-sandbox` | Stripe/SendGrid/Twilio test secrets | Runs real adapters vs sandbox on `main` push; gates `docker-push` |

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Production-code edits in a nominally "CI/test" feature (SendGrid sandbox flag, Twilio basic-auth path) | The real SendGrid adapter has **no** sandbox toggle, so running it as-is would deliver real email (violates SC-003); SendGrid keys have no test/live distinction, making sandbox mode the sole guardrail. Twilio magic-number test mode requires Account SID + Auth Token basic auth, which the API-key-only adapter cannot express. | "Test-only, no production change" is impossible: the no-deliver guarantee for email and the magic-number auth for SMS *are* adapter capabilities. Edits are additive, default-off, and leave production paths untouched. |
| Tenancy/API/storage constitution sections marked N/A | Stage 2 is infrastructure verification, not a product feature — it adds no HOA-scoped rows, endpoints, or blobs. | A tenancy/contract section would be fabricated; the only persistence is the pre-existing `WebhookEventInbox` write from 006. |
