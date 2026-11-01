namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Per-account sign-in lockout (016-A FR-A4: 10 failed attempts lock the account for 30 minutes).
/// Bound from <c>"Identity:Lockout"</c>, validated at startup by
/// <see cref="IdentityLockoutOptionsValidator"/>, and applied to ASP.NET Core Identity's lockout settings.
/// </summary>
public sealed class IdentityLockoutOptions
{
    public const string SectionName = "Identity:Lockout";

    /// <summary>Failed sign-in attempts before the account is locked.</summary>
    public int MaxFailedAttempts { get; set; } = 10;

    /// <summary>How long a locked account stays locked.</summary>
    public int LockoutMinutes { get; set; } = 30;
}
