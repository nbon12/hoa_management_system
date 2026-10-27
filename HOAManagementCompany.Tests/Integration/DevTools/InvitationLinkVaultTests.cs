using System.Net;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.DevTools;
using HOAManagementCompany.Features.Invitations;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.DevTools;

/// <summary>
/// 030 T054 — where e2e test support is enabled, each emailed invitation link is kept in the dev
/// vault and readable through the gated <c>/e2e/auth-codes</c> seam, so Playwright and the
/// quickstart can accept invitations without SES.
/// </summary>
public class InvitationLinkVaultTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private const string Secret = "test-scheduler-shared-secret-placeholder";

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        new Dictionary<string, string?> { ["DevTools:E2ECleanupEnabled"] = "true" };

    [Fact]
    public async Task WhenEnabled_TheNewestLinkIsReadableFromTheVault()
    {
        Assert.IsType<VaultingInvitationLinkObserver>(Services.GetRequiredService<IInvitationLinkObserver>());
        var email = await InviteAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/e2e/auth-codes?contact={Uri.EscapeDataString(email)}");
        req.Headers.Add("X-Scheduler-Secret", Secret);
        var res = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var sentLink = InvitationEndpointsLink(Email.Sent.Last(m => m.Target == email).Body);
        Assert.Equal(sentLink, body.RootElement.GetProperty("invitationLink").GetString());
    }

    internal async Task<string> InviteAsync()
    {
        var companyId = await CreateCompanyAsync();
        var (senderId, _) = await CreateCompanyAdminAsync(companyId);
        var email = $"vault-{Guid.NewGuid():N}@example.com";
        using var scope = NewScope();
        await scope.ServiceProvider.GetRequiredService<InvitationService>().CreateAsync(
            new NewInvitation(InvitationKind.CompanyAdministrator, companyId, null, null, null, null, email), senderId, default);
        return email;
    }

    internal static string InvitationEndpointsLink(string body) =>
        body.Split('\n').Single(l => l.StartsWith("Accept invitation: ")).Substring("Accept invitation: ".Length);
}

/// <summary>030 T054 — without e2e test support, no observer keeps links.</summary>
public class InvitationLinkVaultDisabledTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        new Dictionary<string, string?> { ["DevTools:E2ECleanupEnabled"] = "false" };

    [Fact]
    public async Task WhenDisabled_NoLinkIsKept()
    {
        Assert.IsType<NoInvitationLinkObserver>(Services.GetRequiredService<IInvitationLinkObserver>());

        var companyId = await CreateCompanyAsync();
        var (senderId, _) = await CreateCompanyAdminAsync(companyId);
        var email = $"vault-off-{Guid.NewGuid():N}@example.com";
        using var scope = NewScope();
        await scope.ServiceProvider.GetRequiredService<InvitationService>().CreateAsync(
            new NewInvitation(InvitationKind.CompanyAdministrator, companyId, null, null, null, null, email), senderId, default);

        Assert.Null(Services.GetRequiredService<AuthCodeVault>().GetInvitationLink(email));
    }
}
