# Research: Replace SendGrid with Amazon SES (026)

All Technical Context unknowns are resolved below. There are no open NEEDS CLARIFICATION items.

## R1. SDK and package

**Decision**: Use `AWSSDK.SimpleEmailV2` **3.7.509.10** (the SESv2 API), pinned exactly.

**Rationale**: The backend already references `AWSSDK.S3` 3.7.511.8 for R2/MinIO document storage.
Both packages depend on `AWSSDK.Core [3.7.501.1, 4.0.0)`, so they share one Core assembly with no
version conflict. SESv2 is the current SES API; v1 (`AWSSDK.SimpleEmail`) is legacy. The project
pins AWS packages exactly (S3 is pinned), so this follows suit.

**Alternatives considered**:
- `AWSSDK.SimpleEmailV2` 4.x: requires moving `AWSSDK.S3` to 4.x as well (Core 4.0). That's a
  breaking upgrade of document storage, unrelated to this fix. Rejected; it can be a separate change.
- SES SMTP interface through `System.Net.Mail`/MailKit: needs separate SMTP credentials derived from
  IAM keys and gives coarser errors. Rejected.
- Raw HTTP with SigV4: reinvents the SDK. Rejected.

## R2. Message shape

**Decision**: `SendEmailRequest` with `FromEmailAddress = "\"{FromName}\" <{FromEmail}>"`,
`Destination.ToAddresses = [message.Target]`, and `Content.Simple` with the UTF-8 `Subject` and a
**plain-text body only**. The default subject is `"NekoHOA payment alert"` when `Subject` is null,
the same as today.

**Rationale**: Every current body is plain text (payment alerts, the verification code, the claim
code; see `EmailAuthNotifier`). SendGrid sent the same string as both text and HTML. Sending it as
HTML adds nothing, and interpolated values could be read as markup. Plain text renders identically
in every client.

**Alternatives considered**: Text plus HTML (same string) to mirror SendGrid exactly. Rejected
because it carries a markup-injection risk and no benefit. Templated HTML email is a future feature.

## R3. Credentials and region

**Decision**: `Ses:Region` is required whenever SES is configured. If both `Ses:AccessKeyId` and
`Ses:SecretAccessKey` are set, the client uses `BasicAWSCredentials`. If both are blank, it uses
the SDK's default credential chain. The client is created once, lazily, and reused (singleton).
CI supplies static keys for a send-only IAM user (Clarifications 2026-10-06).

**Rationale**: Static keys match the clarified CI decision. The default-chain fallback costs
nothing and leaves room for keyless runtime auth later. Region is explicit, never inferred from
`AWS_REGION`, so a GitHub runner's environment can't silently redirect sends.

**Alternatives considered**: GitHub OIDC federation (rejected in Clarifications). Requiring keys
always (blocks future keyless auth for no gain).

## R4. No-deliver guard (replaces SendGrid's Sandbox flag)

**Decision**: Add a `Ses:SimulatorOnly` boolean (default `false`). When `true`, the adapter calls a
pure, unit-tested `SimulatorRecipientGuard.IsAllowed(target)` **before** creating or calling the
SES client. It trims the target, takes the part after the last `@`, and requires that domain to
equal `simulator.amazonses.com` (ordinal, case-insensitive). A refused send returns
`AlertSendResult.Fail("SES SimulatorOnly guard refused a non-simulator recipient.")`; the
recipient is never echoed. The sandbox harness's `RequireSes()` hard-fails if credentials are set
and `SimulatorOnly` is not `true`, and the harness injects `Ses:SimulatorOnly=true` by default.

**Rationale**: SES credentials have no test/live distinction, and SES has no per-request "don't
deliver" switch. The mailbox simulator is AWS's official no-delivery recipient set, and simulator
sends are accepted even while the account is in the SES sandbox. An exact domain match (not
`EndsWith` or `Contains`) rejects look-alikes such as `simulator.amazonses.com.evil.test` and
`x@evilsimulator.amazonses.com`.

**Alternatives considered**: Relying on the account staying in the SES sandbox (it's lifted once
production access is granted, so it's no guarantee). A separate AWS account for CI (stronger
isolation, but more owner setup). The guard plus a send-only IAM policy is enough at this scale.

## R5. Error mapping and outage-vs-regression classification

**Decision**: The adapter never throws, including on cancellation, the same as the SendGrid and
Twilio adapters, which catch every exception. A pure, unit-tested `SesErrorMapper.Map(Exception)` returns the failure
reason:

| Exception / condition | Reason prefix | Kind |
|---|---|---|
| `TooManyRequestsException`, `LimitExceededException`, `AmazonServiceException` with status ≥ 500, `HttpRequestException`, `SocketException`, `TimeoutException`, `TaskCanceledException`/`OperationCanceledException` | `SES unavailable:` | transient |
| `MessageRejectedException`, `MailFromDomainNotVerifiedException`, `BadRequestException`, `NotFoundException`, `AccountSuspendedException`, `SendingPausedException`, auth failures (`AmazonServiceException` 4xx), anything else | `SES rejected:` | permanent |

The reason is `"{prefix} {ExceptionTypeName} (HTTP {status})"` and **never** includes `ex.Message`,
which can echo the recipient address or the access key ID. The prefixes are public constants on
`SesErrorMapper`. In the sandbox suite, a new `SandboxResult.SkipIfUnavailable(AlertSendResult)`
helper turns a failed result whose `Error` starts with `SesErrorMapper.UnavailablePrefix` into
`SkipException`, so an SES outage is reported as Skipped, matching the existing FR-005 behavior for
Stripe and Twilio.

**Rationale**: The adapters swallow exceptions by design (outbox semantics: "a rejection is
terminal, never retried"). The current `SandboxResult` can classify only *thrown* exceptions, so
with SendGrid an outage actually showed up as Failed. A stable, constant-backed prefix lets the
sandbox suite tell outages from regressions without changing `AlertSendResult` or the outbox. The
dispatcher still treats every failure as terminal, as it does today.

**Alternatives considered**:
- Adding `bool Transient` to `AlertSendResult`: changes a shared type used by the outbox and Twilio
  for a test-only need. Rejected.
- Letting the adapter throw on transient errors: breaks the never-throw contract (FR-002) and the
  outbox. Rejected.

## R6. Testability seam

**Decision**: `SesEmailProvider` takes an `ISesClientFactory` (`IAmazonSimpleEmailServiceV2
Create(SesOptions)`). Production registers `SesClientFactory`, a thin adapter excluded from coverage
like `StripeGateway`. Unit tests pass a counting fake factory to prove FR-005/FR-011(c): with the
guard on and an ordinary recipient, the factory is never called. A not-configured provider also
never calls it (FR-004).

**Rationale**: The test project has no mocking library, and `IAmazonSimpleEmailServiceV2` is too big
to hand-fake. A one-method factory is trivial to fake and keeps the decision logic (configured
check, guard, request building, error mapping) in `SesEmailProvider`, which therefore stays **in**
coverage scope. The SDK call is the only line that needs the network; the sandbox tests cover it.
This beats today's SendGrid adapter, which is fully excluded from coverage.

**Alternatives considered**: Keeping the whole adapter `[ExcludeFromCodeCoverage]` like SendGrid.
Rejected: the guard is a safety invariant and must be unit-tested.

## R7. Configuration section and validation

**Decision**: Use a new `SesOptions` class (section `"Ses"`) with `Region`, `FromEmail`, `FromName`
(default `"NekoHOA"`), `AccessKeyId`, `SecretAccessKey` and `SimulatorOnly`.
`IsConfigured => Region and FromEmail are both non-blank`. `SesOptionsValidator` follows the 008
pattern:
- **Fully empty** (Region, FromEmail, AccessKeyId and SecretAccessKey all blank) is valid and
  disables email.
- **Otherwise**, each of these is reported separately: Region is required; Region must be a known
  AWS region system name (`RegionEndpoint.EnumerableAllRegions`); FromEmail is required; FromEmail
  must be a valid address; AccessKeyId and SecretAccessKey must be both set or both blank.
- Messages name the setting, never its value.

`FromName` and `SimulatorOnly` don't count toward "in use", like `SendGrid:FromName`/`Sandbox`
today. Registration uses `AddValidatedOptions<SesOptions, SesOptionsValidator>` in place of the
SendGrid line in `Program.cs`.

**Rationale**: This mirrors `SendGridOptionsValidator`/`TwilioOptionsValidator`, keeps the
fail-fast guarantee (constitution §8), and satisfies spec US3.

**Alternatives considered**: A provider-neutral `Email` section. Rejected: the settings are
SES-specific (region, IAM keys), and a neutral name would hide that.

## R8. CI secrets and workflow

**Decision**: The `integration-sandbox` job in `.github/workflows/test.yml` replaces its three
`SendGrid__*` env lines with:

```yaml
Ses__Region:          ${{ secrets.SES_REGION }}
Ses__FromEmail:       ${{ secrets.SES_FROM_EMAIL }}
Ses__AccessKeyId:     ${{ secrets.SES_ACCESS_KEY_ID }}
Ses__SecretAccessKey: ${{ secrets.SES_SECRET_ACCESS_KEY }}
Ses__SimulatorOnly:   'true'
```

The env names deliberately avoid `AWS_ACCESS_KEY_ID`, so the SDK's default chain never picks up CI
keys in another test context. The job stays main-push only, and PR jobs get no secrets. The old
`SENDGRID_API_KEY` and `SENDGRID_FROM_EMAIL` secrets are deleted by the owner after merge.

**Rationale**: Same pattern as the existing provider secrets. Keeping all four as secrets, even
though region and sender aren't sensitive, means one place to configure and matches the 007
checklist style.

## R9. Sending identity and SES account setup

**Decision**: Verify the domain identity `mail.nekohoa.com` with Easy DKIM (3 CNAME records added
in Cloudflare as **DNS only**, not proxied). The sender is `no-reply@mail.nekohoa.com`. Turn on the
account-level suppression list for bounces and complaints. The IAM user's policy allows only
`ses:SendEmail`, scoped to that identity's ARN. The documented default region is `us-east-1`. The
full owner checklist is in `quickstart.md`.

**Rationale**: Clarifications 2026-10-06 (subdomain reputation isolation). Proxied CNAMEs break
DKIM lookups. The suppression list covers bounce and complaint hygiene without SNS (out of scope).
Scoping to the identity ARN stops the keys from sending as any other identity in the account.

## R10. Spec-corpus reconciliation (FR-016)

**Decision**: Update the `spec.md` files that name SendGrid as the email provider:
- **006** (line ~575, "Twilio SendGrid (email)")
- **007** (Clarifications Q about shared SendGrid credentials, plus any FR/SC text naming the
  SendGrid sandbox)
- **013** (Clarifications plus FR-013, "SendGrid sandbox")

Each changes to SES and the mailbox simulator, with a "(superseded by 026)" note. Their plan,
research, tasks and contracts files are not refreshed (constitution §11).
