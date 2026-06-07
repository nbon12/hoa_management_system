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

        // 18 months of history — 16 months fully paid, 2 months outstanding
        for (int i = 18; i >= 3; i--)
        {
            var chargeDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-i));
            balance += 250m;
            entries.Add(new LedgerEntry
            {
                PropertyId = propertyId,
                EntryDate = chargeDate,
                Description = $"Regular Assessment – {chargeDate:MMMM yyyy}",
                DocumentNumber = $"RA{chargeDate:yyyyMM}",
                ChargeAmount = 250m,
                RunningBalance = balance,
                EntryType = LedgerEntryType.RegularAssessment
            });

            balance -= 250m;
            entries.Add(new LedgerEntry
            {
                PropertyId = propertyId,
                EntryDate = chargeDate.AddDays(4),
                Description = "Online Payment – Thank You",
                DocumentNumber = $"PMT{chargeDate:yyyyMM}",
                PaymentAmount = 250m,
                RunningBalance = balance,
                EntryType = LedgerEntryType.Payment
            });
        }

        // Late fee applied ~3 months ago (payment was missed that month)
        var lateFeeMonth = DateOnly.FromDateTime(DateTime.Today.AddMonths(-4));
        balance += 50m;
        entries.Add(new LedgerEntry
        {
            PropertyId = propertyId,
            EntryDate = lateFeeMonth.AddDays(16),
            Description = "Late Fee – Assessment past grace period",
            DocumentNumber = $"LF{lateFeeMonth:yyyyMM}",
            ChargeAmount = 50m,
            RunningBalance = balance,
            EntryType = LedgerEntryType.LateFee
        });

        // Partial payment for that late-fee month
        balance -= 50m;
        entries.Add(new LedgerEntry
        {
            PropertyId = propertyId,
            EntryDate = lateFeeMonth.AddDays(20),
            Description = "Online Payment – Late Fee Cleared",
            DocumentNumber = $"PMT{lateFeeMonth:yyyyMM}B",
            PaymentAmount = 50m,
            RunningBalance = balance,
            EntryType = LedgerEntryType.Payment
        });

        // Current month + last month — NOT paid (outstanding balance)
        for (int i = 2; i >= 1; i--)
        {
            var chargeDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-i));
            balance += 250m;
            entries.Add(new LedgerEntry
            {
                PropertyId = propertyId,
                EntryDate = chargeDate,
                Description = $"Regular Assessment – {chargeDate:MMMM yyyy}",
                DocumentNumber = $"RA{chargeDate:yyyyMM}",
                ChargeAmount = 250m,
                RunningBalance = balance,
                EntryType = LedgerEntryType.RegularAssessment
            });
        }

        db.LedgerEntries.AddRange(entries);

        db.RecurringPayments.Add(new RecurringPayment
        {
            PropertyId = propertyId,
            AmountType = RecurringAmountType.Assessment,
            Method = PaymentMethod.Ach,
            DraftDay = 1,
            Status = "active",
            VaultedPaymentMethodId = "pm_seed_ach",
            MethodBrand = "Bank",
            MethodLast4 = "8847"
        });

        db.DraftEntries.AddRange(
            new DraftEntry { PropertyId = propertyId, DraftDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-5)), SourceLabel = "Monthly Assessment – ACH", Amount = 250m, Status = DraftStatus.Paid },
            new DraftEntry { PropertyId = propertyId, DraftDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-4)), SourceLabel = "Monthly Assessment – ACH", Amount = 250m, Status = DraftStatus.Failed },
            new DraftEntry { PropertyId = propertyId, DraftDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-3)), SourceLabel = "Monthly Assessment – ACH", Amount = 250m, Status = DraftStatus.Paid },
            new DraftEntry { PropertyId = propertyId, DraftDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-2)), SourceLabel = "Monthly Assessment – ACH", Amount = 250m, Status = DraftStatus.Paid },
            new DraftEntry { PropertyId = propertyId, DraftDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-1)), SourceLabel = "Monthly Assessment – ACH", Amount = 250m, Status = DraftStatus.Paid },
            new DraftEntry { PropertyId = propertyId, DraftDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), SourceLabel = "Monthly Assessment – ACH", Amount = 250m, Status = DraftStatus.Scheduled });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("PaymentSeeder complete.");
    }
}
