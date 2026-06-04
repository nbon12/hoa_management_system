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

        builder.Entity<Property>(e =>
        {
            e.HasIndex(x => x.CommunityId);
            e.HasIndex(x => x.AccountNumber).IsUnique();
            e.Property(x => x.AccountNumber).HasMaxLength(50);
            e.Property(x => x.CommunityId).HasMaxLength(20);
            e.Property(x => x.MonthlyAssessment).HasColumnType("decimal(10,2)");
            e.Property(x => x.AnnualAssessment).HasColumnType("decimal(10,2)");
            e.Property(x => x.LateFeeAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.FinanceChargeRate).HasColumnType("decimal(5,4)");
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
            e.Property(x => x.ChargeAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.PaymentAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.RunningBalance).HasColumnType("decimal(10,2)");
            e.Property(x => x.EntryType).HasConversion<string>();
            e.HasOne(x => x.Property).WithMany(p => p.LedgerEntries)
                .HasForeignKey(x => x.PropertyId);
            e.ToTable("LedgerEntries");
        });

        builder.Entity<RecurringPayment>(e =>
        {
            e.HasIndex(x => x.PropertyId).IsUnique();
            e.Property(x => x.FixedAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.ProcessingFee).HasColumnType("decimal(10,2)");
            e.Property(x => x.AmountType).HasConversion<string>();
            e.Property(x => x.Method).HasConversion<string>();
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
            e.ToTable("Announcements");
        });

        builder.Entity<Poll>(e =>
        {
            e.HasIndex(x => x.CommunityId);
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
            e.ToTable("Violations");
        });

        builder.Entity<CalendarEvent>(e =>
        {
            e.HasIndex(x => x.CommunityId);
            e.Property(x => x.Category).HasConversion<string>();
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
            e.ToTable("HoaDocuments");
        });

        builder.Entity<CommunityExpense>(e =>
        {
            e.HasIndex(x => new { x.CommunityId, x.FiscalYear });
            e.Property(x => x.Amount).HasColumnType("decimal(10,2)");
            e.ToTable("CommunityExpenses");
        });
    }
}
