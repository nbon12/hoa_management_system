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

## Teardown (called by `pr-env-teardown.yml`, `pr-env.yml` failure cleanup, and the sweep)

```
GET  /v1/webhook_endpoints?limit=100  → find endpoints where metadata[pr] == <pr_number>
DELETE /v1/webhook_endpoints/{id}     (idempotent; ignore 404)
```

The endpoint is created imperatively (not tofu-managed), so a `tofu destroy` alone cannot
remove it. Every path that tears an env down — normal close (`pr-env-teardown.yml`), a FAILED
provision (`pr-env.yml` clean-failure step), and the daily sweep — must deregister it
explicitly, or a live test endpoint is left pointing at a destroyed Cloud Run URL (Stripe
retries it and emails a delivery-failure warning).

## Invariants

- Test mode only; `STRIPE_SECRET_KEY_TEST` is the shared Dev test key (never production).
- The signing secret exists only in Secret Manager (FR-010), never in logs or PR comments.
- Orphaned endpoints (any teardown path missed) are reclaimable via `metadata[pr]`
  cross-referenced against PR state in the sweep, **independent of tofu state**: the sweep
  lists all test endpoints (paginated via `has_more`/`starting_after`, since orphans can
  exceed one 100-row page) and deletes any whose PR is closed/merged. Open PRs are left
  untouched.

## SendGrid / Twilio

Sandbox/test mode, **outbound-only** for in-scope flows — no per-PR inbound endpoint is
created. Shared sandbox credentials are reused; nothing to register or tear down.
