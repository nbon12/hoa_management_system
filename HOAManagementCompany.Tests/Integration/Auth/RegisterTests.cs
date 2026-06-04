using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Auth;

public class RegisterTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static readonly object ValidRequest = new
    {
        email = "newuser@nekohoa.dev",
        password = "Password1!",
        firstName = "Jane",
        lastName = "Doe",
        accountNumber = "SAKURA-003"   // unclaimed property seeded for this test
    };

    [Fact]
    public async Task Register_HappyPath_Returns201WithTokenPair()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", ValidRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("token"));
        Assert.True(body.ContainsKey("refreshToken"));
        Assert.True(body.ContainsKey("user"));
    }

    [Theory]
    [InlineData("existing@nekohoa.dev", "Password1!", "Jane", "Doe", "SAKURA-001", HttpStatusCode.Conflict, "EMAIL_TAKEN")]
    [InlineData("new@nekohoa.dev", "Password1!", "Jane", "Doe", "NONEXISTENT", (HttpStatusCode)422, "ACCOUNT_NOT_FOUND")]
    [InlineData("weak@nekohoa.dev", "short", "Jane", "Doe", "SAKURA-001", (HttpStatusCode)422, null)]
    public async Task Register_ErrorCases_ReturnsExpectedStatus(
        string email, string password, string firstName, string lastName,
        string accountNumber, HttpStatusCode expectedStatus, string? expectedCode)
    {
        var request = new { email, password, firstName, lastName, accountNumber };
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(expectedStatus, response.StatusCode);

        if (expectedCode is not null)
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            Assert.NotNull(body);
            Assert.Equal(expectedCode, body["code"]?.ToString());
        }
    }
}
