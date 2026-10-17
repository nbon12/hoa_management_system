# Implementation Plan: Company Administration — Communities, Managers and Community Settings

**Branch**: `030-company-administration` | **Date**: 2026-10-17 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/030-company-administration/spec.md`

## Summary

Add a company layer above spec 025's per-community model, and the flows that need it:

- **A management company record and a Company Administrator grant.** These are checked through the existing community-scope resolver, which gains a status-aware decision type (FR-004a) and company-level capabilities (R1, R2).
- **A community lifecycle:** Onboarding → Active → Archived, with restore. It carries preview endpoints and cascades for invitations, autopay and resident emails, all under per-community advisory locks (R4, R5).
- **Invitations for memberships, residents and Company Administrators.**
  - 72-hour, single-use, email-bound tokens kept in URL fragments.
  - Sign-up from a link with no claim code, and acceptance by a signed-in account.
  - A sender-authority rule, and direct sending so tokens are never stored (R6).
- **The open-ended-manager rule**, with downgrade and re-appointment semantics, on the 025 membership endpoints (R7).
- **Sign-in without a linked home**, with a server-computed landing, and a per-request resident-access check on every resident endpoint (R8, R11).
- **Autopay attribution**, so removing a resident cancels only the schedule they enrolled, and archiving cancels all of them (R9).
- **Company default settings**, copied eagerly into new communities. Existing communities are backfilled so a later default change never reaches them (R13).
- **New Angular areas** built against the Claude Design briefs, with visual polish pending the handoff (R20):
  - the company area;
  - one Community settings page with a searchable side list and per-field Profile saving;
  - the Residents area with a property drawer;
  - invitation pages;
  - the no-home pages.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend `HOAManagementCompany`, tests `HOAManagementCompany.Tests`); TypeScript / Angular 17.3 (frontend `neko-hoa`).

**Primary Dependencies**: all existing. **No new packages.**
- **Backend**: FastEndpoints, EF Core 9 (Npgsql), ASP.NET Core Identity/JWT, `ICommunityScopeResolver`, `IAlertProvider` + `SesEmailProvider`, `OutboxMessage` + `OutboxDispatcher`, `Microsoft.AspNetCore.RateLimiting`, FluentValidation via `AddValidatedOptions`, Serilog, `TimeProvider`.
- **Frontend**: Angular standalone components and signals.

**Storage**: PostgreSQL (Neon in production, Testcontainers in CI and locally).
- **New tables**: `ManagementCompanies`, `CompanyAdministratorGrants`, `CompanyDefaultSettings`, `Invitations`, `InvitationDeliveries`.
- **Modified**: `Communities` (company FK, `NormalizedName`, lifecycle stamps, statuses Onboarding/Active/Archived) and `RecurringPayments` (`EnrolledByUserId`, `CancellationReason`).
- **Backfilled**: `CommunityArcSettings` rows for every community.
- **New**: an ICU collation, `natural_sort`.
- No file storage.

**Testing**:
- **Backend**: xUnit + Testcontainers PostgreSQL.
  - `[Theory]` matrices: role × action, status × resident operation, invitation state × action, and the pure `ManagerRule` and `LandingRule`.
  - Concurrency tests: last manager, last administrator, invitation accept vs revoke.
  - A migration test against a seeded legacy database.
  - Static tests for resolver use and resident access.
  - `InMemoryLogSink` assertions on sensitive events, including "no emails or names".
  - `FakeAlertProvider` for invitation sends, `TestClock` for the 72-hour expiry.
- **Frontend**: Karma/Jasmine, Angular Testing Library, Playwright (`@company`), Cypress and Storybook.

**Target Platform**: Cloud Run (backend), Cloudflare Pages (frontend). The setup step runs as a Cloud Run job execution of the service image.

**Project Type**: Web application, in the existing Angular + .NET layout.

**Performance Goals** (from the spec):
- SC-008: the portfolio's first page within 2 s at 200 communities (R17).
- Resident pages: one extra indexed lookup per request for resident access (R8).
- The Residents area pages 25 properties with aggregated joins (R16).

**Constraints**:
- **Authorization**: every board and company endpoint goes through the resolver in FR-004a's order; invitation endpoints are token-authorized only (R2, R3, R6).
- **Concurrency**: the last-manager, last-administrator and invitation-state rules hold under concurrent requests (R5).
- **Tokens**: never in a path, query, log, outbox payload or `Referer` (R6).
- **Sensitive events**: identify people and properties by ID only (R19).
- **Migrations**: forward-only, with a collision check before the new name index (R15).
- **Resident data**: the resident's property and community come from the database, not the token (R8).

**Scale/Scope**:
- One company; tens of communities; hundreds of properties and residents per community.
- 5 new tables, 2 modified tables, 1 collation.
- About 35 new or changed endpoints across the four contracts. About 30 resident endpoints gain the access check.
- 1 CLI verb.
- About 12 new Angular routes, 5 shared UI building blocks and 3 new services.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design below.*

- **Technology fit**: ✅ Angular, FastEndpoints, PostgreSQL/Neon, Identity + JWT, Cloudflare, Cloud Run, Sentry, Swashbuckle (dev only) and GitHub Actions are all reused. No new packages. The setup step reuses the existing `StartupTasks` CLI hook (R18).
- **HOA tenancy**: ✅
  - Every new row carries `CompanyId` or `CommunityId`, and invitations carry both.
  - Community scope is decided per community and never through the parent link (R2).
  - The intentional cross-community surfaces are each documented with their authorization and result scope:
    - the portfolio (Company Administrators; that company's communities; summary fields);
    - the parent picker (only communities the caller manages);
    - "My Communities".
  - Resident endpoints take the community from the database (R8).
- **API contracts**: ✅ Four contract files document authentication, the capability per endpoint, `limit`/`offset` (25/100, clamped), the error shape, `no-store` and statuses for every new error code (`contracts/invitations-and-auth.md § Error codes`). Changes to existing endpoints are additive. Two behavior changes are flagged:
  - `/auth/login` and `/auth/refresh` no longer return `NO_PROPERTY`;
  - resident endpoints now return `PROPERTY_ACCESS_DENIED`, `COMMUNITY_NOT_ACTIVE` and `COMMUNITY_ARCHIVED` where they used to return 500s (no-property tokens) or stale data.
- **Security and operations**: ✅
  - **Secrets**: none added.
  - **Authorization**: server-side through the resolver, including the self-grant ban (FR-003a) and sender authority (R6).
  - **Tokens**: 256-bit, hashed at rest, carried in URL fragments.
  - **Rate limits**: `board-writes` on every write, including the 025 membership endpoints that lack it today; `auth` on the invitation endpoints; per-recipient email caps (R12).
  - **Sensitive events**: logged for every change and for failed acceptances (R19).
  - **Errors**: the production error shape is unchanged.
  - **Observability**: new endpoints are covered by the existing backend Sentry and OpenTelemetry instrumentation. The Angular app's OpenTelemetry instrumentation strips URL fragments, so `/invite` tokens never reach telemetry (it has no Sentry SDK).
  - The pre-existing log-scrubber gap is raised as a separate task (R19).
- **File storage**: ✅ Not applicable. No files.
- **Caching/edge**: ✅ Every endpoint is `no-store`. `/invite` sets `Referrer-Policy: no-referrer`.
- **Testing discipline**: ✅
  - Test-first per user story.
  - Testcontainers PostgreSQL with isolated companies and communities per test, so the suite is parallel-safe.
  - `[Theory]` coverage as listed above.
  - Concurrency tests.
  - Every frontend tool is constitution-approved.
- **CI/CD and documentation**: ✅ Sonar and Codecov (95% on new files), with Repowise markers below. No infrastructure changes beyond documenting the Cloud Run job invocation.
- **Executable & living specs**:
  - ✅ Spec clarified (2026-10-08, 2026-10-10, 2026-10-14), reviewed adversarially (2026-10-11), with terms defined (2026-10-17).
  - ✅ Every acceptance scenario in US1–US9 maps to a planned test, and task generation must cite each one.
  - ✅ Reconciling the older specs (025, 027, 016, 017 sub-spec A, 006) is listed in the spec and becomes tasks.
  - ✅ One spec text change came out of planning: the Security bullet now allows the sender's display name on the invitation page (contract `POST /invitations/preview`).
- **Spec independence & parallelism**: ✅
  - Hard dependencies only on merged specs: 025, 027, 006, 016/017 and 029.
  - `029-resident-arc-requests` merged after this plan was first written. Its resident endpoints authorize through one class, `ResidentArcScope`, which this feature routes through `ResidentAccess` (tasks.md). That gives 029's routes the FR-012a status rule, which they lack today.
  - Notification Settings stays optional.
  - After the MVP foundation, US4, US5's ARC section, US6's flags and US7 can be built in parallel (spec Constitution Requirements).

No gate failures.

## Project Structure

### Documentation (this feature)

```text
specs/030-company-administration/
├── spec.md
├── plan.md                    # this file
├── research.md                # Phase 0
├── data-model.md              # Phase 1
├── quickstart.md              # Phase 1
├── contracts/
│   ├── company.md             # communities, lifecycle, portfolio, defaults, administrators, setup step
│   ├── community-settings.md  # profile, managers and memberships, invitations, residents, ARC section
│   ├── invitations-and-auth.md# invitation acceptance, sign-in changes, resident access, error codes
│   └── emails.md
├── design/                    # Claude Design briefs (00–12) + README; handoff/ arrives later
├── checklists/requirements.md
└── tasks.md                   # /speckit.tasks
```

### Source Code (repository root)

```text
HOAManagementCompany/
├── Domain/
│   ├── Entities/
│   │   ├── ManagementCompany.cs                    # new
│   │   ├── CompanyAdministratorGrant.cs            # new
│   │   ├── CompanyDefaultSettings.cs               # new
│   │   ├── Invitation.cs                           # new
│   │   ├── InvitationDelivery.cs                   # new
│   │   ├── Community.cs                            # modified: company FK, NormalizedName, lifecycle stamps
│   │   └── RecurringPayment.cs                     # modified: EnrolledByUserId, CancellationReason
│   └── Enums/                                      # CommunityStatus (changed), Invitation*, CompanyGrantSource
├── Features/
│   ├── Auth/
│   │   ├── AuthService.cs                          # no-home sign-in, FindUsablePropertyAsync, LandingRule, CurrentUserDto
│   │   ├── LandingRule.cs                          # new, pure (R11)
│   │   └── Models/AuthModels.cs                    # CurrentUserDto additions
│   ├── Board/
│   │   ├── ICommunityScopeResolver.cs              # DecideAsync, CanAccessCompanyAsync, capabilities
│   │   ├── CommunityScopeResolver.cs               # FR-004a order, company grant
│   │   ├── CommunityLocks.cs                       # new (R5)
│   │   ├── ManagerRule.cs                          # new, pure (R7)
│   │   ├── SenderAuthority.cs                      # new (R6)
│   │   ├── MembershipCreateEndpoint.cs             # email path, people check, ManagerRule, board-writes
│   │   ├── MembershipUpdateEndpoint.cs             # end/downgrade/end-date, lock, board-writes
│   │   ├── MembershipsListEndpoint.cs              # openEnded/can* fields
│   │   ├── MyCommunitiesEndpoint.cs                # Onboarding included, Archived excluded
│   │   ├── Settings/                               # new: CommunitySettingsEndpoint, Profile{Get,Patch}Endpoint,
│   │   │                                           #      PeopleEndpoint, ReplaceManagerEndpoint,
│   │   │                                           #      Invitations{List,Resend,Revoke}Endpoint
│   │   ├── Residents/                              # new: ResidentsList, ResidentDetail, ResidentInvite,
│   │   │                                           #      RemovalPreview, ResidentRemove endpoints
│   │   └── Architectural/
│   │       ├── ArcSettingsGetEndpoint.cs           # ViewArcSettings + defaults comparison
│   │       ├── ArcSettingsPutEndpoint.cs           # ManageArcSettings
│   │       ├── ArcSettingsResetEndpoint.cs         # new
│   │       └── ArcSweepService.cs                  # skip non-Active communities
│   ├── Company/                                    # new
│   │   ├── ICompanyContext.cs / CompanyContext.cs
│   │   ├── CommunityCreateEndpoint.cs
│   │   ├── PortfolioEndpoint.cs
│   │   ├── CommunityLifecycleService.cs            # activate/archive/restore/parent + previews (R4)
│   │   ├── Lifecycle{Preview,Activate,Archive,Restore,Parent}Endpoint.cs
│   │   ├── CompanyDefaults{Get,Put}Endpoint.cs
│   │   ├── CompanyAdministrators{List,Invite,Revoke}Endpoint.cs
│   │   ├── CompanyAdministratorService.cs          # grants, revoke under CompanyLocks, SetupAsync (R18)
│   │   └── CompanyEmailRenderer.cs                 # co_* outbox kinds (R10)
│   ├── Invitations/                                # new; token-authorized, outside the scope test
│   │   ├── InvitationService.cs                    # create/send/resend/revoke/accept, caps (R6, R12)
│   │   ├── InvitationTokens.cs
│   │   ├── InvitationEmailRenderer.cs              # direct-send bodies (contracts/emails.md)
│   │   └── Invitation{Preview,Register,Accept}Endpoint.cs
│   ├── Residents/
│   │   └── ResidentAccess.cs                       # new (R8); used by every resident endpoint
│   ├── Payments/                                   # every resident endpoint → ResidentAccess; PaymentService:
│   │                                               # EnrolledByUserId, CancelRecurringForAsync; draft job filter
│   ├── Property/, Community/, Dashboard/           # resident endpoints → ResidentAccess
│   └── Common/ClaimsPrincipalExtensions.cs         # raw Guid.Parse claim reads removed
├── Infrastructure/
│   ├── Configuration/StartupTasks.cs               # --grant-company-admin [--recover] [--company-name]
│   ├── Configuration/InvitationOptions.cs (+ validator)
│   └── Persistence/
│       ├── ApplicationDbContext.cs                 # new DbSets + configuration
│       └── Migrations/<ts>_AddCompanyAdministration.cs
├── Seed/
│   ├── AuthSeeder.cs                               # drop co-residence workaround for staff users
│   └── CompanySeeder.cs                            # new: company@nekohoa.dev, Keystone Ridge (dev-like only)
└── Program.cs                                      # DI, options, rate-limit wiring

HOAManagementCompany.Tests/
├── Unit/Company/                                   # ManagerRuleTheoryTests, LandingRuleTheoryTests, InvitationStateTests
├── Integration/Company/                            # one class per user story (US1–US9), lifecycle cascades,
│                                                   # concurrency, setup step, migration test
├── Integration/Invitations/                        # preview/register/accept, refusal order, caps, sender authority
├── Integration/Residents/                          # ResidentAccessTheoryTests (status × endpoint), removal + autopay
└── Integration/Board/
    ├── BoardScopeEnforcementStaticAnalysisTests.cs # widened folders and method names (R3)
    └── ResidentAccessStaticAnalysisTests.cs        # new (R3)

neko-hoa/src/app/
├── app.routes.ts                                   # routes in research R20; redirects for arc-settings/memberships
├── core/
│   ├── guards/company-admin.guard.ts               # new
│   ├── services/auth.service.ts                    # keep properties, isCompanyAdministrator, landing
│   ├── services/landing.ts                         # follows server landing (R11)
│   ├── services/company.service.ts                 # new
│   ├── services/community-settings.service.ts      # new
│   ├── services/invitation.service.ts              # new
│   ├── services/toast.service.ts                   # new
│   ├── services/board-navigation.service.ts        # + Community settings, Add community entries
│   └── interceptors/auth.interceptor.ts            # PROPERTY_ACCESS_DENIED → re-land with notice
├── shared/
│   ├── disabled-reason.directive.ts                # hover + focus tooltip (Clarifications 2026-10-14)
│   ├── autosave-field.directive.ts                 # per-field save, "✓ Saved"
│   ├── toast-host.component.ts
│   └── property-drawer.component.ts
├── shell/shell.component.ts                        # company-area state, Company menu
└── features/
    ├── auth/company-sign-in.component.ts           # new, bookmarkable
    ├── invitations/invitation-page.component.ts    # new, /invite
    ├── landing/no-home.component.ts, not-live.component.ts
    ├── company/                                    # portfolio, add-community, company-defaults, company-administrators
    └── community-settings/                         # page shell + side nav with search, profile, managers-memberships,
                                                    # residents (list + drawer), architectural-review (moved arc-settings)
```

**Structure Decision**: This follows 025 and 027.
- **Community-scoped endpoints** stay under `Features/Board/` (`Settings/`, `Residents/`, `Architectural/`), so the static scope test keeps covering them.
- **Company-level endpoints** go in a new `Features/Company/`, which the static test now also scans.
- **Token-authorized invitation endpoints** live in `Features/Invitations/`, which is deliberately outside that test. They are authorized by the link token, not by membership, and have their own refusal-order tests.
- **The resident-access check** is one service in `Features/Residents/`, called by about 30 existing endpoints and enforced by its own static test.
- **The frontend** adds `features/company/` and `features/community-settings/` and retires `features/board/membership-admin` (its raw User ID entry). The 027 `arc-settings` component moves into the settings page as its Architectural review section.

## Repowise Documentation

**Status**: In progress. Marker regions are planned for the files below; the `repowise-gate` CI job regenerates them.

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `Domain/Entities/Invitation.cs` | `domain=entities` | Three invitation kinds, the state machine, token hashing |
| `Domain/Entities/Community.cs` | `domain=entities` | Lifecycle statuses and the restore rule |
| `Features/Board/CommunityScopeResolver.cs` | `domain=board` | FR-004a decision order, the company grant, capabilities |
| `Features/Board/ManagerRule.cs` | `domain=board` | The open-ended-manager rule |
| `Features/Company/CommunityLifecycleService.cs` | `domain=company` | Activate, archive and restore cascades |
| `Features/Invitations/InvitationService.cs` | `domain=invitations` | Lifecycle, sender authority, caps, direct send |
| `Features/Residents/ResidentAccess.cs` | `domain=residents` | The per-request resident check and FR-012a outcomes |
| `specs/030-company-administration/spec.md` | `section=summary` | Feature summary for the index |

### Marker syntax

```csharp
// <!-- REPOWISE:START domain=company -->
// ... generated content ...
// <!-- REPOWISE:END -->
```

```markdown
<!-- REPOWISE:START section=summary -->
... generated content ...
<!-- REPOWISE:END -->
```

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

There are no constitution violations. These choices widen the slice and are recorded for reviewers:

| Choice | Why needed | Simpler alternative rejected because |
|--------|------------|--------------------------------------|
| A per-request `ResidentAccess` check in about 30 existing resident endpoints | SC-011 and FR-012a need removal and status to apply on the next request, and today's endpoints trust 15-minute token claims (or return 500 with no property claim) | Shorter tokens still leave a window and add refresh churn. A hidden global pre-processor can't tell read from write from payment-history access (R8). |
| Changing sign-in (`AuthService`) so staff need no home | Managers and Company Administrators often live in no community. Removing a resident's only home must not lock out their board role (spec F01). | Keeping the co-residence workaround would give staff fake homes and resident access they shouldn't have |
| Sending invitation emails directly, not through the outbox | The outbox stores payloads in plain text and would hold live tokens (R6) | A token reference in the outbox would need a second render path only for invitations |
| An ICU collation for natural address order | The design orders #9 before #10 (brief 06) | Sorting in memory breaks paging, and a stored sort key needs extra columns for every address format |
| No Stripe payment-method detach on removal or archive (a known limit) | Today's owner-initiated cancel doesn't detach either, and the spec doesn't require it (R9) | Detaching would add new `IStripeGateway` surface and failure modes for no spec requirement. It can follow if the payments owner wants it. |
