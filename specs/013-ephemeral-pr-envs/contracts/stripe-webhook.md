# Contract: Per-PR Stripe test webhook lifecycle (FR-013, D9)

Gives each PR a real, live, **test-mode** Stripe webhook so payment flows are exercised
end-to-end (Q4 clarification), with no real charges and no production credentials.

## Provision (called by `pr-env.yml` after the API URL is known)

```
POST https://api.stripe.com/v1/webhook_endpoints      (Authorization: Bearer <STRIPE_SECRET_KEY_TEST>)
  url            = https://<pr-api-url>/api/v1/payments/webhook
  enabled_events = [payment_intent.succeeded, payment_intent.payment_failed,
                    setup_intent.succeeded, charge.refunded, ...app's subscribed set]
  metadata[pr]   = <pr_number>
→ capture response.secret (whsec_...) → write to Secret Manager `pr-<n>-stripe-webhook`
```

The Cloud Run service reads `Stripe__WebhookSecret` from `pr-<n>-stripe-webhook` to verify
signatures. `metadata[pr]` lets the sweep find/delete stranded endpoints.

## Teardown (called by `pr-env-teardown.yml` and the sweep)

```
GET  /v1/webhook_endpoints?limit=100  → find endpoints where metadata[pr] == <pr_number>
DELETE /v1/webhook_endpoints/{id}     (idempotent; ignore 404)
```

## Invariants

- Test mode only; `STRIPE_SECRET_KEY_TEST` is the shared Dev test key (never production).
- The signing secret exists only in Secret Manager (FR-010), never in logs or PR comments.
- Orphaned endpoints (teardown missed) are reclaimable via `metadata[pr]` cross-referenced
  against open PRs in the sweep.

## SendGrid / Twilio

Sandbox/test mode, **outbound-only** for in-scope flows — no per-PR inbound endpoint is
created. Shared sandbox credentials are reused; nothing to register or tear down.
