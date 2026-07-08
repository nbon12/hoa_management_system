using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Configuration;

/// <summary>
/// Startup configuration validation (008-config-validation). Boots the real
/// <see cref="Program"/> host via <see cref="WebApplicationFactory{TEntryPoint}"/> and asserts
/// that invalid configuration aborts startup with an <see cref="OptionsValidationException"/>
/// (FR-001/FR-014), while a valid configuration with placeholder secrets starts cleanly
/// (FR-016/SC-003). No PostgreSQL is required — validation throws before any DB access (R4).
/// </summary>
public class StartupValidationTests
{
    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?> overrides) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("environment", "Test");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(ValidBaseline());
                if (overrides.Count > 0)
                    cfg.AddInMemoryCollection(overrides);
            });
        });

    /// <summary>A complete, valid Test configuration using non-functional placeholder secrets.</summary>
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
        // Explicitly blank the optional alert providers so a developer-local
        // appsettings.Secrets.json can't complete a deliberately-partial config
        // and mask the abort-on-startup behavior under test.
        ["SendGrid:ApiKey"] = "",
        ["SendGrid:FromEmail"] = "",
        ["SendGrid:FromName"] = "",
        ["Twilio:AccountSid"] = "",
        ["Twilio:ApiKeySid"] = "",
        ["Twilio:ApiKeySecret"] = "",
        ["Twilio:AuthToken"] = "",
        ["Twilio:FromNumber"] = "",
    };

    /// <summary>Forces host build + start, which triggers ValidateOnStart.</summary>
    private static void StartHost(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
    }

    private static OptionsValidationException AssertStartupThrows(Dictionary<string, string?> overrides)
    {
        using var factory = CreateFactory(overrides);
        var ex = Record.Exception(() => StartHost(factory));
        Assert.NotNull(ex);

        var ove = Unwrap(ex!);
        Assert.NotNull(ove);
        return ove!;
    }

    private static OptionsValidationException? Unwrap(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is OptionsValidationException ove)
                return ove;
        return null;
    }

    // ── US2: valid config with placeholders starts (FR-016) ────────────────────────────────

    [Fact]
    public void ValidTestConfig_WithPlaceholderSecrets_StartsSuccessfully()
    {
        using var factory = CreateFactory(new Dictionary<string, string?>());
        var ex = Record.Exception(() => StartHost(factory));
        Assert.Null(ex);
    }

    // ── US1: single invalid values abort startup (FR-014) ──────────────────────────────────

    [Theory]
    [InlineData("Observability:TraceSampleRatio", "1.5")]   // out of [0,1]
    [InlineData("Observability:TraceSampleRatio", "-0.1")]  // out of [0,1]
    [InlineData("Observability:OtlpProtocol", "grpc")]      // unsupported protocol
    [InlineData("Observability:OtlpEndpoint", "not-a-uri")] // not absolute URI
    [InlineData("Stripe:SecretKey", "")]                    // required secret unset
    [InlineData("Stripe:WebhookToleranceSeconds", "0")]     // must be > 0
    [InlineData("Jobs:SchedulerSharedSecret", "")]          // required secret unset
    [InlineData("Auth:RefreshCookie:SameSite", "sneaky")]   // unknown SameSite value (020-D)
    [InlineData("Auth:RefreshCookie:SameSite", "")]         // SameSite must be explicit (020-D)
    [InlineData("Storage:AccessKey", "")]                   // required field unset (FR-011)
    [InlineData("Payments:ReconcilePendingAchAfterHours", "0")] // must be > 0
    [InlineData("Payments:DefaultFee:CardFeeType", "Bogus")]    // unknown enum value
    public void InvalidSingleValue_AbortsStartup(string key, string value)
    {
        var ex = AssertStartupThrows(new Dictionary<string, string?> { [key] = value });
        Assert.NotEmpty(ex.Failures);
    }

    [Fact]
    public void PercentageFeeWithAllCardsScope_AbortsStartup()
    {
        var ex = AssertStartupThrows(new Dictionary<string, string?>
        {
            ["Payments:DefaultFee:CardFeeType"] = "Percentage",
            ["Payments:DefaultFee:CardScope"] = "AllCards",
        });
        Assert.Contains(ex.Failures, f => f.Contains("CreditOnly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PartiallyConfiguredTwilio_AbortsStartup()
    {
        // AccountSid set but no from-number / auth pair → would fail at send time.
        var ex = AssertStartupThrows(new Dictionary<string, string?> { ["Twilio:AccountSid"] = "AC123" });
        Assert.Contains(ex.Failures, f => f.Contains("Twilio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PartiallyConfiguredSendGrid_AbortsStartup()
    {
        var ex = AssertStartupThrows(new Dictionary<string, string?> { ["SendGrid:ApiKey"] = "SG.test" });
        Assert.Contains(ex.Failures, f => f.Contains("SendGrid", StringComparison.OrdinalIgnoreCase));
    }

    // ── FR-019: failure messages never echo secret values ──────────────────────────────────

    [Fact]
    public void FailureMessage_DoesNotContainSecretValues()
    {
        var ex = AssertStartupThrows(new Dictionary<string, string?>
        {
            ["Stripe:SecretKey"] = "sk_live_DO_NOT_LEAK_ME",
            ["Stripe:WebhookToleranceSeconds"] = "0", // the actual failure
        });
        var combined = string.Join("\n", ex.Failures);
        Assert.DoesNotContain("DO_NOT_LEAK_ME", combined);
    }
}
