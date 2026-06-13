# Stage 2 — Provider Sandbox Tests (007)

These tests run the **real** Stripe / SendGrid / Twilio adapters against each provider's
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
2. Call the matching `RequireStripe()` / `RequireSendGrid()` / `RequireTwilio()` **first**. It
   skips (not fails) when the secret is missing, and hard-fails if a non-test credential is supplied.
3. Wrap every provider call in `SandboxResult.RunAsync(...)` so a provider **outage** classifies as
   Skipped (does not block deploy) while a real **regression** Fails (blocks `docker-push`).
4. Assert only against objects this run created (the sandbox accounts are shared — Clarifications Q4).
5. Use `[SkippableFact]` / `[SkippableTheory]` (not `[Fact]` / `[Theory]`) so dynamic skips are honored.

## Safety invariants (do not regress)

- **SendGrid**: never sends unless `SendGrid:Sandbox == true` (the only no-deliver guarantee).
- **Twilio**: `From` must be the magic number `+15005550006` under test credentials.
- **Stripe**: the harness refuses any key that is not `sk_test_…` / `rk_test_…`.
