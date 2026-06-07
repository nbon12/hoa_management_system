// <!-- REPOWISE:START domain=bootstrap -->
// Middleware pipeline and DI registration — HOAManagementCompany API.
// Observability wiring lives here: Serilog (Console + OTLP/JSON sink, trace/span +
// user-GUID enrichment), Sentry-on-OTel (consumes the OpenTelemetry activity pipeline
// with an independent trace sample rate), and builder.AddObservability() (OTel tracing/
// metrics → OTLP, scrubbing, sampling). Telemetry-init is guarded as non-fatal (FR-008).
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
using HOAManagementCompany.Infrastructure.Observability;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.Storage;
using Sentry.OpenTelemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

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

builder.Services.AddDbContext<ApplicationDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.EnableRetryOnFailure(3)));

builder.Services.AddDbContextFactory<ApplicationDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.EnableRetryOnFailure(3)), ServiceLifetime.Scoped);

// ── ASP.NET Core Identity ──────────────────────────────────────────────────
builder.Services.AddIdentityCore<ApplicationUser>(o =>
{
    o.Password.RequireDigit = true;
    o.Password.RequireUppercase = true;
    o.Password.RequireNonAlphanumeric = true;
    o.Password.RequiredLength = 8;
    o.User.RequireUniqueEmail = true;
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
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();

// ── S3 / MinIO ─────────────────────────────────────────────────────────────
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

var storageOpts = builder.Configuration.GetSection("Storage").Get<StorageOptions>()!;
builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var config = new AmazonS3Config
    {
        ServiceURL = storageOpts.ServiceUrl,
        ForcePathStyle = storageOpts.ForcePathStyle,
        UseHttp = storageOpts.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
    };
    return new AmazonS3Client(
        new BasicAWSCredentials(storageOpts.AccessKey, storageOpts.SecretKey),
        config);
});
builder.Services.AddScoped<IDocumentStorage, S3DocumentStorage>();

// ── Payments (Stripe / alerts / jobs) ──────────────────────────────────────
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection(PaymentsOptions.SectionName));
builder.Services.Configure<JobsOptions>(builder.Configuration.GetSection(JobsOptions.SectionName));
builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection(TwilioOptions.SectionName));
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection(SendGridOptions.SectionName));

// ── Rate Limiting ──────────────────────────────────────────────────────────
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
        opts.QueueLimit = 0;
    });
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

// ── CORS ───────────────────────────────────────────────────────────────────
// AllowAnyHeader/AllowAnyMethod already permit the W3C `traceparent`/`tracestate`
// request headers and the `application/x-protobuf` content type (a non-safelisted type
// that triggers a CORS preflight) used by the browser telemetry proxy path (FR-028).
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200", "https://localhost:4200")
     .AllowAnyHeader()
     .AllowAnyMethod()
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

if (builder.Environment.IsDevelopment())
    builder.Services.SwaggerDocument(o => o.DocumentSettings = s =>
    {
        s.Title = "NekoHOA API";
        s.Version = "v1";
    });

// ── Feature Services ───────────────────────────────────────────────────────
builder.Services.AddScoped<HOAManagementCompany.Features.Auth.AuthService>();
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

// ── --seed CLI flag (T068 amendment) ──────────────────────────────────────
if (args.Contains("--seed"))
{
    if (!app.Environment.IsDevelopment())
    {
        await Console.Error.WriteLineAsync("ERROR: Seeder is restricted to the Development environment.");
        return 1;
    }
    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<HOAManagementCompany.Seed.DatabaseSeeder>();
    await seeder.SeedAsync();
    return 0;
}

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

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        // Apply migrations + seed dev data on every startup. SeedAsync runs
        // MigrateAsync first and is idempotent (skips inserts when the seed user
        // already exists), so a fresh `docker-compose up` yields a ready-to-use,
        // login-able database without a manual --seed step.
        var seeder = scope.ServiceProvider.GetRequiredService<HOAManagementCompany.Seed.DatabaseSeeder>();
        await seeder.SeedAsync();

        // Refresh document PDFs in object storage on every startup — the minio
        // volume can be reset independently of the database.
        var storageInit = scope.ServiceProvider.GetRequiredService<HOAManagementCompany.Seed.DocumentStorageInitializer>();
        await storageInit.EnsureValidPdfsAsync();
    }
}

app.Run();
return 0;

public partial class Program { }
