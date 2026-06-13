# Phase 1 Data Model: Stage 2 Integration CI

Stage 2 introduces **no database entities and no schema migrations**. The "entities" from the spec map to configuration objects, in-test value objects, and one pre-existing persisted row exercised end-to-end. This document captures their shape, fields, validation rules, and (where relevant) state.

---

## 1. Configuration objects (production, additive)

### `SendGridOptions` (modified — `Features/Payments/PaymentOptions.cs`)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `ApiKey` | string | "" | existing |
| `FromEmail` | string | "" | existing |
| `FromName` | string | "NekoHOA" | existing |
| **`Sandbox`** | **bool** | **`false`** | **NEW** — when true, the adapter sets `MailSettings.SandboxMode.Enable=true` (no delivery). Sole no-deliver guardrail for email. |
| `IsConfigured` | bool (computed) | — | existing: `ApiKey` and `FromEmail` non-blank |

**Validation rule (FR-009/R8)**: Stage 2 harness refuses to send unless `Sandbox == true`.

### `TwilioOptions` (modified — `Features/Payments/PaymentOptions.cs`)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `AccountSid` | string | "" | existing — basic-auth username under test creds |
| `ApiKeySid` | string | "" | existing — API-key path (production) |
| `ApiKeySecret` | string | "" | existing |
| **`AuthToken`** | **string** | **""** | **NEW** — test-credential Auth Token; enables basic-auth `Init(AccountSid, AuthToken)` when `ApiKeySid` is empty |
| `FromNumber` | string | "" | existing — must be magic `+15005550006` under test creds |
| `IsConfigured` | bool (computed) | — | existing: AccountSid + ApiKeySid + ApiKeySecret + FromNumber non-blank |

**Note**: `IsConfigured` keeps its current API-key-oriented definition for production. The Stage 2 harness checks the basic-auth fields (`AccountSid`+`AuthToken`+`FromNumber`) explicitly rather than relying on `IsConfigured`, so production semantics are untouched.

### `StripeOptions` (unchanged)

Used as-is. Stage 2 supplies a real `sk_test_…`/`rk_test_…` `SecretKey` and the matching `whsec_test_…` `WebhookSigningSecret`.

---

## 2. In-test value objects (test project, new)

### `SandboxVerificationOutcome` (conceptual — realized as test pass/fail/skip)

| State | Meaning | CI effect |
|-------|---------|-----------|
| `Passed` | Adapter call succeeded and assertions held | Green |
| `Failed` | Domain/assertion failure (regression) | Red → blocks `docker-push` |
| `Unavailable` | Provider transport failure after bounded retry | Skipped (warning annotation) → does **not** block |
| `Unconfigured` | Required secret absent | Skipped with "credential not configured" message |

State is not persisted; it is the xUnit result of each `[Fact]`/`[Theory]`, produced by the `SandboxResult` classifier (research R6).

### `SignedWebhookFixture` (test helper)

| Field | Type | Notes |
|-------|------|-------|
| `Payload` | string (JSON) | captured real Stripe event body |
| `Timestamp` | long (unix) | header `t=` |
| `SignatureHeader` | string | `t=<ts>,v1=<HMAC-SHA256(<ts>.<payload>, whsec)>` |
| `Tamper` | bool | when true, corrupts `v1` to drive the negative case (FR-012) |

---

## 3. Provider-side test artifacts (ephemeral, not stored by us)

These exist only in the providers' sandbox accounts and are created fresh per run (shared-account isolation, Clarifications Q4 — assertions target only self-created objects):

- **Stripe**: test Customer, SetupIntent, PaymentMethod (`pm_card_visa`), PaymentIntent, Charge.
- **SendGrid**: a sandbox-mode send (validated, never delivered) — no persisted artifact.
- **Twilio**: a test-credential message attempt against magic numbers — no real message.

---

## 4. Pre-existing persisted row exercised (no change)

### `WebhookEventInbox` (from 006 — read/write, unchanged schema)

The Stripe webhook test asserts the FR-013 persistence path by POSTing a signed fixture and confirming a row is written:

| Field | Asserted value |
|-------|----------------|
| `StripeEventId` | matches the fixture event id |
| `EventType` | matches the fixture event type |
| `Status` | `Received` → `Processed` (or `Received` on handler failure) |
| `Payload` | the raw fixture JSON |

The negative case (tampered signature) asserts **no** row is created (endpoint 400 before persistence).
