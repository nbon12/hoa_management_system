# Feature Specification: Replace SendGrid with Amazon SES for Transactional Email

**Feature Branch**: `026-ses-email-provider`
**Created**: 2026-10-06
**Status**: Draft
**Input**: User description: "Replace SendGrid with Amazon SES (SESv2) as the transactional email provider. Context: the SendGrid account's free trial ended, so every SendGrid API call returns 401; this fails the main-push "Integration (provider sandbox)" CI job (SendGridSandboxTests), which in turn skips Push Docker Image and Deploy to Dev, so nothing has deployed since 2026-08-28 and the scheduled Trivy image scan fails on the stale published image. SendGrid Essentials (~$20/mo) is too expensive for current volume (~20-60 CI sandbox calls/month, zero real deliveries; no deployed environment currently sends email). SES is pay-per-use (~$0.10/1000) and the owner has a long-lived AWS account; the backend already references AWSSDK.S3. Scope: a new SES-backed email IAlertProvider (channel "email") replacing SendGridEmailProvider; new validated configuration options (region, from address/name, credentials) replacing SendGrid options in the 008 config-validation scheme; remove SendGrid packages, options, DI wiring and GitHub secrets usage; CI sandbox tests rewritten to use the SES mailbox simulator (success@simulator.amazonses.com, bounce@simulator.amazonses.com) so real API calls are made with zero delivery to real people, preserving the existing semantics that a missing/unconfigured secret SKIPs (does not block) while a real failure FAILs; the adapter must never throw and must map non-2xx/SDK errors to AlertSendResult.Fail. Out of scope: bounce/complaint SNS event handling (rely on SES account-level suppression list), marketing email, provisioning SES via OpenTofu, wiring SES into Dev/PR/staging runtime environments. Owner manual steps (domain verification with DKIM in Cloudflare, least-privilege IAM user with ses:SendEmail only, GitHub secrets, production-access request) must be documented."

## Background

Transactional email (payment receipts and failure alerts from 006, verification and claim-code
emails from 016) currently goes through SendGrid. The SendGrid account's free trial has ended, and
SendGrid now rejects every request as unauthorized. The main-branch provider-sandbox CI stage (007)
treats that as a real regression, so it blocks the image push and the Dev deploy. Nothing has
deployed since 2026-08-28. The scheduled container scan keeps flagging the stale published image.

Current volume is about 20–60 sandbox calls a month, all from CI, with zero real deliveries. A
flat-fee plan (about $20/month) is out of proportion to that volume. Amazon SES charges per message
(about $0.10 per 1,000) and the owner already has an established AWS account. This feature swaps the
email provider to SES and keeps every existing behavior and safety guarantee.

## Clarifications

### Session 2026-10-06

- Q: How should the main-push CI job authenticate to AWS to call SES? → A: Static access keys for a send-only IAM user, stored as GitHub repository secrets. GitHub OIDC federation is a documented future improvement and is not built here.
- Q: Which domain should SES verify and send from? → A: The dedicated subdomain `mail.nekohoa.com`, with DKIM records in Cloudflare. The sender address is `no-reply@mail.nekohoa.com`. This keeps transactional sender reputation separate from the root `nekohoa.com` domain.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Main-branch releases flow again (Priority: P1)

As the project owner, I want the main-branch provider-sandbox stage to exercise a working email
provider, so that merges to `main` once again produce a published image and a Dev deploy.

**Why this priority**: This is the outage. Until email sandbox checks pass, no change reaches Dev
and the security scan keeps failing on a stale image.

**Independent Test**: With the SES CI secrets configured, push to `main`. The email sandbox tests
report Passed (not Skipped), the provider-sandbox job is green, and the image push and Dev deploy
jobs run.

**Acceptance Scenarios**:

1. **Given** valid SES test credentials, a verified sender, and the no-deliver guard enabled in CI,
   **When** the sandbox stage sends an email to the SES success simulator address, **Then** the send
   is reported as successful and no real person receives an email.
2. **Given** the same configuration, **When** the sandbox stage sends with a malformed sender address,
   **Then** the adapter returns a handled failure result. It does not throw, and the test asserts
   failure.
3. **Given** the SES credentials secret is absent from CI, **When** the sandbox stage runs, **Then**
   every SES email test reports Skipped (not Failed) and the job does not block the release.
4. **Given** SES credentials that SES rejects as invalid, **When** the sandbox stage runs, **Then**
   the email success test reports Failed and the image push is blocked. This keeps the existing
   "real failure blocks release" rule.

---

### User Story 2 - Real email can never leak from automated tests (Priority: P1)

As the project owner, I want a hard guarantee that automated tests cannot email a real resident,
even with real SES credentials, because SES credentials have no test/live distinction.

**Why this priority**: SendGrid's sandbox flag was the only no-deliver guardrail for email. Removing
it without an equal replacement would make it possible to email real people from CI.

**Independent Test**: With the no-deliver guard on, ask the email adapter to send to an ordinary
address (for example `resident@nekohoa.dev`). The adapter refuses with a handled failure and makes no
provider call. With the guard off, the sandbox harness refuses to run SES tests at all.

**Acceptance Scenarios**:

1. **Given** the no-deliver guard is enabled, **When** a send targets any address outside the SES
   mailbox simulator domain, **Then** the adapter returns a handled failure naming the guard and does
   not contact SES.
2. **Given** the no-deliver guard is enabled, **When** a send targets a simulator address, **Then**
   the send proceeds to SES.
3. **Given** SES credentials are present in the sandbox harness but the no-deliver guard is disabled,
   **When** an SES sandbox test starts, **Then** the test hard-fails with a message that the guard is
   required. It does not skip and does not send.
4. **Given** production-style configuration (guard not set), **When** the application sends a
   payment receipt, **Then** the guard defaults to off and delivery proceeds normally.

---

### User Story 3 - Misconfiguration is caught at startup (Priority: P2)

As an operator, I want incomplete or invalid email configuration to stop the app at startup with a
clear message for each problem, the same way every other provider's settings are validated (008).

**Why this priority**: A half-configured provider would silently drop receipts and verification
emails. Startup validation is the project's standard defense.

**Independent Test**: Start the app with each partial or invalid email configuration and confirm it
refuses to start with one message per problem. Start it with no email configuration at all and
confirm it starts with email disabled.

**Acceptance Scenarios**:

1. **Given** no email settings at all, **When** the app starts, **Then** startup succeeds, email is
   reported as not configured, and verification/claim-code delivery falls back to audit-log-only, as
   it does today.
2. **Given** some but not all required email settings (for example, a region without a sender
   address), **When** the app starts, **Then** startup fails and lists each missing setting
   separately.
3. **Given** a sender address that is not a valid email address, **When** the app starts, **Then**
   startup fails with a message identifying the invalid sender.
4. **Given** an access key ID without its matching secret, or the reverse, **When** the app starts,
   **Then** startup fails with a message that both credential halves are required together.
5. **Given** any startup validation failure, **When** messages are logged, **Then** no credential
   value appears in the output.

---

### User Story 4 - SendGrid is fully removed and the owner knows what to set up (Priority: P2)

As the project owner, I want SendGrid removed from code, CI and documentation, and a written
checklist of the SES steps only I can do, so that the switch is complete and repeatable.

**Why this priority**: Leftover SendGrid code, secrets or docs would mislead future work. The AWS
steps (domain verification, IAM, production access) can't be automated from the repo.

**Independent Test**: Search the application, test project and CI workflows for SendGrid references
and find none. Follow the owner checklist on a fresh AWS account and reach a passing CI run.

**Acceptance Scenarios**:

1. **Given** the feature is merged, **When** the backend, test project and CI workflows are searched
   for SendGrid references, **Then** no package, option, adapter, validator, secret or test
   references remain.
2. **Given** the owner checklist, **When** the owner follows it, **Then** it covers choosing a region,
   verifying the sending domain `mail.nekohoa.com` with DKIM records in Cloudflare, creating a least-privilege identity
   allowed only to send email, adding the CI secrets, enabling the account-level suppression list,
   requesting production access, and removing the old SendGrid secrets.
3. **Given** earlier specs that describe SendGrid as the email provider, **When** this feature
   merges, **Then** those `spec.md` files are updated to name SES so the spec corpus stays consistent.

---

### Edge Cases

- **SES outage or timeout during CI**: transport and availability failures that survive the
  existing bounded retry are classified as Skipped (provider unavailable), not Failed, as with the
  other providers.
- **SES account still in the SES sandbox (no production access yet)**: CI still passes, because
  simulator addresses are always allowed. Real resident delivery is not possible until production
  access is granted. That is documented, not an error.
- **Sender identity not verified in the chosen region**: SES rejects the send. The adapter returns a
  handled failure, and the CI success test Fails, surfacing the setup gap.
- **Bounce simulator**: SES accepts a send to the bounce simulator and reports the bounce later.
  Because bounce events are out of scope, a send to the bounce simulator counts as accepted. The spec
  does not require detecting the bounce.
- **Region mismatch**: credentials valid but sender verified in a different region cause a
  rejection. Same handling as an unverified sender.
- **Whitespace in pasted secrets**: values pasted into CI secret stores with trailing newlines are
  trimmed, and whitespace-only values count as absent (skip), matching the existing harness rule.
- **Recipient address casing or surrounding whitespace**: the no-deliver guard compares the
  recipient domain case-insensitively after trimming, so `SUCCESS@Simulator.AmazonSES.com` is
  allowed and a look-alike domain such as `simulator.amazonses.com.evil.test` is refused.
- **Credentials omitted entirely while region and sender are set**: allowed. The provider uses the
  platform's default credential chain. This keeps a future path to keyless auth from the runtime
  environment.

## Requirements *(mandatory)*

### Functional Requirements

**Email provider**

- **FR-001**: The system MUST deliver transactional email through Amazon SES, using the existing
  email channel (`"email"`) of the alert-provider abstraction. Every current caller (payment
  receipts and alerts, verification and claim-code emails) keeps working with no caller changes.
- **FR-002**: The email adapter MUST never throw to its caller. Every SES rejection, SDK error,
  transport error and non-success response MUST become a handled failure result with a short
  reason that contains no credential values.
- **FR-003**: The email adapter MUST send the subject and body it is given, from the configured
  sender address and display name. It MUST use the existing default subject when none is supplied.
- **FR-004**: When email is not configured, the adapter MUST report itself as not configured and
  return a handled failure on send without contacting SES, as the current adapter does.

**No-deliver guardrail**

- **FR-005**: The system MUST provide a no-deliver guard setting that defaults to off. When on, the
  adapter MUST refuse, without contacting SES, any send whose recipient's domain is not the SES
  mailbox simulator domain (`simulator.amazonses.com`), compared case-insensitively after trimming.
- **FR-006**: The provider-sandbox test harness MUST hard-fail any SES test that starts with
  credentials present but the no-deliver guard off. The harness MUST enable the guard by default.

**Configuration and validation**

- **FR-007**: Email configuration MUST consist of AWS region, sender address, sender display name
  (default "NekoHOA"), optional access key ID and secret access key, and the no-deliver guard flag.
  It replaces the SendGrid configuration section.
- **FR-008**: Startup validation MUST follow the 008 scheme. A fully empty email configuration is
  valid and disables email. Once any field is set, each missing or invalid required field (region,
  sender address, sender address format) is reported as a separate error. The access key ID and
  secret MUST be both present or both absent.
- **FR-009**: Credential values MUST never appear in logs, validation messages, exception messages
  or test output.

**CI provider-sandbox stage**

- **FR-010**: The main-push provider-sandbox CI job MUST run the SES email sandbox tests with SES
  credentials, region and sender supplied from repository secrets. The credentials MUST be a static
  access key ID and secret access key for an IAM user whose only permission is sending email. PR
  jobs MUST still receive no provider secrets.
- **FR-011**: The SES sandbox tests MUST cover at least: (a) a send to the success simulator
  returns success; (b) a malformed sender returns a handled failure; (c) a send to an ordinary
  address with the guard on returns a handled failure without contacting SES.
- **FR-012**: SES sandbox tests MUST skip, not fail, when credentials, region or sender are absent.
  They MUST fail when SES rejects the request for a non-availability reason (for example invalid
  credentials or an unverified sender). Availability failures MUST use the existing
  outage-vs-regression classification.

**Removal and documentation**

- **FR-013**: All SendGrid packages, options, validator, adapter, configuration entries, test
  fixtures, tests and CI secret references MUST be removed.
- **FR-014**: The repository MUST include an owner setup checklist covering every manual SES step
  listed in User Story 4, scenario 2. The checklist MUST include the exact IAM permission to grant
  (send-email only) and the names of the CI secrets to create.
- **FR-015**: The provider-sandbox README's safety invariants MUST be updated to replace the
  SendGrid sandbox-flag invariant with the SES no-deliver guard invariant.
- **FR-016**: Existing `spec.md` files that name SendGrid as the email provider (006, 007, 013) MUST
  be updated to name SES, with a note pointing to this spec.

### Key Entities

- **Email provider configuration**: region, sender address, sender display name, optional
  credential pair, no-deliver guard flag. Replaces the SendGrid configuration. Not persisted;
  supplied through app configuration and secrets.
- **Alert send result**: the existing success/failure outcome with a reason. Unchanged.
- **SES mailbox simulator addresses**: fixed SES test recipients (success, bounce, and others) that
  produce real API responses without delivering to a person.

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: No change. Email delivery is not tenant data; callers already decide
  recipients within their own HOA scope.
- **Authorization**: No new endpoints or protected actions.
- **Ownership and moderation**: Not applicable.
- **API contract**: No public API changes.
- **API implementation and docs**: No FastEndpoints or Swagger changes.
- **Database/runtime**: No schema or migration changes.
- **File storage**: Not applicable. The existing S3-compatible client for R2/MinIO is untouched.
- **Security and abuse controls**: Least-privilege send-only credentials; credentials never logged
  (FR-009); no-deliver guard prevents test email leaking to real people (FR-005/FR-006); the SES
  account-level suppression list is enabled to protect sender reputation.
- **Observability**: Send failures are logged as Serilog warnings with the handled reason and
  without credentials or message bodies. No Sentry changes.
- **Accessibility**: Not applicable (no UI).
- **Quality gates**: The no-deliver guard and configuration validator carry the testable logic and
  MUST have unit tests covering every branch, using xUnit `[Theory]` data for recipient variants
  (simulator, mixed case, whitespace, look-alike domain, ordinary address) and every
  partial-configuration combination. The thin network adapter stays excluded from coverage, matching
  the existing SendGrid and Stripe adapters. Its behavior is covered by the sandbox tests.
  Startup-validation integration tests (008) are updated for the new section. Tests remain safe
  under parallel execution: sandbox tests assert only on their own sends, and simulator addresses
  are shared and stateless. Repowise documentation for the alerts domain is refreshed. This is a
  focused, single-concern PR.
- **Frontend testing**: Not applicable (no frontend change).
- **Executable & living spec**: Every acceptance scenario above maps to an automated test (sandbox,
  unit or startup-validation), except User Story 4 scenario 2 (documentation review) and scenario 3
  (spec-corpus edit), which are verified in code review. Specs 006, 007 and 013 `spec.md` are updated
  per FR-016.
- **Spec independence & parallelism**: Independently completable. No dependency on any open spec.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The first main-branch push after merge, with secrets configured, completes the
  provider-sandbox stage green with the email success test Passed (not Skipped), and the image push
  and Dev deploy run.
- **SC-002**: Zero emails reach a non-simulator recipient from any automated test run. A test sending
  to an ordinary address with the guard on is refused 100% of the time without a provider call.
- **SC-003**: Recurring email cost at current volume (under 100 sandbox sends a month) is under
  $1/month, down from the about $20/month flat-fee alternative.
- **SC-004**: The scheduled image security scan passes once a freshly built image has been
  published, with no new allowlist entries.
- **SC-005**: Every partial or invalid email configuration is rejected at startup with one message
  per problem, and 0 credential values appear in any log or test output.
- **SC-006**: A repository search for SendGrid in the backend, test project and CI workflows
  returns no results.

## Assumptions

- The owner verifies the sending subdomain `mail.nekohoa.com` (DNS in Cloudflare; sender
  `no-reply@mail.nekohoa.com`, see Clarifications) and creates CI
  credentials before the first main push. Until then, SES tests skip and do not block, per FR-012.
- CI uses static access keys for a send-only IAM user (see Clarifications). Keyless federation
  (GitHub OIDC to AWS) is a later improvement and is out of scope. The owner checklist MUST
  recommend rotating the keys periodically.
- One SES region is used for all sends. `us-east-1` is the documented default, but any region works
  if the sender is verified there.
- SES production access is requested but not required for this feature to pass, because simulator
  addresses work while the account is still in the SES sandbox.
- No deployed environment (Dev, PR, staging) sends email today. Wiring SES credentials into those
  runtime environments is a follow-up. Until then they keep the current audit-log-only fallback.
- Bounce and complaint handling relies on SES's account-level suppression list. Processing bounce
  and complaint events is out of scope.
- Marketing or bulk email and provisioning SES through OpenTofu are out of scope.
