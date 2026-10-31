# Contract: Board Access — Overall Design

All routes carry the global `api/v1` prefix (`c.Endpoints.RoutePrefix` in `Program.cs`). None of these responses are
cached (`Cache-Control: no-store`) — every one is authenticated and user/community-specific
(constitution §8, "Authenticated or user-specific responses MUST NOT be edge-cached").

## POST /api/v1/auth/board-mode

Switches the caller's active mode. Mirrors `POST /api/v1/auth/switch-property`
(`SwitchPropertyEndpoint.cs`) — same cookie-refresh-append pattern, same error shape.

- **Auth**: authenticated (bearer).
- **Request**: `{ "mode": "Resident" | "Board" }`
- **Behavior**: Server persists `ApplicationUser.LastActiveMode` (research.md R7) and re-mints the
  token pair. Switching to `Board` when the caller holds **more than one** active non-resident
  membership lands them on the "My Communities" list (FR-025); with **exactly one**, the client navigates directly to that community's Community Home
  (FR-026). *As built in PR #174 (2026-08-28):* the landing is decided by the client's shared landing rule (`neko-hoa/src/app/core/services/landing.ts`) from `user.memberships`, not from a `communityId` in the response. The client's requested mode is never itself an authorization input (FR-014) — the
  server independently verifies at least one active non-resident membership exists before
  allowing `Board` mode.
- **Response 200**: `AuthResponse` `{ token, expiresAt, user }` + rotated refresh cookie. `user.lastActiveMode` is the persisted mode, `user.memberships` lists the effective memberships, and `user.landing` (030) names the landing. There is no top-level `mode` or `communityId` (PR #174).
- **Errors**: `403 NO_ACTIVE_MEMBERSHIP` if `mode: "Board"` is requested and the caller holds no
  active non-resident `CommunityMembership` (FR-020).

## GET /api/v1/me/communities

Backs the "My Communities" nav item (FR-025).

- **Auth**: authenticated (bearer).
- **Request query**: `limit` (default 25, max 100), `offset` (default 0) — constitution §4 pagination standard.
- **Response 200**: `{ items: [{ id, communityName, role, status }], total, limit, offset }` (`status` is the community's status; `role` joins the caller's active roles there with "/"; 030 FR-036b lists Onboarding communities and leaves out Archived ones) — only
  communities where the caller holds an **active** membership; summary fields only, per the
  constitution's "My Communities" cross-community exception (spec.md, Constitution Requirements:
  Tenant boundary).
- **Errors**: none beyond standard 401.

## GET /api/v1/communities/{communityId}/memberships

Lists memberships for the membership-admin surface (User Story 5).

- **Auth**: authenticated; `ICommunityScopeResolver.CanAccessAsync(user, communityId, ManageMemberships)` (FR-012). Community Manager only. *Changed by 030:* a Company Administrator of the community's company also passes (also while Archived), and each item gains `openEnded`, `effective`, `canEnd`, `canDowngrade`, `canEditEndDate` and `reasonCode` (specs/030-company-administration/contracts/community-settings.md).
- **Request query**: `limit` (default 25, max 100), `offset` (default 0) — constitution §4 pagination standard.
- **Response 200**: `{ items: [{ id, userId, userDisplayName, role, status, startDate, endDate }], total, limit, offset }`
- **Errors**: `403 FORBIDDEN` (resolver denies — includes "not a manager of this community" and
  "community does not exist," per FR-016's fail-closed, non-disclosing requirement — same body
  either way).

## POST /api/v1/communities/{communityId}/memberships

Creates a `CommunityMembership` (User Story 5, Acceptance Scenario 1).

- **Auth**: authenticated; `ICommunityScopeResolver.CanAccessAsync(user, communityId, ManageMemberships)`. Community Manager only.
- *Superseded by 030 (contracts/community-settings.md):* the body has exactly one of `userId` (direct; the user must be in the community's people list) or `email` (always an invitation, 201 `{ invitationId, state, expiresAt }`). `startDate` defaults to today. `role = CommunityManager` needs `ManageCommunityManagers`. New errors: 409 `ALREADY_MEMBER` / `INVITATION_PENDING` / `COMMUNITY_ARCHIVED`, 422 `LAST_MANAGER` / `SELF_GRANT_NOT_ALLOWED`. The original 025 shape was:
- **Request**: `{ userId, role, startDate, endDate? }` — `role` excludes `Resident` (residents are
  provisioned via the existing property-claim flow, unchanged).
- **Response 201**: the created membership (same shape as the list item above).
- **Errors**: `403 FORBIDDEN` (not a manager of this community — Acceptance Scenario 2);
  `422 VALIDATION_ERROR` (unknown `userId`, invalid `role`, `endDate` before `startDate`).

## PATCH /api/v1/communities/{communityId}/memberships/{membershipId}

Edits or ends a membership (User Story 5, Acceptance Scenarios 3 & 4).

- **Auth**: same as POST above.
- **Request**: `{ role?, status?, endDate? }` — partial update. *Superseded by 030 (contracts/community-settings.md):* exactly one of `{ "end": true }` (end date today, status Inactive), `{ "downgradeTo": "BoardMember" }` (managers only) or `{ "endDate": "YYYY-MM-DD" | null }`. There is no free role or status edit. Adds 409 `COMMUNITY_ARCHIVED` and 422 `LAST_MANAGER` / `VALIDATION_ERROR`.
- **Response 200**: the updated membership.
- **Effect**: takes effect on the member's **next request** — no forced re-authentication
  (Acceptance Scenario 3); an ended/inactive membership stops conferring permissions on the
  member's next request per FR-023.
- **Errors**: `403 FORBIDDEN`; `404 NOT_FOUND` (membership does not belong to `communityId`).

## GET /api/v1/board/metrics?communityId={communityId}&surface={surface}

Serves the `MetricDescriptor` registry (data-model.md) for a given `MetricSurface`. Ships in this
spec as the generic, registry-driven endpoint; returns an empty `items` array until spec 2
(Community Overview & Metrics) registers concrete descriptors — an empty response renders the
explicit empty state required by the spec's Edge Cases, not an error.

- **Auth**: authenticated; `ICommunityScopeResolver.CanAccessAsync(user, communityId, capability)`
  evaluated **per descriptor** against `MetricDescriptor.RequiredCapability` (a `CommunityCapability`; FR-035) — the
  endpoint itself requires `ViewAssociationData`: an active Board Member, Community Manager or Accountant membership in `communityId` (a Resident-role membership is refused, US2-S4).
- **Request query**: `communityId` (required), `surface` (required — enum name), `limit` (default 25, max 100), `offset` (default 0).
- **Response 200**: `{ items: [{ id, label, definitionText, value, detail?, status, emphasis }], total, limit, offset }`
  — descriptors the caller lacks the capability for are silently omitted (FR-035), not returned
  with a locked flag.
- **Errors**: `403 FORBIDDEN` (no active non-resident membership in `communityId`).

## Shared error shape

Matches the existing `DomainException` pattern (`SwitchPropertyEndpoint.cs:33-37`):

```json
{ "code": "FORBIDDEN", "message": "..." }
```

`FR-016` requires board-scoped denials for an out-of-scope community to be indistinguishable from
denials for a community the caller has never heard of — both return the same `403 FORBIDDEN` body
above, never a `404` that would confirm the community's existence.
