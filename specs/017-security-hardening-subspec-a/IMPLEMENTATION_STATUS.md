# Sub-spec A (Identity & Access) — Implementation Status

**Branch**: `017-security-hardening-subspec-a` · **PR**: #100 (draft) · **Issue**: #94
**Spec**: `specs/017-security-hardening-subspec-a/spec.md` · **Umbrella**: `specs/016-security-hardening/`
**Last updated**: 2026-07-04

> Self-contained handoff so this slice can be resumed in a fresh context. Ignore sub-specs B–F
> (branches 018–022, PRs #101–#105) — this file is only about A.

## Build & test commands
- Build: `dotnet build HOAManagementCompany/HOAManagementCompany.csproj`
- Auth tests: `dotnet test HOAManagementCompany.Tests/HOAManagementCompany.Tests.csproj --filter "FullyQualifiedName~Integration.Auth"`
- EF migrations need `ConnectionStrings__DefaultConnection` env set (placeholder is fine for `migrations add`).

## DONE (committed on this branch; builds clean; 16 auth integration tests green)
- **FR-A4 lockout**: `Program.cs` `AddIdentityCore` (10 fails / 30-min, config `Identity:Lockout:*`); `AuthService.LoginAsync` lockout-aware (IsLockedOut → AccessFailed → ResetAccessFailed); new/seeded users `LockoutEnabled=true`.
- **FR-A7 JWT**: `Program.cs` `TokenValidationParameters` — `ValidAlgorithms=[HS256]`, `ClockSkew=30s`.
- **FR-A1/A1a/A1b/A2/A3/A5 register rework** (the High takeover fix):
  - Entities: `Domain/Entities/EmailVerification.cs`, `PropertyClaimCode.cs`; DbContext config + migration `20260703161713_AddEmailVerificationAndPropertyClaimCode`.
  - `Features/Auth/AuthCrypto.cs` (hash/code/proof gen), `AuthNotifier.cs` (`IAuthNotifier` + `LoggingAuthNotifier`), `EmailVerificationService.cs`, `ClaimCodeService.cs`.
  - `/auth/verify-email/request` + `/auth/verify-email/confirm` (`VerifyEmailEndpoints.cs`), uniform responses.
  - `AuthService.RegisterAsync` reworked: requires verification proof + valid single-use claim code; account-number-only claiming removed; all failures generic (`REGISTRATION_FAILED`). `AuthModels.cs` `RegisterRequest` = (VerificationToken, Password, FirstName, LastName, ClaimCode).
  - `RegisterEndpoint.cs` rate-limited + validator updated; verify endpoints rate-limited.
  - `Program.cs` DI: EmailVerificationService, ClaimCodeService, IAuthNotifier→LoggingAuthNotifier.
  - `Seed/AuthSeeder.cs`: unclaimed `SAKURA-003` + issued claim code (transition); lockout on seeded users.
- **FR-A6**: `Features/DevTools/E2ECleanupEndpoint.cs` — requires `X-Scheduler-Secret` (constant-time) AND hard-blocked in Production/Staging.
- **FR-A9**: rotated committed dev/test JWT throwaways off the audited strings + documented local/test-only (`appsettings.Development.json`, `appsettings.Test.json`).
- **Tests**: `HOAManagementCompany.Tests/Integration/Auth/AuthSecurityTests.cs` (5): uniform verify-email, generic verify-confirm failure, generic register refusal, register validation, 10-attempt lockout. (Old `RegisterTests.cs` removed.)

## REMAINING (in-repo)
_None — all in-repo work complete as of 2026-07-04._

- **FR-A8** done: `Features/Common/ClaimsPrincipalExtensions.cs` (`RequirePropertyId()`/`RequireCommunityId()` → `DomainException(401)`, generic message); `GlobalExceptionHandler` now maps `DomainException` to its status/code instead of 500; applied across `Features/Property/*` + `Features/Dashboard/*`. Tests: `Integration/Auth/ClaimHardeningTests.cs` (6: valid-signature token missing propertyId on 5 routes + missing communityId on dashboard → 401).
- **Email delivery adapter** done: `EmailAuthNotifier` in `Features/Auth/AuthNotifier.cs` (SendGrid `IAlertProvider` channel "email"; delivery failures logged with code withheld and swallowed to keep FR-A1 uniform responses). `Program.cs` DI picks `EmailAuthNotifier` when the email provider `IsConfigured`, else falls back to `LoggingAuthNotifier` (local dev/CI). Tests: `Unit/Auth/EmailAuthNotifierTests.cs` (3).

## HUMAN-NEEDED (leave for last; cannot be done in-repo)
1. **Merge coordination**: this PR changes the `/auth/register` contract → the frontend signup (sub-spec D, PR #103) must land together, or A must not merge to a shared environment first.
2. ~~**Deployed-Dev smoke gate**~~ RESOLVED in-repo (4926e13): both smoke flows (`test.yml` deploy-dev gate, `pr-env.yml` per-PR e2e) fetch `dev-scheduler-secret` from Secret Manager via the deployer identity and pass it to Playwright; `global-setup.ts` sends the header and warns on non-2xx.
3. **Secret rotation**: invalidate the previously-committed dev JWT secret in any dev DB / environment that reused it.

## Key design notes
- Claim code: single-use, 90-day, hashed (SHA-256), max 5 attempts, one live code per property (unique filtered index on `PropertyId WHERE RedeemedAt IS NULL`).
- Email verification: 6-digit code, 30-min expiry, then a 15-min opaque proof token; register consumes the proof + redeems the claim code atomically.
- Lockout: per-account, 10 fails → 30-min, independent of IP; responses stay generic (no lock-state oracle).
