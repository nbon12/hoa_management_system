# Developer Quickstart: NekoHOA .NET API

**Branch**: `003-dotnet-api-backend` | **Date**: 2026-05-24

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 9.x | `dotnet --version` |
| Docker Desktop | 4.x+ | Required for PostgreSQL + MinIO |
| Docker Compose | v2 (bundled with Desktop) | `docker compose version` |
| Rider or VS Code | Latest | C# Dev Kit for VS Code |

---

## Local Development Setup (< 5 minutes)

### 1. Start infrastructure

```bash
# From repository root
docker compose up -d
```

This starts:
- **PostgreSQL 17** on `localhost:5432` (db: `nekohoa`, user: `nekohoa`, password: `nekohoa`)
- **MinIO** on `localhost:9000` (console at `localhost:9001`, credentials: `minioadmin` / `minioadmin`)

### 2. Apply migrations + seed the database

```bash
cd HOAManagementCompany
dotnet run -- --seed
```

This will:
1. Apply all EF Core migrations to the PostgreSQL instance
2. Seed test residents, properties, payments, community data, and placeholder files in MinIO
3. Exit automatically when complete

Expected output:
```
[INFO] Applying migrations...
[INFO] Seeding users...
[INFO] Seeding properties...
[INFO] Seeding ledger history (12 months)...
[INFO] Seeding community data...
[INFO] Uploading placeholder documents to MinIO...
[INFO] Seed complete. 0 errors.
```

### 3. Start the API

```bash
cd HOAManagementCompany
dotnet run
```

The API is available at: **`http://localhost:5000/api/v1`**  
Swagger UI is available at: **`http://localhost:5000/swagger`**

---

## Test Accounts (seeded)

| Account | Email | Password | Property |
|---------|-------|----------|---------|
| Primary resident | `resident@nekohoa.dev` | `Password1!` | 714 Keystone Park Dr (SAKURA) |
| Secondary resident | `resident2@nekohoa.dev` | `Password1!` | Separate property (SAKURA) |

Use these credentials with `POST /api/v1/auth/login` to obtain a JWT access token.

---

## Quick Verification

After seeding, verify the key endpoints work:

```bash
# 1. Login
TOKEN=$(curl -s -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"resident@nekohoa.dev","password":"Password1!"}' \
  | jq -r '.token')

# 2. Dashboard
curl -s http://localhost:5000/api/v1/dashboard \
  -H "Authorization: Bearer $TOKEN" | jq .

# 3. Ledger
curl -s "http://localhost:5000/api/v1/payments/ledger?page=1&pageSize=5" \
  -H "Authorization: Bearer $TOKEN" | jq .total

# 4. Documents (pre-signed URL)
curl -s http://localhost:5000/api/v1/community/documents \
  -H "Authorization: Bearer $TOKEN" | jq '.[0].id' | \
  xargs -I{} curl -s http://localhost:5000/api/v1/community/documents/{}/download \
  -H "Authorization: Bearer $TOKEN" | jq .url
```

---

## Running Tests

```bash
# All tests
cd HOAManagementCompany.Tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Specific domain
dotnet test --filter "Category=Auth"
```

Tests use Testcontainers — Docker must be running. Each test run spins up isolated PostgreSQL + MinIO containers automatically.

---

## Re-seeding

The seeder is idempotent. Running it again on an already-seeded database is safe:

```bash
cd HOAManagementCompany
dotnet run -- --seed
```

---

## Environment Configuration

Key settings in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=nekohoa;Username=nekohoa;Password=nekohoa;Max Pool Size=20"
  },
  "Jwt": {
    "Secret": "dev-secret-change-in-production-must-be-32-chars-min",
    "Issuer": "nekohoa-api",
    "Audience": "nekohoa-frontend",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 30
  },
  "Storage": {
    "ServiceUrl": "http://localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "BucketName": "hoa-documents",
    "ForcePathStyle": true
  },
  "Sentry": {
    "Dsn": ""
  }
}
```

Production values are injected as environment variables (never committed).

---

## Docker Compose Services

```yaml
# docker-compose.yaml (relevant services)
services:
  postgres:
    image: postgres:17
    ports: ["5432:5432"]
    environment:
      POSTGRES_DB: nekohoa
      POSTGRES_USER: nekohoa
      POSTGRES_PASSWORD: nekohoa

  minio:
    image: minio/minio:latest
    ports: ["9000:9000", "9001:9001"]
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: minioadmin
    command: server /data --console-address ":9001"
```

The API itself is run locally via `dotnet run` (not in Docker during development). For production-like local testing, a `nekohoa-api` Docker service can be added to the compose file.

---

## Migrations

```bash
# Add a new migration
cd HOAManagementCompany
dotnet ef migrations add YYYYMMDD_Description

# Apply pending migrations manually
dotnet ef database update

# Roll back one migration
dotnet ef database update PreviousMigrationName
```

---

## Troubleshooting

| Problem | Solution |
|---------|---------|
| `Connection refused` on port 5432 | Run `docker compose up -d` first |
| `Connection refused` on port 9000 | MinIO not started; check `docker compose ps` |
| Seed fails with `ACCOUNT_ALREADY_CLAIMED` | Seed has already run; it is idempotent — this message means the check is working correctly and data already exists |
| `JWT validation failed` | Ensure `Jwt:Secret` in `appsettings.Development.json` is at least 32 characters |
| Tests fail with Docker errors | Ensure Docker Desktop is running before `dotnet test` |
