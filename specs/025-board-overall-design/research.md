# Phase 0 Research: Board Member Experience — Overall Design

All items below were `NEEDS CLARIFICATION` in the initial Technical Context pass. Each is
resolved against the actual repo state (verified by reading the referenced files), not assumed.

## R1 — Is `Community` scoped per management company, or globally unique?

**Decision**: `Community.CommunityName` is unique globally. No `ManagementCompany` entity is
introduced by this spec.

**Rationale**: Verified — no `ManagementCompany`, `Tenant`, or `Company` entity exists anywhere
under `HOAManagementCompany/Domain/Entities/`. This repository (`HOAManagementCompany`) *is* one
management company's own platform, not a multi-management-company SaaS. FR-002's "unique per
management company" language in the spec describes the real-world business rule, but since there
is exactly one management company in this deployment, it collapses to a plain global uniqueness
constraint on `Community.CommunityName`. No schema change beyond a unique index is needed.

**Alternatives considered**: Introducing a `ManagementCompany` entity now to make the constraint
literal. Rejected — nothing in the spec's scope (User Stories 1-5) requires multi-company
tenancy, it would be speculative scope the constitution's "MUST NOT attempt to deliver unrelated
product surface area all at once" (§13) explicitly discourages, and the unique index is trivially
widened later if a `ManagementCompanyId` column is ever added.

## R2 — What does the `communityId` JWT claim carry after this migration?

**Decision**: The claim continues to be named `communityId`, but its value changes from the
denormalized `Property.CommunityId` string to the `Community.Id` GUID.

**Rationale**: `AuthService.cs:188` currently mints `new Claim("communityId", property.CommunityId)`
— a string. Once `Property.CommunityId` becomes a foreign key (FR-004), the natural source value
is `property.Community.Id` (GUID). Every existing consumer of the claim
(`DocumentsEndpoint.cs:16`, `ViolationsEndpoint.cs`, etc.) reads it as an opaque string for
filtering, so switching its contents from a business string to a GUID is a same-shape, safe
change — no consumer parses its format. Refresh tokens are rotating and single-use (016-A), so
there is no stale-claim window to bridge: the next refresh after deploy mints a GUID-valued claim.

**Alternatives considered**: Adding a second claim (`communityGuid`) alongside the existing string
claim to avoid touching existing consumers. Rejected — it doubles the claim surface permanently
for a transition that resolves itself within one refresh-token rotation (minutes), and FR-012
requires exactly one shared resolver to be the source of truth, which is cleaner with one claim,
not two.

## R3 — Does FR-039's shared pre-signed URL primitive need to be built?

**Decision**: No. `HOAManagementCompany/Infrastructure/Storage/IDocumentStorage.cs` already
exposes `Task<string> GetPreSignedUrlAsync(string storageKey, CancellationToken ct)`, backed by
`S3DocumentStorage` (Cloudflare R2 in hosted environments, MinIO locally, per constitution §2/§8).
FR-039 is satisfied by reusing this existing service — sibling specs (Architectural Applications)
inject `IDocumentStorage` directly. No new interface or endpoint is introduced.

**Alternatives considered**: A new board-specific storage abstraction. Rejected — no capability
gap exists; the existing interface already returns short-lived pre-signed URLs, which is the
entire requirement.

## R4 — How is the community-scope resolver (FR-012) implemented?

**Decision**: A plain injected service, `ICommunityScopeResolver`, with a single method
`Task<bool> CanAccessAsync(ClaimsPrincipal user, Guid communityId, CommunityCapability capability, CancellationToken ct)`, called explicitly at the top of each board-scoped endpoint's
`HandleAsync`, mirroring how `DocumentsEndpoint.cs` and `ViolationsEndpoint.cs` already read
claims and call a service method inline — no ASP.NET Core `IAuthorizationHandler` /
`AuthorizationPolicy` framework is introduced.

**Rationale**: The existing codebase has no policy-based authorization anywhere — every endpoint
authorizes by reading a claim and calling a service. Introducing a full policy/handler framework
for this one feature would be a new pattern the rest of the codebase doesn't share, raising
review and maintenance cost for no behavioral benefit over a directly injected, directly called
service. `CanAccessAsync` still centralizes the actual scope-check logic (FR-012's real
requirement) — "one shared resolver" does not require "one framework mechanism."

**Alternatives considered**: A custom `[RequireCommunityScope]` endpoint filter/attribute.
Considered viable, deferred rather than rejected — it can be layered on top of
`ICommunityScopeResolver` later without changing the resolver's contract or requiring endpoints
to be rewritten; starting with the plain service keeps Phase 1 scope minimal while leaving that
door open.

## R5 — How is `Community` backfilled from existing `Property` rows (FR-005)?

**Decision**: A single EF Core migration with a data-seeding step: group existing
`(Property.CommunityId, Property.CommunityName)` string pairs, insert one `Community` row per
distinct pair (new GUID `Id`, `CommunityName` = the string value, other new fields — `LegalName`,
`County`, `FormationDate`, `ManagementStartDate`, `Description` — left null/empty pending manual
data entry by a community manager), then repoint every `Property.CommunityId` /
`Violation.CommunityId` to the new `Community.Id`, then drop the old string columns.

**Rationale**: Matches this repo's existing migration/backfill pattern seen in
`AddEmailVerificationAndPropertyClaimCode` and the constitution's requirement (§3) that "Cloud Run
startup MUST apply migrations idempotently and safely." The insert step is idempotent by checking
for an existing `Community` row with the same `CommunityName` before creating one, so a retried
migration run does not duplicate rows.

**Alternatives considered**: A manual, out-of-band data migration script run once by an operator.
Rejected — the constitution requires migrations to run safely at Cloud Run startup; a manual
step would violate that and risks environments (Dev/Staging/Prod, plus ephemeral PR Neon
branches) drifting out of sync.

## R6 — How does the shell render navigation "as data" (FR-024) without a new endpoint?

**Decision**: No new backend endpoint for navigation. `ShellComponent`'s hardcoded `navGroups`
array (`shell.component.ts:136-156`) is replaced with a computed Angular signal assembled by a
new `BoardNavigationService`, driven entirely by claims the frontend already decodes from the JWT
(role, membership count) via the existing `AuthService` — the same source `shell.component.ts`
already reads via `this.auth.user`.

**Rationale**: The shell already derives per-user state (`user()`, `property()`) client-side from
data the backend already returns at login/refresh; nav visibility is the same shape of problem
(client-side derivation from already-available claims), not a new server round-trip. This keeps
FR-024's shell contract ("accepts navigation set as data") satisfiable by sibling specs
registering their own nav items with `BoardNavigationService` without needing shell code changes
— an in-memory registration API, not a network contract.

**Alternatives considered**: A `GET /api/v1/board/nav` endpoint returning a pre-computed nav tree.
Rejected — it duplicates logic the frontend must already have (role-gating individual routes per
FR-028) and adds a network round-trip and cache-invalidation surface for data that is fully
derivable from claims already in hand.

## R7 — Where does "last-used mode persists across sessions" (FR-022) live?

**Decision**: A new `ApplicationUser.LastActiveMode` column (`Resident` | `Board`, default
`Resident`), set server-side whenever the mode-switch endpoint is called, read at login/refresh
to decide which mode the client boots into.

**Rationale**: `localStorage` alone would not satisfy "persists across sessions" the way a board
member would expect — signing in on a different device should not silently reset them to resident
mode. A server-side column, cheap to add to the existing `ApplicationUser` table for the user
column, is the correct source of truth given `AuthService.cs` already returns hydrated user state
at login.

**Alternatives considered**: Encoding the mode in the JWT itself. Rejected — FR-014 explicitly
forbids the client's mode from being an authorization input, and baking it into the same token
used for authorization blurs that boundary even if unread by the resolver; a separate persisted
column keeps "last UI mode" and "authorization" fully decoupled.

## Summary of dependencies confirmed already present (no new packages)

- Backend: FastEndpoints, EF Core 9 (Npgsql), ASP.NET Core Identity, JWT bearer issuance,
  `IDocumentStorage` — all already referenced and used by `HOAManagementCompany`.
- Frontend: Angular 17.3 signals/standalone components, existing `AuthService` claim decoding —
  no new npm packages required.
- Testing: xUnit + Testcontainers.PostgreSQL (backend), Jasmine/Karma + Angular Testing Library
  (frontend unit/component), Playwright, Cypress, Storybook — all already the constitution-mandated
  toolchain (§9), no new testing dependency introduced.
