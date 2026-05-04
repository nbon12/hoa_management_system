# Quickstart: Dashboard and Violations Summary (001)

**Feature**: 001-dashboard-violations-summary  
**Branch**: `001-dashboard-violations-summary`

## Prerequisites

- .NET 9 SDK
- PostgreSQL (local or configured connection string)
- Okta or ASP.NET Core Identity configured (per project)

## Build

From repository root:

```bash
dotnet build
```

## Run

From repository root:

```bash
dotnet run --project HOAManagementCompany
```

Or from `HOAManagementCompany`:

```bash
dotnet run
```

Ensure appsettings (or environment) has the correct PostgreSQL connection string and any auth settings.

## Test

Per workspace rules and constitution:

- **Unit / integration**: `PLAYWRIGHT_HEADLESS=true dotnet test --verbosity normal`
- **UI (Playwright)**: Tests use namespaced data; do not rely on a clean DB. Create and dispose data unique to the test run.

From repository root:

```bash
PLAYWRIGHT_HEADLESS=true dotnet test --verbosity normal
```

## Feature artifacts

| Artifact | Path |
|----------|------|
| Spec | [spec.md](./spec.md) |
| Plan | [plan.md](./plan.md) |
| Research | [research.md](./research.md) |
| Data model | [data-model.md](./data-model.md) |
| API contracts | [contracts/dashboard-api.md](./contracts/dashboard-api.md), [contracts/my-violations-api.md](./contracts/my-violations-api.md) |
| Tasks | [tasks.md](./tasks.md) (created by `/speckit.tasks`) |

## Implementation checklist (high level)

1. **Data model**: Add Property entity and Violation.PropertyId; migration(s). Existing violations can be erased (no production DB; early development).
2. **Backend**: Dashboard summary endpoint (open violation count + placeholders); My Violations list endpoint (paginated, scoped to current user). On failure, return user-friendly message "Failed to load violation count".
3. **Frontend (Angular in `frontend/`)**: Dashboard page with 2×2 grid; Violations box with loading state, count, link to My Violations; My Violations page with paginated list; placeholders for other three boxes.
4. **Tests**: Unit tests for count/list logic; integration tests for API and DB; Playwright for login → dashboard → My Violations flow (namespaced data).
