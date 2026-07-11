using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

// 017-A FR-A8: a validly-signed token missing a required identity claim must produce a clean
// 401 authorization error, never a NullReferenceException-driven 500.
public class ClaimHardeningTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private string MintToken(bool includePropertyId, bool includeCommunityId)
    {
        var config = Services.GetRequiredService<IConfiguration>();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, "resident1@nekohoa.dev"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (includePropertyId) claims.Add(new Claim("propertyId", Guid.NewGuid().ToString()));
        if (includeCommunityId) claims.Add(new Claim("communityId", "community-1"));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private void UseToken(string token) =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Theory]
    [InlineData("/api/v1/property")]
    [InlineData("/api/v1/property/owner")]
    [InlineData("/api/v1/property/address-history")]
    [InlineData("/api/v1/property/directory-fields")]
    [InlineData("/api/v1/dashboard")]
    public async Task MissingPropertyIdClaim_Returns401NotServerError(string route)
    {
        UseToken(MintToken(includePropertyId: false, includeCommunityId: true));

        var response = await Client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingCommunityIdClaim_Dashboard_Returns401NotServerError()
    {
        UseToken(MintToken(includePropertyId: true, includeCommunityId: false));

        var response = await Client.GetAsync("/api/v1/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
