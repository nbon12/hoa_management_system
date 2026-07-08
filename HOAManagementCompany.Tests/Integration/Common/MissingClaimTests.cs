using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Common;

/// <summary>
/// 015 US2 (FR-008): an authenticated session that lacks the required identity attribute must get
/// a clean authorization failure — 403 with code <c>MISSING_CLAIM</c> — never an internal server
/// error from a null-claim dereference.
/// </summary>
public class MissingClaimTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    /// <summary>Mints a validly-signed JWT that deliberately omits property/community claims.</summary>
    private void AuthenticateWithoutPropertyClaims()
    {
        var config = Services.GetRequiredService<IConfiguration>();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "claimless@test.dev"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    [Theory]
    [InlineData("/api/v1/property")]
    [InlineData("/api/v1/dashboard")]
    [InlineData("/api/v1/community/announcements")]
    public async Task MissingIdentityClaim_Returns403MissingClaim_Not500(string route)
    {
        AuthenticateWithoutPropertyClaims();

        var res = await Client.GetAsync(route);

        Assert.Equal(403, (int)res.StatusCode);
        var json = await res.Content.ReadAsStringAsync();
        Assert.Contains("MISSING_CLAIM", json);
    }
}
