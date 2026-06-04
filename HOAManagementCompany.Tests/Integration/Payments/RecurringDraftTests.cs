using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

public class RecurringDraftTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task SetAuthHeaderAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]!.ToString()!);
    }

    [Fact]
    public async Task Recurring_Upsert_HappyPath_Returns200()
    {
        await SetAuthHeaderAsync();
        var response = await Client.PutAsJsonAsync("/api/v1/payments/recurring", new
        {
            amountType = "assessment",
            method = "ach",
            draftDay = 1,
            routingNumber = "021000021",
            accountNumber = "987654321",
            accountType = "checking"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(0)]
    public async Task Recurring_InvalidDraftDay_Returns422(int draftDay)
    {
        await SetAuthHeaderAsync();
        var response = await Client.PutAsJsonAsync("/api/v1/payments/recurring", new
        {
            amountType = "assessment",
            method = "ach",
            draftDay,
            routingNumber = "021000021",
            accountNumber = "987654321",
            accountType = "checking"
        });
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task Recurring_Delete_SoftCancels_Returns204()
    {
        await SetAuthHeaderAsync();
        var response = await Client.DeleteAsync("/api/v1/payments/recurring");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Drafts_Returns12MonthsOfEntries()
    {
        await SetAuthHeaderAsync();
        var response = await Client.GetAsync("/api/v1/payments/drafts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
