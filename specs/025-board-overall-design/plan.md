# Implementation Plan: Board Member Experience — Overall Design

**Branch**: `025-board-overall-design` | **Date**: 2026-08-23 | **Spec**: `specs/025-board-overall-design/spec.md`
**Input**: Feature specification from `specs/025-board-overall-design/spec.md`

## Summary

Introduce the board member persona as an additive, role-gated mode inside the existing single
sign-in application — no second portal. This requires making community a real, first-class
tenant boundary (`Community` entity + `CommunityMembership`) in place of today's denormalized
`Property.CommunityId` string, a single server-side community-scope resolver every board-side
endpoint must use, a data-driven navigation shell, an in-product membership-admin surface, and the
`MetricDescriptor` registry contract that specs 2-6 build their own board features on top of. This
is spec 1 of 6 in the board member effort and is a deliberate, documented exception to full spec
independence (see Constitution Check below and spec.md's own "Spec independence & parallelism"
section) — specs 2-6 cannot start their board-scoped work until this one's entity, resolver, and
shell contract exist.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend, `HOAManagementCompany`); TypeScript / Angular 17.3 (frontend, `neko-hoa`)
**Primary Dependencies**: FastEndpoints, EF Core 9 (Npgsql), ASP.NET Core Identity, JWT bearer issuance, `IDocumentStorage` (all existing, reused — see research.md R3); Angular standalone components/signals, existing `AuthService` claim decoding. No new package for either project.
**Storage**: PostgreSQL (Neon in production, Testcontainers in CI/local). New tables: `Communities`, `CommunityMemberships`. Modified: `Properties` (`CommunityId`/`CommunityName` strings → `CommunityId` GUID FK), `Violations` (`CommunityId` string → GUID FK), `AspNetUsers` (+ `LastActiveMode`). **No audit table** — the FR-017 trail is Serilog-only (Clarifications 2026-08-23).
**Testing**: xUnit + Testcontainers.PostgreSQL (backend, per constitution §9 and the Spec Kit Testing Constitution); Jasmine/Karma + Angular Testing Library (frontend unit/component); Playwright (mode-switch journey, role-gated route refusal); Cypress (E2E sign-in → board mode → community home); Storybook visual regression (shell, banner, metric table, hero stats, glossary panel).
**Target Platform**: Google Cloud Run (backend, scale-to-zero), Cloudflare Pages (frontend) — existing infrastructure, unchanged by this feature.
**Project Type**: Web application (Angular frontend + .NET backend) — Option 2 structure below.
**Performance Goals**: Not specified numerically by the spec (Success Criteria are functional/security, not latency-based — consistent with this repo's other specs). No new performance target is introduced; this feature inherits existing endpoint latency norms.
**Constraints**: All authorization is server-side through one shared resolver (FR-012, FR-014) — the client's mode is never an authorization input. The resolver decides scope **strictly per `Community` row** and does not traverse the parent/child relation — master membership grants no access to sub-associations, and vice versa (Clarifications 2026-08-23). It grants a capability when **any** of the caller's active membership rows in the target community confers it (**union** across role rows), and the frontend nav derives from that same union of roles — generalizing the wireframe's single-`role` `boardNav()` signature to a role set (Clarifications 2026-08-23). The `Community` backfill migration MUST be idempotent and safe to run at Cloud Run startup (FR-005, constitution §3). Existing Neon connection-pooling and short-lived-`DbContext` expectations are unchanged (constitution §8). All new interactive controls meet WCAG 2.1 AA (FR-038).
**Scale/Scope**: Single logical PostgreSQL database; one management company, so `Community.CommunityName` uniqueness is global, not company-scoped (research.md R1). 2 new entities, 3 new enums, 3 modified entities, 6 new endpoints (`contracts/board-access.md`), 1 new frontend feature area (`features/board/`) plus a shell/routing/auth-service refactor.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design below.*

- **Technology fit**: ✅ Angular, .NET FastEndpoints, PostgreSQL/Neon, in-application ASP.NET Core Identity + JWT, Cloudflare, Cloud Run, Docker, Sentry, Swashbuckle (dev-only), GitHub Actions — all reused, nothing new introduced.
- **HOA tenancy**: ✅ This feature *is* the fix for the gap the constitution assumes is already closed — today's `hoa_id`-equivalent is a denormalized string with no real tenant boundary. `Community` (GUID PK) becomes that boundary; cross-community access is denied by default (FR-013, FR-016); the sole intentional cross-community read is the "My Communities" summary list (documented in spec.md's Constitution Requirements section). Scope is decided strictly per `Community` row — the resolver never widens across the master/sub-association hierarchy in either direction (Clarifications 2026-08-23), so a portfolio holder needs a distinct membership per community.
- **API contracts**: ✅ `contracts/board-access.md` documents auth, authorization, pagination (`limit`/`offset`, default 25 / max 100 on the memberships list), error shape, and cacheability (`no-store`) for every endpoint.
- **Security and operations**: ✅ No new secrets. Authorization enforced server-side only (FR-014). Security-sensitive events (membership grants/edits, association-wide data access) are recorded (FR-017, constitution §7) as **structured Serilog sensitive-events carrying actor, community, resource, and UTC timestamp — this spec introduces no queryable audit table** (Clarifications 2026-08-23); a durable audit store is deferred to a later spec if compliance requires it. Production error shape is unchanged (existing global exception handler, `DomainException` pattern reused from `SwitchPropertyEndpoint.cs`).
- **File storage**: ✅ FR-039 reuses the existing `IDocumentStorage` (R2 hosted / MinIO local) — no new storage mechanism (research.md R3).
- **Caching/edge**: ✅ Every new endpoint is explicitly `no-store`; nothing here is edge-cached.
- **Testing discipline**: ✅ Backend: xUnit + Testcontainers.PostgreSQL, transaction-isolated, Theories for role × membership-status × community-scope variation — including the no-cascade hierarchy boundary and the multi-role union case (Clarifications 2026-08-23) — as spec.md's own Constitution Requirements section commits. Frontend: Jasmine/Karma, Angular Testing Library, Playwright, Cypress, Storybook — all constitution-mandated tools, nothing new.
- **CI/CD and documentation**: ✅ Sonar, Codecov, coverage, and Repowise accounted for — see Repowise Documentation below.
- **Executable & living specs**: ✅ `spec.md` was brought current through the `/speckit.clarify` pass of 2026-08-23, which resolved three ambiguities (master/sub scope, audit-trail storage, multi-role capability) now reflected across this plan; every acceptance scenario in User Stories 1-5 maps to a planned automated test above.
- **Spec independence & parallelism**: ⚠️ **Documented exception, not a violation.** This spec is individually completable and delivers a demonstrable slice on its own (sign in → enter board mode → scoped community home), but specs 2-6 (Community Overview & Metrics, Architectural Applications, Board Approvals, Accounting, Reports) each hold a **hard dependency** on the `Community` entity, `CommunityMembership`/`CommunityRole`, `ICommunityScopeResolver`, and the `MetricDescriptor` registry this spec introduces. Per constitution §12, this dependency is explicitly documented here; each of specs 2-6's own `plan.md` MUST restate it when that spec is planned. The split was already designed (spec.md's dependency table) so specs 2-6 can proceed **in parallel with each other** once this one merges — the exception is scoped to "before specs 2-6," not "between specs 2-6."

No unresolved gate failures. No entries needed in Complexity Tracking — the one constitutional
exception (spec independence) is explicitly permitted by §12 when documented, which this section
does.

## Project Structure

### Documentation (this feature)

```text
specs/025-board-overall-design/
├── plan.md              # This file
├── research.md           # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── board-access.md   # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit.tasks — not created by this command)
```

### Source Code (repository root)

```text
# Option 2: Web application (Angular frontend + .NET backend — matches existing repo shape)

HOAManagementCompany/                          # backend
├── Domain/
│   ├── Entities/
│   │   ├── Community.cs                       # new
│   │   ├── CommunityMembership.cs              # new
│   │   ├── Property.cs                         # modified — CommunityId string→GUID FK
│   │   ├── Violation.cs                        # modified — CommunityId string→GUID FK
│   │   └── ApplicationUser.cs                  # modified — +Memberships, +LastActiveMode
│   └── Enums/
│       ├── CommunityRole.cs                    # new
│       ├── MembershipStatus.cs                 # new
│       ├── CommunityStatus.cs                  # new
│       └── UserMode.cs                         # new
├── Features/
│   ├── Auth/
│   │   └── AuthService.cs                      # modified — communityId claim now GUID (R2), mode switch
│   └── Board/                                  # new feature folder
│       ├── ICommunityScopeResolver.cs
│       ├── CommunityScopeResolver.cs           # per-row scope, union across role rows (Clarifications 2026-08-23)
│       ├── MetricDescriptor.cs                 # + registry DI registration
│       ├── BoardModeEndpoint.cs
│       ├── MyCommunitiesEndpoint.cs
│       ├── MembershipsListEndpoint.cs
│       ├── MembershipCreateEndpoint.cs
│       ├── MembershipUpdateEndpoint.cs
│       └── BoardMetricsEndpoint.cs
└── Infrastructure/Persistence/Migrations/
    └── <timestamp>_AddCommunityAndMembership.cs # new — see data-model.md migration sequencing

HOAManagementCompany.Tests/
└── Integration/Board/                          # new — resolver, membership, migration tests

neko-hoa/                                       # frontend
└── src/app/
    ├── app.routes.ts                            # modified — board/* routes, board guard
    ├── shell/shell.component.ts                 # modified — navGroups → BoardNavigationService signal
    ├── core/
    │   ├── guards/board.guard.ts                 # new
    │   └── services/
    │       ├── auth.service.ts                   # modified — board-mode switch call
    │       └── board-navigation.service.ts        # new — derives nav from the union of the user's active roles
    └── features/board/                           # new feature folder
        ├── mode-toggle/
        ├── community-home/                        # placeholder landing; real content ships in spec 2
        ├── membership-admin/
        └── metrics/                                # generic table + glossary panel components
```

**Structure Decision**: Follows the existing repo's Option-2 layout exactly — a new `Features/Board/` folder on the backend (matching the existing `Features/Auth/`, `Features/Community/`, `Features/Payments/` convention) and a new `features/board/` folder on the frontend (matching `features/auth/`, `features/community/`, `features/payments/`). No new top-level project or directory is introduced.

## Repowise Documentation

**Status**: Not started (Phase 2/implementation not yet begun).

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `HOAManagementCompany/Domain/Entities/ApplicationUser.cs` | `domain=entities` | Existing marker (lines 5-7) — update to mention the new `Memberships` collection and `LastActiveMode` field once implemented. |

New files this feature introduces (`Community.cs`, `CommunityMembership.cs`, `Features/Board/*`)
should gain their own marker regions during implementation, following the same
`domain=entities` / feature-scoped convention as the rest of the codebase.

### Marker syntax

```csharp
// <!-- REPOWISE:START domain=entities -->
// ... generated content ...
// <!-- REPOWISE:END -->
```

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

*No entries.* The one constitutional item requiring explicit documentation (spec independence,
§12) is a documented exception, not a violation requiring a simpler-alternative justification —
see Constitution Check above.

---

## Post-Design Constitution Check (re-evaluated after Phase 1)

All gates from the pre-Phase-0 check above still pass after `data-model.md` and
`contracts/board-access.md` were written, and after the 2026-08-23 clarifications were folded in.
Three design decisions worth calling out because they kept the design *simpler* than the obvious
alternative, not more complex:

- **R4** (research.md) rejected introducing an ASP.NET Core authorization-policy framework in
  favor of a plain injected `ICommunityScopeResolver` service, matching the codebase's existing
  claim-and-service-call pattern rather than adding a new architectural layer. The 2026-08-23
  clarifications keep this service *simpler*: strict per-`Community`-row scope means no
  parent/child tree walk, and union-across-role-rows is a plain "allow if any active row grants
  the capability" check — no precedence table.
- **R6** (research.md) rejected a new navigation-tree endpoint in favor of deriving nav
  client-side from claims the frontend already decodes; that derivation now takes the **union** of
  the user's active roles in the active community rather than a single role, a small generalization
  of the wireframe signature with no new network contract.
- **Audit trail** (FR-017) is a structured Serilog sensitive-event, not a new table — no schema,
  migration, or query surface added for it in this spec.

No new Constitution Check violations were introduced by the Phase 1 design. Ready for
`/speckit.tasks`.
