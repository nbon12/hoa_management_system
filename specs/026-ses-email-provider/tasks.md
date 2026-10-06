# Tasks: Replace SendGrid with Amazon SES for Transactional Email

**Input**: Design documents from `/specs/026-ses-email-provider/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ses-email-adapter.md, contracts/ci-sandbox-ses.md, quickstart.md

**Tests**: Test tasks are included and are part of the completion gate (spec.md → Constitution
Requirements → *Quality gates*; constitution §9: 95% coverage on changed/added relevant files).
Backend only: xUnit, `Xunit.SkippableFact`, and the existing `TestDatabaseFixture`/`WebApplicationFactory`
harness. No frontend tasks.

**Test-first (red→green)**: Per the Spec Kit Testing Constitution §2.4, each test task is written
and seen to **fail** before its paired implementation task. The pairs are:
T004→T005 (validator), T008→T009 (error mapper), T010→T011 (adapter core), T019→T020 (guard),
T021→T022 (guard in adapter), and T023→T024 (harness guard). Non-compiling tests may be stubbed
with a clear restore note until the type exists.

**Organization**: Grouped by user story. Priority order from spec.md: US1 (P1) · US2 (P1) ·
US3 (P2) · US4 (P2).

**Fake SES client for unit tests**: The test project has no mocking library. `AmazonSimpleEmailServiceV2Client.SendEmailAsync`
is `virtual`, so unit tests use a private `FakeSesClient : AmazonSimpleEmailServiceV2Client`,
constructed with `new BasicAWSCredentials("test", "test")` and `RegionEndpoint.USEast1` (no network
on construction). It overrides `SendEmailAsync` to record the request and either return a canned
`SendEmailResponse { HttpStatusCode = OK, MessageId = "msg-123" }` or throw a configured exception.
A `FakeSesClientFactory : ISesClientFactory` counts `Create` calls.

## Path Conventions

- Backend: `HOAManagementCompany/` (`Infrastructure/Payments/Alerts`, `Infrastructure/Configuration`, `Features/Payments`)
- Backend tests: `HOAManagementCompany.Tests/` (`Unit/Alerts`, `Unit/Configuration`, `Integration/Sandbox`, `Integration/Configuration`, `Fixtures`)
- CI: `.github/workflows/test.yml`

---

## Phase 1: Setup

**Purpose**: Add the SES SDK alongside SendGrid so the solution compiles throughout. SendGrid is removed in US4.

- [ ] T001 Add `<PackageReference Include="AWSSDK.SimpleEmailV2" Version="3.7.509.10" />` next to the existing `AWSSDK.S3` reference in `HOAManagementCompany/HOAManagementCompany.csproj`, then run `dotnet restore` and `dotnet build` from the repo root. Confirm there's no `AWSSDK.Core` version conflict (both packages require `[3.7.501.1, 4.0.0)`, research R1).
- [ ] T002 [P] Create the empty folder for the new unit tests, `HOAManagementCompany.Tests/Unit/Alerts/`. It gets populated by T008, T010 and T019.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Validated `SesOptions` and the client-factory seam that every story builds on.

**⚠️ CRITICAL**: No user story work starts until this phase is done.

- [ ] T003 Add `public sealed class SesOptions` to `HOAManagementCompany/Features/Payments/PaymentOptions.cs`, below `SendGridOptions`. Use `SectionName = "Ses"` and these properties, all with XML doc comments: `Region` (`""`), `FromEmail` (`""`), `FromName` (`"NekoHOA"`), `AccessKeyId` (`""`), `SecretAccessKey` (`""`), `SimulatorOnly` (`false`). `IsConfigured => !IsNullOrWhiteSpace(Region) && !IsNullOrWhiteSpace(FromEmail)`. The `SimulatorOnly` doc comment must say it's the only no-deliver guardrail, is false in production, and that the Stage-2 harness refuses to send unless it's true (data-model.md).
- [ ] T004 [P] Create `HOAManagementCompany.Tests/Unit/Configuration/SesOptionsValidatorTests.cs` (red first). Add a `[Theory]` over `(region, fromEmail, accessKeyId, secretAccessKey, expectedValid)` covering:
  - all blank → valid
  - `us-east-1` + `no-reply@mail.nekohoa.com` with no keys → valid
  - same with both keys → valid
  - region only → invalid
  - from only → invalid
  - `not-a-region` + valid from → invalid
  - valid region + `not-an-email` → invalid
  - access key without secret → invalid
  - secret without access key → invalid
  - keys only, no region or from → invalid

  Add a `[Fact]` that a config with region and from missing reports **two** separate errors whose messages contain `Ses:Region` and `Ses:FromEmail`. Add a `[Fact]` that with `AccessKeyId = "AKIASECRETVALUE1"` and no secret, no error message contains `AKIASECRETVALUE1` (FR-009, US3-5).
- [ ] T005 Create `HOAManagementCompany/Infrastructure/Configuration/SesOptionsValidator.cs` (`AbstractValidator<SesOptions>`), modeled on `SendGridOptionsValidator.cs`. Apply the rules only when "in use", meaning any of Region, FromEmail, AccessKeyId or SecretAccessKey is non-blank:
  - Region is required.
  - Region must be in `Amazon.RegionEndpoint.EnumerableAllRegions` by `SystemName`, checked only when non-blank.
  - FromEmail is required, and `.EmailAddress()` when non-blank.
  - AccessKeyId and SecretAccessKey must both be blank or both set.

  Use the exact messages in data-model.md, which name settings and never values. T004 must pass.
- [ ] T006 In `HOAManagementCompany/Program.cs`, add `builder.Services.AddValidatedOptions<SesOptions, SesOptionsValidator>(builder.Configuration, SesOptions.SectionName);` right after the SendGrid `AddValidatedOptions` line (around line 194). The SendGrid line is removed in T030.
- [ ] T007 [P] Create `HOAManagementCompany/Infrastructure/Payments/Alerts/SesClientFactory.cs` containing:
  - `public interface ISesClientFactory { IAmazonSimpleEmailServiceV2 Create(SesOptions options); }`
  - `[ExcludeFromCodeCoverage] public sealed class SesClientFactory : ISesClientFactory`, which returns `new AmazonSimpleEmailServiceV2Client(new BasicAWSCredentials(o.AccessKeyId, o.SecretAccessKey), RegionEndpoint.GetBySystemName(o.Region))` when both keys are non-blank, and otherwise `new AmazonSimpleEmailServiceV2Client(RegionEndpoint.GetBySystemName(o.Region))` (default credential chain).

  Add an XML comment saying it's a thin SDK adapter excluded from coverage like `StripeGateway` (research R3, R6). Register it with `builder.Services.AddSingleton<ISesClientFactory, SesClientFactory>();` in `HOAManagementCompany/Program.cs` next to the alert-provider registrations (around line 387).

**Checkpoint**: `dotnet build` passes and `dotnet test --filter "FullyQualifiedName~SesOptionsValidatorTests"` is green.

---

## Phase 3: User Story 1 - Main-branch releases flow again (Priority: P1) 🎯 MVP

**Goal**: The `email` channel is served by SES. The main-push sandbox stage sends to the SES success
simulator and passes, which unblocks Push Docker Image and Deploy to Dev.

**Independent Test**: With the four `SES_*` secrets set, a push to `main` shows `SesSandboxTests`
Passed (not Skipped), a green `integration-sandbox` job, and `docker-push` and `deploy-dev` running.

### Tests for User Story 1

- [ ] T008 [P] [US1] Create `HOAManagementCompany.Tests/Unit/Alerts/SesErrorMapperTests.cs` (red first).
  - A `[Theory]` asserting that each of these maps to a reason starting with `SesErrorMapper.UnavailablePrefix`:
    - `TooManyRequestsException`
    - `LimitExceededException`
    - `AmazonSimpleEmailServiceV2Exception` with `StatusCode = HttpStatusCode.ServiceUnavailable`
    - `HttpRequestException`
    - `SocketException`
    - `TimeoutException`
    - `TaskCanceledException`
    - `OperationCanceledException`
  - A `[Theory]` asserting that each of these starts with `SesErrorMapper.RejectedPrefix`:
    - `MessageRejectedException`
    - `MailFromDomainNotVerifiedException`
    - `BadRequestException`
    - `NotFoundException`
    - `AccountSuspendedException`
    - `SendingPausedException`
    - `AmazonSimpleEmailServiceV2Exception` with `StatusCode = Forbidden`
    - `InvalidOperationException`
  - A `[Fact]` that an exception whose `Message` is `"denied for resident@nekohoa.dev with key AKIASECRETVALUE1"` produces a reason containing neither `resident@nekohoa.dev` nor `AKIASECRETVALUE1`, but containing the exception type name (FR-009).
- [ ] T009 [US1] Create `HOAManagementCompany/Infrastructure/Payments/Alerts/SesErrorMapper.cs`:
  - `public static class SesErrorMapper` with `public const string UnavailablePrefix = "SES unavailable:"` and `public const string RejectedPrefix = "SES rejected:"`.
  - `public static string Map(Exception ex)` returns `$"{prefix} {ex.GetType().Name}"`, adding `$" (HTTP {(int)status})"` for `AmazonServiceException`. Use the classification table in research R5. It must **never** read `ex.Message`.

  T008 must pass.
- [ ] T010 [P] [US1] Create `HOAManagementCompany.Tests/Unit/Alerts/SesEmailProviderTests.cs` (red first), using the `FakeSesClient`/`FakeSesClientFactory` described at the top of this file. Cover:
  - (a) Not configured (blank Region): `IsConfigured == false`, `SendAsync` returns `Success == false` with `Error == "SES is not configured."`, and factory `Create` count is 0 (FR-004).
  - (b) Configured, `SimulatorOnly = false`, target `resident@nekohoa.dev`: `Success == true`, `ProviderMessageId == "msg-123"`, factory count is 1. The recorded request has `FromEmailAddress == "\"NekoHOA\" <no-reply@mail.nekohoa.com>"`, `Destination.ToAddresses == ["resident@nekohoa.dev"]`, the given subject in `Content.Simple.Subject.Data`, the body in `Content.Simple.Body.Text.Data`, and `Content.Simple.Body.Html == null` (FR-001, FR-003, research R2, **US2-4**: guard off by default lets delivery proceed).
  - (c) `Subject == null` sends `"NekoHOA payment alert"` (FR-003).
  - (d) The fake throws `MessageRejectedException`: `Success == false`, `Error` starts with `SES rejected:`, no exception escapes (FR-002).
  - (e) The fake throws `TooManyRequestsException`: `Error` starts with `SES unavailable:`.
  - (f) The fake returns `HttpStatusCode.BadRequest` without throwing: `Success == false` with `Error` starting `SES rejected:`.
  - (g) `Channel == "email"`.
  - (h) Two sends call `Create` exactly once (the client is cached).
- [ ] T011 [US1] Create `HOAManagementCompany/Infrastructure/Payments/Alerts/SesEmailProvider.cs` per `contracts/ses-email-adapter.md`: `public sealed class SesEmailProvider(IOptions<SesOptions> options, ISesClientFactory clientFactory, ILogger<SesEmailProvider> logger) : IAlertProvider`.
  - Keep a lazily created, cached client (`Lazy<IAmazonSimpleEmailServiceV2>` or a null-check field).
  - Implement steps 1, 3, 4 and 5 of the contract (step 2, the guard, comes in T022).
  - Log failures with `logger.LogWarning("SES email send failed: {Reason}", reason)`. **Do not** pass the exception object (contract invariant).
  - Don't mark it `[ExcludeFromCodeCoverage]`.
  - Add the `// <!-- REPOWISE:START domain=payments-alerts -->` … `// <!-- REPOWISE:END -->` region around the request-building block, with a short comment that this is the Stage-2 no-deliver seam (content finalized in T026).

  T010 (a)–(h) must pass.
- [ ] T012 [US1] In `HOAManagementCompany/Program.cs`, replace `builder.Services.AddSingleton<…IAlertProvider, …SendGridEmailProvider>();` (around line 389) with `builder.Services.AddSingleton<HOAManagementCompany.Infrastructure.Payments.Alerts.IAlertProvider, HOAManagementCompany.Infrastructure.Payments.Alerts.SesEmailProvider>();`. Keep `TwilioSmsProvider` registered. Confirm `AuthService`'s `IAuthNotifier` factory still resolves the email provider by `Channel == "email"`; no logic change is needed there.
- [ ] T013 [P] [US1] In `HOAManagementCompany.Tests/Fixtures/SandboxResult.cs`, add `public static void SkipIfUnavailable(AlertSendResult result)`: if `!result.Success && result.Error?.StartsWith(SesErrorMapper.UnavailablePrefix, StringComparison.Ordinal) == true`, throw `new SkipException($"provider unavailable: {result.Error}")`. In the class doc comment, change "The SendGrid and Twilio adapters swallow exceptions" to "The SES and Twilio adapters swallow exceptions" and add that SES outages are classified via `SkipIfUnavailable` (research R5).
- [ ] T014 [US1] In `HOAManagementCompany.Tests/Fixtures/SandboxIntegrationTestBase.cs`:
  - `ExtraConfiguration()`: add `["Ses:Region"] = Env("Ses__Region")`, `["Ses:FromEmail"] = Env("Ses__FromEmail")`, `["Ses:AccessKeyId"] = Env("Ses__AccessKeyId")`, `["Ses:SecretAccessKey"] = Env("Ses__SecretAccessKey")`, `["Ses:FromName"] = "NekoHOA Sandbox"`, and `["Ses:SimulatorOnly"] = Env("Ses__SimulatorOnly") ?? "true"`, with a comment that it defaults on as the only no-deliver guardrail. Keep the SendGrid entries for now; they're removed in T031.
  - Add `protected void RequireSes()`: `Skip.If` any of `Ses:Region`, `Ses:FromEmail`, `Ses:AccessKeyId` or `Ses:SecretAccessKey` is blank, with the message `"SES not configured"`. The hard-fail half is added in T024.
- [ ] T015 [US1] Create `HOAManagementCompany.Tests/Integration/Sandbox/SesSandboxTests.cs`: `[Trait("Category", "Sandbox")] public class SesSandboxTests : SandboxIntegrationTestBase`, using `[SkippableFact]`. Resolve the provider with `Services.GetServices<IAlertProvider>().Single(p => p.Channel == "email")`. Each test calls `RequireSes()` first and wraps the send in `SandboxResult.RunAsync`, then calls `SandboxResult.SkipIfUnavailable(result)` before asserting.
  - `Simulator_success_send_is_accepted`: target `success@simulator.amazonses.com`; assert `Success` (message is `result.Error`) and `ProviderMessageId` is not empty (US1-1, FR-011a).
  - `Malformed_sender_is_reported_as_a_handled_failure`: build `new SesEmailProvider(Options.Create(new SesOptions { Region, AccessKeyId, SecretAccessKey from config, FromEmail = "not-a-valid-email-address", FromName = "Stage 2 Sandbox", SimulatorOnly = true }), new SesClientFactory(), logger)`; target the success simulator; assert `!Success` and `Error` starts with `SesErrorMapper.RejectedPrefix` (US1-2, FR-011b).
  - `Invalid_credentials_are_a_rejection_not_an_outage`: same manual construction but with `AccessKeyId = "AKIAINVALIDINVALID00"` and `SecretAccessKey = "invalid"` and the real region and from; assert `!Success` and `Error` starts with `RejectedPrefix`. **Don't** call `SkipIfUnavailable`, so this proves invalid credentials Fail and block release (US1-4, FR-012).
  - `Bounce_simulator_send_is_accepted`: target `bounce@simulator.amazonses.com`; assert `Success` (the bounce arrives asynchronously; spec Edge Cases).
- [ ] T016 [P] [US1] Create `HOAManagementCompany.Tests/Integration/Sandbox/SesHarnessTests.cs`. **No** `Sandbox` trait, so it runs in the PR `test` job. Add a nested class deriving `SandboxIntegrationTestBase` that overrides `ExtraConfiguration()` to return the base entries with `Ses:Region`, `Ses:FromEmail`, `Ses:AccessKeyId` and `Ses:SecretAccessKey` set to `""`. Add a `[Fact]` asserting `Assert.Throws<SkipException>(() => RequireSes())`, which proves a missing secret skips instead of failing (US1-3, FR-012). Expose `RequireSes` through a public wrapper on the nested class.
- [ ] T017 [US1] In `.github/workflows/test.yml`, job `integration-sandbox`, step "Sandbox integration tests", add `Ses__Region: ${{ secrets.SES_REGION }}`, `Ses__FromEmail: ${{ secrets.SES_FROM_EMAIL }}`, `Ses__AccessKeyId: ${{ secrets.SES_ACCESS_KEY_ID }}`, `Ses__SecretAccessKey: ${{ secrets.SES_SECRET_ACCESS_KEY }}` and `Ses__SimulatorOnly: 'true'` to `env:`. Don't use the `AWS_*` names (research R8). Leave the `SendGrid__*` lines for T032.
- [ ] T018 [US1] Run `dotnet build` and `dotnet test --filter "Category!=Sandbox"` from the repo root; all tests pass. If SES credentials are available locally (quickstart.md "Local run"), also run `dotnet test --filter "FullyQualifiedName~SesSandboxTests"` and confirm 4 Passed. Otherwise confirm they report Skipped "SES not configured".

**Checkpoint**: The email channel runs on SES, the sandbox suite exercises the real SES API, and CI is wired to the new secrets.

---

## Phase 4: User Story 2 - Real email can never leak from automated tests (Priority: P1)

**Goal**: With `Ses:SimulatorOnly = true`, any non-simulator recipient is refused before any network
call, and the Stage-2 harness refuses to run SES tests with the guard off.

**Independent Test**: The unit test shows that sending to `resident@nekohoa.dev` with the guard on
returns a guard failure with factory `Create` count 0. The harness test shows that the guard off
with credentials present throws `InvalidOperationException`.

### Tests for User Story 2

- [ ] T019 [P] [US2] Create `HOAManagementCompany.Tests/Unit/Alerts/SimulatorRecipientGuardTests.cs` (red first): a `[Theory]` over every row of the guard table in data-model.md:
  - allowed: `success@simulator.amazonses.com`, `"  SUCCESS@Simulator.AmazonSES.com "`, `bounce@simulator.amazonses.com`
  - refused: `resident@nekohoa.dev`, `x@simulator.amazonses.com.evil.test`, `x@evilsimulator.amazonses.com`, `simulator.amazonses.com`, `""`, `null`
  - also refused: `a@b@simulator.amazonses.com.evil`
- [ ] T020 [US2] Create `HOAManagementCompany/Infrastructure/Payments/Alerts/SimulatorRecipientGuard.cs`: `public static class SimulatorRecipientGuard` with `public const string SimulatorDomain = "simulator.amazonses.com"` and `public static bool IsAllowed(string? target)`. It trims, returns false if null or empty or there's no `@`, takes the substring after `LastIndexOf('@')`, and compares it to `SimulatorDomain` with `StringComparison.OrdinalIgnoreCase` equality (never `EndsWith`/`Contains`). T019 must pass.
- [ ] T021 [P] [US2] Extend `HOAManagementCompany.Tests/Unit/Alerts/SesEmailProviderTests.cs` (red first):
  - (i) Configured with `SimulatorOnly = true`, target `resident@nekohoa.dev`: `Success == false`, `Error` contains `SimulatorOnly`, `Error` doesn't contain `resident@nekohoa.dev`, factory `Create` count is 0 (US2-1, FR-005, FR-011c).
  - (j) Configured with `SimulatorOnly = true`, target `success@simulator.amazonses.com`: `Success == true`, factory count 1 (US2-2).
- [ ] T022 [US2] In `HOAManagementCompany/Infrastructure/Payments/Alerts/SesEmailProvider.cs`, add contract step 2 after the configured check and **before** any `clientFactory.Create` or cached-client access: `if (_options.SimulatorOnly && !SimulatorRecipientGuard.IsAllowed(message.Target)) return AlertSendResult.Fail("SES SimulatorOnly guard refused a non-simulator recipient.");`. T021 (i)–(j) and all of T010 must pass.
- [ ] T023 [P] [US2] Extend `HOAManagementCompany.Tests/Integration/Sandbox/SesHarnessTests.cs` (red first). Add a nested harness whose `ExtraConfiguration()` sets non-blank `Ses:Region = "us-east-1"`, `Ses:FromEmail = "no-reply@mail.nekohoa.com"`, `Ses:AccessKeyId = "AKIATESTTESTTEST0000"`, `Ses:SecretAccessKey = "test"` and `Ses:SimulatorOnly = "false"`. Add a `[Fact]` asserting `Assert.Throws<InvalidOperationException>(() => RequireSes())` with a message containing `Ses:SimulatorOnly must be true` (US2-3, FR-006). No network call happens because `RequireSes` throws first.
- [ ] T024 [US2] In `HOAManagementCompany.Tests/Fixtures/SandboxIntegrationTestBase.cs`, extend `RequireSes()`: after the skip check, `if (!Config.GetValue<bool>("Ses:SimulatorOnly")) throw new InvalidOperationException("Refusing to send: Ses:SimulatorOnly must be true in Stage 2 (sole no-deliver guardrail).");`. Add an XML doc comment: SES keys have no test/live form, so `SimulatorOnly` is the only guardrail (FR-009). T023 must pass.
- [ ] T025 [US2] Add `Guard_refuses_real_recipient_without_calling_ses` to `HOAManagementCompany.Tests/Integration/Sandbox/SesSandboxTests.cs`: `RequireSes()`, send to `resident@nekohoa.dev` through the DI provider, and assert `!Success` and that `Error` contains `SimulatorOnly` (US2-1 end-to-end with real config, FR-011c).
- [ ] T026 [US2] Finalize the `domain=payments-alerts` Repowise region in `HOAManagementCompany/Infrastructure/Payments/Alerts/SesEmailProvider.cs`. It should explain that `SimulatorOnly` restricts recipients to the SES mailbox simulator and is refused before any network call, that it's the only no-deliver guardrail for email (SES keys have no test/live form), and that it defaults off so production delivers normally. Run the project's Repowise workflow if available.

**Checkpoint**: The no-deliver guarantee is unit-proven and enforced by the harness. US1 and US2 together are the MVP.

---

## Phase 5: User Story 3 - Misconfiguration is caught at startup (Priority: P2)

**Goal**: A partial or invalid `Ses` config aborts startup with one message per problem; an empty config starts with email disabled.

**Independent Test**: `StartupValidationTests` shows that a partial `Ses` config aborts and an empty config starts.

**Note**: The validator and its unit tests landed in Foundational (T004–T006) because every story binds `SesOptions`. This phase proves the startup behavior end to end.

### Tests for User Story 3

- [ ] T027 [US3] In `HOAManagementCompany.Tests/Integration/Configuration/StartupValidationTests.cs`, add `["Ses:Region"] = ""`, `["Ses:FromEmail"] = ""`, `["Ses:FromName"] = ""`, `["Ses:AccessKeyId"] = ""` and `["Ses:SecretAccessKey"] = ""` to the blanked optional-provider dictionary (around line 49), keeping the comment about developer-local `appsettings.Secrets.json`. Confirm the existing "valid config starts" test still passes with SES fully empty (US3-1).
- [ ] T028 [US3] Add tests to `HOAManagementCompany.Tests/Integration/Configuration/StartupValidationTests.cs`, following the existing abort-on-startup pattern there:
  - `Partial_ses_config_aborts_startup`: `Ses:Region = "us-east-1"` only. Assert startup throws, and the aggregated message contains `Ses:FromEmail is required` (US3-2).
  - `Invalid_ses_sender_aborts_startup`: region set and `Ses:FromEmail = "not-an-email"`. Assert the message contains `Ses:FromEmail must be a valid email address` (US3-3).
  - `Unpaired_ses_credentials_abort_startup`: region and from set, `Ses:AccessKeyId = "AKIASECRETVALUE1"`, secret blank. Assert the message contains `must be set together` **and doesn't contain** `AKIASECRETVALUE1` (US3-4, US3-5).
- [ ] T029 [US3] Run `dotnet test --filter "FullyQualifiedName~StartupValidationTests|FullyQualifiedName~SesOptionsValidatorTests"`; all green.

**Checkpoint**: Fail-fast configuration is proven at the host level.

---

## Phase 6: User Story 4 - SendGrid is fully removed and the owner knows what to set up (Priority: P2)

**Goal**: No SendGrid code, packages, config, tests or CI references remain. The docs and spec corpus name SES. The owner checklist exists.

**Independent Test**: `grep -rni sendgrid HOAManagementCompany HOAManagementCompany.Tests .github` returns nothing (SC-006).

### Implementation for User Story 4

- [ ] T030 [US4] Delete `HOAManagementCompany/Infrastructure/Payments/Alerts/SendGridEmailProvider.cs` and `HOAManagementCompany/Infrastructure/Configuration/SendGridOptionsValidator.cs`. Remove `SendGridOptions` from `HOAManagementCompany/Features/Payments/PaymentOptions.cs`. In `HOAManagementCompany/Program.cs`, remove the `AddValidatedOptions<SendGridOptions, SendGridOptionsValidator>` line, and in the `IAuthNotifier` block (around lines 344–346) change "SendGrid email when configured … where no SendGrid credentials exist" to "SES email when configured … where no SES credentials exist". Remove the `SendGrid` and `SendGrid.Extensions.DependencyInjection` `PackageReference`s from `HOAManagementCompany/HOAManagementCompany.csproj`.
- [ ] T031 [US4] In `HOAManagementCompany.Tests/Fixtures/SandboxIntegrationTestBase.cs`, remove the three `SendGrid:*` `ExtraConfiguration` entries and `RequireSendGrid()`. Delete `HOAManagementCompany.Tests/Integration/Sandbox/SendGridSandboxTests.cs`. In `HOAManagementCompany.Tests/Integration/Configuration/StartupValidationTests.cs`, remove the three `SendGrid:*` blank entries. In `HOAManagementCompany.Tests/Fixtures/AlertTestBase.cs`, change the doc comment "real Twilio/SendGrid providers" to "real Twilio/SES providers".
- [ ] T032 [US4] Rename `HOAManagementCompany.Tests/Unit/Configuration/TwilioSendGridOptionsValidatorTests.cs` to `TwilioOptionsValidatorTests.cs` (class `TwilioOptionsValidatorTests`) and delete its SendGrid theory and field; the SES cases live in `SesOptionsValidatorTests.cs` from T004. In `.github/workflows/test.yml`, remove the `SendGrid__ApiKey`, `SendGrid__FromEmail` and `SendGrid__Sandbox` env lines from `integration-sandbox`, and change the comments at around line 38 ("no real Stripe/SendGrid/Twilio calls") and around line 159 ("REAL Stripe/SendGrid/Twilio adapters") to say SES.
- [ ] T033 [P] [US4] In `HOAManagementCompany/appsettings.json`, replace the `"SendGrid": { "_comment": … }` block with `"Ses": { "_comment": "Optional — email is disabled when Region and FromEmail are absent. Set Ses__Region, Ses__FromEmail, and optionally Ses__AccessKeyId/Ses__SecretAccessKey (otherwise the AWS default credential chain). Ses__SimulatorOnly=true restricts recipients to the SES mailbox simulator (tests only)." }`. In `HOAManagementCompany/Features/Auth/AuthNotifier.cs`, change the comment "Email delivery via the SendGrid alert provider" to "Email delivery via the SES alert provider".
- [ ] T034 [P] [US4] Update `HOAManagementCompany.Tests/Integration/Sandbox/README.md` per `contracts/ci-sandbox-ses.md`:
  - change "Stripe / SendGrid / Twilio" to "Stripe / SES / Twilio"
  - change `RequireSendGrid()` to `RequireSes()`
  - replace the SendGrid safety invariant with: "**SES**: never sends unless `Ses:SimulatorOnly == true`, which restricts recipients to `@simulator.amazonses.com` (the only no-deliver guarantee; SES keys have no test/live form)"
  - add rule 6: "Call `SandboxResult.SkipIfUnavailable(result)` on SES results so outages Skip rather than Fail"
  - add a link to `../../../specs/026-ses-email-provider/quickstart.md` for SES account setup (FR-014, FR-015)
- [ ] T035 [P] [US4] Reconcile older specs (FR-016, research R10). Only the `spec.md` files change.
  - In `specs/006-stripe-payments/spec.md` (around line 575), change "Twilio (SMS) + Twilio SendGrid (email)" to "Twilio (SMS) + Amazon SES (email; superseded SendGrid per spec 026)".
  - In `specs/007-integration-ci-tests/spec.md`, change every reference to SendGrid credentials or SendGrid sandbox mode (Input line, Clarifications line 15, and any FR/SC/story text found with `grep -n -i sendgrid`) to SES / the SES mailbox simulator, with "(superseded by 026)" noted once.
  - In `specs/013-ephemeral-pr-envs/spec.md` (lines around 25, 89 and 111), change "SendGrid sandbox" to "SES mailbox simulator (spec 026)".
- [ ] T036 [US4] Verify removal (SC-006). `grep -rni sendgrid HOAManagementCompany HOAManagementCompany.Tests .github --include=*.cs --include=*.csproj --include=*.json --include=*.yml --include=*.md` must return nothing (excluding `bin/` and `obj/`). Then run `dotnet build` (no warnings about missing SendGrid types) and `dotnet test --filter "Category!=Sandbox"`; all green.

**Checkpoint**: SendGrid is gone and the spec corpus is consistent.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T037 Coverage check (constitution §9). Run `dotnet test --filter "Category!=Sandbox" --collect:"XPlat Code Coverage"` and confirm ≥95% line coverage for `SesEmailProvider.cs`, `SimulatorRecipientGuard.cs`, `SesErrorMapper.cs` and `SesOptionsValidator.cs`. `SesClientFactory` is excluded. Add test cases for any uncovered branches.
- [ ] T038 [P] Adversarial secrets review. Read every new or changed `LogWarning`/`LogError` and `AlertSendResult.Fail` call in `SesEmailProvider.cs`, `SesErrorMapper.cs`, `SesOptionsValidator.cs` and `SandboxResult.cs`, and confirm none interpolates `ex.Message`, `AccessKeyId`, `SecretAccessKey`, `message.Target` or `message.Body` (FR-009).
- [ ] T039 [P] Executable-spec audit (CLAUDE.md NLT rule). For each acceptance scenario and Independent Test in `specs/026-ses-email-provider/spec.md`, confirm the mapped test exists and asserts the stated **Then**:

  | Scenario | Test |
  |---|---|
  | US1-1 | T015 `Simulator_success_send_is_accepted` |
  | US1-2 | T015 `Malformed_sender_…` |
  | US1-3 | T016 harness skip |
  | US1-4 | T015 `Invalid_credentials_…` |
  | US2-1 | T021(i), T025 |
  | US2-2 | T021(j) |
  | US2-3 | T023 |
  | US2-4 | T010(b) |
  | US3-1 | T027 |
  | US3-2 | T028 |
  | US3-3 | T028 |
  | US3-4 | T028 |
  | US3-5 | T004, T028 |
  | US4-1 | T036 grep |
  | US4-2, US4-3 | review of quickstart.md / T035 |

  Update `spec.md` if any scenario wording drifted from the implementation.
- [ ] T040 Mark completed tasks `[X]` in `specs/026-ses-email-provider/tasks.md`, set `**Status**: Implemented` in `specs/026-ses-email-provider/spec.md`, and run the full local suite one more time (`dotnet build && dotnet test --filter "Category!=Sandbox"`) before pushing.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none.
- **Foundational (Phase 2)**: depends on T001; blocks every story.
- **US1 (Phase 3)**: depends on Phase 2.
- **US2 (Phase 4)**: depends on T011 (adapter exists) and T014 (`RequireSes` exists). T019/T020 (pure guard) can start right after Phase 2.
- **US3 (Phase 5)**: depends on Phase 2 only. It can run in parallel with US1 and US2.
- **US4 (Phase 6)**: depends on T012 (SES registered in place of SendGrid) and T015 (SES sandbox tests replace SendGrid's). T033–T035 can start any time.
- **Polish (Phase 7)**: depends on all stories.

### User Story Dependencies

- **US1 (P1)**: depends only on Foundational.
- **US2 (P1)**: builds on US1's adapter and harness. Ship US1 and US2 together; US1 alone already sends only to simulator addresses because the tests target them, but the guarantee isn't enforced until US2.
- **US3 (P2)**: independent of US1 and US2.
- **US4 (P2)**: removal must come after US1 has replaced SendGrid's registration and tests.

### Cross-Spec Dependencies

- None. This spec is independently completable (spec.md → Spec independence). It amends the
  006, 007 and 013 `spec.md` files for consistency only (T035).

### Within Each User Story

- The test task is written and failing before its paired implementation (see the red→green pairs above).
- Pure logic (mapper, guard) comes before the adapter, the adapter before DI, and DI before the sandbox tests and CI wiring.

### Parallel Opportunities

- T002 ∥ T001
- T004 ∥ T007 (different files)
- T008 ∥ T010 ∥ T013 ∥ T016 (different test and fixture files)
- T019 ∥ T021 ∥ T023 (different test files)
- US3 (T027–T029) ∥ US1/US2
- T033 ∥ T034 ∥ T035
- T038 ∥ T039

---

## Parallel Example: User Story 1

```bash
# Write the US1 tests together (all red):
Task: "T008 SesErrorMapperTests in HOAManagementCompany.Tests/Unit/Alerts/SesErrorMapperTests.cs"
Task: "T010 SesEmailProviderTests in HOAManagementCompany.Tests/Unit/Alerts/SesEmailProviderTests.cs"
Task: "T016 SesHarnessTests (skip path) in HOAManagementCompany.Tests/Integration/Sandbox/SesHarnessTests.cs"
Task: "T013 SkipIfUnavailable in HOAManagementCompany.Tests/Fixtures/SandboxResult.cs"
# Then make them green in order: T009 → T011 → T012 → T014 → T015 → T017
```

---

## Implementation Strategy

### MVP First (US1 + US2)

1. Phase 1 → Phase 2 (validated options plus the factory seam).
2. Phase 3 (US1): SES adapter, DI swap, sandbox tests, CI env.
3. Phase 4 (US2): no-deliver guard plus the harness hard-fail.
4. **Stop and validate**: with the owner's `SES_*` secrets set, a main push goes green and deploys.
   This alone fixes the outage.

### Incremental Delivery

1. MVP (US1 + US2) → green releases resume.
2. US3 → startup-validation proof.
3. US4 → SendGrid removal, docs and spec reconciliation.
4. Polish → coverage, secrets review, NLT audit.

All of it ships in **one PR**, a focused single-concern vendor swap (spec Constitution → Quality gates).
The phases order the work within that PR.

---

## Notes

- [P] = different files and no dependency on an incomplete task.
- The owner's manual SES steps (quickstart.md) aren't repo tasks. Until they're done, the SES sandbox tests **skip**, and that's expected (FR-012).
- After merge, the owner deletes `SENDGRID_API_KEY` and `SENDGRID_FROM_EMAIL` (quickstart §7).
- Never skip, disable or quarantine a test to get green.
