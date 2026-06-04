// <!-- REPOWISE:START domain=bootstrap -->
// Middleware pipeline and DI registration — HOAManagementCompany API
// <!-- REPOWISE:END -->

using System.Security.Claims;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using FastEndpoints;
using FastEndpoints.Swagger;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Common;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName));

// ── Sentry ─────────────────────────────────────────────────────────────────
builder.WebHost.UseSentry(builder.Configuration["Sentry:Dsn"] ?? "");
builder.Services.Configure<Sentry.AspNetCore.SentryAspNetCoreOptions>(o =>
{
    o.Environment = builder.Environment.EnvironmentName;
    o.TracesSampleRate = 0.2;
    o.SetBeforeSend((sentryEvent, _) =>
    {
        if (sentryEvent.Request?.Data is IDictionary<string, object?> data)
        {
            foreach (var key in new[] { "cardNumber", "cardCvv", "routingNumber", "accountNumber" })
                data.Remove(key);
        }
        return sentryEvent;
    });
});

// ── Database ───────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));

builder.Services.AddDbContextFactory<ApplicationDbContext>(o =>
    o.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)), ServiceLifetime.Scoped);

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
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── CORS ───────────────────────────────────────────────────────────────────
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
builder.Services.AddScoped<HOAManagementCompany.Features.Property.PropertyService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Community.CommunityService>();
builder.Services.AddScoped<HOAManagementCompany.Features.Community.PollService>();

// ── Seeder (registered for DI so --seed flag can resolve it) ───────────────
builder.Services.AddScoped<HOAManagementCompany.Seed.DatabaseSeeder>();
builder.Services.AddScoped<HOAManagementCompany.Seed.DocumentStorageInitializer>();

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
app.UseSerilogRequestLogging(o =>
{
    o.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("CorrelationId", ctx.TraceIdentifier);
        diag.Set("UserId", ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous");
    };
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
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
        var storageInit = scope.ServiceProvider.GetRequiredService<HOAManagementCompany.Seed.DocumentStorageInitializer>();
        await storageInit.EnsureValidPdfsAsync();
    }
}

app.Run();
return 0;

public partial class Program { }
