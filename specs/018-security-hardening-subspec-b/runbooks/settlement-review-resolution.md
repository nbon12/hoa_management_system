# Runbook: Settlement amount-mismatch review resolution (FR-B5)

**When**: A settlement's provider-reported received amount did not exactly match (integer minor units) the server-computed expected total, so no ledger credit was written and a `SettlementReviewQueue` row was created with `Status=open`. An operator must investigate and resolve or dismiss it.

**Access**: The review endpoints are gated by the operator shared secret (`X-Scheduler-Secret`) — the same trust tier as the reconciliation job. This is a back-office operation, not a resident action.

## List open items

```bash
curl -s -H "X-Scheduler-Secret: $OPERATOR_SECRET" \
  "$API/api/v1/payments/settlement-review?status=open&limit=50"
```

Each item shows `transactionId`, `expectedAmount`, `providerAmount`, `currency`, `createdAt`.

## Investigate

1. Compare `expectedAmount` vs `providerAmount`. Common benign causes: a Stripe-side adjustment, a partial capture (if ever enabled), or a currency/settlement-timing artifact.
2. Cross-reference the transaction in Stripe (the `PaymentTransaction.StripePaymentIntentId` / `StripeChargeId`) to see the actual charged and received amounts.
3. Decide:
   - **Discrepancy is real and the payment is short/over** → this is a financial exception. Take the corrective action out-of-band (contact the owner / initiate a refund or additional charge via the normal payment flow). **Then** `resolve` the item with a note. The resolve endpoint records the decision; it does **not** post a ledger credit — any corrective credit is a deliberate, separate step.
   - **Discrepancy is benign / expected** (e.g. known adjustment) → `dismiss` with a note explaining why.

## Resolve

```bash
curl -s -X POST -H "X-Scheduler-Secret: $OPERATOR_SECRET" -H "Content-Type: application/json" \
  -d '{"resolvedByUserId":"<operator-user-id-or-omit>","resolutionNote":"Refund issued for 12c short-settle; owner notified."}' \
  "$API/api/v1/payments/settlement-review/<id>/resolve"
```

## Dismiss

```bash
curl -s -X POST -H "X-Scheduler-Secret: $OPERATOR_SECRET" -H "Content-Type: application/json" \
  -d '{"resolutionNote":"Known Stripe rounding on multi-currency payout; amounts reconcile at payout level."}' \
  "$API/api/v1/payments/settlement-review/<id>/dismiss"
```

## Notes
- Resolution is a **records** action for audit; the ledger is never auto-credited from the expected amount (FR-B5 — block, don't guess).
- If mismatches recur systematically, that indicates a server-total computation bug or a provider-integration change — escalate to engineering rather than dismissing repeatedly.
