using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Common;

/// <summary>
/// 015 US2 (FR-006, contracts/error-envelope.md): every documented business error returns the
/// uniform <c>{ code, message }</c> envelope with the intended status through the CENTRAL mapping
/// — endpoints carry no per-endpoint error translation, so raising <see cref="Domain.DomainException"/>
/// anywhere must be enough (fail-safe by default).
/// </summary>
public class ErrorContractTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static async Task<(int Status, string? Code, string? Message)> ReadEnvelopeAsync(HttpResponseMessage res)
    {
        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return ((int)res.StatusCode,
            doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null,
            doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null);
    }

    [Theory]
    // code, expected status, endpoint exercised
    [InlineData("INVALID_CREDENTIALS", 401)]
    [InlineData("EMAIL_TAKEN", 409)]
    [InlineData("ACCOUNT_NOT_FOUND", 422)]
    [InlineData("INVALID_REFRESH_TOKEN", 401)]
    public async Task BusinessErrors_ReturnUniformEnvelope(string code, int expectedStatus)
    {
        HttpResponseMessage res = code switch
        {
            "INVALID_CREDENTIALS" => await Client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "resident@nekohoa.dev", password = "definitely-wrong" }),
            "EMAIL_TAKEN" => await Client.PostAsJsonAsync("/api/v1/auth/register",
                new { email = "resident@nekohoa.dev", password = "Password1!", firstName = "A", lastName = "B", accountNumber = "HOA-0001" }),
            "ACCOUNT_NOT_FOUND" => await Client.PostAsJsonAsync("/api/v1/auth/register",
                new { email = $"new-{Guid.NewGuid():N}@test.dev", password = "Password1!", firstName = "A", lastName = "B", accountNumber = $"NOPE-{Guid.NewGuid():N}"[..12] }),
            "INVALID_REFRESH_TOKEN" => await Client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = "not-a-real-token" }),
            _ => throw new ArgumentOutOfRangeException(nameof(code)),
        };

        var (status, actualCode, message) = await ReadEnvelopeAsync(res);
        Assert.Equal(expectedStatus, status);
        Assert.Equal(code, actualCode);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public async Task PropertyAccessDenied_ThroughCentralMapping_ReturnsEnvelope()
    {
        // Authenticate as the seeded resident, then switch to a property they are not linked to.
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await login.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!["token"].GetString());

        var res = await Client.PostAsJsonAsync("/api/v1/auth/switch-property", new { propertyId = Guid.NewGuid() });

        var (status, code, message) = await ReadEnvelopeAsync(res);
        Assert.Equal(403, status);
        Assert.Equal("PROPERTY_ACCESS_DENIED", code);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }
}
