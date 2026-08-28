# Feature Specification: Board Member Experience — Overall Design

**Feature Branch**: `021-board-overall-design`
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
| 4 | Board Approvals | Community scope, board/committee roles |
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

### Edge Cases

- **A user holds different roles in different communities** (board member at home, manager professionally). Role is resolved per community, never globally; entering community A must not carry community B's permissions.
- **A board term expires mid-session.** Membership carries `EndDate`; the server must re-evaluate per request rather than trusting a token minted while the term was active.
- **A board member sells their home** but their term has not ended. Board membership is independent of property ownership; losing `UserProperty` must not revoke board access, and vice versa.
- **A user is a board member of a sub-association only.** Community supports a parent/child relation; scope must not silently widen from a sub-association to its master.
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

**Community as a first-class entity**

- **FR-001**: System MUST introduce a `Community` entity with a stable GUID primary key, and MUST persist: legal name, community name (the management company's human-readable handle), county, formation date, management start date, description, and status.
- **FR-002**: The community name MUST be unique per management company and MUST be usable as a lookup handle; the GUID MUST be the identifier used in all queries and API paths.
- **FR-003**: System MUST support a parent/child relation between communities so a master association can contain sub-associations.
- **FR-004**: `Property.CommunityId` MUST become a foreign key to `Community`, and the denormalized `Property.CommunityName` MUST be derived from the related community rather than stored separately.
- **FR-005**: A migration MUST backfill existing distinct `CommunityId` / `CommunityName` string pairs into `Community` rows with no loss of existing property associations, and MUST be reversible.
- **FR-006**: All entities currently carrying a loose `CommunityId` string (including `Violation`) MUST be migrated to the foreign key.

**Membership and roles**

- **FR-007**: System MUST introduce a `CommunityMembership` entity linking a user to a community with a role, a status, a start date, and an optional end date.
- **FR-008**: System MUST support at minimum these roles: Resident, Board Member, Committee Member, Community Manager, Accountant.
- **FR-009**: A user MUST be able to hold memberships in multiple communities, with a different role in each.
- **FR-010**: A membership whose end date has passed, or whose status is not active, MUST confer no permissions.
- **FR-011**: Board membership MUST be independent of property ownership; granting or revoking one MUST NOT alter the other.

**Community scope (the shared authorization primitive)**

- **FR-012**: System MUST provide a single server-side community-scope resolver that, given the current user, a target community, and a required capability, returns an allow/deny decision. All board-side endpoints across specs 2–6 MUST use it rather than implementing their own check.
- **FR-013**: Cross-community access MUST be denied by default. Any endpoint that intentionally spans communities MUST document its authorization and the scope of its results.
- **FR-014**: Authorization MUST be evaluated server-side on every request from persisted membership. The client's current mode MUST NOT be an input to any authorization decision.
- **FR-015**: Resident-scoped endpoints MUST retain their existing "own properties only" semantics and MUST NOT widen because the user also holds a board membership.
- **FR-016**: A board-scoped request for a community the user does not belong to MUST fail closed and MUST NOT disclose whether that community exists.
- **FR-017**: System MUST record an audit entry when a user accesses association-wide personal or financial data, capturing actor, community, resource, and UTC timestamp.

**Mode switching**

- **FR-018**: System MUST offer a single sign-in for all personas. No separate board portal, URL, or credential set.
- **FR-019**: The mode control MUST render in the top bar within the account-controls cluster, to the left of the alerts and avatar controls, and MUST NOT occupy space in the page body.
- **FR-020**: The mode control MUST be rendered only for users holding at least one active non-resident membership.
- **FR-021**: While in board mode, a visually distinct banner MUST identify the mode and state that association-wide data is shown.
- **FR-022**: The user's last-used mode MUST persist across sessions.
- **FR-023**: When a user's last non-resident membership becomes inactive, the application MUST return them to resident mode on their next request.

**Navigation shell**

- **FR-024**: The board shell MUST present a grouped left sidebar and MUST accept its navigation set as data, so sibling specs add sections without modifying the shell.
- **FR-025**: A "My Communities" navigation item MUST be rendered if and only if the user holds more than one active community membership, independent of role.
- **FR-026**: A user holding exactly one active community membership MUST land directly on that community's home page.
- **FR-027**: Navigation entries for capabilities the user's role does not confer MUST NOT be offered as working links.
- **FR-028**: Direct navigation to a route the user's role cannot use MUST be refused and redirected to a permitted page.

**Metric presentation contract**

- **FR-029**: System MUST define a single metric descriptor contract carrying at minimum: stable id, surface, label, definition text, value, optional supporting detail, status, emphasis, and required capability.
- **FR-030**: All metric surfaces (summary statistics, metric tables, and the glossary) MUST be rendered from that one collection of descriptors. No metric may be positioned by hand in a template.
- **FR-031**: Adding or removing a metric MUST require only a change to the descriptor collection — no layout, component, or template change.
- **FR-032**: The help affordance MUST be the right-most column of every metric row.
- **FR-033**: Selecting a metric's help affordance MUST open a glossary panel positioned at that metric's definition, with the targeted entry visually distinguished.
- **FR-034**: Glossary content MUST be derived from the same descriptors as the rows, so a definition cannot drift from its metric.
- **FR-035**: A descriptor whose required capability the user lacks MUST NOT render on any surface.
- **FR-036**: An individual metric that fails to resolve MUST render an explicit unavailable state without preventing its siblings from rendering.

**Shared presentation**

- **FR-037**: The board side MUST use the existing design tokens and visual language; no separate board palette is introduced.
- **FR-038**: Text and interactive controls introduced by this spec MUST meet WCAG 2.1 AA contrast, including the board mode banner and the help affordance.
- **FR-039**: System MUST provide a shared short-lived pre-signed URL primitive for private documents, so sibling specs (notably Architectural Applications) never expose durable public object URLs.

**Open questions**

- **FR-040**: Navigation entries the user's role does not confer MUST be [NEEDS CLARIFICATION: hidden entirely, or rendered disabled with a lock affordance to signal the capability exists? Hiding avoids support noise; showing a lock helps a board member understand what a manager does. The wireframes currently show a lock.]
- **FR-041**: Committee members MUST have [NEEDS CLARIFICATION: the same read scope as board members, or a narrower scope limited to the committee's subject area?]
- **FR-042**: Board membership records MUST be administered by [NEEDS CLARIFICATION: community managers in-product, or seeded/imported from the management company's system of record? No admin surface is specified in this effort.]

### Key Entities

- **Community**: An association. Stable GUID identity, a unique human-readable name used as a handle, legal name, county, formation date, management start date, description, status, and an optional parent community for master/sub relationships. The tenant boundary for all board features.
- **CommunityMembership**: The relationship granting a user a role within one community, with status and a term (start date, optional end date). The sole source of board-side authorization.
- **CommunityRole**: The enumerated capability set — Resident, Board Member, Committee Member, Community Manager, Accountant.
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
- **Database/runtime**: Strict, reversible EF Core migrations. The community backfill (FR-005) must be idempotent and safe to run at Cloud Run startup, and must be verified against a seeded multi-community dataset. Neon connection limits, pooling, and short-lived DbContext expectations are unchanged.
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
