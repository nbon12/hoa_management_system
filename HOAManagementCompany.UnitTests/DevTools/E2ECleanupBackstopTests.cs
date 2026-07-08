using FastEndpoints;
using HOAManagementCompany.Features.DevTools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HOAManagementCompany.UnitTests.DevTools;

/// <summary>
/// 015 US3 (FR-009) defense-in-depth: even if configuration enables the flag after boot (config
/// reload), the cleanup endpoint behaves as if it does not exist in Production, and the refused
/// attempt is logged as a security-relevant event. Container-free: the Production path returns
/// before any database access, so the endpoint is exercised directly via the FastEndpoints
/// test factory with a null DbContext.
/// </summary>
public class E2ECleanupBackstopTests
{
    private sealed class FakeEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "HOAManagementCompany";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class CapturingLogger : ILogger<E2ECleanupEndpoint>
    {
        public readonly List<(LogLevel Level, string Message)> Events = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Events.Add((logLevel, formatter(state, exception)));
    }

    private static IConfiguration Config(bool enabled) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevTools:E2ECleanupEnabled"] = enabled ? "true" : "false",
        })
        .Build();

    private static async Task<(int Status, CapturingLogger Log)> InvokeAsync(string environment, bool flagEnabled)
    {
        var logger = new CapturingLogger();
        var ep = Factory.Create<E2ECleanupEndpoint>(
            ctx => ctx.Request.Method = "DELETE",
            null!,                       // ApplicationDbContext — never reached on the refused paths
            Config(flagEnabled),
            new FakeEnv(environment),
            logger);

        await ep.HandleAsync(default);
        return (ep.HttpContext.Response.StatusCode, logger);
    }

    [Fact]
    public async Task Production_FlagEnabled_ReturnsNotFound_AndLogsSecurityEvent()
    {
        var (status, log) = await InvokeAsync(Environments.Production, flagEnabled: true);

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Contains(log.Events, e => e.Level == LogLevel.Warning
            && e.Message.Contains("security devtools.e2e-cleanup refused"));
    }

    [Fact]
    public async Task Production_FlagDisabled_ReturnsNotFound_WithoutSecurityNoise()
    {
        var (status, log) = await InvokeAsync(Environments.Production, flagEnabled: false);

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.DoesNotContain(log.Events, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Dev_FlagDisabled_ReturnsNotFound()
    {
        var (status, _) = await InvokeAsync("Dev", flagEnabled: false);

        Assert.Equal(StatusCodes.Status404NotFound, status);
    }
}
