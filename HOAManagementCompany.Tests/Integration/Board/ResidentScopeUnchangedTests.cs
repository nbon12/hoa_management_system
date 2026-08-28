using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board;

/// <summary>
/// T031 — FR-015 / User Story 2: resident-scoped endpoints keep their "own properties
/// only" semantics and MUST NOT widen because the caller also holds a board membership.
/// </summary>
public class ResidentScopeUnchangedTests(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    // A user who ALSO holds a board membership still sees only their OWN property via the
    // existing resident /property endpoint — never a neighbour's, even in the same community.
    [Fact]
    public async Task BoardMembership_DoesNotWidenResidentPropertyScope()
    {
        Guid communityId;
        Guid myPropertyId, neighbourPropertyId;
        string myEmail;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            communityId = await CreateCommunityAsync(db);

            // Me: a resident who is ALSO a board member of the community.
            var (myUserId, e, myProp) = await CreateUserWithPropertyAsync(db, communityId);
            myEmail = e;
            myPropertyId = myProp;
            await AddMembershipAsync(db, myUserId, communityId, CommunityRole.BoardMember);

            // A neighbour in the SAME community whose data must not leak.
            var (_, _, neighbourProp) = await CreateUserWithPropertyAsync(db, communityId);
            neighbourPropertyId = neighbourProp;
        }

        string myAccount, neighbourAccount;
        using (var scope = NewScope())
        {
            var db = Db(scope);
            myAccount = await db.Properties.Where(p => p.Id == myPropertyId).Select(p => p.AccountNumber).FirstAsync();
            neighbourAccount = await db.Properties.Where(p => p.Id == neighbourPropertyId).Select(p => p.AccountNumber).FirstAsync();
        }

        await LoginAsync(myEmail);
        var res = await Client.GetAsync("/api/v1/property");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(myAccount, body.GetProperty("accountNumber").GetString());
        Assert.NotEqual(neighbourAccount, body.GetProperty("accountNumber").GetString());
        Assert.Equal(myPropertyId, body.GetProperty("id").GetGuid());

        // The resident dashboard likewise resolves from the caller's own property claim.
        var dash = await Client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, dash.StatusCode);
    }
}
