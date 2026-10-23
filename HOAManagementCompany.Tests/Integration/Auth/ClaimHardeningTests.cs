using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

// 017-A FR-A8: a validly-signed token missing a required identity claim must produce a clean
// authorization error, never a NullReferenceException-driven 500. Since 030 (FR-024g) resident
// endpoints check the home against stored data on every request, so a token without a home — or
// with a home the caller isn't linked to — is refused 403 PROPERTY_ACCESS_DENIED.
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
    public async Task MissingPropertyIdClaim_Returns403NotServerError(string route)
    {
        UseToken(MintToken(includePropertyId: false, includeCommunityId: true));

        var response = await Client.GetAsync(route);

        await AssertPropertyAccessDeniedAsync(response);
    }

    [Fact]
    public async Task MissingCommunityIdClaim_Dashboard_Returns403NotServerError()
    {
        UseToken(MintToken(includePropertyId: true, includeCommunityId: false));

        var response = await Client.GetAsync("/api/v1/dashboard");

        await AssertPropertyAccessDeniedAsync(response);
    }

    private static async Task AssertPropertyAccessDeniedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PROPERTY_ACCESS_DENIED", body.RootElement.GetProperty("code").GetString());
    }
}
