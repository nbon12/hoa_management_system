# Phase 1 Data Model: Configuration Option Groups & Validation Rules

This feature introduces no persisted entities and no schema changes. The "data model" here is
the set of **configuration option groups** and the **validation rules** applied to each. All
rules are enforced at startup, uniformly across Development, Test, and Production (clarified
2026-06-13). Secret-*presence* is required everywhere; placeholder values satisfy presence.

Legend: **Req** = required non-empty in every environment (placeholder allowed). Messages name
`Section:Field` and MUST NOT echo values (FR-019).

## StripeOptions  (section `Stripe`)

| Field | Type | Rule |
|-------|------|------|
| `SecretKey` | string | **Req** — non-empty (FR-004). |
| `PublishableKey` | string | **Req** — non-empty. |
| `WebhookSigningSecret` | string | **Req** — non-empty. |
| `WebhookToleranceSeconds` | long | Must be `> 0` (FR-008). |

## PaymentsOptions  (section `Payments`)

| Field | Type | Rule |
|-------|------|------|
| `VariableNoticeLeadDays` | int | `>= 0` (FR-007). |
| `ReconcilePendingAchAfterHours` | int | `> 0` (FR-007). |
| `DefaultFee.CardFeeType` | string | One of `Flat`, `Percentage` (case-insensitive) (FR-006). |
| `DefaultFee.CardScope` | string | One of `AllCards`, `CreditOnly` (FR-006). |
| `DefaultFee.CardFeeValue` | decimal | `>= 0` (FR-006). |
| `DefaultFee.AchFeeValue` | decimal | `>= 0` (FR-006). |
| *(cross-field)* | — | When `CardFeeType == Percentage`, `CardScope` MUST equal `CreditOnly` (FR-006). |
| `Nsf.Amount` | decimal | `>= 0` (when `Nsf.Enabled`, SHOULD be `> 0`). |

## ObservabilityOptions  (section `Observability`)

| Field | Type | Rule |
|-------|------|------|
| `TraceSampleRatio` | double | Inclusive `[0, 1]` (FR-009). |
| `SentryTraceSampleRatio` | double | Inclusive `[0, 1]` (FR-009). |
| `OtlpProtocol` | string | Must equal `http/protobuf` (FR-010). |
| `OtlpEndpoint` | string | Well-formed **absolute** URI (FR-010). |
| `TelemetryProxyMaxBodyBytes` | int | `> 0`. |

> Note: `ObservabilityOptions.FromConfiguration` overlays `OTEL_*` env vars. The validator
> runs against the bound section; the manual overlay path is validated for the same invariants
> (protocol/endpoint) where it can diverge — see contracts.

## StorageOptions  (section `Storage`)

| Field | Type | Rule |
|-------|------|------|
| `ServiceUrl` | string | **Req** — non-empty; well-formed absolute URI (FR-011). |
| `AccessKey` | string | **Req** — non-empty. |
| `SecretKey` | string | **Req** — non-empty. |
| `BucketName` | string | **Req** — non-empty (defaulted to `hoa-documents`). |
| `PublicServiceUrl` | string? | Optional; if set, well-formed absolute URI. |

## JobsOptions  (section `Jobs`)

| Field | Type | Rule |
|-------|------|------|
| `SchedulerSharedSecret` | string | **Req** — non-empty (FR-004). |

## TwilioOptions  (section `Twilio`) — optional integration (FR-012)

Absence MUST NOT block startup. A **partially-configured** provider MUST be rejected.

| Condition | Rule |
|-----------|------|
| All Twilio fields empty | Valid (alerts disabled). |
| Any Twilio field set | `AccountSid` and `FromNumber` MUST be present, **and** a usable auth pair MUST exist: (`ApiKeySid` + `ApiKeySecret`) OR `AuthToken` — mirrors `TwilioOptions.IsConfigured`. |

## SendGridOptions  (section `SendGrid`) — optional integration (FR-012)

| Condition | Rule |
|-----------|------|
| All SendGrid fields empty | Valid (alerts disabled). |
| Any SendGrid field set | `ApiKey` and `FromEmail` MUST be present; `FromEmail` MUST be a valid email — mirrors `SendGridOptions.IsConfigured`. |

## Frontend runtime configuration  (`environment.ts`)

Validated at boot only when `production === true` (FR-017/FR-018).

| Field | Rule |
|-------|------|
| `apiBaseUrl` | Required non-empty. |
| `stripePublishableKey` | Required non-empty. |
| `telemetryUrl` | (Optional for v1 — recommended but not boot-blocking.) |

## Validation infrastructure (not persisted; runtime types)

- **`FluentValidateOptions<T>`** — generic `IValidateOptions<T>` adapter; resolves
  `IValidator<T>`, returns `ValidateOptionsResult.Fail(failures)` or `Success`.
- **`AddValidatedOptions<TOptions, TValidator>(section)`** — DI extension: registers the
  validator as `IValidator<TOptions>`, binds the section, adds the adapter, calls
  `ValidateOnStart()`.
- **Runtime environment** — selects the configuration *source* (placeholders vs. real
  secrets); does **not** alter which rules apply.
