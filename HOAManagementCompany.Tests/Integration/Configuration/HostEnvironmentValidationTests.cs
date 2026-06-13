using System;
using System.Threading.Tasks;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Configuration;

/// <summary>
/// Startup guard for the host <c>ASPNETCORE_ENVIRONMENT</c> name (010, constitution §8). Builds a
/// minimal generic host wired with <see cref="OptionsValidationExtensions.AddValidatedHostEnvironment"/>
/// and starts it, so <c>ValidateOnStart</c> runs exactly as it does for the real app — but without the
/// app's DB/Sentry startup, which is environment-sensitive and would mask the validation behavior.
/// A mis-set environment aborts host start with <see cref="OptionsValidationException"/>; the known
/// deployed value (<c>Dev</c>) starts cleanly.
/// </summary>
public class HostEnvironmentValidationTests
{
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = "HOAManagementCompany.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IHost BuildHost(string environmentName) =>
        new HostBuilder()
            .ConfigureServices(services =>
                services.AddValidatedHostEnvironment(new FakeHostEnvironment { EnvironmentName = environmentName }))
            .Build();

    [Theory]
    [InlineData("prod")]        // must be "Production"
    [InlineData("production")]  // wrong casing
    [InlineData("Local")]       // unknown
    public async Task MisSetEnvironment_AbortsStartup(string environmentName)
    {
        using var host = BuildHost(environmentName);

        var ex = await Record.ExceptionAsync(() => host.StartAsync());

        var ove = Assert.IsType<OptionsValidationException>(ex);
        Assert.Contains(ove.Failures, f =>
            f.Contains("ASPNETCORE_ENVIRONMENT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeployedDevEnvironment_StartsSuccessfully()
    {
        using var host = BuildHost("Dev");

        var ex = await Record.ExceptionAsync(() => host.StartAsync());
        Assert.Null(ex);

        await host.StopAsync();
    }
}
