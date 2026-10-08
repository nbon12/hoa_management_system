using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
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
        // An application entering its reminder window, so the sweep has deterministic work for this test.
        var s = await CreateScenarioAsync(3);
        Clock.UtcNow = new DateTimeOffset(2026, 6, 20, 16, 0, 0, TimeSpan.Zero);
        var appId = await CreateApplicationAsync(s, new AppSpec { Received = new DateOnly(2026, 5, 28), Due = new DateOnly(2026, 6, 27) });

        using var req = new HttpRequestMessage(HttpMethod.Post, Url);
        req.Headers.Add("X-Scheduler-Secret", "test-scheduler-shared-secret-placeholder");
        var res = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ArcSweepResultDto>(Json);
        Assert.NotNull(body);
        // The shared database may hold other tests' applications, so counts are lower bounds; the rows are exact.
        Assert.True(body!.RemindersQueued >= 3, $"RemindersQueued was {body.RemindersQueued}");
        var reminders = await WithDbAsync(db => db.OutboxMessages
            .Where(m => m.Kind == ArcEmailKinds.BoardReminder && m.DedupKey!.StartsWith($"arc:{appId}:"))
            .Select(m => m.RecipientUserId!).ToListAsync());
        Assert.Equal(s.Board.Select(b => b.UserId).OrderBy(x => x), reminders.OrderBy(x => x));
    }
}
