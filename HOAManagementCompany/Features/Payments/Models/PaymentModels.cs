namespace HOAManagementCompany.Features.Payments.Models;

public record LedgerRequest(int Page = 1, int PageSize = 50, string? StartDate = null, string? EndDate = null, string? Type = null, string? Search = null);

public record LedgerResponse(IEnumerable<LedgerItemDto> Items, int TotalCount, int Page, int PageSize, int TotalPages);

public record LedgerItemDto(Guid Id, DateOnly EntryDate, string? DocumentNumber, string Description, decimal ChargeAmount, decimal PaymentAmount, decimal RunningBalance, string EntryType);

public record RecurringPaymentDto(
    Guid Id, string AmountType, decimal? FixedAmount, string Method, int DraftDay, string Status,
    decimal ProcessingFee, string? MaskedMethod, DateOnly? NextDraftDate, decimal? NextDraftAmount,
    DateTimeOffset? MandateAcceptedAt);

/// <summary>
/// Upsert request for auto-pay (US2). The browser vaults the method via a SetupIntent and submits
/// its id plus an explicit mandate acceptance — no raw card/bank data ever reaches the backend (SC-001).
/// </summary>
public record RecurringPaymentRequest(
    string AmountType, decimal? FixedAmount, int DraftDay, string SetupIntentId,
    bool MandateAccepted, string? MandateText = null, string? MandateVersion = null);

/// <summary>Request to create a SetupIntent for vaulting a payment method (US2, FR-009).</summary>
public record SetupIntentResponse(string SetupIntentId, string ClientSecret, string PublishableKey);

public record DraftEntryDto(Guid Id, DateOnly DraftDate, string SourceLabel, decimal Amount, string Status);

/// <summary>Result of a run-drafts sweep (FR-010): counts for observability.</summary>
public record RunDraftsResponse(int DueCount, int Charged, int Failed, int Skipped);
