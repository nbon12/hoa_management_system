using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Configuration;

/// <summary>
/// 015 US3 (FR-009): the environment backstop sits ABOVE configuration — a Production (or
/// ambiguous) environment refuses to boot when test-support flags are enabled, whatever the
/// config says. Boots the real <see cref="Program"/> host; validation throws before any DB
/// access, so no containers are needed (same technique as <see cref="StartupValidationTests"/>).
/// </summary>
public class ProductionBackstopValidationTests
{
    private static WebApplicationFactory<Program> CreateFactory(
        string environment, Dictionary<string, string?> overrides) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("environment", environment);
            // UseSetting (not ConfigureAppConfiguration): these values must be visible to
            // Program.cs's inline configuration reads (connection string, StartupOptions.Resolve),
            // which run before the factory's app-configuration callbacks are appended.
            foreach (var (key, value) in ValidBaseline())
                builder.UseSetting(key, value);
            foreach (var (key, value) in overrides)
                builder.UseSetting(key, value);
        });

    private static Dictionary<string, string?> ValidBaseline() => new()
    {
        ["ConnectionStrings:DefaultConnection"] =
            "Host=localhost;Port=5432;Database=nekohoa_test;Username=nekohoa;Password=nekohoa",
        ["Jwt:Secret"] = "test-secret-for-integration-tests-must-be-32-chars!!",
        ["Storage:ServiceUrl"] = "http://localhost:9000",
        ["Storage:AccessKey"] = "minioadmin",
        ["Storage:SecretKey"] = "minioadmin",
        ["Storage:BucketName"] = "hoa-documents",
        ["Stripe:SecretKey"] = "sk_test_placeholder",
        ["Stripe:PublishableKey"] = "pk_test_placeholder",
        ["Stripe:WebhookSigningSecret"] = "whsec_placeholder",
        ["Jobs:SchedulerSharedSecret"] = "test-scheduler-shared-secret-placeholder",
        ["Sentry:Dsn"] = "",
    };

    private static Exception? BootHost(string environment, Dictionary<string, string?> overrides)
    {
        using var factory = CreateFactory(environment, overrides);
        return Record.Exception(() => { using var _ = factory.CreateClient(); });
    }

    [Fact]
    public void Production_WithSeedDataEnabled_RefusesToBoot()
    {
        var ex = BootHost("Production", new() { ["Startup:SeedData"] = "true" });

        Assert.NotNull(ex);
        Assert.Contains("SeedData", Flatten(ex!));
    }

    [Fact]
    public void Production_WithE2ECleanupEnabled_RefusesToBoot()
    {
        var ex = BootHost("Production", new() { ["DevTools:E2ECleanupEnabled"] = "true" });

        Assert.NotNull(ex);
        Assert.NotNull(FindOptionsValidation(ex!));
        Assert.Contains("E2ECleanupEnabled", Flatten(ex!));
    }

    [Fact]
    public void Production_WithoutTestSupportFlags_BootsCleanly()
    {
        // Control: the backstop must not break a legitimate Production boot.
        Assert.Null(BootHost("Production", new()));
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("Staging")]
    public void NonProduction_WithE2ECleanupEnabled_Boots(string environment)
    {
        // The flag is legitimate outside Production (deployed-Dev smoke gate; Test harness).
        Assert.Null(BootHost(environment, new() { ["DevTools:E2ECleanupEnabled"] = "true" }));
    }

    [Fact]
    public void AmbiguousEnvironment_WithTestSupportFlags_RefusesToBoot()
    {
        // Spec edge case (015): an unknown/ambiguous environment name must default to "test
        // machinery disabled" — the seed backstop refuses the boot before any database access
        // (the 008 host-environment validation would also reject the name, but only at host
        // start, which is after startup tasks would have run).
        var ex = BootHost("prod", new()
        {
            ["Startup:SeedData"] = "true",
            ["DevTools:E2ECleanupEnabled"] = "true",
        });

        Assert.NotNull(ex);
        Assert.Contains("test machinery is disabled", Flatten(ex!));
    }

    private static string Flatten(Exception ex)
    {
        var parts = new List<string>();
        for (Exception? e = ex; e is not null; e = e.InnerException) parts.Add(e.Message);
        return string.Join(" | ", parts);
    }

    private static OptionsValidationException? FindOptionsValidation(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is OptionsValidationException ove) return ove;
        return null;
    }
}
