using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;

namespace HOAManagementCompany.Seed;

public class PaymentSeeder(ApplicationDbContext db, SeedResult result, ILogger logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var propertyId = result.PrimaryPropertyId;
        decimal balance = 0m;
        var entries = new List<LedgerEntry>();

        for (int i = 12; i >= 1; i--)
        {
            var date = DateOnly.FromDateTime(DateTime.Today.AddMonths(-i));
            balance += 250m;
            entries.Add(new LedgerEntry
            {
                PropertyId = propertyId,
                EntryDate = date,
                Description = $"Regular Assessment – {date:MMMM yyyy}",
                DocumentNumber = $"RA{date:yyyyMM}",
                ChargeAmount = 250m,
                RunningBalance = balance,
                EntryType = LedgerEntryType.RegularAssessment
            });

            balance -= 250m;
            entries.Add(new LedgerEntry
            {
                PropertyId = propertyId,
                EntryDate = date.AddDays(5),
                Description = "Online Payment – Thank You",
                PaymentAmount = 250m,
                RunningBalance = balance,
                EntryType = LedgerEntryType.Payment
            });
        }

        entries.Add(new LedgerEntry
        {
            PropertyId = propertyId,
            EntryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-3)),
            Description = "Late Fee",
            ChargeAmount = 50m,
            RunningBalance = balance + 50m,
            EntryType = LedgerEntryType.LateFee
        });

        db.LedgerEntries.AddRange(entries);

        db.RecurringPayments.Add(new RecurringPayment
        {
            PropertyId = propertyId,
            AmountType = RecurringAmountType.Assessment,
            Method = PaymentMethod.Ach,
            DraftDay = 1,
            Status = "active",
            RoutingNumberMasked = "****1234",
            AccountNumberMasked = "****5678",
            AccountType = "checking"
        });

        db.DraftEntries.AddRange(
            new DraftEntry { PropertyId = propertyId, DraftDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-2)), SourceLabel = "Monthly Assessment – ACH", Amount = 250m, Status = DraftStatus.Paid },
            new DraftEntry { PropertyId = propertyId, DraftDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-1)), SourceLabel = "Monthly Assessment – ACH", Amount = 250m, Status = DraftStatus.Paid },
            new DraftEntry { PropertyId = propertyId, DraftDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), SourceLabel = "Monthly Assessment – ACH", Amount = 250m, Status = DraftStatus.Scheduled });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("PaymentSeeder complete.");
    }
}
