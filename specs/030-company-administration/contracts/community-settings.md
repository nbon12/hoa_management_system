# Contract: Community Settings (Profile, Managers and Memberships, Invitations, Residents, Architectural Review)

**Common conventions**:
- Same as [company.md](./company.md): `api/v1` prefix, `no-store`, the `{ code, message }` error shape, the non-disclosing 403 and `board-writes` on every write.
- Every route here is community-scoped and authorizes with `DecideAsync(user, communityId, capability, op)` (R2). It returns:
  - `Forbidden` → 403 `FORBIDDEN`;
  - `Archived` → 409 `COMMUNITY_ARCHIVED`;
  - `NotActive` → 409 `COMMUNITY_NOT_ACTIVE`.
- A child id (membership, invitation, property, user) that doesn't belong to the path's community gets the same 403.

---

## GET /communities/{communityId}/settings

The page shell: the header, which sections to show, and what the caller may do (US5).

- **Auth**: `ViewCommunitySettings`, read.
- **200**:

```json
{
  "id": "guid",
  "communityName": "Keystone Park",
  "legalName": "Keystone Park HOA, Inc.",
  "status": "Onboarding",
  "parent": null,
  "noManager": true,
  "missingRequiredFields": [],
  "sections": ["profile", "managers-and-memberships", "architectural-review"],
  "can": {
    "editProfile": true, "changeParent": true,
    "activate": { "allowed": false, "reasonCode": "NO_ACTIVE_MANAGER" },
    "archive": { "allowed": true, "reasonCode": null },
    "restore": { "allowed": false, "reasonCode": null },
    "manageManagers": true, "replaceLastManager": true, "manageMemberships": true,
    "manageResidents": true, "editArcSettings": true, "endMembershipsWhileArchived": false
  }
}
```

  - `sections` never contains `notifications` (FR-028, FR-029).
  - `can.*` drives the UI only: the disabled Mark Active button and its reason (FR-010), and hidden controls. The server re-checks every action.
  - `noManager` is shown only to Company Administrators; it is `null` for others.

## Profile (FR-017, saved per field)

### GET /communities/{communityId}/profile

- **Auth**: `ViewCommunitySettings`, read.
- **200**: `{ "legalName", "communityName", "county", "formationDate", "managementStartDate", "description", "missingRequiredFields": ["county"] }`.

### PATCH /communities/{communityId}/profile

- **Auth**: `EditCommunityProfile`, write.
- **Body**: any subset of the six fields. The UI sends one field per request when the person leaves the field, for example `{ "county": "Wake County, NC" }`.
- **Rules**:
  - FR-006 lengths and dates;
  - a field that has a value can't be set empty (`VALIDATION_ERROR` naming the field);
  - a field still empty from the 025 backfill may stay empty;
  - `communityName` uniqueness (R15).
- **Effects**: one `CommunityProfileChanged Field= Old= New=` event per changed field.
- **200**: the full profile.
- **Errors**:
  - 403;
  - 409 `COMMUNITY_ARCHIVED`;
  - 409 `COMMUNITY_NAME_TAKEN` (names the other community only for Company Administrators);
  - 422 `VALIDATION_ERROR` (`errors[].field` is the field name).

## Managers and memberships

### GET /communities/{communityId}/memberships *(025, extended)*

- **Auth**: `ManageMemberships`, read. Allowed while Archived for Company Administrators.
- **Change**: each item gains `openEnded: bool`, `effective: bool` and `canEnd`, `canDowngrade` and `canEditEndDate`, with a `reasonCode` when false (for example `LAST_MANAGER`). Ended memberships keep their dates.
- **Logging**: still `BoardAssociationDataAccess`.

### GET /communities/{communityId}/people

The people list behind direct appointment (FR-018).

- **Auth**: `ManageMemberships`, read.
- **Query**: `search` (name, ≤ 100), `limit`/`offset`.
- **Contents**: users with an effective membership or a resident link in this community, without the caller (brief conventions §6.1 design choice).
- **200**: `{ items: [{ userId, name, rolesHere: ["BoardMember"], residentOf: ["711 Keystone Park Dr #29"] }], total, limit, offset }`.

### POST /communities/{communityId}/memberships *(025, extended)*

Appoint a Community Manager, Board Member or Accountant.

- **Auth**: `ManageCommunityManagers` for `role = CommunityManager`; `ManageMemberships` otherwise. Write.
- **Body**, exactly one of `userId` or `email`:

```json
{ "role": "BoardMember", "userId": "…", "startDate": "2026-10-12", "endDate": "2027-12-31" }
{ "role": "BoardMember", "email": "new@example.com", "endDate": "2027-12-31" }
```

- **With `userId`** (direct):
  - the user must be in the community's people list, otherwise 422 `VALIDATION_ERROR` "Choose someone from this community, or invite them by email." (the message doesn't reveal whether the id exists);
  - it reactivates a previous row for the same role (R7);
  - it applies `ManagerRule` (FR-020);
  - **201** returns `MembershipDto`.
- **With `email`**: always creates and sends a `Membership` invitation (FR-018).
  - **201** is `{ "invitationId": "guid", "state": "Pending", "expiresAt": "…" }`, the same whether or not the email has an account.
  - An end date on a `CommunityManager` invitation is accepted only while another open-ended manager exists, and is re-checked at acceptance.
- **Errors**:
  - 403;
  - 409 `COMMUNITY_ARCHIVED`;
  - 409 `ALREADY_MEMBER`;
  - 409 `INVITATION_PENDING` (body includes `invitationId`, so the UI can offer Resend);
  - 422 `LAST_MANAGER`;
  - 422 `SELF_GRANT_NOT_ALLOWED`;
  - 422 `VALIDATION_ERROR`;
  - 429 `RATE_LIMITED`.

### PATCH /communities/{communityId}/memberships/{membershipId} *(025, extended)*

- **Auth**: `ManageCommunityManagers` when the row or the requested role is `CommunityManager`; `ManageMemberships` otherwise. Write. While Archived, only `{ "end": true }` by a Company Administrator (`ManageCommunityLifecycle`) is allowed.
- **Body**, one of:
  - `{ "end": true }`: EndDate becomes today and Status Inactive;
  - `{ "downgradeTo": "BoardMember" }`: managers only;
  - `{ "endDate": "2026-12-31" | null }`: set, move or clear the end date.
- **Effects**:
  - under the community lock, `ManagerRule` decides `LAST_MANAGER` (skipped while Archived);
  - a downgrade ends the manager row and reactivates or creates the Board Member row;
  - ending or downgrading a manager runs `SenderAuthority.RevokeOrphanedAsync` in the same transaction;
  - logs `BoardMembershipChange Kind=end|downgrade|edit`, plus one `InvitationChange Reason=SenderLostAuthority` per revoked invitation.
- **200**: `MembershipDto`.
- **Errors**:
  - 403;
  - 404 `NOT_FOUND` (membership not in this community; kept from 025);
  - 409 `COMMUNITY_ARCHIVED`;
  - 422 `LAST_MANAGER`;
  - 422 `VALIDATION_ERROR` (end date before start date; Resident role).

### POST /communities/{communityId}/managers/replace

The one-action replace (FR-020, US2 #11).

- **Auth**: `ReplaceLastManager` (Company Administrator), write.
- **Body**: `{ "currentManagerUserId": "…", "replacementUserId": "…" }`. The replacement must be in the people list.
- **Effects**: in one transaction under the community lock, grant (or reactivate) an open-ended manager row for the replacement, then end the current manager's row. Logs `BoardMembershipChange Kind=replace-grant` and `Kind=replace-end` with the same `Cause`.
- **200**: `{ "ended": MembershipDto, "appointed": MembershipDto }`.
- **Errors**:
  - 403;
  - 409 `COMMUNITY_ARCHIVED`;
  - 422 `VALIDATION_ERROR` (the replacement is not in the community, or is the same person);
  - 422 `SELF_GRANT_NOT_ALLOWED`.

## Invitations

### GET /communities/{communityId}/invitations

- **Auth**: read, allowed while Archived for Company Administrators. `ManageMemberships` for `kind=membership`; `ManageResidents` for `kind=residency`.
- **Query**:
  - `kind`: `membership` (the default) or `residency`. The Residents pages also show residency invitations per property.
  - `state`: `scheduled`, `pending`, `expired` (pending and past its expiry) or `revoked`. With no `state`, every invitation except Accepted ones is listed; an accepted person is listed as a member or resident instead.
  - `limit`/`offset`.

  Newest first.
- **Errors**: 403; 422 `VALIDATION_ERROR` on `kind` ("kind must be membership or residency.") or `state` ("state must be scheduled, pending, expired or revoked.").
- **200**: `{ items: [{ id, kind, email, role, termEndDate, state, effectiveState, revokeReason, sentBy, sentAt, expiresAt, lastSendError, canResend, canRevoke, resendBlockedReason }], total, limit, offset }`.
  - `effectiveState` adds `Expired`.
  - `resendBlockedReason` is `COMMUNITY_NOT_ACTIVE` for scheduled invitations.
  - Times are UTC; the UI shows them in the community's time zone, `timeZoneId` in the response.

### POST /communities/{communityId}/invitations/{invitationId}/resend

### POST /communities/{communityId}/invitations/{invitationId}/revoke

Company Administrator invitations use the same pair under `/company/invitations/{invitationId}/…`.

- **Auth**: the capability that could send it: `ManageCommunityManagers`, `ManageMemberships`, `ManageResidents` or `ManageCompanyAdministrators`. Write.
- **Resend**:
  - pending or expired → new link, new 72 hours, `SenderUserId` becomes the caller;
  - otherwise refused per FR-022.
  - Enforces the resend cooldown and the per-recipient cap (R12).
- **Revoke**: scheduled, pending or expired → `Revoked (Manual)`.
- **200**: the invitation item.
- **Errors**:
  - 403;
  - 409 `COMMUNITY_ARCHIVED`;
  - 409 `COMMUNITY_NOT_ACTIVE` (resend of a scheduled invitation);
  - 410 `INVITATION_INVALID` (accepted or revoked);
  - 429 `RATE_LIMITED`.

## Residents (US8, FR-024–FR-024g)

### GET /communities/{communityId}/residents

- **Auth**: `ManageResidents`, read (allowed while Archived for Company Administrators).
- **Query**:

  | Param | Values | Default |
  |---|---|---|
  | `search` | address, resident name or resident email; partial, case-insensitive; ≤ 100 | none |
  | `filter` | `noResident`, `invitationPending` (includes Scheduled), `invitationExpired` | none (All) |
  | `limit` / `offset` | 25 / 0, clamped at 100 | |

- **200**, list rows sized for the drawer design (FR-024d, Clarifications 2026-10-14):

```json
{
  "items": [{
    "propertyId": "guid",
    "address": "711 Keystone Park Dr #29",
    "residentNames": ["Jane Alvarez", "Sam Ortiz"],
    "matchedEmail": null,
    "invitationSummary": { "scheduled": 0, "pending": 1, "expired": 0 }
  }],
  "total": 120, "limit": 25, "offset": 0,
  "counts": { "all": 120, "noResident": 7, "invitationPending": 2, "invitationExpired": 1 },
  "propertiesLoaded": true
}
```

  - `matchedEmail` is set only when the search matched a resident's email.
  - Rows are ordered by address with `natural_sort` (R16).
  - `propertiesLoaded: false` means the community has no properties; the UI then shows its empty state and no invite action (US8 #14).
- **Logging**: `BoardAssociationDataAccess Resource=communities/residents` on each load and search.

### GET /communities/{communityId}/residents/{propertyId}

The property drawer.

- **Auth**: `ManageResidents`, read.
- **200**:

```json
{
  "propertyId": "guid", "address": "711 Keystone Park Dr #29",
  "residents": [{ "userId": "…", "name": "Jane Alvarez", "email": "jane@example.com", "residentSince": "2024-03-02" }],
  "invitations": [{ "id": "guid", "email": "kim@example.com", "state": "Pending", "effectiveState": "Pending", "sentBy": "Jordan Blake", "sentAt": "…", "expiresAt": "…", "revokeReason": null, "canResend": true, "canRevoke": true, "resendBlockedReason": null }],
  "timeZoneId": "America/New_York",
  "canInvite": true
}
```

- **Logging**: `BoardAssociationDataAccess Resource=communities/residents/property`.

### POST /communities/{communityId}/residents/invitations

- **Auth**: `ManageResidents`, write.
- **Body**: `{ "propertyId": "guid", "email": "kim@example.com" }`.
- **Effects**:
  - Onboarding community: a `Scheduled` invitation, with no token and no email (FR-024b);
  - otherwise: `Pending`, sent at once (R6).

  The response is identical whether or not the email has an account (FR-024a).
- **201**: `{ "invitationId": "guid", "state": "Scheduled" | "Pending", "expiresAt": "…" | null }`.
- **Errors**:
  - 403 (property not in this community, or no access);
  - 409 `COMMUNITY_ARCHIVED`;
  - 409 `ALREADY_RESIDENT`;
  - 409 `INVITATION_PENDING` (with `invitationId`);
  - 422 `SELF_GRANT_NOT_ALLOWED`;
  - 422 `VALIDATION_ERROR`;
  - 429 `RATE_LIMITED`.

### GET /communities/{communityId}/residents/{propertyId}/removal-preview?userId=…

- **Auth**: `ManageResidents`, read.
- **200**: `{ "autopay": "WillCancel" | "Unaffected" | "None", "autopayReason": "EnrolledByThisResident" | "NoEnrollerRecorded" | "EnrolledByAnotherResident" | null, "otherResidentsEmailed": 1 }`. These are the lines the remove dialog shows.

### DELETE /communities/{communityId}/residents/{propertyId}/{userId}

- **Auth**: `ManageResidents`, write.
- **Effects**, in one transaction (FR-024c):
  - delete the `UserProperty` link;
  - cancel the property's autopay if this user enrolled it or no enroller is recorded (`resident_removed`; authorization terminated);
  - enqueue `co_resident_removed` for the removed user, and `co_autopay_ended` for each other resident when autopay was cancelled;
  - log `ResidentLinkChange Change=Removed` and `AutopayCancelled`.

  The owner of record and the balance are untouched. The user's other homes and memberships are untouched.
- **200**: `{ "removed": true, "autopayCancelled": true }`.
- **Errors**: 403; 404 `NOT_FOUND` (that user isn't linked to that property); 409 `COMMUNITY_ARCHIVED`.

## Architectural review section (FR-030, FR-031)

### GET /communities/{communityId}/architectural-settings *(027, auth changed)*

- **Auth**: `ViewArcSettings`, read (Board Members, Community Managers, Company Administrators).
- **Change**: the response adds `companyDefaults` (the six fields), `differsFromDefaults: ["reviewPeriodDays", "reminderDays"]` and `canEdit`.

### PUT /communities/{communityId}/architectural-settings *(027, auth changed)*

- **Auth**: `ManageArcSettings`, write.
- The request and response shapes are otherwise unchanged. One Save for the section (Clarifications 2026-10-14).

### POST /communities/{communityId}/architectural-settings/reset

- **Auth**: `ManageArcSettings`, write.
- **Effects**: copies the six rule fields from the current company defaults. `NextApplicationNumber` and existing applications are untouched. Logs `ArcSettingsChanged Old= New= Cause=reset`.
- **200**: the GET body.
- **Errors**: 403; 409 `COMMUNITY_ARCHIVED`.

The old frontend route `/app/board/arc-settings` redirects to `/app/board/settings/architectural-review`.
