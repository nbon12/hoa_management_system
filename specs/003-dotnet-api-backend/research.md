# Research: Implement .NET Backend for NekoHOA API

**Branch**: `003-dotnet-api-backend` | **Date**: 2026-05-24  
**Phase**: 0 — All NEEDS CLARIFICATION items resolved

---

## 1. FastEndpoints for .NET 9

**Decision**: Use FastEndpoints 5.x as the exclusive endpoint framework.

**Rationale**: FastEndpoints is mandated by the project constitution and is purpose-built for high-throughput REST APIs. It outperforms MVC controllers significantly, integrates FluentValidation natively, supports endpoint grouping (shared base path + auth policy), and generates clean Swagger documentation via Swashbuckle. It produces the same `[Authorize]` / `[AllowAnonymous]` semantics but with less boilerplate per endpoint.

**Pattern — endpoint class**:
```csharp
public class LoginEndpoint : Endpoint<LoginRequest, AuthResponse>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Description(x => x.WithName("Login").WithTags("Auth"));
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct) { ... }
}
```

**Pattern — validator**:
```csharp
public class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}
```

**Pattern — endpoint group** (shared base path + JWT auth):
```csharp
public class PropertyGroup : Group
{
    public PropertyGroup() => Configure("/api/v1/property", ep => ep.RequireAuthorization());
}
```

**Base path**: All application endpoints use `/api/v1` prefix, set globally via `app.UseFastEndpoints(c => c.Endpoints.RoutePrefix = "api/v1")`.

**Alternatives considered**: Minimal API (`MapGet`, `MapPost`) — rejected because it lacks integrated validation and group-level auth; MVC controllers — rejected by constitution.

---

## 2. JWT Access + Refresh Token Architecture

**Decision**: Short-lived JWT access tokens (15 min, configurable) + opaque rotating refresh tokens stored as SHA-256 hashes in PostgreSQL.

**Rationale**: Stateless JWTs cannot be revoked mid-life; pairing with server-side refresh tokens allows effective logout (delete refresh token) and keeps access tokens short-lived to limit the blast radius of token theft. SHA-256 hashing of the refresh token at rest ensures a database breach does not expose usable tokens.

**Token flow**:
1. `POST /auth/login` or `POST /auth/register` → issue access token (JWT, 15 min) + refresh token (opaque GUID, stored as SHA-256 hash, 30 days).
2. Client stores refresh token in `HttpOnly` cookie or secure storage.
3. On access token expiry, client calls `POST /auth/refresh` with the refresh token → server validates hash, deletes old record, issues new access + refresh token pair (rotation).
4. `POST /auth/logout` → server deletes refresh token record; access token expires naturally within 15 minutes.

**JWT claims**:
- `sub` — `ApplicationUser.Id` (UUID)
- `email`
- `propertyId` — currently active property UUID (from `UserProperty`)
- `communityId` — community ID of the active property
- `jti` — token ID (for logging/tracing)
- `exp`, `iat`, `iss`, `aud`

**Refresh token table**:
```sql
RefreshTokens (
  Id          UUID PRIMARY KEY,
  UserId      UUID NOT NULL REFERENCES AspNetUsers(Id),
  TokenHash   VARCHAR(64) NOT NULL,   -- SHA-256 hex of plaintext token
  ExpiresAt   TIMESTAMPTZ NOT NULL,
  CreatedAt   TIMESTAMPTZ NOT NULL,
  RevokedAt   TIMESTAMPTZ NULL
)
```

**Alternatives considered**:
- Token blocklist — rejected (PostgreSQL table + lookup overhead on every request, grows unbounded without TTL cleanup).
- In-memory `HashSet` — rejected (state lost on restart, not suitable for Cloud Run cold starts).
- No server-side invalidation — rejected (spec requires logout to prevent subsequent token use; accepted via the short-lived window trade-off).

---

## 3. Property-Scoped JWT Claims and Multi-Property Support

**Decision**: Embed `propertyId` and `communityId` as custom JWT claims at login/register time (defaulting to the user's first `UserProperty`). A dedicated `POST /auth/switch-property` endpoint re-issues a full token pair scoped to a different property.

**Rationale**: Embedding the active property in the token means every scoped query can be resolved from `HttpContext.User.FindFirst("propertyId")` without an extra database lookup per request. This is the idiomatic approach for single-active-context APIs.

**Implementation pattern**:
```csharp
// In AuthService.CreateTokensAsync:
var claims = new[]
{
    new Claim(JwtRegisteredClaimNames.Sub, user.Id),
    new Claim(JwtRegisteredClaimNames.Email, user.Email!),
    new Claim("propertyId", property.Id.ToString()),
    new Claim("communityId", property.CommunityId),
    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
};
```

**`switch-property` endpoint**:
- Accepts `{ "propertyId": "uuid" }`.
- Verifies `UserProperty` record exists for current `sub` + requested `propertyId`.
- Returns HTTP 403 with `PROPERTY_ACCESS_DENIED` if not linked.
- Issues new access + refresh token pair with updated `propertyId` claim.

---

## 4. MinIO / Cloudflare R2 Pre-Signed URLs

**Decision**: Use `AWSSDK.S3` (the AWS SDK for .NET) against the S3-compatible MinIO API locally and Cloudflare R2 in production.

**Rationale**: MinIO and Cloudflare R2 both implement the S3 API. Using the same `AWSSDK.S3` client with environment-specific endpoint configuration means zero code change between local and production — only `appsettings` values differ.

**Configuration**:
```json
// appsettings.Development.json (MinIO)
"Storage": {
  "ServiceUrl": "http://localhost:9000",
  "AccessKey": "minioadmin",
  "SecretKey": "minioadmin",
  "BucketName": "hoa-documents",
  "ForcePathStyle": true
}

// appsettings.json / env vars (R2)
"Storage": {
  "ServiceUrl": "https://<account-id>.r2.cloudflarestorage.com",
  "AccessKey": "${R2_ACCESS_KEY}",
  "SecretKey": "${R2_SECRET_KEY}",
  "BucketName": "hoa-documents",
  "ForcePathStyle": false
}
```

**Pre-signed URL generation** (5-minute expiry):
```csharp
var request = new GetPreSignedUrlRequest
{
    BucketName = _options.BucketName,
    Key = document.StorageKey,
    Expires = DateTime.UtcNow.AddMinutes(5),
    Verb = HttpVerb.GET
};
string url = _s3Client.GetPreSignedURL(request);
```

**Seeder file upload**:
```csharp
await _s3Client.PutObjectAsync(new PutObjectRequest
{
    BucketName = _options.BucketName,
    Key = storageKey,           // e.g., "documents/2026/budget.pdf"
    ContentBody = "Placeholder content for " + docName,
    ContentType = "application/pdf"
});
```

**Alternatives considered**: Presign via MinIO.NET SDK — rejected (vendor-specific SDK; AWSSDK.S3 is compatible and avoids dual SDK dependency). Generating URLs in a background job — rejected (spec requires on-demand generation, URLs must never be stored).

---

## 5. Entity Framework Core 9 + PostgreSQL Patterns

**Decision**: Single `ApplicationDbContext` extending `IdentityDbContext<ApplicationUser>`. `IDbContextFactory<ApplicationDbContext>` registered for the seeder; scoped `ApplicationDbContext` for request handlers.

**Rationale**: EF Core 9 + Npgsql supports all required query patterns. `IDbContextFactory` is needed for the seeder (which runs outside the DI request scope). Per-request scoped `DbContext` is the standard pattern for FastEndpoints handlers.

**Key configuration**:
```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString,
        npgsql => npgsql.EnableRetryOnFailure(3)));

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
```

**Migrations**: Auto-applied on startup in Development and Staging; gated behind an environment check in Production (manual apply via CI). Migration naming: `YYYYMMDD_Description`.

**Neon connection pooling**: `Max Pool Size=20;Minimum Pool Size=1;Connection Idle Lifetime=30` in the connection string.

**Alternatives considered**: Dapper for queries — rejected (EF Core sufficient for all query shapes; adds consistency with existing project migrations). Repository pattern — not introduced (FastEndpoints handlers access `DbContext` directly via DI, which keeps vertical slices self-contained per the spec's simplicity goals).

---

## 6. Testcontainers Strategy

**Decision**: Shared `TestDatabaseFixture` (xUnit `IAsyncLifetime`) that spins up one PostgreSQL 17 container and one MinIO container per test run, shared across all integration tests in the assembly. Each test method wraps in a transaction that is rolled back after the test.

**Rationale**: Starting one container per test class is too slow; sharing across the assembly with per-test transaction rollback provides both speed and isolation.

**Implementation sketch**:
```csharp
public class TestDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17").Build();
    private readonly MinioContainer _minio = new MinioBuilder().Build();

    public string ConnectionString => _postgres.GetConnectionString();
    public string MinioEndpoint => _minio.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _minio.StartAsync();
        // apply migrations
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _minio.DisposeAsync();
    }
}
```

**Per-test isolation**:
```csharp
public abstract class IntegrationTestBase(TestDatabaseFixture fixture)
{
    protected async Task<IDbContextTransaction> BeginIsolatedAsync()
        => await fixture.DbContext.Database.BeginTransactionAsync();
}
```

---

## 7. FastEndpoints + Swashbuckle OpenAPI Generation

**Decision**: Use `FastEndpoints.Swagger` NuGet package which wraps Swashbuckle and generates OpenAPI docs from FastEndpoints endpoint metadata.

**Configuration** (Development only):
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();   // FastEndpoints.Swagger
}
```

**OperationId**: Set via `Description(x => x.WithName("operationId"))` in each endpoint's `Configure()`, matching the `operationId` values in `openapi.yaml`.

---

## 8. Seeder CLI Flag Pattern

**Decision**: Check `args` for `--seed` before `builder.Build()` in `Program.cs`. If present, build the host, run the seeder, and exit without calling `app.Run()`.

```csharp
if (args.Contains("--seed"))
{
    if (!builder.Environment.IsDevelopment())
    {
        Console.Error.WriteLine("ERROR: Seeder is restricted to the Development environment.");
        return 1;
    }
    var app = builder.Build();
    await app.Services.GetRequiredService<DatabaseSeeder>().SeedAsync();
    return 0;
}

var app = builder.Build();
// ... middleware pipeline ...
app.Run();
```

**Idempotency**: Each seeder checks for the existence of a well-known record (e.g., `resident@nekohoa.dev` user) before inserting. Upsert semantics via `ExecuteUpdateAsync` or `AddOrUpdate` pattern.

---

## 9. Serilog + Sentry Configuration

**Serilog**:
```csharp
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}: {Message}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName));
```

**Sentry** (never log PII or payment credentials):
```csharp
builder.WebHost.UseSentry(o =>
{
    o.Dsn = builder.Configuration["Sentry:Dsn"];
    o.Environment = builder.Environment.EnvironmentName;
    o.TracesSampleRate = 0.2;
    o.BeforeSend = @event =>
    {
        // strip sensitive fields
        @event.Request?.Data?.Remove("cardNumber");
        @event.Request?.Data?.Remove("cardCvv");
        @event.Request?.Data?.Remove("routingNumber");
        return @event;
    };
});
```

---

## 10. Rate Limiting

**Decision**: Use ASP.NET Core's built-in `RateLimiter` middleware (available from .NET 7+). Apply a fixed-window rate limiter to payment endpoints and a stricter limiter to auth endpoints.

**Policy**:
- Auth endpoints (`/auth/login`, `/auth/register`, `/auth/refresh`): 10 requests / 1 minute per IP
- Payment endpoints (`/payments/one-time`, `/payments/recurring`): 20 requests / 1 minute per authenticated user

```csharp
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("auth", opts =>
    {
        opts.PermitLimit = 10;
        opts.Window = TimeSpan.FromMinutes(1);
        opts.QueueLimit = 0;
    });
    o.AddFixedWindowLimiter("payments", opts =>
    {
        opts.PermitLimit = 20;
        opts.Window = TimeSpan.FromMinutes(1);
    });
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

---

## All NEEDS CLARIFICATION Items — Resolved

| Item | Resolution |
|------|-----------|
| Project structure (Blazor vs fresh webapi) | Replace existing project with `dotnet new webapi`; same `.sln` reference |
| JWT logout invalidation | Short-lived access tokens (15 min) + rotating refresh tokens in PostgreSQL |
| Multi-property user linkage | `UserProperty` join table; `propertyId` JWT claim; `POST /auth/switch-property` |
| Seeder invocation | `dotnet run -- --seed` CLI flag |
| MinIO for local dev | Required `docker-compose.yaml` service; real pre-signed URLs |
| Seeder MinIO content | Seeder uploads placeholder files; end-to-end download works |
| Property assignment at registration | `accountNumber` field in `RegisterRequest`; `ACCOUNT_NOT_FOUND` / `ACCOUNT_ALREADY_CLAIMED` errors |
| Registration response shape | Returns full token pair (access + refresh) + user profile |
