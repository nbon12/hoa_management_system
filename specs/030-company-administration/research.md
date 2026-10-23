# Research: Company Administration

**Feature**: `030-company-administration` | **Date**: 2026-10-17 | **Spec**: [spec.md](./spec.md)

Each item records a decision, why it was chosen, and what else was considered. Everything was checked against `main` at `21e2491` (spec 027 merged). Paths are relative to the repo root; `HMC` is `HOAManagementCompany/`.

---

## R1. Management company and Company Administrator grants

**Decision**: Add two tables.

- `ManagementCompanies(Id, Name, CreatedAt)`: the migration inserts exactly one row.
- `CompanyAdministratorGrants(Id, CompanyId, UserId, GrantedByUserId?, Source, GrantedAt, RevokedAt?, RevokedByUserId?)`: a filtered unique index on `(CompanyId, UserId) WHERE RevokedAt IS NULL` stops duplicate live grants.

`Community` gains `ManagementCompanyId` (FK, not null after the backfill).

An `ICompanyContext` service, cached per request, resolves the caller's company: the company of their live grant, otherwise the company of their effective Community Manager memberships. For a community-scoped request it is the community's company. Only the setup step and the dev seed ask for "the deployment's company", the single row. Every query filters by `ManagementCompanyId`, so a second company later needs no query changes (FR-001), and tests that create their own companies stay isolated.

**Rationale**: FR-001 and FR-002 need the company as a record and the role as a grant separate from memberships. A grant table with revocation fields keeps history without an audit table: the audit trail stays Serilog-only, and the table only holds state.

**Alternatives considered**:
- A boolean on `ApplicationUser`. Rejected: it can't model a second company, has no grant history, and blurs the "role, not a membership" rule.
- An ASP.NET Identity role. Rejected: Identity roles are global, and the codebase decides authorization through the resolver, never `[Authorize(Roles=…)]` (025 FR-012).

## R2. Authorization: extending the resolver (FR-004, FR-004a)

**Decision**: Keep `ICommunityScopeResolver` as the single entry point and add two methods.

```csharp
Task<ScopeDecision> DecideAsync(ClaimsPrincipal user, Guid communityId,
    CommunityCapability capability, ScopeOperation op, CancellationToken ct = default);
Task<bool> CanAccessCompanyAsync(ClaimsPrincipal user, CompanyCapability capability,
    CancellationToken ct = default);
```

`ScopeDecision` is one of `Allowed`, `Forbidden`, `Archived` or `NotActive`. `ScopeOperation` is `Read` or `Write`. `DecideAsync` runs FR-004a's order:

1. **Load the community.** If it doesn't exist, return `Forbidden`.
2. **Authorize as if it were Active.** Grant the capability if any of the caller's *effective* memberships there confers it (status Active, end date unset or in the future; the community's status is not considered), **or** the caller holds a live grant for the community's company and the capability is one a Company Administrator may hold. Otherwise return `Forbidden`.
3. **Apply the status rule.**
   - Active or Onboarding: `Allowed`. Non-resident members work in Onboarding communities (FR-011, FR-036b).
   - Archived, by a Company Administrator: `Allowed` for reads of the settings sections, and for the `ManageCommunityLifecycle` writes (restore, end membership). Every other operation returns `Archived`.
   - Archived, by anyone else: `Archived`.

`CanAccessAsync` stays as a thin wrapper, `DecideAsync(..., Read) == Allowed`, for the callers that only need a yes or no.

**Capabilities** (the names are a plan decision, per the spec):

| Capability | Kind | Granted to | Used for |
|---|---|---|---|
| `ViewAssociationData` | community | BoardMember, CommunityManager, Accountant | unchanged (025) |
| `ManageMemberships` | community | CommunityManager, **Company Administrator** | Board Member and Accountant memberships, people list, invitations for those roles |
| `ManageCommunityManagers` | community | CommunityManager, Company Administrator | appoint, end, downgrade managers; manager invitations |
| `ReplaceLastManager` | community | Company Administrator | the one-action replace (FR-020) |
| `EditCommunityProfile` | community | CommunityManager, Company Administrator | Profile fields (FR-017) |
| `ManageResidents` | community | CommunityManager, Company Administrator | Residents area: list, invite, resend, revoke, remove (FR-024) |
| `ViewArcSettings` | community | BoardMember, CommunityManager, Company Administrator | GET ARC settings (FR-030; Board Members keep their 027 read access) |
| `ManageArcSettings` | community | CommunityManager, Company Administrator | PUT and reset ARC settings (FR-030, FR-031) |
| `ManageCommunityLifecycle` | community | Company Administrator | activate, archive, restore, change parent, end memberships while archived |
| `ViewCommunitySettings` | community | CommunityManager, Company Administrator | the page shell and its read endpoints |
| `ViewArchitecturalApplications`, `VoteArchitecturalApplications`, `ManageArchitecturalReview` | community | unchanged (027) | **never** granted through the Company Administrator grant (FR-003) |
| `CreateCommunity` | company (b) | Company Administrator, or an effective CommunityManager in any Onboarding or Active community | FR-006 |
| `ViewCompanyDefaults` | company (b) | same as above | FR-027 |
| `ManageCompanyDefaults` | company (c) | Company Administrator | FR-025 |
| `ManageCompanyAdministrators` | company (c) | Company Administrator | FR-037 |
| `ViewPortfolio` | company (c) | Company Administrator | FR-034, FR-035 |

The ARC settings endpoints move from `ViewArchitecturalApplications` / `ManageArchitecturalReview` to `ViewArcSettings` / `ManageArcSettings`. No one who has access today loses it (FR-030).

**Rationale**: 025 FR-012 and its static test require one resolver. A decision type instead of a bool lets every endpoint return the right refusal, `403 FORBIDDEN` or `409 COMMUNITY_ARCHIVED`, from one place, in FR-004a's order. Splitting the community-scoped and company-level checks matches the spec's (a), (b) and (c).

**Alternatives considered**:
- A second "company resolver". Rejected: two authorization entry points is the failure 025 R1 warned about.
- Letting the Company Administrator grant satisfy every community capability. Rejected: FR-003.

## R3. Keeping the static scope test honest

**Decision**: `BoardScopeEnforcementStaticAnalysisTests` scans `Features/Board` recursively today. Widen it to `Features/Board`, `Features/Company` and `Features/Residents`, and accept `CanAccessAsync`, `DecideAsync` or `CanAccessCompanyAsync`.

Two more checks join it:

- **`ResidentAccessStaticAnalysisTests`**: every endpoint whose source reads the `propertyId` or `communityId` claim must also call `ResidentAccess.RequireAsync` (R8), and no endpoint may use `Guid.Parse(User.FindFirst("propertyId")!…)` any more.
- **Invitation acceptance endpoints** (`Features/Invitations/`) are token-authorized (R6). They sit outside the scanned folders, and the plan's Structure Decision documents why.

## R4. Community status and lifecycle

**Decision**: `CommunityStatus` becomes `Onboarding | Active | Archived`. The column is already `text`, so the migration updates rows: `'Inactive'` becomes `'Archived'`. `Community` gains:

- `ActivatedAt?`, `ActivatedByUserId?`
- `ArchivedAt?`, `ArchivedByUserId?`
- `RestoredAt?`, `RestoredByUserId?`
- `CreatedByUserId?`

Existing Active communities get `ActivatedAt = CreatedAt`, so they count as "was activated" for FR-010's restore rule.

The transitions live in `CommunityLifecycleService`. Each runs inside the community lock (R5) and one transaction:

- **Activate.** Requires an open-ended manager (R7). Sends due scheduled resident invitations after commit (R6).
- **Archive.** Checks open work: ARC applications that are Open or DecisionReached, and sub-associations that aren't archived. Then:
  - revokes scheduled and pending invitations, each with reason `CommunityArchived`;
  - cancels autopay, each with reason `CommunityArchived` (R9);
  - enqueues one "no longer managed" email per linked resident (R10).
- **Restore.** The community returns to Active if `ActivatedAt != null` and it has an open-ended manager, and to Onboarding otherwise. Restoring a sub-association whose parent is Archived is refused with `PARENT_ARCHIVED`.

A **preview** endpoint per transition returns the counts and names the dialogs show (brief 04 D4) without changing anything.

**Rationale**: The spec fixes the states and rules (FR-010–FR-015). Storing "activated" as a timestamp avoids a separate "pre-archive status" column; FR-010 only needs to know whether the community was ever live.

## R5. Concurrency: last manager, last administrator, invitation state

**Decision**: Use Postgres transaction-scoped advisory locks inside the EF execution strategy, the pattern `LedgerService` already uses (`pg_advisory_xact_lock`). Retries are on (`EnableRetryOnFailure(3)`), so every locked unit runs through `CreateExecutionStrategy().ExecuteAsync`.

- `CommunityLocks.RunAsync(communityId, body)` locks on a key derived from the community. Every manager-membership change, every invitation acceptance or revoke, and activate, archive and restore take it. Two managers ending each other (US2 #14) serialize, so the second request sees one manager left and gets `LAST_MANAGER`.
- `CompanyLocks.RunAsync(companyId, body)` does the same for Company Administrator revokes (US7 #4).
- Invitation state changes also take a row lock: `SELECT … FOR UPDATE`, as in `ArcLocks`. Accept, revoke and resend on the same invitation therefore resolve to exactly one winner (FR-022).

**Rationale**: Today's `LAST_MANAGER` check is a plain count with no lock (`MembershipUpdateEndpoint.cs:48-64`), so it is racy. Advisory locks serialize per community without locking the table, and they are already used in this codebase.

**Alternatives considered**: `SERIALIZABLE` transactions with retry. Rejected: retry-on-failure plus serialization failures makes the outcome harder to reason about and test.

## R6. Invitations

**Decision**: One `Invitations` table for all three kinds: `Membership`, `Residency` and `CompanyAdministrator`.

**Token**: 32 bytes from `RandomNumberGenerator`, base64url-encoded (256 bits), the same as `AuthCrypto.NewProofToken`. Only `AuthCrypto.Hash(token)` (SHA-256) is stored.
- The link is `{AppBaseUrl}/invite#t={token}`. A URL fragment is never sent to the server, and never written to logs or `Referer` (spec Security).
- The page reads the fragment and POSTs the token in a request body.
- `/invite` sets `Referrer-Policy: no-referrer` via a meta tag. The Angular app has no Sentry SDK; its client telemetry is OpenTelemetry (`otel.bootstrap.ts`), which strips URL fragments from span attributes.

**Timing**: `SentAt` and `ExpiresAt = SentAt + 72h` come from the injected `TimeProvider` (UTC). Expiry is derived (`State == Pending && now >= ExpiresAt`), not swept. Responses expose a computed `effectiveState`.

**States**: `Scheduled`, `Pending`, `Accepted`, `Revoked`. *Expired* is the derived state.
- A revoke stores a reason: `Manual`, `CommunityArchived`, `SenderLostAuthority` or `Superseded`.
- A resend writes a new token hash, `SentAt` and `ExpiresAt`, sets `SenderUserId` to the resender, and increments `ResendCount`. The old hash is gone, so the old link becomes `INVITATION_INVALID`.

**Duplicates**:
- Filtered unique indexes enforce one open invitation per target where `State IN ('Scheduled','Pending')`:
  - `(CommunityId, NormalizedEmail, Role)` for memberships;
  - `(PropertyId, NormalizedEmail)` for residency;
  - `(CompanyId, NormalizedEmail)` for Company Administrator invitations.
- A new invitation for a target whose open invitation has already *expired* first revokes that row with reason `Superseded`, in the same transaction, then inserts the new one.
- "Already holds the role or link" is checked in code (`ALREADY_MEMBER`, `ALREADY_RESIDENT`).

**Sender authority**: checked at acceptance and at activation-time send by re-running `DecideAsync` for the sender with the invitation's capability (FR-022). When a manager membership ends or is downgraded, or a Company Administrator grant is revoked, a `SenderAuthority.RevokeOrphanedAsync(userId, communityId?)` step runs in the same transaction. It revokes every open invitation that user can no longer send.

**Delivery**: invitation emails are **sent directly** through the email `IAlertProvider` at send time, the way `EmailAuthNotifier` sends verification codes. They are **not** written to `OutboxMessages`, because the outbox payload is stored in plain text and would hold a live token.
- A failed send leaves the invitation Pending with `LastSendError` set. The list shows "Couldn't send. Resend.".
- Every attempt is recorded in `InvitationDeliveries(InvitationId, NormalizedEmail, AttemptedAt, Succeeded)`. That table also feeds the per-recipient cap (R12).

**Acceptance endpoints** (`Features/Invitations/`, anonymous or authenticated, never through the resolver):
- `POST /invitations/preview`: token → what is offered, or a refusal code.
- `POST /invitations/register`: token, names and password → a new account with `EmailConfirmed = true`, no claim code, and the grant applied (FR-022a).
- `POST /invitations/accept`: signed-in caller plus token → the grant applied.

All three check the refusals in FR-022's order: invalid, then expired, then email mismatch. Every failure is logged (FR-023).

**Rationale**: One table gives one lifecycle and one set of tests for FR-022. A URL fragment is the standard way to keep a bearer token out of server and edge logs. Direct sending avoids persisting tokens, and the outbox's payload model can't hold a token safely.

**Alternatives considered**:
- Three invitation tables. Rejected: the lifecycle would be duplicated three times.
- A token in a query string. Rejected: query strings appear in Cloud Run and Cloudflare logs.
- The outbox with a token reference. Rejected: the dispatcher renders a stored payload, so a reference would need a second render path just for invitations.

## R7. The open-ended-manager rule and membership changes

**Decision**: A pure `ManagerRule.Evaluate(currentRows, proposedChange)` returns whether the community would still have at least one Community Manager row with `Status = Active && EndDate == null`, and it is unit-tested with `[Theory]` data. It runs inside the community lock for every:
- manager create or update (end, downgrade, deactivate, new or earlier end date);
- invitation acceptance for the CommunityManager role;
- replace.

Archived communities skip it (FR-020).

**Unique index**: the existing unique index on `(UserId, CommunityId, Role)` stays.
- Appointing someone who held the role before, directly or by invitation, **reactivates** the old row: Status Active, StartDate today, and EndDate from the request or the invitation.
- A **downgrade** ends the CommunityManager row (EndDate = today, Status Inactive) and reactivates or creates the BoardMember row (FR-019).

This removes today's two hazards: a role change that collides with the unique index (a 500), and "already holds this role" for someone who returns after ending.

The 025 create and update endpoints get these rules, `board-writes` rate limiting and `ManageMemberships` or `ManageCommunityManagers` as the role requires. Their request shapes are unchanged, plus an optional `email` on create (R6, FR-018).

## R8. Resident access checked on every request (FR-024g, FR-012a)

**Decision**: Add a scoped `ResidentAccess` service:

```csharp
Task<ResidentContext> RequireAsync(ClaimsPrincipal user, ResidentOperation op, CancellationToken ct);
// op: Read | ReadHistory | Write
```

It reads `propertyId` from the token. If the claim is missing, it returns `PROPERTY_ACCESS_DENIED` 403. Then it runs one query:

```sql
SELECT p."Id", p."CommunityId", c."Status"
FROM "UserProperties" up
JOIN "Properties" p ON p."Id" = up."PropertyId"
JOIN "Communities" c ON c."Id" = p."CommunityId"
WHERE up."UserId" = @user AND up."PropertyId" = @property
```

- No row: `PROPERTY_ACCESS_DENIED`.
- Onboarding: `COMMUNITY_NOT_ACTIVE`.
- Archived:
  - `ReadHistory` (ledger, statements, receipts, transactions, unpaid assessments, and the home's submitted architectural requests from spec 029): allowed;
  - every other operation: `COMMUNITY_ARCHIVED`.

It returns the property and **community from the database, not the token** (FR-012a: "never from the token").

Every resident endpoint in `Features/Payments`, `Features/Property` (including spec 029's `Property/Architectural`, through `ResidentArcScope`), `Features/Community` (including `Community/Violations`) and `Features/Dashboard` (about 45 endpoints, listed in the contract) replaces `Guid.Parse(User.FindFirst("propertyId")!…)` / `RequirePropertyId()` / `RequireCommunityId()` with a call to `RequireAsync`. The static test in R3 enforces it.

**Cost**: one indexed lookup per request, on the unique `(UserId, PropertyId)` index. That is acceptable under the Neon pool limits.

**Rationale**: The spec's "next request" promise (SC-011) can't be kept from token claims (15-minute tokens). An explicit call per endpoint is visible in review and testable by the static check. A FastEndpoints global pre-processor would hide the operation kind, which differs per endpoint.

**Alternatives considered**:
- Shorter tokens. Rejected: they still leave a window, and refresh churn rises.
- Revoking refresh tokens on removal. Rejected: refresh already re-derives the home, and per-request checks make it unnecessary (spec review F06).

## R9. Autopay attribution and cancellation (FR-013, FR-024c)

**Decision**: `RecurringPayment` gains two fields:
- `EnrolledByUserId` (string, nullable, FK SetNull), set from the caller in `UpsertRecurringAsync`, which gains a `userId` parameter;
- `CancellationReason` (string(30), nullable: `owner_cancelled`, `community_archived` or `resident_removed`).

A new `PaymentService.CancelRecurringForAsync(propertyId, reason, ct)` reuses the existing cancel:
- Status becomes `"inactive"`;
- the current `PaymentAuthorization.TerminatedAt` is set, as spec 006 FR-011b requires; the mandate record is kept;
- the reason is recorded.

It makes **no Stripe calls**, matching today's owner-initiated cancel, which also leaves the vaulted method on the owner's customer. Removal cancels only when `EnrolledByUserId == removedUser || EnrolledByUserId == null` (FR-024c).

**Defense in depth**: `RecurringDraftService.RunDueDraftsAsync` adds a join filter on `Community.Status == Active`. Even a schedule left active in an archived community never drafts (SC-005).

**Migration**: for communities moving from `Inactive` to `Archived`, active schedules are set to inactive with reason `community_archived` and their authorizations terminated, in SQL, with no email (Database/runtime bullet).

**Rationale**: There is one schedule per property (unique index on `PropertyId`), billed to the owner's Stripe customer. Attribution is the only way to cancel the right schedule. Detaching payment methods from Stripe is not required by the spec, and today's cancel doesn't do it either. Recorded under Complexity Tracking as a known limit.

## R10. Notification emails (non-invitation)

**Decision**: Add a `CompanyEmailRenderer` modelled on `ArcEmailRenderer`. It renders plain text now; the HTML templates come from Claude Design brief 12 later. Its kinds are constants of at most 30 characters, because `OutboxMessage.Kind` is capped at 30:

| Kind | Recipient (outbox) | Dedup key |
|---|---|---|
| `co_community_added` | each Company Administrator (`RecipientUserId`) | `co:community-added:{communityId}:{userId}` |
| `co_community_archived` | each resident linked to the community's properties | `co:archived:{communityId}:{archivedAt:yyyyMMddHHmmss}:{userId}` |
| `co_resident_removed` | the removed resident | `co:removed:{propertyId}:{userId}:{removedAt:…}` |
| `co_autopay_ended` | each other resident of the property, after a removal-cancel | `co:autopay-ended:{recurringId}:{userId}:{at}` |
| `co_admin_recovery` | each current Company Administrator | `co:recovery:{grantId}:{userId}` |

All of these go to users with accounts, so the existing `RecipientUserId` path and the check constraint are enough, and the outbox schema doesn't change.

Archive can enqueue hundreds of rows, so the endpoint calls `DispatchPendingAsync` in a loop of at most 10 batches of 100. Anything left goes out on the existing reconcile backstop.

`ArchitecturalReviewOptions.AppBaseUrl` is reused for links. It is renamed in configuration only by adding an `App:BaseUrl` alias that both features read; the old key keeps working.

## R11. Sign-in without a home (FR-036a, US9)

**Decision**:

- `GetActivePropertyAsync` becomes `FindUsablePropertyAsync`. It returns `Property?`, choosing the user's linked property in an Active community first, then an Archived one (oldest link first, so the choice is stable), and never an Onboarding one. It no longer throws `NO_PROPERTY` from login, refresh or board-mode.
- `CreateTokenPairAsync(user, Property? property)` omits the `propertyId` and `communityId` claims when no property is given.
- `RefreshAsync` keeps rotation. Today it deletes the token before resolving the property; the deletion and the mint now happen in one transaction, so a failure can't strand the user.
- `CurrentUserDto` gains:
  - `properties[]` with `communityStatus` (the field already exists and is now filled and used);
  - `isCompanyAdministrator`;
  - `landing`, one of `Board`, `Company`, `Resident`, `NotLive` or `NoHome`, computed by a pure `LandingRule` server-side.

  The Angular `landingTargetFor` uses it. There is one rule and it is tested in both places.
- `BuildCurrentUserAsync` memberships and `/me/communities` include Onboarding communities and exclude Archived ones (FR-036b). Each item gains `communityStatus`.
- `SwitchModeAsync` board eligibility counts Onboarding communities too.
- `SwitchPropertyAsync` refuses Onboarding homes with `COMMUNITY_NOT_ACTIVE`.

**Rationale**: The spec requires all of this (F01). Computing the landing on the server keeps the client from deciding authorization-adjacent routing on its own data.

## R12. Rate limits and invitation-email caps (FR-041)

**Decision**:

- Writes use `board-writes`. That covers every new write endpoint and the 025 membership create and update.
- `/invitations/preview`, `/invitations/register` and `/invitations/accept` use the existing `auth` policy, partitioned by client (`ClientIdentityResolver.ResolveAuthPartition`).
- New validated options, `InvitationOptions` (section `Invitations`, FluentValidation, `AddValidatedOptions`):
  - `ExpiryHours = 72`, fixed by the spec; validated `== 72` so configuration can't drift from it;
  - `MaxEmailsPerRecipientPer24h = 5`;
  - `ResendCooldownSeconds = 60`.

  These are enforced in `InvitationService` against `InvitationDeliveries`. Exceeding them returns the existing 429 shape with `code: "RATE_LIMITED"`.
- The activation fan-out (FR-024b) bypasses the per-recipient cap.

## R13. Company default settings (FR-025, FR-026, FR-031)

**Decision**: `CompanyDefaultSettings(CompanyId PK, ReviewPeriodDays, LapseRule, DecisionRule, ReminderDays, TimeZoneId, FormalDisapprovalStatement(1000), UpdatedAt, UpdatedByUserId)`. The migration creates the one row with spec 027's values.

- **Creation copies eagerly**: creating a community inserts its `CommunityArcSettings` row, copied from the defaults (FR-009), in the same transaction.
- **Backfill**: the migration inserts a `CommunityArcSettings` row with 027's constants for every existing community that has none. From then on, the lazy `GetOrCreateSettingsAsync` path only fires for rows created before this migration ran, which no longer exist. It keeps using the 027 constants, never the company defaults, so FR-026 holds by construction.
- **Reset** updates the six rule fields only and leaves `NextApplicationNumber` alone (FR-031).
- **"Differs from defaults"** compares the six fields in SQL for the portfolio, and per field for the section's indicators.

## R14. Profile saved per field (FR-017, Clarifications 2026-10-14)

**Decision**: `PATCH /communities/{id}/profile` accepts any subset of the six fields. The UI sends one field per request when the person leaves it.

- Validation is per field: FR-006's rules, plus "a filled required field can't be cleared" (FR-017).
- The name check is case- and space-insensitive (R15).
- Each successful field is one `CommunityProfileChanged` sensitive event with that field's old and new value.

A Community Manager sending `parent` or `status` gets 403 `FORBIDDEN`; those are separate endpoints.

## R15. Community name uniqueness (FR-007)

**Decision**: Add a `NormalizedName` column, computed in code as `lower(trim(CommunityName))`, with a unique index on `(ManagementCompanyId, NormalizedName)`. It replaces the global, case-sensitive unique index on `CommunityName`.

Archived communities keep their row, so their names stay reserved.

The migration fills `NormalizedName`, then **checks for collisions before creating the index**:

```sql
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM "Communities"
             GROUP BY "ManagementCompanyId", "NormalizedName" HAVING count(*) > 1) THEN
    RAISE EXCEPTION 'Community names collide when compared without case and surrounding spaces; rename them before applying 030.';
  END IF;
END $$;
```

The refusal names the other community only when the caller is a Company Administrator (FR-007). A unique-violation catch maps races to the same `COMMUNITY_NAME_TAKEN`.

## R16. Residents area query and ordering (FR-024d)

**Decision**: `GET /communities/{id}/residents` runs one paged query over `Properties` in the community.

- **Residents** come from `UserProperties` joined to `AspNetUsers`, aggregated per page.
- **Invitations** are open, expired and revoked residency invitations, aggregated per page.
- **Search** is `ILIKE` on address, `FirstName || ' ' || LastName` and email (≤ 100 characters, escaped).
- **Filters** are `noResident`, `invitationPending` (Scheduled or unexpired Pending) and `invitationExpired`.
- **Order** follows design brief 06: street A–Z, then house and unit numbers in numeric order. That needs an ICU collation, created in the migration with `CREATE COLLATION IF NOT EXISTS natural_sort (provider = icu, locale = 'und-u-kn-true', deterministic = true)` and used as `ORDER BY "Address" COLLATE natural_sort`. Neon and Postgres 17 ship ICU. The Testcontainers image `postgres:17` does too.
- **Logging**: each load logs `BoardAssociationDataAccess` with resource `communities/residents` (FR-024d).

For the property drawer, `GET /communities/{id}/residents/{propertyId}` returns one property's detail. The list rows carry only the summary the drawer design needs.

## R17. Portfolio query (FR-034, SC-008)

**Decision**: `GET /company/communities` returns one row per community: name, legal name, status, parent name, open-ended managers (names), pending manager invitations (emails), the `noManager` flag and the `differsFromDefaults` flag.

- Built as a single SQL projection with `EXISTS` and aggregated subqueries.
- Default order is `NormalizedName` A–Z. `sort` accepts `name`, `status` or `parent`, with a direction.
- Filters are `status` and `noManager`; search is on community or legal name.

200 communities is small. The 2-second target is met with the existing indexes plus `(ManagementCompanyId, NormalizedName)`.

## R18. The one-time setup step (FR-038)

**Decision**: Extend `StartupTasks` with `--grant-company-admin <email> [--recover] [--company-name "<name>"]`, beside the existing `--seed` check. It runs before `app.Run()`, as `--seed` does. Unlike `--seed`, it is allowed in every environment, because it is the operator path. It applies migrations first, then calls `CompanyAdministratorService.SetupAsync`.

- **No administrator exists**:
  - if the email has an account, grant the role (`Source = Setup`);
  - otherwise create a Company Administrator invitation and send it (R6);
  - print the result and exit 0.
- **An administrator already exists and `--recover` is absent**: print "A Company Administrator already exists; nothing changed" and exit 0 with no writes.
- **`--recover`**: grant or invite even though administrators exist, log `CompanyAdminRecovery`, and enqueue `co_admin_recovery` to every current administrator.
- **`--company-name`** sets `ManagementCompanies.Name` (used in emails and the company sign-in page).

The step is invoked as a Cloud Run job execution, or `docker run … --grant-company-admin owner@example.com`, documented in quickstart.md. There is no HTTP endpoint.

The dev seed (`DatabaseSeeder`, dev-like environments only) calls the same `SetupAsync` for `company@nekohoa.dev`. Seed invitations are only ever sent to `@simulator.amazonses.com` addresses or captured by the dev `AuthCodeVault` (extended to capture invitation links for e2e).

## R19. Sensitive events (FR-023, FR-024e, FR-039, FR-040)

**Decision**: New Serilog templates. They carry IDs only: no emails, names, addresses or tokens. Values are community fields and settings, which are not personal data. Each caused change logs its own event, carrying the parent's `Actor` and a `Cause` property (FR-040).

| Event | Template (abbreviated) |
|---|---|
| `CommunityLifecycle` | `Actor= Community= Action={Created|Activated|Archived|Restored|ParentChanged} From= To= At=` |
| `CommunityProfileChanged` | `Actor= Community= Field= Old= New= At=` |
| `CompanyDefaultsChanged` / `ArcSettingsChanged` (027, reused for reset) | `Actor= Old= New= At=` |
| `BoardMembershipChange` (025, extended) | adds `Cause=` and `Kind={grant|edit|end|downgrade|replace-end|replace-grant|reactivate}` |
| `InvitationChange` | `Actor= Community= Property= Invitation= Kind= Change={Created|Scheduled|Sent|Resent|Revoked|Accepted} Reason= Cause= At=` |
| `InvitationAcceptFailed` | `Invitation={id or "unknown"} Community= Property= User= Reason={code} At=` |
| `ResidentLinkChange` | `Actor= Community= Property= TargetUser= Change={Linked|Removed} Cause= At=` |
| `AutopayCancelled` | `Actor= Property= Recurring= Reason= Cause= At=` |
| `CompanyAdminChange` | `Actor={id or "setup"} Company= TargetUser= Invitation= Change={Granted|Revoked|Recovery} At=` |

Tests assert them through `InMemoryLogSink`. A test also asserts that none of the events' rendered properties matches an email pattern (spec Quality gates).

The existing gap, where `TelemetryScrubbingEnricher` is never registered and `LoginAsync` logs `{Email}`, is out of this spec's scope and has been raised as a separate task. This spec's own events don't depend on that scrubber.

## R20. Frontend architecture (designs pending)

**Decision**: Build structure and behavior against the Claude Design briefs. Visual details (spacing, the exact component chrome) are marked *pending handoff* and finished when `design/handoff/` lands. The existing tokens in `styles.scss` are used. The briefs' decisions are binding:
- the company sign-in page;
- the Company menu;
- the searchable side list;
- per-field save with "✓ Saved";
- the disabled Mark Active button with its reason tooltip;
- the property drawer;
- A–Z portfolio sorting with the "No manager" filter.

**Routes** (`app.routes.ts`):

| Path | Component | Guard |
|---|---|---|
| `/company/sign-in` | `CompanySignInComponent` (public) | — |
| `/invite` | `InvitationPageComponent` (public; reads `#t=`) | — |
| `/app/no-home`, `/app/not-live` | `NoHomeComponent`, `NotLiveComponent` | `authGuard` |
| `/app/company/portfolio` | `PortfolioComponent` | `companyAdminGuard` |
| `/app/company/communities/new` | `AddCommunityComponent` | `companyAdminGuard` |
| `/app/company/defaults` | `CompanyDefaultsComponent` | `companyAdminGuard`, or a Community Manager read-only via `/app/board/company-defaults` |
| `/app/company/administrators` | `CompanyAdministratorsComponent` | `companyAdminGuard` |
| `/app/company/communities/:communityId/settings/:section` | `CommunitySettingsPageComponent` | `companyAdminGuard` |
| `/app/board/settings/:section` | the same page for the active community | `boardGuard` with `requiredRoles: ['CommunityManager']` |
| `/app/board/communities/new` | `AddCommunityComponent` (manager variant) | `boardGuard` with CommunityManager |
| `/app/board/arc-settings`, `/app/board/memberships` | redirects to the matching settings section (FR-030) | — |

**Shell**: `ShellComponent` gains a "company area" state derived from the router URL (`/app/company/**`). It shows company nav (Portfolio · Add community · Company defaults · Company administrators), no board banner, and the Company menu in the account cluster for Company Administrators. The board nav gains "Community settings" and "Add community" entries via `BOARD_NAV`.

**New shared pieces**:
- `ToastService` and `ToastHostComponent`: none exists today. It is a polite live region.
- `DisabledReasonDirective`: a tooltip on hover **and** focus, using `aria-describedby` and `aria-disabled`, and closed with Esc.
- `AutosaveFieldDirective`: saves on blur or change and shows "Saving…" then "✓ Saved", with errors under the field.
- `SettingsSideNavComponent`: the side list and settings search. It matches a static registry of setting labels to section anchors.
- `PropertyDrawerComponent`: the glossary-panel pattern, with focus management.

**Services**:
- `CompanyService`: portfolio, communities, lifecycle, defaults, administrators.
- `CommunitySettingsService`: profile, managers and memberships, invitations, residents, ARC settings and reset.
- `InvitationService`: preview, register, accept.
- `AuthService` keeps `properties`, `isCompanyAdministrator` and `landing` from `/auth/me`.

**Interceptor**: a 403 `PROPERTY_ACCESS_DENIED` triggers `/auth/me`, then navigation to the new landing with the brief 11 notice. A 409 `COMMUNITY_ARCHIVED` on resident pages renders the archived notice component instead of the page.

**Retired**: `membership-admin` (raw User ID entry) is replaced by the Managers and memberships section, and its tests move with it.

**Rationale**: The design decisions are made. Only pixels are pending, so building behavior now and restyling on handoff avoids blocking. Storybook stories for every new component make the restyle a contained change.

## R21. What stays out (recorded so nobody builds it)

- No Stripe payment-method detach or mandate cancel on removal or archive (R9).
- No owner-of-record changes, and no owner or tenant distinction (spec Terms).
- No property import (spec Assumptions; a follow-up Properties spec).
- No notification-settings opt-outs (the Notification Settings spec).
- No change to the 027 ARC sweep beyond skipping non-Active communities (defense in depth; archive already requires no open applications).
