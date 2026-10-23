# Quickstart: Company Administration

**Branch**: `030-company-administration`. Check each user story locally before relying on CI.

## Build and run

```bash
# Backend: migrations and seed apply automatically in Development/Dev
dotnet build
dotnet run --project HOAManagementCompany

# Frontend
cd neko-hoa && npm ci && npm start          # http://localhost:4200
```

The dev seed (dev-like environments only; research R18) adds:
- **A Company Administrator**: `company@nekohoa.dev` / `Password1!`, created through the same `SetupAsync` as the command-line step, with no home and no membership.
- **An Onboarding community**, "Keystone Ridge", with `manager@nekohoa.dev` as its open-ended Community Manager and properties loaded by the seed.
- **The existing seeded users and community**, unchanged, except that board users no longer need the co-residence workaround (they can sign in without a home).

To capture invitation links locally, set `DevTools:E2ECleanupEnabled=true` (Development and Dev already do). Invitation links are then kept in the dev vault and readable at `GET /api/v1/e2e/auth-codes`, the same seam as verification codes, instead of needing SES.

## The setup step (first Company Administrator, FR-038)

```bash
# Local
dotnet run --project HOAManagementCompany -- --grant-company-admin owner@example.com --company-name "Juniper Row Community Management"

# Second run: nothing changes
dotnet run --project HOAManagementCompany -- --grant-company-admin owner@example.com
# → "A Company Administrator already exists; nothing changed."

# Deployed (Cloud Run job execution of the service image)
gcloud run jobs execute <service>-admin-setup --region <region> \
  --args="--grant-company-admin,owner@example.com"
```

`--recover` grants or invites even when administrators exist, and emails every current administrator (US7 #8). There is no HTTP equivalent.

## Check the user stories

1. **Sign in without a home (US9)**: sign in as `company@nekohoa.dev`.
   - You land on **Portfolio**. There is no board-mode control and no violet banner.
   - Then sign out and open `/company/sign-in` (bookmarkable). It lands on the portfolio too.
   - Sign in there as `resident@nekohoa.dev`. You get the usual dashboard with the "isn't a Company Administrator" notice.
2. **Add a community (US1)**: **Add community** → "Keystone Park HOA, Inc." / "Keystone Park".
   - It appears as Onboarding with "No manager".
   - Add "keystone park " again: refused with the other community named.
   - As `manager@nekohoa.dev`, add a community from board mode. You become its manager, and an `co_community_added` outbox row exists for `company@`.
3. **Managers (US2)**: Keystone Park → **Managers and memberships** → appoint `pat@example.com`.
   - The row shows *pending*, with no name.
   - Read the link from the dev vault, open it in a private window and create an account. Pat lands in board mode for Keystone Park.
   - Try to end Pat (the only open-ended manager): refused, "LAST_MANAGER".
4. **Lifecycle (US3)**:
   - On Keystone Park's Profile, hover **Mark Active** before a manager exists. It is disabled, and the reason shows on hover and on keyboard focus.
   - After Pat accepts, mark it Active.
   - **Archive** the seeded community with open ARC applications: refused, with the applications listed.
   - Archive an empty test community. Its linked resident sees the home as "No longer managed", and the ledger is readable.
   - **Restore** it.
5. **Profile per field (Clarifications 2026-10-14)**: edit County and tab out. "Saving…" then "✓ Saved" appear. Clear it and tab out: an error, and nothing saved.
6. **Residents (US8)**: **Managers and memberships → Residents**.
   - Search "#29" and open the property drawer.
   - Invite `kim@example.com`, accept from the vault link, and Kim sees the home.
   - **Remove** a resident who enrolled autopay. The schedule becomes `inactive` with reason `resident_removed`, and their next resident request returns `403 PROPERTY_ACCESS_DENIED`.
   - In Keystone Ridge (Onboarding), invite someone. The invitation shows *Scheduled* and nothing is sent until it is marked Active.
7. **Company defaults (US4)**: change the lapse rule to *Deemed approved*, add a community, and it inherits the change. Existing communities don't change. As `manager@`, the defaults are read-only.
8. **Settings page (US5)**:
   - In the side list, search "review". **Architectural review › Review period** is listed and jumps to the field.
   - **Reset architectural review to company defaults** keeps the next `ARC-` number.
   - `/app/board/arc-settings` redirects to this section.
9. **Portfolio (US6)**: A–Z by default. Click **Status** to sort. **No manager** filters.
10. **Company Administrators (US7)**:
    - Invite `ops@example.com` and accept it.
    - Revoke yourself while another administrator exists: allowed.
    - Revoke the last one: refused, `LAST_COMPANY_ADMIN`.

## Inspect sensitive events and emails

Sensitive events go to the console sink in Development. Filter for `CommunityLifecycle`, `InvitationChange`, `ResidentLinkChange`, `AutopayCancelled` and `CompanyAdminChange`; none should contain an email or a name.

```sql
SELECT "Kind", "DedupKey", "Status" FROM "OutboxMessages"
WHERE "Kind" LIKE 'co\_%' ORDER BY "CreatedAt" DESC;

SELECT "Kind", "State", "RevokeReason", "SentAt", "ExpiresAt", "LastSendError"
FROM "Invitations" ORDER BY "CreatedAt" DESC;
```

## Tests

```bash
dotnet test --filter "FullyQualifiedName~Company|FullyQualifiedName~Invitations|FullyQualifiedName~Residents|FullyQualifiedName~ResidentAccess"
dotnet test                                     # full suite, including the static scope tests
cd neko-hoa && npm run test:ci
npx playwright test --grep @company             # from neko-hoa/
```
