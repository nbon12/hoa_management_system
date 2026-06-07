using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments.Models;
using HOAManagementCompany.Features.Payments.Statements;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// Phase 6 reporting (T081): the periodic account statement (FR-039) and the statutory statement of
/// unpaid assessments (NC § 47F-3-118). The exact-figure tests run the service against a freshly
/// seeded, isolated property so the math is deterministic on the shared collection database; the
/// HTTP tests prove auth and wiring against the seeded resident.
/// </summary>
public class StatementTests(TestDatabaseFixture fixture) : PaymentTestBase(fixture)
{
    private record Line(LedgerEntryType Type, decimal Charge, decimal Payment, DateOnly Date, string Desc);

    /// <summary>
    /// Seeds (or resets) an isolated property with a deterministic ledger built directly from
    /// <paramref name="lines"/>, computing the monotonic Sequence and running balance ourselves so the
    /// figures are independent of any other test's activity on the shared database.
    /// </summary>
    private async Task SeedLedgerAsync(Guid propertyId, params Line[] lines)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!await db.Properties.AnyAsync(p => p.Id == propertyId))
            db.Properties.Add(new HOAManagementCompany.Domain.Entities.Property
            {
                Id = propertyId,
                AccountNumber = $"SAKURA-{propertyId.ToString("N")[^6..]}",
                CommunityId = "SAKURA",
                CommunityName = "Sakura Heights HOA",
                Address = "78 Statement Way",
                City = "San Jose", State = "CA", Zip = "95101", Lot = "B78", Section = "2",
                FiscalYear = 2026, YearBuilt = 2008, Status = "active",
                MonthlyAssessment = 250m, AnnualAssessment = 3000m,
                AssessmentDueDay = 1, LateFeeAmount = 50m, LateFeeGraceDays = 15, FinanceChargeRate = 0.015m,
            });

        await db.LedgerEntries.Where(e => e.PropertyId == propertyId).ExecuteDeleteAsync();

        long seq = 0;
        decimal running = 0m;
        foreach (var l in lines)
        {
            running += l.Charge - l.Payment;
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(), PropertyId = propertyId, Sequence = ++seq,
                EntryDate = l.Date, Description = l.Desc,
                ChargeAmount = l.Charge, PaymentAmount = l.Payment, RunningBalance = running,
                EntryType = l.Type,
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task<T> WithServiceAsync<T>(Func<StatementService, Task<T>> act)
    {
        using var scope = Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<StatementService>();
        return await act(svc);
    }

    [Fact]
    public async Task Statements_Unauthenticated_Returns401()
    {
        var res = await Client.GetAsync("/api/v1/payments/statements");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task UnpaidAssessments_Unauthenticated_Returns401()
    {
        var res = await Client.GetAsync("/api/v1/payments/unpaid-assessments");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Statements_HttpHappyPath_ReturnsWindowedStatement()
    {
        await AuthenticateAsync();
        var res = await Client.GetAsync("/api/v1/payments/statements?startDate=2026-01-01&endDate=2026-12-31");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<StatementResponse>();
        Assert.NotNull(body);
        Assert.Equal(new DateOnly(2026, 1, 1), body!.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), body.EndDate);
        // Closing = opening carried in + charges − payments within the window (internally consistent).
        Assert.Equal(body.OpeningBalance + body.TotalCharges - body.TotalPayments, body.ClosingBalance);
    }

    [Fact]
    public async Task Statement_Window_CarriesOpeningBalanceAndScopesLines()
    {
        var propertyId = Guid.Parse("dddddddd-0000-0000-0000-000000000081");
        await SeedLedgerAsync(propertyId,
            // Before the window — establishes the opening balance only.
            new Line(LedgerEntryType.RegularAssessment, 250m, 0m, new DateOnly(2025, 12, 1), "Dec assessment"),
            // Inside the window.
            new Line(LedgerEntryType.RegularAssessment, 250m, 0m, new DateOnly(2026, 1, 1), "Jan assessment"),
            new Line(LedgerEntryType.Payment, 0m, 250m, new DateOnly(2026, 1, 10), "Jan payment"),
            new Line(LedgerEntryType.LateFee, 50m, 0m, new DateOnly(2026, 2, 16), "Feb late fee"),
            // After the window — must be excluded.
            new Line(LedgerEntryType.RegularAssessment, 250m, 0m, new DateOnly(2026, 4, 1), "Apr assessment"));

        var stmt = await WithServiceAsync(s => s.GetStatementAsync(
            propertyId, new StatementRequest("2026-01-01", "2026-03-31")));

        Assert.Equal(250m, stmt.OpeningBalance);          // Dec assessment carried in.
        Assert.Equal(3, stmt.Lines.Count());              // Jan/Jan/Feb only.
        Assert.Equal(300m, stmt.TotalCharges);            // 250 + 50.
        Assert.Equal(250m, stmt.TotalPayments);
        Assert.Equal(300m, stmt.ClosingBalance);          // 250 + 300 − 250.
        Assert.Equal(new DateOnly(2026, 1, 1), stmt.Lines.First().EntryDate); // chronological by sequence.
    }

    [Fact]
    public async Task UnpaidAssessments_PartialPayment_AllocatesByStatutoryPriority()
    {
        var propertyId = Guid.Parse("dddddddd-0000-0000-0000-000000000082");
        // Charges total 135; a 110 payment clears the assessment, then 10 of the late fee in priority
        // order (RegularAssessment → LateFee → FinanceCharge), leaving 15 late fee + 10 finance charge.
        await SeedLedgerAsync(propertyId,
            new Line(LedgerEntryType.RegularAssessment, 100m, 0m, new DateOnly(2026, 1, 1), "Assessment"),
            new Line(LedgerEntryType.LateFee, 25m, 0m, new DateOnly(2026, 1, 16), "Late fee"),
            new Line(LedgerEntryType.FinanceCharge, 10m, 0m, new DateOnly(2026, 1, 20), "Finance charge"),
            new Line(LedgerEntryType.Payment, 0m, 110m, new DateOnly(2026, 1, 25), "Partial payment"));

        var result = await WithServiceAsync(s => s.GetUnpaidAssessmentsAsync(propertyId));

        Assert.Equal(25m, result.TotalDue);
        Assert.Equal(0m, result.CreditBalance);
        var byCat = result.Breakdown.ToDictionary(b => b.Category, b => b.Amount);
        Assert.False(byCat.ContainsKey(nameof(LedgerEntryType.RegularAssessment))); // fully paid → omitted.
        Assert.Equal(15m, byCat[nameof(LedgerEntryType.LateFee)]);
        Assert.Equal(10m, byCat[nameof(LedgerEntryType.FinanceCharge)]);
    }

    [Fact]
    public async Task UnpaidAssessments_Overpayment_ReportsCreditAndNothingDue()
    {
        var propertyId = Guid.Parse("dddddddd-0000-0000-0000-000000000083");
        await SeedLedgerAsync(propertyId,
            new Line(LedgerEntryType.RegularAssessment, 100m, 0m, new DateOnly(2026, 1, 1), "Assessment"),
            new Line(LedgerEntryType.Payment, 0m, 150m, new DateOnly(2026, 1, 5), "Overpayment"));

        var result = await WithServiceAsync(s => s.GetUnpaidAssessmentsAsync(propertyId));

        Assert.Equal(0m, result.TotalDue);
        Assert.Empty(result.Breakdown);
        Assert.Equal(50m, result.CreditBalance);
    }
}
