# Contract: Provider-sandbox CI for SES (amends 007)

## Workflow: `.github/workflows/test.yml`, job `integration-sandbox`

Unchanged: main-push only (`github.ref == 'refs/heads/main' && github.event_name == 'push'`), runs
`dotnet test --filter "Category=Sandbox"`, and gates `docker-push`.

Env changes:

| Remove | Add |
|---|---|
| `SendGrid__ApiKey: ${{ secrets.SENDGRID_API_KEY }}` | `Ses__Region: ${{ secrets.SES_REGION }}` |
| `SendGrid__FromEmail: ${{ secrets.SENDGRID_FROM_EMAIL }}` | `Ses__FromEmail: ${{ secrets.SES_FROM_EMAIL }}` |
| `SendGrid__Sandbox: true` | `Ses__AccessKeyId: ${{ secrets.SES_ACCESS_KEY_ID }}` |
| | `Ses__SecretAccessKey: ${{ secrets.SES_SECRET_ACCESS_KEY }}` |
| | `Ses__SimulatorOnly: 'true'` |

Update the comment at `test.yml:38` ("no real Stripe/SendGrid/Twilio calls") to name SES.

## Harness: `SandboxIntegrationTestBase`

- `ExtraConfiguration()` maps the env vars `Ses__Region`, `Ses__FromEmail`, `Ses__AccessKeyId` and
  `Ses__SecretAccessKey` (trimmed; whitespace-only counts as absent), plus
  `Ses:FromName = "NekoHOA Sandbox"` and `Ses:SimulatorOnly = Env("Ses__SimulatorOnly") ?? "true"`.
- `RequireSes()` replaces `RequireSendGrid()`:
  - **Skip** if any of Region, FromEmail, AccessKeyId or SecretAccessKey is blank
    (`"SES not configured"`).
  - **Hard-fail** (`InvalidOperationException`) if `Ses:SimulatorOnly` is not `true`:
    `"Refusing to send: Ses:SimulatorOnly must be true in Stage 2 (sole no-deliver guardrail)."`

## Classifier: `SandboxResult`

- Add `SkipIfUnavailable(AlertSendResult r)`: if `!r.Success` and `r.Error` starts with
  `SesErrorMapper.UnavailablePrefix`, throw `SkipException("provider unavailable: …")`.
  Otherwise do nothing.
- Update the doc comment: replace "SendGrid and Twilio adapters" with "SES and Twilio adapters".

## Tests: `HOAManagementCompany.Tests/Integration/Sandbox/SesSandboxTests.cs` (replaces `SendGridSandboxTests.cs`)

`[Trait("Category", "Sandbox")]`, derives from `SandboxIntegrationTestBase`, uses `[SkippableFact]`.
Each test calls `RequireSes()` first.

| Test | Arrange / Act | Assert | Spec |
|---|---|---|---|
| `Simulator_success_send_is_accepted` | send to `success@simulator.amazonses.com` through the DI email provider | `SkipIfUnavailable`; `Success == true`; `ProviderMessageId` not empty | US1-1, FR-011a |
| `Malformed_sender_is_reported_as_a_handled_failure` | provider built with `FromEmail = "not-a-valid-email-address"`, real keys, `SimulatorOnly = true`; send to the success simulator | `SkipIfUnavailable`; `Success == false`; `Error` starts with `SES rejected:` | US1-2, FR-011b |
| `Guard_refuses_real_recipient_without_calling_ses` | send to `resident@nekohoa.dev` through the DI provider | `Success == false`; `Error` mentions `SimulatorOnly` | US2-1, FR-011c |
| `Bounce_simulator_send_is_accepted` | send to `bounce@simulator.amazonses.com` | `SkipIfUnavailable`; `Success == true` (bounce is asynchronous; see the Edge Cases) | Edge case |

## README: `HOAManagementCompany.Tests/Integration/Sandbox/README.md`

- Replace `RequireSendGrid()` with `RequireSes()` and "SendGrid" with "SES" throughout.
- Replace the safety invariant with: **SES**: never sends unless `Ses:SimulatorOnly == true`, which
  restricts recipients to `@simulator.amazonses.com`. This is the only no-deliver guarantee, since
  SES keys have no test/live form.
- Link to `specs/026-ses-email-provider/quickstart.md` for account setup.
