# Contract: Gateway-Neutral Provider Event Model

**Applies to**: the boundary between `Infrastructure/Payments/StripeGateway` and `Features/Payments` (FR-021). Internal contract — not exposed over HTTP.

## Interface

```
IStripeGateway.ParseEvent(json: string, signatureHeader: string) : PaymentProviderEvent
```

- Verifies the provider signature (unchanged behavior; throws on invalid signature → webhook endpoint returns 400).
- Maps the raw provider event to `PaymentProviderEvent` (see data-model.md for fields) via `StripeEventTranslator` — a coverable pure translator in `Infrastructure/Payments` (not coverage-excluded; only the gateway's SDK-I/O methods keep `[ExcludeFromCodeCoverage]`).
- All provider-SDK types (`Stripe.*`) remain inside `Infrastructure/Payments`; enforced by architecture test (SC-005: 0 references outside the gateway adapter; today 3).

## Event kind mapping (exhaustive)

| Provider event type | `Kind` |
|---------------------|--------|
| `payment_intent.succeeded` | `PaymentSucceeded` |
| `payment_intent.payment_failed` | `PaymentFailed` |
| `charge.refunded` | `Refunded` |
| ACH return (failed charge with return code) | `AchReturned` |
| `charge.dispute.*` | `DisputeUpdated` |
| anything else | not emitted — inbox row recorded and marked processed-as-ignored (current behavior preserved) |

## Consumer guarantees

- `WebhookProcessor` and `ReconciliationService` depend only on `PaymentProviderEvent` — unit-testable without provider SDK object construction.
- Monetary fields arrive in major units (`decimal`); minor-unit conversion (`/100m`) happens only in the gateway (FR-015 / MoneyPolicy).
- `EventId` is the inbox dedupe key; semantics unchanged.

## Verification

- Unit tests (container-free project): kind mapping table as a Theory against `StripeEventTranslator`; handler behavior per kind against hand-built `PaymentProviderEvent` values.
- Architecture test: `Stripe` namespace usage confined to `Infrastructure.Payments`.
