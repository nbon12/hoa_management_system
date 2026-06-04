# HOA Management Company

<!-- REPOWISE:START section=overview -->
An HOA management portal with an Angular frontend (NekoHOA) and a .NET 9 FastEndpoints REST API backend. Residents can manage their property, view the ledger, pay assessments, participate in polls, and access community documents.
<!-- REPOWISE:END -->

## Technology Stack

<!-- REPOWISE:START section=tech-stack -->
- **Angular 19** — frontend (NekoHOA), served from `neko-hoa/`
- **.NET 9 / C# 13** — REST API with **FastEndpoints** (30 endpoints, `api/v1` prefix)
- **PostgreSQL 17** — primary data store via EF Core 9 + Npgsql
- **ASP.NET Core Identity** — password hashing + user store; custom JWT auth
- **MinIO (local) / Cloudflare R2 (production)** — document storage via AWSSDK.S3
- **Serilog** — structured logging; **Sentry** — error tracking and performance monitoring
- **xUnit + Testcontainers** — integration test suite
- **Repowise** — repository intelligence documentation
<!-- REPOWISE:END -->

## Quick Start

<!-- REPOWISE:START section=quickstart -->
See [`specs/003-dotnet-api-backend/quickstart.md`](specs/003-dotnet-api-backend/quickstart.md) for the full developer setup guide.

```bash
# 1. Start infrastructure
docker compose up -d

# 2. Seed the database
dotnet run --project HOAManagementCompany -- --seed

# 3. Run the API
dotnet run --project HOAManagementCompany

# 4. Swagger UI (dev only)
open http://localhost:5212/swagger
```
<!-- REPOWISE:END -->

## Documentation

<!-- REPOWISE:START section=documentation -->
- **API backend**: [`specs/003-dotnet-api-backend/`](specs/003-dotnet-api-backend/) — OpenAPI contract, data model, quickstart
- **Repowise**: [`repowise/generation-prompt.md`](repowise/generation-prompt.md) — marker region catalog; PR health gate via [`.github/workflows/repowise.yml`](.github/workflows/repowise.yml)
- **Frontend (canonical)**: [`neko-hoa/`](neko-hoa/) — Angular app; legacy `frontend/` is deprecated
- **Tests**: `PLAYWRIGHT_HEADLESS=true dotnet test --verbosity normal` from repo root
<!-- REPOWISE:END -->
