# Contract: SES email adapter (`IAlertProvider`, channel `email`)

File: `HOAManagementCompany/Infrastructure/Payments/Alerts/SesEmailProvider.cs`.
Replaces `SendGridEmailProvider`. Callers (`OutboxDispatcher`, `EmailAuthNotifier`) are unchanged.

```csharp
public sealed class SesEmailProvider(
    IOptions<SesOptions> options,
    ISesClientFactory clientFactory,
    ILogger<SesEmailProvider> logger) : IAlertProvider
{
    public string Channel => "email";
    public bool IsConfigured => options.Value.IsConfigured;
    public Task<AlertSendResult> SendAsync(AlertMessage message, CancellationToken ct = default);
}

public interface ISesClientFactory
{
    IAmazonSimpleEmailServiceV2 Create(SesOptions options);   // called at most once; cached
}
```

## SendAsync behavior (in order)

1. If not configured, return `Fail("SES is not configured.")`. The factory is **not** called.
2. If `SimulatorOnly` and `!SimulatorRecipientGuard.IsAllowed(message.Target)`, return
   `Fail("SES SimulatorOnly guard refused a non-simulator recipient.")`. The factory is **not**
   called, and the recipient is not logged.
3. Build a `SendEmailRequest`:
   - `FromEmailAddress = "\"{FromName}\" <{FromEmail}>"`
   - `Destination.ToAddresses = [Target]`
   - `Content.Simple.Subject = Subject ?? "NekoHOA payment alert"` (UTF-8)
   - `Content.Simple.Body.Text = Body` (UTF-8). No HTML part.
4. Call `client.SendEmailAsync(request, ct)`. If the HTTP status is 2xx, return `Ok(response.MessageId)`.
   Otherwise return `Fail($"{SesErrorMapper.RejectedPrefix} HTTP {status}")`.
5. On any exception: `logger.LogWarning("SES email send failed: {Reason}", reason)` with
   `reason = SesErrorMapper.Map(ex)`. **Do not** pass `ex` to the logger, because the SDK message
   can contain the recipient or the access key ID. Return `Fail(reason)`. Never throw.

## Invariants

- No credential value, recipient address or message body appears in any log line or `Error` string.
- With `SimulatorOnly = true`, no network call is made for a non-simulator recipient (proved by a
  unit test with a counting fake `ISesClientFactory`).
- `SesClientFactory` (production): `new AmazonSimpleEmailServiceV2Client(creds?,
  RegionEndpoint.GetBySystemName(Region))`, using `BasicAWSCredentials` when both keys are set and
  the default chain otherwise. It's a thin adapter marked `[ExcludeFromCodeCoverage]`.
  `SesEmailProvider` itself stays in coverage scope.

## DI (Program.cs)

```csharp
builder.Services.AddValidatedOptions<SesOptions, SesOptionsValidator>(builder.Configuration, SesOptions.SectionName);
builder.Services.AddSingleton<ISesClientFactory, SesClientFactory>();
builder.Services.AddSingleton<IAlertProvider, SesEmailProvider>();
```

These replace the SendGrid options and provider registrations. Comments that mention "SendGrid"
(Program.cs auth-notifier block, `AuthNotifier.cs`) are updated to say "SES".
