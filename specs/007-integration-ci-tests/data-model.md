# Phase 1 Data Model: Stage 2 Integration CI

Stage 2 introduces **no database entities and no schema migrations**. The "entities" from the spec map to configuration objects, in-test value objects, and one pre-existing persisted row exercised end-to-end. This document captures their shape, fields, validation rules, and (where relevant) state.

---

## 1. Configuration objects (production, additive)

### `SendGridOptions` (modified — `Features/Payments/PaymentOptions.cs`)

> **Superseded by specs/026-ses-email-provider (PR #208):** `SendGridOptions` was removed. Email uses `SesOptions`: `Region`, `FromEmail`, `FromName` (default "NekoHOA"), optional `AccessKeyId`/`SecretAccessKey`, and `SimulatorOnly` (default `false`; the sole no-deliver guardrail, which limits recipients to `@simulator.amazonses.com`). `IsConfigured` = `Region` and `FromEmail` non-blank. Stage 2 refuses to send unless `Ses:SimulatorOnly == true` (`RequireSes()`). The table below is historical.

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
| `IsConfigured` | bool (computed) | — | AccountSid + FromNumber non-blank **and** either (ApiKeySid + ApiKeySecret) or AuthToken |

**Note** *(amended 2026-10-31; commit 53ebab3 / PR #29)*: `IsConfigured` accepts either auth pairing, mirroring the adapter's auth selection, so an Auth-Token-only test credential is not reported as "not configured". Production (API-key) behavior is unchanged, and the 008 `TwilioOptionsValidator` uses the same pairing rule. The Stage 2 harness still checks `AccountSid` + `AuthToken` explicitly in `RequireTwilio()`.

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
- **SES**: sends to SES mailbox-simulator addresses (`success@` / `bounce@simulator.amazonses.com`). These get a real API response but are never delivered to a person, and no persisted artifact is created. (Previously a SendGrid sandbox-mode send; superseded by specs/026-ses-email-provider.)
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
