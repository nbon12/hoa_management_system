# Contract: Per-PR Stripe test webhook lifecycle (FR-013, D9)

Gives each PR a real, live, **test-mode** Stripe webhook so payment flows are exercised
end-to-end (Q4 clarification), with no real charges and no production credentials.

## Provision (called by `pr-env.yml` after the API URL is known)

```
POST https://api.stripe.com/v1/webhook_endpoints      (Authorization: Bearer <STRIPE_SECRET_KEY_TEST>)
  url            = https://<pr-api-url>/api/v1/payments/webhooks/stripe
  enabled_events = [payment_intent.succeeded, payment_intent.payment_failed,
                    setup_intent.succeeded, charge.refunded, charge.dispute.created,
                    charge.dispute.closed]   (the EVENTS list in scripts/stripe-webhook-register.sh)
  metadata[pr]   = <pr_number>
→ capture response.secret (whsec_...) → write to Secret Manager `pr-<n>-stripe-webhook`
```

The Cloud Run service reads `Stripe__WebhookSigningSecret` from `pr-<n>-stripe-webhook` to verify
signatures. `metadata[pr]` lets the sweep find/delete stranded endpoints.

## Teardown (called by `pr-env-teardown.yml` and the sweep)

```
GET  /v1/webhook_endpoints?limit=100  → find endpoints where metadata[pr] == <pr_number>
DELETE /v1/webhook_endpoints/{id}     (idempotent; ignore 404)
```

## Invariants

- Test mode only; `STRIPE_SECRET_KEY_TEST` is the shared Dev test key (never production).
- The signing secret exists only in Secret Manager (FR-010), never in logs or PR comments.
- Endpoints are deleted by `metadata[pr]` when their PR's environment goes: by teardown, by
  `pr-env.yml`'s failure cleanup, by the sweep when it reclaims a PR that still has `state/pr/<n>`, and by
  `pr-env-repair` `action=reap`. The sweep does not list Stripe endpoints across all PRs:
  `scripts/pr-env-audit.sh` leaves them out (open draft PR #118 proposes that reconciliation).

## Amazon SES / Twilio

*Superseded by specs/026-ses-email-provider: email is Amazon SES (SendGrid removed); its test mode is the mailbox
simulator via `Ses:SimulatorOnly`.* Both are **outbound-only** for in-scope flows: no per-PR inbound endpoint is
created, and nothing is registered or torn down. Per-PR Cloud Run does not yet receive SES or Twilio credentials
(only the 7 shared operator secrets), so PR environments send no email or SMS alerts (spec FR-013 follow-up).
