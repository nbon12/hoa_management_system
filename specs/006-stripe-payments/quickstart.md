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

---

## 12. Backups, PITR & Disaster Recovery (FR-036, T087)

Because Stripe is the system of record for card/bank credentials and is itself the authoritative
ledger of money movement, our DR posture only needs to protect the **HOA-owned relational state**
(transactions, drafts, recurring config, mandates, webhook inbox/outbox) and lean on Stripe to
reconstruct anything that crosses the money boundary.

| Concern | Target | Mechanism |
|---------|--------|-----------|
| **Backups** | Continuous | Neon Postgres automated backups (managed). Local/CI uses ephemeral Testcontainers — no backup needed. |
| **PITR** | 7-day window (Neon plan-dependent) | Neon branch-from-timestamp restore. Promote a recovery branch, repoint `ConnectionStrings:Default`, redeploy. |
| **RPO** (max data loss) | ≤ 5 min | Bounded by Neon WAL shipping cadence. The webhook **inbox** (`ProcessedWebhookEvents`) is written *before* the 2xx ack, so any event accepted by Stripe is durably recorded or never acked → Stripe redelivers. |
| **RTO** (max downtime) | ≤ 1 hr | Stateless Cloud Run service: redeploy + restore Neon branch. No local disk state to recover. |

### Stripe-based reconstruction

If the HOA database is lost between the last backup and the incident, replay closes the gap — no
money state is invented locally:

1. **Restore** the most recent Neon backup / PITR branch.
2. **Backfill missed webhooks**: Stripe retries undelivered events for up to 3 days; for older gaps
   list events via the API (`stripe events list --created>=<ts>`) and re-POST them to
   `/payments/webhooks/stripe`. The inbox dedupe (`StripeEventId`) makes replay **idempotent** — already-applied events are acked without re-applying side effects.
3. **Reconcile**: run the reconciliation sweep (Section 7) — it resolves `Pending` ACH past the
   window, flushes the alert outbox, and retries any `Received` inbox rows. Recurring drafts are
   idempotent per `{recurringId}:{period}`, so a re-run after restore cannot double-charge.
4. **Vaulted methods & customers** live entirely in Stripe; we only store `cus_…`/`pm_…` references,
   which the restore brings back. No re-collection of card/bank data is ever required.

---

## 13. End-to-End Validation Procedure (T092)

This is the manual acceptance run that exercises the full money path against Stripe **test mode**.
It complements the automated suites (backend Testcontainers + frontend Karma/Cypress) which run in
CI on every PR. The Stripe-CLI/live-webhook legs below are **run locally** — they are intentionally
*not* wired into CI because they require an interactive `stripe login` and a forwarding tunnel.

**Pre-req:** backend + SPA running (Sections 5), `stripe listen` forwarding (Section 3).

1. **One-time card** — `/app/payments/one-time`: pick a preset → method `card` → enter test card
   `4242 4242 4242 4242` in the Payment Element → review shows server-authoritative Amount/Fee/Total
   → Submit → receipt with a confirmation number. Verify in DevTools that **no** request body contains
   a PAN/CVV/account (SC-001).
2. **One-time ACH** — repeat with `eCheck (ACH)` and Stripe's test bank
   (routing `110000000`, account `000123456789`). Posts as `Pending`.
3. **Recurring setup** — `/app/payments/recurring`: choose amount type + draft day → enter a test
   method → accept the mandate → Save → status card flips to **Active** with a masked method.
4. **Draft sweep** — `curl -X POST .../payments/jobs/run-drafts -H "X-Scheduler-Secret: <dev secret>"`
   → a recurring transaction + draft row appears; re-run the same day → **no** duplicate (idempotency).
5. **Webhook handling** — trigger events via the CLI (Section 8): `payment_intent.succeeded`,
   `charge.refunded`, and a recurring `payment_intent.payment_failed`. Confirm statuses update and,
   for the failure with an opted-in resident, an alert is enqueued and dispatched (SC-006 ≤ 5 min).
6. **Reconciliation** — `curl -X POST .../payments/jobs/reconcile -H "X-Scheduler-Secret: <dev secret>"`
   → stuck `Pending` ACH past the window resolves; outbox/inbox drain.
7. **Statement** — `/app/payments/statement`: the ledger, open balance, and **Payments** tab
   (Stripe transaction history, masked methods only) all reflect the activity above.

> **CI note:** Steps 4–6 depend on a live Stripe CLI session and shared-secret-authenticated job
> endpoints, so they are validated locally rather than gated in CI. The deterministic equivalents —
> off-session draft idempotency, webhook inbox dedupe, and reconciliation — are covered by the
> `Integration/Payments` Testcontainers suite that **does** run on every PR.
