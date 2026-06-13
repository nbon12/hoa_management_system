# SC-001 Adapter-Method Coverage Checklist (Stage 2)

**Why this exists**: the live adapters (`StripeGateway`, `SendGridEmailProvider`, `TwilioSmsProvider`)
are `[ExcludeFromCodeCoverage]` thin network wrappers, so the coverage report will **not** reflect
that Stage 2 exercises them. SC-001 ("100% of adapter operations exercised against test mode") is
therefore tracked behaviorally here: each operation below maps to the sandbox test that drives it.

## `IStripeGateway` (US1 — `StripeSandboxTests`)

- [x] `EnsureCustomerAsync` → `Adapter_surface_round_trips_against_test_mode`
- [x] `CreatePaymentIntentAsync` → `Adapter_surface_round_trips_against_test_mode`
- [x] `GetPaymentIntentAsync` → `Adapter_surface_round_trips_against_test_mode`
- [x] `CreateSetupIntentAsync` → `Adapter_surface_round_trips_against_test_mode`
- [x] `GetSetupIntentResultAsync` → `Adapter_surface_round_trips_against_test_mode` (after server-side `pm_card_visa` confirm)
- [x] `ChargeOffSessionAsync` → `Adapter_surface_round_trips_against_test_mode`
- [x] `GetChargeAsync` → `Adapter_surface_round_trips_against_test_mode`
- [x] `ConstructEvent` (signature verify) → `Valid_signed_webhook_is_accepted_and_persisted` (pass) + `Tampered_signature_is_rejected_and_not_persisted` (reject)

Webhook persistence (FR-013) → `WebhookEventInbox` row asserted on the valid case; absence asserted on the tampered case.

## `IAlertProvider` — email (US2 — `SendGridSandboxTests`)

- [x] `SendAsync` accepted in sandbox mode, no delivery → `Sandbox_send_is_accepted_without_delivery`
- [x] `SendAsync` handled failure (unverified sender) → `Unverified_sender_is_reported_as_a_handled_failure`
- [x] Sandbox no-deliver seam (`MailSettings.SandboxMode`) → exercised by both tests (guardrail asserts `Sandbox==true`)

## `IAlertProvider` — sms (US3 — `TwilioSandboxTests`)

- [x] `SendAsync` success (magic `+15005550006`, returns `Sid`) → `Magic_numbers_drive_success_and_handled_failure(true)`
- [x] `SendAsync` handled failure (magic `+15005550001`) → `Magic_numbers_drive_success_and_handled_failure(false)`
- [x] Test-credential basic-auth path (`Init(AccountSid, AuthToken)`) → exercised by both theory cases

## Notes

- Every call is wrapped in `SandboxResult.RunAsync(...)`, so a provider outage skips (does not fail).
- Assertions target only objects this run created (shared sandbox accounts — Clarifications Q4).
