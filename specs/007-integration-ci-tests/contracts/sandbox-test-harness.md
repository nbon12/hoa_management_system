# Contract: Sandbox test harness

**Files**: `HOAManagementCompany.Tests/Fixtures/SandboxIntegrationTestBase.cs`, `Fixtures/SandboxResult.cs` (both new)
**Requirements**: FR-002, FR-004, FR-005, FR-007, FR-009, FR-010

## `SandboxIntegrationTestBase : IntegrationTestBase`

- Does **not** override `IStripeGateway` or `IAlertProvider` → real adapters run (research R1).
- Overrides `ExtraConfiguration()` to inject real test-mode secrets read from environment variables:
  `Stripe:SecretKey`, `Stripe:WebhookSigningSecret`, `SendGrid:ApiKey`, `SendGrid:FromEmail`, `SendGrid:Sandbox=true`,
  `Twilio:AccountSid`, `Twilio:AuthToken`, `Twilio:FromNumber=+15005550006`.
- Exposes a guarded entry per provider:

| Method | Guardrail (FR-009) | Missing-secret behavior (FR-007) |
|--------|--------------------|----------------------------------|
| `RequireStripe()` | `sk_test_`/`rk_test_` prefix | `Assert.Skip("Stripe test key not configured")` |
| `RequireSendGrid()` | `SendGrid:Sandbox == true` | `Assert.Skip("SendGrid not configured")` |
| `RequireTwilio()` | `AccountSid` starts `AC` + `TWILIO_TEST_CREDENTIALS=true` | `Assert.Skip("Twilio test creds not configured")` |

## `SandboxResult` — retry + classification (FR-005, SC-005)

```
RunAsync(Func<Task> probe, retries: 3, backoff: exponential)
  → on transport/availability error (timeout, HttpRequestException, SocketException,
     StripeException status 0/≥500, Twilio 5xx) after retries: throw SkipException("provider unavailable: …")
  → on domain/assertion error (4xx, wrong status, failed assert): rethrow → test Fails
```

- **Independent reporting (FR-004)**: each provider has its own test class/`[Fact]`, so xUnit reports per-provider pass/fail/skip; one provider's failure never masks another's.
- **Secret masking (FR-010)**: the harness never logs raw secret values; failure messages include provider + operation + status code only. GitHub Actions masks registered secrets as a second layer.

## Trait gate (FR-001, FR-002)

Every sandbox test class carries `[Trait("Category", "Sandbox")]`. The PR `test` job excludes it (`--filter Category!=Sandbox`); the `integration-sandbox` job includes only it (`--filter Category=Sandbox`).
