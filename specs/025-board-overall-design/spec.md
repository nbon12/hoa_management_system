# Feature Specification: Board Member Experience — Overall Design

**Feature Branch**: `025-board-overall-design`
**Created**: 2026-08-19
**Status**: Draft
**Input**: User description: "Overall design for the board member flows — the shared shell, role model, community scope, and metric presentation contract that the Community Overview, Architectural Applications, Board Approvals, Accounting, and Reports specs all build on."

## Context

Today the product serves exactly one persona: a homeowner looking at their own property. This spec introduces the second persona — the board member — plus the community-scoped foundations that every subsequent board spec depends on.

This is spec 1 of 6 in the board member effort:

| # | Spec | Depends on this one for |
| --- | --- | --- |
| 1 | **Overall design** (this spec) | — |
| 2 | Community Overview & Metrics | Community entity, metric descriptor contract, shell |
| 3 | Architectural Applications | Community scope, board role, attachment URLs |
| 4 | Board Approvals | Community scope, board role |
| 5 | Accounting (invoices, vendor aging) | Community scope, manager/accountant roles |
| 6 | Reports | Community scope, role gating, scheduled delivery identity |

**This spec is a hard dependency for specs 2–6.** See *Spec independence & parallelism* for the justification.

## Grounding: what exists today

Verified against `main` at the time of writing:

- **There is no `Community` entity.** `Property.CommunityId` is a denormalized `string`, and `Property.CommunityName` is a second denormalized copy. `Violation.CommunityId` repeats the same string. No table holds a community's legal name, county, formation date, or description.
- **There is no role model.** `ApplicationUser` (ASP.NET Identity) has `FirstName`, `LastName`, `UserProperties`, `RefreshTokens`. No HOA-scoped role or membership table exists.
- **Authorization is property-scoped only.** Every protected read resolves `User → UserProperty → Property`. There is no primitive that answers "may this user see all properties in community X?"
- **The Angular shell is single-mode.** `ShellComponent` renders a fixed `navGroups` array (Dashboard / Payments / Property / Community) with no role input; `app.routes.ts` guards everything behind `authGuard` alone.

The board experience cannot be built on any of this without first introducing community as a real thing, and membership as a real relationship.

## Clarifications

### Session 2026-08-19

- Q: How does a board member sign in — separate portal, portal picker, or one login? → A: One login. A user signs in normally and switches into Board mode inside the app. No second portal, no separate URL.
- Q: Who else uses this shell? → A: Board members, community managers, and accountants share one role-gated shell.
- Q: Where does the Resident ↔ Board switcher live? → A: In the top bar, in the account-controls cluster to the left of alerts and the avatar. A distinct colored banner marks board mode but carries no control.
- Q: How many communities does a typical board member see? → A: Exactly one. Managers may hold a portfolio.
- Q: Should "My Workspace" / "My Communities" always appear? → A: No. It appears only when the user holds more than one community, regardless of role. One community means the nav item is not rendered and the user lands directly on Community Home.
- Q: How should metric definitions be surfaced? → A: Clicking a per-row help link opens a right-side glossary panel scrolled to that term.
- Q: Visual language for the board side? → A: Identical to the resident side — same palette, same warmth.
- Q: Should metrics be hand-built per page? → A: No. A single reusable registry must drive them so metrics can be added or retired without redesign.

### Session 2026-08-20

- Q: How are board membership records administered? → A: In-product — community managers can create and edit board memberships within the application, not only via seed/import.
- Q: What read scope should Committee Members have? → A: Question is moot — there is no separate Committee Member role. Architectural review is a Board Member capability; the architectural review committee is the board.
- Q: Are nav entries for capabilities the role doesn't have hidden entirely, or shown disabled with a lock affordance? → A: Shown disabled with a lock affordance, matching the wireframes.

### Session 2026-08-23

- Q: Does membership in a master community grant scope over its sub-associations' data? → A: No cascade. Scope is strictly per `Community` row; a separate membership is required for each sub-association.
- Q: How is the FR-017 audit trail for association-wide data access recorded — a queryable table or log events? → A: Serilog structured sensitive-events only; no new audit table in this spec. A queryable audit store is deferred to a later spec if compliance requires it.
- Q: When a user holds two roles in the same community, what is their effective capability? → A: The union of all active roles' capabilities. The resolver allows a capability if any active membership row grants it; nav derives from the union of roles.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Enter and leave board mode (Priority: P1)

As a homeowner who also serves on my HOA board, I sign in with my normal account and see an "Enter board mode" control alongside my usual account controls. Selecting it switches the application into board mode, where I see association-wide information instead of only my own home. I can return to resident mode at any time.

**Why this priority**: Without the mode switch there is no entry point to any board feature. Every other board story is unreachable.

**Independent Test**: Sign in as a user with an active board membership, confirm the control is present, enter board mode, confirm the shell changes and the banner appears, switch back, confirm the resident view is intact.

**Acceptance Scenarios**:

1. **Given** I hold an active non-resident membership, **When** I sign in, **Then** an "Enter board mode" control appears in the top bar to the left of the alerts and avatar controls.
2. **Given** I hold no non-resident membership, **When** I sign in, **Then** no board mode control is rendered anywhere in the application.
3. **Given** I am in resident mode, **When** I enter board mode, **Then** a visually distinct banner identifies the mode and a Resident/Board toggle appears in the top bar.
4. **Given** I am in board mode, **When** I sign out and sign in again, **Then** I return to the mode I was last in.
5. **Given** I am in board mode, **When** my board term ends (membership `EndDate` passes), **Then** my next request is served in resident mode and the board mode control is no longer rendered.

---

### User Story 2 — See only my own community's data (Priority: P1)

As a board member, everything I see in board mode is scoped to the community I serve. I cannot see another association's homeowners, balances, or documents, whether through the interface or by manipulating a request.

**Why this priority**: This is the security boundary of the entire board effort. Board members see other homeowners' delinquency and personal data; getting the scope wrong is a data breach, not a bug.

**Independent Test**: With two seeded communities and a board member in one, confirm every board endpoint returns only in-scope records and returns a not-found/forbidden response for an out-of-scope community id.

**Acceptance Scenarios**:

1. **Given** I am a board member of community A, **When** I request any community-scoped resource for community A, **Then** I receive only records belonging to community A.
2. **Given** I am a board member of community A, **When** I request the same resource for community B by substituting its identifier, **Then** the request is denied server-side regardless of what the interface offered me.
3. **Given** I am in board mode, **When** the server evaluates any request, **Then** authorization is decided from my persisted membership and never from the client-supplied mode.
4. **Given** I hold a resident-only membership, **When** I craft a request for a board-only resource, **Then** the request is denied.
5. **Given** I am a board member, **When** I view association-wide homeowner data, **Then** the access is recorded in an audit log with actor, community, resource, and timestamp.

---

### User Story 3 — Navigate the board shell (Priority: P1)

As a board member with one community, I land directly on that community's home page and navigate its sections from a grouped sidebar. Sections I have no right to use are not offered to me.

**Why this priority**: The shell is the container every sub-spec renders into; its contract must be settled before those specs can be built in parallel.

**Independent Test**: Sign in as each of board, manager, and accountant against a seeded community and confirm the rendered navigation matches that role's permitted set and the landing route is correct.

**Acceptance Scenarios**:

1. **Given** I hold exactly one community membership, **When** I enter board mode, **Then** no "My Communities" item is rendered and I land on that community's home.
2. **Given** I hold two or more community memberships of any role, **When** I enter board mode, **Then** a "My Communities" item is rendered and I land on that list.
3. **Given** I am a board member, **When** I view the sidebar, **Then** finance sections restricted to managers and accountants are not offered as working links.
4. **Given** I am a community manager, **When** I view the sidebar in the same community, **Then** those finance sections are present and usable.
5. **Given** I navigate directly to a route my role cannot use, **When** the route resolves, **Then** I am refused and redirected to a permitted page rather than shown an empty screen.

---

### User Story 4 — Read a metric and understand what it means (Priority: P2)

As a board member looking at a table of community metrics, I can click a help link on any row to open a glossary panel scrolled to that term's definition, so I know what "over 60 days delinquent" actually counts before I act on it.

**Why this priority**: Board members are volunteers, not operators. An undefined metric is a metric that gets misread in a meeting. It is P2 because the metrics themselves ship in spec 2.

**Independent Test**: Render any metric surface from a seeded registry, click a row's help link, confirm the panel opens with the matching definition targeted.

**Acceptance Scenarios**:

1. **Given** any metric surface, **When** it renders, **Then** the help affordance is the right-most column of the row.
2. **Given** I click a row's help link, **When** the panel opens, **Then** it is positioned at that metric's definition and the entry is visually distinguished.
3. **Given** a metric is added to the registry with a definition, **When** its surface renders, **Then** both the row and its glossary entry appear with no layout or component change.
4. **Given** a metric is removed from the registry, **When** its surface renders, **Then** neither the row nor its glossary entry appears and no gap is left behind.

---

### User Story 5 — Grant and edit board membership (Priority: P1)

As a community manager, I can create a `CommunityMembership` for a homeowner or outside professional — assigning them a community, a role, and an optional end date — and edit or end that membership later, without engineering involvement.

**Why this priority**: Every other story in this spec assumes an active membership already exists. Without an in-product way to create one, no board member can ever enter board mode in a real deployment — this is the only path into the entire feature outside of test fixtures.

**Independent Test**: Sign in as a community manager, grant a board membership to a resident user, confirm they can now enter board mode for that community; edit the membership's end date to the past, confirm they lose access on their next request.

**Acceptance Scenarios**:

1. **Given** I am a community manager for community A, **When** I create a membership for a user with role Board Member and no end date, **Then** the membership is active immediately and that user's mode control appears on their next request.
2. **Given** I am a community manager, **When** I attempt to create a membership for a community I do not manage, **Then** the request is denied server-side.
3. **Given** an active membership I administer, **When** I change its role or end date, **Then** the change takes effect on the member's next request with no re-authentication required.
4. **Given** an active membership I administer, **When** I end it (set an end date in the past, or set status inactive), **Then** the member loses the associated permissions on their next request per FR-023.
5. **Given** I am not a community manager, **When** I attempt to reach the membership-admin surface, **Then** I am refused per the same role-gated navigation rules as any other restricted section (FR-027, FR-028).

---

### Edge Cases

- **A user holds different roles in different communities** (board member at home, manager professionally). Role is resolved per community, never globally; entering community A must not carry community B's permissions.
- **A board term expires mid-session.** Membership carries `EndDate`; the server must re-evaluate per request rather than trusting a token minted while the term was active.
- **A board member sells their home** but their term has not ended. Board membership is independent of property ownership; losing `UserProperty` must not revoke board access, and vice versa.
- **A user is a board member of a sub-association only.** Community supports a parent/child relation; scope must not silently widen from a sub-association to its master. Nor does it widen in the other direction: membership in a master community grants no scope over its sub-associations. Scope is decided strictly per `Community` row — a user needs a distinct membership for the master and for each sub-association they may access (Clarifications, 2026-08-23).
- **Ending the last manager.** A community may not be left with zero active Community Managers via the membership-admin surface; the operation is refused (constitution §3). Freshly backfilled communities start with none until a first manager is assigned — that first assignment is the exception.
- **A community has no metrics configured.** Surfaces render an explicit empty state, not a zero-row table with headers.
- **A metric's value is unavailable** (upstream failure). The row renders with an explicit unavailable state; one failed metric must not blank the page.
- **A user holds exactly one community but it is inactive/offboarded.** They are treated as holding zero board-accessible communities.
- **Mode is stale in a second browser tab.** The server decides authorization per request; a stale client mode can never grant access.

## Assumptions

- Authentication, refresh tokens, and `authGuard` already exist and are reused unchanged; this spec adds authorization, not authentication.
- The existing string `Property.CommunityId` values are stable enough to backfill into `Community` rows as part of this feature's migration.
- Resident-mode behavior is unchanged by this spec. Existing property-scoped endpoints keep their current semantics.
- The board side reuses the existing design tokens in `styles.scss`; no new palette is introduced.
- Community photos and calendar content shown on the board home are existing resident-side features re-scoped to the community, not new subsystems.
- Sub-associations (Keystone Crossing SF / TH under the Master) are modeled as communities with a parent, not as a separate entity type.
- Mobile layout for the board shell is out of scope for this feature; board tools are desktop-first.

## Requirements *(mandatory)*

### Functional Requirements

> **Design references.** UI-facing requirements below cite the board wireframe bundle at `wireframes/HOA Management CRM v1.0.1/` (`WFb`) — canvas section **`7 · Board & manager side`**. Screen components live in `wf-board-screens.jsx` (`BoardModeSwitch`, `BoardHome`, `BoardMetrics`, `BoardOverview`, `BoardArchApps`, `BoardVendors`, `BoardNavMulti`); the shared shell, metric registry, and nav derivation live in `wf-board.jsx` (`WFShellBoard`, `METRICS`, `boardNav()`, `MetricTable`, `GlossaryPanel`, `HeroStat`). These are **lofi wireframes**: layout, hierarchy, table columns, and copy are intentional; color/spacing are placeholder — map them to `neko-hoa/src/styles.scss` tokens, not the sketch palette (`wireframe-styles.css`). Do **not** port the JSX; the target is Angular. The bundle's written spec is `specs/021-board-overall-design/spec.md`, an **earlier revision of this one** — where they differ (notably FR-040/FR-041/FR-042, clarified here), this `025` spec is authoritative. Requirements with no wireframe are marked `[no WFb]`.

**Community as a first-class entity**

- **FR-001**: System MUST introduce a `Community` entity with a stable GUID primary key, and MUST persist: legal name, community name (the management company's human-readable handle), county, formation date, management start date, description, and status. *[design: `WFb BoardOverview` is the surface that displays these fields — legal name, community name, county, formation date, management start date, Community GUID, and description; on-canvas note: "Community GUID drives queries; Community Name is the human handle."]*
- **FR-002**: The community name MUST be unique per management company and MUST be usable as a lookup handle; the GUID MUST be the identifier used in all queries and API paths.
- **FR-003**: System MUST support a parent/child relation between communities so a master association can contain sub-associations. *[design: `WFb BoardOverview` "Sub-associations" card shows Keystone Crossing SF and TH under the Master; `BoardNavMulti` lists the Master and both children as distinct communities.]*
- **FR-004**: `Property.CommunityId` MUST become a foreign key to `Community`, and the denormalized `Property.CommunityName` MUST be derived from the related community rather than stored separately.
- **FR-005**: A migration MUST backfill existing distinct `CommunityId` / `CommunityName` string pairs into `Community` rows with no loss of existing property associations. Migrations are **forward-only** (owner decision, 2026-08-26): `Down()` is not maintained or exercised, and recovery from a bad migration is by restoring a database branch or snapshot, never by rolling the migration back. The up-migration MUST still be verified to apply cleanly against a seeded multi-community dataset and to leave no orphan community foreign keys on any community-scoped table.
- **FR-006**: All entities currently carrying a loose `CommunityId` string (including `Violation`) MUST be migrated to the foreign key.

> **Migration notes (forward-only, FR-005).**
>
> - **Known landmine — `Down()` is broken and stays broken.** In `HOAManagementCompany/Infrastructure/Persistence/Migrations/20260825181517_AddCommunityAndMembership.cs`, `Down()` restores the string `CommunityId` columns by writing the *uncapped* `Communities.CommunityName` into them and then narrows the column to `character varying(20)`. Any community name longer than 20 characters makes that step fail. Harmless while nothing runs `Down()`, and it disappears at squash time. Recorded so it is not rediscovered as a surprise; it is not a defect to fix, and no down-migration test is written.
> - **Re-baselining at squash time.** Any environment that has **already applied** `20260825181517` must be re-baselined when the migrations are squashed: the squashed migration gets a new id that will not match `__EFMigrationsHistory`. Per-PR Neon branches are disposable and need nothing. The persistent Neon **Dev** database — and **Staging**, if deployed by then — needs a drop-and-reseed or a manual `__EFMigrationsHistory` fixup.

**Membership and roles**

- **FR-007**: System MUST introduce a `CommunityMembership` entity linking a user to a community with a role, a status, a start date, and an optional end date.
- **FR-008**: System MUST support at minimum these roles: Resident, Board Member, Community Manager, Accountant. Architectural review is a Board Member capability, not a separate role.
- **FR-009**: A user MUST be able to hold memberships in multiple communities, with a different role in each. A user MAY also hold more than one role within a single community; in that case their effective capability in that community is the **union** of all their active roles' capabilities (Clarifications, 2026-08-23).
- **FR-010**: A membership whose end date has passed, or whose status is not active, MUST confer no permissions.
- **FR-011**: Board membership MUST be independent of property ownership; granting or revoking one MUST NOT alter the other.
- **FR-041**: *(withdrawn — architectural review is a Board Member capability; there is no separate committee-member role. See Clarifications 2026-08-20.)*
- **FR-042**: System MUST provide an in-product surface for community managers to create and edit `CommunityMembership` records (assign a user, community, role, status, and term) without requiring a seed/import step. Every membership create, edit, or end MUST be recorded as a security-sensitive Serilog event capturing actor, community, target user, the change, and UTC timestamp (constitution §7). The surface MUST refuse any operation that would reduce a community that currently has an active Community Manager to zero active Community Managers — the last manager cannot be ended or downgraded (constitution §3); provisioning a community's first manager remains allowed. *[design: `[no WFb]` — the handoff states plainly "No admin surface is specified in this effort"; this FR was added by the 2026-08-20 clarification, beyond what the wireframes cover. Closest reusable layout is the resident bundle's editable-form + history pattern; the surface needs a new wireframe or a Storybook-first build.]*

**Community scope (the shared authorization primitive)**

- **FR-012**: System MUST provide a single server-side community-scope resolver that, given the current user, a target community, and a required capability, returns an allow/deny decision. All board-side endpoints across specs 2–6 MUST use it rather than implementing their own check. The resolver MUST decide scope strictly per `Community` row and MUST NOT traverse the parent/child relation — master membership confers no access to sub-associations, and vice versa (Clarifications, 2026-08-23). The resolver MUST grant a capability when **any** of the caller's active membership rows in the target community confers it (union across role rows), so a user holding two roles in one community receives the combined capability set (Clarifications, 2026-08-23).
- **FR-013**: Cross-community access MUST be denied by default. Any endpoint that intentionally spans communities MUST document its authorization and the scope of its results.
- **FR-014**: Authorization MUST be evaluated server-side on every request from persisted membership. The client's current mode MUST NOT be an input to any authorization decision.
- **FR-015**: Resident-scoped endpoints MUST retain their existing "own properties only" semantics and MUST NOT widen because the user also holds a board membership.
- **FR-016**: A board-scoped request for a community the user does not belong to MUST fail closed and MUST NOT disclose whether that community exists.
- **FR-017**: System MUST record an audit entry when a user accesses association-wide personal or financial data, capturing actor, community, resource, and UTC timestamp. The entry MUST be emitted as a dedicated structured Serilog sensitive-event to the existing logging sink; this spec introduces no queryable audit table (Clarifications, 2026-08-23). The US2 acceptance test asserts the structured event is emitted with all four fields. A durable, queryable audit store is out of scope here and deferred to a later spec should compliance require it.

**Mode switching**

- **FR-018**: System MUST offer a single sign-in for all personas. No separate board portal, URL, or credential set. *[design: `WFb BoardModeSwitch` — one login; a board-eligible resident sees "🗝️ Enter board mode" in their own top bar and the resident dashboard stays unchanged, with no portal picker. (The older resident bundle's `§1` portal-selection screen offered a separate "Board / Mgmt" door; that approach is superseded by this design and FR-018.)]*
- **FR-019**: The mode control MUST render in the top bar within the account-controls cluster, to the left of the alerts and avatar controls, and MUST NOT occupy space in the page body. *[design: `WFb BoardModeSwitch` — the "🗝️ Enter board mode" chip sits in the `wf-user` cluster immediately left of `🔔 3` and the `NB` avatar; the on-canvas note reads "sits with the other account controls, top-right — nothing added to the page body."]*
- **FR-020**: The mode control MUST be rendered only for users holding at least one active non-resident membership.
- **FR-021**: While in board mode, a visually distinct banner MUST identify the mode and state that association-wide data is shown. *[design: `WFb WFShellBoard` renders the violet board banner above the shell; it carries no control (the toggle lives in the top bar). Binding contrast decision from the handoff: dark ink on the violet fill — white-on-violet measured 2.51:1 and fails WCAG 2.1 AA (FR-038).]*
- **FR-022**: The user's last-used mode MUST persist across sessions.
- **FR-023**: When a user's last non-resident membership becomes inactive, the application MUST return them to resident mode on their next request.

**Navigation shell**

- **FR-024**: The board shell MUST present a grouped left sidebar and MUST accept its navigation set as data, so sibling specs add sections without modifying the shell. *[design: `WFb boardNav()` in `wf-board.jsx` returns the grouped nav array (Community management / Finance / Vendors groups) that `WFShellBoard` renders as data; sibling specs extend the array, not the shell.]*
- **FR-025**: A "My Communities" navigation item MUST be rendered if and only if the user holds more than one active community membership, independent of role. *[design: `WFb boardNav()` — `if (communities > 1) g.push({ items: [{ label: 'My Communities' }] })`; the `BoardNavMulti` screen shows the multi-community state (role Manager, communities 4).]*
- **FR-026**: A user holding exactly one active community membership MUST land directly on that community's home page. *[design: `WFb BoardHome` is that landing page; `boardNav()` omits the "My Communities" item when `communities === 1`.]*
- **FR-027**: Navigation entries for capabilities the user's role does not confer MUST NOT be offered as working links. Where the user holds more than one role in the active community, the offered navigation is derived from the **union** of those active roles' capabilities (Clarifications, 2026-08-23) — the wireframe's single-`role` `boardNav()` signature is generalized to a role set in implementation. *[design: `WFb boardNav()` tags Finance items with `roles: ['manager', 'accountant']`; `WFShellBoard` renders them non-navigable for a board member — see the `BoardVendors` note "board sees approval + insurance; managers open the full grid."]*
- **FR-028**: Direct navigation to a route the user's role cannot use MUST be refused and redirected to a permitted page. *[design: behavioral, no static frame — the handoff specifies "refused and redirected to a permitted page — never an empty screen."]*
- **FR-040**: Navigation entries the user's role does not confer MUST be rendered visible but disabled, with a lock affordance signaling the capability exists and is unavailable to this role. *[design: `WFb WFShellBoard` (`wf-board.jsx`) computes `const locked = it.roles && !it.roles.includes(role)` and renders a `🔒` titled "Not available to your role" with the `locked` class — this is the wireframe the 2026-08-20 clarification refers to.]*

**Metric presentation contract**

- **FR-029**: System MUST define a single metric descriptor contract carrying at minimum: stable id, surface, label, definition text, value, optional supporting detail, status, emphasis, and required capability. *[design: `WFb METRICS` in `wf-board.jsx` is that contract — each descriptor carries `id`, `surface` (routes to a page section), `label`, `value`, `status`, `help` (glossary copy), `tone` (emphasis), `hero` (promotes to a summary card).]*
- **FR-030**: All metric surfaces (summary statistics, metric tables, and the glossary) MUST be rendered from that one collection of descriptors. No metric may be positioned by hand in a template. *[design: `WFb` — `HeroStat` (via `heroes()`), `MetricTable` (via `bySurface()`), and `GlossaryPanel` all read the single `METRICS` array; `BoardHome` renders Work Processed + Community Metrics tables and hero stats entirely from it.]*
- **FR-031**: Adding or removing a metric MUST require only a change to the descriptor collection — no layout, component, or template change.
- **FR-032**: The help affordance MUST be the right-most column of every metric row. *[design: `WFb MetricTable` puts the help trigger in the right-most column; the `BoardHome` on-canvas note reads "help moved to the right-most column →".]*
- **FR-033**: Selecting a metric's help affordance MUST open a glossary panel positioned at that metric's definition, with the targeted entry visually distinguished. *[design: `WFb BoardMetrics` renders the open state — `WFShellBoard(... glossary="over60")` drives `GlossaryPanel target=` to scroll to and highlight that term in the right-side panel.]*
- **FR-034**: Glossary content MUST be derived from the same descriptors as the rows, so a definition cannot drift from its metric. *[design: `WFb GlossaryPanel` reads the `help` field of the same `METRICS` descriptors the rows render from — one source, no drift.]*
- **FR-035**: A descriptor whose required capability the user lacks MUST NOT render on any surface.
- **FR-036**: An individual metric that fails to resolve MUST render an explicit unavailable state without preventing its siblings from rendering.

**Shared presentation**

- **FR-037**: The board side MUST use the existing design tokens and visual language; no separate board palette is introduced. *[design: `WFb` is lofi — its sketch palette in `wireframe-styles.css` is placeholder only. The handoff is explicit: map to `neko-hoa/src/styles.scss` tokens; no new board palette. The `--violet` fill and `--ink` text of the banner are the one binding color pairing (see FR-021).]*
- **FR-038**: Text and interactive controls introduced by this spec MUST meet WCAG 2.1 AA contrast, including the board mode banner and the help affordance.
- **FR-039**: System MUST provide a shared short-lived pre-signed URL primitive for private documents, so sibling specs (notably Architectural Applications) never expose durable public object URLs. *[design: `WFb BoardArchApps` — homeowner attachments (plans, elevations, plat surveys) are labeled "stored in S3, opens in a new tab" and link out rather than embedding; the handoff requires these be served through the short-lived pre-signed URL, never a durable public object URL.]*

### Key Entities

- **Community**: An association. Stable GUID identity, a unique human-readable name used as a handle, legal name, county, formation date, management start date, description, status, and an optional parent community for master/sub relationships. The tenant boundary for all board features.
- **CommunityMembership**: The relationship granting a user a role within one community, with status and a term (start date, optional end date). The sole source of board-side authorization.
- **CommunityRole**: The enumerated capability set — Resident, Board Member, Community Manager, Accountant. Architectural review authority belongs to Board Member; there is no separate committee role.
- **MetricDescriptor**: The presentation contract for a single metric — id, surface, label, definition, value, detail, status, emphasis, required capability. Drives summary statistics, tables, and glossary alike.
- **Property** *(modified)*: Gains a real foreign key to Community; loses its denormalized community name.
- **Violation** *(modified)*: Gains a real foreign key to Community in place of its loose string.
- **ApplicationUser** *(unchanged)*: Gains a memberships collection; identity and authentication semantics are untouched.

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: `Community` becomes the tenant boundary for all board-side features, replacing the loose `CommunityId` string. Every board-scoped entity resolves to exactly one community. Cross-community access is denied by default (FR-013). The only intentional cross-community surface is the "My Communities" list, which returns only communities where the caller holds an active membership and exposes summary fields only.
- **Authorization**: Board-side actions require an active `CommunityMembership` of sufficient role in the target community, checked server-side on every request via the shared resolver (FR-012, FR-014). The Resident/Board mode toggle is UX state only and is never an authorization input. Frontend role checks exist solely to avoid presenting unusable controls.
- **Ownership and moderation**: This spec introduces no user-generated content. It introduces the audit trail (FR-017) that sibling specs record board decisions against.
- **API contract**: Community-scoped collections follow the existing response and error shapes with `limit`/`offset` pagination and documented defaults and maximums. Community is addressed by GUID in paths. Timestamps are UTC. Membership changes are additive; no existing resident endpoint changes shape, though `Property` responses derive community name rather than reading a stored copy.
- **API implementation and docs**: New endpoints are FastEndpoints, documented via Swashbuckle, with `/swagger` remaining development-only and disabled in production.
- **Database/runtime**: Strict, **forward-only** EF Core migrations (FR-005 — `Down()` is unmaintained; recovery is by database branch/snapshot restore). The community backfill (FR-005) must be idempotent and safe to run at Cloud Run startup, and must be verified against a seeded multi-community dataset. Neon connection limits, pooling, and short-lived DbContext expectations are unchanged.
- **File storage**: This spec adds no new file types but defines the shared pre-signed URL primitive (FR-039) that sibling specs use. Objects live in Cloudflare R2 with metadata in PostgreSQL; MinIO covers local Docker Compose and tests.
- **Security and abuse controls**: Community-scope denials fail closed and must not disclose the existence of out-of-scope communities (FR-016). Membership grants, revocations, and association-wide data access are logged as sensitive events. Board-side endpoints inherit existing rate limits; all community identifiers from the client are treated as untrusted and re-resolved against membership.
- **Observability**: Sentry captures errors across both modes with the active community and role attached as tags (never homeowner personal data). Trace context propagates from the Angular shell through board endpoints. Environment and release tags unchanged.
- **Accessibility**: The mode toggle, sidebar, help affordance, and glossary panel must be fully keyboard operable with visible focus. The glossary panel must move focus to the targeted definition when opened and return focus on close. All new text meets WCAG 2.1 AA contrast (FR-038) — note the board banner is dark ink on the violet fill, matching the house pattern, because white-on-violet measured 2.51:1.
- **Quality gates**: The community-scope resolver, membership evaluation, and migration are the 95%-coverage-critical files. xUnit with Testcontainers covers the resolver against a seeded two-community dataset, including expired terms, sub-association boundaries, and role-mismatch denials. Theory data must vary role × membership status × target community. Tests must be parallel-safe and must not depend on prior run artifacts. Serilog records authorization denials. Repowise docs refreshed for PR delivery. Scope stays a vertical slice: entity, migration, resolver, shell.
- **Frontend testing**: Jasmine/Karma for the mode service and nav-derivation logic (including the "more than one community" rule at 0, 1, and 2+). Angular Testing Library for the shell, metric table, and glossary panel. Playwright for the mode-switch journey and role-gated route refusal. Cypress E2E for sign-in → board mode → community home. Storybook visual regression for the shell, banner, metric table, hero statistics, and glossary panel.
- **Executable & living spec**: Every acceptance scenario above maps to an automated test that runs on demand and passes before merge. This `spec.md` is updated before the PR. Specs 001 and 003 describe a property-scoped world; where this spec widens that model, those specs must be reconciled rather than left contradictory — in particular spec 001's statement that board members see only their own properties remains true of the resident dashboard and must be annotated as scoped to resident mode.
- **Spec independence & parallelism**: **This spec is a deliberate exception to the independence rule.** It is individually completable — the entity, membership model, resolver, and shell can be built, tested, and merged with no sibling spec present, and it delivers a demonstrable slice (a board member signs in, enters board mode, and lands on a scoped community home). But specs 2–6 each hold a hard dependency on it. The split could not remove that dependency: community scope is a security primitive, and letting five specs each implement their own version would produce divergent authorization logic — the precise failure mode that leaks one association's homeowner data to another's board. Sequencing this spec first is the mitigation. Once it lands, specs 2–6 can proceed in parallel, as their remaining dependencies are on this foundation rather than on each other.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user holding board membership in one community can sign in and reach that community's home in a single action from the top bar, with no intermediate list to traverse.
- **SC-002**: 100% of board-scoped endpoints deny requests for a community the caller does not belong to, verified by an automated test that attempts every such endpoint with an out-of-scope identifier.
- **SC-003**: No board-scoped endpoint implements its own scope check; every one resolves through the shared primitive, verified by static analysis.
- **SC-004**: Adding a new community metric requires changing exactly one descriptor collection and zero layout, component, or template files, demonstrated by adding one in test.
- **SC-005**: A user whose board term has ended loses board access on their next request without administrator action.
- **SC-006**: Existing resident-mode behavior is unchanged, verified by the pre-existing dashboard, payments, and property test suites passing without modification.
- **SC-007**: Every metric row exposes its definition in one click, and no definition exists outside the descriptor collection.
- **SC-008**: All new interactive controls pass WCAG 2.1 AA contrast and are reachable by keyboard alone.
- **SC-009**: A community manager can grant a new board membership and have that member enter board mode within the same test session, with no engineering, database, or seed-script involvement.
