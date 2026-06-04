# NekoHOA API — Developer Quick Start

## Prerequisites

- .NET 9 SDK
- Docker Desktop
- Node.js 22+ (for Angular frontend)
- `dotnet-ef` CLI tool: `dotnet tool install -g dotnet-ef`

## First-Time Setup

```bash
# 1. Clone and enter the repo
cd HOAManagementCompany

# 2. Start infrastructure (PostgreSQL + MinIO)
docker compose up -d

# 3. Wait for postgres to be healthy (~5s), then apply migrations
dotnet ef database update --project HOAManagementCompany

# 4. Seed development data
dotnet run --project HOAManagementCompany -- --seed

# 5. Run the API
dotnet run --project HOAManagementCompany
```

The API will be available at:
- HTTP: http://localhost:5212
- Swagger UI: http://localhost:5212/swagger (Development only)
- Health check: http://localhost:5212/health

## Seeded Credentials

| Email | Password | Role |
|---|---|---|
| resident@nekohoa.dev | Password1! | Primary resident (SAKURA-001) |
| resident2@nekohoa.dev | Password1! | Secondary resident (SAKURA-002) |

## Running Tests

> Tests require Docker to spin up isolated Testcontainers.

```bash
dotnet test
```

## Project Structure

```
HOAManagementCompany/
├── Domain/
│   ├── Entities/       # EF Core entity classes
│   └── Enums/          # Strongly-typed enums
├── Features/
│   ├── Auth/           # Login, register, refresh, switch-property, me, logout
│   ├── Dashboard/      # GET /dashboard
│   ├── Payments/       # Ledger, one-time, recurring, drafts
│   ├── Property/       # Property info, owner, address history, directory fields
│   ├── Community/      # Announcements, poll, violations, events, documents
│   └── Common/         # GlobalExceptionHandler
├── Infrastructure/
│   ├── Persistence/    # ApplicationDbContext, migrations, DesignTimeFactory
│   └── Storage/        # IDocumentStorage, S3DocumentStorage, StorageOptions
├── Seed/               # DatabaseSeeder + sub-seeders (--seed flag)
└── Program.cs          # Bootstrap + middleware pipeline
```

## Environment Variables

| Key | Description | Default (Development) |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection | `Host=localhost;...` |
| `Jwt__Secret` | ≥32 char signing secret | dev placeholder |
| `Jwt__Issuer` | JWT iss claim | `nekohoa-api` |
| `Jwt__Audience` | JWT aud claim | `nekohoa-frontend` |
| `Storage__ServiceUrl` | MinIO/R2 endpoint | `http://localhost:9000` |
| `Storage__AccessKey` | S3 access key | `minioadmin` |
| `Storage__SecretKey` | S3 secret key | `minioadmin` |
| `Storage__BucketName` | Bucket name | `hoa-documents` |
| `Storage__ForcePathStyle` | MinIO path style | `true` |
| `Sentry__Dsn` | Sentry DSN (optional) | empty |

## Adding a New Endpoint

1. Create `Features/<Domain>/<EndpointName>.cs` inheriting `Endpoint<TReq, TResp>` or `EndpointWithoutRequest<TResp>`.
2. Implement `Configure()` with the HTTP verb, route, and `Description(x => x.WithName("OperationId"))`.
3. FastEndpoints auto-registers all endpoints via assembly scan — no additional wiring required.
