# Contract: Stripe test-mode adapter surface

**Files**: `Infrastructure/Payments/StripeGateway.cs` (unchanged), `Features/Payments/Webhooks/StripeWebhookEndpoint.cs` (exercised, unchanged)
**Requirements**: FR-011, FR-012, FR-013

No production change to Stripe code. Stage 2 exercises every `IStripeGateway` method the fake bypasses (the operations counted by SC-001).

## Adapter operations covered (FR-011)

| Operation | Test action | Assert |
|-----------|-------------|--------|
| `EnsureCustomerAsync` | create a test customer | non-empty `cus_…` id |
| `CreatePaymentIntentAsync` | create a one-time PI (test amount) | `requires_payment_method`/`succeeded`; `pi_…` id |
| `GetPaymentIntentAsync` | fetch the PI | status + amount echo |
| `CreateSetupIntentAsync` | create SI for the customer | `seti_…` id, client secret |
| `GetSetupIntentResultAsync` | after server-side confirm with `pm_card_visa` | vaulted `pm_…` + brand/last4 |
| `ChargeOffSessionAsync` | charge vaulted PM off-session | `succeeded` |
| `GetChargeAsync` | settlement detail on the resulting charge | `ch_…`, balance txn present |

**Headless SetupIntent confirm (research R5)**: the test confirms the SetupIntent server-side with `pm_card_visa` (test PM) using the test key, reaching `succeeded` without a browser, then calls `GetSetupIntentResultAsync`.

## Webhook signature + persistence (FR-012, FR-013)

| Case | Request | Assert |
|------|---------|--------|
| Valid | `POST /payments/webhooks/stripe` with body = captured event JSON, header `t=<ts>,v1=<HMAC256(<ts>.<body>, whsec_test)>` | `200`; `WebhookEventInbox` row written with matching `StripeEventId`/`EventType` |
| Tampered | same body, corrupted `v1` | `400`; **no** `WebhookEventInbox` row |
| Replay/dedupe (optional, reuses 006) | POST the valid event twice | both `200`; single row; second acked as `duplicate` |

The HMAC is computed in-test (`HMACSHA256`) because Stripe.net exposes no public signer. The real `StripeGateway.ConstructEvent` verifies against the configured `WebhookSigningSecret`.

## Guardrail (FR-009)

Harness requires `Stripe:SecretKey` to start with `sk_test_` or `rk_test_` before any call.
