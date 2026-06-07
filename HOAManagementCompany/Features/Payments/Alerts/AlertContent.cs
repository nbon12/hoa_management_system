using System.Globalization;
using HOAManagementCompany.Domain.Entities;

namespace HOAManagementCompany.Features.Payments.Alerts;

/// <summary>
/// Renders masked, PII-free failure-alert copy (FR-016, FR-018). Carries the amount and a friendly
/// reason only — never a card/bank number, name, or address. The delivery target lives on the
/// outbox row, not in the body.
/// </summary>
public static class AlertContent
{
    public sealed record FailureCopy(string Sms, string EmailSubject, string EmailBody);

    public static FailureCopy PaymentFailed(PaymentTransaction txn, string reason)
    {
        var amount = txn.Total.ToString("C", CultureInfo.GetCultureInfo("en-US"));
        var kind = txn.IsRecurring ? "automatic payment" : "payment";

        var sms = $"NekoHOA: your {kind} of {amount} could not be completed ({reason}). "
                + "Please sign in to update your payment method.";

        var subject = "NekoHOA: payment could not be completed";
        var body =
            $"Your {kind} of {amount} could not be completed.\n\n"
            + $"Reason: {reason}.\n\n"
            + "Please sign in to your NekoHOA account to review and update your payment method. "
            + "No action was taken against your account beyond this notice.";

        return new FailureCopy(sms, subject, body);
    }

    /// <summary>
    /// NACHA variable-amount advance notice (FR-011c): tells the resident the exact amount and date
    /// of an upcoming open-balance auto-pay draft, far enough ahead to dispute or cancel. PII-free —
    /// amount and date only; the delivery target lives on the outbox row.
    /// </summary>
    public static FailureCopy VariableAmountNotice(decimal amount, DateOnly draftDate)
    {
        var money = amount.ToString("C", CultureInfo.GetCultureInfo("en-US"));
        var when = draftDate.ToString("MMMM d, yyyy", CultureInfo.GetCultureInfo("en-US"));

        var sms = $"NekoHOA: your auto-pay of {money} is scheduled for {when}. "
                + "Sign in to review or cancel before then.";

        var subject = "NekoHOA: upcoming automatic payment";
        var body =
            $"This is advance notice of an upcoming automatic payment.\n\n"
            + $"Amount: {money}\nScheduled draft date: {when}\n\n"
            + "Because your auto-pay is set to your current balance, the amount can change each cycle, "
            + "so we notify you ahead of every draft. If you'd like to review or cancel this payment, "
            + "please sign in to your NekoHOA account before the draft date.";

        return new FailureCopy(sms, subject, body);
    }

    /// <summary>Maps a raw Stripe failure/return code to friendly, non-sensitive copy.</summary>
    public static string FriendlyReason(string? code) => code switch
    {
        null or "" => "the payment was declined",
        "insufficient_funds" or "R01" => "insufficient funds",
        "card_declined" or "generic_decline" => "the card was declined",
        "expired_card" => "the card has expired",
        "account_closed" or "R02" => "the bank account is closed",
        "no_account" or "R03" => "the bank account could not be found",
        "authorization_revoked" or "R07" => "authorization was revoked",
        "payment_stopped" or "R08" => "payment was stopped",
        _ => "the payment was declined",
    };
}
