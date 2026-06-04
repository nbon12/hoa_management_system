using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

public class OneTimePaymentTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task SetAuthHeaderAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]!.ToString()!);
    }

    [Fact]
    public async Task OneTimePayment_Ach_HappyPath_Returns200WithConfirmation()
    {
        await SetAuthHeaderAsync();
        var response = await Client.PostAsJsonAsync("/api/v1/payments/one-time", new
        {
            method = "ach",
            amount = 250.00m,
            routingNumber = "021000021",
            accountNumber = "123456789",
            accountType = "checking"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.True(body!.ContainsKey("confirmationNumber"));
    }

    [Fact]
    public async Task OneTimePayment_Card_AddsFee()
    {
        await SetAuthHeaderAsync();
        var response = await Client.PostAsJsonAsync("/api/v1/payments/one-time", new
        {
            method = "card",
            amount = 250.00m,
            cardNumber = "4111111111111111",
            cardExpiry = "12/27",
            cardCvv = "123",
            cardholderName = "Jane Doe",
            billingZip = "95101"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body!["processingFee"]);
    }

    [Theory]
    [InlineData("ach", 250, null, null, null)]
    [InlineData("ach", 0, "021000021", "123456789", "checking")]
    public async Task OneTimePayment_ValidationErrors_Returns422(
        string method, double amount, string? routing, string? account, string? accountType)
    {
        decimal amountDecimal = (decimal)amount;
        await SetAuthHeaderAsync();
        var response = await Client.PostAsJsonAsync("/api/v1/payments/one-time", new
        {
            method,
            amount = amountDecimal,
            routingNumber = routing,
            accountNumber = account,
            accountType
        });
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }
}
