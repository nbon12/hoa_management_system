---
description: "Task list for Stage 2 Integration CI — Provider Sandbox Verification"
---

# Tasks: Stage 2 Integration CI — Provider Sandbox Verification

**Input**: Design documents from `/specs/007-integration-ci-tests/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: This feature **is** a test tier — the "implementation" of each story is its sandbox test plus (for US2/US3) the small adapter seam it requires. Tests are written first and MUST fail before the seam edit (US2/US3). US1 needs no production change.

**Organization**: Tasks are grouped by user story (US1 Stripe P1, US2 SendGrid P2, US3 Twilio P3) so each provider can be implemented, run, and validated independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- Paths are absolute-from-repo-root; production edits live in `HOAManagementCompany/`, tests in `HOAManagementCompany.Tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Keep the PR/unit pipeline hermetic before any sandbox tests exist.

- [X] T001 [P] Narrow the existing `test` job in `.github/workflows/test.yml` to `dotnet test ... --filter "Category!=Sandbox"` so PR and non-`main` runs never execute sandbox tests or require provider secrets (FR-001, FR-002).
- [X] T002 Create the `HOAManagementCompany.Tests/Integration/Sandbox/` directory and a short `Integration/Sandbox/README.md` documenting the `[Trait("Category","Sandbox")]` gate and that every sandbox call is wrapped by `SandboxResult` and guarded by a `RequireX()` precondition.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared test harness every story depends on.

**⚠️ CRITICAL**: No user story can run until this phase is complete.

- [X] T003 [P] Implement the retry + outage/regression classifier `SandboxResult` in `HOAManagementCompany.Tests/Fixtures/SandboxResult.cs` — bounded retry (3 attempts, exponential backoff); transport/availability errors (timeout, `HttpRequestException`, `SocketException`, `StripeException` status 0/≥500, Twilio 5xx) → throw `Xunit.SkipException`; domain/assertion errors → rethrow (Fail). Per `contracts/sandbox-test-harness.md` (FR-005, SC-005).
- [X] T004 Implement `SandboxIntegrationTestBase : IntegrationTestBase` in `HOAManagementCompany.Tests/Fixtures/SandboxIntegrationTestBase.cs` — does NOT override `IStripeGateway`/`IAlertProvider` (keeps real adapters); overrides `ExtraConfiguration()` to inject real test-mode secrets from environment variables; exposes `RequireStripe()`/`RequireSendGrid()`/`RequireTwilio()` guardrails that assert test-scoped credentials and `Assert.Skip` with a clear message when a secret is missing; never logs raw secret values. Per `contracts/sandbox-test-harness.md` (FR-002, FR-007, FR-009, FR-010). Depends on T003.

**Checkpoint**: Real adapters are reachable from a guarded, outage-aware test base.

---

## Phase 3: User Story 1 — Stripe (Priority: P1) 🎯 MVP

**Goal**: Exercise the real Stripe adapter (payment/setup/charge surface) and the webhook signature→persistence path against Stripe test mode, with no real charges.

**Independent Test**: Export only the Stripe test secrets and run `dotnet test --filter Category=Sandbox` — the Stripe tests pass end-to-end (test customer, PaymentIntent, vaulted SetupIntent, off-session charge, signed webhook persisted) while SendGrid/Twilio tests skip. No production code changes.

### Tests + harness for User Story 1

- [X] T005 [P] [US1] Add a captured real Stripe event JSON fixture at `HOAManagementCompany.Tests/Integration/Sandbox/Fixtures/stripe-payment-intent-succeeded.json` (matches the provider's actual event schema, per research R2).
- [X] T006 [US1] Implement `SignedWebhookFixture` in `HOAManagementCompany.Tests/Fixtures/SignedWebhookFixture.cs` — computes the `Stripe-Signature` header `t=<unix>,v1=HMAC-SHA256("<t>.<payload>", whsec)` with `HMACSHA256`, plus a `Tamper` option that corrupts `v1` for the negative case (FR-012). Depends on T004.
- [X] T007 [US1] Write `StripeSandboxTests` in `HOAManagementCompany.Tests/Integration/Sandbox/StripeSandboxTests.cs` (`[Trait("Category","Sandbox")]`, derives `SandboxIntegrationTestBase`, `RequireStripe()`, every call via `SandboxResult`):
  - Adapter surface (FR-011): `EnsureCustomerAsync` → `CreatePaymentIntentAsync` → `GetPaymentIntentAsync`; `CreateSetupIntentAsync` → confirm server-side with test PM `pm_card_visa` → `GetSetupIntentResultAsync` → `ChargeOffSessionAsync` → `GetChargeAsync`. Per `contracts/stripe-sandbox-surface.md` and research R5.
  - Webhook valid (FR-012/FR-013): POST the signed fixture to `/payments/webhooks/stripe` → assert `200` and a `WebhookEventInbox` row with matching `StripeEventId`/`EventType`.
  - Webhook tampered (FR-012): POST with `Tamper=true` → assert `400` and **no** `WebhookEventInbox` row.
  Depends on T006.

**Checkpoint**: MVP — the highest-risk integration (payments + webhook signature) is verified against test mode and gateable.

---

## Phase 4: User Story 2 — SendGrid (Priority: P2)

**Goal**: Exercise the real SendGrid adapter in sandbox mode (no delivery), verifying auth/sender/payload and the failure path.

**Independent Test**: Export SendGrid test secrets with `SendGrid:Sandbox=true` and run the sandbox filter — the SendGrid accepted-response test passes and no email is delivered; the invalid-sender test reports a handled failure. Stripe/Twilio tests skip.

### Tests for User Story 2 (write first — fails until the seam exists)

- [X] T008 [US2] Write `SendGridSandboxTests` in `HOAManagementCompany.Tests/Integration/Sandbox/SendGridSandboxTests.cs` (`[Trait("Category","Sandbox")]`, `RequireSendGrid()` which asserts `Sandbox==true`, via `SandboxResult`):
  - FR-015: send a representative alert/receipt → assert `AlertSendResult.Success==true` (provider 2xx) with sandbox mode on (no delivery).
  - FR-016: configure a blank/unverified `FromEmail` → assert `AlertSendResult.Success==false`.
  This FAILS against the current adapter (no sandbox seam). Per `contracts/sendgrid-sandbox.md`.

### Implementation for User Story 2

- [X] T009 [US2] Add `bool Sandbox` (default `false`) to `SendGridOptions` in `HOAManagementCompany/Features/Payments/PaymentOptions.cs`.
- [X] T010 [US2] In `HOAManagementCompany/Infrastructure/Payments/Alerts/SendGridEmailProvider.cs`, set `msg.MailSettings = new MailSettings { SandboxMode = new SandboxMode { Enable = true } }` when `_options.Sandbox` is true (production path unchanged); wrap the new branch in a `REPOWISE:START domain=payments-alerts` marker. Depends on T009.

**Checkpoint**: T008 now passes; email integration verified with zero delivery.

---

## Phase 5: User Story 3 — Twilio (Priority: P3)

**Goal**: Exercise the real Twilio adapter against test credentials and magic numbers, verifying number formatting, success, and error mapping — no real SMS.

**Independent Test**: Export Twilio test Account SID + Auth Token, `From=+15005550006`, `TWILIO_TEST_CREDENTIALS=true`, run the sandbox filter — the success magic number returns a `Sid`, the failure magic number reports a handled error, no SMS sent. Stripe/SendGrid tests skip.

### Tests for User Story 3 (write first — fails until the basic-auth path exists)

- [X] T011 [US3] Write `TwilioSandboxTests` as an xUnit `[Theory]` in `HOAManagementCompany.Tests/Integration/Sandbox/TwilioSandboxTests.cs` (`[Trait("Category","Sandbox")]`, `RequireTwilio()`, via `SandboxResult`) with `[InlineData("+15005550006", true)]` and `[InlineData("+15005550001", false)]` (FR-017/FR-018): success → `AlertSendResult.Success==true` with a `Sid`; failure → `Success==false`. FAILS against the current API-key-only adapter (magic numbers not honored). Per `contracts/twilio-test-credentials.md`.

### Implementation for User Story 3

- [X] T012 [US3] Add `string AuthToken` (default `""`) to `TwilioOptions` in `HOAManagementCompany/Features/Payments/PaymentOptions.cs` (same file as T009 — sequence after it; not parallel).
- [X] T013 [US3] In `HOAManagementCompany/Infrastructure/Payments/Alerts/TwilioSmsProvider.cs`, add a basic-auth branch: when `ApiKeySid` is empty and `AuthToken` is set, call `TwilioClient.Init(AccountSid, AuthToken)`; otherwise keep the existing API-key `Init` (production unchanged); wrap in a `REPOWISE:START domain=payments-alerts` marker. Depends on T012.

**Checkpoint**: T011 now passes; all three providers verified independently.

---

## Phase 6: Cross-Cutting — CI Gate, Secrets, Docs, Validation

**Purpose**: Wire the `main`-only gate and finalize delivery. Depends on all stories being green locally.

- [X] T014 Add the `integration-sandbox` job to `.github/workflows/test.yml`: `runs-on: ubuntu-latest`, `needs: test`, `if: github.ref == 'refs/heads/main' && github.event_name == 'push'`, runs `dotnet test --filter Category=Sandbox --configuration Release` with provider secrets mapped into `env` (`Stripe__*`, `SendGrid__*` + `SendGrid__Sandbox=true`, `Twilio__*` + `Twilio__FromNumber=+15005550006` + `TWILIO_TEST_CREDENTIALS=true`). Per `contracts/ci-integration-sandbox-job.md` (FR-001, FR-006, FR-010).
- [X] T015 Change `docker-push` in `.github/workflows/test.yml` from `needs: test` to `needs: [test, integration-sandbox]` so a sandbox **failure** blocks the image while a **skip** (outage/unconfigured) does not (FR-003, SC-005).
- [ ] T016 [P] Provision the six test-mode secrets in the GitHub repository settings: `STRIPE_SECRET_KEY_TEST`, `STRIPE_WEBHOOK_SIGNING_SECRET_TEST`, `SENDGRID_API_KEY`, `SENDGRID_FROM_EMAIL` (a verified sender), `TWILIO_TEST_ACCOUNT_SID`, `TWILIO_TEST_AUTH_TOKEN` — distinct from production secrets (FR-006).
- [X] T017 [P] Update Repowise marker regions for the two edited adapter files and regenerate/confirm Repowise outputs so indexed docs match merged code (constitution CI/CD & docs gate).
- [X] T018 [P] Add an SC-001 adapter-method checklist to `specs/007-integration-ci-tests/` enumerating each exercised `IStripeGateway`/`IAlertProvider` operation, since the adapters remain `[ExcludeFromCodeCoverage]` and the coverage report will not reflect this behavioral coverage.
- [ ] T019 Run `quickstart.md` validation locally: with test secrets, `dotnet test --filter Category=Sandbox` passes (and unconfigured providers skip cleanly); `dotnet test --filter "Category!=Sandbox"` excludes them and the existing suite stays green (SC-003 — confirm no real email/SMS/charge produced).
- [ ] T020 Confirm PR gates: Sonar passes, Codecov/diff-cover is satisfied (adapter edits are `[ExcludeFromCodeCoverage]`; new options are auto-properties), and the narrowed `test` job is green on the feature PR.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: T004 depends on T003. BLOCKS all stories.
- **US1 (Phase 3)**: after Foundational. No production code; no dependency on US2/US3.
- **US2 (Phase 4)** and **US3 (Phase 5)**: after Foundational. Independent of US1 and of each other in behavior — but T009 and T012 edit the **same file** (`PaymentOptions.cs`), so sequence them.
- **Cross-cutting (Phase 6)**: after the stories you intend to ship are green.

### Within Each Story

- US1: T005 → T006 → T007.
- US2: T008 (failing test) → T009 → T010.
- US3: T011 (failing test) → T012 → T013.

### Parallel Opportunities

- T001 and T002 (Setup) — different concerns; T001 is `[P]`.
- T003 `[P]` (classifier) alongside T001/T002.
- T005 `[P]` (fixture JSON) early.
- T016, T017, T018 `[P]` in Phase 6 (different artifacts).
- US2 and US3 can be developed by different people **except** the shared `PaymentOptions.cs` edit (T009/T012) must be serialized.

---

## Parallel Example: Foundational + US1 kickoff

```bash
# After Setup, these touch different files and can run together:
Task: "T003 Implement SandboxResult classifier in Fixtures/SandboxResult.cs"
Task: "T005 Add captured Stripe event JSON in Integration/Sandbox/Fixtures/stripe-payment-intent-succeeded.json"
# Then T004 (base) → T006 (signer) → T007 (Stripe tests).
```

---

## Implementation Strategy

### MVP First (US1 — Stripe only)

1. Phase 1 Setup → Phase 2 Foundational.
2. Phase 3 US1 (Stripe). **STOP and validate**: run the sandbox filter with only Stripe secrets; payments + webhook path verified, no real charge.
3. Optionally wire Phase 6 CI for Stripe-only gating, or wait for US2/US3.

### Incremental Delivery

1. Foundation ready → US1 (MVP, highest risk) → demo.
2. Add US2 (SendGrid) → verify no-delivery → demo.
3. Add US3 (Twilio) → verify magic numbers → demo.
4. Wire Phase 6 once the providers you want gated are green; the deploy gate (T015) activates the protection.

---

## Notes

- Production edits are strictly additive and default-off; production email/SMS behavior is unchanged when `Sandbox`/`AuthToken` are unset.
- Safety invariants (do not regress): SendGrid never sends unless `Sandbox==true`; Twilio `From` must be magic under test creds; Stripe harness refuses non-`sk_test_`/`rk_test_` keys; assertions target only self-created sandbox objects (shared accounts, Clarifications Q4).
- A red sandbox test = real regression → blocks `docker-push`. A skip = provider unavailable/unconfigured → does not block (fix forward, Clarifications Q2).
