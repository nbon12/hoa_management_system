namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the payments integrations (006-stripe-payments; moved out of
/// the feature slice in 015 US5 so Infrastructure adapters/validators never depend on Features).
/// Bound from the <c>Stripe</c>, <c>Payments</c>, <c>Jobs</c>, <c>Twilio</c>, and
/// <c>SendGrid</c> config sections. Secrets live in appsettings.Secrets.json (local) or
/// environment variables (deployed); appsettings.json carries only non-secret defaults.
/// </summary>
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Restricted API key (rk_…) preferred over a secret key (sk_…). Server-only.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Publishable key (pk_…) — safe to expose to the browser.</summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>Signing secret (whsec_…) for verifying webhook payloads.</summary>
    public string WebhookSigningSecret { get; set; } = string.Empty;

    /// <summary>Allowed clock skew (seconds) for webhook timestamp/replay tolerance (FR-030).</summary>
    public long WebhookToleranceSeconds { get; set; } = 300;
}

/// <summary>Per-deployment payment policy defaults seeded into <c>HoaPaymentConfig</c> (FR-004b).</summary>
public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>NACHA variable-amount notice lead time before a draft (FR-011c).</summary>
    public int VariableNoticeLeadDays { get; set; } = 10;

    /// <summary>Hours after which a still-Pending ACH transaction is swept by reconcile (FR-033).</summary>
    public int ReconcilePendingAchAfterHours { get; set; } = 96;

    public FeeOptions DefaultFee { get; set; } = new();
    public NsfOptions Nsf { get; set; } = new();

    public sealed class FeeOptions
    {
        /// <summary>"Flat" or "Percentage".</summary>
        public string CardFeeType { get; set; } = "Percentage";

        /// <summary>Flat amount (e.g. 1.95) or rate (e.g. 0.03).</summary>
        public decimal CardFeeValue { get; set; }

        /// <summary>"AllCards" or "CreditOnly". Percentage requires CreditOnly.</summary>
        public string CardScope { get; set; } = "CreditOnly";

        public decimal AchFeeValue { get; set; }

        public bool SurchargingEnabled { get; set; }
    }

    public sealed class NsfOptions
    {
        public bool Enabled { get; set; }
        public decimal Amount { get; set; }
    }
}

/// <summary>Auth for Cloud Scheduler-triggered internal job endpoints (run-drafts, reconcile).</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Shared secret expected in the <c>X-Scheduler-Secret</c> header (OIDC is the alt path).</summary>
    public string SchedulerSharedSecret { get; set; } = string.Empty;
}

/// <summary>Twilio SMS credentials (US3 alerts). Alerts are disabled when unset.</summary>
public sealed class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = string.Empty;
    public string ApiKeySid { get; set; } = string.Empty;
    public string ApiKeySecret { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;

    /// <summary>
    /// Test-credential Auth Token (Stage 2 sandbox, 007). When set and <see cref="ApiKeySid"/> is
    /// empty, the adapter authenticates with <c>Init(AccountSid, AuthToken)</c> basic auth so Twilio
    /// honors magic numbers (test mode). Empty in production, where API-key auth is used.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Configured when an account + from-number plus a usable auth pair are present. Production uses
    /// API-key auth (<see cref="ApiKeySid"/> + <see cref="ApiKeySecret"/>); the Stage 2 sandbox uses
    /// basic auth (<see cref="AuthToken"/>). Either pair satisfies the adapter — mirror its auth
    /// selection so an Auth-Token-only test credential isn't reported as "not configured".
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(FromNumber)
        && ((!string.IsNullOrWhiteSpace(ApiKeySid) && !string.IsNullOrWhiteSpace(ApiKeySecret))
            || !string.IsNullOrWhiteSpace(AuthToken));
}

/// <summary>SendGrid email credentials (US3 alerts / receipts). Disabled when unset.</summary>
public sealed class SendGridOptions
{
    public const string SectionName = "SendGrid";

    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "NekoHOA";

    /// <summary>
    /// When true, the adapter sets <c>MailSettings.SandboxMode.Enable=true</c> so SendGrid validates
    /// the request (auth + payload; sender verification is <b>not</b> enforced in sandbox) and returns
    /// 2xx <b>without delivering</b> (Stage 2 sandbox, 007).
    /// SendGrid API keys have no test/live distinction, so this flag is the sole no-deliver guardrail —
    /// it is <b>false</b> in production. The Stage 2 harness refuses to send unless this is true.
    /// </summary>
    public bool Sandbox { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(FromEmail);
}
