using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // <!-- REPOWISE:START domain=schema -->
    // Schema ownership: HOAManagementCompany.Infrastructure.Persistence
    // <!-- REPOWISE:END -->

    public DbSet<UserProperty> UserProperties => Set<UserProperty>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();
    public DbSet<PropertyClaimCode> PropertyClaimCodes => Set<PropertyClaimCode>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<AddressHistory> AddressHistories => Set<AddressHistory>();
    public DbSet<DirectoryField> DirectoryFields => Set<DirectoryField>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<RecurringPayment> RecurringPayments => Set<RecurringPayment>();
    public DbSet<DraftEntry> DraftEntries => Set<DraftEntry>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();
    public DbSet<PollVote> PollVotes => Set<PollVote>();
    public DbSet<Violation> Violations => Set<Violation>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<EventRsvp> EventRsvps => Set<EventRsvp>();
    public DbSet<HoaDocument> HoaDocuments => Set<HoaDocument>();
    public DbSet<CommunityExpense> CommunityExpenses => Set<CommunityExpense>();

    // Board member experience (025-board-overall-design).
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<CommunityMembership> CommunityMemberships => Set<CommunityMembership>();

    // ── Board architectural review (027-board-arc-review) ───────────────────
    public DbSet<CommunityArcSettings> CommunityArcSettings => Set<CommunityArcSettings>();
    public DbSet<ArchitecturalApplication> ArchitecturalApplications => Set<ArchitecturalApplication>();
    public DbSet<ArchitecturalAttachment> ArchitecturalAttachments => Set<ArchitecturalAttachment>();
    public DbSet<ArchitecturalVote> ArchitecturalVotes => Set<ArchitecturalVote>();
    public DbSet<ArchitecturalInfoRequest> ArchitecturalInfoRequests => Set<ArchitecturalInfoRequest>();

    // Payments (006-stripe-payments).
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PaymentAuthorization> PaymentAuthorizations => Set<PaymentAuthorization>();
    public DbSet<AlertConsent> AlertConsents => Set<AlertConsent>();
    public DbSet<WebhookEventInbox> WebhookEventInbox => Set<WebhookEventInbox>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<HoaPaymentConfig> HoaPaymentConfigs => Set<HoaPaymentConfig>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserProperty>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.PropertyId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.PropertyId);
            e.HasOne(x => x.User).WithMany(u => u.UserProperties)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Property).WithMany(p => p.UserProperties)
                .HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // 016-A FR-A3: email-verification gate.
        builder.Entity<EmailVerification>(e =>
        {
            e.HasIndex(x => new { x.Email, x.Purpose });
            e.HasIndex(x => x.ProofHash);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Purpose).HasMaxLength(32);
            e.Property(x => x.CodeHash).HasMaxLength(64);
            e.Property(x => x.ProofHash).HasMaxLength(64);
        });

        // 016-A FR-A1/A1a: one live (unredeemed) claim code per property.
        builder.Entity<PropertyClaimCode>(e =>
        {
            e.HasIndex(x => x.PropertyId).IsUnique().HasFilter("\"RedeemedAt\" IS NULL");
            e.Property(x => x.CodeHash).HasMaxLength(64);
            e.Property(x => x.DeliveredToContact).HasMaxLength(256);
            e.HasOne(x => x.Property).WithMany()
                .HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Property>(e =>
        {
            e.HasIndex(x => x.CommunityId);
            e.HasIndex(x => x.AccountNumber).IsUnique();
            e.Property(x => x.AccountNumber).HasMaxLength(50);
            e.Property(x => x.MonthlyAssessment).HasColumnType("decimal(10,2)");
            e.Property(x => x.AnnualAssessment).HasColumnType("decimal(10,2)");
            e.Property(x => x.LateFeeAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.FinanceChargeRate).HasColumnType("decimal(5,4)");
            e.HasOne(x => x.Community).WithMany(c => c.Properties)
                .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("Properties");
        });

        builder.Entity<Owner>(e =>
        {
            e.HasIndex(x => x.PropertyId).IsUnique();
            e.HasOne(x => x.Property).WithOne(p => p.Owner)
                .HasForeignKey<Owner>(x => x.PropertyId);
            e.ToTable("Owners");
        });

        builder.Entity<AddressHistory>(e =>
        {
            e.HasIndex(x => x.PropertyId);
            e.HasOne(x => x.Property).WithMany(p => p.AddressHistories)
                .HasForeignKey(x => x.PropertyId);
            e.ToTable("AddressHistories");
        });

        builder.Entity<DirectoryField>(e =>
        {
            e.HasIndex(x => new { x.PropertyId, x.FieldKey }).IsUnique();
            e.HasOne(x => x.Property).WithMany(p => p.DirectoryFields)
                .HasForeignKey(x => x.PropertyId);
            e.ToTable("DirectoryFields");
        });

        builder.Entity<LedgerEntry>(e =>
        {
            e.HasIndex(x => x.PropertyId);
            // Deterministic per-property balance recompute order (FR-007d, SC-009).
            e.HasIndex(x => new { x.PropertyId, x.Sequence }).IsUnique();
            e.Property(x => x.ChargeAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.PaymentAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.RunningBalance).HasColumnType("decimal(10,2)");
            e.Property(x => x.EntryType).HasConversion<string>();
            e.Property(x => x.FundCode).HasMaxLength(50);
            e.HasOne(x => x.Property).WithMany(p => p.LedgerEntries)
                .HasForeignKey(x => x.PropertyId);
            // Audit/ledger rows persist independently of the transaction (no cascade).
            e.HasOne(x => x.Transaction).WithMany(t => t.LedgerEntries)
                .HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.SetNull);
            e.ToTable("LedgerEntries");
        });

        builder.Entity<RecurringPayment>(e =>
        {
            e.HasIndex(x => x.PropertyId).IsUnique();
            e.Property(x => x.FixedAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.ProcessingFee).HasColumnType("decimal(10,2)");
            e.Property(x => x.AmountType).HasConversion<string>();
            e.Property(x => x.Method).HasConversion<string>();
            e.Property(x => x.MethodFunding).HasConversion<string>();
            e.Property(x => x.VaultedPaymentMethodId).HasMaxLength(255);
            e.Property(x => x.MethodBrand).HasMaxLength(50);
            e.Property(x => x.MethodLast4).HasMaxLength(4);
            e.HasOne(x => x.Property).WithMany(p => p.RecurringPayments)
                .HasForeignKey(x => x.PropertyId);
            e.ToTable("RecurringPayments");
        });

        builder.Entity<DraftEntry>(e =>
        {
            e.HasIndex(x => x.PropertyId);
            e.Property(x => x.Amount).HasColumnType("decimal(10,2)");
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Property).WithMany(p => p.DraftEntries)
                .HasForeignKey(x => x.PropertyId);
            e.ToTable("DraftEntries");
        });

        builder.Entity<Announcement>(e =>
        {
            e.HasIndex(x => x.CommunityId);
            e.Property(x => x.Category).HasConversion<string>();
            e.HasOne(x => x.Community).WithMany()
                .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("Announcements");
        });

        builder.Entity<Poll>(e =>
        {
            e.HasIndex(x => x.CommunityId);
            e.HasOne(x => x.Community).WithMany()
                .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("Polls");
        });

        builder.Entity<PollOption>(e =>
        {
            e.Property(x => x.Percentage).HasColumnType("decimal(5,2)");
            e.HasOne(x => x.Poll).WithMany(p => p.Options)
                .HasForeignKey(x => x.PollId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("PollOptions");
        });

        builder.Entity<PollVote>(e =>
        {
            e.HasIndex(x => new { x.PollId, x.UserId }).IsUnique();
            e.HasOne(x => x.Poll).WithMany(p => p.Votes)
                .HasForeignKey(x => x.PollId);
            e.ToTable("PollVotes");
        });

        builder.Entity<Violation>(e =>
        {
            e.HasIndex(x => x.PropertyId);
            e.Property(x => x.FineAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.Category).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Property).WithMany(p => p.Violations)
                .HasForeignKey(x => x.PropertyId);
            e.HasIndex(x => x.CommunityId);
            e.HasOne(x => x.Community).WithMany()
                .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("Violations");
        });

        builder.Entity<CalendarEvent>(e =>
        {
            e.HasIndex(x => x.CommunityId);
            e.Property(x => x.Category).HasConversion<string>();
            e.HasOne(x => x.Community).WithMany()
                .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("CalendarEvents");
        });

        builder.Entity<EventRsvp>(e =>
        {
            e.HasIndex(x => new { x.EventId, x.UserId }).IsUnique();
            e.HasOne(x => x.Event).WithMany(ev => ev.Rsvps)
                .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("EventRsvps");
        });

        builder.Entity<HoaDocument>(e =>
        {
            e.HasIndex(x => x.CommunityId);
            e.Property(x => x.Category).HasConversion<string>();
            e.HasOne(x => x.Community).WithMany()
                .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("HoaDocuments");
        });

        builder.Entity<CommunityExpense>(e =>
        {
            e.HasIndex(x => new { x.CommunityId, x.FiscalYear });
            e.Property(x => x.Amount).HasColumnType("decimal(10,2)");
            e.HasOne(x => x.Community).WithMany()
                .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("CommunityExpenses");
        });

        // ── Payments (006-stripe-payments) ──────────────────────────────────
        builder.Entity<Owner>(e =>
        {
            // AlertPhone holds PII (E.164); encrypted at rest at the provider level (Neon, FR-029).
            e.Property(x => x.StripeCustomerId).HasMaxLength(255);
            e.Property(x => x.AlertPhone).HasMaxLength(32);
        });

        builder.Entity<PaymentTransaction>(e =>
        {
            e.HasIndex(x => x.PropertyId);
            e.HasIndex(x => x.OwnerId);
            e.HasIndex(x => x.StripeChargeId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            // One transaction per intent; collapse double-submits (FR-007a). Filtered (where not null).
            e.HasIndex(x => x.StripePaymentIntentId).IsUnique()
                .HasFilter("\"StripePaymentIntentId\" IS NOT NULL");
            e.HasIndex(x => x.IdempotencyKey).IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
            e.Property(x => x.StripePaymentIntentId).HasMaxLength(255);
            e.Property(x => x.StripeChargeId).HasMaxLength(255);
            e.Property(x => x.StripeBalanceTransactionId).HasMaxLength(255);
            e.Property(x => x.StripePayoutId).HasMaxLength(255);
            e.Property(x => x.IdempotencyKey).HasMaxLength(255);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.FailureCode).HasMaxLength(100);
            e.Property(x => x.ReturnCode).HasMaxLength(10);
            e.Property(x => x.GrossAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.FeeAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.Total).HasColumnType("decimal(10,2)");
            e.Property(x => x.CumulativeRefundedAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.ProcessorFeeAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.PaymentMethod).HasConversion<string>();
            e.Property(x => x.CardFunding).HasConversion<string>();
            e.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Owner).WithMany(o => o.PaymentTransactions).HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.ToTable("PaymentTransactions");
        });

        builder.Entity<PaymentAuthorization>(e =>
        {
            e.HasIndex(x => x.RecurringPaymentId);
            e.Property(x => x.MandateVersion).HasMaxLength(50);
            e.Property(x => x.StripeMandateId).HasMaxLength(255);
            // AcceptedIp is PII — encrypted at rest at the provider level (FR-029).
            e.Property(x => x.AcceptedIp).HasMaxLength(64);
            e.HasOne(x => x.RecurringPayment).WithMany(r => r.Authorizations)
                .HasForeignKey(x => x.RecurringPaymentId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("PaymentAuthorizations");
        });

        builder.Entity<AlertConsent>(e =>
        {
            e.HasIndex(x => new { x.OwnerId, x.Channel });
            e.Property(x => x.Channel).HasMaxLength(10);
            e.Property(x => x.Action).HasMaxLength(10);
            e.Property(x => x.SourceIp).HasMaxLength(64);
            e.HasOne(x => x.Owner).WithMany(o => o.AlertConsents)
                .HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("AlertConsents");
        });

        builder.Entity<WebhookEventInbox>(e =>
        {
            e.HasIndex(x => x.StripeEventId).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.StripeEventId).HasMaxLength(255);
            e.Property(x => x.EventType).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>();
            e.ToTable("WebhookEventInbox");
        });

        builder.Entity<OutboxMessage>(e =>
        {
            e.HasIndex(x => x.Status);
            e.Property(x => x.Kind).HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.DedupKey).HasMaxLength(255);
            // One row per dedup token; a re-run of a producing job is a no-op. Filtered (where not null).
            e.HasIndex(x => x.DedupKey).IsUnique()
                .HasFilter("\"DedupKey\" IS NOT NULL");
            e.HasOne<Owner>().WithMany(o => o.OutboxMessages)
                .HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            // 027 R5: board emails target a user instead of an owner; exactly one is set.
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("OutboxMessages", t => t.HasCheckConstraint(
                "CK_OutboxMessages_SingleRecipient",
                "(\"OwnerId\" IS NULL) <> (\"RecipientUserId\" IS NULL)"));
        });

        builder.Entity<HoaPaymentConfig>(e =>
        {
            e.HasIndex(x => x.CommunityId).IsUnique();
            e.HasOne(x => x.Community).WithMany()
                .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.AllocationOrderJson).HasColumnType("jsonb");
            e.Property(x => x.CardFeeType).HasConversion<string>();
            e.Property(x => x.CardScope).HasConversion<string>();
            e.Property(x => x.CardFeeValue).HasColumnType("decimal(10,4)");
            e.Property(x => x.AchFeeValue).HasColumnType("decimal(10,4)");
            e.Property(x => x.NsfFeeAmount).HasColumnType("decimal(10,2)");
            e.ToTable("HoaPaymentConfigs");
        });

        builder.Entity<Receipt>(e =>
        {
            e.HasIndex(x => x.TransactionId).IsUnique();
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.ConfirmationNumber).HasMaxLength(50);
            e.Property(x => x.MaskedMethod).HasMaxLength(50);
            e.Property(x => x.GrossAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.FeeAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.Total).HasColumnType("decimal(10,2)");
            e.HasOne(x => x.Transaction).WithOne(t => t.Receipt)
                .HasForeignKey<Receipt>(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("Receipts");
        });

        builder.Entity<DraftEntry>(e =>
        {
            e.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Board member experience (025-board-overall-design) ───────────────
        builder.Entity<Community>(e =>
        {
            // FR-002: CommunityName is the unique human-readable handle; the GUID
            // Id is the identifier used in all queries and API paths.
            e.HasIndex(x => x.CommunityName).IsUnique();
            e.Property(x => x.Status).HasConversion<string>();
            // FR-003: self-referencing master/sub-association relation.
            e.HasOne(x => x.ParentCommunity).WithMany(c => c.SubCommunities)
                .HasForeignKey(x => x.ParentCommunityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("Communities");
        });

        builder.Entity<CommunityMembership>(e =>
        {
            // A user cannot hold the same role twice in one community; they MAY hold
            // two different roles (each a separate row) — the resolver grants the
            // union of their capabilities (Clarifications 2026-08-23).
            e.HasIndex(x => new { x.UserId, x.CommunityId, x.Role }).IsUnique();
            e.HasIndex(x => x.CommunityId);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Role).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.User).WithMany(u => u.Memberships)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Community).WithMany(c => c.Memberships)
                .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("CommunityMemberships");
        });

        // ── Board architectural review (027-board-arc-review) ───────────────
        builder.Entity<CommunityArcSettings>(e =>
        {
            e.HasKey(x => x.CommunityId);
            e.Property(x => x.LapseRule).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.DecisionRule).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.TimeZoneId).HasMaxLength(64);
            e.Property(x => x.FormalDisapprovalStatement).HasMaxLength(1000);
            e.HasOne(x => x.Community).WithOne()
                .HasForeignKey<CommunityArcSettings>(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.ToTable("CommunityArcSettings");
        });

        builder.Entity<ArchitecturalApplication>(e =>
        {
            e.HasIndex(x => new { x.CommunityId, x.ApplicationNumber, x.Revision }).IsUnique();
            e.HasIndex(x => new { x.CommunityId, x.Status, x.DueDate });
            e.HasIndex(x => x.PropertyId);
            e.HasIndex(x => x.PreviousRevisionId);
            e.Property(x => x.OwnerName).HasMaxLength(200);
            e.Property(x => x.ProjectTitle).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.TimeZoneId).HasMaxLength(64);
            e.Property(x => x.ConditionsOfApproval).HasMaxLength(2000);
            e.Property(x => x.OwnerReason).HasMaxLength(2000);
            e.Property(x => x.ProjectType).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.DecisionRule).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.LapseRule).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DecisionOutcome).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DecisionWording).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.DecisionSource).HasConversion<string>().HasMaxLength(20);
            e.Ignore(x => x.DisplayId);
            e.HasOne(x => x.Community).WithMany()
                .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Property).WithMany()
                .HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PreviousRevision).WithMany()
                .HasForeignKey(x => x.PreviousRevisionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(x => x.ClosedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.ToTable("ArchitecturalApplications", t =>
            {
                t.HasCheckConstraint("CK_ArchitecturalApplications_Revision", "\"Revision\" >= 1");
                t.HasCheckConstraint("CK_ArchitecturalApplications_RevisionLink",
                    "(\"Revision\" = 1) = (\"PreviousRevisionId\" IS NULL)");
                t.HasCheckConstraint("CK_ArchitecturalApplications_ConditionsOnlyWhenApproved",
                    "\"ConditionsOfApproval\" IS NULL OR \"DecisionOutcome\" = 'Approved'");
            });
        });

        builder.Entity<ArchitecturalAttachment>(e =>
        {
            e.HasIndex(x => x.ApplicationId);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.StorageKey).HasMaxLength(500);
            e.HasOne(x => x.Application).WithMany(a => a.Attachments)
                .HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable("ArchitecturalAttachments");
        });

        builder.Entity<ArchitecturalVote>(e =>
        {
            // FR-016: one vote per board member per application revision.
            e.HasIndex(x => new { x.ApplicationId, x.VoterUserId }).IsUnique();
            e.Property(x => x.Choice).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.HasOne(x => x.Application).WithMany(a => a.Votes)
                .HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Voter).WithMany()
                .HasForeignKey(x => x.VoterUserId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("ArchitecturalVotes");
        });

        builder.Entity<ArchitecturalInfoRequest>(e =>
        {
            e.HasIndex(x => x.ApplicationId);
            e.Property(x => x.Message).HasMaxLength(2000);
            e.HasOne(x => x.Application).WithMany(a => a.InfoRequests)
                .HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.RequestedBy).WithMany()
                .HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("ArchitecturalInfoRequests");
        });
    }
}
