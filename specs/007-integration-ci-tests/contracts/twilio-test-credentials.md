# Contract: Twilio test-credential (basic-auth) path

**File**: `HOAManagementCompany/Infrastructure/Payments/Alerts/TwilioSmsProvider.cs`
**Option**: `TwilioOptions.AuthToken` (new, default `""`)
**Requirements**: FR-017, FR-018; safety FR-008/FR-009/SC-003

## Behavior contract

| Given | When | Then |
|-------|------|------|
| `ApiKeySid` empty, `AccountSid`+`AuthToken` set | `SendAsync` | `TwilioClient.Init(AccountSid, AuthToken)` (basic auth) |
| `ApiKeySid` set (production) | `SendAsync` | `TwilioClient.Init(ApiKeySid, ApiKeySecret, AccountSid)` (unchanged) |
| test creds, `From=+15005550006`, `To=+15005550006` | `SendAsync` | message accepted, `Sid` returned, `AlertSendResult.Ok(sid)`, **no real SMS** |
| test creds, `To=+15005550001` | `SendAsync` | Twilio `ApiException` (invalid number) → caught → `AlertSendResult.Fail(...)` |
| not `IsConfigured` | `SendAsync` | `AlertSendResult.Fail("Twilio is not configured.")` (unchanged) |

## Implementation note

```csharp
if (string.IsNullOrWhiteSpace(_options.ApiKeySid) && !string.IsNullOrWhiteSpace(_options.AuthToken))
    TwilioClient.Init(_options.AccountSid, _options.AuthToken);          // test-credential basic auth
else
    TwilioClient.Init(_options.ApiKeySid, _options.ApiKeySecret, _options.AccountSid);  // production
```

The opt-out STOP suffix and `From`/`To` handling are unchanged.

## Magic numbers (Twilio test credentials)

| Number | Role | Result |
|--------|------|--------|
| `+15005550006` | required `From`, and a valid `To` | success |
| `+15005550001` | `To` | invalid-number error (exercises error mapping) |

A non-magic `From` throws under test credentials, so `Twilio:FromNumber` MUST be `+15005550006` in Stage 2.

## Guardrail (harness-enforced, FR-009)

Twilio test SIDs are not syntactically distinguishable from live SIDs. The harness requires `AccountSid` to start with `AC` **and** an explicit `TWILIO_TEST_CREDENTIALS=true` acknowledgement before sending.
