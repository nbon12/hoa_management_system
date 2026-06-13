# Feature Specification: Startup Configuration Validation

**Feature Branch**: `008-config-validation`  
**Created**: 2026-06-13  
**Status**: Draft  
**Input**: User description: "Add startup configuration validation to the .NET backend, test project, and Angular frontend — strongly-typed options classes are bound without validation; misconfiguration surfaces late (mid-request, deferred throws, or null reference) instead of failing fast. Validate all options at startup using FluentValidation (already a dependency) so the application refuses to start when configured incorrectly, with environment-aware rules. Add tests proving the app fails to start on invalid config, and add a frontend boot-time guard that fails loudly on missing required values."

## Clarifications

### Session 2026-06-13

- Q: Should configuration validation be environment-aware (secrets required only in Production), or strict in all environments? → A: Strict in all environments — there is no environment-conditional relaxation. Every option group, including secret *presence*, is validated identically in Development, Test, and Production. Local and CI environments satisfy secret-presence checks with non-functional placeholder values (injected by the test fixture / committed to dev config), never real credentials.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Backend refuses to start on invalid configuration (Priority: P1)

An operator deploys or runs the backend API with a configuration mistake — a missing
secret in production, a sample ratio outside its valid range, an unsupported telemetry
protocol, or a fee policy that contradicts itself (e.g. a percentage fee scoped to all
cards). Instead of starting "successfully" and then failing unpredictably during the
first payment, webhook, or telemetry export, the application refuses to start and emits a
single, specific error naming the configuration section and field that is wrong.

**Why this priority**: This is the core value of the feature. Today, misconfiguration is
discovered at the worst possible time — when a real user transaction or webhook hits the
broken code path — and the resulting error (a deferred payment-provider exception, a null
reference on a missing storage section) does not point operators at the fix. Failing fast
at boot with a precise message turns a production incident into a deploy-time error.

**Independent Test**: Boot the backend with a deliberately invalid configuration value
and confirm startup aborts with a clear, field-level error message; boot it with a valid
configuration and confirm it starts normally.

**Acceptance Scenarios**:

1. **Given** any environment with the payment secret key or webhook signing secret unset
   (no placeholder provided), **When** the application starts, **Then** startup fails with
   an error identifying the missing payment configuration field.
2. **Given** a payments fee policy configured as a percentage fee with an "all cards"
   scope, **When** the application starts, **Then** startup fails with an error stating
   that percentage fees must be scoped to credit cards only.
3. **Given** an observability trace sample ratio set outside the range 0 to 1 (e.g. 1.5),
   **When** the application starts, **Then** startup fails with an error identifying the
   out-of-range sampling value.
4. **Given** a telemetry export protocol set to an unsupported value, **When** the
   application starts, **Then** startup fails with an error stating only the supported
   protocol is allowed.
5. **Given** a missing or empty storage configuration section, **When** the application
   starts, **Then** startup fails with a clear error naming the missing storage fields
   (rather than a null-reference error deep in dependency wiring).
6. **Given** a fully valid configuration for the active environment, **When** the
   application starts, **Then** it starts successfully with no validation errors.

---

### User Story 2 - Local and CI workflows stay frictionless without real credentials (Priority: P1)

A developer runs the backend locally or in CI without real third-party credentials. The
test suite uses in-memory fakes for the payment provider and other integrations.
Validation is strict in every environment — there is no environment-conditional
relaxation — so to satisfy secret-presence checks the local and CI configurations supply
non-functional **placeholder** values (the test fixture injects them; dev config commits
throwaway values). The app boots normally because presence is satisfied, and no real
credentials are ever required or committed.

**Why this priority**: Equally critical to P1 — strict validation in every environment is
only adoptable if local runs and the CI pipeline (which rely on fake gateways) can satisfy
it without live credentials. Uniform rules plus placeholder values keep development and CI
fast while still catching a genuinely missing secret.

**Independent Test**: Boot the backend in Development and Test with placeholder secret
values and confirm it starts; remove a required secret entirely (no placeholder) and
confirm it refuses to start in that same environment.

**Acceptance Scenarios**:

1. **Given** the Development environment with placeholder secret values supplied, **When**
   the application starts, **Then** it starts successfully.
2. **Given** the Test environment whose fixture injects placeholder secret values, **When**
   the test host starts, **Then** it starts successfully without any real provider
   credentials.
3. **Given** any environment with a required secret entirely unset (no placeholder),
   **When** the application starts, **Then** it refuses to start and names the missing
   secret.
4. **Given** any environment, **When** a structural rule is violated (e.g. an out-of-range
   numeric value or contradictory fee policy), **Then** startup fails — validation rules
   are identical across all environments.

---

### User Story 3 - Configuration rules are guarded by automated tests (Priority: P2)

A maintainer changes a configuration option or its defaults. The test suite includes
tests that boot a minimal host with invalid configuration and assert that startup is
rejected, plus focused tests for each validation rule. Previously, rules such as
"percentage fee requires credit-only scope" lived only in code comments and were not
enforced or tested; now they are regression-guarded.

**Why this priority**: Ensures the validation rules stay correct over time and documents
the intended configuration contract as executable tests. It depends on P1 existing but
delivers durable protection rather than a one-time fix.

**Independent Test**: Run the test suite and confirm it includes passing tests that assert
startup failure for each category of invalid configuration and startup success for valid
configuration.

**Acceptance Scenarios**:

1. **Given** the test suite, **When** it runs, **Then** it includes a test that boots a
   host with an out-of-range sample ratio and asserts startup fails.
2. **Given** the test suite, **When** it runs, **Then** it includes a test that boots a
   Production-like host with a missing required secret and asserts startup fails.
3. **Given** the test suite, **When** it runs, **Then** it includes a test that asserts a
   percentage-fee-with-all-cards-scope policy is rejected.
4. **Given** the test suite, **When** it runs, **Then** it includes tests that exercise
   each validator's pass and fail paths directly.

---

### User Story 4 - Frontend fails loudly on missing required configuration (Priority: P3)

The single-page application is built and deployed with a missing or empty required
value — most importantly the payment publishable key or the API base URL in a production
build. Instead of loading and then failing silently or mid-checkout in the browser, the
app detects the missing value during startup and surfaces a clear, visible failure so the
deployment problem is caught immediately.

**Why this priority**: Lower priority because the backend gap is the larger correctness
risk, but valuable because a missed deploy-time key injection currently produces a
confusing in-browser failure during a real payment rather than an obvious boot failure.

**Independent Test**: Produce a production frontend configuration with the publishable key
empty and confirm the app surfaces a clear startup failure; with the key present, confirm
the app boots normally.

**Acceptance Scenarios**:

1. **Given** a production frontend configuration with the payment publishable key empty,
   **When** the app starts, **Then** it surfaces a clear, visible error indicating the
   required configuration value is missing.
2. **Given** a production frontend configuration with the API base URL empty, **When** the
   app starts, **Then** it surfaces a clear startup error naming the missing value.
3. **Given** a complete and valid frontend configuration, **When** the app starts, **Then**
   it boots normally with no configuration error.
4. **Given** a non-production (development) build, **When** required values follow local
   defaults, **Then** the guard does not block startup.

---

### Edge Cases

- **Multiple invalid values at once**: When several configuration fields are invalid,
  startup failure SHOULD report all detected validation errors together (not just the
  first), so an operator can fix everything in one pass.
- **Whitespace-only values**: A secret or required field set to whitespace MUST be treated
  as empty/missing, not as a valid value.
- **Optional-by-design integrations**: Alerting providers (SMS/email) are intended to be
  optional and disabled when unset; validation MUST NOT force these to be present, but
  MUST reject partially-configured providers that would fail at runtime (e.g. an account
  identifier present with no usable authentication pairing).
- **Boundary values**: Sample ratios of exactly 0 and exactly 1 MUST be accepted; values
  just outside (e.g. -0.01, 1.01) MUST be rejected.
- **Placeholder vs. missing secret**: A non-empty placeholder value satisfies a
  secret-presence check (validation enforces presence, not authenticity); a secret that is
  entirely unset or whitespace-only MUST be rejected in every environment.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The backend MUST validate all strongly-typed configuration option groups at
  application startup and refuse to start when any validation rule fails.
- **FR-002**: Startup validation failures MUST produce a clear error that identifies the
  configuration section and field(s) that failed and the reason for each failure.
- **FR-003**: When multiple configuration values are invalid, the startup error MUST
  aggregate and report all detected failures rather than only the first.
- **FR-004**: Validation MUST apply uniformly across all environments (Development, Test,
  Production) with no environment-conditional relaxation. Required secrets (e.g. payment
  secret key, webhook signing secret, scheduler shared secret) MUST be present (non-empty)
  in every environment; presence is validated, not authenticity, so a placeholder value
  satisfies the check.
- **FR-005**: All validation — structural, business-rule, and secret-presence — MUST be
  enforced identically in every environment (Development, Test, and Production).
- **FR-006**: The payments fee policy MUST be validated such that: fee type is one of the
  supported values ("Flat" or "Percentage"); card scope is one of the supported values
  ("AllCards" or "CreditOnly"); a percentage fee MUST be scoped to credit cards only; all
  fee amounts/rates MUST be non-negative.
- **FR-007**: Payments timing values MUST be validated: the variable-amount notice lead
  time MUST be zero or greater, and the pending-transaction sweep window MUST be greater
  than zero.
- **FR-008**: The payment webhook tolerance window MUST be validated as greater than zero.
- **FR-009**: Observability sampling ratios (overall trace sampling and the independent
  error-tracking trace sampling) MUST each be validated to fall within the inclusive range
  0 to 1.
- **FR-010**: The telemetry export protocol MUST be validated to equal the single supported
  value, and the telemetry endpoint MUST be validated as a well-formed absolute URL.
- **FR-011**: The storage configuration MUST be validated for the presence of its required
  fields, replacing the current behavior where a missing section causes a null-reference
  failure during dependency wiring.
- **FR-012**: Optional alerting providers (SMS and email) MUST remain optional — absence
  MUST NOT block startup — but a partially-configured provider that could not function at
  runtime MUST be rejected at startup.
- **FR-013**: The backend MUST NOT defer required-configuration failures to first use; a
  configuration error that makes a core capability unusable MUST be caught at startup
  rather than during a request.
- **FR-014**: The automated test suite MUST include tests that boot a host with invalid
  configuration and assert that startup is rejected, covering at minimum: an out-of-range
  sample ratio, a required secret left entirely unset (no placeholder), and a contradictory
  fee policy.
- **FR-015**: The automated test suite MUST include focused tests for each configuration
  validator covering both passing and failing inputs.
- **FR-016**: The automated test suite MUST include a test confirming that a valid Test
  configuration — using placeholder secret values to satisfy presence checks — starts
  successfully (guarding against validation breaking CI).
- **FR-017**: The frontend MUST validate required configuration values during application
  startup and surface a clear, visible failure when a required value (at minimum the
  payment publishable key and the API base URL) is missing in a production build.
- **FR-018**: The frontend startup configuration guard MUST NOT block startup for
  non-production builds that rely on local defaults.
- **FR-019**: Configuration error messages MUST NOT echo secret values; they may name the
  field that is missing or invalid but MUST NOT print its contents.

### Key Entities *(include if feature involves data)*

- **Configuration option group**: A named, strongly-typed grouping of related settings
  (e.g. storage, observability, payments, payment-provider, scheduler, SMS, email) bound
  from the application's configuration sources. Each group has a set of fields, defaults,
  and rules describing valid combinations.
- **Validation rule set**: The collection of constraints applied to a configuration option
  group — required-ness (possibly environment-dependent), allowed value sets, numeric
  ranges, format checks (e.g. absolute URL), and cross-field rules (e.g. percentage fee
  implies credit-only scope).
- **Runtime environment**: The active deployment context (Development, Test, Production).
  It does not change which validation rules apply — rules are uniform across environments —
  but it does select which configuration source supplies values (e.g. dev config /
  test-fixture placeholders vs. real deployed secrets).
- **Frontend runtime configuration**: The set of values the single-page application needs
  at startup (e.g. API base URL, payment publishable key, telemetry endpoint) whose
  required members are checked during boot.

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: Not applicable — this feature concerns application-level
  configuration, not per-HOA data. No `hoa_id`/`association_id`-scoped entities are
  introduced or queried.
- **Authorization**: Not applicable to runtime requests. Configuration is supplied by
  operators/deployers; no new user-facing protected actions are added.
- **Ownership and moderation**: Not applicable — no user-generated content is involved.
- **API contract**: No new or changed API endpoints, response shapes, or pagination. The
  only observable behavior change is that a misconfigured deployment fails to start.
- **API implementation and docs**: No endpoint changes; Swagger remains development-only.
- **Database/runtime**: No schema or migration changes. The startup-migration and
  short-lived-DbContext expectations are unaffected; validation runs before the app begins
  serving and must not interfere with idempotent startup migrations.
- **File storage**: No new file storage. Storage configuration is validated for presence of
  required fields only; local Docker Compose/MinIO and test defaults must continue to
  satisfy validation.
- **Security and abuse controls**: Configuration error messages MUST NOT leak secret
  values (FR-019). Failing fast on missing/invalid secrets reduces the window in which the
  app runs in an insecure or partially-broken state.
- **Observability**: Observability configuration (sampling ratios, protocol, endpoint) is
  itself validated. Telemetry initialization remains non-fatal where it is today; this
  feature adds validation of the *configuration values*, not new telemetry that could block
  startup.
- **Accessibility**: The frontend startup failure indication for missing configuration MUST
  be perceivable (a visible message, not a silent console-only failure) so the deployment
  problem is noticeable.
- **Quality gates**: New validation logic and validators require unit and startup-level
  test coverage (FR-014–FR-016) consistent with the project's coverage expectations;
  Theory-style data variations should cover boundary values (e.g. 0 and 1 for ratios) and
  each allowed/disallowed enumerated value. Tests MUST remain safe under parallel
  execution and must not depend on real external credentials.
- **Frontend testing**: The frontend startup guard requires unit/component test coverage
  asserting it fails on missing required production values and passes on complete
  configuration.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the defined configuration option groups are validated at startup
  (none start unvalidated).
- **SC-002**: A deployment with any single invalid required configuration value fails to
  start in 100% of cases, with an error message that names the offending section and
  field.
- **SC-003**: Local Development and CI/Test runs continue to start successfully using only
  placeholder secret values (no real third-party credentials) in 100% of cases (no
  regression to developer or pipeline workflows).
- **SC-004**: Every configuration rule described in this specification is covered by at
  least one automated test asserting both a passing and a failing case.
- **SC-005**: For a misconfiguration, the time from "bad config deployed" to "operator sees
  a precise, actionable error" is reduced to application-startup time (seconds), versus the
  current state where the error appears only when an affected user action occurs.
- **SC-006**: A production frontend build missing a required value (payment publishable key
  or API base URL) surfaces a visible failure at startup in 100% of cases, rather than
  failing later during a user action.

## Assumptions

- Validation rules are uniform across all environments (no environment-conditional
  relaxation); local and CI environments satisfy secret-presence checks with
  non-functional placeholder values (committed dev config / test-fixture injection), never
  real credentials. This keeps the existing fake-gateway CI path working.
- Alerting providers (SMS and email) are intentionally optional and are expected to be
  disabled when their configuration is absent; validation only hardens against
  partially-configured providers.
- Secrets continue to be supplied via the existing mechanisms (local secrets file or
  environment variables); this feature validates their presence/shape, it does not change
  how they are provided or stored.
- The single supported telemetry export protocol and the existing option group names and
  fields described in the problem statement remain as they are today; this feature adds
  validation rather than redesigning the configuration surface.
- The frontend's required runtime values include, at minimum, the API base URL and the
  payment publishable key; additional required values may be added to the same guard
  without changing the approach.
- Aggregating all validation errors at startup is preferred over stop-on-first-error, to
  minimize fix-and-retry cycles for operators.
