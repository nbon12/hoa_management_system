namespace HOAManagementCompany.Infrastructure.Payments.Alerts;

/// <summary>
/// The SES no-deliver guard (026 FR-005). With <c>Ses:SimulatorOnly</c> on, only addresses in the SES
/// mailbox simulator domain — real API responses, no delivery to a person — may receive mail. The
/// domain must match exactly (case-insensitive, after trimming), so look-alikes such as
/// <c>simulator.amazonses.com.evil.test</c> are refused.
/// </summary>
public static class SimulatorRecipientGuard
{
    public const string SimulatorDomain = "simulator.amazonses.com";

    public static bool IsAllowed(string? target)
    {
        var trimmed = target?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;
        var at = trimmed.LastIndexOf('@');
        return at >= 0
            && string.Equals(trimmed[(at + 1)..], SimulatorDomain, StringComparison.OrdinalIgnoreCase);
    }
}
