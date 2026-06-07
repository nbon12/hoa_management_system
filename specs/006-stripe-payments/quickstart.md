# Developer Quickstart: Stripe Payments

**Feature**: 006-stripe-payments | **Date**: 2026-06-06

---

## Prerequisites

- Docker Desktop running (for Testcontainers PostgreSQL + MinIO)
- .NET 9 SDK
- Node.js 20+
- A Stripe test-mode account (free at stripe.com)
- Optional: Twilio trial account, SendGrid free tier

---

## 1. Backend: Environment Variables

Add to `appsettings.Development.json` or your local `secrets.json` (never commit):

```json
{
  "Stripe": {
    "SecretKey": "sk_test_...",
    "PublishableKey": "pk_test_...",
    "WebhookSigningSecret": "whsec_..."
  },
  "Payments": {
    "AutoPayPageUrl": "http://localhost:4200/payments/recurring",
    "VariableNoticeLeadDays": 10,
    "ReconcilePendingAchAfterHours": 96,
    "DefaultFee": { "CardFeeType": "Percentage", "CardFeeValue": 0.03, "CardScope": "CreditOnly", "AchFeeValue": 0.00, "SurchargingEnabled": true },
    "Nsf": { "Enabled": false, "Amount": 25.00 }
  },
  "Jobs": {
    "SchedulerSharedSecret": "dev-only-local-secret"
  },
  "Twilio": {
    "AccountSid": "AC...",
    "AuthToken": "...",
    "FromNumber": "+15005550006"
  },
  "SendGrid": {
    "ApiKey": "SG...",
    "FromEmail": "noreply@nekohoa.test",
    "FromName": "NekoHOA"
  }
}
```

**Stripe test keys**: Dashboard → Developers → API keys → "Reveal test key".  
**Webhook signing secret**: See step 3 (Stripe CLI).  
**Twilio test number**: `+15005550006` is Twilio's magic test-from number (no real SMS sent in trial).  
**SendGrid**: Use a sandbox/test mode API key; emails are suppressed in sandbox.

---

## 2. Run Migrations

After the migration is generated (`dotnet ef migrations add StripePayments`), apply it:

```bash
cd HOAManagementCompany
dotnet ef database update
```

Or let the app apply it at startup (configured in `Program.cs`).

---

## 3. Stripe CLI — Local Webhook Forwarding

Install the [Stripe CLI](https://stripe.com/docs/stripe-cli):

```bash
brew install stripe/stripe-cli/stripe
stripe login
```

Forward Stripe events to your local backend:

```bash
stripe listen --forward-to http://localhost:5000/api/v1/payments/webhooks/stripe
```

The CLI prints a `whsec_...` signing secret — paste it into `Stripe:WebhookSigningSecret` in your local config.

---

## 4. Frontend: Environment Config

In `neko-hoa/src/environments/environment.ts`:

```ts
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000',
  stripePublishableKey: 'pk_test_...'
};
```

Install Stripe.js dependencies:

```bash
cd neko-hoa
npm install @stripe/stripe-js ngx-stripe
```

---

## 5. Run Locally

**Backend**:
```bash
cd HOAManagementCompany
dotnet run
# API at http://localhost:5000
# Swagger at http://localhost:5000/swagger
```

**Frontend**:
```bash
cd neko-hoa
npm start
# SPA at http://localhost:4200
```

**Stripe CLI** (in a third terminal):
```bash
stripe listen --forward-to http://localhost:5000/api/v1/payments/webhooks/stripe
```

---

## 6. Test the One-Time Payment Flow

1. Log in as a resident with an outstanding balance.
2. Navigate to **Payments → Pay Now**.
3. Select "Current balance" preset → summary shows Amount + Fee + Total.
4. In the Stripe Payment Element, enter test card:
   - **Visa success**: `4242 4242 4242 4242`, any future expiry, any CVV
   - **Decline (insufficient funds)**: `4000 0000 0000 9995`
   - **ACH (bank)**: Use the test bank credentials provided in the Stripe Element
5. Click Pay → confirm screen shows masked method.

Test cards reference: https://stripe.com/docs/testing#cards

---

## 7. Test the Recurring Setup Flow

1. Navigate to **Payments → Auto-Pay**.
2. Toggle auto-pay ON.
3. Select amount type and draft day.
4. Enter a test payment method in the SetupIntent Element.
5. Accept the mandate and save.
6. Verify the "Method on file" row shows `brand •• last4`.

To simulate a draft locally (Cloud Scheduler → internal job endpoint in prod):
```bash
# Drafts due today — auth via the dev shared secret (Cloud Scheduler OIDC in prod)
curl -X POST http://localhost:5000/api/v1/payments/jobs/run-drafts \
  -H "X-Scheduler-Secret: dev-only-local-secret"

# Reconciliation sweep + outbox flush + webhook-inbox retry
curl -X POST http://localhost:5000/api/v1/payments/jobs/reconcile \
  -H "X-Scheduler-Secret: dev-only-local-secret"

# Or run the tests
dotnet test --filter "RecurringDraft"
```

> **Note**: drafts and reconciliation run via **external Cloud Scheduler** in deployed
> environments (the backend scales to zero — no in-process timer). The endpoints are
> idempotent (per-draft `draft:{recurringId}:{period}` key) and safe to re-run.

---

## 8. Test Webhooks Manually

Use the Stripe CLI to send test events:

```bash
# Simulate ACH settlement
stripe trigger payment_intent.succeeded

# Simulate a recurring payment failure (triggers alert if opted in)
stripe trigger payment_intent.payment_failed

# Simulate a refund
stripe trigger charge.refunded
```

Or send a specific event:
```bash
stripe events resend evt_...
```

---

## 9. Run Backend Integration Tests

Tests use Testcontainers (spins up PostgreSQL + MinIO via Docker):

```bash
cd HOAManagementCompany.Tests
dotnet test
```

Stripe is mocked via `IStripeGateway` — no real Stripe API calls in tests. Alert providers (`IAlertProvider`) are mocked — no real SMS/email.

To run only payment tests:
```bash
dotnet test --filter "Integration/Payments"
```

---

## 10. Key Configuration Reference

| Config Key | Default | Notes |
|-----------|---------|-------|
| `Stripe:SecretKey` | — | Required; `sk_test_...` for dev |
| `Stripe:PublishableKey` | — | Passed to frontend via `/payments/options` or env |
| `Stripe:WebhookSigningSecret` | — | Required; from Stripe CLI or Dashboard |
| `Payments:DefaultFee:*` | Percentage/0.03/CreditOnly | Seeds `HoaPaymentConfig`; per-HOA overrides in DB. Percentage ⇒ CreditOnly (debit never %-surcharged, FR-004b) |
| `Payments:Nsf:*` | disabled / $25 | Returned-payment fee (FR-014e); per-HOA toggle in DB |
| `Payments:VariableNoticeLeadDays` | `10` | NACHA variable-amount advance notice lead (FR-011c) |
| `Payments:ReconcilePendingAchAfterHours` | `96` | Window before the sweep chases a stuck ACH `Pending` (FR-033) |
| `Jobs:SchedulerSharedSecret` | — | Auth for `/payments/jobs/*` in local/dev (Cloud Scheduler OIDC in prod) |
| `Payments:AutoPayPageUrl` | — | Link included in failure alerts |
| `Twilio:AccountSid` | — | Optional; alerts disabled if absent |
| `Twilio:AuthToken` | — | Optional |
| `Twilio:FromNumber` | — | Optional |
| `SendGrid:ApiKey` | — | Optional; alerts disabled if absent |
| `SendGrid:FromEmail` | — | Optional |

---

## 11. PCI Compliance Reminder

- The HOA backend **never** receives raw card numbers, CVVs, or bank account numbers.
- Only Stripe payment-method IDs (`pm_...`) and payment-intent IDs (`pi_...`) reach the backend.
- Do **not** log or store any field that could contain a PAN, routing number, or account number.
- Use the existing `TelemetryScrubbingProcessor` — it strips PII from OTel spans automatically.
