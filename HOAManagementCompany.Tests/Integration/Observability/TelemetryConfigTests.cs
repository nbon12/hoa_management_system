using HOAManagementCompany.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Observability;

/// <summary>
/// US5 / FR-007 / SC-004: telemetry destination, headers, and service name bind from the
/// standard OTEL_* environment variables with no code change; an unset endpoint in
/// Development defaults to the local Aspire Dashboard.
/// </summary>
public class TelemetryConfigTests
{
    private sealed class FakeEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Binds_Endpoint_Headers_ServiceName_FromOtelEnvironment()
    {
        var config = Config(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "https://vendor.example.com",
            ["OTEL_EXPORTER_OTLP_HEADERS"] = "Authorization=Bearer abc123",
            ["OTEL_SERVICE_NAME"] = "hoa-api-prod",
        });

        var options = ObservabilityOptions.FromConfiguration(config, new FakeEnv { EnvironmentName = Environments.Production });

        Assert.Equal("https://vendor.example.com", options.OtlpEndpoint);
        Assert.Equal("Authorization=Bearer abc123", options.OtlpHeaders);
        Assert.Equal("hoa-api-prod", options.ServiceName);
        Assert.True(options.HasExplicitEndpoint);
    }

    [Fact]
    public void UnsetEndpoint_InDevelopment_DefaultsToLocalDashboard()
    {
        var options = ObservabilityOptions.FromConfiguration(Config(new()), new FakeEnv());

        Assert.Equal("http://aspire-dashboard:18890", options.OtlpEndpoint);
        Assert.False(options.HasExplicitEndpoint);
        // SQL capture defaults ON in Development (FR-010).
        Assert.True(options.CaptureSqlText);
    }

    [Fact]
    public void UnsetEndpoint_OutsideDevelopment_GatesSqlTextOff()
    {
        var options = ObservabilityOptions.FromConfiguration(
            Config(new()), new FakeEnv { EnvironmentName = Environments.Production });

        Assert.False(options.CaptureSqlText);
    }
}
