namespace HOAManagementCompany.Domain.Entities;

public class Owner
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? OwnerName2 { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool MailingToProperty { get; set; } = true;
    public string? MailingAddress { get; set; }
    public bool PaperlessStatements { get; set; }
    public bool SmsReminders { get; set; }
    public bool VotingRights { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Payments / alerts (006-stripe-payments).
    public string? StripeCustomerId { get; set; }
    public bool AlertSmsOptIn { get; set; }
    public bool AlertEmailOptIn { get; set; }

    /// <summary>E.164 phone for SMS alerts; required when <see cref="AlertSmsOptIn"/> (encrypted at rest, FR-029).</summary>
    public string? AlertPhone { get; set; }

    public Property Property { get; set; } = null!;
    public ICollection<AlertConsent> AlertConsents { get; set; } = [];
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = [];
    public ICollection<OutboxMessage> OutboxMessages { get; set; } = [];
}
