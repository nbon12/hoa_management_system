using System.Net;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Payments;

/// <summary>
/// 030 T101 — stopping autopay with a recorded reason (R9; FR-012, FR-030c), and the draft run's
/// guard that only homes in Active communities are charged.
/// </summary>
public class RecurringCancellationTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    [Fact]
    public async Task TheOwnersOwnCancel_RecordsOwnerCancelled()
    {
        var home = await CreatePropertyAsync(await CreateCommunityInAsync(await CreateCompanyAsync()));
        var (residentId, residentEmail) = await CreateResidentAsync(home);
        var schedule = await CreateAutopayAsync(home, residentId);
        using var resident = await ClientForAsync(residentEmail);

        var res = await resident.DeleteAsync("/api/v1/payments/recurring");

        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        var row = await ScheduleAsync(schedule);
        Assert.Equal(("inactive", RecurringCancellationReasons.OwnerCancelled), (row.Status, row.CancellationReason));
        Assert.NotNull(row.Authorizations.Single(a => a.Id == row.CurrentAuthorizationId).TerminatedAt);
    }

    [Fact]
    public async Task CancelRecurringFor_StopsTheSchedule_WithTheReason_AndNoProcessorCall()
    {
        var home = await CreatePropertyAsync(await CreateCommunityInAsync(await CreateCompanyAsync()));
        var schedule = await CreateAutopayAsync(home, null);

        using (var scope = Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<PaymentService>();
            var cancelled = await service.CancelRecurringForAsync(home, RecurringCancellationReasons.CommunityArchived);
            Assert.Equal(schedule, cancelled?.Id);
            await scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>().SaveChangesAsync();
            // A second call finds nothing active.
            Assert.Null(await service.CancelRecurringForAsync(home, RecurringCancellationReasons.CommunityArchived));
        }

        var row = await ScheduleAsync(schedule);
        Assert.Equal(("inactive", RecurringCancellationReasons.CommunityArchived), (row.Status, row.CancellationReason));
        Assert.NotNull(row.Authorizations.Single(a => a.Id == row.CurrentAuthorizationId).TerminatedAt);
        Assert.Equal("pm_test", row.VaultedPaymentMethodId);
        Assert.Empty(Stripe.CreatedIntents);
        Assert.Empty(Stripe.OffSessionCharges);
        Assert.Empty(Stripe.CreatedSetupIntents);
    }

    [Fact]
    public async Task AScheduleLeftActiveInAnArchivedCommunity_NeverDrafts()
    {
        var companyId = await CreateCompanyAsync();
        var activeHome = await CreatePropertyAsync(await CreateCommunityInAsync(companyId));
        var archivedHome = await CreatePropertyAsync(await CreateCommunityInAsync(companyId, CommunityStatus.Archived));
        await ChargeableAutopayAsync(activeHome);
        var archivedSchedule = await ChargeableAutopayAsync(archivedHome);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/jobs/run-drafts?date=2026-11-01");
        request.Headers.Add("X-Scheduler-Secret", Services.GetRequiredService<IConfiguration>()["Jobs:SchedulerSharedSecret"]);
        var res = await CreateClient().SendAsync(request);

        Assert.True(res.StatusCode == HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        // The run did charge: the Active community's schedule drafted, the Archived one didn't.
        Assert.Single(Stripe.OffSessionCharges);
        Assert.True(await WithDbAsync(db => db.PaymentTransactions.AnyAsync(t => t.PropertyId == activeHome)));
        Assert.False(await WithDbAsync(db => db.PaymentTransactions.AnyAsync(t => t.PropertyId == archivedHome)));
        Assert.Equal("active", (await ScheduleAsync(archivedSchedule)).Status);
    }

    // Due on the 1st, for a fixed amount, with the owner's Stripe customer on file.
    private async Task<Guid> ChargeableAutopayAsync(Guid home)
    {
        var id = await CreateAutopayAsync(home, null);
        await WithDbAsync(async db =>
        {
            await db.RecurringPayments.Where(r => r.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.AmountType, RecurringAmountType.Fixed).SetProperty(r => r.FixedAmount, 100m));
            var customerId = $"cus_test_{Guid.NewGuid():N}";
            await db.Owners.Where(o => o.PropertyId == home).ExecuteUpdateAsync(s => s
                .SetProperty(o => o.StripeCustomerId, customerId));
        });
        return id;
    }

    private Task<RecurringPayment> ScheduleAsync(Guid id) => WithDbAsync(db => db.RecurringPayments.AsNoTracking()
        .Include(r => r.Authorizations).SingleAsync(r => r.Id == id));
}
