using System.Collections.Generic;
using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

/// <summary>
/// Config-gated debug behavior (014 US3 / contract debug-gating-behavior.md). Verifies that exception
/// detail and SQL-text capture are driven by configuration that defaults to dev-like (local
/// <c>Development</c> AND deployed <c>Dev</c>) and are forced off in <c>Production</c> — so the deployed
/// <c>Dev</c> environment is debuggable (SC-006) while production posture never regresses (SC-007).
/// </summary>
public class DebugGatingTests
{
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(params (string key, string value)[] values)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in values)
            dict[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    // ── DevTools:ExposeExceptionDetail (exception handler detail) ──────────────────────────

    [Theory]
    [InlineData("Dev", true)]          // DG-1: deployed Dev → on by default
    [InlineData("Development", true)]  // DG-2: local Development → on by default
    [InlineData("Staging", false)]     // non dev-like → off
    [InlineData("Production", false)]  // DG-3: production → off by default
    public void ExposeExceptionDetail_DefaultsByEnvironment(string environment, bool expected)
    {
        var options = new DevToolsOptions(); // unset
        options.ApplyEnvironmentDefaults(new FakeHostEnvironment { EnvironmentName = environment });

        Assert.Equal(expected, options.ExposeExceptionDetail);
    }

    [Fact]
    public void ExposeExceptionDetail_ProductionForcesOff_EvenWhenConfiguredTrue() // DG-4 / SC-007
    {
        var options = new DevToolsOptions { ExposeExceptionDetail = true };
        options.ApplyEnvironmentDefaults(new FakeHostEnvironment { EnvironmentName = "Production" });

        Assert.False(options.ExposeExceptionDetail);
    }

    [Fact]
    public void ExposeExceptionDetail_DevCanExplicitlyDisable() // DG-5 (config wins, safe direction)
    {
        var options = new DevToolsOptions { ExposeExceptionDetail = false };
        options.ApplyEnvironmentDefaults(new FakeHostEnvironment { EnvironmentName = "Dev" });

        Assert.False(options.ExposeExceptionDetail);
    }

    // ── Observability:CaptureSqlText ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("Dev", true)]          // DG-6: deployed Dev → SQL text captured (was off before the fix)
    [InlineData("Development", true)]
    [InlineData("Production", false)]  // DG-7
    [InlineData("Staging", false)]
    public void CaptureSqlText_DefaultsByEnvironment(string environment, bool expected)
    {
        var options = ObservabilityOptions.FromConfiguration(
            Config(), new FakeHostEnvironment { EnvironmentName = environment });

        Assert.Equal(expected, options.CaptureSqlText);
    }

    [Theory]
    [InlineData("Production", "true", true)]   // DG-8: explicit value wins, even in Production
    [InlineData("Dev", "false", false)]
    public void CaptureSqlText_ExplicitConfigWins(string environment, string configured, bool expected)
    {
        var options = ObservabilityOptions.FromConfiguration(
            Config(("Observability:CaptureSqlText", configured)),
            new FakeHostEnvironment { EnvironmentName = environment });

        Assert.Equal(expected, options.CaptureSqlText);
    }
}
