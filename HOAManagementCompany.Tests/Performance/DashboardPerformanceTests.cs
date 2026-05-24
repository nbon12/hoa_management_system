using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Performance;

public class DashboardPerformanceTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private const int Iterations = 20;
    private const int P50ThresholdMs = 200;

    private async Task SetAuthHeaderAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]!.ToString()!);
    }

    [Fact]
    public async Task Dashboard_P50ResponseTime_Under200ms()
    {
        await SetAuthHeaderAsync();
        var times = new List<long>();

        for (int i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var res = await Client.GetAsync("/api/v1/dashboard");
            sw.Stop();
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            times.Add(sw.ElapsedMilliseconds);
        }

        times.Sort();
        var p50 = times[Iterations / 2];
        Assert.True(p50 < P50ThresholdMs, $"Dashboard p50 was {p50}ms (threshold: {P50ThresholdMs}ms)");
    }

    [Fact]
    public async Task Ledger_P50ResponseTime_Under200ms()
    {
        await SetAuthHeaderAsync();
        var times = new List<long>();

        for (int i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var res = await Client.GetAsync("/api/v1/payments/ledger");
            sw.Stop();
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            times.Add(sw.ElapsedMilliseconds);
        }

        times.Sort();
        var p50 = times[Iterations / 2];
        Assert.True(p50 < P50ThresholdMs, $"Ledger p50 was {p50}ms (threshold: {P50ThresholdMs}ms)");
    }
}
