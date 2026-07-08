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

    // 017-A FR-A6: the endpoint additionally requires the scheduler shared secret.
    private const string TestSchedulerSecret = "test-scheduler-shared-secret-placeholder";

    private static HttpRequestMessage CleanupRequest(string? secret)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/e2e/cleanup");
        if (secret is not null) req.Headers.Add("X-Scheduler-Secret", secret);
        return req;
    }

    [Fact]
    public async Task Returns_ok_when_enabled_with_valid_secret()
    {
        var resp = await Client.SendAsync(CleanupRequest(TestSchedulerSecret));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Returns_unauthorized_without_secret_even_when_enabled()
    {
        var resp = await Client.SendAsync(CleanupRequest(secret: null));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Returns_unauthorized_with_wrong_secret_even_when_enabled()
    {
        var resp = await Client.SendAsync(CleanupRequest("wrong-secret"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
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
