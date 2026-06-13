# Contract: `integration-sandbox` CI job

**File**: `.github/workflows/test.yml`
**Requirements**: FR-001, FR-003, FR-006, FR-010

## Changes

1. **Narrow the existing `test` job** so PRs stay hermetic (no secrets, no sandbox tests):
   ```
   dotnet test ... --filter Category!=Sandbox
   ```

2. **Add `integration-sandbox` job**:
   ```yaml
   integration-sandbox:
     name: Integration (provider sandbox)
     runs-on: ubuntu-latest
     needs: test
     if: github.ref == 'refs/heads/main' && github.event_name == 'push'   # FR-001 main-only
     steps:
       - uses: actions/checkout@v4
       - uses: actions/setup-dotnet@v4
         with: { dotnet-version: '9.0.x' }
       - run: dotnet test --filter Category=Sandbox --configuration Release
         env:                                                              # FR-006 secrets, FR-010 masked
           ASPNETCORE_ENVIRONMENT: Test
           Stripe__SecretKey:            ${{ secrets.STRIPE_SECRET_KEY_TEST }}
           Stripe__WebhookSigningSecret: ${{ secrets.STRIPE_WEBHOOK_SIGNING_SECRET_TEST }}
           SendGrid__ApiKey:             ${{ secrets.SENDGRID_API_KEY }}
           SendGrid__FromEmail:          ${{ secrets.SENDGRID_FROM_EMAIL }}
           SendGrid__Sandbox:            'true'
           Twilio__AccountSid:           ${{ secrets.TWILIO_TEST_ACCOUNT_SID }}
           Twilio__AuthToken:            ${{ secrets.TWILIO_TEST_AUTH_TOKEN }}
           Twilio__FromNumber:           '+15005550006'
           TWILIO_TEST_CREDENTIALS:      'true'
   ```

3. **Gate the deploy (FR-003)**: change `docker-push` from `needs: test` to:
   ```yaml
   needs: [test, integration-sandbox]
   ```
   A failed `integration-sandbox` blocks `docker-push`; a *skipped* test (provider unavailable / unconfigured) does not fail the job, so an outage does not block deploy (SC-005) — but the skip is surfaced as a CI annotation.

## Secrets to provision (CI store)

| Secret | Provider | Notes |
|--------|----------|-------|
| `STRIPE_SECRET_KEY_TEST` | Stripe | `sk_test_…`/`rk_test_…` |
| `STRIPE_WEBHOOK_SIGNING_SECRET_TEST` | Stripe | `whsec_…` for the test endpoint |
| `SENDGRID_API_KEY` | SendGrid | any key; safety is sandbox mode, not the key |
| `SENDGRID_FROM_EMAIL` | SendGrid | a verified sender |
| `TWILIO_TEST_ACCOUNT_SID` | Twilio | test Account SID (`AC…`) |
| `TWILIO_TEST_AUTH_TOKEN` | Twilio | test Auth Token |

All are **test-scoped** and distinct from production secrets. `.NET` reads `Section__Key` env vars into `IConfiguration` automatically.

## Failure-vs-outage in CI (FR-005)

- **Red (Fail)** → real regression → blocks `docker-push`.
- **Skipped** (`SkipException` from the harness) → provider unavailable or unconfigured → job stays green with an annotation; deploy proceeds. Fix-forward on the next push (Clarifications Q2 — no auto-revert).
