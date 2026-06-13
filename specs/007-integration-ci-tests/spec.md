# Feature Specification: Stage 2 Integration CI — Provider Sandbox Verification

**Feature Branch**: `007-integration-ci-tests`
**Created**: 2026-06-11
**Status**: Draft
**Input**: User description: "Stage 2 — Integration CI (runs on merge to main). Use each service's test/sandbox mode. Real HTTP calls, real responses, no real charges or messages. Stripe test-mode key + webhook trigger, SendGrid sandbox mode, Twilio test credentials with magic numbers. Testcontainers for the database. Secrets stored in CI."

## Clarifications

### Session 2026-06-11

- Q: How should the Stripe webhook signature-verification path (FR-012) be exercised in Stage 2? → A: Signed fixture (in-process) — re-sign a canned/captured event payload with the real test signing secret in the test process; no Stripe CLI sidecar or network dependency. Signature verification is pure local cryptography, so a fixture signed with the real test secret is equivalent to a Stripe-delivered event for verification purposes.
- Q: When Stage 2 fails after code is already merged to main, what should happen? → A: Block deploy + alert (fix forward) — halt the release-artifact build/deploy and notify the team; the offending commit stays on main and is fixed forward (follow-up commit or manual revert). No automatic revert, so a transient provider outage (FR-005) can never revert good code.
- Q: Which events should trigger the Stage 2 integration run? → A: main only — run on push/merge to the mainline branch, immediately before the deploy artifact (image build) is produced. Not run on develop or feature branches, and no scheduled/nightly run (consistent with the no-CLI-sidecar decision).
- Q: What provider accounts should Stage 2's sandbox calls use? → A: Shared test accounts — reuse the project's existing Stripe test-mode / SendGrid / Twilio test credentials, relying on per-run unique objects for isolation. No dedicated CI-only accounts. Tests MUST assert only on objects they themselves created so shared sandbox state never causes flakiness.

## User Scenarios & Testing *(mandatory)*

<!--
  Context: The PR/unit pipeline (Stage 1) already runs fast, mocked tests — every external
  provider is replaced by an in-memory fake (FakeStripeGateway, FakeAlertProvider), and the
  real adapters are excluded from coverage and never called. The real adapter code (SDK wiring,
  webhook signature verification, sandbox-mode flags, magic-number handling, phone/email payload
  shaping) is therefore unverified until production. Stage 2 closes that gap by running the REAL
  adapters against each provider's test/sandbox mode after code merges to the mainline.
-->

### User Story 1 - Catch broken Stripe integration before it ships (Priority: P1)

As an engineer merging payment-related changes to the mainline, I need the real Stripe adapter exercised against Stripe's test mode — including the webhook signature-verification path — so that a broken payment flow is caught automatically before the release image is built, instead of surfacing as a failed resident payment in production.

**Why this priority**: Payments are the highest-risk, highest-complexity integration. Webhook signature verification, payment-intent creation, vaulting, and off-session charges all fail in ways the in-memory fake cannot reproduce (wrong API version, malformed request, signature tolerance, SDK upgrade regressions). This is the single most valuable check and is a viable standalone MVP on its own.

**Independent Test**: Merge a branch that intentionally breaks the Stripe adapter (e.g., a malformed request or a webhook-verification change) to the mainline; confirm the Stage 2 run fails and blocks the downstream deploy, while a clean branch passes — with zero real charges created.

**Acceptance Scenarios**:

1. **Given** a commit lands on the mainline with a working Stripe adapter, **When** Stage 2 runs against the Stripe test-mode key, **Then** a real PaymentIntent is created and retrieved, a SetupIntent vaulting flow completes, and the run passes — with no real money moved.
2. **Given** a webhook test event is generated with the real test signing secret, **When** the adapter verifies the signature and constructs the event, **Then** the event is accepted and the resulting ledger/transaction record is persisted to a real database instance.
3. **Given** a tampered or unsigned webhook payload, **When** the adapter verifies it, **Then** verification is rejected and the scenario is reported as the expected negative result (not a false pass).
4. **Given** a regression that breaks the Stripe adapter, **When** Stage 2 runs, **Then** the run fails and the downstream release/image-build step does not proceed.

---

### User Story 2 - Verify email delivery integration without sending email (Priority: P2)

As an engineer changing payment-alert or receipt email behavior, I need the real email adapter exercised against the provider in sandbox mode so that authentication, sender verification, and payload shape are validated on every mainline merge — without delivering mail to any real inbox.

**Why this priority**: Email is a real but lower-blast-radius integration than payments. Sandbox mode validates the request end-to-end (auth, verified sender, template/payload) and returns a real accepted response while suppressing delivery, so regressions in alert/receipt wiring are caught cheaply.

**Independent Test**: Run the email adapter in sandbox mode against the provider with valid test credentials; confirm an accepted response is returned and no message is delivered. Break the sender configuration and confirm the run fails.

**Acceptance Scenarios**:

1. **Given** valid email test credentials and sandbox mode enabled, **When** the adapter sends a payment-alert/receipt message, **Then** the provider returns an accepted response and no email is delivered to a real recipient.
2. **Given** an invalid or unverified sender configuration, **When** the adapter attempts to send, **Then** the provider rejects it and Stage 2 reports a failure.

---

### User Story 3 - Verify SMS alert integration without sending SMS (Priority: P3)

As an engineer changing payment-alert SMS behavior, I need the real SMS adapter exercised against the provider's test credentials and magic numbers so that number formatting, success handling, and error mapping are validated on every mainline merge — without sending a real text or incurring carrier cost.

**Why this priority**: SMS alerts are an opt-in convenience, the lowest blast radius of the three. Test credentials with magic numbers let us assert both the success path and a representative failure path deterministically, validating the adapter's request shaping and error mapping.

**Independent Test**: Run the SMS adapter against test credentials using the success magic number and the failure magic number; confirm the success number returns a queued/sent result and the failure number surfaces the expected provider error — with no real SMS sent.

**Acceptance Scenarios**:

1. **Given** test credentials and the success magic recipient number, **When** the adapter sends an alert, **Then** the provider returns a queued/sent result and no real SMS is delivered.
2. **Given** test credentials and a failure magic recipient number (e.g., an invalid number), **When** the adapter sends an alert, **Then** the provider returns the expected error and the adapter surfaces it as a handled failure rather than crashing.

---

### Edge Cases

- **Provider sandbox unreachable / transient 5xx**: A sandbox outage or network blip must be distinguishable from a real integration regression. Transient failures are retried a bounded number of times; if still failing, the run reports an infrastructure problem (provider unavailable) rather than silently passing or being indistinguishable from a code defect.
- **Live credentials misconfigured into Stage 2**: The pipeline must refuse to run provider calls with non-test/live credentials, so a misconfigured secret can never cause a real charge, real email, or real SMS.
- **Missing secret**: If a required provider secret is absent, the relevant provider check fails loudly (clear "credential not configured" message) rather than skipping silently and giving a false green.
- **Secret leakage in logs**: Provider responses, request dumps, and error output must never echo secret values into CI logs.
- **Partial provider failure**: One provider failing (e.g., SMS) must not mask the pass/fail status of the others; each provider's result is independently reported.
- **Concurrent mainline merges / shared sandbox accounts**: Stage 2 reuses the project's shared provider test accounts, so parallel runs (and manual developer testing) must not interfere. Each run relies only on isolated, self-created test objects and an isolated database instance; assertions MUST target only objects the run itself created and MUST NOT depend on global mutable sandbox state (e.g., listing/counting all objects in the account).

## Requirements *(mandatory)*

### Functional Requirements

**Pipeline orchestration**

- **FR-001**: The system MUST provide an integration verification stage that runs only on push/merge to the mainline branch (not on `develop`, feature branches, or a schedule), positioned immediately before the deploy artifact is produced, and separate and distinct from the fast mocked PR check.
- **FR-002**: The integration stage MUST execute the *real* provider adapters (not the in-memory fakes) so that adapter code excluded from the mocked suite is exercised at least once per mainline merge.
- **FR-003**: A failure of the integration stage MUST block the downstream release artifact build/deploy so a broken integration cannot ship, AND MUST notify the team. The offending commit remains on the mainline and is fixed forward (follow-up commit or manual revert); the pipeline MUST NOT automatically revert commits, so a transient provider outage (FR-005) can never revert good code.
- **FR-004**: Each provider's verification result MUST be reported independently, so a failure in one provider is attributable and does not mask the status of the others.
- **FR-005**: The integration stage MUST distinguish a real integration regression (fail the run) from a transient provider/sandbox outage (bounded retry, then report as an infrastructure/availability problem), so flaky external availability does not erode trust in the gate.
- **FR-006**: Required provider credentials MUST be sourced from CI-managed secrets, never committed to the repository, and masked in all CI output.
- **FR-007**: The stage MUST fail with a clear, actionable message when a required provider credential is missing, rather than skipping silently.

**Safety guardrails**

- **FR-008**: The system MUST guarantee that no real money is moved, no real email is delivered, and no real SMS is sent during the run.
- **FR-009**: The system MUST refuse to execute provider calls unless the supplied credentials are test/sandbox credentials (e.g., test-mode key prefix / test account identifier), preventing accidental use of live credentials.
- **FR-010**: Secret values MUST NOT appear in provider request/response dumps, assertion failures, or any logged output.

**Stripe verification (P1)**

- **FR-011**: The system MUST verify, against Stripe test mode, the payment flows the mocked suite bypasses: creating and retrieving a PaymentIntent, creating a SetupIntent and resolving the vaulted method, charging a vaulted method off-session, and retrieving charge/settlement detail.
- **FR-012**: The system MUST verify webhook signature construction using a payload signed in-process with the real test signing secret (a captured/canned event re-signed for the run — no Stripe CLI sidecar or network dependency), and MUST exercise the negative case where an invalid/tampered signature is rejected. The fixture payload SHOULD be a captured real event so it reflects the provider's actual event schema.
- **FR-013**: The system MUST persist the result of a verified webhook through to a real database instance, so the webhook→persistence path (not just the SDK call) is covered.
- **FR-014**: The database used for Stripe persistence checks MUST be a real, disposable instance provisioned for the run (not an in-memory or production database).

**Email verification (P2)**

- **FR-015**: The system MUST send a representative payment-alert/receipt email through the real email adapter with the provider's sandbox/no-deliver mode enabled, and assert the provider returns an accepted response.
- **FR-016**: The system MUST verify the failure path where sender/authentication configuration is invalid surfaces as a handled error.

**SMS verification (P3)**

- **FR-017**: The system MUST send a representative alert through the real SMS adapter using the provider's test credentials and the success magic recipient number, and assert a queued/sent result with no real delivery.
- **FR-018**: The system MUST exercise a failure magic recipient number and assert the adapter surfaces the provider's error as a handled failure (validating number formatting and error mapping).

### Key Entities *(include if feature involves data)*

- **Provider Test Credential Set**: The test/sandbox authentication material for one provider (test API key, test signing secret, test account/from identifiers). Lives only in CI secret storage; scoped to non-production. Each provider has exactly one set used by Stage 2.
- **Sandbox Verification Result**: The pass/fail/unavailable outcome of one provider's checks for a single run, with enough detail to attribute a failure to a code regression vs. a provider outage vs. a missing credential — without exposing secret values.
- **Webhook Test Event**: A captured/canned event payload re-signed in-process with the real test signing secret, used to exercise signature verification and the downstream persistence path; not derived from any real resident activity and not fetched over the network.

### Constitution Requirements *(mandatory when applicable)*

- **Security and abuse controls**: All provider credentials are CI secrets, test/sandbox-scoped, masked in logs, and validated as non-live before any call (FR-006, FR-009, FR-010). No untrusted external input is processed beyond the providers' own sandbox responses, which are treated as data.
- **Database/runtime**: Stripe persistence checks run against a disposable real database instance provisioned per run (Testcontainers-style), consistent with the existing integration-test database strategy; no production database is touched (FR-014).
- **Observability**: Each provider result is independently reported with a clear regression-vs-outage-vs-missing-credential distinction (FR-004, FR-005, FR-007); no sensitive values are emitted (FR-010).
- **Quality gates**: This stage covers the real provider adapters that are intentionally excluded from the mocked unit suite's coverage, exercising them against sandbox at least once per mainline merge (FR-002). It runs in addition to — not as a replacement for — the existing fast mocked tests, which continue to gate PRs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the real provider adapter operations that the mocked PR suite bypasses are exercised against the providers' sandbox/test mode at least once per mainline merge.
- **SC-002**: An integration regression (broken webhook verification, malformed payment request, broken email sender config, or malformed SMS recipient) is caught by Stage 2 and blocks the release artifact, rather than reaching production — demonstrated by an intentionally-broken branch failing the stage.
- **SC-003**: Zero real charges, zero delivered emails, and zero sent SMS result from any Stage 2 run.
- **SC-004**: No secret value ever appears in CI logs across runs.
- **SC-005**: A transient provider/sandbox outage is reported as an availability problem (not a code regression) and does not produce a false green.
- **SC-006**: The integration stage completes within 5 minutes on a typical mainline merge, keeping the path to deploy fast.

## Assumptions

- **Runs post-merge on the mainline, gating deploy**: "Runs on merge to main" is interpreted as triggering on push to the mainline branch (after PR merge), positioned before the release-image build/deploy so a failure blocks shipping. PRs continue to be gated only by the existing fast mocked suite to keep PR feedback quick and free of external dependencies.
- **Scope is the existing adapter surface**: Verification covers the operations the current real adapters implement and that the fakes bypass (one-time PaymentIntent, SetupIntent vaulting, off-session charge, charge/settlement lookup, webhook signature verification + persistence; sandbox email send; test-credential SMS send with success/failure magic numbers). New provider capabilities are out of scope until they exist in the adapters.
- **Provider sandbox features are available and free**: Stripe test mode + webhook trigger tooling, the email provider's sandbox/no-deliver mode, and the SMS provider's test credentials with magic numbers are assumed available on the project's existing provider accounts at no per-call cost.
- **Reuses the existing disposable-database test strategy**: The real database instance for persistence checks uses the same disposable-container approach already used by the integration test suite.
- **Secrets are provisionable in CI**: The team can store test-mode secrets in the CI platform's secret store (Stripe test secret + webhook signing secret, email API key, SMS test account/auth + from-number) and these are distinct from any production secrets.
- **Bounded retry policy for outages**: A small, fixed retry count with backoff is acceptable for absorbing transient sandbox unavailability before declaring an availability problem.
