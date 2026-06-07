# 006-stripe-payments — Compliance, Security & Ops Review

> Polish-phase verification covering tasks **T052, T068, T084, T085, T086, T089, T091, T092**.
> Companion ops/DR + e2e procedure live in `quickstart.md` §12–13. Accessibility (T088) is verified
> in the component specs (`statement.component.spec.ts`, `alerts.component.spec.ts`).

## T052 / T068 — Validation, error shape, audit logging, PII-free telemetry

**Validation & error shape.** Every write endpoint validates through a FastEndpoints validator and
returns the framework's standard `ErrorResponse` shape (`{ statusCode, message, errors }`):

- One-time: `CreateIntentEndpoint` (amount > 0, method ∈ {card, ach}), `ConfirmPaymentEndpoint`
  (`PaymentIntentId` required).
- Recurring: `RecurringPaymentValidator` enforces `AmountType`, `DraftDay` ∈ 1–28, `SetupIntentId`
  present, `MandateAccepted == true`, and `FixedAmount > 0` when `AmountType == fixed`.
- Alerts: `UpdateAlertPreferencesValidator` requires an E.164 phone when SMS is opted in.

**Audit logging (FR-029).** Financial-record writes and config changes now emit a structured Serilog
audit line with **non-PII identifiers only** (ids, statuses, counts — never names, card/bank data, or
amounts-as-PII):

| Event | Endpoint | Fields |
|-------|----------|--------|
| `audit payments.one-time.confirm` | `ConfirmPaymentEndpoint` | transactionId, status, method, propertyId |
| `audit payments.recurring.upsert` | `RecurringUpsertEndpoint` | propertyId, amountType, draftDay |
| `audit payments.jobs.run-drafts` | `RunDraftsEndpoint` | asOf, due, charged, failed, skipped, notices |

Each audit line sits on a test-covered success path (`OneTimePaymentTests`, `RecurringSetupTests`,
`RecurringDraftTests`), so the diff-coverage gate is satisfied.

**Swagger.** All payment endpoints are tagged `Payments` and visible in dev Swagger
(`/swagger`, Section 5 of the quickstart); job endpoints are present but secret-authenticated.

**PII-free telemetry.** `TelemetryScrubbingProcessor` strips PII from OTel spans; Sentry/OTel receive
Stripe reference ids (`pi_…`, `pm_…`, `cus_…`) and our own GUIDs only. No PAN/CVV/routing/account ever
reaches a span, log, or the backend at all (SC-001).

## T084 — PII encryption-at-rest review (FR-029)

- **Card/bank credentials:** never stored. Only Stripe references (`cus_…`, `pm_…`, `pi_…`) and the
  PCI-permitted **last 4 / brand / funding** display fields are persisted. There is therefore no
  cardholder data at rest to encrypt — PCI scope is reduced to SAQ-A.
- **At-rest encryption:** the Neon Postgres store is encrypted at rest (managed, AES-256) and in
  transit (TLS-required connection string). Backups inherit the same encryption.
- **Contact PII** (resident alert phone/email) lives in the existing `Owners`/alert tables under the
  same encrypted store; access is via property-scoped endpoints behind auth.
- **Access auditing:** financial-record writes and fee/alert/schedule config changes are audit-logged
  (see T052/T068 above).

## T085 — Rate-limit & fraud-tooling review (FR-028)

- A dedicated **`payments`** fixed-window limiter (`Program.cs`) now guards the money-movement
  endpoints: `CreateIntentEndpoint`, `ConfirmPaymentEndpoint`, and **`SetupIntentEndpoint`**
  (added in this slice) via `RequireRateLimiting("payments")`. The `auth` limiter continues to guard
  login/registration.
- **Job endpoints** (`run-drafts`, `reconcile`) are not user-facing; they are authenticated by the
  `X-Scheduler-Secret` shared secret (Cloud Scheduler OIDC in prod), which is the appropriate control
  for a machine-to-machine trigger rather than a per-IP rate limit.
- **Processor fraud tooling:** card authorization risk is delegated to **Stripe Radar**, which scores
  every PaymentIntent server-side at Stripe. No card data touches us, so application-layer fraud
  scoring is intentionally out of scope; Radar rules are managed in the Stripe dashboard.

## T086 — NC late-fee / interest caps & surcharge gating

- **Fee configuration** is seeded into `HoaPaymentConfig` from `Payments:*` settings
  (`DefaultFee`, `Nsf`) — see quickstart §10. Late-fee / NSF / finance-charge behaviour is
  config-driven (`NsfFeeEnabled`, `NsfFeeAmount`, `AllocationOrderJson`) so NC § 47F-3-102/3-118 caps
  are enforced by configuration rather than hard-coded constants.
- **Surcharge gating:** `FeeCalculator` only adds a card surcharge when
  `config.SurchargingEnabled && funding == Credit`. **`SurchargingEnabled` defaults to `false`**
  (C# `bool` default; column is non-nullable with no implicit true), so a jurisdiction that prohibits
  surcharging is **safe by default** — a surcharge is opt-in per HOA config, never automatic.
- Debit cards are never surcharged (the `funding == Credit` guard), matching card-network rules.

## T089 — SC-008: deprecated masked card/bank columns removed

Migration `20260607044739_RecurringVaultedMethod` **Up** drops all seven legacy masked/raw columns
from `RecurringPayments`:

`AccountNumberMasked`, `AccountType`, `BillingZip`, `CardExpiry`, `CardNumberMasked`,
`CardholderName`, `RoutingNumberMasked`.

They are replaced by the PCI-permitted, Stripe-sourced display fields `MethodBrand`, `MethodFunding`,
and `MethodLast4` (last-4 is explicitly outside PCI cardholder-data scope). A grep of the current
`ApplicationDbContextModelSnapshot.cs` confirms **0** deprecated masked card/bank columns remain.
`Property.AccountNumber` is the HOA member account identifier (not a bank account) and is unrelated.

**Rollback/mitigation:** the migration **Down** re-adds the seven columns as nullable `text`, so a
rollback is non-destructive to schema; because the dropped columns only ever held masked (non-raw)
values and the vaulted-method references are unaffected, a rollback degrades display fidelity but
loses no money state — Stripe remains the source of truth for the method.

## T091 — Gate verification (to confirm on the PR)

The three required CI checks must be green on the final PR:

- **Build & Test** (backend `dotnet test`, Testcontainers).
- **Frontend (neko-hoa)** — `test:ci` (Karma headless), `build`, `build-storybook`, `e2e:ci` (Cypress).
- **Repowise health gate (no LLM).**

Plus the **90% diff-coverage gate** (`diff-cover … --fail-under 90`), which measures C# cobertura
coverage on changed executable lines. This slice's new executable C# lines are the three audit-log
calls and one `RequireRateLimiting` chain edit, all on test-covered success paths; frontend `.ts`,
markdown docs, and comments are not measured by the gate. Sonar PR scan and Codecov changed-file
coverage are reported on the PR. *(Final status recorded once CI completes.)*

## T092 — Quickstart end-to-end validation

The full money-path acceptance run is documented as a repeatable procedure in `quickstart.md` §13,
including the Stripe-CLI webhook legs and the `run-drafts` / `reconcile` job curls. The Stripe-CLI and
shared-secret job steps are validated **locally** (they need an interactive `stripe login` + tunnel);
their deterministic equivalents — off-session draft idempotency, webhook inbox dedupe, reconciliation —
are covered by the `Integration/Payments` Testcontainers suite that runs on every PR.
