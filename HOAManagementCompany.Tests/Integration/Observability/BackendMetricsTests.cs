using System.Net.Http.Json;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Observability;

/// <summary>
/// US1 / FR-012: the backend emits request count + duration histogram + error-rate
/// metrics per endpoint to the in-memory metric reader.
/// </summary>
public class BackendMetricsTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task BackendRequest_EmitsHttpServerDurationHistogram_PerEndpoint()
    {
        await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });

        FlushTelemetry();

        // The ASP.NET Core meter's request-duration histogram carries both the request
        // count (its data-point count) and the status code (error-rate dimension).
        var duration = ExportedMetrics.FirstOrDefault(m => m.Name == "http.server.request.duration");
        Assert.NotNull(duration);

        var sawRouteAndStatus = false;
        foreach (ref readonly var point in duration!.GetMetricPoints())
        {
            string? route = null;
            var hasStatus = false;
            foreach (var tag in point.Tags)
            {
                if (tag.Key == "http.route") route = tag.Value as string;
                if (tag.Key == "http.response.status_code") hasStatus = true;
            }
            if (route is not null && route.Contains("login") && hasStatus)
                sawRouteAndStatus = true;
        }

        Assert.True(sawRouteAndStatus,
            "Expected an http.server.request.duration data point tagged with the endpoint route and status code.");
    }
}
