# Contract: Company Area and Community Lifecycle

**Common conventions**:
- All routes carry the global `api/v1` prefix.
- Every response sets `Cache-Control: no-store` (`BoardHttp.NoStore`).
- Errors use `{ code, message }`; validation failures are 422 `VALIDATION_ERROR` with `errors[]`.
- A caller who fails authorization gets the 025 body `403 { code: "FORBIDDEN", message: "You do not have access to this community." }`, identical for a community that doesn't exist (FR-004a).
- Every write carries `RequireRateLimiting("board-writes")` (FR-041a).
- Capabilities are from research R2. "Company" checks use `CanAccessCompanyAsync`; "community" checks use `DecideAsync`.

The error codes and their HTTP statuses for this feature are listed once, in [invitations-and-auth.md § Error codes](./invitations-and-auth.md#error-codes).

---

## POST /communities

Add a community (US1, FR-006–FR-009, FR-016).

- **Auth**: `CreateCommunity` (company, kind b). When `parentCommunityId` is set and the caller is not a Company Administrator, the parent must also pass `DecideAsync(parent, ManageCommunityManagers, Read) == Allowed`. Otherwise the request gets the 403 `FORBIDDEN` body, the same as for a parent that doesn't exist (US1 #8).
- **Body**:

```json
{
  "legalName": "Keystone Park HOA, Inc.",
  "communityName": "Keystone Park",
  "county": "Hamilton",
  "formationDate": "2004-03-14",
  "managementStartDate": "2026-11-01",
  "description": null,
  "parentCommunityId": null
}
```

  | Field | Rule |
  |---|---|
  | `legalName` | required, ≤ 200 |
  | `communityName` | required, 2–80 after trimming |
  | `county` | required, ≤ 100 |
  | `formationDate` | required, not after today (UTC) |
  | `managementStartDate` | required |
  | `description` | optional, ≤ 2,000 |
  | `parentCommunityId` | optional; must be a non-archived community of the same company |

- **Effects**, in one transaction:
  - insert the community with `Status = Onboarding`;
  - copy `CommunityArcSettings` from the company defaults;
  - if the caller is not a Company Administrator, insert an open-ended `CommunityManager` membership for the caller (FR-008);
  - log `CommunityLifecycle Action=Created`.

  After commit, when the caller added it as a Community Manager, enqueue `co_community_added` for every Company Administrator.
- **201**:

```json
{ "id": "guid", "communityName": "Keystone Park", "status": "Onboarding", "settingsPath": "/app/company/communities/{id}/settings/profile" }
```

  `settingsPath` points at the company route for a Company Administrator, and at `/app/board/settings/profile` for a Community Manager.
- **Errors**:
  - 403 `FORBIDDEN`;
  - 409 `COMMUNITY_NAME_TAKEN`. The message names the other community only for a Company Administrator, and says *"That community name is unavailable."* to anyone else (FR-007);
  - 409 `PARENT_ARCHIVED`;
  - 422 `VALIDATION_ERROR`.

## GET /company/communities

The portfolio (US6, FR-034, FR-035).

- **Auth**: `ViewPortfolio` (company, kind c).
- **Query**:

  | Param | Values | Default |
  |---|---|---|
  | `status` | `onboarding`, `active`, `archived` | all |
  | `noManager` | `true` | off |
  | `search` | community or legal name, case-insensitive partial match, ≤ 100 characters | none |
  | `sort` | `name`, `status`, `parent` | `name` |
  | `dir` | `asc`, `desc` | `asc` |
  | `limit` / `offset` | 25 / 0; limits above 100 are clamped | |

- **200**:

```json
{
  "items": [{
    "id": "guid",
    "communityName": "Keystone Park",
    "legalName": "Keystone Park HOA, Inc.",
    "status": "Onboarding",
    "parent": null,
    "managers": [],
    "pendingManagerInvitations": [{ "email": "pat@example.com", "expiresAt": "2026-10-14T13:15:00Z" }],
    "noManager": true,
    "differsFromDefaults": false
  }],
  "total": 31, "limit": 25, "offset": 0,
  "counts": { "all": 31, "onboarding": 3, "active": 26, "archived": 2, "noManager": 2 }
}
```

  - `managers` lists open-ended managers only, as `{ "userId": "…", "name": "Jordan Blake" }`.
  - `counts` ignores `search` and paging.
  - `noManager` means there is no open-ended Community Manager membership.
  - `differsFromDefaults` compares the six ARC rule fields (R13).
- **Errors**: 403 `FORBIDDEN`; 422 `VALIDATION_ERROR`.

## Lifecycle: preview and act

The dialogs ask the server first, then act (brief 04 D4).

### GET /communities/{communityId}/lifecycle/{action}/preview

`action` is `activate`, `archive` or `restore`.

- **Auth**: `ManageCommunityLifecycle`, read (Company Administrator only; allowed when Archived).
- **200**: a preview of what would happen, or the refusal that would be returned.

```json
{
  "action": "archive",
  "allowed": true,
  "refusal": null,
  "consequences": {
    "autopayCancelled": 3,
    "residentsEmailed": 79,
    "invitationsRevoked": [{ "id": "guid", "email": "drew@example.com", "kind": "Membership", "role": "CommunityManager" }],
    "membershipsKept": [{ "membershipId": "guid", "userId": "…", "name": "Morgan Ellis", "role": "CommunityManager", "endDate": null }]
  }
}
```

  - **Activate preview**: `consequences.scheduledInvitationsToSend` lists each scheduled invitation (`email`, `propertyAddress`) with its `expiresAtIfSentNow`. Scheduled invitations whose sender has lost authority are listed under `scheduledInvitationsRevoked`.
  - **Restore preview**: `consequences` holds:
    - `returnsTo` (`Active` or `Onboarding`);
    - `reason` (`WasActiveWithManager`, `NeverActivated` or `NoOpenEndedManager`);
    - `membershipsRegainingAccess[]`.
  - When the action would be refused, `allowed` is false and `refusal` is `{ code, message, items[] }`, for example `OPEN_WORK` with `items: [{ "kind": "ArcApplication", "label": "ARC-1041", "status": "Open" }, { "kind": "SubAssociation", "label": "Keystone Crossing SF", "status": "Active" }]`.

### POST /communities/{communityId}/activate

- **Auth**: `ManageCommunityLifecycle`, write.
- **Effects**, under the community lock (R5):
  1. require an open-ended manager;
  2. set `Status = Active`, `ActivatedAt` and `ActivatedByUserId`;
  3. move scheduled residency invitations whose sender is still authorized to `Pending`, with token, `SentAt` and `ExpiresAt`, and revoke the rest (`SenderLostAuthority`);
  4. commit, then send the invitation emails directly (R6).

  Log `CommunityLifecycle Action=Activated`, plus one `InvitationChange` per invitation.
- **200**: `{ "status": "Active", "invitationsSent": 2, "invitationsFailedToSend": 0, "invitationsRevoked": 0 }`.
- **Errors**: 403; 409 `NO_ACTIVE_MANAGER`; 409 `COMMUNITY_ARCHIVED`; 409 `INVALID_TRANSITION` (already Active).

### POST /communities/{communityId}/archive

- **Auth**: `ManageCommunityLifecycle`, write.
- **Effects**, under the community lock and in one transaction:
  1. check open work;
  2. set `Status = Archived`, `ArchivedAt` and `ArchivedByUserId`;
  3. revoke scheduled and pending invitations (`CommunityArchived`);
  4. cancel active autopay for the community's properties (`community_archived`);
  5. enqueue `co_community_archived` for every resident linked to the community's properties;
  6. commit, then dispatch.

  Each caused change is logged (FR-040).
- **200**: `{ "status": "Archived", "autopayCancelled": 3, "residentsEmailed": 79, "invitationsRevoked": 2 }`.
- **Errors**: 403; 409 `OPEN_WORK` (with `items[]`); 409 `INVALID_TRANSITION` (already Archived).

### POST /communities/{communityId}/restore

- **Auth**: `ManageCommunityLifecycle`, write (allowed while Archived).
- **Effects**: the community moves to `Active` or `Onboarding` per FR-010. `RestoredAt` and `RestoredByUserId` are set. Memberships, settings and their end dates are unchanged. Autopay is not restarted, and revoked invitations stay revoked.
- **200**: `{ "status": "Onboarding", "reason": "NoOpenEndedManager" }`.
- **Errors**: 403; 409 `PARENT_ARCHIVED` (names the parent); 409 `INVALID_TRANSITION` (not Archived).

### PUT /communities/{communityId}/parent

- **Auth**: `ManageCommunityLifecycle`, write.
- **Body**: `{ "parentCommunityId": "guid" | null }`.
- **Errors**:
  - 403;
  - 409 `COMMUNITY_ARCHIVED` (this community is archived);
  - 409 `PARENT_ARCHIVED`;
  - 422 `PARENT_CYCLE` (the chosen parent is this community or one of its descendants);
  - 422 `VALIDATION_ERROR`.
- **Logs**: `CommunityLifecycle Action=ParentChanged From= To=`.

## Company defaults

### GET /company/defaults

- **Auth**: `ViewCompanyDefaults` (company, kind b: Company Administrators and effective Community Managers).
- **200**:

```json
{ "reviewPeriodDays": 30, "lapseRule": "FlagOverdueOnly", "decisionRule": "MajorityOfMembers", "reminderDays": 7, "timeZoneId": "America/New_York", "formalDisapprovalStatement": "…", "updatedAt": null, "canEdit": true }
```

  `canEdit` is a UX hint only.

### PUT /company/defaults

- **Auth**: `ManageCompanyDefaults` (company, kind c).
- **Body**: the six fields. Validation is the same as the 027 ARC settings PUT: review period 1–365, reminder days 0–30, known enum names, a resolvable IANA zone, and a statement of 1 to 1,000 characters (R13).
- **Effects**: updates only the defaults row. Existing communities and applications are never touched (FR-026). Logs `CompanyDefaultsChanged Old= New=`.
- **200**: the updated body.
- **Errors**: 403; 422 `VALIDATION_ERROR`.

## Company Administrators

### GET /company/administrators

- **Auth**: `ManageCompanyAdministrators`.
- **Query**: `limit`/`offset` (25 / 0; limits above 100 are clamped).
- **200**, ordered by name A–Z:

```json
{
  "items": [{ "userId": "…", "name": "Robin Hale", "email": "owner@example.com", "grantedAt": "…", "grantedBy": null, "source": "Setup" }],
  "total": 2, "limit": 25, "offset": 0
}
```

  `grantedBy` is `null` when the source is `Setup`.

### GET /company/invitations

The Company Administrator invitations shown on the same page.

- **Auth**: `ManageCompanyAdministrators`.
- **Query**: `state` (optional), `limit`/`offset` (25 / 0; limits above 100 are clamped).
- **200**, newest first:

```json
{
  "items": [{ "id": "guid", "email": "ops@example.com", "state": "Pending", "effectiveState": "Pending", "revokeReason": null, "sentBy": "Robin Hale", "sentAt": "…", "expiresAt": "…", "canResend": true, "canRevoke": true }],
  "total": 1, "limit": 25, "offset": 0
}
```

### POST /company/administrators/invitations

- **Auth**: `ManageCompanyAdministrators`.
- **Body**: `{ "email": "ops@example.com" }`.
- **Effects**: creates and sends a `CompanyAdministrator` invitation (FR-037). The response is the same whether or not the email has an account (FR-018 by analogy).
- **201**: `{ "id": "guid", "state": "Pending", "expiresAt": "…" }`.
- **Errors**:
  - 403;
  - 409 `INVITATION_PENDING`;
  - 409 `ALREADY_MEMBER` (the email belongs to a current Company Administrator, who is visible on the same page);
  - 422 `SELF_GRANT_NOT_ALLOWED`;
  - 429 `RATE_LIMITED`.

### POST /company/administrators/{userId}/revoke

- **Auth**: `ManageCompanyAdministrators`.
- **Effects**, under the company lock:
  - refused when it would leave no live grant;
  - sets `RevokedAt` and `RevokedByUserId`;
  - in the same transaction, revokes every open invitation the user could only send as a Company Administrator (`SenderLostAuthority`).

  Logs `CompanyAdminChange Change=Revoked`.
- **200**: `{ "revoked": true, "invitationsRevoked": 1 }`.
- **Errors**: 403; 404 `NOT_FOUND` (no live grant); 422 `LAST_COMPANY_ADMIN`.

Company Administrator invitations are resent and revoked through the shared routes in [community-settings.md § Invitations](./community-settings.md#invitations).

## Setup step (command line, never HTTP; FR-038)

```text
dotnet HOAManagementCompany.dll --grant-company-admin <email> [--company-name "<name>"]
dotnet HOAManagementCompany.dll --grant-company-admin <email> --recover
```

1. Apply pending migrations.
2. Resolve the company, and set its name if `--company-name` is given.
3. Then:
   - **no live grant exists**: if the email has an account, grant the role (`Source = Setup`), log `CompanyAdminChange Actor=setup Change=Granted`, print `Granted Company Administrator to <userId>.` and exit 0. Otherwise create and send a `CompanyAdministrator` invitation (sender: the system), print `Invitation sent; it expires <time UTC>.` and exit 0;
   - **a live grant exists, without `--recover`**: print `A Company Administrator already exists; nothing changed.`, write nothing and exit 0;
   - **a live grant exists, with `--recover`**: grant or invite as above (`Source = Recovery`), log `CompanyAdminChange Change=Recovery`, enqueue `co_admin_recovery` for every current Company Administrator, and exit 0.

An invalid email argument prints the reason and exits 2. A database failure exits 1.

Runs in every environment: it is the operator path. In Cloud Run it runs as a job execution of the service image with these arguments (see quickstart.md).
