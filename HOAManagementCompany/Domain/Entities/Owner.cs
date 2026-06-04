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

    public Property Property { get; set; } = null!;
}
