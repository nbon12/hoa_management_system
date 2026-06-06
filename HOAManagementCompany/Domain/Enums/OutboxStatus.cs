namespace HOAManagementCompany.Domain.Enums;

/// <summary>Transactional outbox dispatch state (FR-034).</summary>
public enum OutboxStatus
{
    Pending,
    Sent,
    Failed
}
