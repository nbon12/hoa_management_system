using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

/// <summary>
/// 030 T115 — FR-024f: a claim code only claims an empty home. Once someone lives there by
/// invitation, the home's outstanding code gets the generic refusal, like any other bad code.
/// </summary>
public partial class ClaimCodeAfterInvitationTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [GeneratedRegex("/invite#t=([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();

    [Fact]
    public async Task AClaimCode_ForAHomeWithAResidentByInvitation_IsRefusedGenerically()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var (_, managerEmail) = await CreateManagerAsync(community);
        var home = await CreatePropertyAsync(community);
        var code = await IssueCodeAsync(home);
        using var manager = await ClientForAsync(managerEmail);
        var jane = $"jane-{Guid.NewGuid():N}@example.com";
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync($"/api/v1/communities/{community}/residents/invitations",
            new { propertyId = home, email = jane })).StatusCode);
        var token = LinkToken().Match(Email.Sent.Single(m => m.Target == jane).Body).Groups[1].Value;
        Assert.Equal(HttpStatusCode.Created, (await CreateClient().PostAsJsonAsync("/api/v1/invitations/register",
            new { token, firstName = "Jane", lastName = "Alvarez", password = "Str0ng!Pass" })).StatusCode);
        var stranger = $"sam-{Guid.NewGuid():N}@example.com";

        var res = await RegisterAsync(stranger, code);

        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("REGISTRATION_FAILED", (await JsonAsync(res)).GetProperty("code").GetString());
        Assert.False(await WithDbAsync(db => db.Users.AnyAsync(u => u.Email == stranger)));
        Assert.Equal(1, await WithDbAsync(db => db.UserProperties.CountAsync(up => up.PropertyId == home)));
    }

    [Fact]
    public async Task AClaimCode_StillWorks_ForAHomeWithNoResident()
    {
        var community = await CreateCommunityInAsync(await CreateCompanyAsync());
        var home = await CreatePropertyAsync(community);
        var code = await IssueCodeAsync(home);
        var lena = $"lena-{Guid.NewGuid():N}@example.com";

        var res = await RegisterAsync(lena, code);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var userId = await WithDbAsync(db => db.Users.Where(u => u.Email == lena).Select(u => u.Id).SingleAsync());
        Assert.True(await WithDbAsync(db => db.UserProperties.AnyAsync(up => up.UserId == userId && up.PropertyId == home)));
        Assert.NotNull(await WithDbAsync(db => db.PropertyClaimCodes.Where(c => c.PropertyId == home).Select(c => c.RedeemedAt).SingleAsync()));
    }

    private async Task<string> IssueCodeAsync(Guid home)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ClaimCodeService>().IssueAsync(home, "owner@example.com", default);
    }

    private async Task<HttpResponseMessage> RegisterAsync(string email, string code)
    {
        var proof = await WithDbAsync(async db =>
        {
            var raw = AuthCrypto.NewProofToken();
            db.EmailVerifications.Add(new EmailVerification
            {
                Email = email.ToLowerInvariant(), Purpose = EmailVerificationService.PurposeRegistration,
                CodeHash = AuthCrypto.Hash("000000"), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                ConfirmedAt = DateTimeOffset.UtcNow, ProofHash = AuthCrypto.Hash(raw), ProofExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            });
            await db.SaveChangesAsync();
            return raw;
        });
        return await CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { verificationToken = proof, password = "Str0ng!Pass", firstName = "Sam", lastName = "Ortiz", claimCode = code });
    }
}
