# Quickstart: Stage 2 Integration CI

How to run and extend the provider sandbox verification locally and in CI.

## What it does

Runs the **real** Stripe / SendGrid / Twilio adapters against each provider's test/sandbox mode on every push to `main`, before the release image builds. Catches integration regressions (webhook signature, payment flow, email payload, SMS formatting) that the mocked PR suite cannot. Zero real charges, emails, or SMS.

## Run locally

```bash
# Requires Docker (Testcontainers spins up PostgreSQL + MinIO) and test-mode secrets.
export Stripe__SecretKey='sk_test_...'
export Stripe__WebhookSigningSecret='whsec_...'
export SendGrid__ApiKey='SG....'
export SendGrid__FromEmail='alerts@yourverifieddomain.dev'
export SendGrid__Sandbox='true'
export Twilio__AccountSid='ACxxxxxxxxtest'
export Twilio__AuthToken='xxxxtest'
export Twilio__FromNumber='+15005550006'
export TWILIO_TEST_CREDENTIALS='true'

# Run ONLY the sandbox tier:
dotnet test --filter Category=Sandbox

# Run everything EXCEPT sandbox (the normal PR suite):
dotnet test --filter Category!=Sandbox
```

Any provider whose secrets are absent **skips** with a clear message (it does not fail). So you can run just the Stripe tier by exporting only the Stripe vars.

## Interpreting results

| Result | Meaning | Action |
|--------|---------|--------|
| Pass | Adapter + sandbox round-trip OK | — |
| **Fail** | Real regression (signature, status, payload) | Fix forward; this blocks deploy on `main` |
| Skipped: "provider unavailable" | Sandbox outage after retries | Re-run; does not block deploy |
| Skipped: "… not configured" | Secret missing | Provide the secret (or intentionally skipping that provider) |

## CI behavior

- Runs as the `integration-sandbox` job in `.github/workflows/test.yml`, **only** on push to `main`.
- `docker-push` depends on it — a failure blocks the release image and alerts the team (no auto-revert).
- Secrets come from the CI secret store and are masked in logs.

## Extending

- Adding a provider operation → add a `[Fact]`/`[Theory]` in the matching `Integration/Sandbox/*Tests.cs`, tag `[Trait("Category","Sandbox")]`, and wrap the call in `SandboxResult.RunAsync(...)` so outages classify correctly.
- New external provider → add an options seam (default-off), a `RequireX()` guardrail, and a `*SandboxTests.cs` class. Keep production behavior inert unless explicitly configured.

## Safety invariants (do not regress)

1. SendGrid: never send unless `Sandbox == true` (only no-deliver guarantee).
2. Twilio: `From` must be a magic number under test credentials; non-magic throws.
3. Stripe: harness refuses non-`sk_test_`/`rk_test_` keys.
4. Assertions target only objects the run created (shared sandbox accounts).
