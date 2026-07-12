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

    // The full verified-registration round trip (020-D US4 / 017-A FR-A1): request a code,
    // confirm it via the vaulted raw value, issue a claim code for the seed property, register —
    // 201 with the cookie session and no refreshToken in the body.
    [Fact]
    public async Task VerifiedRegistration_EndToEnd_CreatesAccountWithCookieSession()
    {
        var email = $"e2e+reg{Guid.NewGuid():N}@test.dev";
        await Client.PostAsJsonAsync("/api/v1/auth/verify-email/request", new { email });

        var codeRes = await Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/e2e/auth-codes?contact={Uri.EscapeDataString(email)}"));
        using var codeBody = JsonDocument.Parse(await codeRes.Content.ReadAsStringAsync());
        var code = codeBody.RootElement.GetProperty("verificationCode").GetString();

        var confirm = await Client.PostAsJsonAsync("/api/v1/auth/verify-email/confirm", new { email, code });
        using var confirmBody = JsonDocument.Parse(await confirm.Content.ReadAsStringAsync());
        var proof = confirmBody.RootElement.GetProperty("verificationToken").GetString();

        var claimRes = await Client.SendAsync(Req(HttpMethod.Post, "/api/v1/e2e/claim-code"));
        using var claimBody = JsonDocument.Parse(await claimRes.Content.ReadAsStringAsync());
        var claimCode = claimBody.RootElement.GetProperty("claimCode").GetString();

        var register = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            verificationToken = proof,
            password = "Password1!",
            firstName = "Reg",
            lastName = "Istrant",
            claimCode
        });

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var cookie = register.Headers.GetValues("Set-Cookie").FirstOrDefault(c => c.StartsWith("neko_refresh="));
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        using var regBody = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        Assert.True(regBody.RootElement.TryGetProperty("token", out _));
        Assert.False(regBody.RootElement.TryGetProperty("refreshToken", out _));
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
