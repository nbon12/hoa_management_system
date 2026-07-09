using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.DevTools;

// 020-D FR-D11 (T027): e2e registration code seams. Raw codes are stored hashed and exist only
// inside the notifier call, so test support = vaulting notifier + endpoints gated exactly like
// /e2e/cleanup (flag + constant-time shared secret + prod/staging hard block).
public class E2EAuthSupportTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        new Dictionary<string, string?> { ["DevTools:E2ECleanupEnabled"] = "true" };

    private const string Secret = "test-scheduler-shared-secret-placeholder";

    private HttpRequestMessage Req(HttpMethod method, string url, string? secret = Secret)
    {
        var req = new HttpRequestMessage(method, url);
        if (secret is not null) req.Headers.Add("X-Scheduler-Secret", secret);
        return req;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-secret")]
    public async Task Endpoints_RequireTheSharedSecret(string? secret)
    {
        var codes = await Client.SendAsync(Req(HttpMethod.Get, "/api/v1/e2e/auth-codes?contact=x@test.dev", secret));
        var claim = await Client.SendAsync(Req(HttpMethod.Post, "/api/v1/e2e/claim-code", secret));

        Assert.Equal(HttpStatusCode.Unauthorized, codes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, claim.StatusCode);
    }

    [Fact]
    public async Task ClaimCode_IssuesRawCodeForTheUnclaimedSeedProperty()
    {
        var res = await Client.SendAsync(Req(HttpMethod.Post, "/api/v1/e2e/claim-code"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var raw = body.RootElement.GetProperty("claimCode").GetString();
        Assert.False(string.IsNullOrWhiteSpace(raw));

        // The raw code corresponds to a live, unredeemed row for the seed property.
        var db = Services.CreateScope().ServiceProvider
            .GetRequiredService<HOAManagementCompany.Infrastructure.Persistence.ApplicationDbContext>();
        var live = await db.PropertyClaimCodes
            .Include(c => c.Property)
            .Where(c => c.RedeemedAt == null && c.Property!.AccountNumber == "SAKURA-003")
            .ToListAsync();
        Assert.Single(live);
    }

    [Fact]
    public async Task AuthCodes_ReturnsTheDeliveredVerificationCode()
    {
        var contact = $"e2e+vault{Guid.NewGuid():N}@test.dev";
        var request = await Client.PostAsJsonAsync("/api/v1/auth/verify-email/request", new { email = contact });
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);

        var res = await Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/e2e/auth-codes?contact={Uri.EscapeDataString(contact)}"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var code = body.RootElement.GetProperty("verificationCode").GetString();
        Assert.Matches("^[0-9]{6}$", code!);

        // The vaulted code is the real one: confirming with it yields a proof token.
        var confirm = await Client.PostAsJsonAsync("/api/v1/auth/verify-email/confirm", new { email = contact, code });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        using var confirmBody = JsonDocument.Parse(await confirm.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(
            confirmBody.RootElement.GetProperty("verificationToken").GetString()));
    }

    [Fact]
    public async Task AuthCodes_UnknownContact_Returns404()
    {
        var res = await Client.SendAsync(Req(HttpMethod.Get, "/api/v1/e2e/auth-codes?contact=never-requested@test.dev"));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}

public class E2EAuthSupportDisabledTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        new Dictionary<string, string?> { ["DevTools:E2ECleanupEnabled"] = "false" };

    [Fact]
    public async Task Endpoints_Return404_WhenTestSupportDisabled()
    {
        var codes = new HttpRequestMessage(HttpMethod.Get, "/api/v1/e2e/auth-codes?contact=x@test.dev");
        codes.Headers.Add("X-Scheduler-Secret", "test-scheduler-shared-secret-placeholder");
        var claim = new HttpRequestMessage(HttpMethod.Post, "/api/v1/e2e/claim-code");
        claim.Headers.Add("X-Scheduler-Secret", "test-scheduler-shared-secret-placeholder");

        Assert.Equal(HttpStatusCode.NotFound, (await Client.SendAsync(codes)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.SendAsync(claim)).StatusCode);
    }
}
