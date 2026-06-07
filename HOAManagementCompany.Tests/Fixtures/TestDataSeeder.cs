using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// Seeds deterministic test data for integration tests. Idempotent — skips if resident@nekohoa.dev exists.
/// </summary>
public class TestDataSeeder(ApplicationDbContext db)
{
    private const string PrimaryEmail = "resident@nekohoa.dev";
    private const string SecondaryEmail = "resident2@nekohoa.dev";
    private const string ExistingEmail = "existing@nekohoa.dev";
    private const string CommunityId = "SAKURA";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == PrimaryEmail, ct))
            return;

        // Users (hash passwords with Identity's default hasher)
        var hasher = new PasswordHasher<ApplicationUser>();

        var primaryUser = new ApplicationUser
        {
            Id = "test-user-primary-id",
            Email = PrimaryEmail,
            NormalizedEmail = PrimaryEmail.ToUpperInvariant(),
            UserName = PrimaryEmail,
            NormalizedUserName = PrimaryEmail.ToUpperInvariant(),
            FirstName = "Jane",
            LastName = "Resident",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        primaryUser.PasswordHash = hasher.HashPassword(primaryUser, "Password1!");

        var secondaryUser = new ApplicationUser
        {
            Id = "test-user-secondary-id",
            Email = SecondaryEmail,
            NormalizedEmail = SecondaryEmail.ToUpperInvariant(),
            UserName = SecondaryEmail,
            NormalizedUserName = SecondaryEmail.ToUpperInvariant(),
            FirstName = "John",
            LastName = "Resident",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        secondaryUser.PasswordHash = hasher.HashPassword(secondaryUser, "Password1!");

        var existingUser = new ApplicationUser
        {
            Id = "test-user-existing-id",
            Email = ExistingEmail,
            NormalizedEmail = ExistingEmail.ToUpperInvariant(),
            UserName = ExistingEmail,
            NormalizedUserName = ExistingEmail.ToUpperInvariant(),
            FirstName = "Existing",
            LastName = "User",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        existingUser.PasswordHash = hasher.HashPassword(existingUser, "Password1!");

        db.Users.AddRange(primaryUser, secondaryUser, existingUser);

        // Properties
        var primaryProperty = new Property
        {
            Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            AccountNumber = "SAKURA-001",
            CommunityId = CommunityId,
            CommunityName = "Sakura Heights HOA",
            Address = "1 Sakura Drive",
            City = "San Jose",
            State = "CA",
            Zip = "95101",
            Lot = "A1",
            Section = "1",
            FiscalYear = 2026,
            YearBuilt = 2005,
            Status = "active",
            MonthlyAssessment = 250m,
            AnnualAssessment = 3000m,
            AssessmentDueDay = 1,
            LateFeeAmount = 50m,
            LateFeeGraceDays = 15,
            FinanceChargeRate = 0.015m
        };

        var secondaryProperty = new Property
        {
            Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            AccountNumber = "SAKURA-002",
            CommunityId = CommunityId,
            CommunityName = "Sakura Heights HOA",
            Address = "2 Sakura Drive",
            City = "San Jose",
            State = "CA",
            Zip = "95101",
            Lot = "A2",
            Section = "1",
            FiscalYear = 2026,
            YearBuilt = 2005,
            Status = "active",
            MonthlyAssessment = 250m,
            AnnualAssessment = 3000m,
            AssessmentDueDay = 1,
            LateFeeAmount = 50m,
            LateFeeGraceDays = 15,
            FinanceChargeRate = 0.015m
        };

        // Unclaimed property (no UserProperty link) — used by register happy-path test
        var unclaimedProperty = new Property
        {
            Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
            AccountNumber = "SAKURA-003",
            CommunityId = CommunityId,
            CommunityName = "Sakura Heights HOA",
            Address = "3 Sakura Drive",
            City = "San Jose",
            State = "CA",
            Zip = "95101",
            Lot = "A3",
            Section = "1",
            FiscalYear = 2026,
            YearBuilt = 2005,
            Status = "active",
            MonthlyAssessment = 250m,
            AnnualAssessment = 3000m,
            AssessmentDueDay = 1,
            LateFeeAmount = 50m,
            LateFeeGraceDays = 15,
            FinanceChargeRate = 0.015m
        };

        db.Properties.AddRange(primaryProperty, secondaryProperty, unclaimedProperty);
        await db.SaveChangesAsync(ct);

        // Link users to properties
        db.UserProperties.AddRange(
            new UserProperty { Id = Guid.NewGuid(), UserId = primaryUser.Id, PropertyId = primaryProperty.Id },
            new UserProperty { Id = Guid.NewGuid(), UserId = secondaryUser.Id, PropertyId = secondaryProperty.Id });

        // Owner record for primary
        db.Owners.Add(new Owner
        {
            Id = Guid.NewGuid(),
            PropertyId = primaryProperty.Id,
            FirstName = "Jane",
            LastName = "Resident",
            Email = PrimaryEmail,
            Phone = "408-555-0101",
            MailingToProperty = true,
            VotingRights = true
        });

        // Per-HOA payment policy (006-stripe-payments): 3% credit-card surcharge, flat ACH, NSF fee.
        db.HoaPaymentConfigs.Add(new HoaPaymentConfig
        {
            Id = Guid.NewGuid(),
            CommunityId = CommunityId,
            CardFeeType = FeeType.Percentage,
            CardFeeValue = 0.03m,
            CardScope = CardScope.CreditOnly,
            SurchargingEnabled = true,
            AchFeeValue = 0m,
            NsfFeeEnabled = true,
            NsfFeeAmount = 25m,
        });

        // Directory fields for primary
        db.DirectoryFields.AddRange(
            new DirectoryField { Id = Guid.NewGuid(), PropertyId = primaryProperty.Id, FieldKey = "name", Label = "Full Name", Shared = true },
            new DirectoryField { Id = Guid.NewGuid(), PropertyId = primaryProperty.Id, FieldKey = "email", Label = "Email", Shared = false },
            new DirectoryField { Id = Guid.NewGuid(), PropertyId = primaryProperty.Id, FieldKey = "phone", Label = "Phone", Shared = false },
            new DirectoryField { Id = Guid.NewGuid(), PropertyId = primaryProperty.Id, FieldKey = "address", Label = "Address", Shared = true });

        // Address history
        db.AddressHistories.Add(new AddressHistory
        {
            Id = Guid.NewGuid(),
            PropertyId = primaryProperty.Id,
            EventType = "created",
            Address = "1 Sakura Drive, San Jose CA 95101",
            EffectiveDate = new DateOnly(2005, 6, 1)
        });

        // Ledger entries (12 months of assessments + payments + 1 late fee).
        // Sequence is the append-only per-property ordering enforced by IX_LedgerEntries_PropertyId_Sequence;
        // the seeder assigns it explicitly since it bypasses LedgerService's advisory-lock allocation.
        decimal balance = 0m;
        long sequence = 0;
        for (int i = 12; i >= 1; i--)
        {
            var date = DateOnly.FromDateTime(DateTime.Today.AddMonths(-i));
            balance += 250m;
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(), PropertyId = primaryProperty.Id, Sequence = ++sequence,
                EntryDate = date, Description = $"Regular Assessment – {date:MMMM yyyy}",
                ChargeAmount = 250m, RunningBalance = balance, EntryType = LedgerEntryType.RegularAssessment
            });
            balance -= 250m;
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(), PropertyId = primaryProperty.Id, Sequence = ++sequence,
                EntryDate = date.AddDays(5), Description = "Online Payment",
                PaymentAmount = 250m, RunningBalance = balance, EntryType = LedgerEntryType.Payment
            });
        }

        db.RecurringPayments.Add(new RecurringPayment
        {
            Id = Guid.NewGuid(), PropertyId = primaryProperty.Id,
            AmountType = RecurringAmountType.Assessment, Method = PaymentMethod.Ach,
            DraftDay = 1, Status = "active", VaultedPaymentMethodId = "pm_seed_ach",
            MethodBrand = "Bank", MethodLast4 = "5678"
        });

        // Community data
        db.Announcements.AddRange(
            new Announcement { Id = Guid.NewGuid(), CommunityId = CommunityId, Title = "Board Meeting – June 2026", Body = "Meeting at 7pm in the clubhouse.", PublishedAt = DateTimeOffset.UtcNow.AddDays(-5), Category = AnnouncementCategory.Board, Pinned = true, AuthorName = "HOA Board" },
            new Announcement { Id = Guid.NewGuid(), CommunityId = CommunityId, Title = "Pool Maintenance", Body = "Pool closed June 1–3.", PublishedAt = DateTimeOffset.UtcNow.AddDays(-10), Category = AnnouncementCategory.Maintenance, AuthorName = "Facilities" });

        var poll = new Poll
        {
            Id = Guid.NewGuid(), CommunityId = CommunityId,
            Question = "Which improvement should we prioritize?",
            ClosingLabel = "Closes June 30", IsActive = true, TotalVotes = 10
        };
        poll.Options.Add(new PollOption { Id = Guid.NewGuid(), PollId = poll.Id, OptionIndex = 0, OptionText = "Playground", VoteCount = 6, Percentage = 60m });
        poll.Options.Add(new PollOption { Id = Guid.NewGuid(), PollId = poll.Id, OptionIndex = 1, OptionText = "Tennis courts", VoteCount = 4, Percentage = 40m });
        db.Polls.Add(poll);

        db.Violations.AddRange(
            new Violation { Id = Guid.NewGuid(), PropertyId = primaryProperty.Id, CommunityId = CommunityId, Title = "Overgrown hedges", Category = ViolationCategory.Landscape, Status = ViolationStatus.Open, IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-20)) },
            new Violation { Id = Guid.NewGuid(), PropertyId = primaryProperty.Id, CommunityId = CommunityId, Title = "Parking violation", Category = ViolationCategory.Parking, Status = ViolationStatus.Closed, IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-60)), ResolvedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-55)) });

        db.CalendarEvents.AddRange(
            new CalendarEvent { Id = Guid.NewGuid(), CommunityId = CommunityId, Title = "Board Meeting", EventDate = DateTimeOffset.UtcNow.AddDays(3), Location = "Clubhouse", Category = EventCategory.Board, RsvpEnabled = true },
            new CalendarEvent { Id = Guid.NewGuid(), CommunityId = CommunityId, Title = "Pool Opening", EventDate = DateTimeOffset.UtcNow.AddDays(7), Location = "Pool", Category = EventCategory.Amenity, RsvpEnabled = false });

        db.CommunityExpenses.AddRange(
            new CommunityExpense { Id = Guid.NewGuid(), CommunityId = CommunityId, Label = "Landscaping", Color = "#4CAF50", Amount = 28500m, FiscalYear = 2026 },
            new CommunityExpense { Id = Guid.NewGuid(), CommunityId = CommunityId, Label = "Pool", Color = "#2196F3", Amount = 14200m, FiscalYear = 2026 });

        db.HoaDocuments.AddRange(
            new HoaDocument { Id = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"), CommunityId = CommunityId, Name = "2026 Budget", Category = DocumentCategory.Budgets, EffectiveDate = new DateOnly(2026, 1, 1), FileSizeLabel = "1.2 MB", Pinned = true, StorageKey = "documents/2026/budget.pdf" },
            new HoaDocument { Id = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"), CommunityId = CommunityId, Name = "Community Rules", Category = DocumentCategory.Rules, EffectiveDate = new DateOnly(2024, 1, 1), FileSizeLabel = "2.1 MB", Pinned = false, StorageKey = "documents/rules/rules.pdf" },
            new HoaDocument { Id = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003"), CommunityId = CommunityId, Name = "CC&R Declaration", Category = DocumentCategory.Governing, EffectiveDate = new DateOnly(2005, 6, 1), FileSizeLabel = "5.0 MB", Pinned = true, StorageKey = "documents/governing/ccr-declaration.pdf" });

        await db.SaveChangesAsync(ct);
    }
}
