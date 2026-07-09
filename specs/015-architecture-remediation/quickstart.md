# Quickstart: Architecture Remediation (015)

How to run and verify each remediation slice locally. Baseline commands are unchanged from `CLAUDE.md` (backend from repo root; frontend from `neko-hoa/`).

## Prerequisites

- .NET 9 SDK; Docker (only for the integration tier); Node 20+ for the frontend.

## Fast, container-free loop (P5/P6)

```bash
dotnet test HOAManagementCompany.UnitTests    # pure unit + architecture tests, no Docker, target < 60 s
```

Architecture rules live in `HOAManagementCompany.UnitTests/Architecture/LayeringTests.cs`:
Infrastructure↛Features, Domain↛(Features|Infrastructure), `Stripe` only in `Infrastructure.Payments`, no cross-feature internals.

## Full backend suite (unchanged entry point)

```bash
dotnet test                                   # unit + integration (Testcontainers: postgres:17 + MinIO)
```

P1 verification: `HOAManagementCompany.Tests/Integration/Payments/WebhookAtomicityTests.cs` — interrupt-and-retry Theories per provider-event kind (success / failure / ACH return / refund / dispute), asserting exactly-once ledger effects and status/ledger agreement.

## Error-envelope checks (P2)

```bash
dotnet test --filter FullyQualifiedName~ErrorContract
```

Asserts every documented `code` returns `{ code, message }` at its intended status, and that a missing identity claim yields 403 `MISSING_CLAIM` (not a 500).

## Production backstop (P3)

```bash
ASPNETCORE_ENVIRONMENT=Production Startup__SeedData=true DevTools__E2ECleanupEnabled=true \
  dotnet run --project HOAManagementCompany   # must fail fast at startup config validation
```

Covered by `Tests/Integration/Configuration/StartupValidation` additions. Publish check: `dotnet publish HOAManagementCompany -c Release` output contains no `testdata/`.

## Client type generation + drift gate (P4)

```bash
cd neko-hoa
npm ci
npm run generate:api-types                    # exports OpenAPI via `dotnet run -- --export-openapi`, runs openapi-typescript
git diff --exit-code src/app/core/api/generated-types.ts   # non-empty diff = you forgot to commit regenerated types
npm run test:ci                               # Karma unit tests
```

## CI/CD split (P6)

- `ci.yml` — runs on PRs: backend+frontend verification, Sonar, Codecov, type-drift gate, Trivy (scan-only).
- `release.yml` — runs on push to `main`: image build/push, dev deploy, health gate, smoke, promote.
- Composite actions under `.github/actions/` (dotnet-setup, node-setup, gcloud-auth, existing `pr-env-tofu-init`).

## IaC shared core (P6)

```bash
cd infra/environments/dev && tofu init -backend=false && tofu validate
cd ../pr && tofu init -backend=false && tofu validate
```

Both roots resolve their Cloud Run/secrets/Neon resources through `infra/modules/cloud-run-service/`.

## Ledger consistency report (P1 / FR-005)

Runs on the existing reconcile schedule; manual trigger (Dev):

```bash
curl -X POST "$API/payments/jobs/reconcile" -H "X-Scheduler-Secret: $SECRET"
```

Findings appear as structured Serilog warnings (`LedgerInconsistencyFinding`) and a Sentry alert; report-only, no data mutation.
