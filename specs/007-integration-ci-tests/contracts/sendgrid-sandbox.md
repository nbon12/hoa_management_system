# Contract: SendGrid sandbox-mode seam

**File**: `HOAManagementCompany/Infrastructure/Payments/Alerts/SendGridEmailProvider.cs`
**Option**: `SendGridOptions.Sandbox` (new, default `false`)
**Requirements**: FR-015, FR-016; safety FR-008/FR-009/SC-003

## Behavior contract

| Given | When | Then |
|-------|------|------|
| `Sandbox == true`, valid key + verified sender | `SendAsync(message)` | Request includes `mail_settings.sandbox_mode.enable = true`; SendGrid returns 2xx; `AlertSendResult.Ok()`; **no email delivered** |
| `Sandbox == false` (production default) | `SendAsync(message)` | No `MailSettings` set; behavior identical to today (real delivery) |
| `Sandbox == true`, invalid/unverified sender or bad key | `SendAsync(message)` | SendGrid returns non-2xx or SDK throws → caught → `AlertSendResult.Fail(...)` |
| not `IsConfigured` | `SendAsync(message)` | `AlertSendResult.Fail("SendGrid is not configured.")` (unchanged) |

## Implementation note

Set on the `SendGridMessage` before send:

```csharp
if (_options.Sandbox)
    msg.MailSettings = new MailSettings { SandboxMode = new SandboxMode { Enable = true } };
```

Production path (`Sandbox == false`) constructs the message exactly as today — strictly additive.

## Guardrail (harness-enforced, FR-009)

SendGrid API keys have **no test/live distinction**. The Stage 2 harness MUST assert `SendGrid:Sandbox == true` before invoking the adapter and refuse to run otherwise. Sandbox mode is the only thing preventing real delivery.

## Repowise marker

Wrap the new sandbox branch in a Repowise marker region (`domain=payments-alerts`) — paired START/END comment markers — describing the no-deliver seam.
