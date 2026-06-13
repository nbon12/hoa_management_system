# Phase 0 Research: Stage 2 Integration CI

All Technical Context unknowns are resolved below. Each entry is a decision grounded in the actual adapter code (`Infrastructure/Payments/Alerts/*`, `Infrastructure/Payments/StripeGateway.cs`) and the existing test seam (`Fixtures/IntegrationTestBase.cs`, `PaymentTestBase.cs`, `AlertTestBase.cs`).

---

## R1 — How to run the *real* adapters (not the fakes) in an integration test

**Decision**: Add `SandboxIntegrationTestBase : IntegrationTestBase`. Do **not** override `IStripeGateway` or `IAlertProvider`. Supply real test-mode secrets through the existing `ExtraConfiguration()` hook (read from environment variables).

**Rationale**: The fakes are injected only by `PaymentTestBase.ConfigureTestServices` (swaps `IStripeGateway`) and `AlertTestBase.ConfigureTestServices` (swaps both `IAlertProvider`s). `IntegrationTestBase` itself swaps only DbContext/S3/telemetry and **leaves the real `StripeGateway`, `SendGridEmailProvider`, `TwilioSmsProvider` registered** (Program.cs:236, 251, 253). So deriving from `IntegrationTestBase` and *not* re-registering fakes yields the real adapters automatically, against the Testcontainers DB. No production DI change.

**Alternatives considered**:
- Environment-switched DI in Program.cs (register fakes when `ASPNETCORE_ENVIRONMENT=Test`) — rejected: invasive, changes production startup, and the current opt-in-at-test-base model is cleaner.
- A separate test project — rejected: duplicates the Testcontainers fixture and the `WebApplicationFactory` harness for no benefit.

---

## R2 — Stripe webhook signature verification without a network round-trip (Q1: signed fixture)

**Decision**: Build a JSON event payload in the test and compute the `Stripe-Signature` header in-process as `t=<unix>,v1=<HMAC-SHA256(\"<t>.<payload>\", whsec_test_…)>`, then `POST` it to `/payments/webhooks/stripe`. The real `StripeGateway.ConstructEvent` (which calls `EventUtility.ConstructEvent` with the configured `WebhookSigningSecret`) verifies it. Use a **captured real event** JSON as the payload body so it matches Stripe's actual schema.

**Rationale**: Signature verification is pure local HMAC against the signing secret — no Stripe API is involved (Clarifications Q1). A fixture signed with the real test `whsec_` is cryptographically identical to a Stripe-delivered event for verification. `StripeWebhookEndpoint` (lines 43–51) reads the raw body + header and calls `ConstructEvent`, then persists to `WebhookEventInbox` (FR-013) — exactly the path under test. Stripe.net exposes no public signer, so the test computes the HMAC with `System.Security.Cryptography.HMACSHA256`.

**Negative case (FR-012)**: POST the same payload with a tampered/short signature → endpoint returns 400 (`StripeException` caught at line 53) → assert 400 and no `WebhookEventInbox` row.

**Alternatives considered**: Stripe CLI `stripe trigger`/`listen` sidecar — rejected in Q1 (network dependency + flakiness in a deploy gate; only adds event-shape drift detection, mitigated by using a captured real event and the SDK's pinned API version).

---

## R3 — SendGrid: deliver-free verification (FR-015) and the missing sandbox seam

**Decision**: Add `bool Sandbox` to `SendGridOptions` (default `false`). In `SendGridEmailProvider.SendAsync`, when `Sandbox` is true set `msg.MailSettings = new MailSettings { SandboxMode = new SandboxMode { Enable = true } }`. Stage 2 configures `SendGrid:Sandbox=true` and asserts a 2xx accepted response with **no delivery**.

**Rationale**: The current adapter (`SendGridEmailProvider.cs:28–34`) sends with no `MailSettings`, so running it as-is delivers a **real email** — violating FR-008/SC-003. SendGrid sandbox mode (`mail_settings.sandbox_mode.enable=true`) makes SendGrid validate the request (auth, verified sender, payload) and return `200` **without delivering**. This is additive and default-off, so production behavior is unchanged.

**Critical safety note (feeds FR-009)**: SendGrid API keys have **no test/live distinction** — every key is live. Therefore sandbox mode is the *sole* no-deliver guardrail for email. The harness MUST assert `Sandbox==true` before any SendGrid send and refuse to run otherwise.

**Failure path (FR-016)**: Configure an unverified/blank `FromEmail` or an invalid key → SendGrid returns non-2xx (or the SDK throws) → adapter returns `AlertSendResult.Fail` → assert `Success==false`. Note the adapter never throws (catches to `Fail`, lines 40–44), so assertions target `AlertSendResult.Success`.

**Alternatives considered**: A SendGrid "mail sink"/test inbox — rejected: still delivers (just to a sink) and needs extra infrastructure; sandbox mode is the provider-native, zero-delivery answer.

---

## R4 — Twilio: test credentials, magic numbers, and the auth-model mismatch (FR-017/FR-018)

**Decision**: Add `AuthToken` to `TwilioOptions`. In `TwilioSmsProvider.SendAsync`, when `ApiKeySid` is empty but `AccountSid`+`AuthToken` are present, call `TwilioClient.Init(AccountSid, AuthToken)` (basic auth); otherwise keep the existing API-key path `TwilioClient.Init(ApiKeySid, ApiKeySecret, AccountSid)`. Stage 2 configures the **test** Account SID + **test** Auth Token, `From=+15005550006`, and recipient magic numbers.

**Rationale**: Twilio's magic-number test mode is only honored under **test credentials = test Account SID + test Auth Token via basic auth**; it is not honored under API-key auth (`TwilioSmsProvider.cs:30` uses API keys, and `TwilioOptions` has no Auth Token field). The magic `From` `+15005550006` is required (a real `From` throws under test creds). Recipients: `+15005550006` → success; `+15005550001` → invalid-number error. Additive + default-off (production continues on API keys).

**Theory (FR-017/FR-018)**: An xUnit `[Theory]` over `(recipient, expectSuccess)`:
- `(+15005550006, true)` → `AlertSendResult.Success==true`, a `Sid` returned, no real SMS.
- `(+15005550001, false)` → adapter catches the Twilio `ApiException` → `AlertSendResult.Success==false` (validates number formatting + error mapping).

**Alternatives considered**: Stuffing the test Auth Token into the existing `ApiKeySecret` slot (works mechanically because `Init` does basic auth) — rejected: semantically misleading config and fragile. A clean `AuthToken` field documents intent.

---

## R5 — Stripe: driving SetupIntent vaulting + off-session charge headlessly (FR-011)

**Decision**: Use Stripe's server-side test PaymentMethods. Flow:
1. `EnsureCustomerAsync(null, email, name)` → test customer.
2. `CreateSetupIntentAsync(customerId)` → confirm it server-side with test PM `pm_card_visa` (via a direct `SetupIntentService.ConfirmAsync` in the test using the test key) so it reaches `succeeded`.
3. `GetSetupIntentResultAsync(setupIntentId)` → assert vaulted method reference + brand/last4.
4. `ChargeOffSessionAsync(customerId, paymentMethodId, amount, …)` → assert `succeeded`.
5. One-time path: `CreatePaymentIntentAsync` → `GetPaymentIntentAsync`; `GetChargeAsync` on the resulting charge for settlement detail.

**Rationale**: The 006 design vaults via Stripe.js in a browser; CI has no browser. Stripe test mode allows confirming a SetupIntent server-side with canonical test PaymentMethod tokens (`pm_card_visa`), reaching `succeeded` without a client. This exercises every `IStripeGateway` method the fake bypasses (the adapter operations counted by SC-001).

**Alternatives considered**: Playwright-driven Stripe.js confirm — rejected: that is Stage 3 (staging E2E) scope; heavyweight for a per-merge gate. Skipping vaulting/off-session — rejected: those are exactly the adapter methods most likely to regress on an SDK upgrade.

---

## R6 — Distinguishing a real regression from a provider outage (FR-005 / SC-005)

**Decision**: A `SandboxResult` helper wraps each provider call with a bounded retry (e.g., 3 attempts, exponential backoff). It classifies exceptions:
- **Transport/availability** (timeouts, `HttpRequestException`/`SocketException`, Stripe `StripeException` with status 0 or ≥500, Twilio 5xx) → after retries, throw `Xunit.SkipException` so the test reports **Skipped (provider unavailable)**, not Failed.
- **Domain/assertion** (4xx, signature rejected when it should pass, wrong status, failed assertion) → **Fail**.

**Rationale**: xUnit renders transport and assertion failures identically (both red). The existing adapters swallow exceptions to `AlertSendResult.Fail` (so SMS/email outages need inspecting the result/inner state), while `StripeGateway` throws. A single classifier gives FR-005 a concrete home. The CI step then treats skips distinctly (job is green with a warning annotation; a real failure is red and blocks `docker-push`).

**Alternatives considered**: Native xUnit retry attributes — rejected: they retry on *any* failure, masking real regressions. Treating all red as fail — rejected: a Stripe sandbox blip would block deploys (erodes trust in the gate, SC-005).

---

## R7 — Keeping Stage 2 out of the PR/unit run; gating the deploy (FR-001/FR-002/FR-003)

**Decision**: Tag all Stage 2 tests `[Trait("Category","Sandbox")]`. Narrow the existing `test` job to `dotnet test --filter Category!=Sandbox` (stays hermetic, no secrets). Add an `integration-sandbox` job with `if: github.ref == 'refs/heads/main' && github.event_name == 'push'` running `dotnet test --filter Category=Sandbox` with provider secrets in `env`. Change `docker-push` from `needs: test` to `needs: [test, integration-sandbox]`.

**Rationale**: `test.yml` currently runs all tests under `ASPNETCORE_ENVIRONMENT=Test` on every PR and push; without a filter, sandbox tests would run on PRs (violating FR-001 main-only and FR-002 separation) and need secrets in PR context (a security risk for forks). `docker-push` already gates on `needs: test` + `if: main && push` (lines 132–136) — adding `integration-sandbox` to `needs` is the exact FR-003 hook. The job uses GitHub Actions' automatic secret masking (FR-010).

**Alternatives considered**: A separate workflow file triggered on `push: main` — viable, but co-locating in `test.yml` keeps the `docker-push` `needs:` gate in one place. A scheduled/nightly trigger — rejected in Q3 (main-only).

---

## R8 — Credential guardrail: refuse non-test/live credentials (FR-009)

**Decision**: `SandboxIntegrationTestBase` asserts, before any provider call:
- **Stripe**: `Stripe:SecretKey` starts with `sk_test_` or `rk_test_`; fail fast otherwise.
- **Twilio**: `Twilio:AccountSid` starts with `AC` **and** an explicit `TWILIO_TEST_CREDENTIALS=true` acknowledgement is set (Twilio test SIDs are not syntactically distinguishable from live SIDs).
- **SendGrid**: `SendGrid:Sandbox == true` (the only guardrail — keys have no test/live form, per R3).
- **Missing secret**: if a required secret is absent, the provider's tests **Skip with a clear "credential not configured" message** (FR-007) rather than fail or silently pass.

**Rationale**: Different providers expose different test markers; the guardrail is per-provider. The Stripe prefix check is strong; SendGrid relies wholly on sandbox mode; Twilio needs an explicit ack because test/live SIDs look alike. This makes a live key physically unable to run Stage 2 (FR-008).

**Alternatives considered**: A single uniform "is this a test key" check — rejected: impossible, because SendGrid has no such concept and Twilio SIDs are ambiguous.
