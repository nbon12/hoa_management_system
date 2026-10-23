# Data Model: Company Administration

**Feature**: `030-company-administration` | **Date**: 2026-10-17 | **Research**: [research.md](./research.md)

All changes ship in one forward-only EF Core migration, `<timestamp>_AddCompanyAdministration`, applied idempotently at Cloud Run startup. Conventions:

- **Times** are `timestamptz` (UTC); plain dates are `date`.
- **Enums** are stored as strings (`HasConversion<string>()`), as in the rest of the schema.
- **User references** are `string` FKs to `AspNetUsers` with `SetNull` on delete, unless noted.

## Enums (`HOAManagementCompany/Domain/Enums/`)

| Enum | Values | Notes |
|---|---|---|
| `CommunityStatus` *(changed)* | `Onboarding`, `Active`, `Archived` | Was `Active`, `Inactive`. The migration maps `Inactive` to `Archived`. |
| `InvitationKind` | `Membership`, `Residency`, `CompanyAdministrator` | R6 |
| `InvitationState` | `Scheduled`, `Pending`, `Accepted`, `Revoked` | *Expired* is derived: `Pending` and `now ≥ ExpiresAt` |
| `InvitationRevokeReason` | `Manual`, `CommunityArchived`, `SenderLostAuthority`, `Superseded` | |
| `CompanyGrantSource` | `Setup`, `Invitation`, `Recovery` | How a Company Administrator grant came about |
| `ScopeDecision` | `Allowed`, `Forbidden`, `Archived`, `NotActive` | Resolver result (R2); not stored |
| `CompanyCapability` | `CreateCommunity`, `ViewCompanyDefaults`, `ManageCompanyDefaults`, `ManageCompanyAdministrators`, `ViewPortfolio` | Not stored |
| `CommunityCapability` *(extended)* | adds `ManageCommunityManagers`, `ReplaceLastManager`, `EditCommunityProfile`, `ManageResidents`, `ViewArcSettings`, `ManageArcSettings`, `ManageCommunityLifecycle`, `ViewCommunitySettings` | Not stored |

## New entities

### `ManagementCompany`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid PK | |
| `Name` | string(200) | Required. Set by the migration to `"Management company"` and by `--company-name` (R18). |
| `CreatedAt` | timestamptz | |

Exactly one row per deployment for now. Nothing in code assumes only one: every query filters by `ManagementCompanyId`.

### `CompanyAdministratorGrant`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid PK | |
| `CompanyId` | Guid FK → `ManagementCompanies` | Cascade |
| `UserId` | string FK → `AspNetUsers` | Cascade |
| `Source` | `CompanyGrantSource` | |
| `GrantedByUserId` | string? | Null when `Source = Setup` |
| `GrantedAt` | timestamptz | |
| `InvitationId` | Guid? FK → `Invitations` | Set when accepted from an invitation |
| `RevokedAt` | timestamptz? | |
| `RevokedByUserId` | string? | |

**Indexes**:
- unique `(CompanyId, UserId) WHERE "RevokedAt" IS NULL`;
- `(UserId)`.

**Live grant**: `RevokedAt IS NULL`. Revoking the last live grant is refused with `LAST_COMPANY_ADMIN`, under `CompanyLocks` (R5).

### `CompanyDefaultSettings`

| Field | Type | Rules |
|---|---|---|
| `CompanyId` | Guid PK + FK → `ManagementCompanies` | One row per company; the migration creates it |
| `ReviewPeriodDays` | int | 1–365, initial 30 |
| `LapseRule` | `ArcLapseRule` | Initial `FlagOverdueOnly` |
| `DecisionRule` | `ArcDecisionRule` | Initial `MajorityOfMembers` |
| `ReminderDays` | int | 0–30, initial 7 |
| `TimeZoneId` | string(64) | IANA id; must resolve; initial `America/New_York` |
| `FormalDisapprovalStatement` | string(1000) | Required; initial = 027's default text |
| `UpdatedAt` | timestamptz | |
| `UpdatedByUserId` | string? | |

These are the same six rule fields as `CommunityArcSettings`, with the same validation, shared through `ArcRuleValidator`.

### `Invitation`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid PK | Appears in logs in place of the email (FR-040) |
| `Kind` | `InvitationKind` | |
| `CompanyId` | Guid FK → `ManagementCompanies` | Always set |
| `CommunityId` | Guid? FK → `Communities` | Required for `Membership` and `Residency`; null for `CompanyAdministrator` |
| `PropertyId` | Guid? FK → `Properties` | Required for `Residency`; must belong to `CommunityId` |
| `Role` | `CommunityRole`? | Required for `Membership`: `CommunityManager`, `BoardMember` or `Accountant` |
| `TermEndDate` | date? | `Membership` only; must not be in the past when sent or accepted |
| `Email` | string(256) | As typed, trimmed |
| `NormalizedEmail` | string(256) | `trim().ToLowerInvariant()`, the same as `EmailVerificationService.Normalize` |
| `TokenHash` | string(64)? | SHA-256 hex of the current link token. Null while `Scheduled`; replaced on resend. |
| `State` | `InvitationState` | |
| `SenderUserId` | string? | The current sender; updated on resend (FR-022). `Restrict` on delete. Null only for a `CompanyAdministrator` invitation created by the setup step, which passes the sender-authority check until a Company Administrator resends it. |
| `CreatedAt` | timestamptz | |
| `SentAt` | timestamptz? | Null while `Scheduled` |
| `ExpiresAt` | timestamptz? | `SentAt + 72h` |
| `ResendCount` | int | |
| `LastSendError` | string(200)? | Set when the direct send failed; cleared on a successful send |
| `AcceptedAt` | timestamptz? | |
| `AcceptedByUserId` | string? | |
| `RevokedAt` | timestamptz? | |
| `RevokedByUserId` | string? | Null when revoked by the system (archive, sender authority) |
| `RevokeReason` | `InvitationRevokeReason`? | |

**Indexes** (R6):
- unique `TokenHash WHERE "TokenHash" IS NOT NULL`;
- unique `(CommunityId, NormalizedEmail, Role) WHERE "Kind" = 'Membership' AND "State" IN ('Scheduled','Pending')`;
- unique `(PropertyId, NormalizedEmail) WHERE "Kind" = 'Residency' AND "State" IN ('Scheduled','Pending')`;
- unique `(CompanyId, NormalizedEmail) WHERE "Kind" = 'CompanyAdministrator' AND "State" IN ('Scheduled','Pending')`;
- `(CommunityId, State)`, `(PropertyId)`, `(SenderUserId, State)`.

**Check constraint** `CK_Invitations_KindShape`. Its SQL lives in the migration.
- `Membership` requires `CommunityId` and `Role`, with `PropertyId` null.
- `Residency` requires `CommunityId` and `PropertyId`, with `Role` null.
- `CompanyAdministrator` requires `CommunityId`, `PropertyId` and `Role` all null.
- `SenderUserId` may be null only for `CompanyAdministrator`.

**State transitions** (FR-022):

```text
                      activate (sender still authorized)
   [create, community Onboarding, Residency] ──► Scheduled ───────────────────► Pending
   [create, otherwise] ─────────────────────────────────────────────────────► Pending
   Scheduled ── revoke / archive / sender lost authority ──► Revoked
   Scheduled ── resend ──► refused COMMUNITY_NOT_ACTIVE (state unchanged)
   Pending   ── accept (valid, unexpired, email matches, sender authorized) ──► Accepted
   Pending   ── resend (cooldown + cap ok) ──► Pending (new TokenHash, SentAt, ExpiresAt, SenderUserId)
   Pending   ── revoke / archive / sender lost authority / superseded ──► Revoked
   Pending   ── time passes ExpiresAt ──► (derived) Expired: accept → INVITATION_EXPIRED; resend → Pending; revoke → Revoked
   Accepted, Revoked ── any action ──► refused INVITATION_INVALID
```

Records are never deleted.

### `InvitationDelivery`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid PK | |
| `InvitationId` | Guid FK → `Invitations` | Cascade |
| `NormalizedEmail` | string(256) | Copied, so the per-recipient cap can be counted with one index |
| `AttemptedAt` | timestamptz | |
| `Succeeded` | bool | |
| `BypassesRecipientCap` | bool | True for the activation fan-out (FR-041c) |

**Index**: `(NormalizedEmail, AttemptedAt)`.

## Modified entities

### `Community`

| Field | Change |
|---|---|
| `ManagementCompanyId` | **new**, Guid FK → `ManagementCompanies`, `Restrict`; backfilled to the one company; not null |
| `NormalizedName` | **new**, string(80), `lower(trim(CommunityName))`; unique with `ManagementCompanyId` (R15) |
| `Status` | values become `Onboarding`, `Active`, `Archived` |
| `CreatedByUserId` | **new**, string? |
| `ActivatedAt` / `ActivatedByUserId` | **new**. Backfilled `ActivatedAt = CreatedAt` for existing Active communities. |
| `ArchivedAt` / `ArchivedByUserId` | **new**. Backfilled `ArchivedAt = now()` for migrated `Inactive` rows. |
| `RestoredAt` / `RestoredByUserId` | **new** |
| `LegalName`, `County`, `FormationDate`, `ManagementStartDate`, `Description` | unchanged columns. FR-006 lengths are enforced in code; legal name ≤ 200, description ≤ 2,000. |

**Index changes**:
- **dropped**: the global unique index on `CommunityName`;
- **added**: unique `(ManagementCompanyId, NormalizedName)`;
- **added**: `(ManagementCompanyId, Status)`.

**Status transitions** (FR-010; all by a Company Administrator, under `CommunityLocks`):

```text
   [created] ──► Onboarding ── activate (has open-ended manager) ──► Active
   Onboarding ── archive (no open work) ──► Archived
   Active     ── archive (no open work) ──► Archived
   Archived   ── restore ──► Active      if ActivatedAt != null AND has open-ended manager
                       └─► Onboarding  otherwise
   Archived sub-association ── restore while parent Archived ──► refused PARENT_ARCHIVED
```

### `CommunityMembership`

The columns and the unique `(UserId, CommunityId, Role)` index are unchanged. The behavior changes (R7):

- **Re-appointment** reactivates the existing row for that role: Status Active, StartDate today, EndDate from the request or invitation.
- **Downgrade** ends the CommunityManager row and reactivates or creates the BoardMember row.
- **"Effective"** means `Status = Active AND (EndDate IS NULL OR EndDate >= today UTC)`.
- **"Open-ended manager"** means `Role = CommunityManager AND Status = Active AND EndDate IS NULL`.

### `CommunityArcSettings`

The shape is unchanged. Behavior:
- a row is created eagerly at community creation, copied from `CompanyDefaultSettings`;
- the migration backfills a row from the 027 constants for every community that lacks one;
- reset updates the six rule fields and never `NextApplicationNumber`.

### `RecurringPayment`

| Field | Change |
|---|---|
| `EnrolledByUserId` | **new**, string? FK → `AspNetUsers`, `SetNull`; set on every enroll or re-enroll (R9) |
| `CancellationReason` | **new**, string(30)?: `owner_cancelled`, `community_archived`, `resident_removed`; set when `Status` becomes `inactive` |

`PaymentAuthorization` is unchanged. Cancelling still sets `TerminatedAt` on the current authorization (spec 006 FR-011b).

### `UserProperty` (the resident link)

The shape is unchanged; a property may already have several rows. Removal deletes the row. The audit trail is the `ResidentLinkChange` event. Created by claim-code registration (unchanged), by `POST /invitations/register` and by `POST /invitations/accept` for `Residency` invitations.

### `OutboxMessage`

Unchanged. New kinds (R10): `co_community_added`, `co_community_archived`, `co_resident_removed`, `co_autopay_ended`, `co_admin_recovery`, each ≤ 30 characters, all with `RecipientUserId`. Invitation emails do **not** use the outbox (R6).

## Migration outline (`<ts>_AddCompanyAdministration`)

1. Create `ManagementCompanies`. Insert one row (`Name = 'Management company'`).
2. Create `CompanyDefaultSettings` and insert that company's row with the 027 defaults.
3. `Communities`:
   - add the new columns;
   - set `ManagementCompanyId` to the company and `NormalizedName = lower(btrim("CommunityName"))`;
   - set `ActivatedAt = "CreatedAt"` where `Status = 'Active'`;
   - set `Status = 'Archived', ArchivedAt = now()` where `Status = 'Inactive'`;
   - make `ManagementCompanyId` and `NormalizedName` not null.
4. Run the collision check (R15), which raises an exception on duplicates. Then drop the old `CommunityName` unique index and create the new indexes.
5. Insert `CommunityArcSettings` with the 027 constants for every community that has none (`INSERT … SELECT … WHERE NOT EXISTS`).
6. Create `CompanyAdministratorGrants`, `Invitations` (with the check constraint and filtered indexes) and `InvitationDeliveries`.
7. `RecurringPayments`: add `EnrolledByUserId` and `CancellationReason`. For properties in `Archived` communities with `Status = 'active'`:
   - set `Status = 'inactive'`, `CancellationReason = 'community_archived'`, `UpdatedAt = now()`;
   - set `TerminatedAt = now()` on their current `PaymentAuthorizations`.

   No email is sent (spec Database/runtime).
8. `CREATE COLLATION IF NOT EXISTS natural_sort (provider = icu, locale = 'und-u-kn-true')` for the Residents ordering (R16).

Steps 3–7 are plain SQL in `migrationBuilder.Sql(...)`, as in `20260825181517_AddCommunityAndMembership`. Each step is idempotent where it touches data (`WHERE NOT EXISTS`, `WHERE … IS NULL`). The migration is verified by a Testcontainers test against a seeded database with one Active and one Inactive community, an active autopay schedule and no ARC settings rows (spec Database/runtime).

## Relationships (summary)

```text
ManagementCompany 1 ─── * Community (ManagementCompanyId)
ManagementCompany 1 ─── 1 CompanyDefaultSettings
ManagementCompany 1 ─── * CompanyAdministratorGrant ─── 1 ApplicationUser
Community 1 ─── * CommunityMembership ─── 1 ApplicationUser
Community 1 ─── 1 CommunityArcSettings
Community 1 ─── * Property 1 ─── * UserProperty (resident link) ─── 1 ApplicationUser
Property 1 ─── 0..1 RecurringPayment (EnrolledByUserId → ApplicationUser)
Invitation * ─── 1 ManagementCompany; 0..1 Community; 0..1 Property; 1 sender (ApplicationUser)
Invitation 1 ─── * InvitationDelivery
```
