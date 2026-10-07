# Implementation Plan: Replace SendGrid with Amazon SES for Transactional Email

**Branch**: `026-ses-email-provider` | **Date**: 2026-10-06 | **Spec**: `specs/026-ses-email-provider/spec.md`
**Input**: Feature specification from `specs/026-ses-email-provider/spec.md`

## Summary

Swap the backend's `email` alert provider from SendGrid, whose expired trial now returns 401 and
blocks every main-branch release, to Amazon SES via the SESv2 SDK. The change is contained to one
adapter, one options class and its validator, DI wiring, the provider-sandbox CI harness and tests,
and docs.

SendGrid's per-request sandbox flag is replaced by a `Ses:SimulatorOnly` guard, a pure, unit-tested
recipient check that allows only `@simulator.amazonses.com`. With the guard on, the adapter refuses
any other recipient before touching the network. A pure `SesErrorMapper` turns SDK failures into
stable `SES unavailable:` or `SES rejected:` reasons. The sandbox suite uses the "unavailable"
prefix to report outages as Skipped instead of Failed. CI authenticates with static keys for a
send-only IAM user (Clarifications 2026-10-06) and sends as `no-reply@mail.nekohoa.com`.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend `HOAManagementCompany`, tests `HOAManagementCompany.Tests`). No frontend change.
**Primary Dependencies**: **Add** `AWSSDK.SimpleEmailV2` 3.7.509.10. It shares `AWSSDK.Core [3.7.501.1, 4.0.0)` with the existing `AWSSDK.S3` 3.7.511.8 (research R1). **Remove** `SendGrid` 9.* and `SendGrid.Extensions.DependencyInjection` 1.*. Existing: FluentValidation via `AddValidatedOptions`, Serilog, xUnit + `Xunit.SkippableFact`.
**Storage**: N/A. No schema, migration or persistence changes.
**Testing**: xUnit unit tests (validator, guard, error mapper, adapter with a fake client factory); Stage-2 sandbox integration tests against the real SES API and mailbox simulator (main-push only); 008 startup-validation integration tests updated for the `Ses` section. No Testcontainers changes.
**Target Platform**: Unchanged (Cloud Run backend). SES is called over HTTPS from CI only. No deployed environment is wired to SES in this feature (spec Assumptions).
**Project Type**: Web service (backend only).
**Performance Goals**: None new. One SES call per email, the same shape as SendGrid.
**Constraints**: The adapter never throws (FR-002). Credentials, recipients and bodies are never logged (FR-009). The guard check runs before any client creation (FR-005). 95% coverage on changed/added relevant files (constitution §9).
**Scale/Scope**: About 20–60 sandbox sends a month; under $1/month (SC-003). Roughly 6 new files, 4 deleted, about 10 edited (see Project Structure).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Technology fit**: ✅ No stack change. SES is an external integration like Stripe and Twilio. The
  constitution doesn't mandate an email vendor. Sentry, Serilog, Docker and GitHub Actions are
  unchanged.
- **HOA tenancy**: ✅ N/A. No tenant data or queries change; callers already choose recipients
  within their own scope.
- **API contracts**: ✅ N/A. No endpoints are added or changed.
- **Security and operations**: ✅ Secrets live in GitHub secrets and are never committed or baked
  into images. The new `SesOptions` ships with `SesOptionsValidator` and fails fast at startup
  (constitution §8, "New configuration of any kind MUST ship with its validator"). Least-privilege
  IAM (`ses:SendEmail` on one identity ARN). Serilog warnings carry only sanitized reasons.
- **File storage**: ✅ N/A. `AWSSDK.S3` usage for R2/MinIO is untouched.
- **Caching/edge**: ✅ N/A.
- **Testing discipline**: ✅ Test-first for the guard, mapper and validator (red-green). xUnit
  `[Theory]` data covers recipient variants, every partial-config combination and each exception
  class. No persistence, so no Testcontainers work is needed. Sandbox tests are parallel-safe:
  simulator addresses are stateless and each test asserts only on its own send.
- **CI/CD and documentation**: ✅ Coverage: `SesEmailProvider`, `SimulatorRecipientGuard`,
  `SesErrorMapper` and `SesOptionsValidator` are in coverage scope and unit-tested. Only
  `SesClientFactory`, the one-line SDK constructor, is `[ExcludeFromCodeCoverage]`, matching
  `StripeGateway`. Sonar and Codecov are unchanged. The Repowise `domain=payments-alerts` marker
  region moves to the new adapter (see below).
- **Executable & living specs**: ✅ Every acceptance scenario maps to a test (mapping table in
  `contracts/ci-sandbox-ses.md` plus the unit tests listed in tasks). US4-2 and US4-3 are verified
  in review. Spec files 006, 007 and 013 are reconciled (research R10). This feature's `spec.md`
  and `tasks.md` are updated before the PR.
- **Spec independence & parallelism**: ✅ Independently completable; no sibling dependency.

**Gate result: PASS.** No violations, so Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/026-ses-email-provider/
├── spec.md
├── plan.md              # this file
├── research.md          # R1–R10 decisions
├── data-model.md        # SesOptions, guard, mapper
├── quickstart.md        # owner SES setup checklist (FR-014)
├── contracts/
│   ├── ses-email-adapter.md
│   └── ci-sandbox-ses.md
├── checklists/requirements.md
└── tasks.md             # /speckit.tasks (not created here)
```

### Source Code (repository root)

```text
HOAManagementCompany/
├── HOAManagementCompany.csproj                         # + AWSSDK.SimpleEmailV2; − SendGrid, SendGrid.Extensions.DependencyInjection
├── Program.cs                                          # swap options + provider registration; + ISesClientFactory; comment edits
├── appsettings.json                                    # SendGrid block → Ses block (_comment only)
├── Features/Payments/PaymentOptions.cs                 # − SendGridOptions; + SesOptions
├── Features/Auth/AuthNotifier.cs                       # comment: SendGrid → SES
└── Infrastructure/
    ├── Configuration/
    │   ├── SendGridOptionsValidator.cs                 # DELETE
    │   └── SesOptionsValidator.cs                      # NEW
    └── Payments/Alerts/
        ├── SendGridEmailProvider.cs                    # DELETE
        ├── SesEmailProvider.cs                         # NEW (adapter; in coverage)
        ├── SesClientFactory.cs                         # NEW (ISesClientFactory + thin SDK factory; excluded)
        ├── SimulatorRecipientGuard.cs                  # NEW (pure)
        └── SesErrorMapper.cs                           # NEW (pure)

HOAManagementCompany.Tests/
├── Fixtures/
│   ├── SandboxIntegrationTestBase.cs                   # SendGrid config + RequireSendGrid → Ses config + RequireSes
│   ├── SandboxResult.cs                                # + SkipIfUnavailable; doc comment
│   └── AlertTestBase.cs                                # doc comment: SendGrid → SES
├── Unit/
│   ├── Configuration/TwilioSendGridOptionsValidatorTests.cs  # RENAME → TwilioOptionsValidatorTests.cs (SendGrid theory removed)
│   ├── Configuration/SesOptionsValidatorTests.cs       # NEW
│   └── Alerts/
│       ├── SimulatorRecipientGuardTests.cs             # NEW
│       ├── SesErrorMapperTests.cs                      # NEW
│       └── SesEmailProviderTests.cs                    # NEW (fake ISesClientFactory)
└── Integration/
    ├── Configuration/StartupValidationTests.cs         # blank Ses:* instead of SendGrid:*; + partial-SES abort case
    └── Sandbox/
        ├── SendGridSandboxTests.cs                     # DELETE
        ├── SesSandboxTests.cs                          # NEW
        ├── SesHarnessTests.cs                          # NEW (no Sandbox trait; skip + guard-off harness checks)
        └── README.md                                   # SES invariant + quickstart link

.github/workflows/test.yml                              # integration-sandbox env: SendGrid__* → Ses__*; comment
specs/006-stripe-payments/spec.md                       # FR-016 reconciliation
specs/007-integration-ci-tests/spec.md                  # FR-016 reconciliation
specs/013-ephemeral-pr-envs/spec.md                     # FR-016 reconciliation
```

**Structure Decision**: Backend-only change inside the existing `Infrastructure/Payments/Alerts`
folder, next to `TwilioSmsProvider`, following the established "thin adapter plus pure logic" split.
Unit tests go under `HOAManagementCompany.Tests/Unit/Alerts/`, a new folder next to
`Unit/Configuration/`.

## Repowise Documentation

**Status**: Bootstrapped. The Repowise MCP server wasn't available in the planning session, so the
regions are planned from the existing marker in `SendGridEmailProvider.cs`. Regenerate with the
project Repowise workflow during implementation.

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `HOAManagementCompany/Infrastructure/Payments/Alerts/SesEmailProvider.cs` | `domain=payments-alerts` | Stage-2 no-deliver seam. Moves here from the deleted `SendGridEmailProvider.cs` and is rewritten for the `SimulatorOnly` guard |

### Marker syntax

```csharp
// <!-- REPOWISE:START domain=payments-alerts -->
// ... generated content ...
// <!-- REPOWISE:END -->
```

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

No constitution violations.

## Post-Design Constitution Check (re-evaluated after Phase 1)

- The design adds **no** new project, persistence, endpoint or frontend surface. **PASS.**
- **Coverage is better than today:** the SendGrid adapter was entirely excluded from coverage. The
  `ISesClientFactory` seam (research R6) brings the SES adapter's decision logic into coverage, so
  the safety guard is unit-proven, not just sandbox-proven. **PASS.**
- **Fail-fast config:** `SesOptionsValidator` covers region validity, sender format and credential
  pairing (data-model.md), with value-free messages. **PASS.**
- **Secrets hygiene:** no `ex.Message` passes to logs or `Error` strings (contract invariant). Env
  names avoid the SDK's default-chain variables (research R8). **PASS.**
- **Living spec:** the 006, 007 and 013 `spec.md` updates are planned (research R10). **PASS.**
