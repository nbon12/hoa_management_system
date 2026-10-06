using HOAManagementCompany.Tests.Fixtures;
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
            .Where(kv => !kv.Key.StartsWith("Ses:", StringComparison.Ordinal))
            .Concat(new Dictionary<string, string?>
            {
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
            .Where(kv => !kv.Key.StartsWith("Ses:", StringComparison.Ordinal))
            .Concat(new Dictionary<string, string?>
            {
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
