# Stage 2 — Provider Sandbox Tests (007)

These tests run the **real** Stripe / SES / Twilio adapters against each provider's
test/sandbox mode. They catch integration regressions (webhook signature, payment flow,
email payload, SMS formatting) that the mocked PR suite cannot — with **zero** real charges,
emails, or SMS.

## Trait gate

Every test class here carries `[Trait("Category", "Sandbox")]`.

- The PR / unit `test` job runs `dotnet test --filter "Category!=Sandbox"` — these never run on PRs
  and require no provider secrets.
- The `integration-sandbox` CI job (push to `main` only) runs `dotnet test --filter "Category=Sandbox"`
  with test-scoped secrets, and gates `docker-push`.

## Rules for tests in this folder

1. Derive from `SandboxIntegrationTestBase` (keeps the real adapters; loads test-mode secrets).
2. Call the matching `RequireStripe()` / `RequireSes()` / `RequireTwilio()` **first**. It
   skips (not fails) when the secret is missing, and hard-fails if a non-test credential is supplied.
3. Wrap every provider call in `SandboxResult.RunAsync(...)` so a provider **outage** classifies as
   Skipped (does not block deploy) while a real **regression** Fails (blocks `docker-push`).
4. Assert only against objects this run created (the sandbox accounts are shared — Clarifications Q4).
5. Use `[SkippableFact]` / `[SkippableTheory]` (not `[Fact]` / `[Theory]`) so dynamic skips are honored.
6. Call `SandboxResult.SkipIfUnavailable(result)` on SES results — the adapter swallows exceptions,
   so this is how an SES outage Skips rather than Fails (026).

## Safety invariants (do not regress)

- **SES**: never sends unless `Ses:SimulatorOnly == true`, which restricts recipients to
  `@simulator.amazonses.com` (the only no-deliver guarantee; SES keys have no test/live form).
  `RequireSes()` hard-fails if credentials are present with the guard off.
- **Twilio**: `From` must be the magic number `+15005550006` under test credentials.
- **Stripe**: the harness refuses any key that is not `sk_test_…` / `rk_test_…`.

## SES account setup

The SES identity, send-only IAM user, and the `SES_*` CI secrets are set up by the repository owner —
see [`specs/026-ses-email-provider/quickstart.md`](../../../specs/026-ses-email-provider/quickstart.md).
Until those secrets exist, the SES tests skip (they do not block the release).
