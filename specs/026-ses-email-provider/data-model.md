# Data Model: Replace SendGrid with Amazon SES (026)

No database schema, migration or persisted entity changes. The "model" is configuration plus the
existing in-memory alert types.

## SesOptions (new; replaces SendGridOptions)

Config section `Ses` (env vars `Ses__*`). File: `HOAManagementCompany/Features/Payments/PaymentOptions.cs`.

| Field | Type | Default | Notes |
|---|---|---|---|
| `Region` | string | `""` | AWS region system name, e.g. `us-east-1`. Required when in use. |
| `FromEmail` | string | `""` | Verified sender, e.g. `no-reply@mail.nekohoa.com`. Required when in use; must be a valid address. |
| `FromName` | string | `"NekoHOA"` | Display name. Doesn't count toward "in use". |
| `AccessKeyId` | string | `""` | Optional. Must be paired with `SecretAccessKey`. Secret; never logged. |
| `SecretAccessKey` | string | `""` | Optional. Must be paired with `AccessKeyId`. Secret; never logged. |
| `SimulatorOnly` | bool | `false` | No-deliver guard (FR-005). Production leaves it `false`; the sandbox harness forces `true`. Doesn't count toward "in use". |
| `IsConfigured` | bool (computed) | n/a | `Region` and `FromEmail` both non-blank. |

### Validation (`SesOptionsValidator`, FluentValidation, ValidateOnStart)

"In use" means any of `Region`, `FromEmail`, `AccessKeyId` or `SecretAccessKey` is non-blank.

| Condition (only when in use) | Message |
|---|---|
| `Region` blank | `Ses:Region is required when SES is configured.` |
| `Region` not a known AWS region | `Ses:Region must be a valid AWS region name (e.g. us-east-1).` |
| `FromEmail` blank | `Ses:FromEmail is required when SES is configured.` |
| `FromEmail` set but invalid | `Ses:FromEmail must be a valid email address.` |
| exactly one of `AccessKeyId`/`SecretAccessKey` set | `Ses:AccessKeyId and Ses:SecretAccessKey must be set together.` |

Fully empty → valid (email disabled; auth codes fall back to `LoggingAuthNotifier`).

## SimulatorRecipientGuard (new, pure)

`IsAllowed(string? target) → bool`. Trim the target; take the substring after the **last** `@`;
return `true` only if it equals `simulator.amazonses.com` (OrdinalIgnoreCase). Null, empty, no `@`,
or anything else returns `false`.

| Input | Result |
|---|---|
| `success@simulator.amazonses.com` | allowed |
| `  SUCCESS@Simulator.AmazonSES.com ` | allowed |
| `bounce@simulator.amazonses.com` | allowed |
| `resident@nekohoa.dev` | refused |
| `x@simulator.amazonses.com.evil.test` | refused |
| `x@evilsimulator.amazonses.com` | refused |
| `simulator.amazonses.com` (no `@`) | refused |
| `""` / `null` | refused |

## SesErrorMapper (new, pure)

`Map(Exception) → string reason`, with public constants `UnavailablePrefix = "SES unavailable:"`
and `RejectedPrefix = "SES rejected:"`. See research R5 for the full table. Reasons contain only
the exception type name and HTTP status, never `ex.Message`.

## Existing types (unchanged)

- `AlertMessage(Target, Subject?, Body)`: input.
- `AlertSendResult(Success, ProviderMessageId?, Error?)`. On success, `ProviderMessageId` is set to
  SES's `MessageId` (SendGrid set none).
- `IAlertProvider` with `Channel = "email"`.

## Removed

`SendGridOptions`, `SendGridOptionsValidator`, `SendGridEmailProvider`, the `SendGrid` and
`SendGrid.Extensions.DependencyInjection` packages, and the `SendGrid` block in `appsettings.json`
(replaced by a `Ses` block with a `_comment`).
