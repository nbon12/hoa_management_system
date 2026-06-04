# Implementation Plan: Implement .NET Backend for NekoHOA API

**Branch**: `003-dotnet-api-backend` | **Date**: 2026-05-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-dotnet-api-backend/spec.md`

---

## Summary

Replace the existing Blazor/Razor `HOAManagementCompany` project with a clean `.NET 9` **FastEndpoints** REST API that fulfils the `openapi.yaml` contract. The API exposes **30 endpoints** across 6 domains (Auth, Dashboard, Payments, Property, Community, plus 2 new Auth endpoints added by clarification). All resident-facing data is scoped by a `propertyId` claim embedded in short-lived JWT access tokens (15-min expiry) paired with rotating PostgreSQL-backed refresh tokens (30-day expiry). Document file storage uses **MinIO** locally and **Cloudflare R2** in production; the seeder uploads real placeholder files so pre-signed download URLs work end-to-end. A CLI seeder (`dotnet run -- --seed`) populates the development database idempotently and is restricted to the Development environment.

---

## Technical Context

**Language/Version**: C# 13 / .NET 9  
**Primary Dependencies**:
- `FastEndpoints` 5.x — all application endpoints (constitution mandate)
- `ASP.NET Core Identity` — `UserManager<ApplicationUser>` for password hashing and user store only; no cookie auth, no Identity UI
- `Microsoft.EntityFrameworkCore` 9.x + `Npgsql.EntityFrameworkCore.PostgreSQL` 9.x
- `Microsoft.AspNetCore.Authentication.JwtBearer` — JWT middleware
- `AWSSDK.S3` — MinIO (local) and Cloudflare R2 (hosted) via S3-compatible API
- `Serilog` + `Serilog.AspNetCore` + `Serilog.Sinks.Console`
- `Sentry.AspNetCore`
- `Swashbuckle.AspNetCore` (Development environment only)
- `FluentValidation` (bundled with FastEndpoints)
- `xUnit` 2.x + `Testcontainers` for .NET (PostgreSQL + MinIO containers)
- `Repowise` — repository intelligence documentation; indexed via MCP/GitHub Actions workflow; outputs maintained in marker regions across `README.md` and source files

**Storage**: PostgreSQL — Docker Compose locally (`postgres:17`), Neon in staging/production  
**Testing**: xUnit 2.x, Testcontainers .NET, per-test transaction rollback isolation, xUnit Theories for data-varied cases  
**Target Platform**: Docker container (Linux/amd64) on Google Cloud Run  
**Project Type**: REST API (web service)  
**Performance Goals**:
- Auth round-trip (register → login → /me → logout): < 500 ms
- Dashboard (`GET /dashboard`): < 300 ms for 24 months of ledger history
- One-time payment confirmation: < 2 s
- Seed command: < 30 s on a standard developer machine

**Constraints**:
- Access token expiry: 15 minutes (configurable via `Jwt:AccessTokenExpiryMinutes`)
- Refresh token expiry: 30 days (configurable via `Jwt:RefreshTokenExpiryDays`)
- `pageSize` maximum: 200; default: 50
- `draftDay` range: 1–28
- Seeder restricted to `ASPNETCORE_ENVIRONMENT=Development`
- No real payment gateway — ACH and card payments are simulated server-side
- Sensitive fields (card numbers, CVV, routing numbers) must never appear in logs
- PRs must include regenerated Repowise outputs in all marker regions (no-op commit if nothing changed)

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Rule | Status | Notes |
|------|--------|-------|
| FastEndpoints for all application endpoints | ✅ Pass | Used throughout — no MVC controllers for application endpoints |
| PostgreSQL + Neon | ✅ Pass | PostgreSQL via EF Core/Npgsql; Neon in hosted environments |
| Auth0 authentication | ⚠️ Justified deviation | See Complexity Tracking #1 |
| Docker + Docker Hub | ✅ Pass | Dockerized; `docker-compose.yaml` updated with all services |
| Cloud Run target | ✅ Pass | Linux/amd64 container |
| Cloudflare R2 (hosted file storage) | ✅ Pass | R2 in production; MinIO locally |
| MinIO (local/CI file storage) | ✅ Pass | Required service in `docker-compose.yaml` |
| Sentry error tracking | ✅ Pass | `Sentry.AspNetCore` configured in `Program.cs` |
| Swashbuckle (dev only) | ✅ Pass | Swagger UI at `/swagger`, disabled in production |
| GitHub Actions CI/CD | ✅ Pass | Existing workflow extended |
| HOA tenancy (`communityId` boundary) | ✅ Pass | All community-scoped entities carry `CommunityId`; `propertyId` JWT claim scopes resident data |
| API contract (pagination `limit`/`offset`) | ⚠️ Justified deviation | See Complexity Tracking #2 |
| Secrets externalized | ✅ Pass | `appsettings.Development.json` + env vars; no secrets committed |
| Serilog structured logging | ✅ Pass | Configured in `Program.cs`; payment credentials excluded |
| Sentry trace propagation | ✅ Pass | `TracesSampleRate` set; PII excluded from events |
| PostgreSQL Testcontainers for integration tests | ✅ Pass | `TestDatabaseFixture` spins up PostgreSQL + MinIO containers |
| Test-first (red-green) | ✅ Pass | Acceptance criteria drive test authoring before implementation |
| xUnit Theories for data-varied tests | ✅ Pass | Validation, pagination, and auth failure cases use `[Theory]` |
| Per-test transaction rollback isolation | ✅ Pass | All DB tests wrap in `BeginTransactionAsync` + `RollbackAsync` |
| Repowise PR documentation updates | ✅ Planned | Repowise bootstrapped in this feature; marker regions added to `README.md` + source; GitHub Actions workflow runs Repowise on every PR; refreshed outputs committed before merge |

---

## Project Structure

### Documentation (this feature)

```text
specs/003-dotnet-api-backend/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/           ← Phase 1 output
│   ├── README.md
│   └── auth-contract-additions.md
└── tasks.md             ← Phase 2 output (/speckit.tasks)
```

### Source Code (repository)

```text
HOAManagementCompany/                    # main API project (replaced with dotnet new webapi)
├── Program.cs                           # app bootstrap, DI, middleware pipeline
├── appsettings.json
├── appsettings.Development.json
├── HOAManagementCompany.csproj
│
├── Features/                            # FastEndpoints vertical slices
│   ├── Auth/
│   │   ├── RegisterEndpoint.cs
│   │   ├── LoginEndpoint.cs
│   │   ├── LogoutEndpoint.cs
│   │   ├── RefreshEndpoint.cs
│   │   ├── MeEndpoint.cs
│   │   ├── SwitchPropertyEndpoint.cs
│   │   └── Models/                      # request/response DTOs for Auth
│   ├── Dashboard/
│   │   ├── DashboardEndpoint.cs
│   │   └── Models/
│   ├── Payments/
│   │   ├── LedgerEndpoint.cs
│   │   ├── OneTimePaymentEndpoint.cs
│   │   ├── RecurringGetEndpoint.cs
│   │   ├── RecurringUpsertEndpoint.cs
│   │   ├── RecurringDeleteEndpoint.cs
│   │   ├── DraftsEndpoint.cs
│   │   └── Models/
│   ├── Property/
│   │   ├── PropertyEndpoint.cs
│   │   ├── OwnerGetEndpoint.cs
│   │   ├── OwnerPatchEndpoint.cs
│   │   ├── AddressHistoryEndpoint.cs
│   │   ├── DirectoryFieldsEndpoint.cs
│   │   ├── DirectoryFieldPatchEndpoint.cs
│   │   └── Models/
│   └── Community/
│       ├── Announcements/
│       ├── Poll/
│       ├── Violations/
│       ├── Events/
│       ├── Documents/
│       └── Models/
│
├── Domain/
│   ├── Entities/                        # EF Core entity classes
│   │   ├── ApplicationUser.cs           # extends IdentityUser
│   │   ├── UserProperty.cs
│   │   ├── RefreshToken.cs
│   │   ├── Property.cs
│   │   ├── Owner.cs
│   │   ├── AddressHistory.cs
│   │   ├── DirectoryField.cs
│   │   ├── LedgerEntry.cs
│   │   ├── RecurringPayment.cs
│   │   ├── DraftEntry.cs
│   │   ├── Announcement.cs
│   │   ├── Poll.cs
│   │   ├── PollOption.cs
│   │   ├── PollVote.cs
│   │   ├── Violation.cs
│   │   ├── CalendarEvent.cs
│   │   ├── EventRsvp.cs
│   │   ├── HoaDocument.cs
│   │   └── CommunityExpense.cs
│   └── Enums/
│       ├── LedgerEntryType.cs
│       ├── ViolationStatus.cs
│       ├── ViolationCategory.cs
│       ├── RecurringAmountType.cs
│       ├── PaymentMethod.cs
│       ├── DraftStatus.cs
│       ├── AnnouncementCategory.cs
│       ├── EventCategory.cs
│       └── DocumentCategory.cs
│
├── Infrastructure/
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs
│   │   ├── DesignTimeDbContextFactory.cs
│   │   └── Migrations/
│   └── Storage/
│       ├── IDocumentStorage.cs
│       └── S3DocumentStorage.cs         # handles both MinIO + R2 via AWSSDK.S3
│
└── Seed/
    ├── DatabaseSeeder.cs                # orchestrates all seeders
    ├── AuthSeeder.cs
    ├── PropertySeeder.cs
    ├── PaymentSeeder.cs
    ├── CommunitySeeder.cs
    └── StorageSeeder.cs                 # uploads placeholder files to MinIO

HOAManagementCompany.Tests/
├── HOAManagementCompany.Tests.csproj
├── Fixtures/
│   ├── TestDatabaseFixture.cs           # Testcontainers PostgreSQL + MinIO
│   └── IntegrationTestBase.cs           # transaction isolation helpers
├── Factories/                           # test data factories
│   ├── UserFactory.cs
│   ├── PropertyFactory.cs
│   └── ...
├── Integration/
│   ├── Auth/
│   ├── Dashboard/
│   ├── Payments/
│   ├── Property/
│   └── Community/
└── Unit/
    ├── Payments/
    └── Auth/

docker-compose.yaml                      # updated: api, postgres, minio
README.md                                # Repowise marker regions (see Repowise section below)
.github/
└── workflows/
    ├── test.yml                         # existing — extended with Sonar, Codecov
    └── repowise.yml                     # NEW — Repowise documentation regeneration on PRs
```

**Structure Decision**: Single-solution approach — `HOAManagementCompany` API project (replaced with `dotnet new webapi`) and `HOAManagementCompany.Tests` project within the existing `HOAManagementCompany.sln`. Feature code is organized as FastEndpoints vertical slices under `Features/`. Domain entities live in `Domain/Entities/`. Infrastructure concerns (DbContext, storage) are in `Infrastructure/`. The seeder is a `Seed/` sub-namespace within the API project, invoked via `--seed` CLI flag.

---

## Repowise Documentation

**Status**: Bootstrapped in this feature (no prior Repowise configuration exists in the repository).

### What Repowise indexes

Repowise scans the codebase and generates structured documentation about ownership, architecture, API contracts, and key decisions. It emits its output into **marker regions** — specially delimited comment blocks — which are committed to the repository so the documentation stays co-located with the code and stays current on every PR.

### Marker regions for this feature

The following files will contain Repowise-maintained marker regions after this feature is implemented:

| File | Region purpose |
|------|---------------|
| `README.md` | Project overview, technology stack, quick-start summary |
| `HOAManagementCompany/Program.cs` | Middleware pipeline and DI registration notes |
| `HOAManagementCompany/Features/` (per domain) | Endpoint ownership and domain boundaries |
| `HOAManagementCompany/Domain/Entities/` | Entity relationship summary |
| `HOAManagementCompany/Infrastructure/Persistence/ApplicationDbContext.cs` | Schema ownership |

### Marker region format

Repowise uses XML-style comment markers (exact syntax is project-configured):

```csharp
// <!-- REPOWISE:START domain=auth -->
// ... generated documentation content ...
// <!-- REPOWISE:END -->
```

```markdown
<!-- REPOWISE:START section=overview -->
... generated content ...
<!-- REPOWISE:END -->
```

### GitHub Actions workflow

A dedicated `.github/workflows/repowise.yml` workflow runs Repowise on every pull request and commits any changed marker region outputs back to the PR branch before the merge check. If no marker content changes, the step is a no-op (no commit created).

```yaml
# .github/workflows/repowise.yml (outline)
name: Repowise Documentation
on:
  pull_request:
    branches: [main]
jobs:
  repowise:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Repowise
        uses: repowise/action@v1          # exact action reference per Repowise docs
        with:
          token: ${{ secrets.GITHUB_TOKEN }}
      - name: Commit updated docs
        run: |
          git config user.name "repowise-bot"
          git config user.email "repowise@users.noreply.github.com"
          git diff --quiet || (git add -A && git commit -m "chore: regenerate Repowise outputs")
          git push
```

### PR requirement

Every pull request in this branch (and all future PRs touching backend code) **MUST** include a Repowise run. The PR checklist item is:

> ✅ Repowise outputs regenerated (or confirmed unchanged) in all marker regions.

This is enforced as a required GitHub Actions check on the `main` branch.

---

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| **Auth0 → ASP.NET Core Identity + custom JWT** | The `openapi.yaml` contract defines `/auth/register`, `/auth/login`, `/auth/logout`, `/auth/refresh`, and `/auth/switch-property` as first-class API endpoints. These are incompatible with Auth0's token issuance model, which issues tokens from Auth0's own domain and does not expose registration/login as application-owned REST endpoints. | Adopting Auth0 would require removing all `/auth/*` endpoints and replacing them with an OAuth 2.0 PKCE flow, fundamentally breaking the frontend contract and requiring a parallel frontend change outside this feature's scope. Deviation is locked by the fixed external `openapi.yaml` contract. |
| **`page`/`pageSize` pagination instead of `limit`/`offset`** | The `openapi.yaml` contract explicitly defines reusable `PageParam` (`page`, min 1, default 1) and `PageSizeParam` (`pageSize`, min 1, max 200, default 50) parameters used across all paginated endpoints. | Changing pagination to `limit`/`offset` would break the Angular frontend's existing `MockDataService` and all consumers of the contract. The external contract is fixed for this feature. A future amendment can align the constitution if `page`/`pageSize` is adopted project-wide. |
