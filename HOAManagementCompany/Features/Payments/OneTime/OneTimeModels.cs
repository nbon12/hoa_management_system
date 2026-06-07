namespace HOAManagementCompany.Features.Payments.OneTime;

/// <summary>Amount presets + fee policy for the one-time payment screen (FR-007, research §10).</summary>
public record PaymentOptionsResponse(
    decimal CurrentBalance,
    decimal CreditBalance,
    decimal NextAssessment,
    DateOnly? NextAssessmentDueDate,
    string CardFeeType,
    decimal CardFeeValue,
    string CardScope,
    bool SurchargingEnabled,
    decimal AchFeeValue);

public record CreateIntentRequest(decimal Amount, string Method);

public record CreateIntentResponse(
    string PaymentIntentId, string ClientSecret, decimal Amount, decimal Fee, decimal Total);

public record ConfirmPaymentRequest(string PaymentIntentId);

public record ConfirmPaymentResponse(
    Guid TransactionId,
    string Status,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal Total,
    string MaskedMethod,
    string? ConfirmationNumber,
    Guid? ReceiptId);

public record TransactionDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal Total,
    decimal CumulativeRefundedAmount,
    string Status,
    string PaymentMethod,
    string MaskedMethod,
    bool IsRecurring);

public record TransactionsResponse(IEnumerable<TransactionDto> Items, int TotalCount, int Limit, int Offset);

public record ReceiptResponse(
    Guid Id,
    Guid TransactionId,
    string ConfirmationNumber,
    string MaskedMethod,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal Total,
    DateTimeOffset IssuedAt);
