# Contract: Invitation Acceptance, Sign-in Changes and Resident Access

**Common conventions**:
- Same as [company.md](./company.md): `api/v1` prefix and the `{ code, message }` error shape.
- Invitation endpoints are **not** authorized by the resolver. They are authorized by the link token (R6). They live in `Features/Invitations/` and use the per-client `auth` rate limit (FR-041b).

---

## Invitation links

**Email link**: `{App:BaseUrl}/invite#t={token}`. The token is 43 base64url characters (256 bits).
- It sits in the URL fragment, so it never reaches the server or edge logs.
- The `/invite` page reads it, removes it from the address bar with `history.replaceState`, and sends it in request bodies only.

The refusal order is the same for all three endpoints (FR-022):

1. No row matches the token hash, or the invitation is `Accepted` or `Revoked`, or (for a membership) its term end date has passed: **410 `INVITATION_INVALID`**.
2. The state is `Pending` but `now ≥ ExpiresAt`: **410 `INVITATION_EXPIRED`**.
3. *(accept and register only)* The sender no longer has authority. The system revokes the invitation (`SenderLostAuthority`) and returns **410 `INVITATION_INVALID`**.
4. *(accept only)* The signed-in account's normalized email ≠ the invited email: **403 `INVITATION_EMAIL_MISMATCH`**. The message never includes the invited email, and the invitation stays `Pending`.

Each refusal logs `InvitationAcceptFailed Reason={code}`, with the invitation id when known and never the token.

### POST /invitations/preview

- **Anonymous**; `auth` rate limit.
- **Body**: `{ "token": "…" }`.
- **200**: only for a valid, unexpired link.

```json
{
  "kind": "Residency",
  "communityName": "Sakura Heights",
  "offer": { "role": null, "termEndDate": null, "propertyAddress": "711 Keystone Park Dr #29" },
  "sentBy": "Jordan Blake",
  "expiresAt": "2026-10-13T20:40:00Z",
  "timeZoneId": "America/New_York",
  "invitedEmail": "kim@example.com",
  "hasAccount": false
}
```

  - `invitedEmail` and `hasAccount` are returned **only** when the request is anonymous, and only for a valid link. They let the page show the fixed email on "Create account" and pick "Create account" or "Sign in". When the caller is signed in, both are `null`. A different signed-in account (Bob on a shared laptop) therefore never receives the invited email, even in the network response (FR-022, `INVITATION_EMAIL_MISMATCH`). The data shown follows spec FR-022a.
  - `sentBy` is the sender's display name (the spec's Security bullet, as amended in this plan).
  - `kind = CompanyAdministrator` has `communityName` set to the company name.
- **Errors**: 410 `INVITATION_INVALID`; 410 `INVITATION_EXPIRED`; 429.

### POST /invitations/register

FR-022a: create an account from the link. No claim code.

- **Anonymous**; `auth` rate limit.
- **Body**: `{ "token": "…", "firstName": "Kim", "lastName": "Tran", "password": "…" }`. The password follows the Identity rules (8+ characters, a digit, upper and lower case, a symbol).
- **Effects**:
  - in one transaction under the community lock (or the company lock for Company Administrator invitations), create the user with `Email = invited email` and `EmailConfirmed = true`;
  - apply the offer: create or reactivate the membership (with `ManagerRule`), create the `UserProperty` link, or create the Company Administrator grant;
  - mark the invitation `Accepted`;
  - log `InvitationChange Change=Accepted` and the resulting `BoardMembershipChange`, `ResidentLinkChange` or `CompanyAdminChange`.

  Then sign the user in, as `/auth/login` does.
- **201**: `AuthResponse`, with the refresh cookie set (see below). `user.landing` says where the app goes next.
- **Errors**: the invitation codes above; 422 `VALIDATION_ERROR` (password rules); **422 `REGISTRATION_FAILED`**, generic, when an account with that email already exists or creation fails (spec 017 FR-A5). The UI then says "sign in to accept".

### POST /invitations/accept

- **Authenticated** (any signed-in user); `auth` rate limit.
- **Body**: `{ "token": "…" }`.
- **Effects**: as for register, applied to the caller's account. If the caller already holds the role or the link, the invitation is marked `Accepted` and nothing is duplicated (FR-022).
- **200**: `{ "accepted": true, "alreadyHeld": false, "landing": "Board" }`. Then the client calls `/auth/me`.
- **Errors**: the invitation codes above; 409 `COMMUNITY_ARCHIVED` (the community was archived after the link was sent — not reachable in practice, because archiving revokes; kept for races).

---

## Sign-in changes (FR-036a, FR-036b, FR-036c, US9)

| Endpoint | Change |
|---|---|
| `POST /auth/login` | No longer returns 422 `NO_PROPERTY`. A user with no usable home signs in, and the token carries no `propertyId` or `communityId`. |
| `POST /auth/refresh` | Same. Token deletion and the new mint happen in one transaction. |
| `POST /auth/board-mode` | Eligibility counts memberships in Onboarding and Active communities. Users with no home can switch. |
| `POST /auth/switch-property` | 409 `COMMUNITY_NOT_ACTIVE` for a home in an Onboarding community. Archived homes are allowed (read-only, FR-012a). |
| `GET /auth/me` | `CurrentUserDto` gains the fields below. |
| `POST /auth/register` (claim code) | Unchanged, except that a code for a property in an Onboarding community returns 409 `COMMUNITY_NOT_ACTIVE` *after* the proof and the code have been checked. The code is not used up. Once a property has a resident by any route, its outstanding code is refused with the existing generic `REGISTRATION_FAILED` (FR-024f). |

**`CurrentUserDto` (`/auth/me`, and `user` in `AuthResponse`)**:

```json
{
  "id": "…", "firstName": "Robin", "lastName": "Hale", "email": "owner@example.com", "initials": "RH",
  "properties": [{ "id": "guid", "accountNumber": "SAKURA-001", "address": "711 Keystone Park Dr #29", "communityId": "guid", "communityName": "Sakura Heights", "communityStatus": "Active" }],
  "lastActiveMode": "Resident",
  "memberships": [{ "communityId": "guid", "communityName": "Keystone Ridge", "role": "CommunityManager", "communityStatus": "Onboarding" }],
  "isCompanyAdministrator": true,
  "landing": "Company"
}
```

- `properties` includes homes in Active and Archived communities and leaves out Onboarding ones.
- `memberships` includes Onboarding and Active communities and leaves out Archived ones.
- `landing` is `Board`, `Company`, `Resident`, `NotLive` or `NoHome`, from `LandingRule` (R11). For a user with a usable home, `Resident` or `Board` follows `lastActiveMode`, as today.

**Company sign-in page** (`/company/sign-in`, frontend only; FR-036c):
- It calls the same `POST /auth/login`. After success, the client routes to `/app/company/portfolio` when `user.isCompanyAdministrator`.
- Otherwise it routes to the `landing` target with `?notice=not-company-admin`, which shows "This account isn't a Company Administrator, so you've been signed in to your usual page."
- Sign-in failures stay generic (`INVALID_CREDENTIALS`), and the lockout and `auth` rate limit are shared with the usual page.

---

## Resident access on every request (FR-024g, FR-012a)

Every resident endpoint calls `ResidentAccess.RequireAsync(User, op)` first (R8). Spec 029's endpoints do it through their single authorization point, `ResidentArcScope`:

| Operation | Endpoints |
|---|---|
| `ReadHistory` | `GET /payments/ledger`, `/payments/statements`, `/payments/statements/{id}` (if present), `/payments/unpaid-assessments`, `/payments/transactions`, `/payments/receipts/{id}`; spec 029's submitted requests: `/property/architectural-applications`, `/property/architectural-applications/{id}`, `/property/architectural-applications/{id}/attachments/{attachmentId}/url` |
| `Read` | `GET /dashboard`, `/property`, `/property/owner`, `/property/address-history`, `/property/directory-fields`, `/community/announcements`, `/community/announcements/{id}`, `/community/events`, `/community/documents`, `/community/documents/{id}/download`, `/community/poll`, `/community/directory`, `/community/violations`, `/payments/options`, `/payments/recurring`, `/payments/drafts`, `/payments/alert-preferences`; spec 029's drafts: `/property/architectural-applications/drafts/{draftId}`, `/property/architectural-applications/drafts/{draftId}/attachments/{attachmentId}/url` |
| `Write` | `PATCH /property/owner`, `PATCH /property/directory-fields/{key}`, `POST /community/events/{id}/rsvp`, `POST /community/poll/{id}/vote`, `POST /payments/intent`, `POST /payments/one-time/confirm`, `POST /payments/recurring/setup-intent`, `PUT /payments/recurring`, `DELETE /payments/recurring`, `PUT /payments/alert-preferences`; spec 029: `POST /property/architectural-applications/drafts`, `PUT` and `DELETE /property/architectural-applications/drafts/{draftId}`, `POST /property/architectural-applications/drafts/{draftId}/submit`, `POST /property/architectural-applications/drafts/{draftId}/attachments`, `DELETE /property/architectural-applications/drafts/{draftId}/attachments/{attachmentId}`, `POST /property/architectural-applications/{id}/info-requests/{infoRequestId}/attachments`, `POST /property/architectural-applications/{id}/info-requests/{infoRequestId}/reply`, `POST /property/architectural-applications/{id}/withdraw`, `POST /property/architectural-applications/{id}/revise` |

**Results**:

| Situation | Response |
|---|---|
| No `propertyId` claim, or no resident link to that property | **403 `PROPERTY_ACCESS_DENIED`** |
| The home's community is Onboarding | **409 `COMMUNITY_NOT_ACTIVE`** |
| Archived, and the operation is not `ReadHistory` | **409 `COMMUNITY_ARCHIVED`** |
| Otherwise | the endpoint runs with the **database's** property and community, not the token's |

Every row is enforced by `ResidentAccessStaticAnalysisTests` (R3) and by a `[Theory]` over status × endpoint (spec Quality gates).

`PUT /payments/recurring` now records `EnrolledByUserId = caller` (R9).

---

## Error codes

These are the codes this feature adds or reuses, with their HTTP statuses. The spec leaves statuses to this contract.

| Code | HTTP | Meaning |
|---|---|---|
| `FORBIDDEN` | 403 | Not authorized, or the community doesn't exist (025 body, unchanged) |
| `COMMUNITY_NAME_TAKEN` | 409 | FR-007 |
| `NO_ACTIVE_MANAGER` | 409 | FR-010 activation |
| `OPEN_WORK` | 409 | FR-014; the body adds `items[]` |
| `INVALID_TRANSITION` | 409 | A lifecycle action not allowed from the current status (for example activating an Active community) |
| `COMMUNITY_ARCHIVED` | 409 | FR-012, FR-012a |
| `COMMUNITY_NOT_ACTIVE` | 409 | FR-011, FR-012a, resend of a scheduled invitation |
| `PARENT_ARCHIVED` | 409 | FR-010, FR-016 |
| `PARENT_CYCLE` | 422 | FR-016 |
| `INVITATION_INVALID` | 410 | FR-022 |
| `INVITATION_EXPIRED` | 410 | FR-022 |
| `INVITATION_EMAIL_MISMATCH` | 403 | FR-022 |
| `INVITATION_PENDING` | 409 | FR-022 duplicates; the body adds `invitationId` |
| `ALREADY_MEMBER` | 409 | FR-022 |
| `ALREADY_RESIDENT` | 409 | FR-024 |
| `SELF_GRANT_NOT_ALLOWED` | 422 | FR-003a |
| `LAST_MANAGER` | 422 | FR-020 (025 code and status, unchanged) |
| `LAST_COMPANY_ADMIN` | 422 | FR-037 |
| `PROPERTY_ACCESS_DENIED` | 403 | FR-024g (existing code) |
| `REGISTRATION_FAILED` | 422 | Generic account-creation failure (spec 017, unchanged) |
| `RATE_LIMITED` | 429 | FR-041c caps. The existing middleware 429 is unchanged; the invitation caps add this body code. |
| `VALIDATION_ERROR` | 422 | unchanged |
