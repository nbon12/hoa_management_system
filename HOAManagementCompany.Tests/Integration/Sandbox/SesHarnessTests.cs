using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Sandbox;

/// <summary>
/// 026 FR-012 (US1-3): with no SES secrets, the Stage-2 SES tests Skip rather than Fail, so an
/// unconfigured secret never blocks a release. Deliberately <b>not</b> in the Sandbox category — it
/// runs in the PR suite and never contacts SES.
/// </summary>
public class SesHarnessUnconfiguredTests(TestDatabaseFixture fixture) : SandboxIntegrationTestBase(fixture)
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        base.ExtraConfiguration()
            // Drop Ses:* and Twilio:* — the base defaults Twilio:FromNumber, which without Twilio
            // secrets (the PR job) would leave Twilio half-configured and abort host startup.
            .Where(kv => !kv.Key.StartsWith("Ses:", StringComparison.Ordinal)
                         && !kv.Key.StartsWith("Twilio:", StringComparison.Ordinal))
            .Concat(new Dictionary<string, string?>
            {
                ["Twilio:FromNumber"] = "",
                ["Ses:Region"] = "",
                ["Ses:FromEmail"] = "",
                ["Ses:AccessKeyId"] = "",
                ["Ses:SecretAccessKey"] = "",
            })
            .ToList();

    [Fact]
    public void Missing_ses_secrets_skip_instead_of_failing() =>
        Assert.Throws<SkipException>(() => RequireSes());
}

/// <summary>
/// 026 FR-006 (US2-3): SES credentials present but <c>Ses:SimulatorOnly</c> off must hard-fail the
/// Stage-2 SES tests — never skip, never send. Not in the Sandbox category; no SES call is made
/// because <c>RequireSes</c> throws first (the keys here are fake).
/// </summary>
public class SesHarnessGuardOffTests(TestDatabaseFixture fixture) : SandboxIntegrationTestBase(fixture)
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        base.ExtraConfiguration()
            // Drop Ses:* and Twilio:* — the base defaults Twilio:FromNumber, which without Twilio
            // secrets (the PR job) would leave Twilio half-configured and abort host startup.
            .Where(kv => !kv.Key.StartsWith("Ses:", StringComparison.Ordinal)
                         && !kv.Key.StartsWith("Twilio:", StringComparison.Ordinal))
            .Concat(new Dictionary<string, string?>
            {
                ["Twilio:FromNumber"] = "",
                ["Ses:Region"] = "us-east-1",
                ["Ses:FromEmail"] = "no-reply@mail.nekohoa.com",
                ["Ses:AccessKeyId"] = "test-access-key-id",
                ["Ses:SecretAccessKey"] = "test",
                ["Ses:SimulatorOnly"] = "false",
            })
            .ToList();

    [Fact]
    public void Guard_off_with_credentials_refuses_to_run()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RequireSes());
        Assert.Contains("Ses:SimulatorOnly must be true", ex.Message);
    }
}

/// <summary>
/// 026 FR-012 / FR-006 (US1-3, US2-3): the <c>RequireSes</c> decision for each individual gap, so the
/// "credentials secret absent" scenario is proven on its own and not only with every setting blank.
/// Pure configuration checks; no host, database or SES call.
/// </summary>
public class SesHarnessRequireSesTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["Ses:Region"] = "us-east-1",
            ["Ses:FromEmail"] = "no-reply@mail.nekohoa.com",
            ["Ses:AccessKeyId"] = "test-access-key-id",
            ["Ses:SecretAccessKey"] = "test",
            ["Ses:SimulatorOnly"] = "true",
        };
        foreach (var (key, value) in overrides) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Missing_credentials_skip_even_with_region_and_sender_set() =>
        Assert.Throws<SkipException>(() => SandboxIntegrationTestBase.RequireSes(
            Config(("Ses:AccessKeyId", null), ("Ses:SecretAccessKey", null))));

    [Theory]
    [InlineData("Ses:AccessKeyId")]
    [InlineData("Ses:SecretAccessKey")]
    [InlineData("Ses:Region")]
    [InlineData("Ses:FromEmail")]
    public void Any_single_missing_setting_skips(string missing) =>
        Assert.Throws<SkipException>(() => SandboxIntegrationTestBase.RequireSes(Config((missing, "  "))));

    [Theory]
    [InlineData("false")]
    [InlineData(null)]
    public void Credentials_present_without_the_guard_hard_fail(string? simulatorOnly)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SandboxIntegrationTestBase.RequireSes(Config(("Ses:SimulatorOnly", simulatorOnly))));
        Assert.Contains("Ses:SimulatorOnly must be true", ex.Message);
    }

    [Fact]
    public void Full_configuration_with_the_guard_on_runs() =>
        Assert.Null(Record.Exception(() => SandboxIntegrationTestBase.RequireSes(Config())));
}

/// <summary>
/// 007 FR-004 / 026 FR-012: with no Twilio secrets (the PR job, where CI still exports
/// <c>Twilio__FromNumber</c>), the harness blanks every Twilio key so the host starts and the
/// Twilio tests Skip instead of every Stage-2 test erroring. Not in the Sandbox category; no
/// provider is contacted.
/// </summary>
public class TwilioHarnessUnconfiguredTests(TestDatabaseFixture fixture) : SandboxIntegrationTestBase(fixture)
{
    private static string? CiShapedEnv(string name) => name == "Twilio__FromNumber" ? "+15005550006" : null;

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        BuildSandboxConfiguration(CiShapedEnv);

    [Fact]
    public void Without_twilio_credentials_every_twilio_key_is_blank()
    {
        var config = BuildSandboxConfiguration(CiShapedEnv).ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal("", config["Twilio:AccountSid"]);
        Assert.Equal("", config["Twilio:AuthToken"]);
        Assert.Equal("", config["Twilio:FromNumber"]);
        Assert.Equal("", config["Twilio:ApiKeySid"]);
        Assert.Equal("", config["Twilio:ApiKeySecret"]);
    }

    [Fact]
    public void Missing_twilio_secrets_skip_instead_of_failing() =>
        Assert.Throws<SkipException>(() => RequireTwilio());
}
