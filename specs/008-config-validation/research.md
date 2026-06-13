# Phase 0 Research: Startup Configuration Validation

All Technical Context unknowns are resolved below. No outstanding NEEDS CLARIFICATION.

## R1. Validation mechanism: FluentValidation bridged to `IValidateOptions<T>` + `ValidateOnStart`

- **Decision**: Bind each option group with
  `services.AddOptions<T>().Bind(config.GetSection(T.SectionName)).ValidateOnStart()` and
  register a generic `FluentValidateOptions<T> : IValidateOptions<T>` that resolves an
  `IValidator<T>` (FluentValidation) and maps failures to `ValidateOptionsResult.Fail(...)`.
  A `services.AddValidatedOptions<TOptions, TValidator>(section)` extension encapsulates the
  three-line registration so `Program.cs` stays declarative.
- **Rationale**: FluentValidation is **already a project dependency** (via FastEndpoints;
  endpoint `Validator<T>` classes derive from `AbstractValidator<T>`), so config validators
  reuse the team's existing, familiar rule syntax — including the cross-field rules
  (`RuleFor(x => x.CardScope).Equal(CreditOnly).When(x => x.CardFeeType == "Percentage")`)
  that DataAnnotations cannot express. `ValidateOnStart()` forces evaluation during host
  startup (`IHost.StartAsync`) rather than lazily on first `IOptions<T>.Value` access, giving
  the fail-fast guarantee (FR-001, FR-013).
- **Alternatives considered**:
  - *DataAnnotations + `ValidateDataAnnotations()`*: simpler but cannot express cross-field
    rules (percentage⇒credit-only) cleanly and would split validation across two idioms.
    Rejected for consistency and expressiveness.
  - *Manual guard clauses in `Program.cs`* (the current `?? throw` style): does not scale to
    7 option groups, is untestable in isolation, and is the status quo we are replacing.

## R2. FluentValidation package availability

- **Decision**: Add an explicit `<PackageReference Include="FluentValidation" />` to
  `HOAManagementCompany.csproj` pinned to the version FastEndpoints already resolves
  (verify with `dotnet list package --include-transitive`), rather than relying on the
  transitive reference.
- **Rationale**: The config validators use FluentValidation directly (`AbstractValidator<T>`,
  `IValidator<T>` for DI) outside of FastEndpoints' `Validator<T>` wrapper. An explicit,
  version-aligned reference makes the dependency intentional and avoids breakage if
  FastEndpoints later drops or bumps it. Aligning the version prevents a duplicate/diamond.
- **Alternatives considered**: Rely purely on the transitive dependency — works today but is
  implicit and fragile; rejected.

## R3. Error aggregation strategy (FR-003)

- **Decision**: Within each option group, FluentValidation naturally collects **all** rule
  failures; the bridge joins them into one `ValidateOptionsResult.Fail(IEnumerable<string>)`.
  Across groups, register each validated option group so each contributes its own failures;
  to report **all groups at once**, run validation eagerly in startup order and aggregate.
  Practical approach: keep `ValidateOnStart()` (which throws an `OptionsValidationException`
  on the first failing group) **and** the per-group aggregation, which already satisfies the
  spirit of FR-003 (an operator sees every error within the offending section). If full
  cross-group aggregation is desired, add a single composite startup check that resolves and
  validates all groups and throws once with the combined message.
- **Rationale**: Per-group aggregation covers the common case (one section misconfigured)
  with zero extra machinery; the optional composite check covers the "several sections wrong"
  edge case from the spec. Messages name `section:field` and never include values (FR-019).
- **Alternatives considered**: Stop-on-first-error only — rejected per the spec's explicit
  preference for aggregation to minimize fix-and-retry cycles.

## R4. Startup fail-fast testing with `WebApplicationFactory<Program>`

- **Decision**: Reuse the existing `WebApplicationFactory<Program>` pattern. Build a factory
  with `.WithWebHostBuilder(b => b.UseEnvironment(...).ConfigureAppConfiguration(...))` to
  inject the bad config, then assert that triggering host start throws
  `OptionsValidationException` (e.g. `Assert.Throws<OptionsValidationException>(() =>
  factory.Services.GetService<...>())` / `CreateClient()` forces build+start).
- **Rationale**: `ValidateOnStart` failures surface when the host starts, which
  `WebApplicationFactory` does on first server access. **No PostgreSQL/Testcontainers is
  required** for failure tests because validation throws before any DB connection is opened —
  keeping these tests fast and independent of the integration-DB collection. The positive
  "valid Test config (placeholder secrets) starts" case is already exercised by the existing
  `IntegrationTestBase` boot, plus an explicit assertion test.
- **Alternatives considered**: A bare `Host.CreateApplicationBuilder` mini-host — viable for
  pure validator wiring, but reusing the real `Program` start path gives higher-fidelity
  coverage that the actual registrations are validated.

## R5. Placeholder secrets for Development/Test (clarified 2026-06-13)

- **Decision**: Validation is uniform across environments; secret-*presence* is required
  everywhere. Development supplies non-functional placeholder values via
  `appsettings.Development.json` / `appsettings.Secrets.json` (gitignored) or env vars; the
  Test environment supplies them via the test fixture's `ConfigureAppConfiguration` (in-memory
  collection), never committing real or realistic credentials.
- **Rationale**: Removes all environment-branching from the validators (simpler, one rule
  set) while keeping CI/local frictionless. Presence — not authenticity — is validated, so a
  value like `sk_test_placeholder` satisfies the check; the `FakeStripeGateway` path used in
  tests means the placeholder is never sent to Stripe.
- **Alternatives considered**: Environment-conditional rules (secrets required only in
  Production) — rejected during clarification in favor of uniform, consistent validation.

## R6. Frontend boot-time guard (FR-017, FR-018, US4 = Option A)

- **Decision**: Add a pure `validateRuntimeConfig(environment)` function that returns the
  list of missing required keys (publishable key, API base URL) **only when
  `environment.production` is true**. In `main.ts`, run it **before** `bootstrapApplication`;
  if it reports missing values, skip bootstrap and render a minimal static full-page error
  (plain DOM injected into the app root) naming the missing value(s). The existing
  `APP_INITIALIZER` observability init stays as-is.
- **Rationale**: Running the guard before `bootstrapApplication` guarantees the app never
  loads into a partially-working state (Option A). A pre-bootstrap DOM render is perceivable
  (accessibility) without depending on Angular having booted. Gating on
  `environment.production` satisfies FR-018 (dev defaults never blocked).
- **Alternatives considered**: Throwing inside `APP_INITIALIZER` — yields a blank page +
  console error (Option C), rejected as not perceivable; an in-app banner (Option B) —
  rejected because it lets the broken app render.

## R7. `StorageOptions` null-forgiving bind removal (FR-011)

- **Decision**: Replace `config.GetSection("Storage").Get<StorageOptions>()!` in `Program.cs`
  with the validated-options path: register `StorageOptions` via `AddValidatedOptions`, and
  where the `IAmazonS3` singleton needs the values, resolve `IOptions<StorageOptions>` (whose
  `.Value` is guaranteed valid post-`ValidateOnStart`) instead of a raw, possibly-null bind.
- **Rationale**: Eliminates the `NullReferenceException`-on-missing-section failure mode and
  routes storage config through the same validation as everything else.
- **Alternatives considered**: Keep the raw bind but add a null check — rejected; it would
  duplicate validation and bypass the uniform mechanism.
