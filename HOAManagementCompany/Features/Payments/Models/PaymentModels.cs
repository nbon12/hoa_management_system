namespace HOAManagementCompany.Features.Payments.Models;

public record LedgerRequest(int Page = 1, int PageSize = 50, string? StartDate = null, string? EndDate = null, string? Type = null, string? Search = null);

public record LedgerResponse(IEnumerable<LedgerItemDto> Items, int TotalCount, int Page, int PageSize, int TotalPages);

public record LedgerItemDto(Guid Id, DateOnly EntryDate, string? DocumentNumber, string Description, decimal ChargeAmount, decimal PaymentAmount, decimal RunningBalance, string EntryType);

public record RecurringPaymentDto(Guid Id, string AmountType, decimal? FixedAmount, string Method, int DraftDay, string Status, decimal ProcessingFee, string? RoutingNumberMasked, string? AccountNumberMasked, string? AccountType, string? CardNumberMasked, string? CardExpiry, string? CardholderName, string? BillingZip);

public record RecurringPaymentRequest(string AmountType, decimal? FixedAmount, string Method, int DraftDay, string? RoutingNumber = null, string? AccountNumber = null, string? AccountType = null, string? CardNumber = null, string? CardExpiry = null, string? CardCvv = null, string? CardholderName = null, string? BillingZip = null);

public record DraftEntryDto(Guid Id, DateOnly DraftDate, string SourceLabel, decimal Amount, string Status);
