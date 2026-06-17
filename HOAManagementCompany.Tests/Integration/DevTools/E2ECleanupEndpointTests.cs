using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.DevTools;

/// <summary>
/// The e2e cleanup endpoint is config-gated (DevTools:E2ECleanupEnabled) rather than gated on the
/// host environment name, so it works in the deployed Dev environment where the Playwright
/// registration smoke test runs. These verify both sides of the gate.
/// </summary>
public class E2ECleanupEnabledTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        new Dictionary<string, string?> { ["DevTools:E2ECleanupEnabled"] = "true" };

    [Fact]
    public async Task Returns_ok_when_enabled()
    {
        var resp = await Client.DeleteAsync("/api/v1/e2e/cleanup");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}

public class E2ECleanupDisabledTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        new Dictionary<string, string?> { ["DevTools:E2ECleanupEnabled"] = "false" };

    [Fact]
    public async Task Returns_not_found_when_disabled()
    {
        var resp = await Client.DeleteAsync("/api/v1/e2e/cleanup");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
