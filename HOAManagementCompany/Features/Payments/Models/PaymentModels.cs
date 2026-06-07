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

/// <summary>
/// A scheduled/historical auto-pay draft. <see cref="Status"/> is the draft's own lifecycle
/// (Scheduled/Paid/Failed); <see cref="TransactionStatus"/> surfaces the linked
/// <c>PaymentTransaction</c> outcome (null until a charge is attempted) so callers see the
/// authoritative settlement state without a second round-trip (T065).
/// </summary>
public record DraftEntryDto(
    Guid Id, DateOnly DraftDate, string SourceLabel, decimal Amount, string Status, string? TransactionStatus);

/// <summary>Paginated drafts envelope (T065): newest-first window plus the unfiltered total.</summary>
public record DraftsResponse(IEnumerable<DraftEntryDto> Items, int TotalCount, int Limit, int Offset);

/// <summary>Query for the drafts list — limit/offset pagination over the last 12 months.</summary>
public record DraftsRequest(int Limit = 50, int Offset = 0);

/// <summary>Result of a run-drafts sweep (FR-010): counts for observability.</summary>
public record RunDraftsResponse(int DueCount, int Charged, int Failed, int Skipped, int NoticesSent = 0);

/// <summary>Query for an account statement (FR-039): an optional date window, ISO yyyy-MM-dd.</summary>
public record StatementRequest(string? StartDate = null, string? EndDate = null);

/// <summary>One chronological statement line — a charge or a payment with its running balance.</summary>
public record StatementLineDto(
    DateOnly EntryDate, string? DocumentNumber, string Description,
    decimal ChargeAmount, decimal PaymentAmount, decimal RunningBalance, string EntryType);

/// <summary>
/// A periodic account statement (FR-039): the balance carried into the window, every charge and
/// payment within it in ledger order, the period totals, and the balance carried out.
/// </summary>
public record StatementResponse(
    DateOnly StartDate, DateOnly EndDate, decimal OpeningBalance,
    decimal TotalCharges, decimal TotalPayments, decimal ClosingBalance,
    IEnumerable<StatementLineDto> Lines);

/// <summary>One outstanding category in the statutory statement of unpaid assessments.</summary>
public record UnpaidAssessmentLineDto(string Category, decimal Amount);

/// <summary>
/// Statement of unpaid assessments (NC § 47F-3-118): the amount currently owed against the lot as of
/// <see cref="AsOf"/>, any credit on file, and a category breakdown derived by applying lifetime
/// payments to lifetime charges in statutory allocation order (FR-007b).
/// </summary>
public record UnpaidAssessmentsResponse(
    DateOnly AsOf, decimal TotalDue, decimal CreditBalance, IEnumerable<UnpaidAssessmentLineDto> Breakdown);
