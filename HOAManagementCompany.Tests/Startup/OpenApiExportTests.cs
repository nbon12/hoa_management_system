using System.Text.Json;
using HOAManagementCompany.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HOAManagementCompany.Tests.Startup;

/// <summary>
/// 015 US4 (FR-011): the <c>--export-openapi</c> pipeline must produce a valid, non-empty OpenAPI
/// document — the client type-generation source of truth. Exercises
/// <see cref="StartupTasks.ExportOpenApiAsync"/> against a started host (the TestServer), which is
/// exactly what Program.cs does on an ephemeral loopback port. No database is touched.
/// </summary>
public class OpenApiExportTests
{
    [Fact]
    public async Task ExportOpenApi_ProducesValidNonEmptyDocument()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("environment", "Test");
            // Test defaults disable Swagger; the export path force-registers it in Program.cs —
            // mirror that here for the in-process host.
            builder.UseSetting("Startup:EnableSwagger", "true");
        });
        using var client = factory.CreateClient();   // starts the host → endpoint data sources materialize

        var path = Path.Combine(Path.GetTempPath(), $"nekohoa-openapi-test-{Guid.NewGuid():N}.json");
        try
        {
            var exitCode = await StartupTasks.ExportOpenApiAsync(factory.Services, path);

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var paths = doc.RootElement.GetProperty("paths");
            Assert.True(paths.EnumerateObject().Count() > 30, "expected the full endpoint surface");
            Assert.True(paths.TryGetProperty("/api/v1/payments/one-time/confirm", out _));
            var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
            Assert.True(schemas.TryGetProperty("ConfirmPaymentResponse", out _), "short schema names expected");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(new string[] { "--export-openapi", "/tmp/x.json" }, "/tmp/x.json")]
    [InlineData(new string[] { "--export-openapi" }, null)]   // missing value → not an export run
    [InlineData(new string[] { "--seed" }, null)]
    [InlineData(new string[0], null)]
    public void GetExportOpenApiPath_ParsesFlag(string[] args, string? expected) =>
        Assert.Equal(expected, StartupTasks.GetExportOpenApiPath(args));
}
