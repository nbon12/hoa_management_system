using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

/// <summary>
/// 030 T005 — one frontend origin for emailed links (research R10): the new <c>App:BaseUrl</c> key
/// wins, the older ArchitecturalReview key still works, and deployed environments must set an
/// absolute HTTPS origin (constitution §8).
/// </summary>
public class AppLinksTests
{
    private static readonly AppLinksOptionsValidator Validator = new();

    private static AppLinksOptions Resolve(string environment, string? newKey, string? legacyKey)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:BaseUrl"] = newKey,
            [AppLinksOptions.LegacyKey] = legacyKey,
        }).Build();
        var options = new AppLinksOptions();
        config.GetSection(AppLinksOptions.SectionName).Bind(options);
        options.ApplyEnvironment(config, new Env(environment));
        return options;
    }

    [Fact]
    public void NewKey_WinsOverTheLegacyKey()
    {
        var options = Resolve("Production", "https://app.example.com", "https://old.example.com");
        Assert.Equal("https://app.example.com", options.BaseUrl);
    }

    [Fact]
    public void LegacyKeyAlone_StillWorks_AndLinksAreAbsolute()
    {
        var options = Resolve("Production", null, "https://old.example.com/");
        Assert.True(Validator.Validate(options).IsValid);
        Assert.Equal("https://old.example.com/invite#t=abc", new AppLinks(Options.Create(options)).Link("/invite#t=abc"));
    }

    [Theory]
    [InlineData("Production", null, "must be set")]
    [InlineData("Staging", "app.example.com", "absolute")]
    [InlineData("Dev", "http://app.example.com", "HTTPS")]
    [InlineData("Development", "/relative", "absolute")]
    public void InvalidValues_AreRejected(string environment, string? value, string expected)
    {
        var result = Validator.Validate(Resolve(environment, value, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains(expected));
    }

    [Theory]
    [InlineData("Development", null)]
    [InlineData("Test", null)]
    [InlineData("Development", "http://localhost:4200")]
    public void LocalEnvironments_MayLeaveItEmpty_OrUseHttp(string environment, string? value)
    {
        var options = Resolve(environment, value, null);
        Assert.True(Validator.Validate(options).IsValid);
    }

    [Fact]
    public void ShippedDevSettings_SupplyAnHttpsOrigin_WithoutAnyEnvironmentVariable()
    {
        // The Dev service's ArchitecturalReview__AppBaseUrl comes only from infra-apply, which can lag
        // the code; a deploy without it crashed at startup. The shipped Dev settings must be enough.
        var apiDir = Path.Combine(FindRepoRoot(), "HOAManagementCompany");
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(apiDir, "appsettings.json"))
            .AddJsonFile(Path.Combine(apiDir, "appsettings.Dev.json"))
            .Build();
        var options = new AppLinksOptions();
        config.GetSection(AppLinksOptions.SectionName).Bind(options);
        options.ApplyEnvironment(config, new Env("Dev"));

        Assert.True(Validator.Validate(options).IsValid);
        Assert.Equal("https://dev.nekohoa.com", options.BaseUrl);
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "HOAManagementCompany.sln")))
                return dir.FullName;
        throw new InvalidOperationException("Could not locate the repository root (HOAManagementCompany.sln).");
    }

    private sealed class Env(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
