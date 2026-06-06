using System.Net.Http.Json;
using System.Text.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Observability;

/// <summary>
/// US1 / FR-018 / FR-019 / SC-009: an in-flight request writes a structured, valid-JSON
/// log record carrying the minimum field set to the real Serilog sink.
/// </summary>
public class BackendLoggingTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task InFlightRequest_WritesValidStructuredJsonLog_WithMinimumFields()
    {
        await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });

        Assert.NotEmpty(LogSink.JsonLines);

        var parsedAny = false;
        foreach (var line in LogSink.JsonLines)
        {
            using var doc = JsonDocument.Parse(line); // throws if not valid JSON
            var root = doc.RootElement;

            // Compact JSON minimum fields: timestamp (@t) and a message (@m or @mt).
            Assert.True(root.TryGetProperty("@t", out _), "Log record is missing a timestamp (@t).");
            Assert.True(root.TryGetProperty("@m", out _) || root.TryGetProperty("@mt", out _),
                "Log record is missing a message (@m/@mt).");
            parsedAny = true;
        }

        Assert.True(parsedAny);
    }
}
