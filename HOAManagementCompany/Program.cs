// <!-- REPOWISE:START domain=bootstrap -->
// Middleware pipeline and DI registration — HOAManagementCompany API.
// Observability wiring lives here: Serilog (Console + OTLP/JSON sink, trace/span +
// user-GUID enrichment), Sentry-on-OTel (consumes the OpenTelemetry activity pipeline
// with an independent trace sample rate), and builder.AddObservability() (OTel tracing/
// metrics → OTLP, scrubbing, sampling). Telemetry-init is guarded as non-fatal (FR-008).
// All strongly-typed options groups are bound via AddValidatedOptions(...).ValidateOnStart(),
// so invalid configuration fails the host at startup with a clear error (008-config-validation).
// Startup behavior (apply-migrations / seed / Swagger) and CORS allowed-origins are
// configuration-driven via StartupOptions and Cors:AllowedOrigins (009-dev-auto-deploy), so a
// deployed `Dev` service migrates+seeds and exposes Swagger while Production stays locked down.
// <!-- REPOWISE:END -->

using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Amazon.Runtime;
using Amazon.S3;
using FastEndpoints;
using FastEndpoints.Swagger;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Observability;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.RateLimiting;
using HOAManagementCompany.Infrastructure.Storage;
using Sentry.OpenTelemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Npgsql;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

// ── Startup behavior (config-driven; replaces hardcoded IsDevelopment() gating) ──────────────
// Drives migrations/seed/Swagger from the "Startup" section so a deployed `Dev` service behaves
// correctly without running as `Development` (which would leak dev error pages and localhost CORS).
// Defaults preserve existing local Development behavior. See StartupOptions (009-dev-auto-deploy).
var startupOptions = StartupOptions.Resolve(builder.Configuration, builder.Environment);

// ── Serilog ────────────────────────────────────────────────────────────────
// 3-arg form so DI-registered ILogEventSink/ILogEventEnricher (e.g. the integration
// test in-memory sink, the scrubbing + trace enrichers) are composed in via
// ReadFrom.Services. The OTLP sink ships structured logs to the dashboard/vendor; the
// human-readable Console sink is kept for local DX (FR-019/FR-020).
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
        .Enrich.FromLogContext()
        .Enrich.With(new ActivityTraceEnricher()) // trace_id/span_id on every record (FR-003).
        .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName);

    // Tests use the in-memory sink only — no external OTLP egress in the test path.
    if (!ctx.HostingEnvironment.IsEnvironment("Test"))
    {
        var obs = ObservabilityOptions.FromConfiguration(ctx.Configuration, ctx.HostingEnvironment);
        cfg.WriteTo.OpenTelemetry(o =>
        {
            o.Endpoint = $"{obs.OtlpEndpoint.TrimEnd('/')}/v1/logs";
            o.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
            o.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = obs.ServiceName };
            if (!string.IsNullOrWhiteSpace(obs.OtlpHeaders))
                foreach (var pair in obs.OtlpHeaders.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var idx = pair.IndexOf('=');
                    if (idx > 0) o.Headers[pair[..idx].Trim()] = pair[(idx + 1)..].Trim();
                }
        });
    }
});

// ── Sentry (consumes the OpenTelemetry pipeline — Sentry-on-OTel, FR-023) ────
var sentryTraceSampleRate = builder.Configuration.GetValue<double?>("Observability:SentryTraceSampleRatio") ?? 0.2;
builder.WebHost.UseSentry();
builder.Services.Configure<Sentry.AspNetCore.SentryAspNetCoreOptions>(o =>
{
    o.Dsn = builder.Configuration["Sentry:Dsn"] ?? "";
    o.Environment = builder.Environment.EnvironmentName;
    // Sentry keeps its OWN trace sample rate so its quota is decoupled from OTel's 100%
    // default; error capture stays unconditional (FR-013/FR-023/FR-027).
    o.TracesSampleRate = sentryTraceSampleRate;
    o.UseOpenTelemetry();
    // Broaden the before-send scrub to the FR-009 field set (adds emails, names, etc.).
    var scrubKeys = new[]
    {
        "password", "token", "cardNumber", "cardCvv",
        "routingNumber", "accountNumber", "email", "fullName"
    };
    o.SetBeforeSend((sentryEvent, _) =>
    {
        if (sentryEvent.Request?.Data is IDictionary<string, object?> data)
        {
            foreach (var key in data.Keys.ToList())
                if (scrubKeys.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase)))
                    data.Remove(key);
        }
        return sentryEvent;
    });
});

// ── Database ───────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

// One traced data source shared by both registrations so DB spans carry SQL text (FR-004).
// Registered via a factory so the DI container owns and disposes its connection pool.
builder.Services.AddSingleton(_ => ObservabilityNpgsql.BuildTracedDataSource(connectionString));

// ConfigureWarnings ignores ManyServiceProvidersCreatedWarning: it fires only when 20+ EF internal
// providers are built in one process — a WebApplicationFactory test artifact (many factories per
// run), not a real issue here since the app uses one shared NpgsqlDataSource singleton. Matches the
// suppression IntegrationTestBase already applies, so test factories booting Program don't throw.
builder.Services.AddDbContext<ApplicationDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.EnableRetryOnFailure(3))
     .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)));

builder.Services.AddDbContextFactory<ApplicationDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.EnableRetryOnFailure(3))
     .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)), ServiceLifetime.Scoped);

// ── ASP.NET Core Identity ──────────────────────────────────────────────────
builder.Services.AddIdentityCore<ApplicationUser>(o =>
{
    o.Password.RequireDigit = true;
    o.Password.RequireUppercase = true;
    o.Password.RequireNonAlphanumeric = true;
    o.Password.RequiredLength = 8;
    o.User.RequireUniqueEmail = true;
    // 016-A FR-A4: per-account lockout (10 failed attempts → 30-minute lock, config-driven).
    o.Lockout.AllowedForNewUsers = true;
    o.Lockout.MaxFailedAccessAttempts = builder.Configuration.GetValue("Identity:Lockout:MaxFailedAttempts", 10);
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Identity:Lockout:LockoutMinutes", 30));
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── JWT Bearer ─────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            // 016-A FR-A7: pin the signing algorithm (symmetric HS256) and tighten clock skew.
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();

// ── Validated configuration (008-config-validation) ─────────────────────────
// Every strongly-typed options group is bound AND validated at startup via FluentValidation
// (FluentValidateOptions<T>) + ValidateOnStart, so misconfiguration fails the host fast with a
// clear Section:Field error instead of surfacing later mid-request (FR-001/FR-013). Validation
// is uniform across environments; local/CI satisfy secret presence with placeholder values.
builder.Services.AddValidatedOptions<StorageOptions, StorageOptionsValidator>(
    builder.Configuration, "Storage");
builder.Services.AddValidatedOptions<StripeOptions, StripeOptionsValidator>(
    builder.Configuration, StripeOptions.SectionName);
builder.Services.AddValidatedOptions<PaymentsOptions, PaymentsOptionsValidator>(
    builder.Configuration, PaymentsOptions.SectionName);
builder.Services.AddValidatedOptions<JobsOptions, JobsOptionsValidator>(
    builder.Configuration, JobsOptions.SectionName);
builder.Services.AddValidatedOptions<TwilioOptions, TwilioOptionsValidator>(
    builder.Configuration, TwilioOptions.SectionName);
builder.Services.AddValidatedOptions<SendGridOptions, SendGridOptionsValidator>(
    builder.Configuration, SendGridOptions.SectionName);
builder.Services.AddValidatedOptions<ObservabilityOptions, ObservabilityOptionsValidator>(
    builder.Configuration, ObservabilityOptions.SectionName);
builder.Services.AddValidatedOptions<RateLimitingOptions, RateLimitingOptionsValidator>(
    builder.Configuration, RateLimitingOptions.SectionName);
// DevTools toggles are config-gated (not host-name-gated) so they evaluate correctly in the deployed
// `Dev` environment; defaults derive from IsDevLike(env) and are forced off in Production (014 US3).
builder.Services.AddValidatedOptions<DevToolsOptions, DevToolsOptionsValidator>(
    builder.Configuration, DevToolsOptions.SectionName, o => o.ApplyEnvironmentDefaults(builder.Environment));

// The host environment name itself must be one of the known set — fail fast on a mis-set
// ASPNETCORE_ENVIRONMENT (e.g. "prod" instead of "Production", or deployed-"Dev" vs local
// "Development"). The 010 IaC sets this on Cloud Run (FR-006/FR-030); this guards it (constitution §8).
builder.Services.AddValidatedHostEnvironment(builder.Environment);

// ── S3 / MinIO ─────────────────────────────────────────────────────────────
// Resolve the validated StorageOptions (guaranteed non-null/valid post ValidateOnStart) rather
// than a raw, possibly-null bind (FR-011).
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var storageOpts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    var config = new AmazonS3Config
    {
        ServiceURL = storageOpts.ServiceUrl,
        ForcePathStyle = storageOpts.ForcePathStyle,
        UseHttp = storageOpts.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
        // Cloudflare R2 does not implement the SDK's default streaming-trailer checksum
        // ("STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER not implemented" → 501), which silently
        // breaks every PutObject in deployed envs. WhenRequired returns the SDK to a normal,
        // SigV4-signed (x-amz-content-sha256) PUT — integrity is still covered by the signed
        // payload hash + TLS. MinIO tolerates either, so local behaviour is unchanged.
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        // R2 expects SigV4 region "auto"; the SDK can't infer a region from a non-AWS ServiceURL.
        AuthenticationRegion = "auto",
    };
    return new AmazonS3Client(
        new BasicAWSCredentials(storageOpts.AccessKey, storageOpts.SecretKey),
        config);
});
builder.Services.AddScoped<IDocumentStorage, S3DocumentStorage>();

// ── Rate Limiting ──────────────────────────────────────────────────────────
// Per-client limiting (014-post-deploy-hardening): the `auth` and `payments` policies are
// PARTITIONED so one abusive client cannot throttle the whole user base (the prior global,
// partition-less fixed-window limiters were a self-inflicted DoS — token refreshes alone exhausted
// them). `auth` partitions by the true client IP resolved from Cloudflare's `CF-Connecting-IP`
// (trusted only on edge-verified requests; forged headers are ignored → FR-002); `payments`
// partitions by the authenticated user so NAT-shared users keep independent quotas. Un-attributable
// requests collapse to one shared "unknown" bucket with its own strict quota. Thresholds are
// env-tunable via the validated RateLimitingOptions (FR-004). NB: app.UseRateLimiter() runs AFTER
// UseAuthentication/UseAuthorization (below), so HttpContext.User is populated for the `payments`
// partition key.
var rateLimitingOptions = new RateLimitingOptions();
builder.Services.AddRateLimiter(o =>
{
    // Bind inside the configure delegate (which runs at options-build time, after
    // WebApplication.CreateBuilder) so host/test configuration overrides are honored — same reason
    // the telemetry permit is read here. Binding at registration time would capture only the
    // appsettings defaults and ignore later in-memory overrides (e.g. WebApplicationFactory tests).
    builder.Configuration.GetSection(RateLimitingOptions.SectionName).Bind(rateLimitingOptions);
    o.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIdentityResolver.ResolveAuthPartition(httpContext, rateLimitingOptions.TrustedEdge),
            partitionKey => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsFor(partitionKey, rateLimitingOptions.AuthPermitsPerMinute, rateLimitingOptions),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    o.AddPolicy("payments", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIdentityResolver.ResolvePaymentsPartition(httpContext),
            partitionKey => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsFor(partitionKey, rateLimitingOptions.PaymentsPermitsPerMinute, rateLimitingOptions),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    // Browser telemetry proxy limiter — keyed by client IP so a single noisy client
    // cannot exhaust the window for everyone (FR-031). Permit count is env-tunable.
    var telemetryPermits = builder.Configuration.GetValue<int?>("Observability:TelemetryProxyRateLimitPerMinute") ?? 120;
    o.AddPolicy("telemetry", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = telemetryPermits,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// The shared "unknown" partition gets its own (strict) quota, never the per-client quota.
static int PermitsFor(string partitionKey, int clientPermits, RateLimitingOptions options) =>
    partitionKey == ClientIdentityResolver.UnknownPartition ? options.UnknownPermitsPerMinute : clientPermits;

// ── CORS ───────────────────────────────────────────────────────────────────
// Origins are configuration-driven via Cors:AllowedOrigins so each environment sets its own
// (Dev → the Cloudflare Pages Dev origin); local Development falls back to localhost when unset
// (009-dev-auto-deploy). The explicit header list covers: JWT bearer, content negotiation, W3C
// trace context (traceparent/tracestate/baggage), and the non-safelisted Content-Type values
// (application/x-protobuf) used by the browser telemetry proxy (FR-028). AllowAnyHeader/
// AllowAnyMethod are intentionally NOT used (SonarCloud S5122).
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (corsOrigins is null || corsOrigins.Length == 0)
    corsOrigins = ["http://localhost:4200", "https://localhost:4200"];

// Cloudflare Pages preview deployments use a per-deploy origin (https://<hash>.nekohoa-dev.pages.dev)
// that can't be enumerated up front, so the post-deploy Playwright smoke gate (which loads the preview
// URL and logs in against this API) was blocked by CORS. Cors:AllowedOriginSuffixes lets Dev allow any
// host under a trusted suffix; it is set ONLY for Dev, so Production keeps its exact-origin allow-list.
var corsSuffixes = builder.Configuration.GetSection("Cors:AllowedOriginSuffixes").Get<string[]>() ?? [];

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(origin => CorsOriginPolicy.IsAllowed(origin, corsOrigins, corsSuffixes))
     .WithHeaders(
         "Authorization", "Content-Type", "Accept",
         "traceparent", "tracestate", "baggage")
     .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
     .AllowCredentials()));

// ── Exception Handler ──────────────────────────────────────────────────────
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ── Health Checks ──────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── FastEndpoints + Swagger ────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient(); // used by the browser telemetry proxy forward (FR-029)
builder.Services.AddFastEndpoints();

if (startupOptions.EnableSwagger)
    builder.Services.SwaggerDocument(o => o.DocumentSettings = s =>
    {
        s.Title = "NekoHOA API";
        s.Version = "v1";
    });

// ── Feature Services ───────────────────────────────────────────────────────
builder.Services.AddScoped<HOAManagementCompany.Features.Auth.AuthService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Auth.EmailVerificationService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Auth.ClaimCodeService>();
// Verification/claim-code delivery: SendGrid email when configured, otherwise audit-log only
// (local dev / CI, where no SendGrid credentials exist).
builder.Services.AddScoped<HOAManagementCompany.Features.Auth.IAuthNotifier>(sp =>
{
    var emailProvider = sp.GetServices<HOAManagementCompany.Infrastructure.Payments.Alerts.IAlertProvider>()
        .FirstOrDefault(p => p.Channel == "email");
    return emailProvider is { IsConfigured: true }
        ? new HOAManagementCompany.Features.Auth.EmailAuthNotifier(
            emailProvider,
            sp.GetRequiredService<ILogger<HOAManagementCompany.Features.Auth.EmailAuthNotifier>>())
        : new HOAManagementCompany.Features.Auth.LoggingAuthNotifier(
            sp.GetRequiredService<ILogger<HOAManagementCompany.Features.Auth.LoggingAuthNotifier>>());
});
builder.Services.AddScoped<HOAManagementCompany.Features.Dashboard.DashboardService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.PaymentService>();
// Stripe payments (006-stripe-payments). Gateway is the network adapter; the rest is testable logic.
builder.Services.AddSingleton<HOAManagementCompany.Infrastructure.Payments.IStripeGateway,
    HOAManagementCompany.Infrastructure.Payments.StripeGateway>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Services.FeeCalculator>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Services.IdempotencyService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Services.PaymentConfigService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Ledger.LedgerService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Ledger.AllocationService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Webhooks.WebhookProcessor>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Jobs.ReconciliationService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Recurring.RecurringDraftService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Recurring.VariableNoticeService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Statements.StatementService>();
// US3 failure alerts (006-stripe-payments): transactional outbox + opt-in alerting.
builder.Services.AddMetrics(); // ensures IMeterFactory is available for PaymentMetrics.
builder.Services.AddSingleton<HOAManagementCompany.Infrastructure.Observability.PaymentMetrics>();
builder.Services.AddSingleton<HOAManagementCompany.Infrastructure.Payments.Alerts.IAlertProvider,
    HOAManagementCompany.Infrastructure.Payments.Alerts.TwilioSmsProvider>();
builder.Services.AddSingleton<HOAManagementCompany.Infrastructure.Payments.Alerts.IAlertProvider,
    HOAManagementCompany.Infrastructure.Payments.Alerts.SendGridEmailProvider>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Alerts.AlertService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Payments.Alerts.OutboxDispatcher>();
builder.Services.AddScoped<HOAManagementCompany.Features.Property.PropertyService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Community.CommunityService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Community.PollService>();

// ── Seeder (registered for DI so --seed flag can resolve it) ───────────────
builder.Services.AddScoped<HOAManagementCompany.Seed.DatabaseSeeder>();
builder.Services.AddScoped<HOAManagementCompany.Seed.DocumentStorageInitializer>();

// ── OpenTelemetry Observability ────────────────────────────────────────────
// Telemetry-init failures MUST never block startup (FR-008/US1 AS3).
try
{
    builder.AddObservability();
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        $"[Observability] OpenTelemetry initialization failed; continuing without telemetry: {ex.Message}");
}

// ═══════════════════════════════════════════════════════════════════════════
var app = builder.Build();

// ── --seed CLI flag (T068 amendment) — bootstrap glue lives in StartupTasks (009) ─────────
var seedExitCode = await StartupTasks.RunSeedCommandAsync(app, args);
if (seedExitCode is not null)
    return seedExitCode.Value;

// ── Middleware Pipeline ────────────────────────────────────────────────────
app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// Push the authenticated user's subject GUID into the LogContext BEFORE request logging,
// so the request-completion log (and any request-scoped entry) carries user_id (FR-011).
app.UseMiddleware<TraceEnrichmentMiddleware>();
app.UseSerilogRequestLogging(o =>
{
    o.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("CorrelationId", ctx.TraceIdentifier);
        diag.Set("UserId", ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous");
    };
});
app.UseRateLimiter();

app.MapHealthChecks("/health");

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api/v1";
    c.Errors.StatusCode = 422;
    c.Errors.ResponseBuilder = (failures, ctx, status) =>
    {
        var errors = failures.Select(f => new { field = f.PropertyName, message = f.ErrorMessage });
        return new { code = "VALIDATION_ERROR", message = "One or more validation errors occurred.", errors };
    };
});

if (startupOptions.EnableSwagger)
    app.UseSwaggerGen();

// Apply migrations and/or seed at startup, driven by config (009-dev-auto-deploy). The imperative
// side-effects live in StartupTasks (excluded from coverage — they need a live database); the
// decision logic is the config-driven StartupOptions, which is unit-tested.
await StartupTasks.ApplyStartupDatabaseAsync(app, startupOptions);

app.Run();
return 0;

public partial class Program { }
