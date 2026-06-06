using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Infrastructure.Observability;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Observability;

/// <summary>
/// US4 / FR-004: a DB-reading request produces span(s) carrying the SQL statement text
/// (and per-operation durations) when SQL capture is enabled.
/// </summary>
public class SqlSpanCaptureTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration() =>
        new[] { new KeyValuePair<string, string?>("Observability:CaptureSqlText", "true") };

    private static bool IsSqlStatement(KeyValuePair<string, object?> tag) =>
        tag.Key is "db.statement" or "db.query.text" &&
        tag.Value is string s && s.Contains("SELECT", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task DbReadingRequest_ProducesSpan_WithSqlStatementAndDuration()
    {
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var token = (await login.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["token"]!.ToString();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await Client.GetAsync("/api/v1/auth/me"); // reads the user from PostgreSQL
        FlushTelemetry();

        var sqlSpan = ExportedSpans.FirstOrDefault(s => s.TagObjects.Any(IsSqlStatement));
        Assert.NotNull(sqlSpan);

        // A DB span carries an individual, non-negative duration (FR-004).
        Assert.True(sqlSpan!.Duration >= TimeSpan.Zero);
    }
}

/// <summary>
/// US4 / FR-010: SQL statement text is gated by the CaptureSqlText flag — present when
/// enabled, stripped when disabled — verified directly against the scrubbing processor.
/// </summary>
public class SqlTextGatingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Processor_GatesSqlStatement_OnCaptureFlag(bool captureSqlText)
    {
        const string sql = "SELECT * FROM users WHERE id = 1";
        var policy = new ScrubbingPolicy(new[] { "password" });
        using var processor = new TelemetryScrubbingProcessor(policy, captureSqlText);

        using var source = new ActivitySource("test-sql-gate");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("db-query", ActivityKind.Client)!;
        activity.SetTag("db.statement", sql);
        activity.Stop();

        processor.OnEnd(activity);

        var statement = activity.GetTagItem("db.statement");
        if (captureSqlText)
            Assert.Equal(sql, statement);
        else
            Assert.Null(statement);
    }
}
