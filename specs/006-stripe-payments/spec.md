# Feature Specification: Stripe Payments (One-Time & Recurring)

**Feature Branch**: `006-stripe-payments`  
**Created**: 2026-06-05  
**Status**: Implemented  
**Input**: User description: "Implement the two payment methods for Stripe — recurring billing and one-time payments — for the NekoHOA resident portal. Replace simulated/raw card collection with PCI-compliant hosted payment fields, vault payment methods, record a Stripe-specific transaction audit trail, handle payment lifecycle webhooks, send opt-in SMS/email alerts on recurring payment failures, and emit PII-scrubbed telemetry."

## Clarifications

### Session 2026-06-05

- Q: For a one-time ACH payment, how should the transaction + ledger be treated at submission (ACH settles asynchronously)? → A: Card stays synchronous (Succeeded immediately); ACH records the Transaction as Pending at submit and writes the ledger entry only when the success webhook arrives.
- Q: On each scheduled draft day, what amount should an auto-pay recurring draft charge? → A: At setup the resident chooses either a fixed amount or "current balance due", resolved at draft time.
- Q: If the SMS or email provider fails to send a recurring-failure alert, how should the system respond? → A: Record the failed send (alert.sent metric success=false, errored span), do not retry, and never block the webhook acknowledgement.
- Q: How should the webhook endpoint guarantee idempotency against duplicate event deliveries? → A: Persist each handled processor event ID and skip already-seen events, plus guard on terminal transaction status.
- Q: How should the processing/convenience fee shown in the wireframes be handled? → A: Include it — card payments add a configurable percentage surcharge (≈3%), ACH/bank is free; the fee is shown in the summary, included in the charged total, and recorded. Rate is configurable to accommodate state surcharge restrictions.
- Q: How should the amount-selection model reconcile with the wireframes? → A: Adopt the wireframe presets — one-time: Current balance / Next assessment / Balance + next / Other (custom); recurring: Just the assessment / Whatever I owe (open balance) / Fixed amount.
- Note: This spec is derived from the Claude Design "NekoHOA Wireframes" handoff bundle. The selected directions are one-time **Embedded Payment Element** (Stripe artboard "Idea B") and recurring **Embedded SetupIntent** with a vaulted method-on-file list ("Idea B" + "Idea A"). Stripe Customer Portal ("Idea C") and hosted-checkout redirect ("Idea A") are not selected. Design source files are preserved under `design/` in this feature folder.

### Session 2026-06-06

- Q: Does a recurring (auto-pay) card draft apply the convenience fee, and how is the fee stored? → A: Recurring drafts apply a similar fee model; card and ACH may have different (configurable) fees. The fee is shown in the status amount and recorded on each recurring transaction. The gross amount (without fee) and the fee amount are stored as two distinct fields on the transaction record.
- Q: What happens to the ledger when a payment is later refunded or disputed? → A: Write a new compensating (reversing) ledger entry that restores the owner's balance for both refunds and dispute/chargebacks that remove funds; the original ledger entry stays intact and the transaction status flips to Refunded/Disputed.
- Q: Is a failed recurring auto-pay draft retried? → A: No automatic retry. Record the failed transaction, alert the opted-in resident, and wait until the next scheduled draft day.
- Q: How are duplicate/double-submission payment initiations prevented? → A: Payment-initiation endpoints accept a client-generated idempotency key; duplicate keys return the original result instead of charging again, and the key is forwarded to the processor.
- Q: How are partial refunds handled? → A: Each refund event writes a compensating ledger entry for its actual refunded amount and accumulates the total refunded; transaction status becomes Refunded only when fully refunded, otherwise Partially-Refunded.
- Q: How should the card fee be modeled to stay compliant with card-network and state rules? → A: Generic configurable fee — `feeType` (flat | percentage), `cardScope` (all-cards | credit-only), and per-jurisdiction enable/disable. A percentage fee is a credit-card surcharge (credit-only; debit and prepaid are never percentage-surcharged); a flat fee is a convenience fee that may apply to all cards including debit. The combination percentage + debit/all-cards is rejected by validation. Card funding type comes from the processor (`card.funding`). Default ships safe (percentage, credit-only, debit exempt; disabled where surcharging is restricted).
- Q: How is a payment applied across multiple outstanding charges (partial / open-balance / custom amounts)? → A: Category-priority allocation — assessments (oldest→newest) → late fees → finance charges/interest → other; the order is configurable per HOA, defaulting to assessments-first to satisfy statutory order-of-application rules (e.g. CA Civ. Code §5655).
- Q: Is the card fee HOA revenue or pass-through, and is it returned on refund? → A: The fee is HOA fee-income booked to its own ledger/GL line. On refund the convenience fee is retained (mirroring that the processor keeps its processing fee), except a full refund driven by HOA/processing error returns the fee too.
- Q: When a settled ACH later returns (NSF/closed account), should the system assess a fee? → A: Yes — when the HOA has enabled it, auto-assess a configurable returned-payment/NSF fee as a new ledger charge, in addition to reversing the original payment and alerting. Per-HOA toggle + configurable amount.
- Q: How is an overpayment (payment exceeding the balance due) handled? → A: Accept it and carry the surplus as an on-account credit balance (balance may go negative) automatically applied to future charges; no auto-refund.
- Note: Compliance & recovery review (NC focus) incorporated — added security/abuse controls (FR-028–FR-031: rate limiting/fraud tooling, PII encryption + access audit, webhook replay tolerance, TCPA consent), reliability & recovery (FR-032–FR-036: durable webhook intake + dead-letter, reconciliation sweep for missed webhooks, transactional outbox, durable idempotency, backups/PITR + processor-based reconstruction with RPO/RTO), accounting reconciliation/reporting (FR-037–FR-039: settlement references, fund/GL code hook, per-owner & unpaid-assessment statements), and a North Carolina compliance section (Ch. 47F/47C, configurable late-fee/interest caps, surcharge legality verify-with-counsel, NC Debt Collection Act/TCPA dunning, unclaimed-property note).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pay an assessment one time, securely (Priority: P1)

A resident owes an HOA assessment and wants to pay it immediately with a card or bank
account, without typing raw card/bank numbers into a form the HOA stores. They pick an
amount via presets (Current balance / Next assessment / Balance + next / Other custom
amount), enter their details into the embedded Stripe Payment Element (Card / US bank /
Wallet tabs) rendered inside the page, review a masked summary with the amount, any card
convenience fee, and total, and confirm. The payment is charged, their ledger reflects it,
and they receive an on-screen confirmation.

Per the wireframes, the summary shows Amount + Processing fee + Total, the payment region
is visibly Stripe-owned ("Powered by Stripe", "we store only a token + •• last4"), card
payments add a convenience fee while ACH is free, and the page notes payments post within
1 business day.

**Why this priority**: One-time payment is the most common and highest-value resident
action. It is the minimum viable slice — a resident can settle a balance end-to-end —
and it is independently demonstrable without any of the recurring or alerting machinery.

**Independent Test**: Drive the one-time wizard with a test card/bank, confirm the
charge succeeds, a transaction audit record and a ledger entry are written, and the
review screen never displays or transmits raw card/bank numbers.

**Acceptance Scenarios**:

1. **Given** a resident with an outstanding balance, **When** they enter a valid card in
   the secure payment field and confirm, **Then** the card convenience fee is added to the
   total, the payment succeeds, a transaction record with status "Succeeded" and a ledger
   entry are created, and a masked confirmation (brand + last 4) is shown.
2. **Given** a resident entering bank-account (ACH) details, **When** they complete the
   hosted bank-collection flow and confirm, **Then** the payment is initiated, a
   transaction record with status "Pending" is written showing the bank name + last 4
   only, and the ledger entry is deferred until the success webhook confirms settlement.
3. **Given** a card that the processor declines, **When** the resident confirms, **Then**
   a transaction record with status "Failed" and the failure reason is written, and the
   resident sees a clear error without losing their place in the wizard.
4. **Given** any point in the flow, **When** the request reaches the backend, **Then** the
   payload contains a tokenized payment-method reference — never a raw card or bank number.

---

### User Story 2 - Set up automatic recurring payment (Priority: P2)

A resident wants their assessment drafted automatically each month. On the auto-pay page
they toggle auto-pay on, choose what gets paid (Just the assessment / Whatever I owe (open
balance) / A fixed amount I pick), choose a draft date, authorize a payment method through
the embedded Stripe SetupIntent flow, and accept the recurring ACH/charge mandate. Their
payment method is vaulted with the processor (not stored by the HOA) and shown as a masked
method-on-file row (e.g. "Fidelity •• 747 · ACH · no fee"). A status card shows Status /
Next draft (date · amount) / Method, and a drafts table lists past and scheduled drafts
with their status. On each scheduled draft day the system charges the vaulted method
automatically.

**Why this priority**: Recurring billing reduces missed payments and resident effort, but
it builds on the same secure-collection foundation as P1 and is only valuable once
one-time payment works.

**Independent Test**: Complete the auto-pay setup with a test method, verify a vaulted
customer + payment method reference is stored (no raw numbers), toggle auto-pay on/off,
and simulate a scheduled draft producing a recurring transaction record.

**Acceptance Scenarios**:

1. **Given** a resident on the auto-pay page, **When** they enter a payment method and
   save, **Then** a vaulted customer is created (or reused), the payment method is
   attached, and the page shows the masked saved method.
2. **Given** auto-pay is enabled with a draft day, **When** the scheduled draft runs on
   that day, **Then** the vaulted method is charged and a recurring transaction record is
   written.
3. **Given** auto-pay is enabled, **When** the resident toggles it off, **Then** no
   further scheduled drafts occur and the stored schedule reflects the disabled state.
4. **Given** a resident saves auto-pay, **When** the record is persisted, **Then** the
   system stores only references (vaulted customer/method ids) and never raw card/bank
   numbers or self-masked PANs.

---

### User Story 3 - Get alerted when an auto-pay charge fails (Priority: P3)

A resident on auto-pay wants to know promptly if a scheduled draft fails (e.g. expired
card) so they can fix it before incurring late fees. They opt in to SMS and/or email
alerts on the auto-pay page and provide a phone number/email. When a recurring charge
fails, the system notifies them on the channels they chose, with a link to update their
payment method.

**Why this priority**: Failure alerting materially improves collection rates but is a
secondary enhancement that only applies once recurring billing exists and only for
residents who explicitly opt in.

**Independent Test**: Opt a resident in to SMS and email, simulate a recurring payment
failure event, and confirm an alert is sent on each opted-in channel with the correct
masked content; confirm no alert is sent when the resident has not opted in.

**Acceptance Scenarios**:

1. **Given** a resident who has opted in to SMS alerts with a phone number on file,
   **When** a recurring charge fails, **Then** an SMS is sent with the failed amount,
   date, and an update link, and the send is recorded.
2. **Given** a resident who has opted in to email alerts, **When** a recurring charge
   fails, **Then** an email with the same information and a link to the auto-pay page is
   sent.
3. **Given** a resident who has NOT opted in, **When** a recurring charge fails, **Then**
   no alert of any kind is sent.
4. **Given** a one-time (non-recurring) payment fails, **When** the failure is processed,
   **Then** no auto-pay failure alert is triggered.
5. **Given** the alert toggles default to OFF, **When** a resident first views the page,
   **Then** both SMS and email alerts are shown as off until explicitly enabled.

---

### Edge Cases

- **Duplicate webhook delivery**: the processor may deliver the same lifecycle event more
  than once; status updates must be idempotent and not double-write or double-alert.
- **Webhook for unknown/unmatched payment**: an event whose payment reference matches no
  local transaction must be logged and acknowledged, not error.
- **Unhandled event types**: events the system does not handle must be logged at info
  level and acknowledged with success (no error response).
- **Invalid webhook signature**: a request that fails signature verification must be
  rejected and must not mutate any transaction.
- **Opt-in to SMS without a phone number on file**: enabling SMS must prompt for and
  require a phone number before the preference is considered active.
- **Vaulted-customer reuse**: a resident who already has a vaulted customer must not get a
  duplicate customer created on subsequent setups.
- **Decline vs. system error**: a clean card decline (insufficient funds) and a processor
  outage must produce distinguishable transaction outcomes and user messaging.
- **PII in telemetry/exceptions**: traces, metrics, and recorded exceptions must never
  contain card numbers, bank accounts, names, emails, or phone numbers.
- **Alert provider send failure**: if the SMS or email provider rejects or fails the send,
  the failure must be recorded (metric + errored span) without retry and without blocking
  or failing the webhook acknowledgement.
- **Double-submitted payment**: a double-clicked confirm or a retried initiation request
  (same idempotency key) must collapse to a single charge and return the original result,
  not create a second transaction.
- **Missed/dropped webhook**: if the system is down when an event fires, the reconciliation
  sweep (FR-033) must later resolve any transaction stuck non-terminal (e.g. ACH Pending past
  its settlement window); no payment may be silently lost.
- **Webhook handler crash mid-processing**: a verified event whose downstream processing
  fails must be retried from the durable store (FR-032), not lost and not double-applied.
- **Card-testing attack**: bursts of small failing card attempts must be throttled and
  deferred to processor fraud tooling, not allowed to enumerate cards (FR-028).
- **Stale credit balance**: an on-account credit (FR-007c) owed to a departed owner may, if
  long unclaimed, fall under NC unclaimed-property law; the system must make such balances
  reportable (escheat automation itself is out of scope).

## Requirements *(mandatory)*

### Functional Requirements

**Secure collection (frontend)**

- **FR-001**: The one-time payment wizard MUST collect card and bank-account details
  exclusively through the payment processor's hosted, PCI-compliant fields, replacing all
  raw card/ACH input fields.
- **FR-002**: The recurring (auto-pay) page MUST collect payment details exclusively
  through the same hosted fields, replacing all raw card/ACH input fields.
- **FR-003**: After tokenization, the review/summary UI MUST display only masked
  information (card brand + last 4, or bank name + last 4).
- **FR-004**: The frontend MUST transmit only a tokenized payment-method reference (or
  client-secret-confirmed intent) to the backend, and MUST NOT transmit raw card or bank
  numbers under any circumstance.

**One-time payment (backend)**

- **FR-004a**: The one-time payment page MUST offer amount presets — "Current balance"
  (as of today), "Next assessment" (with its due date), "Balance + next", and an "Other"
  custom amount — and display a summary card with Amount, Processing fee, and Total.
- **FR-004b**: The system MUST apply a configurable card-fee model with three knobs:
  `feeType` (flat | percentage), `cardScope` (all-cards | credit-only), and per-jurisdiction
  enable/disable. A **percentage** fee is a credit-card surcharge and MUST apply to credit
  cards only — debit and prepaid cards MUST NEVER be percentage-surcharged (the combination
  percentage + debit/all-cards MUST be rejected by configuration validation). A **flat** fee
  is a convenience fee and MAY apply to all card types including debit. The card funding type
  (credit/debit/prepaid) MUST be obtained from the processor (`card.funding`), not inferred.
  ACH/bank payments are free. The fee MUST be shown in the summary, included in the total
  charged via the processor, and recorded with the gross amount (before fee) and the fee
  amount as two distinct fields on the transaction (not a single combined total). The default
  configuration MUST ship safe: percentage, credit-only, debit exempt, and disabled in
  jurisdictions that restrict surcharging.
- **FR-004d**: The card fee MUST be treated as HOA fee-income and booked to its own
  ledger/GL line distinct from the principal payment. On a refund the fee MUST be retained
  (not returned), mirroring that the processor retains its processing fee — EXCEPT a full
  refund driven by an HOA or processing error, which MUST return the fee as well.
- **FR-004c**: The saved/vaulted method display MUST show only masked information — brand
  + last 4 (and expiry where applicable) for cards, bank name + last 4 for bank accounts,
  plus the per-method fee note (card = fee, ACH = no fee).
- **FR-005**: The system MUST accept a tokenized payment-method reference plus amount,
  create and confirm a payment with the processor, and return a confirmation on success.
  Card payments resolve synchronously (Succeeded at submission); ACH/bank payments settle
  asynchronously and MUST be recorded as Pending at submission, transitioning to Succeeded
  or Failed only when the corresponding lifecycle webhook arrives.
- **FR-006**: On a successful card one-time payment, the system MUST write a transaction
  audit record (Succeeded) and a ledger entry at submission. For ACH one-time payments,
  the transaction audit record is written as Pending at submission and the ledger entry
  MUST be written only when the success webhook confirms settlement.
- **FR-007**: On a failed one-time payment, the system MUST write a transaction audit
  record capturing the failure code and message, and return an error to the caller.
- **FR-007a**: Payment-initiation endpoints MUST accept a client-generated idempotency key.
  A repeated request with the same key MUST return the original result rather than creating a
  second charge, and the key MUST be forwarded to the processor so duplicate submissions
  (double-clicks, network retries, draft-vs-manual overlap) cannot produce a double charge.

**Accounting & ledger (backend)**

- **FR-007b**: When a payment settles for less than the total amount owed (partial, custom,
  or open-balance payments), the system MUST apply it across outstanding charges by category
  priority — assessments (oldest→newest) → late fees → finance charges/interest → other.
  The application order MUST be configurable per HOA and MUST default to assessments-first to
  satisfy statutory order-of-application rules (e.g. CA Civ. Code §5655).
- **FR-007c**: When a payment exceeds the total amount owed, the system MUST accept it and
  carry the surplus as an on-account credit balance (the running balance MAY go negative),
  automatically applied to future charges. No automatic refund is issued.
- **FR-007d**: The ledger MUST be append-only with deterministic ordering: each entry MUST
  carry a monotonic sequence and a UTC timestamp so the running balance is computed
  deterministically even when entries (deferred ACH settlements, refunds, reversals) are
  written out of chronological order or concurrently. Entries MUST NOT be mutated or deleted;
  corrections are made by new compensating entries.
- **FR-007e**: The ledger MUST support entry types for the new money movements — at minimum
  Refund, Reversal/Chargeback, Returned-Payment fee, on-account Credit, and Adjustment — in
  addition to the existing assessment/payment/late-fee/finance-charge types. Each payment,
  reversal, and fee ledger entry MUST reference the originating transaction so the ledger and
  the transaction audit trail can be reconciled.
- **FR-007f**: On a successful payment the system MUST issue a durable receipt — emailed to
  the resident and retrievable/downloadable in the portal — showing the masked method, gross
  amount, fee, total, date, and confirmation reference. For ACH the receipt MUST be issued
  when settlement is confirmed by webhook, not at submission.

**Recurring payment (backend)**

- **FR-008**: The system MUST vault the resident's payment method with the processor via a
  setup flow, creating a vaulted customer for the resident if one does not already exist
  and reusing it if it does.
- **FR-009**: The system MUST persist only references to the vaulted customer and vaulted
  payment method on the resident/recurring records, and MUST NOT store raw or self-masked
  card/bank numbers or related cardholder details.
- **FR-010**: The system MUST retain the existing draft-day scheduling behavior and, on
  the scheduled draft day, charge the vaulted method automatically, writing a recurring
  transaction record for the attempt.
- **FR-010a**: At setup the resident MUST choose what gets paid from three options — "Just
  the assessment" (the standard assessment charge), "Whatever I owe" (the current open
  balance, variable), or "A fixed amount I pick". On each draft day the system resolves the
  amount accordingly at draft time.
- **FR-010d**: Recurring auto-pay drafts MUST apply the same per-method convenience fee
  model as one-time payments (card adds the configurable card surcharge, ACH is free; card
  and ACH rates independently configurable). The fee MUST be reflected in the displayed
  "Next draft" status amount and the mandate text, and each recurring transaction MUST store
  the gross amount and the fee amount as two distinct fields.
- **FR-010b**: The auto-pay page MUST provide an on/off toggle, a status card showing
  Status, Next draft (date · amount), and the masked method on file, and a drafts table
  listing past and scheduled drafts (date, method, amount, status of Paid/Scheduled).
- **FR-010c**: Enabling auto-pay MUST require the resident to accept a recurring mandate
  (e.g. "I agree to the recurring ACH mandate — charged on the selected day each month
  until auto-pay is turned off"); the mandate text MUST reflect the chosen amount and draft
  day.
- **FR-011**: Residents MUST be able to enable and disable auto-pay, and a disabled
  schedule MUST produce no further drafts.
- **FR-011a**: A failed recurring draft MUST NOT be automatically retried. The system
  records the failed recurring transaction, alerts the opted-in resident (FR-015), and waits
  until the next scheduled draft day; no extra re-attempts occur within the same cycle.
- **FR-011b**: When auto-pay is enabled, the system MUST persist an immutable
  authorization (mandate) record capturing the exact mandate text shown and its version, the
  acceptance timestamp (UTC), the resident's IP address and user-agent, and the agreed
  amount/draft-day terms. The authorization record MUST be retained for at least two years
  after the schedule is terminated (NACHA WEB-debit requirement) and be reproducible on
  demand.
- **FR-011c**: For variable-amount auto-pay ("Whatever I owe", where the drafted amount can
  change between cycles), the system MUST send the resident advance notice of the upcoming
  draft amount before the draft date, on a configurable lead time, satisfying NACHA's
  notice-of-variable-amount requirement. Fixed-amount and standard-assessment schedules do
  not require this notice unless the amount changes.
- **FR-011d**: Each scheduled draft MUST use a deterministic idempotency key derived from
  the schedule and the billing period so that an overlapping or re-run draft job cannot draft
  the same resident twice for the same period.

**Transaction audit trail**

- **FR-012**: The system MUST maintain a transaction audit trail, separate from the
  existing ledger, recording for each payment attempt: property, owner, processor
  payment/charge references, gross amount (before fee) and fee amount as two distinct
  fields, cumulative refunded amount, currency, status (Pending, Succeeded, Failed,
  Partially-Refunded, Refunded, Disputed, DisputeLost, Returned), payment method type
  (Card, Ach) and card funding type (credit/debit/prepaid) when applicable, failure or
  return code/message when applicable, whether it was recurring, optional metadata, and
  created/updated timestamps.

**Webhooks**

- **FR-013**: The system MUST expose a webhook endpoint that verifies the authenticity
  (signature) of incoming processor events before acting on them, rejecting unverifiable
  requests without mutating data.
- **FR-014**: The system MUST update the matching transaction's status from lifecycle
  events: payment succeeded → Succeeded; payment failed → Failed (storing failure
  code/message); charge refunded → Refunded; dispute created → Disputed. When a previously
  Pending payment (e.g. ACH) transitions to Succeeded, the system MUST also write the
  deferred ledger entry for that payment.
- **FR-014a**: When a payment that has an existing ledger entry is refunded, or when a
  dispute/chargeback removes funds, the system MUST write a new compensating (reversing)
  ledger entry that restores the owner's balance. The original ledger entry MUST remain
  intact (ledger is append-only); only the transaction status changes to Refunded/Disputed.
- **FR-014b**: Refunds MAY be partial. Each refund event MUST write a compensating ledger
  entry for its actual refunded amount and MUST update the transaction's cumulative refunded
  amount. The transaction status MUST become Refunded only when the cumulative refunded
  amount equals the charged total; otherwise it MUST be Partially-Refunded. Multiple refund
  events on the same payment MUST accumulate correctly (and remain idempotent per event ID).
- **FR-014c**: A previously Succeeded ACH payment that later returns (NACHA return such as
  R01 insufficient funds or R02 closed account) MUST be handled even though it already
  settled: the transaction status MUST move to Returned (storing the return code/reason), a
  compensating reversing ledger entry MUST restore the owner's balance, and — when the
  payment was recurring AND the resident has opted in — a failure alert MUST be sent.
- **FR-014d**: Dispute lifecycle MUST be tracked to resolution, not only creation. On
  dispute closed-won the funds are restored: the system MUST reverse the earlier compensating
  entry (restoring the charge) and return the transaction to Succeeded. On dispute closed-lost
  the chargeback stands: the transaction MUST move to DisputeLost and the reversing entry
  remains.
- **FR-014e**: On an ACH return (FR-014c) and on a lost chargeback (FR-014d), when the HOA
  has enabled the returned-payment fee, the system MUST auto-assess a configurable
  returned-payment/NSF fee as a new ledger charge (Returned-Payment fee type) in addition to
  the reversal. The fee MUST be per-HOA enabled with a configurable amount and MUST NOT be
  applied when disabled.
- **FR-015**: On a recurring payment-failed event, the system MUST trigger a failure alert
  only when the payment was recurring AND the resident has opted in.
- **FR-016**: The webhook endpoint MUST log unhandled event types at info level without
  erroring, and MUST acknowledge receipt promptly with success.
- **FR-017**: Webhook processing MUST be idempotent so duplicate event deliveries do not
  double-update or double-alert. The system MUST persist each handled processor event ID
  and skip events whose ID has already been processed, and MUST additionally guard against
  re-applying changes to transactions already in a terminal status.

**Failure alerts & opt-in**

- **FR-018**: Residents MUST be able to opt in to SMS alerts, email alerts, neither, or
  both, with both defaulting to OFF.
- **FR-019**: Enabling SMS alerts MUST require a phone number; enabling email alerts MUST
  use the resident's email (editable).
- **FR-020**: The system MUST send a failure alert only on channels the resident has
  explicitly opted into, and MUST send nothing when the resident has not opted in.
- **FR-021**: The SMS alert MUST state the failed amount, the date, and a link/instruction
  to update the payment method, and include opt-out instructions.
- **FR-022**: The email alert MUST convey the same information with a link to the auto-pay
  page.
- **FR-022a**: A failed alert send (provider rejection/error) MUST be recorded via the
  alert.sent metric (success=false) and an errored span, MUST NOT be retried, and MUST NOT
  block or fail webhook acknowledgement or transaction status updates.
- **FR-023**: Residents MUST be able to retrieve and update their alert preferences (SMS
  opt-in, email opt-in, phone number) through the portal.

**Observability & privacy**

- **FR-024**: The system MUST emit traces and metrics for one-time payments, recurring
  charges, recurring setup, webhook receipt, and alert sends, so they are visible in the
  observability dashboard.
- **FR-025**: All telemetry (traces, metrics, recorded exceptions) MUST be free of PII —
  no card numbers, bank accounts, names, emails, or phone numbers. Payment amounts are
  permitted.
- **FR-026**: On payment failures, the corresponding trace MUST be marked as errored with
  a PII-scrubbed exception/record.

**Migration & data**

- **FR-027**: The resident record MUST gain a vaulted-customer reference, SMS/email alert
  opt-in flags (default OFF), and a phone number for alert delivery; the recurring record
  MUST gain a vaulted-payment-method reference and an authorization-record reference and MUST
  drop all deprecated raw/self-masked card and bank fields. New persistence MUST be added for
  the authorization (mandate) records, the new ledger entry types and ledger sequence/timestamp
  ordering, and the transaction↔ledger reference. Migrations MUST be reversible and MUST NOT
  destroy historical ledger rows.

**Security & compliance (backend)**

- **FR-028**: Payment-initiation and setup endpoints MUST be rate-limited per resident/source
  and MUST rely on the processor's fraud tooling (e.g. Stripe Radar) to mitigate card-testing
  / BIN-enumeration attacks; bursts of failed attempts MUST be throttled.
- **FR-029**: PII (phone numbers, emails, names) and authorization records MUST be encrypted
  at rest and access-controlled. Access to financial records and any change to fee/alert/
  schedule configuration MUST be audit-logged with actor, timestamp, and before/after values.
- **FR-030**: Webhook verification MUST enforce a signature timestamp tolerance to reject
  replayed or stale events, in addition to event-ID idempotency (FR-017).
- **FR-031**: SMS/email alert consent MUST be captured with proof sufficient for TCPA — the
  consent text/version, timestamp, and channel — analogous to the mandate record. An opt-out
  (e.g. STOP) MUST immediately and durably disable that channel and be honored thereafter.

**Reliability, recovery & reconciliation (backend)**

- **FR-032**: Webhook intake MUST be durable. A signature-verified event MUST be persisted
  (type + PII-scrubbed payload) before downstream processing, and a 2xx acknowledgement
  returned only after durable capture. If downstream processing fails it MUST be retried from
  the durable store and moved to a dead-letter on repeated failure, so no verified event is
  lost.
- **FR-033**: A periodic reconciliation sweep MUST poll the processor for charge/event status
  and reconcile any local transaction left non-terminal (e.g. ACH Pending) beyond a configured
  window, catching missed or dropped webhooks. The processor is the external system-of-record
  for charge outcomes.
- **FR-034**: Alerts and receipts MUST be dispatched via a transactional outbox so a crash
  during webhook processing neither loses nor duplicates them; with FR-017 and FR-022a this
  preserves exactly-the-intended-once delivery. The outbox MUST be dispatched promptly —
  immediately after the webhook is acknowledged, not only on the periodic reconciliation job —
  so failure alerts meet the SC-006 ≤5-minute target; the periodic job is the backstop for
  anything left pending.
- **FR-035**: Persisted idempotency keys/results (FR-007a, FR-011d) and processed-event
  records MUST survive process restarts so post-crash retries still deduplicate.
- **FR-036**: Financial data (ledger, transactions, authorizations) MUST be covered by
  database backups with point-in-time recovery; the design MUST document RPO/RTO targets and
  MUST be able to reconstruct the transaction trail from the processor's records plus stored
  references in a disaster.

**Accounting reconciliation & reporting (backend)**

- **FR-037**: Each transaction MUST capture the processor settlement references needed for
  financial reconciliation — balance-transaction id, the processor's own processing fee, and
  the payout id — so ledger entries (recorded gross at charge time) can be reconciled against
  the processor's batched, net payouts.
- **FR-038**: Ledger entries MUST carry an optional fund/GL category code (operating /
  reserve / fee-income / etc.) so fund accounting and future accounting-software export are
  possible without a schema change. (Full fund accounting and external export remain out of
  scope.)
- **FR-039**: The system MUST be able to produce a per-owner account statement (charges,
  payments, running balance, credits) and a statement of unpaid assessments, supporting owners'
  statutory record-inspection and payoff-statement rights (NC Chapter 47F, incl. § 47F-3-118).

### Key Entities *(include if feature involves data)*

- **Transaction (audit record)**: One per payment attempt. Represents the processor-side
  outcome of a charge — links to a property and an owner; carries processor payment and
  charge references, gross amount and fee amount as two distinct fields, currency, status
  (incl. Returned and DisputeLost), payment-method type and card funding type, failure/return
  details, recurring flag, cumulative refunded amount, settlement references (balance-
  transaction id, processor processing fee, payout id) for reconciliation, optional metadata,
  and timestamps. Distinct from and complementary to the existing ledger; referenced from the
  ledger entries it produces for reconciliation.
- **Recurring payment (auto-pay schedule)**: A resident's auto-pay configuration — draft
  day, amount type (Just the assessment / Whatever I owe / Fixed amount, with the fixed
  value when applicable), enabled/disabled state, a reference to the vaulted payment method,
  and a reference to the current authorization (mandate) record. No longer holds raw or
  self-masked payment details.
- **Payment authorization (mandate record)**: Immutable record of a resident's recurring
  authorization — exact mandate text and version, acceptance timestamp (UTC), IP address,
  user-agent, and the agreed amount/draft-day terms. Retained at least two years after the
  schedule terminates (NACHA) and reproducible on demand.
- **Owner (resident)**: Gains a vaulted-customer reference, alert opt-in flags for SMS and
  email (default OFF), and a phone number used for SMS delivery (email already present).
- **Ledger entry**: Existing accounting record; written on successful card one-time
  payments at submission and on ACH one-time payments when settlement is confirmed by
  webhook. Append-only with a monotonic sequence + UTC timestamp for deterministic running
  balance; never mutated or deleted. Refunds, dispute/chargebacks, and ACH returns add
  compensating reversing entries; the card fee is booked as its own fee-income line;
  returned-payment/NSF fees and on-account credits (negative balances from overpayment) are
  represented via dedicated entry types; each payment/reversal/fee entry references its
  originating transaction.
- **Webhook event inbox**: A durable record of each verified processor event (event ID,
  type, PII-scrubbed payload, processing status) used to (a) detect and skip duplicate
  deliveries, (b) retry processing that failed after acknowledgement, and (c) dead-letter
  events that exhaust retries (idempotency + recovery; supersedes the earlier
  "processed webhook event" idempotency log).

## Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: Transactions, recurring payments, and ledger entries are
  HOA/property-scoped via the owning property and owner; webhook-driven updates resolve the
  local transaction by processor reference and MUST stay within the resolved record's HOA
  scope. Cross-HOA access is denied by default.
- **Authorization**: A resident may only initiate payments, view transactions, manage
  auto-pay, and change alert preferences for properties/owners they are authorized for;
  all checks are server-side. The webhook endpoint is unauthenticated by session but
  authenticated by signature verification.
- **Ownership and moderation**: Transaction records are system-owned, immutable audit
  entries (status transitions only); residents own their auto-pay schedule and alert
  preferences.
- **API contract**: New/updated endpoints (one-time payment, recurring upsert/get/delete,
  alert-preferences get/update, webhook) follow the project's standard response and error
  shapes, UTC timestamps, and ID format; the webhook returns prompt 2xx acknowledgement.
- **API implementation and docs**: Endpoints implemented as FastEndpoints; Swagger/OpenAPI
  available in development only.
- **Database/runtime**: Schema changes delivered via strict EF Core migrations applied
  idempotently at startup; short-lived DbContext usage.
- **Security and abuse controls**: No raw PAN/bank data is ever stored or logged; webhook
  signature verification is mandatory; sensitive payment events are auditable via the
  transaction trail.
- **Payments compliance**: Card fees follow card-network rules — percentage surcharges are
  credit-only and disabled where state law restricts them; debit/prepaid are never
  percentage-surcharged (FR-004b). Recurring ACH follows NACHA WEB-debit rules — authorization
  capture and ≥2-year retention (FR-011b) and variable-amount advance notice (FR-011c).
  Payment application follows statutory order-of-application rules, configurable per HOA
  (FR-007b). PCI scope stays at SAQ A via processor-hosted fields (annual SAQ A attestation
  is an operational task).
- **North Carolina compliance**: The primary jurisdiction is North Carolina (Planned
  Community Act, Chapter 47F; condominiums Chapter 47C). Late-fee and interest charges MUST be
  configurable per association to stay within NC limits (commonly the greater of $20 or 10% of
  the overdue amount for late fees, and ≤18%/yr interest, unless the declaration provides
  otherwise) — values are not hard-coded. Owner record-inspection and statement-of-unpaid-
  assessment rights are supported via FR-039. NC credit-card-surcharge legality MUST be
  verified with counsel before enabling a percentage surcharge; the per-jurisdiction fee
  gating (FR-004b) supports a flat convenience-fee posture as the conservative default. SMS/
  email dunning MUST comply with the NC Debt Collection Act (Ch. 75) and TCPA (FR-031):
  factual content, honored opt-out, captured consent.
- **Observability**: Traces and metrics via OpenTelemetry/Aspire with environment/release
  tagging and strict PII exclusion (see FR-024–FR-026).
- **Accessibility**: Hosted payment fields and the new alert section meet keyboard access,
  labeling, and validation-message expectations (WCAG 2.1 AA) for the payment flows.
- **Quality gates**: Integration tests cover one-time and recurring flows (processor
  mocked), webhook signature verification (incl. replay/timestamp tolerance), webhook event
  processing including failure→alert, opt-in/opt-out logic, transaction creation on success
  and failure, payment allocation order and overpayment credit, refund/ACH-return/dispute
  reversal with deterministic balance recomputation, durable webhook intake + reconciliation
  sweep for missed events, and idempotent dedupe across restarts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of card and bank details are entered into hosted payment fields; zero
  raw card or bank numbers reach or are stored by the HOA backend (verified by request and
  storage inspection).
- **SC-002**: A resident can complete a one-time payment end-to-end in under 2 minutes.
- **SC-003**: Every payment attempt (success or failure, one-time or recurring) produces
  exactly one corresponding transaction audit record.
- **SC-004**: 100% of successfully verified lifecycle events result in the correct
  transaction status, and duplicate deliveries cause no duplicate updates or alerts.
- **SC-005**: Failure alerts are delivered only to opted-in residents on only their chosen
  channels — 0% of non-opted-in residents receive an alert.
- **SC-006**: A recurring-failure alert reaches the resident within 5 minutes of the
  failure event being received.
- **SC-007**: 0 occurrences of PII (card, bank, name, email, phone) in emitted traces,
  metrics, or recorded exceptions, sampled across all payment operations.
- **SC-008**: All deprecated raw/self-masked payment fields are removed from storage after
  migration, with 0 such columns remaining on the recurring-payment table.
- **SC-009**: Partial payments are applied in the configured statutory order in 100% of
  cases; the resulting ledger running balance recomputes deterministically regardless of
  entry insertion order (verified by replaying out-of-order settlements/refunds).
- **SC-010**: Every successful payment produces a durable, retrievable receipt; every
  recurring authorization produces an immutable, reproducible mandate record retained ≥2 years.
- **SC-011**: Refunds, ACH returns, and lost chargebacks each produce exactly one
  compensating ledger entry and the correct terminal transaction status, with no double
  reversal under duplicate webhook delivery.
- **SC-012**: 0 percentage-surcharge fees are applied to debit/prepaid cards, and 0 card
  fees are applied in jurisdictions configured as restricted (verified by configuration and
  transaction sampling).

## Assumptions

- Stripe is the payment processor and Twilio (SMS) + Twilio SendGrid (email) are the alert
  providers, per the tech-stack direction; secret keys and signing secrets are supplied via
  configuration and not committed.
- ACH/bank collection uses the processor's hosted bank-collection flow (manual entry or
  financial-connections); same-day vs. standard ACH settlement timing is the processor's
  default.
- Stripe-native subscriptions are NOT used; the HOA keeps its own draft-day scheduling and
  charges a vaulted customer + saved payment method.
- Refunds, disputes (creation and won/lost resolution), and ACH returns are handled inbound
  via webhook only; no resident- or admin-facing refund-initiation UI is in scope (refunds
  are issued out-of-band, e.g. via the processor dashboard, and reconciled by webhook).
- The existing scheduled-draft mechanism (or an equivalent job) exists or will be reused to
  trigger recurring charges on the draft day.
- One currency (USD) is assumed by default.
- The resident's email may already exist on the owner record; only phone number and opt-in
  flags are guaranteed-new fields.
- UI is derived from the Claude Design "NekoHOA Wireframes" handoff bundle (preserved under
  `design/` in this feature folder). The selected directions are one-time **Embedded
  Payment Element** and recurring **Embedded SetupIntent + vaulted method-on-file list**;
  the hosted-checkout redirect and Stripe Customer Portal variants are not built. Visual
  fidelity (pastel low-fi wireframe vocabulary: `wf-card`, `wf-toggle`, `wf-field-label`,
  `wf-pill`, etc.) should be recreated in Angular, not copied verbatim from the prototype.
- The **payment-alerts opt-in** UI (Twilio SMS/email, User Story 3) is NOT present in the
  wireframes — it originates from the written requirements. It will be added as a new
  "Payment alerts" section styled consistently with the existing recurring page (card,
  toggle, field-label patterns).
- The card-fee rate/type, the returned-payment (NSF) fee, the payment-application order, and
  the assessment/preset amounts shown in the wireframes ($35 assessment, ≈3% / $1.95 examples)
  are illustrative; actual values are configuration / data-driven per HOA, not hard-coded.

## Out of Scope

- Ops/maintenance alerting (PagerDuty, Slack, etc.) for production errors.
- WhatsApp alerts.
- Stripe-native subscriptions.
- Resident/admin refund-initiation UI (webhook handling only).
- Stripe Connect / multi-merchant.
- Full fund accounting (operating/reserve segregation) and accounting-software export/sync
  (only data-model hooks are provided — FR-038).
- Admin/back-office reconciliation & reporting dashboards and offline (check/cash) payment
  entry UI (the ledger model accommodates them; the surfaces are not built here).
- Automated unclaimed-property/escheat processing for stale credit balances (balances are
  made reportable; remittance workflow is out of scope).
