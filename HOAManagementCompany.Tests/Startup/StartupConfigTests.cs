using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HOAManagementCompany.Tests.Startup;

/// <summary>
/// Unit tests for <see cref="StartupOptions"/> (009-dev-auto-deploy) — verifies that
/// migrations/seed/Swagger gating is config-driven with environment-derived defaults, and that
/// the Production Swagger invariant holds. Pure unit tests (no host), so they stay fast and cover
/// StartupOptions.cs for the diff-coverage gate (Program.cs itself is coverage-excluded).
/// </summary>
public class StartupConfigTests
{
    private sealed class FakeEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = "HOAManagementCompany.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Theory]
    [InlineData("Development")]
    [InlineData("Dev")]
    public void DevLikeEnvironments_DefaultAllOn(string env)
    {
        var options = StartupOptions.Resolve(Config(), new FakeEnv { EnvironmentName = env });

        Assert.True(options.ApplyMigrations);
        Assert.True(options.SeedData);
        Assert.True(options.EnableSwagger);
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void NonDevLikeEnvironments_DefaultAllOff(string env)
    {
        var options = StartupOptions.Resolve(Config(), new FakeEnv { EnvironmentName = env });

        Assert.False(options.ApplyMigrations);
        Assert.False(options.SeedData);
        Assert.False(options.EnableSwagger);
    }

    [Fact]
    public void Configuration_OverridesEnvironmentDefault()
    {
        // Development would default everything ON; explicit config turns specific flags off.
        var options = StartupOptions.Resolve(
            Config(("Startup:ApplyMigrations", "false"), ("Startup:SeedData", "false")),
            new FakeEnv { EnvironmentName = "Development" });

        Assert.False(options.ApplyMigrations);
        Assert.False(options.SeedData);
        Assert.True(options.EnableSwagger); // untouched key keeps the env default
    }

    [Fact]
    public void Production_ForcesSwaggerOff_EvenIfConfigEnablesIt()
    {
        var options = StartupOptions.Resolve(
            Config(("Startup:EnableSwagger", "true")),
            new FakeEnv { EnvironmentName = "Production" });

        Assert.False(options.EnableSwagger);
    }

    [Fact]
    public void Dev_CanOptOutOfSeed_WhileKeepingMigrations()
    {
        var options = StartupOptions.Resolve(
            Config(("Startup:SeedData", "false")),
            new FakeEnv { EnvironmentName = "Dev" });

        Assert.True(options.ApplyMigrations);
        Assert.False(options.SeedData);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Dev", true)]
    [InlineData("Test", false)]
    [InlineData("Production", false)]
    public void IsDevLike_MatchesDevelopmentAndDev(string env, bool expected)
    {
        Assert.Equal(expected, StartupOptions.IsDevLike(new FakeEnv { EnvironmentName = env }));
    }
}
