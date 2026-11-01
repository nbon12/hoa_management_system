# HOAManagementCompany.Tests

xUnit tests for the backend API, in `Integration/`, `Unit/`, `Performance/` and `Startup/`, with shared harness code in `Fixtures/` and `Factories/`.

## How tests get a database

No local PostgreSQL, test database or `dotnet ef` step is needed. Docker must be running.

- `Fixtures/TestDatabaseFixture.cs` starts throwaway `postgres:17` and MinIO containers with Testcontainers, applies the EF Core migrations and seeds test data (`Fixtures/TestDataSeeder.cs`).
- `Fixtures/IntegrationTestBase.cs` hosts the API in-process with `WebApplicationFactory<Program>` in the `Test` environment (`HOAManagementCompany/appsettings.Test.json`) and points it at those containers.
- Test parallelization is disabled (`AssemblyInfo.cs`).

## Running tests

From the repo root:

```bash
dotnet test --filter "Category!=Sandbox"   # what the CI `test` job runs
```

The `Sandbox` category runs the real Stripe/SES/Twilio adapters against provider test modes and needs test-mode secrets; see [`Integration/Sandbox/README.md`](Integration/Sandbox/README.md).

## CI

`.github/workflows/test.yml` runs on pushes and pull requests to `main` or `develop`:

- `test` job: `dotnet test --filter "Category!=Sandbox"` with `ASPNETCORE_ENVIRONMENT=Test`, plus coverage (Codecov, and a diff-coverage report on pull requests).
- `integration-sandbox` job (push to `main` only): `dotnet test --filter "Category=Sandbox"` with test-scoped provider secrets.
