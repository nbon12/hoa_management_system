using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 FR-040: a refusal whose message names an invited email (INVITATION_PENDING,
/// ALREADY_MEMBER) never reaches the logs. GlobalExceptionHandler logs only the code, and no
/// event may carry the DomainException itself — on .NET 9 the exception middleware logs every
/// exception with its message before the handler runs.
/// </summary>
public class DomainRefusalLoggingTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [Fact]
    public async Task ARefusalNamingAnInvitedEmail_LogsItsCodeOnly()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        var (_, managerEmail) = await CreateManagerAsync(community);
        using var client = await ClientForAsync(managerEmail);
        var invited = $"pat-{Guid.NewGuid():N}@example.com";
        var body = new { role = (int)CommunityRole.BoardMember, email = invited };

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/v1/communities/{community}/memberships", body)).StatusCode);
        var duplicate = await client.PostAsJsonAsync($"/api/v1/communities/{community}/memberships", body);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var refusal = await JsonAsync(duplicate);
        Assert.Equal("INVITATION_PENDING", refusal.GetProperty("code").GetString());
        Assert.Contains(invited, refusal.GetProperty("message").GetString());

        Assert.Contains(LogSink.Events, e => e.MessageTemplate.Text.StartsWith("Domain error")
            && e.Properties["Code"].ToString() == "\"INVITATION_PENDING\"");
        Assert.DoesNotContain(LogSink.Events, e => e.Exception is DomainException);
        Assert.DoesNotContain(LogSink.Events, e =>
            (e.RenderMessage() + " " + e.Exception + " " + string.Join(" ", e.Properties.Values))
            .Contains(invited, StringComparison.OrdinalIgnoreCase));
    }
}
