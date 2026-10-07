using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>T057 — POST /architectural/jobs/sweep is secret-authenticated.</summary>
public class ArcSweepJobEndpointTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private const string Url = "/api/v1/architectural/jobs/sweep";

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-secret")]
    public async Task MissingOrWrongSecret_Returns401(string? secret)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url);
        if (secret is not null) req.Headers.Add("X-Scheduler-Secret", secret);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task CorrectSecret_RunsTheSweep_AndReturnsCounts()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url);
        req.Headers.Add("X-Scheduler-Secret", "test-scheduler-shared-secret-placeholder");
        var res = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ArcSweepResultDto>(Json);
        Assert.NotNull(body);
        Assert.True(body!.RemindersQueued >= 0 && body.LapsesProcessed >= 0 && body.DecisionsAtDueDate >= 0 && body.EmailsDispatched >= 0);
    }
}
