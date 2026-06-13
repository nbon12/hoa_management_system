# CI Secrets Provisioning (Stage 2) — manual step

These six **test-scoped** secrets must be added in **GitHub → Settings → Secrets and variables →
Actions** before the `integration-sandbox` job can run on `main`. They are distinct from production
secrets (FR-006) and are masked in logs by GitHub Actions (FR-010). Until they exist, the sandbox
job's guarded tests **skip** cleanly rather than fail.

| Secret | Provider | Value |
|--------|----------|-------|
| `STRIPE_SECRET_KEY_TEST` | Stripe | a test secret key (`sk_test_…` or `rk_test_…`) |
| `STRIPE_WEBHOOK_SIGNING_SECRET_TEST` | Stripe | the test endpoint's signing secret (`whsec_…`) |
| `SENDGRID_API_KEY` | SendGrid | any key — safety is sandbox mode, not the key |
| `SENDGRID_FROM_EMAIL` | SendGrid | a **verified** sender address |
| `TWILIO_TEST_ACCOUNT_SID` | Twilio | the **test** Account SID (`AC…`) from the console |
| `TWILIO_TEST_AUTH_TOKEN` | Twilio | the **test** Auth Token paired with that SID |

The job also sets non-secret env inline: `SendGrid__Sandbox=true`, `Twilio__FromNumber=+15005550006`,
`TWILIO_TEST_CREDENTIALS=true`.

- [ ] `STRIPE_SECRET_KEY_TEST` added
- [ ] `STRIPE_WEBHOOK_SIGNING_SECRET_TEST` added
- [ ] `SENDGRID_API_KEY` added
- [ ] `SENDGRID_FROM_EMAIL` added (verified sender)
- [ ] `TWILIO_TEST_ACCOUNT_SID` added
- [ ] `TWILIO_TEST_AUTH_TOKEN` added

> The Stripe test webhook signing secret comes from a Stripe **test-mode** webhook endpoint pointed at
> the deployed `/payments/webhooks/stripe`. Stage 2 verifies signatures locally (in-process HMAC), so
> the endpoint need only exist to mint the `whsec_…`; no live delivery is required for CI.
