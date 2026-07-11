using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Auth.Models;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

// 016-A security regressions: enumeration defense (FR-A3/A5), property-claim takeover blocked
// (FR-A1), and login lockout (FR-A4).
public class AuthSecurityTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task VerifyEmailRequest_IsUniform_ForKnownAndUnknownEmails()
    {
        var known = await Client.PostAsJsonAsync("/api/v1/auth/verify-email/request",
            new { email = "resident@nekohoa.dev" });
        var unknown = await Client.PostAsJsonAsync("/api/v1/auth/verify-email/request",
            new { email = $"nobody-{Guid.NewGuid():N}@nowhere.dev" });

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task VerifyEmailConfirm_WrongCode_ReturnsGenericFailure()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/verify-email/confirm",
            new { email = $"nobody-{Guid.NewGuid():N}@nowhere.dev", code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutValidVerificationAndClaimCode_IsRefusedGenerically()
    {
        // No account-number path exists any more: a made-up proof + claim code cannot claim a property.
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            verificationToken = "bogus-proof",
            password = "Password1!",
            firstName = "Jane",
            lastName = "Doe",
            claimCode = "AAAAA-BBBBB"
        });
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("REGISTRATION_FAILED", body!["code"]?.ToString());
    }

    [Fact]
    public async Task Register_MissingVerificationToken_IsRejectedByValidation()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            password = "Password1!",
            firstName = "Jane",
            lastName = "Doe",
            claimCode = "AAAAA-BBBBB"
        });
        Assert.True(response.StatusCode is (HttpStatusCode)400 or (HttpStatusCode)422);
    }

    [Fact]
    public async Task Login_LocksOutAfterRepeatedFailures_IndependentOfSuccess()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var auth = sp.GetRequiredService<AuthService>();

        var email = $"lockout-{Guid.NewGuid():N}@test.dev";
        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            FirstName = "Lock",
            LastName = "Out",
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        Assert.True((await userManager.CreateAsync(user, "Password1!")).Succeeded);

        for (var i = 0; i < 10; i++)
            await Assert.ThrowsAsync<DomainException>(
                () => auth.LoginAsync(new LoginRequest(email, "wrong-password"), default));

        var locked = await userManager.FindByEmailAsync(email);
        Assert.True(await userManager.IsLockedOutAsync(locked!));

        // Correct password is still refused while locked out.
        await Assert.ThrowsAsync<DomainException>(
            () => auth.LoginAsync(new LoginRequest(email, "Password1!"), default));
    }
}
