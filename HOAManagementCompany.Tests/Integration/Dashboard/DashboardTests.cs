using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Dashboard;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Dashboard;

public class DashboardTests(TestDatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<string> GetTokenAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "resident@nekohoa.dev", password = "Password1!" });
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return body!["token"]!.ToString()!;
    }

    [Fact]
    public async Task GetDashboard_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("UNAUTHORIZED", body!["code"]!.ToString());
    }

    [Fact]
    public async Task GetDashboard_WithValidToken_Returns200WithRequiredFields()
    {
        var token = await GetTokenAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("currentBalance"));
        Assert.True(body.ContainsKey("openViolations"));
        Assert.True(body.ContainsKey("recentActivity"));
        Assert.True(body.ContainsKey("communityExpenses"));
    }

    // 006: Sequence is the authoritative ledger order. A payment booked later with an earlier
    // EntryDate (backdated) must still be the balance the dashboard shows.
    [Fact]
    public async Task GetDashboard_CurrentBalance_UsesTheLatestSequenceNotTheLatestEntryDate()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var propertyId = Guid.NewGuid();
        db.Properties.Add(new HOAManagementCompany.Domain.Entities.Property
        {
            Id = propertyId, AccountNumber = $"SEQ-{propertyId:N}"[..20], CommunityId = TestDataSeeder.SakuraCommunityId,
            Address = "9 Sequence Way", City = "San Jose", State = "CA", Zip = "95101", Lot = "Z9", Section = "9",
            FiscalYear = 2026, YearBuilt = 2005, Status = "active", MonthlyAssessment = 100m, AnnualAssessment = 1200m,
            AssessmentDueDay = 1, LateFeeAmount = 0m, LateFeeGraceDays = 15, FinanceChargeRate = 0m,
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, Sequence = 1, EntryDate = new DateOnly(2026, 10, 1),
            Description = "Assessment", ChargeAmount = 100m, RunningBalance = 100m, EntryType = LedgerEntryType.RegularAssessment,
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(), PropertyId = propertyId, Sequence = 2, EntryDate = new DateOnly(2026, 9, 30),
            Description = "Backdated payment", PaymentAmount = 100m, RunningBalance = 0m, EntryType = LedgerEntryType.Payment,
        });
        await db.SaveChangesAsync();

        var dashboard = await scope.ServiceProvider.GetRequiredService<DashboardService>()
            .GetDashboardAsync(propertyId, TestDataSeeder.SakuraCommunityId);

        Assert.Equal(0m, dashboard.CurrentBalance);
    }
}
