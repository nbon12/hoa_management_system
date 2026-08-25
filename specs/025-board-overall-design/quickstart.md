# Quickstart: Board Member Experience — Overall Design

**Branch**: `025-board-overall-design` · Verify each user story locally before relying on CI.

## Build & run

```bash
# Backend
dotnet build HOAManagementCompany/HOAManagementCompany.csproj
dotnet run --project HOAManagementCompany          # http://localhost:5212

# Frontend
cd neko-hoa && npm ci
npm start                                          # http://localhost:4200
```

## Seed a two-community dataset

Membership admin (User Story 5) is required to grant board access, so use it to build the fixture
rather than a raw SQL insert — this also exercises FR-042 in the same pass:

1. Seed (or reuse existing) `Property` rows in two distinct communities via the normal
   `PropertySeeder`/claim-code flow so `Community` rows exist after migration backfill.
2. Sign in as an existing user with a `CommunityManager` membership (or grant one directly via
   the migration's backfilled data if this is the very first run).
3. `POST /api/v1/communities/{communityId}/memberships` to grant a second user a `BoardMember`
   membership in community A only.

## Verify the user stories

1. **Enter and leave board mode (US1)** — sign in as the seeded Board Member. Confirm "Enter
   board mode" renders in the top bar's account-controls cluster. Click it:
   `POST /api/v1/auth/board-mode {mode:"Board"}` fires, the banner appears, and (with exactly one
   membership) you land directly on that community's home. Switch back to Resident; confirm the
   dashboard is unchanged. Sign out and back in: you return to the mode you left in (FR-022).
2. **Community scope (US2)** — as the community-A Board Member, call any board-scoped endpoint
   (e.g. `GET /api/v1/communities/{communityA}/memberships`) — succeeds. Substitute community B's
   id — `403 FORBIDDEN`, same body as a nonexistent community id (FR-016). Check `SecurityEvents`
   (or the equivalent Serilog sink) for an audit entry after viewing association-wide data
   (FR-017).
3. **Navigate the board shell (US3)** — sign in as Board Member, Community Manager, and
   Accountant against the same seeded community in turn; confirm the sidebar differs per role, and
   sections a role can't use render locked (FR-040), not absent. Navigate directly to a
   manager-only route as the Board Member: refused and redirected (FR-028), not a blank page.
4. **Metric glossary (US4)** — `GET /api/v1/board/metrics?communityId=...&surface=...` returns
   `{ items: [] }` until spec 2 registers descriptors; confirm the frontend renders the explicit
   empty state, not a headerless table.
5. **Grant and edit membership (US5)** — as the Community Manager, grant a membership per
   step 3 above; confirm the new Board Member's mode control appears on their very next request
   with no engineering/seed-script step (SC-009). Edit the membership's `endDate` to yesterday;
   confirm the member loses board access on their next request (FR-023).

## Automated checks

```bash
# Backend — resolver + membership + migration coverage (95%-critical files per constitution §9)
dotnet test HOAManagementCompany.Tests --filter "FullyQualifiedName~CommunityScope|FullyQualifiedName~CommunityMembership"

# Frontend — mode service, nav-derivation (0/1/2+ community counts), shell, metric table, glossary
cd neko-hoa && npm run test:ci

# Playwright — mode-switch journey, role-gated route refusal
npx playwright test --grep "board-mode|role-gate"

# Cypress E2E — sign-in → board mode → community home
npm run e2e:ci

# Storybook — shell, banner, metric table, hero stats, glossary panel visual regression
npm run storybook -- --ci
```

## Repowise

Update the `REPOWISE:START domain=entities` marker region in
`HOAManagementCompany/Domain/Entities/ApplicationUser.cs` to mention the new `Memberships`
collection and `LastActiveMode` field before opening the PR (constitution §2). (Already done in
this feature's implementation.)
