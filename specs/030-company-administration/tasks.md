---
description: "Task list for 030-company-administration"
---

# Tasks: Company Administration — Communities, Managers and Community Settings

**Input**: Design documents from `specs/030-company-administration/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (`company.md`, `community-settings.md`, `invitations-and-auth.md`, `emails.md`), quickstart.md, design/ (Claude Design briefs 00–12)

**Tests**: Required. The spec's "Executable & living spec" clause and CLAUDE.md's NLT rule need a faithful automated test for every acceptance scenario and Independent Test.
- Each test task cites the scenarios it covers as `USn-Sm` (story n, scenario m in `spec.md`).
- Write the tests first and watch them fail before implementing.
- Denial scenarios assert the exact status and code, not just that the happy path works.

**Designs**: UI tasks build structure, behavior and copy from the briefs in `design/` (research R20). Brief numbers:

| Brief | Covers |
|---|---|
| 01 | company area and navigation, including the company sign-in page |
| 02 | portfolio |
| 03 | add community |
| 04 | the Community settings page shell, Profile and status |
| 05 | Managers and memberships |
| 06 | Residents |
| 07 | Architectural review section |
| 08 | company defaults |
| 09 | Company Administrators |
| 10 | invitation acceptance |
| 11 | sign-in and resident states |
| 12 | emails |

Visual polish waits for `design/handoff/` (T160).

**Organization**:
- **Phase 2** holds:
  - the spec's MVP foundation slice (Spec independence, items a–e);
  - the shared pieces several stories need: per-request resident access (FR-024g), sign-in without a home (FR-036a, backend), the invitation core (FR-022), and the Community settings page shell.
- **Story phases** follow priority: P1 = US1, US2, US3, US8, US9; P2 = US4, US5, US7; P3 = US6.
- **US9's backend** is in Phase 2, because every other story needs managers and Company Administrators who can sign in without a home. Its phase adds the pages.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: the user story the task belongs to

## Path Conventions

| Short name | Path |
|---|---|
| Backend | `HOAManagementCompany/` |
| Backend tests | `HOAManagementCompany.Tests/` |
| Company backend | `HOAManagementCompany/Features/Company/` |
| Invitations backend | `HOAManagementCompany/Features/Invitations/` |
| Settings backend | `HOAManagementCompany/Features/Board/Settings/` |
| Residents backend | `HOAManagementCompany/Features/Board/Residents/` (endpoints), `HOAManagementCompany/Features/Residents/` (`ResidentAccess`) |
| Unit tests | `HOAManagementCompany.Tests/Unit/Company/` |
| Company tests | `HOAManagementCompany.Tests/Integration/Company/` |
| Invitation tests | `HOAManagementCompany.Tests/Integration/Invitations/` |
| Resident tests | `HOAManagementCompany.Tests/Integration/Residents/` |
| Frontend | `neko-hoa/src/app/` |
| Company frontend | `neko-hoa/src/app/features/company/` |
| Settings frontend | `neko-hoa/src/app/features/community-settings/` |
| Shared UI | `neko-hoa/src/app/shared/` |
| Playwright | `neko-hoa/e2e/` |
| Cypress | `neko-hoa/cypress/e2e/` |

---

## Phase 1: Setup

- [X] T001 Create the backend folders and the test folders:
  - backend: `HOAManagementCompany/Features/Company/`, `HOAManagementCompany/Features/Invitations/`, `HOAManagementCompany/Features/Residents/`, `HOAManagementCompany/Features/Board/Settings/`, `HOAManagementCompany/Features/Board/Residents/`;
  - tests: `HOAManagementCompany.Tests/Unit/Company/`, `HOAManagementCompany.Tests/Integration/Company/`, `HOAManagementCompany.Tests/Integration/Invitations/`, `HOAManagementCompany.Tests/Integration/Residents/`.
- [X] T002 [P] Create the frontend folders `neko-hoa/src/app/features/company/`, `neko-hoa/src/app/features/community-settings/`, `neko-hoa/src/app/features/invitations/`, `neko-hoa/src/app/features/landing/` and `neko-hoa/src/app/shared/`
- [X] T003 [P] Write `InvitationOptionsValidatorTests` first. Rules:
  - `ExpiryHours` must equal 72;
  - `MaxEmailsPerRecipientPer24h` is 1–50, default 5;
  - `ResendCooldownSeconds` is 0–3600, default 60;
  - a bad value fails startup validation and names the option.

  File: `HOAManagementCompany.Tests/Unit/Configuration/InvitationOptionsValidatorTests.cs`
- [X] T004 Add `InvitationOptions` (section `Invitations`) and its FluentValidation `InvitationOptionsValidator` (R12), until T003 passes.
  - Files: `HOAManagementCompany/Infrastructure/Configuration/InvitationOptions.cs` and `InvitationOptionsValidator.cs`.
  - Register them with `AddValidatedOptions` in `HOAManagementCompany/Program.cs`.
  - Add the defaults to `HOAManagementCompany/appsettings.json`.
- [X] T005 [P] Add `AppLinks`, which reads `App:BaseUrl` and falls back to `ArchitecturalReview:AppBaseUrl` (R10). File: `HOAManagementCompany/Infrastructure/Configuration/AppLinks.cs`.
  - Switch `HOAManagementCompany/Features/Board/Architectural/ArcEmailRenderer.cs` to it without changing its output.
  - Add `AppLinksValidator` (FluentValidation, registered with `AddValidatedOptions`; constitution §8): one of the two keys must be set, and the URL must be absolute, and HTTPS outside Development.
  - Test (`HOAManagementCompany.Tests/Unit/Configuration/AppLinksTests.cs`): the new key wins when both keys are set; the old key alone still works; the validator refuses a missing value, a relative URL, and plain HTTP outside Development.

---

## Phase 2: Foundational (blocking: must finish before any user story)

**Purpose**: the data model and migration; authorization; locks and pure rules; per-request resident access; sign-in without a home; the invitation core and acceptance page; the setup step and seed; the company area and Community settings page shell. Every story uses these.

### Enums and entities (data-model.md)

- [X] T006 Change `CommunityStatus` to `Onboarding`, `Active`, `Archived` in `HOAManagementCompany/Domain/Enums/CommunityStatus.cs`. Its one other use, in `HOAManagementCompany.Tests/Integration/Board/CommunityScopeResolverTests.cs:84`, moves to `Archived` in T016. `MembershipStatus.Inactive` is a different enum and stays.
- [X] T007 [P] Create these enums, each in `HOAManagementCompany/Domain/Enums/<Name>.cs`:
  - `InvitationKind`, `InvitationState`;
  - `InvitationRevokeReason`: `Manual`, `CommunityArchived`, `SenderLostAuthority`, `Superseded`;
  - `CompanyGrantSource`: `Setup`, `Invitation`, `Recovery`.
- [X] T008 [P] Create `ManagementCompany` in `HOAManagementCompany/Domain/Entities/ManagementCompany.cs` and `CompanyDefaultSettings` in `HOAManagementCompany/Domain/Entities/CompanyDefaultSettings.cs`. The defaults entity holds the six ARC rule fields, and its initial values are the 027 defaults, shared as constants with `CommunityArcSettings`.
- [X] T009 [P] Create `CompanyAdministratorGrant` (data-model.md fields, plus `IsLive => RevokedAt == null`) in `HOAManagementCompany/Domain/Entities/CompanyAdministratorGrant.cs`
- [X] T010 [P] Create `Invitation` and `InvitationDelivery` per data-model.md in `HOAManagementCompany/Domain/Entities/Invitation.cs` and `InvitationDelivery.cs`.
  - `Invitation.EffectiveState(DateTimeOffset now)` adds the derived `Expired` state.
  - `SenderUserId` is null only for a `CompanyAdministrator` invitation created by the setup step.
  - Add a Repowise `domain=entities` marker describing the three kinds, the state machine and token hashing.
- [X] T011 Modify `HOAManagementCompany/Domain/Entities/Community.cs`:
  - add `ManagementCompanyId` and its navigation;
  - add `NormalizedName`, set only through a `SetCommunityName(string)` method that trims and lower-cases, so the two can't drift;
  - add `CreatedByUserId` and the activated, archived and restored stamps;
  - add a Repowise `domain=entities` marker for the lifecycle and the restore rule.
- [X] T012 [P] Modify `RecurringPayment` in `HOAManagementCompany/Domain/Entities/RecurringPayment.cs`: add `EnrolledByUserId` and `CancellationReason`, plus a `RecurringCancellationReasons` constants class (`owner_cancelled`, `community_archived`, `resident_removed`).

### Persistence

- [X] T013 Configure the new and changed entities in `HOAManagementCompany/Infrastructure/Persistence/ApplicationDbContext.cs`:
  - the DbSets;
  - string-converted enums and max lengths;
  - FKs and delete rules per data-model.md: `Invitation.SenderUserId` Restrict, other user references SetNull, grants Cascade;
  - the filtered unique indexes on `CompanyAdministratorGrants` and `Invitations`, and `InvitationDeliveries (NormalizedEmail, AttemptedAt)`;
  - on `Communities`, unique `(ManagementCompanyId, NormalizedName)` and `(ManagementCompanyId, Status)`, replacing the global `CommunityName` unique index;
  - the `CK_Invitations_KindShape` check, which also requires `SenderUserId` unless `Kind = 'CompanyAdministrator'`.
- [X] T014 Generate the forward-only migration with `dotnet ef migrations add AddCompanyAdministration` into `HOAManagementCompany/Infrastructure/Persistence/Migrations/`.
  - Add the hand-written SQL for data-model.md steps 1–8:
    - the company row and its defaults row;
    - the community backfill;
    - the R15 collision check, before the index swap;
    - the `CommunityArcSettings` backfill;
    - for `Inactive` → `Archived` communities, cancel autopay and terminate authorizations, with no email;
    - `CREATE COLLATION IF NOT EXISTS natural_sort`.
  - Every data step is idempotent and nothing is destructive. Follow the pattern of `20260825181517_AddCommunityAndMembership`.
- [X] T015 Add `CompanyAdministrationMigrationTests`, following `HOAManagementCompany.Tests/Integration/Board/CommunityBackfillMigrationTests.cs`.
  - **Setup**: migrate a fresh Testcontainers database to the migration before this one. Seed one Active and one Inactive community, an active autopay schedule in the Inactive one, and no ARC settings rows. Then migrate.
  - **Asserts**:
    - exactly one `ManagementCompanies` row, and its `CompanyDefaultSettings` row has the 027 values;
    - both communities are linked to that company;
    - the Active community stays Active, with `ActivatedAt = CreatedAt`;
    - the Inactive community is Archived, with `ArchivedAt` set;
    - the schedule is `inactive` with `CancellationReason = community_archived`, its authorization has `TerminatedAt`, and no `OutboxMessages` row was written;
    - every community has a `CommunityArcSettings` row with the 027 constants;
    - `ORDER BY … COLLATE natural_sort` puts `#9` before `#10`;
    - a second database seeded with "Keystone Park" and "keystone park " fails the migration with the R15 message, and nothing is half-applied;
    - inserting an invitation that breaks `CK_Invitations_KindShape` fails, and so does a second open invitation for the same target.
  - File: `HOAManagementCompany.Tests/Integration/Company/CompanyAdministrationMigrationTests.cs`

- [X] T016 Update the existing code that creates communities, so the suite survives the migration's non-null `ManagementCompanyId` and the `SetCommunityName`-only `NormalizedName`:
  - `HOAManagementCompany/Seed/AuthSeeder.cs` (the seeded "Sakura Heights HOA");
  - `HOAManagementCompany.Tests/Fixtures/TestDataSeeder.cs`;
  - `HOAManagementCompany.Tests/Integration/Board/BoardTestBase.cs` (`CreateCommunityAsync`);
  - `HOAManagementCompany.Tests/Integration/Seed/SeederTests.cs` and `ArchitecturalSeederTests.cs`;
  - `HOAManagementCompany.Tests/Integration/Board/CommunityScopeResolverTests.cs:84`, where `CommunityStatus.Inactive` becomes `Archived`.

  Seeders use the deployment's company. Test helpers take a company id, defaulting to the deployment's. Then run `dotnet build` and the 025, 027 and 029 suites, to confirm nothing else builds a `Community` the old way.

### Test harness

Isolation follows the pattern recorded in plan.md's Complexity Tracking: unique rows per test instead of a rolled-back transaction (testing constitution §2.1, approved 2026-10-18).


- [X] T017 Create `CompanyTestBase : BoardTestBase` in `HOAManagementCompany.Tests/Integration/Company/CompanyTestBase.cs`. Its factory helpers create only their own rows, keyed by new GUIDs, so tests are parallel-safe:
  - `CreateCompanyAsync()`, `CreateCompanyAdminAsync(companyId)`;
  - `CreateCommunityAsync(companyId, status, name?, parentId?)`, `CreateManagerAsync(communityId, endDate?)`;
  - `CreatePropertyWithResidentAsync(communityId, address?)`;
  - `CreateInvitationAsync(kind, state, …)`, `CreateAutopayAsync(propertyId, enrolledBy?)`;
  - `ReadInvitationLinkAsync(email)`, which reads the link from `FakeAlertProvider`;
  - `LoginAsync(user)`, which returns an authenticated client;
  - `CreateFreshDatabaseAsync()`, which creates a new database in the same container and migrates it. It is for tests that read the deployment's single company (T058, T135).

  Move `TestClock` from `ArcTestBase.cs` to `HOAManagementCompany.Tests/Fixtures/TestClock.cs` so both bases share it.

### Authorization (research R2, R3)

- [X] T018 Extend `HOAManagementCompany/Features/Board/ICommunityScopeResolver.cs`:
  - the eight new `CommunityCapability` values;
  - the `CompanyCapability`, `ScopeDecision` and `ScopeOperation` enums;
  - `DecideAsync(user, communityId, capability, op, ct)` and `CanAccessCompanyAsync(user, capability, ct)`.
- [X] T019 [P] Write `ScopeDecisionTheoryTests` first.
  - **Theory**: role (Company Administrator, Community Manager of this community, Community Manager of another, Board Member, Accountant, Resident, no role, and a user who is both a Company Administrator and this community's Community Manager) × every community capability × status (Onboarding, Active, Archived) × op (Read, Write). Assert the R2 capability table.
  - **Also assert**:
    - a nonexistent community gives `Forbidden` to everyone;
    - an ended membership, or one past its end date, confers nothing;
    - the Company Administrator grant never satisfies `ViewAssociationData` or the three 027 ARC capabilities (FR-003);
    - a membership in a parent never reaches a sub-association (US1-S7, the access half);
    - in an Archived community (FR-004a), a Company Administrator gets `Allowed` for settings reads and `ManageCommunityLifecycle` writes and `Archived` for everything else, and members get `Archived`;
    - the user who holds both roles gets the union of their capabilities (spec Edge Cases).

  File: `HOAManagementCompany.Tests/Integration/Company/ScopeDecisionTheoryTests.cs`
- [X] T020 [P] Write `CompanyCapabilityTheoryTests` first.
  - `CreateCommunity` and `ViewCompanyDefaults`:
    - allowed for a Company Administrator, and for an effective Community Manager in an Onboarding or Active community;
    - refused for a Community Manager whose only membership is in an Archived community (US1-S6), ended, or past its end date;
    - refused for Board Member, Accountant, Resident and no role (US1-S5).
  - `ManageCompanyDefaults`, `ManageCompanyAdministrators` and `ViewPortfolio` are allowed for Company Administrators only.
  - A revoked grant confers nothing.

  File: `HOAManagementCompany.Tests/Integration/Company/CompanyCapabilityTheoryTests.cs`
- [X] T021 Implement `DecideAsync` and `CanAccessCompanyAsync` in `HOAManagementCompany/Features/Board/CommunityScopeResolver.cs`, in FR-004a's order (R2), until T019 and T020 pass.
  - Keep `CanAccessAsync` as the wrapper `DecideAsync(…, Read) == Allowed`.
  - Set `community.id` and `capability` tags on `Activity.Current` for each decision. The backend's Sentry reads the OpenTelemetry pipeline, so its events carry them (spec Observability). Never tag an email or a name.
  - Add the Repowise `domain=board` marker.
  - Confirm `CommunityScopeResolverTests`, `ArcCapabilityTests` and `ResidentScopeUnchangedTests` still pass.
- [X] T022 [P] Add `BoardHttp.SendDecisionAsync(ScopeDecision)` in `HOAManagementCompany/Features/Board/BoardHttp.cs`. Each status gets a plain-language message (FR-042):
  - `Forbidden` → the existing 403 body;
  - `Archived` → 409 `COMMUNITY_ARCHIVED`;
  - `NotActive` → 409 `COMMUNITY_NOT_ACTIVE`.
- [X] T023 [P] Add `CompanyErrors` in `HOAManagementCompany/Features/Company/CompanyErrors.cs`: one factory per code in `contracts/invitations-and-auth.md § Error codes`, each a `DomainException` with the contract's HTTP status.
  - Add an optional `Extensions` payload to `DomainException` (`HOAManagementCompany/Features/Auth/AuthService.cs`), and write it into the body in `HOAManagementCompany/Features/Common/GlobalExceptionHandler.cs`. It carries `OPEN_WORK`'s `items[]` and `INVITATION_PENDING`'s `invitationId`.
  - Unit test the body shape in `HOAManagementCompany.Tests/Unit/Company/CompanyErrorsTests.cs`.
- [X] T024 Widen `HOAManagementCompany.Tests/Integration/Board/BoardScopeEnforcementStaticAnalysisTests.cs` (R3):
  - scan `Features/Board`, `Features/Company` and `Features/Residents` recursively, matching both `*Endpoint.cs` and `*Endpoints.cs`;
  - accept `CanAccessAsync`, `DecideAsync` or `CanAccessCompanyAsync`;
  - keep the allow-list rule that every listed name must exist;
  - document why `Features/Invitations/` (token-authorized) is not scanned.
- [X] T025 [P] Add `WriteRateLimitStaticAnalysisTests` (FR-041a, b), and add `RequireRateLimiting("board-writes")` now to the 025 `MembershipCreateEndpoint.cs` and `MembershipUpdateEndpoint.cs`.
  - The rule: every `*Endpoint.cs` or `*Endpoints.cs` under `Features/Board`, `Features/Company` and `Features/Invitations` that configures `Post`, `Put`, `Patch` or `Delete` must call `RequireRateLimiting("board-writes")`, or `RequireRateLimiting("auth")` under `Features/Invitations`.
  - The allow-list carries a reason for each entry (`ArcSweepJobEndpoint`, `BoardModeEndpoint`).

  File: `HOAManagementCompany.Tests/Integration/Board/WriteRateLimitStaticAnalysisTests.cs`

### Company context, locks and pure rules

- [X] T026 [P] Add `ICompanyContext` and `CompanyContext` (scoped, cached per request) in `HOAManagementCompany/Features/Company/ICompanyContext.cs` and `CompanyContext.cs`, and register them in `Program.cs`.
  - `ForUserAsync(user)` returns the company of the caller's live grant, else the company of their effective Community Manager memberships.
  - `ForCommunityAsync(communityId)` returns the community's company.
  - `GetDeploymentCompanyAsync()` returns the single row, and throws unless there is exactly one. It is for the setup step and the seed only.
  - Every company query filters by the returned id (FR-001). That also keeps tests that create their own companies isolated.
- [X] T027 [P] Add `CommunityLocks.RunAsync(communityId, body, ct)` in `HOAManagementCompany/Features/Board/CommunityLocks.cs` and `CompanyLocks.RunAsync(companyId, body, ct)` in `HOAManagementCompany/Features/Company/CompanyLocks.cs` (R5).
  - Each takes `pg_advisory_xact_lock` on a key derived from the id, in a key space distinct from `LedgerService`'s.
  - Each runs inside `CreateExecutionStrategy().ExecuteAsync` and one transaction.
  - Test (`HOAManagementCompany.Tests/Integration/Company/AdvisoryLockTests.cs`): two concurrent bodies serialize on the same id, and don't on different ids.
- [X] T028 [P] Write `ManagerRuleTheoryTests` first. These are pure tests: current rows × proposed change → allowed or `LAST_MANAGER`.
  - **Changes covered**: end, downgrade, deactivate; set, move or clear an end date; appoint; replace; accept a manager invitation with or without an end date.
  - **Cases**:
    - the only open-ended manager can't be ended, downgraded or end-dated (the US2-S9 and US2-S10 data);
    - one of two open-ended managers can be;
    - an end-dated co-manager is allowed only while another open-ended manager remains;
    - a first manager must be open-ended;
    - a community that has never had a manager accepts any open-ended first appointment;
    - Archived skips the rule.

  File: `HOAManagementCompany.Tests/Unit/Company/ManagerRuleTheoryTests.cs`
- [X] T029 Implement the pure, static `ManagerRule.Evaluate(currentRows, change, today)`, with a Repowise `domain=board` marker, until T028 passes. File: `HOAManagementCompany/Features/Board/ManagerRule.cs`
- [X] T030 [P] Write `LandingRuleTheoryTests` first. These are pure tests.
  - **Inputs**: usable homes (Active, Archived, Onboarding only, none); effective non-resident memberships in Onboarding or Active communities; whether the user is a Company Administrator; `lastActiveMode`; which sign-in page was used.
  - **Output**: `Board`, `Company`, `Resident`, `NotLive` or `NoHome`, in FR-036a's order.
  - **Cases**:
    - US9-S1 → `Company`;
    - US9-S2 → `Board`;
    - US9-S3 → `NoHome`;
    - US9-S5 → `NotLive`;
    - US9-S7: the company page gives `Company`, whatever else the user holds;
    - US9-S8: the company page for a non-administrator gives the usual landing.

  File: `HOAManagementCompany.Tests/Unit/Company/LandingRuleTheoryTests.cs`
- [X] T031 Implement the pure `LandingRule.Decide(...)` until T030 passes. File: `HOAManagementCompany/Features/Auth/LandingRule.cs`
- [X] T032 [P] Add `CompanyLog` in `HOAManagementCompany/Features/Company/CompanyLog.cs`, following `ArcLog`.
  - It has one method per R19 template: `CommunityLifecycle`, `CommunityProfileChanged`, `CompanyDefaultsChanged`, `InvitationChange`, `InvitationAcceptFailed`, `ResidentLinkChange`, `AutopayCancelled`, `CompanyAdminChange`.
  - Each method takes IDs, codes and setting values only, plus an optional `Cause`.
  - Add a `BoardMembershipChange` overload that adds `Kind` and `Cause`.
- [X] T033 [P] Add `CompanyEmailRenderer` in `HOAManagementCompany/Features/Company/CompanyEmailRenderer.cs`, modelled on `ArcEmailRenderer`.
  - It has the five `co_*` kinds as constants, with their dedup keys and plain-text bodies per `contracts/emails.md`.
  - Register it with the outbox dispatcher.
  - Unit test (`HOAManagementCompany.Tests/Unit/Company/CompanyEmailRendererTests.cs`): every kind is at most 30 characters; each kind's subject, body fields and dedup-key format; no body contains an invitation link.
- [X] T034 [P] Add `refusalMessage(code, context)` in `neko-hoa/src/app/shared/refusal-messages.ts`, mapping every error code to the conventions table in `design/00-conventions.md` (FR-042). Test it in `refusal-messages.spec.ts`: every contract code has a message, and the non-disclosing codes never interpolate a name or an email.

### Resident access on every request (FR-024g, FR-012a; research R8)

- [X] T035 [P] Write `ResidentAccessTheoryTests` first.
  - **Theory**: every endpoint row in `contracts/invitations-and-auth.md § Resident access`, including spec 029's `/property/architectural-applications` routes, × home status (Active, Onboarding, Archived) × caller. The callers are a linked resident; a token for a property the user is no longer linked to; and a token with no `propertyId` claim.
  - **Assert exactly**:
    - Active and linked → the endpoint's normal status;
    - no claim, or no link → 403 `PROPERTY_ACCESS_DENIED`, and no row changed (US9-S4; US8-S6, first bullet);
    - Onboarding → 409 `COMMUNITY_NOT_ACTIVE` for every read and write (US3-S1);
    - Archived → 200 for the `ReadHistory` endpoints (payment history, and spec 029's submitted requests), 409 `COMMUNITY_ARCHIVED` for every other endpoint, drafts included (US3-S8).
  - **Also**: a token whose `communityId` claim is wrong still gets the right community's data, because the database's community is used.

  File: `HOAManagementCompany.Tests/Integration/Residents/ResidentAccessTheoryTests.cs`
- [X] T036 [P] Add `ResidentAccessStaticAnalysisTests` (R3). File: `HOAManagementCompany.Tests/Integration/Board/ResidentAccessStaticAnalysisTests.cs`
  - Every `*Endpoint.cs` or `*Endpoints.cs` under `Features/Payments`, `Features/Property`, `Features/Community` and `Features/Dashboard` that reads the `propertyId` or `communityId` claim, `RequirePropertyId()` or `RequireCommunityId()` must also call `ResidentAccess.RequireAsync`. Matching both names matters: `AlertPreferencesEndpoints.cs` and 029's `Property/Architectural/*Endpoints.cs` are plural.
  - 029's endpoints authorize through `Property/Architectural/ResidentArcScope.cs`. That file must call `ResidentAccess.RequireAsync` and must no longer read the claim itself.
  - No file under `Features/` may contain `Guid.Parse(User.FindFirst(`.
- [X] T037 Implement `ResidentAccess.RequireAsync(user, ResidentOperation, ct)` in `HOAManagementCompany/Features/Residents/ResidentAccess.cs`. It returns `ResidentContext(PropertyId, CommunityId, CommunityStatus)`, using R8's query and outcomes. `ResidentOperation` is `Read`, `ReadHistory` or `Write`. Add a Repowise `domain=residents` marker and register it as scoped in `Program.cs`.
- [X] T038 Adopt `ResidentAccess` in the payment endpoints, using each one's operation from the contract table and the returned `PropertyId`/`CommunityId` in place of token claims:
  - `HOAManagementCompany/Features/Payments/LedgerEndpoint.cs` and `DraftsEndpoint.cs`;
  - `OneTime/ConfirmPaymentEndpoint.cs`, `CreateIntentEndpoint.cs`, `PaymentOptionsEndpoint.cs`, `ReceiptEndpoint.cs` and `TransactionsEndpoint.cs`;
  - `Recurring/RecurringDeleteEndpoint.cs`, `RecurringGetEndpoint.cs`, `RecurringUpsertEndpoint.cs` and `SetupIntentEndpoint.cs`;
  - `Statements/StatementsEndpoint.cs` and `UnpaidAssessmentsEndpoint.cs`;
  - `Alerts/AlertPreferencesEndpoints.cs`.

  Check that `StatementService` and `RecurringDraftService` take ids from their callers, not from claims.
- [X] T039 [P] Adopt `ResidentAccess` the same way in the other resident endpoints, removing any raw `Guid.Parse` claim reads. Do this together with T038 until T035 and T036 pass.
  - `HOAManagementCompany/Features/Property/`: `AddressHistoryEndpoint.cs`, `DirectoryFieldPatchEndpoint.cs`, `DirectoryFieldsEndpoint.cs`, `OwnerGetEndpoint.cs`, `OwnerPatchEndpoint.cs`, `PropertyEndpoint.cs`.
  - `HOAManagementCompany/Features/Community/`:
    - `Announcements/AnnouncementGetEndpoint.cs` and `Announcements/AnnouncementsListEndpoint.cs`;
    - `Directory/CommunityDirectoryEndpoint.cs`;
    - `Documents/DocumentDownloadEndpoint.cs` and `Documents/DocumentsEndpoint.cs`;
    - `Events/EventRsvpEndpoint.cs` and `Events/EventsEndpoint.cs`;
    - `Poll/PollGetEndpoint.cs` and `Poll/PollVoteEndpoint.cs`;
    - `Violations/ViolationsEndpoint.cs`.
  - `HOAManagementCompany/Features/Dashboard/DashboardEndpoint.cs`.
- [X] T040 [P] Adopt `ResidentAccess` in spec 029's resident ARC endpoints (merged after this plan was written), through their single authorization point `HOAManagementCompany/Features/Property/Architectural/ResidentArcScope.cs`. Do this with T038 and T039 until T035 and T036 pass.
  - Replace the static `PropertyId(user)` claim read with an async `RequireAsync(user, op, ct)` that calls `ResidentAccess.RequireAsync` and pins drafts and applications to the returned `PropertyId`.
  - The operation is:
    - `ReadHistory` for submitted requests (the list, the detail and attachment links), so they stay readable after an archive (FR-012a);
    - `Read` for draft reads;
    - `Write` for draft changes, uploads, submit, info replies, withdraw and revise.
  - Update its callers in `DraftEndpoints.cs`, `DraftAttachmentEndpoints.cs`, `ApplicationEndpoints.cs` and the `ResidentArc*Service` classes.
  - In 029's `HOAManagementCompany.Tests/Integration/Property/Architectural/ResidentArcScopeStaticAnalysisTests.cs`, the `RequirePropertyId()` assertion becomes `ResidentAccess.RequireAsync`.
  - Every other test under `HOAManagementCompany.Tests/Integration/Property/Architectural/` stays green.

### Sign-in without a home (FR-036a, FR-036b; research R11)

- [X] T041 [P] Write `SignInWithoutHomeTests` first, against `/auth/login`, `/auth/refresh`, `/auth/board-mode`, `/auth/switch-property` and `/auth/me`. File: `HOAManagementCompany.Tests/Integration/Auth/SignInWithoutHomeTests.cs`
  - **US9-S1**: a Company Administrator with no home and no membership signs in with 200.
    - The token has no `propertyId` or `communityId`.
    - `/auth/me` returns `isCompanyAdministrator: true` and `landing: "Company"`.
  - **US9-S2**: an effective Community Manager of an Onboarding community, with no home, signs in.
    - `landing` is `"Board"` and `/auth/board-mode` succeeds.
    - `memberships` lists the community with `communityStatus: "Onboarding"`.
  - **US9-S3**: no home, no membership and no grant → 200 and `landing: "NoHome"`.
  - **US9-S5**: homes only in Onboarding communities → `landing: "NotLive"` and empty `properties`.
  - **US9-S6**: homes in an Active and an Archived community → the token's `propertyId` is the Active home, and `properties` lists both with their `communityStatus`.
  - **Refresh**: refresh succeeds and rotates the token for a user with no home. A failure inside the refresh transaction leaves the old refresh token usable.
  - **Switch property**: `/auth/switch-property` to an Onboarding home → 409 `COMMUNITY_NOT_ACTIVE`; to an Archived home → 200.
  - **Archived communities**: `memberships` and `/me/communities` leave them out (FR-036b).
- [X] T042 Change `HOAManagementCompany/Features/Auth/AuthService.cs` per R11, until T041 passes and `LoginLogoutTests`, `RefreshSwitchTests` and `BoardModeEndpointTests` still pass (update their `NO_PROPERTY` expectations to the new contract).
  - `FindUsablePropertyAsync` replaces `GetActivePropertyAsync`: Active first, then Archived, oldest link first, never Onboarding.
  - `CreateTokenPairAsync(user, Property?)` omits the property claims when the property is null.
  - Refresh deletes the old token and mints the new one in one transaction.
  - `SwitchModeAsync` and `SwitchPropertyAsync` follow the contract.
  - `BuildCurrentUserAsync` fills `properties[].communityStatus`, `memberships[].communityStatus`, `isCompanyAdministrator` and `landing` (from `LandingRule`).
  - Also update `HOAManagementCompany/Features/Auth/Models/AuthModels.cs`, and `HOAManagementCompany/Features/Board/MyCommunitiesEndpoint.cs` and `BoardModeEndpoint.cs` (Onboarding included, Archived excluded).
- [X] T043 Remove the staff co-residence workaround from `HOAManagementCompany/Seed/AuthSeeder.cs`, since board and manager users no longer need a home to sign in. Update `HOAManagementCompany.Tests/Integration/Seed/SeederTests.cs`.
- [X] T044 [P] Frontend sign-in state:
  - `neko-hoa/src/app/core/models/index.ts` gains the new DTO fields;
  - `neko-hoa/src/app/core/services/auth.service.ts` keeps `properties`, `isCompanyAdministrator` and `landing` from `/auth/me`;
  - `neko-hoa/src/app/core/services/landing.ts` follows the server's `landing`: `Company` → `/app/company/portfolio`, `NoHome` → `/app/no-home`, `NotLive` → `/app/not-live`;
  - `neko-hoa/src/app/core/guards/board.guard.ts` counts Onboarding memberships (FR-036b).

  Update `auth.service.spec.ts`, `landing.spec.ts` and `board.guard.spec.ts` for the five landings and the Onboarding case.

### Invitation core (FR-022, FR-022a, FR-041b, FR-041c; research R6, R12)

- [X] T045 [P] Write `InvitationStateTests` first. A pure `[Theory]` over state (Scheduled, Pending, Expired, Accepted, Revoked) × action (accept, resend, revoke, activate, archive). Each case gives the next state or the refusal code, exactly as in data-model.md's transition table. File: `HOAManagementCompany.Tests/Unit/Company/InvitationStateTests.cs`
- [X] T046 [P] Add `InvitationTokens` in `HOAManagementCompany/Features/Invitations/InvitationTokens.cs`:
  - a token is 32 random bytes, base64url-encoded (43 characters);
  - `Hash` is `AuthCrypto.Hash`;
  - the link builder makes `{App:BaseUrl}/invite#t={token}`.

  Unit test the length, the alphabet and that the token sits in the fragment: `HOAManagementCompany.Tests/Unit/Company/InvitationTokensTests.cs`
- [X] T047 [P] Write `InvitationLifecycleTests` first, using `CompanyAdministrator` invitations so the core is tested on its own. US2, US7 and US8 test the kind-specific offers. File: `HOAManagementCompany.Tests/Integration/Invitations/InvitationLifecycleTests.cs`
  - **Create** stores only the token hash and sets `SentAt` and `ExpiresAt` (72 hours later) from `TestClock`. It sends one email through `FakeAlertProvider` whose body holds the link, and records one `InvitationDelivery`.
  - **A failed send** leaves the invitation Pending with `LastSendError`. A later successful resend clears it.
  - **Resend** gives a new hash, a new 72 hours and the resender as sender. The old token is refused with `INVITATION_INVALID`.
  - **The refusal order on accept**:
    1. unknown, accepted or revoked → 410 `INVITATION_INVALID`, even when old;
    2. Pending past `ExpiresAt` → 410 `INVITATION_EXPIRED`;
    3. a sender who lost authority → the invitation is revoked (`SenderLostAuthority`) and the response is 410 `INVITATION_INVALID`;
    4. a signed-in account with a different email → 403 `INVITATION_EMAIL_MISMATCH`, with no invited email in the body, and the invitation stays Pending.
  - **Accept and revoke at the same moment**: exactly one wins, and the loser is told the final state (spec Edge Cases).
  - **The per-recipient cap**: the sixth send to one email within 24 hours → 429 `RATE_LIMITED`. A resend inside the 60-second cooldown → 429. The activation fan-out bypasses the cap (FR-041c).
  - **Supersede**: a new invitation for a target whose open invitation has expired first revokes the old one as `Superseded`.
  - **Logging**: every change logs one `InvitationChange` and every refusal one `InvitationAcceptFailed`. No rendered event property contains the token, an email or a name.
- [X] T048 Implement `InvitationService` in `HOAManagementCompany/Features/Invitations/InvitationService.cs`, with an `IInvitationOfferHandler` per `InvitationKind` in `IInvitationOfferHandler.cs`, until T045 and T047 pass.
  - **Operations**: create, send, resend, revoke, preview lookup, accept, and the activation fan-out entry point.
  - **Caps**: counted against `InvitationDeliveries`.
  - **Locking**: a `SELECT … FOR UPDATE` row lock per invitation, inside `CommunityLocks` or `CompanyLocks`.
  - **Handler interface**: `ValidateCreateAsync`, `SenderStillAuthorizedAsync`, `ApplyAsync`, `DescribeAsync`.
  - **Delivery**: send directly through the email `IAlertProvider`, never the outbox (R6).
  - Add a Repowise `domain=invitations` marker.
- [X] T049 [P] Add `InvitationEmailRenderer` in `HOAManagementCompany/Features/Invitations/InvitationEmailRenderer.cs`.
  - It renders the three direct-send templates in `contracts/emails.md`.
  - Expiry is shown in the community's time zone, or the company default's for Company Administrator invitations.
  - Bodies are never logged.
  - Test (`HOAManagementCompany.Tests/Unit/Company/InvitationEmailRendererTests.cs`): subjects, body fields, the local expiry text, and the setup-step sender line.
- [X] T050 Add `SenderAuthority` in `HOAManagementCompany/Features/Board/SenderAuthority.cs`:
  - `CouldSendAsync(userId, invitation)` re-runs `DecideAsync`, or the company grant check, for the invitation's capability. A setup-step invitation with no sender always passes.
  - `RevokeOrphanedAsync(userId, communityId?, cause)` revokes, in the caller's transaction, every Scheduled or Pending invitation the user can no longer send. Each is logged with `SenderLostAuthority`.
- [X] T051 Implement the `CompanyAdministrator` offer handler in `HOAManagementCompany/Features/Invitations/CompanyAdministratorOfferHandler.cs`. Applying it creates a live grant with `Source = Invitation` and the `InvitationId`. It does nothing when a live grant already exists.
- [X] T052 [P] Write `InvitationEndpointsTests` first, for `POST /invitations/preview`, `/invitations/register` and `/invitations/accept`, using the `CompanyAdministrator` kind. File: `HOAManagementCompany.Tests/Integration/Invitations/InvitationEndpointsTests.cs`
  - **Preview**:
    - a valid link returns the contract body;
    - `invitedEmail` and `hasAccount` are set only for an anonymous caller, and are `null` for a signed-in one (FR-022a);
    - an invalid or expired link returns only its code.
  - **Register**:
    - creates the account for the invited email with `EmailConfirmed = true` and no claim code;
    - applies the offer and marks the invitation Accepted;
    - returns `AuthResponse`, with the refresh cookie and `user.landing`.
  - **Register for an email that already has an account** → the generic 422 `REGISTRATION_FAILED`, and the invitation stays Pending.
  - **Accept** applies the offer to the caller. Accepting while already holding the grant returns `alreadyHeld: true` and duplicates nothing.
  - **All three** use the `auth` rate limit (a burst gets 429) and set `no-store`.
- [X] T053 Implement `InvitationPreviewEndpoint`, `InvitationRegisterEndpoint` and `InvitationAcceptEndpoint` in `HOAManagementCompany/Features/Invitations/`, until T052 passes. They use the `auth` rate limit. Register reuses the Identity password rules and the `AuthService` sign-in.
- [X] T054 [P] Let the dev tools capture invitation links.
  - Add an `IInvitationLinkObserver` that `InvitationService` calls after each send.
  - Its vault-backed implementation is registered only when `DevTools:E2ECleanupEnabled` is true, and never in Production (`E2ETestSupportGate`).
  - Expose the links through `E2EAuthCodesEndpoint`, so Playwright and the quickstart can read them without SES.
  - Files: `HOAManagementCompany/Features/DevTools/AuthCodeVault.cs` and `E2EAuthCodesEndpoint.cs`.
  - Test (`HOAManagementCompany.Tests/Integration/DevTools/InvitationLinkVaultTests.cs`): links are captured when enabled, and the observer isn't registered when disabled.
- [X] T055 [P] Add the frontend `InvitationService` (`preview`, `register`, `accept`) in `neko-hoa/src/app/core/services/invitation.service.ts`. Its spec, `invitation.service.spec.ts`, uses HttpTestingController to show the token is only ever in a request body, never in a URL.
- [X] T056 [P] Write `neko-hoa/src/app/features/invitations/invitation-page.component.spec.ts` first (Angular Testing Library):
  - reads `#t=` and removes it with `history.replaceState`;
  - a valid link shows the community, the offer and the sender;
  - which action it shows:
    - "Create account", with the fixed email, for an anonymous visitor with no account;
    - "Sign in to accept" when `hasAccount`;
    - "Accept" for a signed-in visitor;
  - register enforces the password rules;
  - the expiry is shown in the community's time zone, from `timeZoneId` (FR-022);
  - `INVITATION_EXPIRED`, `INVITATION_INVALID` and `INVITATION_EMAIL_MISMATCH` show brief 10's copy, and the mismatch never shows an email;
  - `REGISTRATION_FAILED` says to sign in and accept;
  - on success it routes by `landing`.
- [X] T057 Implement `InvitationPageComponent` at `/invite` in `neko-hoa/src/app/features/invitations/invitation-page.component.ts`, and add the public route in `neko-hoa/src/app/app.routes.ts`. It is public, follows brief 10, and works at phone width.
  - It sets a `Referrer-Policy: no-referrer` meta tag while shown.
  - Make `neko-hoa/src/app/core/observability/otel.bootstrap.ts` strip URL fragments from every span URL attribute. The Angular app has no Sentry SDK; its client telemetry is OpenTelemetry.
  - Test the stripping in `otel.bootstrap.spec.ts`.

### Setup step and seed (FR-038; research R18)

- [X] T058 [P] Write `SetupStepTests` first. Each test runs against its own fresh database, because the step reads the deployment's single company. File: `HOAManagementCompany.Tests/Integration/Company/SetupStepTests.cs`
  - **US7-S6**, no administrator exists:
    - when the email has an account: a live grant with `Source = Setup`; `CompanyAdminChange Actor=setup Change=Granted` is logged; stdout says `Granted Company Administrator to <userId>.`; exit 0;
    - when it doesn't: a `CompanyAdministrator` invitation is sent through `FakeAlertProvider`; stdout says `Invitation sent; it expires …`; exit 0.
  - **US7-S7**: a second run prints `A Company Administrator already exists; nothing changed.` and exits 0. It writes nothing: grant, invitation and outbox counts are unchanged.
  - `--company-name` sets the company's name.
  - An invalid email exits 2.
- [X] T059 Add `CompanyAdministratorService.SetupAsync(email, recover, companyName)` in `HOAManagementCompany/Features/Company/CompanyAdministratorService.cs`, until T058 passes. This task builds the non-recovery path; T139 adds recovery.
  - Add the `--grant-company-admin <email> [--recover] [--company-name "<name>"]` verb to `HOAManagementCompany/Infrastructure/Configuration/StartupTasks.cs`.
  - The verb applies migrations first and is allowed in every environment. It is never an HTTP route.
- [X] T060 Add the idempotent `CompanySeeder` in `HOAManagementCompany/Seed/CompanySeeder.cs`, for dev-like environments only, and call it from `HOAManagementCompany/Seed/DatabaseSeeder.cs`. Per quickstart.md it adds:
  - `company@nekohoa.dev`, created through `SetupAsync`;
  - the Onboarding community "Keystone Ridge", with `manager@nekohoa.dev` as its open-ended Community Manager and seeded properties.

  Test (`HOAManagementCompany.Tests/Integration/Seed/CompanySeederTests.cs`): a second run adds nothing, and the seeder doesn't run outside dev-like environments.

### Company area and Community settings page shell (MVP foundation d and e)

- [X] T061 [P] Add `CompanyService` and `CommunitySettingsService` in `neko-hoa/src/app/core/services/company.service.ts` and `community-settings.service.ts`, typed per the contracts, each with an HttpTestingController spec beside it.
  - **`CompanyService`**: communities, `createCommunity`, the lifecycle previews and actions, parent, defaults, administrators and Company Administrator invitations (both paged).
  - **`CommunitySettingsService`**: settings, profile, `patchProfile`, memberships, people, `replaceManager`, invitations, residents, the ARC settings and `resetArc`.
- [X] T062 [P] Add `companyAdminGuard` in `neko-hoa/src/app/core/guards/company-admin.guard.ts`, with `company-admin.guard.spec.ts`. It allows a user with `isCompanyAdministrator` and routes anyone else to their landing target.
- [X] T063 [P] Write specs first for the shared UI in `neko-hoa/src/app/shared/`:
  - **`disabled-reason.directive.spec.ts`**:
    - the reason shows on hover and on keyboard focus, tied by `aria-describedby`;
    - the control has `aria-disabled` and stays focusable;
    - Esc hides the reason.
  - **`autosave-field.directive.spec.ts`**:
    - saves on blur or change, only when the value changed;
    - shows "Saving…", then "✓ Saved";
    - on error, shows the server's message under the field and keeps the typed value;
    - announces politely.
  - **`toast-host.component.spec.ts`**: a polite live region that auto-dismisses.
  - **`property-drawer.component.spec.ts`**: opens beside the list, and full width below 768 px; moves focus in; Esc closes it and returns focus to the row.
- [X] T064 Implement `DisabledReasonDirective`, `AutosaveFieldDirective`, `ToastHostComponent` and `PropertyDrawerComponent` (the glossary-panel pattern) in `neko-hoa/src/app/shared/`, and `ToastService` in `neko-hoa/src/app/core/services/toast.service.ts`, until T063 pass
- [X] T065 [P] Write `CommunitySettingsShellTests` first, for `GET /communities/{id}/settings`. File: `HOAManagementCompany.Tests/Integration/Company/CommunitySettingsShellTests.cs`
  - **US5-S1** (shell): a Company Administrator with no membership gets `sections` = profile, managers-and-memberships, architectural-review, and every `can.*` that fits the status.
  - **US5-S2**: the community's Community Manager gets `changeParent`, `activate`, `archive` and `restore` false, and the other `can.*` true.
  - **US5-S3**: Board Member, Accountant, Resident and a Community Manager of another community get 403 `FORBIDDEN`, identical to a nonexistent id.
  - `sections` never contains `notifications` (FR-029).
  - `noManager` is null for non-administrators.
  - With no open-ended manager, `can.activate.reasonCode` is `NO_ACTIVE_MANAGER`.
- [X] T066 Implement `CommunitySettingsEndpoint` (`ViewCommunitySettings`, read, `no-store`) in `HOAManagementCompany/Features/Board/Settings/CommunitySettingsEndpoint.cs`, until T065 passes
- [X] T067 [P] Write `community-settings-page.component.spec.ts` and `settings-side-nav.component.spec.ts` first, in `neko-hoa/src/app/features/community-settings/`:
  - sections render from `sections[]`;
  - controls are hidden according to `can.*`;
  - a 403 routes to the landing page (US5-S3, frontend);
  - the side list's search filters section names and setting labels from the registry, and choosing a result opens the section and focuses the setting;
  - a Community Manager of an Onboarding community sees the "a Company Administrator must mark it Active" notice on Profile (FR-029);
  - below 768 px the side list collapses into a menu that keeps the search (constitution §6).
- [X] T068 Implement `CommunitySettingsPageComponent` (the header, the status shown as text, the side list and the section outlet) and `SettingsSideNavComponent` (search over a static registry of setting labels), following brief 04, in `neko-hoa/src/app/features/community-settings/`. Do this until T067 pass.
  - Add the routes to `neko-hoa/src/app/app.routes.ts`:
    - `/app/company/communities/:communityId/settings/:section`, with `companyAdminGuard`;
    - `/app/board/settings/:section`, with `boardGuard` and `requiredRoles: ['CommunityManager']`.
- [X] T069 [P] Extend the specs first.
  - **`neko-hoa/src/app/shell/shell.component.spec.ts`**:
    - under `/app/company/**`, the shell shows the company nav (Portfolio · Add community · Company defaults · Company administrators) and no board banner;
    - the Company menu appears in the account cluster only for Company Administrators;
    - the grant alone shows no board-mode control (FR-036).
  - **`board-navigation.service.spec.ts`**: the board nav gains "Community settings" and "Add community" for Community Managers.
- [X] T070 Implement the company-area state and the Company menu in `neko-hoa/src/app/shell/shell.component.ts`, and the new `BOARD_NAV` entries in `neko-hoa/src/app/core/services/board-navigation.service.ts` (brief 01), until T069 pass

**Checkpoint**: the migration applies, the resolver decides per FR-004a, resident endpoints check access on every request, a user with no home can sign in, invitations can be created, sent and accepted, the setup step works, and the settings page shell renders.

---

## Phase 3: User Story 1: Add a community the company has taken on (P1) 🎯 MVP

**Goal**: A Company Administrator or Community Manager adds a community, which starts Onboarding with the company defaults. A Community Manager becomes its first manager, and Company Administrators are emailed.

**Independent Test** (spec US1):
- As a Company Administrator, add Keystone Park. Confirm it is Onboarding, appears in the portfolio, and copied the defaults.
- Repeat as a Community Manager. Confirm the open-ended membership and the administrator emails.
- Confirm a duplicate name and a non-manager are both refused, with nothing created.

### Tests (write first)

- [X] T071 [P] [US1] Create `AddCommunityTests` in `HOAManagementCompany.Tests/Integration/Company/AddCommunityTests.cs`:
  - **US1-S1**: a Company Administrator adds Keystone Park with the scenario's fields.
    - The response is 201 with `status: "Onboarding"`.
    - The community has no parent and zero CommunityManager memberships, and it is listed by `GET /company/communities`.
    - Its `CommunityArcSettings` equal the company defaults field by field. Change the defaults to non-027 values first, so the copy is observable.
  - **US1-S2**: a Company Administrator adds "keystone park " → 409 `COMMUNITY_NAME_TAKEN`. The message contains "Keystone Park", and the community count is unchanged.
  - **US1-S3**: a Community Manager who doesn't manage it adds that name → 409 with exactly "That community name is unavailable." The body contains no other community's name or id.
  - **US1-S4**: an effective Community Manager adds Keystone Ridge.
    - It is Onboarding, and the caller holds an Active CommunityManager membership with a null `EndDate`.
    - `/me/communities` lists it on the next request.
    - There is one `co_community_added` outbox row, with the settings link, per Company Administrator of the company, and none for anyone else.
  - **US1-S5**: `[Theory]` over Board Member, Accountant, Resident and no role → 403 `FORBIDDEN`, and nothing is created.
  - **US1-S6**: a Community Manager whose only membership is in an Archived community → 403, and nothing is created.
  - **US1-S7**: a Company Administrator adds a community under "Keystone Crossing Master", so `ParentCommunityId` is set. A Board Member of the master gets 403 from the new community's board metrics.
  - **US1-S8**: a Community Manager of A only, choosing B as parent → 403 and nothing created. The body is identical whether B exists or is a random id.
  - **US1-S9**: `[Theory]` over each required field left empty, and a formation date of tomorrow → 422 `VALIDATION_ERROR`, with `errors[].field` naming exactly the fields at fault. Also the length limits: a legal name of 201 characters, community names of 1 and 81, a description of 2,001.
  - **Also**:
    - an archived parent → 409 `PARENT_ARCHIVED`;
    - `CommunityLifecycle Action=Created` is logged with IDs only.
- [X] T072 [P] [US1] Create `PortfolioEndpointTests`, the minimal list that US6 extends. File: `HOAManagementCompany.Tests/Integration/Company/PortfolioEndpointTests.cs`
  - A Company Administrator sees only their own company's communities, with name, legal name, status and parent.
  - The order is A–Z by `NormalizedName`.
  - The page size defaults to 25, and `limit=101` is clamped to 100.
  - The response is `no-store`.
  - A Community Manager gets 403 `FORBIDDEN`. This is US6-S6's backend half; it lives here because the endpoint is gated from day one.
- [X] T073 [P] [US1] Create `add-community.component.spec.ts` and the minimal `portfolio.component.spec.ts` in `neko-hoa/src/app/features/company/`:
  - the form (brief 03) labels every field and ties each error to its field (US1-S9);
  - it submits the contract body;
  - it shows the `COMMUNITY_NAME_TAKEN` message under Community name (US1-S2, US1-S3);
  - the parent picker lists only the caller's managed communities for a Community Manager (from `/me/communities`, role CommunityManager), and non-archived communities for a Company Administrator (US1-S8);
  - after a 201 it routes to `settingsPath` and shows a toast;
  - the portfolio lists name, status (as text) and a settings link (brief 02).

### Implementation

- [X] T074 [US1] Implement `CommunityCreateEndpoint` (`POST /communities`) per `contracts/company.md` in `HOAManagementCompany/Features/Company/CommunityCreateEndpoint.cs`, until T071 passes. Rate limit: `board-writes`.
  - **Check**: `CreateCommunity`.
  - **Parent rules**:
    - a Company Administrator may choose any non-archived community of the company;
    - a Community Manager's choice must pass `DecideAsync(parent, ManageCommunityManagers, Read)`, or the request gets the 403 body.
  - **One transaction**:
    - insert the community (Onboarding, with `CreatedByUserId`);
    - insert its `CommunityArcSettings`, copied from `CompanyDefaultSettings` (FR-009);
    - for a non-administrator, insert the open-ended CommunityManager membership (FR-008).
  - **Name conflicts**: a unique-violation becomes `COMMUNITY_NAME_TAKEN`, with FR-007's message rule.
  - **After commit**: enqueue `co_community_added` for each Company Administrator and dispatch inline.
- [X] T075 [US1] Implement `PortfolioEndpoint` (`GET /company/communities`, `ViewPortfolio`) in `HOAManagementCompany/Features/Company/PortfolioEndpoint.cs`, until T072 passes. This first version has name, legal name, status, parent, paging and A–Z order only; US6 adds the rest.
- [X] T076 [US1] Implement `AddCommunityComponent` (company and board variants; brief 03) and the minimal `PortfolioComponent` (brief 02) in `neko-hoa/src/app/features/company/`, until T073 pass. Add the routes `/app/company/portfolio`, `/app/company/communities/new` and `/app/board/communities/new` to `neko-hoa/src/app/app.routes.ts`.

**Checkpoint**: communities can be created in the product, with defaults copied, and the company area lists them.

---

## Phase 4: User Story 2: Appoint and change a community's managers (P1)

**Goal**: Company Administrators and Community Managers appoint, end, downgrade and replace managers, directly or by invitation. A community never loses its last open-ended manager.

**Independent Test** (spec US2):
- Invite a new email as manager of a new community, accept within 3 days by creating an account from the link, and land in board mode.
- Let a second invitation pass 3 days, and confirm its link is refused.
- Try to end, or end-date, the only manager, and confirm it is refused.

### Tests (write first)

- [X] T077 [P] [US2] Create `ManagerAppointmentTests` in `HOAManagementCompany.Tests/Integration/Company/ManagerAppointmentTests.cs`:
  - **US2-S1** (send): a Company Administrator appoints pat@example.com in a new community.
    - The response is 201 `{ invitationId, state: "Pending", expiresAt }`.
    - One email goes to Pat through `FakeAlertProvider`.
    - `GET /communities/{id}/invitations` lists the invitation as Pending.
  - **US2-S3**: a Community Manager of A appoints Board Member Lee, chosen from `GET /communities/{id}/people`.
    - The response is a 201 `MembershipDto`: CommunityManager, Active and open-ended at once.
    - No invitation row is created, and A now has two open-ended managers.
  - **US2-S4**: appointing has-account@ and no-account@ by email gives responses identical apart from ids and times, with no name. Both are emailed.
  - **US2-S8**: a Community Manager of A appointing in B → 403, with the same body as for a nonexistent community.
  - **US2-S11** (replace, `POST /managers/replace`): a Company Administrator replaces Pat with Lee.
    - Lee becomes open-ended and Pat's membership ends.
    - A forced failure after the grant leaves both unchanged.
    - The log has `replace-grant` and `replace-end` with one shared `Cause`.
    - A replacement who isn't in A → 422, and Pat stays active.
    - A Community Manager calling replace → 403.
  - **US2-S16** (send): a Community Manager invites new@ as Board Member with end date 2027-12-31. The Pending invitation has that `TermEndDate`.
  - **US2-S17**: a Community Manager appoints themself as Board Member by `userId`, or invites their own email (in any case, with spaces) → 422 `SELF_GRANT_NOT_ALLOWED`, and nothing is created.
  - **US2-S18**: `[Theory]` over Board Member, Accountant and Resident → 403 on creating, and on ending, a CommunityManager membership.
  - **Also**:
    - a direct appointment of someone not in the people list → 422 with "Choose someone from this community, or invite them by email.", for both a real outsider and a random id;
    - re-appointing a former manager reactivates the old row (FR-019);
    - `ALREADY_MEMBER` for a current holder;
    - `INVITATION_PENDING`, with `invitationId`, for a duplicate open invitation.
- [X] T078 [P] [US2] Create `ManagerEndAndDowngradeTests` in `HOAManagementCompany.Tests/Integration/Company/ManagerEndAndDowngradeTests.cs`:
  - **US2-S9**: ending the only open-ended manager → 422 `LAST_MANAGER`, and the manager stays Active.
  - **US2-S10**: giving that manager the end date 2026-12-31 → 422, and `EndDate` stays null.
  - **US2-S12**: a Community Manager ends their own membership while another open-ended manager exists. `EndDate` is today, and their next `GET /communities/{id}/memberships` → 403.
  - **US2-S13**: a co-manager who also holds Board Member is downgraded.
    - Their manager row ends and their Board Member row is Active.
    - Their next manager request gets 403; their next board request gets 200.
    - Downgrading a co-manager with no Board Member row creates one.
  - **Also**:
    - ending or downgrading a manager revokes the Pending invitations they can no longer send (`SenderLostAuthority`, logged; this sets up US2-S15);
    - the list returns `canEnd`, `canDowngrade` and `canEditEndDate` with `reasonCode: "LAST_MANAGER"`.
- [X] T079 [P] [US2] Create `ManagerConcurrencyTests` in `HOAManagementCompany.Tests/Integration/Company/ManagerConcurrencyTests.cs`.
  - **US2-S14**: two open-ended managers end each other at once, synchronized by a barrier with `Task.WhenAll`.
  - Over 20 iterations, assert exactly one 200 and one 422 `LAST_MANAGER`, and one open-ended manager left every time.
- [X] T080 [P] [US2] Create `MembershipInvitationAcceptanceTests` in `HOAManagementCompany.Tests/Integration/Invitations/MembershipInvitationAcceptanceTests.cs`:
  - **US2-S1** (accept): Pat accepts within 72 hours. Pat holds an Active CommunityManager membership with `StartDate` = the acceptance date and a null `EndDate`.
  - **US2-S2**: Pat registers from the link with names and a password, and no claim code.
    - The account is for pat@ with `EmailConfirmed`, and the membership exists.
    - `landing` is `"Board"`, and `/auth/board-mode` for that community succeeds without a home.
  - **US2-S5**: the link is used 72 hours and 1 minute after sending (`TestClock`) → 410 `INVITATION_EXPIRED`, and no membership is created.
  - **US2-S6**: resending a Pending and an Expired invitation emails a new link valid for 72 hours. The old token → 410 `INVITATION_INVALID`.
  - **US2-S7**: bob@, signed in, accepts ann@'s invitation → 403 `INVITATION_EMAIL_MISMATCH`.
    - The body doesn't contain "ann@".
    - No membership is created, and the invitation stays Pending.
  - **US2-S15**: co-manager X sends an invitation, X is removed as manager, and the link is used → 410 `INVITATION_INVALID`, with no membership.
    - The invitation was revoked when X was removed.
    - Also cover a sender who lost authority some other way: the check at acceptance catches it.
  - **US2-S16** (accept): new@ accepts and holds Board Member, Active from the acceptance date to 2027-12-31. A term end date that has already passed at acceptance → 410 `INVITATION_INVALID`.
  - **Also**:
    - an end-dated CommunityManager invitation is refused at acceptance with `LAST_MANAGER` when no other open-ended manager remains;
    - accepting while already holding the role returns `alreadyHeld: true` and duplicates nothing.
- [X] T081 [P] [US2] Create `MembershipInvitationsListTests` for `GET /communities/{id}/invitations` and its resend and revoke routes. File: `HOAManagementCompany.Tests/Integration/Company/MembershipInvitationsListTests.cs`
  - **US5-S8**: a Pending Board Member invitation and an Expired Community Manager invitation are both listed. Each has email, role, sent by, sent at, expires at and `effectiveState`, with `canResend` and `canRevoke` true.
  - **Also**:
    - revoke gives Revoked (`Manual`), and its link → 410;
    - resending an Accepted or Revoked invitation → 410 `INVITATION_INVALID`;
    - a child id from another community → 403;
  - **SC-005 rows**: add membership create and update, and invitation resend and revoke, to the `ArchivedWriteEndpoints` registry of T095.
- [X] T082 [P] [US2] Create `MembershipSensitiveEventTests` in `HOAManagementCompany.Tests/Integration/Company/MembershipSensitiveEventTests.cs`. For **US2-S19**:
  - **Actions covered**: appoint (direct and by email), end-date change, end, downgrade, replace, invitation, resend, revoke, accept, and a failed accept.
  - **Each change** logs exactly one event, with actor, community, the target user id (or invitation id), the change and UTC time.
  - **No event** renders an email address, a name or the token.
- [X] T083 [P] [US2] Create `managers-memberships.component.spec.ts` in `neko-hoa/src/app/features/community-settings/` (brief 05):
  - managers, board and accountants are listed with dates, and "open-ended" where it applies;
  - pending invitations are listed with Resend and Revoke;
  - the appoint dialog has the people picker and the email path, which shows "pending" and never a name;
  - End and Downgrade are disabled with the `LAST_MANAGER` reason, through `DisabledReasonDirective`;
  - Replace is offered only to Company Administrators;
  - refusals use `refusalMessage`.

### Implementation

- [X] T084 [US2] Implement the `Membership` offer handler in `HOAManagementCompany/Features/Invitations/MembershipOfferHandler.cs`:
  - validate the role and the term;
  - check sender authority through `ManageCommunityManagers` or `ManageMemberships`;
  - applying it creates or reactivates the membership, with `ManagerRule` checked at acceptance.
- [X] T085 [US2] Extend `HOAManagementCompany/Features/Board/MembershipCreateEndpoint.cs` per the contract:
  - `userId` or `email`, with the capability chosen by role;
  - the people-list check and the self-grant check;
  - `ManagerRule` under `CommunityLocks`, and reactivation of a previous row;
  - the email path through `InvitationService`, with `INVITATION_PENDING` and `ALREADY_MEMBER`;
  - `DecideAsync` with `SendDecisionAsync`;
  - log `BoardMembershipChange Kind=grant|reactivate`.
- [X] T086 [US2] Extend `HOAManagementCompany/Features/Board/MembershipUpdateEndpoint.cs`:
  - accept `end`, `downgradeTo` and `endDate`;
  - `ManagerRule` under the lock, replacing today's racy count at lines 48–64;
  - downgrade per R7;
  - `SenderAuthority.RevokeOrphanedAsync` in the same transaction;
  - while Archived, only a Company Administrator's `end` is allowed;
  - logging.
- [X] T087 [P] [US2] Extend `HOAManagementCompany/Features/Board/MembershipsListEndpoint.cs`:
  - add `openEnded`, `effective`, `canEnd`, `canDowngrade`, `canEditEndDate` and `reasonCode`;
  - allow it while Archived for Company Administrators.
- [X] T088 [US2] Add `PeopleEndpoint` and `ReplaceManagerEndpoint` in `HOAManagementCompany/Features/Board/Settings/` per `contracts/community-settings.md`
- [X] T089 [US2] Add `InvitationsListEndpoint`, `InvitationResendEndpoint` and `InvitationRevokeEndpoint` (community routes; the capability depends on the invitation's kind) in `HOAManagementCompany/Features/Board/Settings/`. Do this with the other US2 implementation tasks until T077, T078, T079, T080, T081 and T082 pass.
- [X] T090 [US2] Implement `ManagersMembershipsSectionComponent`, with its appoint dialog, invitation list and replace dialog, in `neko-hoa/src/app/features/community-settings/`, until T083 pass
- [X] T091 [US2] Retire `neko-hoa/src/app/features/board/membership-admin/` (the component and its spec).
  - `/app/board/memberships` now redirects to `/app/board/settings/managers-and-memberships`.
  - Move any still-relevant assertions into the new section's spec.
  - Update `HOAManagementCompany.Tests/Integration/Board/MembershipAdminEndpointsTests.cs` for the extended 025 endpoints.

**Checkpoint**: a community can get its first manager by invitation, and the open-ended-manager rule holds under concurrent requests.

---

## Phase 5: User Story 3: Take a community live, edit its profile, and archive or restore it (P1)

**Goal**: Company Administrators move communities through Onboarding, Active and Archived, and restore them. Archiving cancels autopay and notifies residents. Managers edit the profile one field at a time.

**Independent Test** (spec US3):
1. Create a community (Onboarding). A linked resident can't use it.
2. Appoint a manager and mark it Active. The resident can now use it.
3. Seed autopay on three properties and archive the community. Confirm:
   - autopay is cancelled;
   - residents are emailed;
   - writes are refused;
   - payment history is still readable;
   - the records stay readable to the Company Administrator.
4. Restore it. Confirm memberships and settings are unchanged.

### Tests (write first)

- [X] T092 [P] [US3] Create `LifecycleActivateTests` in `HOAManagementCompany.Tests/Integration/Company/LifecycleActivateTests.cs`:
  - **US3-S3**: a Company Administrator activates an Onboarding community that has an open-ended manager and two Scheduled residency invitations.
    - It becomes Active, with `ActivatedAt` set.
    - Both invitations become Pending with `SentAt` = now, and are emailed. The response has `invitationsSent: 2`.
    - A linked resident's next `/auth/me` lists the home.
    - A scheduled invitation whose sender has lost authority is revoked instead of sent.
  - **US3-S4**: with no open-ended manager → 409 `NO_ACTIVE_MANAGER`, and it stays Onboarding.
    - The preview returns `allowed: false` with that code.
    - `GET /settings` has `can.activate.reasonCode = NO_ACTIVE_MANAGER`.
  - **Also**: activating an Active community → 409 `INVALID_TRANSITION`, and a Community Manager → 403 (US3-S20).
- [X] T093 [P] [US3] Create `OnboardingCommunityWorkTests` in `HOAManagementCompany.Tests/Integration/Company/OnboardingCommunityWorkTests.cs`. For **US3-S2**, in an Onboarding community:
  - the Community Manager creates a Board Member membership (201) and saves ARC settings (200);
  - the Board Member reads metrics (200);
  - the Board Member saving ARC settings → 403 `FORBIDDEN`, the same as in an Active community.
- [X] T094 [P] [US3] Create `LifecycleArchiveTests` in `HOAManagementCompany.Tests/Integration/Company/LifecycleArchiveTests.cs`:
  - **US3-S5**: archiving an Active community with a pending manager invitation and a pending resident invitation:
    - it becomes Archived, and is gone from `/me/communities` for its manager and its Board Member;
    - both invitations are Revoked (`CommunityArchived`), and their links → 410 `INVITATION_INVALID`;
    - `GET /settings`, the profile, the memberships and the residents still return 200 to the Company Administrator;
    - no rows are deleted (counts before = after).
  - **US3-S7**: three properties with active autopay.
    - All three schedules become `inactive` with `community_archived`, and their authorizations get `TerminatedAt`.
    - `RecurringDraftService.RunDueDraftsAsync` starts no charge for them.
    - Each linked resident gets one `co_community_archived`. A resident with two homes gets one email naming both, saying autopay ended for the right properties.
    - One `AutopayCancelled` is logged per schedule.
  - **US3-S10**: ARC-1041 (Open) and ARC-1042 (Decision Reached) → 409 `OPEN_WORK`. `items` lists both labels with their statuses, the message names them, and nothing changes.
  - **US3-S11**: a master with a non-archived sub-association → 409 `OPEN_WORK`, listing it.
  - **Onboarding archive** (spec Edge Cases, SC-012): archiving an Onboarding community with two Scheduled resident invitations revokes both (`CommunityArchived`) and sends no email.
  - **Also**: the archive preview returns the counts and lists without changing anything, and a Community Manager → 403 (US3-S20).
- [X] T095 [P] [US3] Create `ArchivedCommunityWritesTheoryTests` (SC-005) in `HOAManagementCompany.Tests/Integration/Company/ArchivedCommunityWritesTheoryTests.cs`.
  - **Endpoints** (this story's rows): profile PATCH; parent change; ARC vote and info request; record outcome and resend outcome.
  - Keep the rows in an `ArchivedWriteEndpoints` registry (`MemberData`). US2, US5 and US8 add their own rows (T081, T129, T113), so this test passes at the end of US3.
  - **Authorized caller** → 409 `COMMUNITY_ARCHIVED`. This includes a Board Member voting (US3-S6, first half).
  - **User with no membership or home there** → 403 `FORBIDDEN`, identical to a nonexistent community (US3-S6, second half).
  - **Exception**: a Company Administrator ending the last manager's membership while archived → 200.
- [X] T096 [P] [US3] Create `LifecycleRestoreTests` in `HOAManagementCompany.Tests/Integration/Company/LifecycleRestoreTests.cs`:
  - **US3-S12**: it was Active and has an open-ended manager, so it returns to Active.
    - Memberships (with end dates) and ARC settings are equal before and after.
    - Autopay is still inactive, and revoked invitations are still Revoked.
  - **US3-S13**: archived while Onboarding, so it returns to Onboarding. A linked resident still gets `COMMUNITY_NOT_ACTIVE`.
  - **US3-S14**: it was Active, but the only manager's end date passed during the archive.
    - It returns to Onboarding with `reason: "NoOpenEndedManager"`, and the portfolio row has `noManager: true`.
    - Activating it → 409 `NO_ACTIVE_MANAGER`.
  - **US3-S15**: a Company Administrator ends M's manager membership while archived → 200, with no `LAST_MANAGER` check. After restore it is Onboarding, and M's next request in the community → 403.
  - **US3-S16**: a sub-association and its parent are both archived. Restoring the sub-association → 409 `PARENT_ARCHIVED`, naming the parent. Restoring the master doesn't restore its sub-associations.
  - **Name reservation** (spec Edge Cases): while archived, the community's name stays reserved, so another community can't take it and a restore never collides.
  - **Also**: the restore preview lists `membershipsRegainingAccess`, and a Community Manager → 403 (US3-S20).
- [X] T097 [P] [US3] Create `CommunityParentTests` for `PUT /parent` in `HOAManagementCompany.Tests/Integration/Company/CommunityParentTests.cs`:
  - a Company Administrator sets and clears the parent;
  - choosing the community's own descendant → 422 `PARENT_CYCLE`;
  - an archived parent → 409 `PARENT_ARCHIVED`;
  - a Community Manager → 403 (US3-S20);
  - `CommunityLifecycle Action=ParentChanged` is logged.
- [X] T098 [P] [US3] Create `CommunityProfileTests` in `HOAManagementCompany.Tests/Integration/Company/CommunityProfileTests.cs`:
  - **US3-S17**: a Community Manager PATCHes `{ county }`.
    - The response is 200, only county changes, and one `CommunityProfileChanged Field=County Old= New=` is logged.
    - Each invalid value → 422 with `errors[].field`, and nothing is saved: a future formation date, a 201-character legal name, clearing a filled required field.
  - **US3-S18**: renaming Keystone Ridge to " KEYSTONE PARK" → 409 `COMMUNITY_NAME_TAKEN`, and the name is unchanged.
  - **US3-S19**: a backfilled community with an empty legal name and county. PATCHing the description succeeds, and `missingRequiredFields` is legalName and county.
  - **US3-S20**: a Community Manager gets 403 on activate, archive, restore and parent (shared with the tests above).
  - **US3-S21**: a Community Manager of A only PATCHes B → 403, identical to a nonexistent id.
  - **Rename frees the old name** (spec Edge Cases): after Keystone Ridge is renamed Keystone Bluff, a new community can be created as "Keystone Ridge".
- [X] T099 [P] [US3] Create `ArchivedResidentHomeTests`. These are the backend cases beyond the R8 theory. File: `HOAManagementCompany.Tests/Integration/Residents/ArchivedResidentHomeTests.cs`
  - **US3-S8**: after the archive, Jane's `/auth/me` lists the home with `communityStatus: "Archived"`, and she signs in to it. Ledger, statements, receipts and transactions return 200, and so do her home's submitted architectural requests (list, detail, attachment link). An architectural draft returns 409 `COMMUNITY_ARCHIVED` (FR-012a).
  - **US3-S9**: with her token from before the archive, `PUT /payments/recurring` and `POST /payments/intent` → 409 `COMMUNITY_ARCHIVED`. No `RecurringPayment` or `PaymentTransaction` row is created.
  - **US3-S1** (claim code): `POST /auth/register` with a valid code for a property in an Onboarding community → 409 `COMMUNITY_NOT_ACTIVE`, after the proof and the code are checked. The code isn't used up, and no account or link exists.
- [X] T100 [P] [US3] Create `ArchivedCommunityProcessorWritesTests` (FR-012, spec Edge Cases "autopay mid-charge") in `HOAManagementCompany.Tests/Integration/Payments/ArchivedCommunityProcessorWritesTests.cs`, using `SignedWebhookFixture` and `FakeStripeGateway`. For a property in an Archived community:
  - a charge submitted before the archive settles: its succeeded webhook marks the transaction succeeded and posts the ledger entry;
  - a failed or returned payment, a refund and a dispute each still apply;
  - reconciliation still updates the property's transactions;
  - none of these is refused with `COMMUNITY_ARCHIVED`, and no new charge starts.
- [X] T101 [P] [US3] Create `RecurringCancellationTests` in `HOAManagementCompany.Tests/Integration/Payments/RecurringCancellationTests.cs`:
  - the owner's own cancel records `owner_cancelled`;
  - `CancelRecurringForAsync` sets `inactive`, the reason and `TerminatedAt`, and makes no `FakeStripeGateway` call;
  - a schedule left `active` in an Archived community never drafts.
- [X] T102 [P] [US3] Write the US3 frontend specs first (briefs 04 and 11):
  - **`profile-section.component.spec.ts`** in `neko-hoa/src/app/features/community-settings/`:
    - per-field save shows "✓ Saved" (US3-S17), and a failing field shows its error under the field (US3-S17);
    - missing required fields are flagged (US3-S19);
    - status and parent are read-only for a Community Manager;
    - Mark Active is disabled, with its reason on hover and on keyboard focus (US3-S4).
  - **`lifecycle-dialogs.component.spec.ts`**:
    - the archive dialog shows the preview's counts and the `OPEN_WORK` list (US3-S10, US3-S11);
    - the restore dialog lists the memberships that regain access, each with an End action (FR-015), and says where the community returns to (US3-S12 to US3-S14).
  - **The resident side**:
    - `shell.component.spec.ts`: an archived home is labelled "No longer managed" in the home list (US3-S8);
    - `neko-hoa/src/app/shared/archived-notice.component.spec.ts`: a 409 `COMMUNITY_ARCHIVED` on a resident page shows the archived notice instead of the page.

### Implementation

- [X] T103 [US3] Add `PaymentService.CancelRecurringForAsync(propertyId, reason, ct)` in `HOAManagementCompany/Features/Payments/PaymentService.cs` (R9), until T101 passes.
  - It sets `inactive`, `CancellationReason` and `TerminatedAt`, and makes no Stripe calls.
  - The existing owner cancel now records `owner_cancelled`.
  - Add the Active-community filter to `RunDueDraftsAsync` in `HOAManagementCompany/Features/Payments/Recurring/RecurringDraftService.cs`. Leave the webhook, refund, dispute and reconciliation paths unfiltered, until T100 passes too.
- [X] T104 [US3] Implement `CommunityLifecycleService` in `HOAManagementCompany/Features/Company/CommunityLifecycleService.cs`, per R4 and `contracts/company.md`. Add a Repowise `domain=company` marker.
  - **Operations**: activate, archive, restore and parent, plus the three previews. Each runs under `CommunityLocks`, in one transaction.
  - **Open work**: 027 applications that are Open or DecisionReached, and sub-associations that aren't archived.
  - **Archive cascade**:
    - revoke the invitations;
    - cancel autopay through `CancelRecurringForAsync`;
    - enqueue `co_community_archived` per linked resident, dispatched inline in at most 10 batches of 100.
  - **Activate**: the fan-out of scheduled invitations goes through `InvitationService`.
- [X] T105 [US3] Add `LifecyclePreviewEndpoint`, `LifecycleActivateEndpoint`, `LifecycleArchiveEndpoint`, `LifecycleRestoreEndpoint` and `LifecycleParentEndpoint` in `HOAManagementCompany/Features/Company/`. They use `ManageCommunityLifecycle` and `board-writes`.
- [X] T106 [US3] Add `ProfileGetEndpoint` and `ProfilePatchEndpoint` in `HOAManagementCompany/Features/Board/Settings/` per R14, until T098 passes
- [X] T107 [US3] Switch the existing board endpoints to `DecideAsync` with `BoardHttp.SendDecisionAsync` and the right operation, so an Archived community answers its members with `COMMUNITY_ARCHIVED`. Do this until T095, T093, T092, T094, T096 and T097 pass.
  - The endpoints are `HOAManagementCompany/Features/Board/BoardMetricsEndpoint.cs` and every endpoint under `HOAManagementCompany/Features/Board/Architectural/` except `ArcSweepJobEndpoint.cs` and the ARC settings endpoints (`ArcSettingsGetEndpoint.cs`, `ArcSettingsPutEndpoint.cs`), which T131 owns.
  - `ArcSweepService.cs` skips non-Active communities (R21).
- [X] T108 [US3] In `HOAManagementCompany/Features/Auth/AuthService.cs` (`RegisterAsync`) and `ClaimCodeService.cs`, refuse a claim code for a property in an Onboarding community with 409 `COMMUNITY_NOT_ACTIVE`, after the proof and the code are checked and without using up the code. Do this until T099 passes.
- [X] T109 [US3] Build the US3 frontend (briefs 04 and 11), until T102 pass:
  - in `neko-hoa/src/app/features/community-settings/`:
    - `ProfileSectionComponent`: autosave per field, and the Status card with Mark Active, Archive and Restore, showing the disabled reason;
    - `LifecycleDialogsComponent`: the activate, archive and restore dialogs;
  - in the shell's home list: the "No longer managed" label;
  - `ArchivedNoticeComponent` in `neko-hoa/src/app/shared/`, rendered by resident pages on a 409 `COMMUNITY_ARCHIVED`.

**Checkpoint**: communities can go live and be archived or restored, with every cascade and refusal. The Profile section saves one field at a time.

---

## Phase 6: User Story 8: Invite and remove residents (P1)

**Goal**: Managers and Company Administrators invite residents to properties by email and remove them. Removal takes effect on the resident's next request, and cancels the autopay they enrolled.

**Independent Test** (spec US8):
- In an Active community, invite a new email, accept from the link, and confirm the resident sees the home.
- Invite to an Onboarding community, and confirm nothing is sent until it is marked Active.
- Remove the resident. Confirm:
  - they lose the home on their very next request;
  - the autopay they enrolled is cancelled;
  - their other homes and roles are untouched.

### Tests (write first)

- [X] T110 [P] [US8] Create `ResidentInvitationTests` in `HOAManagementCompany.Tests/Integration/Residents/ResidentInvitationTests.cs`:
  - **US8-S1**: a Community Manager of Active A invites jane@ to #29. The invitation is Pending and emailed with the property address.
    - Jane registers from the link, with no claim code.
    - A `UserProperty` link to #29 exists, and `/auth/me` lists #29 with `landing: "Resident"`.
  - **US8-S2**: #29 already has a resident. A second invitee accepts, and both are linked; there is no "already claimed" refusal.
  - **US8-S3**: sam@ already has an account with another home.
    - Inviting him gives the same response as a no-account invite.
    - Nothing is linked until he accepts while signed in, after which `/auth/me` lists both homes.
  - **US8-S4**: in an Onboarding community, the invitation is created with `state: "Scheduled"`, a null `expiresAt`, no email and no `TokenHash`.
    - Resend → 409 `COMMUNITY_NOT_ACTIVE`, and nothing is sent.
    - Activation, through the lifecycle endpoint, emails it with `ExpiresAt` = activation + 72 hours.
    - A scheduled invitation whose sender has lost authority is revoked instead.
  - **US8-S5**: used after 72 hours → 410 `INVITATION_EXPIRED`, with no link made. Resend emails a new link valid for 72 hours, and the old link → 410 `INVITATION_INVALID`.
  - **US8-S9**: jane@ is already a resident of #29 → 409 `ALREADY_RESIDENT`.
  - **US8-S10**: kim@ has a Scheduled or Pending invitation to #29 → 409 `INVITATION_PENDING`, with `invitationId`.
  - **US8-S15**: revoking a Pending invitation makes its link return 410 `INVITATION_INVALID`, with no link made.
  - **US8-S16**: a Company Administrator with no membership invites jane@ to #29, and it is emailed. After a later removal, her next request for #29 → 403 `PROPERTY_ACCESS_DENIED`.
  - **US8-S17**: a Community Manager inviting their own email, in any case → 422 `SELF_GRANT_NOT_ALLOWED`. US7-S10's resident half: a Company Administrator inviting their own email gets the same.
  - **Also**:
    - inviting the same email to two properties creates two independent invitations;
    - an invitation for a property outside the community → 403, identical to a nonexistent one.
- [X] T111 [P] [US8] Create `ResidentRemovalTests` in `HOAManagementCompany.Tests/Integration/Residents/ResidentRemovalTests.cs`:
  - **US8-S6**: Jane enrolled #29's autopay (`EnrolledByUserId` = Jane, set by `PUT /payments/recurring`), and she is removed.
    - Her next `GET /property`, with her unexpired token → 403 `PROPERTY_ACCESS_DENIED`.
    - The schedule is `inactive` with `resident_removed` and `TerminatedAt`, and no draft starts for it.
    - She gets `co_resident_removed`, saying autopay ended; each other resident gets `co_autopay_ended`.
    - Her other home and her account are unchanged.
    - `ResidentLinkChange Change=Removed` and `AutopayCancelled` are logged.
  - **No recorded enroller**: the schedule is cancelled the same way.
  - **US8-S7**: Sam enrolled. Removing Jane leaves the schedule `active` and sends no `co_autopay_ended`.
  - **US8-S8**: Jane is also a Board Member, and #29 was her only home. Her membership is unchanged, and she signs in with `landing: "Board"`.
  - **Removal preview**: returns `WillCancel` with `EnrolledByThisResident`, `WillCancel` with `NoEnrollerRecorded`, or `Unaffected`, for the three cases.
  - **Also**:
    - when the last resident is removed, the property shows no resident, and its owner of record and balance are unchanged;
    - DELETE for a user who isn't linked → 404.
- [X] T112 [P] [US8] Create `ResidentsAreaEndpointTests` in `HOAManagementCompany.Tests/Integration/Residents/ResidentsAreaEndpointTests.cs`:
  - **US8-S13**, with 120 properties, 7 of them without residents:
    - The first page has 25 items, each with its address, resident names and invitation summary, and `total` is 120.
    - The drawer endpoint returns residents (name, email, resident since) and invitations (Scheduled, Pending, Expired and Revoked, with who sent them).
    - Search and filters:
      - `search=keystone park dr #29` returns exactly #29;
      - `filter=noResident` returns exactly the 7;
      - `invitationPending` includes Scheduled invitations;
      - `invitationExpired` works;
      - a search that matches an email sets `matchedEmail`.
    - Addresses are in natural order (#9 before #10), and `limit=101` is clamped.
  - **US8-S14**: with no properties → `propertiesLoaded: false` and no items.
  - **US8-S18**: each list load and search, and each drawer load, logs one `BoardAssociationDataAccess` with actor, community, resource and UTC time.
- [X] T113 [P] [US8] Create `ResidentAuthorizationTheoryTests` in `HOAManagementCompany.Tests/Integration/Residents/ResidentAuthorizationTheoryTests.cs`:
  - **US8-S11**: a Community Manager of A only invites to, removes from, lists or previews a property in B → 403, identical to a nonexistent property or community.
  - **US8-S12**: `[Theory]` over Board Member, Accountant and Resident → 403 on invite and on remove.
  - **SC-005 rows**: add resident invite and remove to the `ArchivedWriteEndpoints` registry of T095.
- [X] T114 [P] [US8] Create `ResidentSensitiveEventTests` in `HOAManagementCompany.Tests/Integration/Residents/ResidentSensitiveEventTests.cs`. For **US8-S19**:
  - **Actions covered**: invite, resend, revoke, acceptance, failed acceptance and removal.
  - **Each change** logs one event, with actor, community, property id, target user id or invitation id, the change and UTC time.
  - **No event** renders an email, a name, a street address or the token.
- [X] T115 [P] [US8] Create `ClaimCodeAfterInvitationTests` (FR-024f) in `HOAManagementCompany.Tests/Integration/Auth/ClaimCodeAfterInvitationTests.cs`:
  - once a property has a resident by invitation, its outstanding claim code gets the generic `REGISTRATION_FAILED`;
  - a claim code still works for a property with no resident.
- [X] T116 [P] [US8] Create `residents-section.component.spec.ts` in `neko-hoa/src/app/features/community-settings/residents/` (brief 06):
  - the list has 25 rows, with address, resident names and invitation chips ("Scheduled" as text);
  - search, and the three filters with their counts;
  - when `propertiesLoaded` is false, the empty state shows with no invite action (US8-S14);
  - selecting a row opens the property drawer, with residents, invitations, and Invite, Resend, Revoke and Remove;
  - the invite form handles `ALREADY_RESIDENT`, `INVITATION_PENDING` (offering Resend) and `SELF_GRANT_NOT_ALLOWED`;
  - the remove dialog shows the removal-preview lines;
  - everything is read-only when Archived (US5-S7, the Residents part).

### Implementation

- [X] T117 [US8] Record `EnrolledByUserId` = the caller on every enroll and re-enroll (R9): `UpsertRecurringAsync` in `HOAManagementCompany/Features/Payments/PaymentService.cs` gains a `userId` parameter, passed by `Recurring/RecurringUpsertEndpoint.cs`
- [X] T118 [US8] Implement the `Residency` offer handler in `HOAManagementCompany/Features/Invitations/ResidencyOfferHandler.cs`:
  - the property must belong to the community, or `ALREADY_RESIDENT` applies;
  - the invitation is Scheduled in an Onboarding community;
  - sender authority is checked through `ManageResidents`;
  - applying it creates the `UserProperty` link unless it exists, and logs `ResidentLinkChange Change=Linked`.

  Also make `ClaimCodeService` refuse a code once its property has any resident (FR-024f).
- [X] T119 [US8] Add `ResidentsListEndpoint`, `ResidentDetailEndpoint`, `ResidentInviteEndpoint`, `RemovalPreviewEndpoint` and `ResidentRemoveEndpoint` in `HOAManagementCompany/Features/Board/Residents/`, per the contract, until T110, T111, T112, T113, T114 and T115 pass.
  - They use `ManageResidents`.
  - The list orders addresses with `natural_sort` and logs `BoardAssociationDataAccess`.
  - Removal runs in one transaction: it uses `CancelRecurringForAsync`, enqueues `co_resident_removed` and `co_autopay_ended`, and logs.
- [X] T120 [US8] Implement `ResidentsSectionComponent`, with `PropertyDrawerComponent`, the invite form and the remove dialog, in `neko-hoa/src/app/features/community-settings/residents/`, until T116 pass

**Checkpoint**: the MVP P1 backend is complete. Residents join by invitation and lose access on removal.

---

## Phase 7: User Story 9: Sign in without a linked home (P1)

**Goal**: The pages and routing for users with no usable home, and the bookmarkable company sign-in page. The backend was done in Phase 2 (T042).

**Independent Test** (spec US9):
- Sign in as each kind of user with no linked home, and confirm where each lands.
- Remove a Board Member's only home, and confirm they still reach board mode.

The backend is covered by T041 (US9-S1 to US9-S6) and T030 (US9-S7 and US9-S8 rule).

### Tests (write first)

- [X] T121 [P] [US9] Create `company-sign-in.component.spec.ts` in `neko-hoa/src/app/features/auth/` (brief 01, 8.01.a–c):
  - it calls `POST /auth/login`;
  - **US9-S7**: it routes to `/app/company/portfolio` when `isCompanyAdministrator`;
  - **US9-S8**: otherwise it routes to the landing with `?notice=not-company-admin`, and that page shows "This account isn't a Company Administrator, so you've been signed in to your usual page.";
  - failures show the same generic message as the usual sign-in page.
- [X] T122 [P] [US9] Create the landing-page specs:
  - `no-home.component.spec.ts` (US9-S3: tells the user to use an invitation link or contact their community manager) and `not-live.component.spec.ts` (US9-S5), in `neko-hoa/src/app/features/landing/`;
  - `neko-hoa/src/app/core/interceptors/auth.interceptor.spec.ts`: a 403 `PROPERTY_ACCESS_DENIED` calls `/auth/me`, then navigates to the new landing with brief 11's notice (US9-S4, FR-024g), with no refresh loop.

### Implementation

- [X] T123 [US9] Build the US9 frontend until T121 and T122 pass:
  - `CompanySignInComponent` at `/company/sign-in`, public and bookmarkable, in `neko-hoa/src/app/features/auth/company-sign-in.component.ts`. It is titled "Company sign-in" and shows no company name. If the design handoff keeps the name (brief 01, D4), T160 adds it.
  - `NoHomeComponent` and `NotLiveComponent` at `/app/no-home` and `/app/not-live`, with `authGuard`, in `neko-hoa/src/app/features/landing/`.
  - The `PROPERTY_ACCESS_DENIED` handling in `neko-hoa/src/app/core/interceptors/auth.interceptor.ts`.
  - The routes in `neko-hoa/src/app/app.routes.ts`.

**Checkpoint**: MVP complete (Phases 1–7). Deliver it as one demoable increment.

---

## Phase 8: User Story 4: Company-wide default settings (P2)

**Goal**: Company Administrators edit the defaults that new communities copy. Community Managers can read them.

**Independent Test** (spec US4):
- Change the lapse rule to "deemed approved", create a community, and confirm it starts with that rule.
- Confirm existing communities and their applications are unchanged.
- Confirm a Community Manager can read the defaults but not change them.

### Tests (write first)

- [ ] T124 [P] [US4] Create `CompanyDefaultsEndpointTests` in `HOAManagementCompany.Tests/Integration/Company/CompanyDefaultsEndpointTests.cs`:
  - **US4-S1**: a Company Administrator PUTs `lapseRule: DeemedApproved`. A community created afterwards has `DeemedApproved`, and every existing community keeps its lapse rule.
  - **US4-S2**: take two existing communities (one created by this spec, one whose settings came from the migration backfill) and their received applications. After every default changes, their `CommunityArcSettings` and the applications' snapshot fields are unchanged.
  - **US4-S3**: a Community Manager's GET → 200 with `canEdit: false`, and their PUT → 403.
  - **US4-S4**: `[Theory]` over Board Member, Accountant and Resident → 403 on GET.
  - **US4-S5**: `[Theory]` over a review period of 0 and of 400, reminder days of 31, and a 1,001-character statement → 422, naming the allowed range.
  - **Also**: `CompanyDefaultsChanged` is logged with old and new values.
- [ ] T125 [P] [US4] Create `company-defaults.component.spec.ts` in `neko-hoa/src/app/features/company/` (brief 08):
  - **US4-S6**: exactly one section, "Architectural review", is shown;
  - it is editable for a Company Administrator;
  - **US4-S3**: it is read-only, with no Save, for a Community Manager who opens it through `/app/board/company-defaults`;
  - **US4-S5**: validation messages appear under their fields;
  - it never offers to push changes to existing communities (FR-026).

### Implementation

- [ ] T126 [US4] Extract the six-field validation from `HOAManagementCompany/Features/Board/Architectural/ArcSettingsPutEndpoint.cs` into `ArcRuleValidator.cs` in the same folder, without changing 027's behavior (`ArcSettingsEndpointTests` stay green)
- [ ] T127 [US4] Add `CompanyDefaultsGetEndpoint` (`ViewCompanyDefaults`) and `CompanyDefaultsPutEndpoint` (`ManageCompanyDefaults`, `board-writes`, `ArcRuleValidator`) in `HOAManagementCompany/Features/Company/`, until T124 passes
- [ ] T128 [US4] Implement `CompanyDefaultsComponent` in `neko-hoa/src/app/features/company/`, until T125 pass. Add its routes to `neko-hoa/src/app/app.routes.ts`: `/app/company/defaults`, and the read-only `/app/board/company-defaults` for Community Managers.

---

## Phase 9: User Story 5: One settings page per community (P2)

**Goal**: Spec 027's ARC settings become the page's Architectural review section, with a reset to company defaults, a redirect from the old location, and archived read-only mode.

**Independent Test** (spec US5):
- As a Company Administrator with no membership, open a community's settings. Every section is editable, including the reset.
- As its manager, status and parent are read-only.
- As a Board Member, the page is refused.

US5-S3 is covered by T065 and T067, and US5-S8 by T081.

### Tests (write first)

- [ ] T129 [P] [US5] Create `ArcSettingsSectionTests` in `HOAManagementCompany.Tests/Integration/Company/ArcSettingsSectionTests.cs`:
  - **US5-S1**: a Company Administrator with no membership GETs and PUTs ARC settings (200) and resets them, but still gets 403 when listing or voting on applications (FR-003).
  - **US5-S2**: the Community Manager edits ARC settings as in 027. A Board Member still reads them (200) and gets 403 on PUT.
  - **US5-S4**: each PUT logs one `ArcSettingsChanged` with old and new values.
  - **US5-S5**: the review period is 45 against a default of 30. After reset:
    - all six fields equal the current defaults;
    - an application already received keeps its snapshot;
    - `NextApplicationNumber` is unchanged, so the next new application gets the expected `ARC-` number;
    - the reset is logged with `Cause=reset`.
  - **US5-S7**: in an Archived community:
    - the Company Administrator's GETs of settings, profile, memberships, invitations and residents return 200, and revoked invitations carry `revokeReason: "CommunityArchived"`;
    - PUT and reset of ARC settings → 409 `COMMUNITY_ARCHIVED`;
    - the shell's `can.*` offers only `restore` and `endMembershipsWhileArchived`;
    - **SC-005 rows**: add ARC settings PUT and reset to the `ArchivedWriteEndpoints` registry of T095.
  - **Also**: the GET adds `companyDefaults`, `differsFromDefaults` and `canEdit`.
- [ ] T130 [P] [US5] Write the US5 frontend specs first (brief 07):
  - **`architectural-review-section.component.spec.ts`** in `neko-hoa/src/app/features/community-settings/`: the moved 027 form, a "differs from default" marker per field, the "Company defaults" link (FR-027), and the reset confirmation.
  - **`settings-side-nav.component.spec.ts`** (US5-S9): typing "review" lists "Architectural review › Review period", and choosing it opens the section with focus on that field.
  - **`neko-hoa/src/app/app.routes.spec.ts`** (US5-S6): `/app/board/arc-settings` redirects to `/app/board/settings/architectural-review`.
  - **`community-settings-page.component.spec.ts`** (US5-S7): when Archived, the whole page is read-only, revoked invitations show "Revoked — community archived", and only Restore and End are offered.

### Implementation

- [ ] T131 [US5] Change the ARC settings endpoints in `HOAManagementCompany/Features/Board/Architectural/`, until T129 passes:
  - `ArcSettingsGetEndpoint.cs` uses `ViewArcSettings` and adds `companyDefaults`, `differsFromDefaults` and `canEdit`;
  - `ArcSettingsPutEndpoint.cs` uses `ManageArcSettings` through `DecideAsync`;
  - add `ArcSettingsResetEndpoint.cs`, which copies the six fields only and never touches `NextApplicationNumber`.
- [ ] T132 [US5] Move `neko-hoa/src/app/features/board/architectural/arc-settings.component.ts` and its spec into the settings page as `ArchitecturalReviewSectionComponent` in `neko-hoa/src/app/features/community-settings/`, until T130 pass.
  - Add the defaults comparison and the reset.
  - Register its setting labels in the side-nav registry.
  - Redirect `/app/board/arc-settings` in `app.routes.ts`.
  - Apply the Archived read-only mode across every section.

---

## Phase 10: User Story 7: Grant and revoke the Company Administrator role (P2)

**Goal**: Company Administrators invite and revoke others, never the last one. The setup step gains its recovery mode.

**Independent Test** (spec US7):
- Run the setup step on an empty deployment, then run it again, and confirm only the first run changes anything (covered by T058).
- Grant a second administrator and revoke the first.
- Confirm the last administrator can't be revoked, and that the role gives no vote.

### Tests (write first)

- [ ] T133 [P] [US7] Create `CompanyAdministratorsTests` in `HOAManagementCompany.Tests/Integration/Company/CompanyAdministratorsTests.cs`:
  - **US7-S1**: a Company Administrator invites ops@, giving a Pending `CompanyAdministrator` invitation valid for 72 hours, which is emailed. Ops accepts by registering. On the next request, `/auth/me` has `isCompanyAdministrator: true` and `GET /company/communities` returns 200.
  - **US7-S2**: with two administrators, one revokes the other.
    - The revoked user's next `GET /company/communities` → 403.
    - Their pending Company Administrator invitations are revoked (`SenderLostAuthority`).
    - Invitations they can still send as a Community Manager are kept.
  - **US7-S3**: the only administrator revoking themself → 422 `LAST_COMPANY_ADMIN`.
  - **US7-S5**: a Community Manager → 403 on list, invite and revoke.
  - **US7-S10**: a Company Administrator granting themself a Board Member membership by `userId`, or inviting their own email as an administrator → 422 `SELF_GRANT_NOT_ALLOWED`. The resident half is in T110.
  - **US7-S11**: each grant, revoke and invitation logs one `CompanyAdminChange` with actor, target user id or invitation id, the change and UTC time, and no email.
  - **Also**:
    - `GET /company/administrators` and `GET /company/invitations` page with `limit`/`offset` (default 25; `limit=101` is clamped to 100), and their contents follow the contract (constitution §4);
    - `ALREADY_MEMBER` for a current administrator's email;
    - `INVITATION_PENDING` for a duplicate;
    - `/company/invitations/{id}/resend` and `/revoke` work as in T081.
- [ ] T134 [P] [US7] Create `CompanyAdminConcurrencyTests` in `HOAManagementCompany.Tests/Integration/Company/CompanyAdminConcurrencyTests.cs`.
  - **US7-S4**: two administrators revoke each other at once, synchronized by a barrier.
  - Over 20 iterations, assert exactly one 200 and one 422 `LAST_COMPANY_ADMIN`, and one live grant left.
- [ ] T135 [P] [US7] Create `SetupStepRecoveryTests` in `HOAManagementCompany.Tests/Integration/Company/SetupStepRecoveryTests.cs`, each against its own database. For **US7-S8**:
  - with an administrator present, `--recover` for owner@ grants the role (`Source = Recovery`) when the email has an account, and invites otherwise;
  - `CompanyAdminChange Change=Recovery` is logged;
  - one `co_admin_recovery` outbox row goes to each current administrator;
  - exit 0.
- [ ] T136 [P] [US7] Create `CompanyAdminNoBoardPowersTests` in `HOAManagementCompany.Tests/Integration/Company/CompanyAdminNoBoardPowersTests.cs`. For **US7-S9**, a Company Administrator with no Board Member membership in A gets 403 on (FR-003):
  - casting a vote and recording an outcome;
  - resending an outcome email;
  - the applications list and detail;
  - board metrics.
- [ ] T137 [P] [US7] Create `company-administrators.component.spec.ts` in `neko-hoa/src/app/features/company/` (brief 09):
  - it lists administrators and invitations, each paged, and has the invite form;
  - Revoke asks for confirmation;
  - the last administrator's Revoke is disabled, with the `LAST_COMPANY_ADMIN` reason;
  - revoking yourself warns that you'll lose access.

### Implementation

- [ ] T138 [US7] Add these in `HOAManagementCompany/Features/Company/`, until T133, T134 and T136 pass:
  - `CompanyAdministratorsListEndpoint` (paged) and `CompanyInvitationsListEndpoint` (`GET /company/invitations`, paged);
  - `CompanyAdministratorsInviteEndpoint`;
  - `CompanyAdministratorsRevokeEndpoint`, which revokes under `CompanyLocks` and runs `SenderAuthority.RevokeOrphanedAsync`;
  - `CompanyInvitationResendEndpoint` and `CompanyInvitationRevokeEndpoint`.
- [ ] T139 [US7] Add the `--recover` path to `CompanyAdministratorService.SetupAsync` in `HOAManagementCompany/Features/Company/CompanyAdministratorService.cs`, until T135 passes. It grants or invites with `Source = Recovery`, logs, and enqueues `co_admin_recovery`.
- [ ] T140 [US7] Implement `CompanyAdministratorsComponent` in `neko-hoa/src/app/features/company/`, with the route `/app/company/administrators`, until T137 pass

---

## Phase 11: User Story 6: Portfolio view (P3)

**Goal**: The portfolio gains the manager columns, the "No manager" and "differs from defaults" flags, the status filter, search and sortable columns.

**Independent Test** (spec US6):
- Seed communities in each status, one with no manager and one whose ARC settings differ from the defaults.
- Confirm the flags, the status filter and the search.
- Confirm a Community Manager is never offered the view, and is refused if they request it.

### Tests (write first)

- [ ] T141 [P] [US6] Extend `HOAManagementCompany.Tests/Integration/Company/PortfolioEndpointTests.cs`:
  - **US6-S1**: with no open-ended manager, the row has `noManager: true`.
  - **US6-S2**: with only a pending manager invitation, `noManager` is true and `pendingManagerInvitations` lists it.
  - **US6-S3**: an ARC review period that differs from the default gives `differsFromDefaults: true`; an equal one gives false.
  - **US6-S4**: `status=archived` returns only archived communities.
  - **US6-S5**: `search=Keystone` matches community or legal name in any case, and nothing else.
  - **US6-S6**: covered by T072.
  - **US6-S7**, with 31 communities:
    - the default order is A–Z by name;
    - `sort=status` orders by status, then name;
    - `noManager=true` returns only those communities;
    - `counts` ignore search and paging.
- [ ] T142 [P] [US6] Create `PortfolioPerformanceTests` (SC-008) in `HOAManagementCompany.Tests/Performance/PortfolioPerformanceTests.cs`: with 200 communities that have managers, invitations and settings, the first page answers in under 2 s.
- [ ] T143 [P] [US6] Extend `neko-hoa/src/app/features/company/portfolio.component.spec.ts` (brief 02):
  - the "No manager" and "differs from defaults" flags are text labels;
  - the status filter has counts;
  - search works;
  - the Name, Status and Parent headers sort, with `aria-sort`;
  - the "No manager" filter works;
  - **US6-S6** (frontend): `company-admin.guard.spec.ts` and the shell spec show that a Community Manager is never offered the Company menu or the portfolio route.

### Implementation

- [ ] T144 [US6] Extend `HOAManagementCompany/Features/Company/PortfolioEndpoint.cs` as one SQL projection (R17), until T141 and T142 pass. It adds:
  - managers and pending manager invitations;
  - `noManager`, `differsFromDefaults` and `counts`;
  - the `status`, `noManager` and `search` filters;
  - `sort` and `dir`.
- [ ] T145 [US6] Extend `PortfolioComponent` in `neko-hoa/src/app/features/company/` with the flags, the filters, search and sorting, until T143 pass

---

## Phase 12: Polish and cross-cutting

- [ ] T146 [P] Add Playwright journeys tagged `@company` in `neko-hoa/e2e/company-administration.spec.ts`. They read links from the dev vault (T054) and use fresh emails on each run, so the shared seed isn't damaged. The first two journeys are the automated proxies for SC-001 and SC-010:
  - "add community → invite manager → accept → activate";
  - "invite resident → sign up from the link → see the home";
  - a Board Member is refused `/app/board/settings/profile` and taken to their landing page;
  - the company sign-in page lands an administrator on the portfolio.
- [ ] T147 [P] Add a Cypress journey in `neko-hoa/cypress/e2e/company-admin.cy.ts`: sign in as `company@nekohoa.dev` (no home) → portfolio → Keystone Ridge → Community settings
- [ ] T148 [P] Add accessibility checks in `neko-hoa/e2e/company-administration.spec.ts` for every surface in the spec's Accessibility list. Use axe through the existing Playwright setup if present; otherwise use explicit role and label assertions.
  - keyboard reach for every control;
  - labels, visible focus, and errors tied to their fields;
  - status and flags conveyed as text;
  - the disabled-reason tooltip shown on focus;
  - the drawer's focus handling.
- [ ] T149 [P] Add Storybook stories, in both themes and at phone, tablet and desktop viewports, using tokens only, in a `*.stories.ts` beside each new component:
  - every company page;
  - every settings section;
  - the property drawer and the dialogs;
  - the invitation page's states;
  - the landing pages.
- [ ] T150 [P] Add responsive checks (constitution §6) to `neko-hoa/e2e/company-administration.spec.ts`. Every new page at 390, 834 and 1360 px wide:
  - has no sideways page scrolling;
  - keeps its tables usable: they scroll inside their own container or stack into rows;
  - collapses the settings side list into a menu below 768 px;
  - opens the property drawer full width on a phone.
- [ ] T151 [P] Create `PermissionsMatrixTheoryTests` (SC-002, FR-005) in `HOAManagementCompany.Tests/Integration/Company/PermissionsMatrixTheoryTests.cs`.
  - **Theory**: every action row of the spec's permissions table × the seven roles from Quality gates (Company Administrator; Community Manager of this community; Community Manager of another; Board Member; Accountant; Resident; no role).
  - **Each case** calls the real endpoint and asserts the table's answer: allowed (2xx, or an entity-rule code such as `LAST_MANAGER`), or 403 `FORBIDDEN`.
  - **FR-021's rows** are included: a Company Administrator grants and ends Board Member and Accountant memberships, directly and by invitation.
- [ ] T152 [P] Create `CompanyTelemetryHygieneTests` (FR-040, SC-007) in `HOAManagementCompany.Tests/Integration/Company/CompanyTelemetryHygieneTests.cs`. Run one of each action across the feature, then assert:
  - every caused change has its own event, carrying the parent's actor and `Cause`;
  - no change is logged twice;
  - request spans for company, board and invitation endpoints carry the `community.id` and `capability` tags (spec Observability), and those tags never hold an email or a name;
  - no captured log event or OpenTelemetry span attribute contains any email, name, street address or token used in the test.
- [ ] T153 [P] Create `CompanyAdministrationRateLimitTests` in `HOAManagementCompany.Tests/Integration/RateLimiting/CompanyAdministrationRateLimitTests.cs`:
  - a burst over the `board-writes` limit on community create, profile PATCH, membership create and resident invite → 429;
  - a burst over the `auth` limit on `/invitations/preview` → 429.
- [ ] T154 [P] Document the operator setup step in `infra/README.md`: `--grant-company-admin`, `--recover`, `--company-name`, and running it as a Cloud Run job execution of the service image
- [ ] T155 Reconcile `specs/025-board-overall-design/spec.md` per the spec's list:
  - `Community` status (FR-001) and its "inactive/offboarded" edge case;
  - board eligibility in FR-020, FR-023, FR-025 and FR-026 counts Onboarding communities;
  - FR-042: co-manager removal, Company Administrators, invitations and the open-ended-manager rule;
  - the "authentication reused unchanged" Assumption and the tenant-boundary note.
- [ ] T156 [P] Reconcile `specs/027-board-arc-review/spec.md` FR-030: who may edit becomes "a Company Administrator or that community's Community Manager"
- [ ] T157 [P] Reconcile `specs/016-security-hardening/spec-identity-access.md` and `specs/017-security-hardening-subspec-a/spec.md`:
  - mark these as superseded by FR-022a, FR-024 and FR-036a:
    - the umbrella clarification ("one-time claim code only … no administrator-approval path");
    - FR-A1's clarification;
    - the Edge Case that rejects an administrator path;
    - the claim-code-only Assumption;
  - note that FR-A10's 15-minute token window no longer covers property access.
- [ ] T158 [P] Reconcile `specs/006-stripe-payments/spec.md` and `specs/006-stripe-payments/data-model.md`: recurring payments gain the enroller and the cancellation reason, and are cancelled on archive and on removal of the enrolling resident
- [ ] T159 [P] Reconcile `specs/029-resident-arc-requests/spec.md`. Its resident endpoints now re-check the resident link and the community's status on every request, through `ResidentAccess` (T040; 030 FR-024g, FR-012a). Onboarding homes get `COMMUNITY_NOT_ACTIVE`, Archived homes `COMMUNITY_ARCHIVED`, and an unlinked caller `PROPERTY_ACCESS_DENIED`, in place of trusting the token's `propertyId` claim.
- [ ] T160 When `specs/030-company-administration/design/handoff/` arrives:
  - apply it to the components built here;
  - replace the `[no WFb]` tags in `spec.md` with design citations;
  - refresh the Storybook stories;
  - if brief 01 keeps the company name on the company sign-in page, add it as a runtime-config value, with its rule in `neko-hoa/src/app/core/config/runtime-config.validator.ts` (constitution §8);
  - re-run T148 and T150.
- [ ] T161 Run the full backend suite (`dotnet test`) and frontend (`cd neko-hoa && npm run test:ci && npm run build`). Confirm:
  - coverage on new files is at least 95%;
  - the payment, ARC and auth suites still pass after the access and sign-in changes;
  - Sonar passes;
  - the new endpoints appear in the development Swagger UI and match the contracts (constitution §4);
  - the `ArchivedWriteEndpoints` registry of T095 lists every write endpoint in the permissions table.
- [ ] T162 Walk through `specs/030-company-administration/quickstart.md` against a local stack, and fix any drift in the quickstart or the code
- [ ] T163 NLT audit per CLAUDE.md. For every `USn-Sm` and Independent Test in `spec.md`, confirm that the citing test exists, asserts the stated Then, and isn't skipped. Record the mapping table at the end of this file.
- [ ] T164 Keep the spec truthful:
  - update `spec.md`, `plan.md` and the contracts for any change made during implementation;
  - mark this file's tasks done;
  - refresh the Repowise marker regions listed in `plan.md`.

---

## Dependencies and Execution Order

### Phase dependencies

| Phase | Depends on |
|---|---|
| Setup (1) | nothing |
| Foundational (2) | Setup. Blocks every story. |
| US1 (3) | Phase 2 |
| US2 (4) | Phase 2. Its UI uses the settings shell (T068). The US1 frontend isn't needed: the tests create communities through `CompanyTestBase`. Its SC-005 rows go in T095's registry, as for US8. |
| US3 (5) | Phase 2. The activation fan-out cases in T092 need the residency offer (T118); build that task first, or keep those cases red until US8. T104 needs T103. |
| US8 (6) | Phase 2. US8-S4's activation case needs the activate endpoint (T105). The removal cancel needs T103. Its SC-005 rows go in T095's registry; if US3 hasn't landed yet, add them when it does. |
| US9 (7) | Phase 2 (backend done there) |
| US4 (8) | Phase 2 |
| US5 (9) | Phase 2. US5-S7's archived case needs the archive endpoint (T105). US5 owns the ARC settings endpoints; T107 leaves them alone. Its SC-005 rows go in T095's registry, as for US8. |
| US7 (10) | Phase 2 |
| US6 (11) | US1 (T075) |
| Polish (12) | all stories |

### Within each story

Tests first, watched failing → offer handlers and services → endpoints → frontend components → routes and nav.

### Cross-spec dependencies

- **Hard dependencies (merged)**:
  - 025: communities, memberships, the resolver, `BoardHttp`, `boardGuard`, `BoardNavigationService`;
  - 027: ARC settings, `ArcLocks`, `ArcEmailRenderer`, the outbox `RecipientUserId`;
  - 006: `RecurringPayment` and `PaymentService`;
  - 016/017: registration, claim codes and `AuthCrypto`;
  - 029: the resident ARC endpoints and `ResidentArcScope`, which this spec routes through `ResidentAccess` (T040, T159).
- **Notification Settings** (optional): its sections stay hidden. Nothing here waits on it.

---

## Parallel examples

```text
# Phase 2, after the entities (T010, T011):
T019, T020, T028, T030, T035, T036, T041,
T045, T047, T052, T058, T065
# Frontend, alongside:
T044, T061, T062, T063, T055, T056, T067, T069

# Once Phase 2 is done, stories in parallel (different files):
US1 (T071…), US2 (T077…), US4 (T124…), US7 (T133…)

# US3 tests together:
T092, T093, T094, T095, T096, T097, T098, T099, T101, T102

# US8 tests together:
T110, T111, T112, T113, T114, T115, T116
```

---

## Implementation strategy

1. **MVP** (the spec's MVP: US1–US3, US8, US9 and the foundation slice):
   1. Phase 1, then Phase 2.
   2. US1, then US2, US3 and US8 (one developer can follow this order; a team can split them per the table above).
   3. US9's pages.
   4. Demo with the quickstart's steps 1–6.
2. **Increment 2**, in parallel after the MVP: US4 (the defaults page), US5 (the ARC section, reset and redirect) and US7 (grant and revoke).
3. **Increment 3**: US6 (the portfolio flags, filters and sorting).
4. **Polish**:
   - T151, T152, T153, T150 and the NLT audit (T163) before the PR goes to review;
   - T160 whenever the Claude Design handoff arrives (it doesn't block merging behavior).

## NLT mapping (T163)

To be filled in by T163 during implementation. Every scenario above already names its planned test task.
