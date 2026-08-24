# Board Member Experience — work broken into issues

Nine issues. Each block below is written to be pasted into a GitHub issue body as-is; the heading is
the issue title.

**Read `README.md` first.** In particular: this is an **Angular + .NET** codebase. The design files
are React/JSX because that is the medium they were drawn in — they are references, not code to port.

`spec.md` in this bundle is authoritative. FR numbers below point into it.

---

## Sequencing

```
        ┌─────────────────────────────────────────┐
        │  #1  Community entity + migration       │   ← nothing starts without this
        └────────────────┬────────────────────────┘
                         │
        ┌────────────────▼────────────────────────┐
        │  #2  CommunityMembership + roles        │
        └────────────────┬────────────────────────┘
                         │
        ┌────────────────▼────────────────────────┐
        │  #3  Community-scope resolver + audit   │   ← the security primitive
        └────────┬───────────────────┬────────────┘
                 │                   │
   ┌─────────────▼──────┐   ┌────────▼───────────┐   ┌──────────────────────┐
   │ #4 Mode switching  │   │ #5 Board shell nav │   │ #6 Metric contract   │
   └─────────────┬──────┘   └────────┬───────────┘   └────────┬─────────────┘
                 │                   │                        │
                 └───────────────────┴────────┬───────────────┘
                                              │
                              ┌───────────────▼──────────────┐
                              │  #8  Community Home screen   │
                              └──────────────────────────────┘

   #7 Pre-signed URL primitive  — independent, any time after #3
   #9 Community Overview screen — after #5
```

**#1 → #2 → #3 is a hard chain.** Once #3 merges, #4, #5, #6, and #7 can run in parallel. #8 needs
#5 and #6 both landed.

Once this whole set lands, the five sibling features (Community Overview & Metrics, Architectural
Applications, Board Approvals, Accounting, Reports) can proceed in parallel — their remaining
dependencies are on this foundation, not on each other.

---

## Issue #1 — Introduce the `Community` entity and backfill migration

**Depends on**: nothing. **Blocks**: everything.

### Context

There is no `Community` entity today. `Property.CommunityId` is a denormalized `string`,
`Property.CommunityName` is a second denormalized copy, and `Violation.CommunityId` repeats the same
string. No table holds a community's legal name, county, formation date, or description. Community
has to become a real thing before it can be a security boundary.

### Scope

- New `Community` entity, **stable GUID primary key**. Persists: legal name, community name (the
  management company's human-readable handle), county, formation date, management start date,
  description, status. (FR-001)
- Community name **unique per management company** and usable as a lookup handle. The **GUID** is the
  identifier used in all queries and API paths. (FR-002)
- **Parent/child relation** between communities so a master association can contain sub-associations
  — Keystone Crossing SF / TH under the Master. Modeled as communities with a parent, *not* a
  separate entity type. (FR-003)
- `Property.CommunityId` becomes a **foreign key** to `Community`. Denormalized
  `Property.CommunityName` is **removed** and derived from the relation. (FR-004)
- Every other entity carrying a loose `CommunityId` string — including `Violation` — migrates to the
  foreign key. (FR-006)
- EF Core migration backfills existing distinct `CommunityId` / `CommunityName` string pairs into
  `Community` rows, **with no loss of existing property associations**, and is **reversible**. (FR-005)

### Acceptance

- [ ] Migration is reversible, idempotent, and safe to run at Cloud Run startup.
- [ ] Verified against a seeded **multi-community** dataset.
- [ ] No existing property association is lost or reassigned by the backfill.
- [ ] `Property` API responses **derive** community name rather than reading a stored copy. Response
      shape is otherwise unchanged.
- [ ] Existing dashboard, payments, and property test suites pass **without modification** (SC-006).

### Files

`Domain/Entities/Property.cs`, `Domain/Entities/Violation.cs`, new `Domain/Entities/Community.cs`,
EF Core migrations.

### Note

95%-coverage-critical file. Neon connection limits, pooling, and short-lived DbContext expectations
are unchanged — don't alter them here.

---

## Issue #2 — `CommunityMembership` and the role model

**Depends on**: #1. **Blocks**: #3.
**⚠️ Blocked on a decision**: FR-041 (see below).

### Context

`ApplicationUser` has `FirstName`, `LastName`, `UserProperties`, `RefreshTokens` — no HOA-scoped role
or membership table exists anywhere. This issue creates the sole source of board-side authorization.

### Scope

- `CommunityMembership` entity linking a user to a community with a **role**, a **status**, a
  **start date**, and an **optional end date**. (FR-007)
- Roles, at minimum: **Resident, Board Member, Committee Member, Community Manager, Accountant**. (FR-008)
- A user may hold memberships in **multiple communities, with a different role in each**. Role
  resolves per community, never globally — entering community A must not carry community B's
  permissions. (FR-009)
- A membership whose end date has passed, or whose status is not active, **confers no permissions**. (FR-010)
- **Board membership is independent of property ownership.** Granting or revoking one must not alter
  the other. A board member who sells their home keeps their term; losing `UserProperty` must not
  revoke board access. (FR-011)
- `ApplicationUser` gains a memberships collection. Identity and authentication semantics are
  **untouched** — this issue adds authorization, not authentication.

### Acceptance

- [ ] A user with an expired term is denied, with no administrator action required (SC-005).
- [ ] A user with a non-active status is denied.
- [ ] The same user holding Board Member in A and Community Manager in B gets exactly those
      permissions in each, and no bleed between them.
- [ ] Membership grants and revocations are logged as sensitive events.

### ⚠️ Decision needed before starting

**FR-041 — committee member read scope**: do committee members get the **same read scope as board
members**, or a **narrower scope limited to the committee's subject area**? This changes the shape of
the role enum (a flat enum vs. role + subject-area qualifier), so resolve it before writing the entity.

### Also unresolved (not blocking this issue, but note it)

**FR-042 — who administers memberships**: community managers in-product, or seeded/imported from the
management company's system of record? **No admin surface is specified in this effort.** If the
answer is "in-product," that is additional scope with no design behind it. For now, seed memberships.

---

## Issue #3 — The shared community-scope resolver + audit trail

**Depends on**: #2. **Blocks**: #4, #5, #6, #7, and all five sibling features.

### Context

**This is the security boundary of the entire board effort.** Board members see other homeowners'
delinquency and personal data. Getting this wrong is a data breach, not a bug.

Today every protected read walks `User → UserProperty → Property`. There is no primitive that answers
*"may this user see all properties in community X?"*. This issue builds exactly one.

### Scope

- **One** server-side resolver: given the current user, a target community, and a required
  capability, returns allow/deny. **Every** board-side endpoint across all sibling features uses it
  rather than implementing its own check. (FR-012)
- **Cross-community access denied by default.** Any endpoint that intentionally spans communities
  documents its authorization and the scope of its results. The only intentional cross-community
  surface in this effort is the "My Communities" list, which returns **only** communities where the
  caller holds an active membership and exposes **summary fields only**. (FR-013)
- Authorization evaluated **server-side on every request, from persisted membership**. The client's
  current mode is **not** an input to any authorization decision. (FR-014)
- Resident-scoped endpoints **retain** their existing "own properties only" semantics and must not
  widen because the user also holds a board membership. (FR-015)
- A board-scoped request for a community the user doesn't belong to **fails closed** and **must not
  disclose whether that community exists**. (FR-016)
- Scope must not silently widen from a **sub-association to its master**.
- Audit entry when a user accesses association-wide personal or financial data: **actor, community,
  resource, UTC timestamp**. (FR-017)
- All community identifiers arriving from the client are treated as **untrusted** and re-resolved
  against membership.

### Acceptance

- [ ] 100% of board-scoped endpoints deny requests for a community the caller does not belong to,
      verified by an automated test that attempts **every** such endpoint with an out-of-scope
      identifier (SC-002).
- [ ] No board-scoped endpoint implements its own scope check — **verified by static analysis** (SC-003).
- [ ] A resident-only user crafting a request for a board-only resource is denied.
- [ ] A term that expires mid-session is caught on the next request, not trusted from a token minted
      while the term was active.
- [ ] A stale client mode in a second browser tab cannot grant access.
- [ ] Denials fail closed and leak no existence information.
- [ ] Serilog records authorization denials.

### Testing

95%-coverage-critical. xUnit + Testcontainers against a seeded **two-community** dataset. Theory data
must vary **role × membership status × target community**, covering expired terms, sub-association
boundaries, and role-mismatch denials. Parallel-safe; no dependence on prior run artifacts.

---

## Issue #4 — Mode switching (resident ↔ board)

**Depends on**: #3. **Parallel with**: #5, #6, #7.

> **📄 Split into session-sized stories in `USER-STORIES-login.md` (S1–S9).** This issue is a
> reasonable feature but a poor single session — it mixes a backend read model, an Angular control,
> persisted preference, route resolution, term-expiry demotion, and a negative-security suite. Use the
> stories file to run it as nine sessions. Keep this issue as the epic.

### Context

Without the mode switch there is no entry point to any board feature — every other board story is
unreachable. See artboard `b-switch` in the wireframe.

### Scope

- **One login for all personas.** No separate board portal, URL, or credential set. (FR-018)
- The mode control renders **in the top bar, within the account-controls cluster, to the left of the
  alerts and avatar controls**. It must **not** occupy space in the page body. (FR-019)
- Rendered **only** for users holding at least one active non-resident membership. A resident-only
  user sees no trace of it anywhere in the application. (FR-020)
- In board mode, a **visually distinct banner** identifies the mode and states that association-wide
  data is shown. The banner carries **no control** — the Resident/Board toggle stays in the top bar. (FR-021)
- Last-used mode **persists across sessions**. (FR-022)
- When a user's last non-resident membership becomes inactive, the application returns them to
  resident mode on their next request and stops rendering the control. (FR-023)

### Acceptance

- [ ] Sign in with an active non-resident membership → control present in the correct top-bar slot.
- [ ] Sign in with no non-resident membership → control absent everywhere.
- [ ] Enter board mode → shell changes, banner appears, toggle appears in top bar.
- [ ] Switch back → resident view intact.
- [ ] Sign out and back in → returned to the mode last used.
- [ ] Term ends → next request served in resident mode.
- [ ] Mode is UX state only and is never sent as, or used as, an authorization input.

### Accessibility

Banner is **dark ink on the violet fill** — white-on-violet measures 2.51:1 and fails AA. Toggle must
be keyboard operable with visible focus. (FR-038)

### Testing

Jasmine/Karma for the mode service. Playwright for the mode-switch journey. Storybook visual
regression for the banner.

---

## Issue #5 — Role-gated board shell and navigation

**Depends on**: #3. **Parallel with**: #4, #6, #7.
**⚠️ Blocked on a decision**: FR-040 (see below).

### Context

The shell is the container every sibling feature renders into — its contract must settle before those
features can be built in parallel. `ShellComponent` currently renders a fixed `navGroups` array with
no role input, and `app.routes.ts` guards everything behind `authGuard` alone.

Board members, community managers, and accountants **share one role-gated shell** — there is no
separate manager shell.

### Scope

- Grouped left sidebar. The shell **accepts its navigation set as data**, so sibling features add
  sections without modifying the shell. (FR-024)
- **"My Communities" renders if and only if the user holds more than one active community
  membership**, independent of role. (FR-025)
- A user holding **exactly one** active membership lands **directly on that community's home** — no
  intermediate list to traverse (FR-026, SC-001). With one community, the nav item is not drawn at all.
- Navigation entries for capabilities the role does not confer are **not offered as working links**.
  Finance sections restricted to managers and accountants are not usable by a board member; the same
  sections are present and usable for a community manager in the same community. (FR-027)
- Direct navigation to a route the role cannot use is **refused and redirected to a permitted page**,
  not shown as an empty screen. (FR-028)
- Board side uses **existing design tokens** from `styles.scss`. No separate board palette. (FR-037)

### Acceptance

- [ ] Sign in as each of board, manager, accountant against a seeded community → rendered navigation
      matches that role's permitted set and the landing route is correct.
- [ ] Nav derivation tested at **0, 1, and 2+** communities.
- [ ] Forbidden route → redirect to a permitted page.

### ⚠️ Decision needed before starting

**FR-040**: are role-gated nav entries **hidden entirely**, or **rendered disabled with a lock
affordance**? Hiding avoids support noise; showing a lock helps a board member understand what a
manager does. **The wireframes currently show a lock** — treat that as the default if no other answer
comes back.

### Files

`neko-hoa/src/app/shell/shell.component.ts`, `neko-hoa/src/app/app.routes.ts`.
Reference: `boardNav()` in `wf-board.jsx`.

### Note

Mobile layout for the board shell is **out of scope** — board tools are desktop-first.

### Testing

Jasmine/Karma for nav-derivation logic. Angular Testing Library for the shell. Playwright for
role-gated route refusal. Cypress E2E for sign-in → board mode → community home. Storybook visual
regression for the shell.

---

## Issue #6 — Metric descriptor contract, table, and glossary panel

**Depends on**: #3. **Parallel with**: #4, #5, #7.

### Context

Metrics must not be hand-built per page. A single reusable registry drives them so metrics can be
added or retired without redesign. See `METRICS` in `wf-board.jsx` and artboard `b-metrics`.

Board members are volunteers, not operators. An undefined metric is a metric that gets misread in a
meeting — the glossary is why this issue includes a help panel rather than deferring it.

### Scope

- A **single metric descriptor contract** carrying at minimum: stable id, surface, label, definition
  text, value, optional supporting detail, status, emphasis, required capability. (FR-029)
- **All** metric surfaces — summary statistics, metric tables, and the glossary — render from that
  **one collection**. No metric is positioned by hand in a template. (FR-030)
- Adding or removing a metric requires **only** a change to the descriptor collection — zero layout,
  component, or template changes. (FR-031)
- The **help affordance is the right-most column of every metric row**. (FR-032)
- Selecting it opens a **glossary panel positioned at that metric's definition**, with the targeted
  entry visually distinguished. (FR-033)
- Glossary content is **derived from the same descriptors as the rows**, so a definition cannot drift
  from its metric. (FR-034)
- A descriptor whose required capability the user lacks **does not render on any surface**. (FR-035)
- A metric that fails to resolve renders an **explicit unavailable state** without preventing its
  siblings from rendering — one failed metric must not blank the page. (FR-036)
- A community with no metrics configured renders an **explicit empty state**, not a zero-row table
  with headers.

### Acceptance

- [ ] Adding a new metric in test changes **exactly one** descriptor collection and **zero** layout,
      component, or template files (SC-004).
- [ ] Removing a metric leaves no gap behind on any surface.
- [ ] Every metric row exposes its definition in **one click**, and **no definition exists outside the
      descriptor collection** (SC-007).
- [ ] One metric failing does not blank its siblings.

### Accessibility

Help affordance and glossary panel fully keyboard operable with visible focus. The panel **moves
focus to the targeted definition when opened and returns focus on close**. (FR-038)

### Testing

Angular Testing Library for the metric table and glossary panel. Storybook visual regression for
metric table, hero statistics, and glossary panel.

---

## Issue #7 — Shared pre-signed URL primitive for private documents

**Depends on**: #3. **Parallel with**: #4, #5, #6.

### Context

Homeowner attachments on architectural applications — plans, photos, surveys — contain property
details and personal information. Sibling features must never expose durable public object URLs for
them. This issue builds the primitive once so Architectural Applications and everything after it
inherit it.

### Scope

- A shared **short-lived pre-signed URL** primitive for private documents. (FR-039)
- Objects live in **Cloudflare R2** with metadata in PostgreSQL; **MinIO** covers local Docker Compose
  and tests.
- No new file types are introduced by this issue — it is the primitive only.

### Acceptance

- [ ] Generated URLs expire.
- [ ] No durable public object URL is reachable for a private document.
- [ ] Access is subject to the community-scope resolver from #3.
- [ ] Works against MinIO locally and R2 in deployed environments.

---

## Issue #8 — Community Home screen

**Depends on**: #5 **and** #6. See artboards `b-home` (1360×1860) and `b-metrics` (1440×880).

### Context

The board member's landing page, and the first demonstrable slice of the whole effort: a board member
signs in, enters board mode, and lands on a scoped community home.

### Scope

**One long scrolling page** — community metrics are deliberately merged in here, not split into a
separate screen. Top to bottom:

1. Hero statistics — the `hero`-flagged descriptors from #6.
2. Open votes.
3. Community photos.
4. Calendar.
5. **Work Processed (last 30 days)** table — assessment payments processed, work orders, violation
   notices issued after inspection.
6. **Community Metrics** table — community status, delinquency and financial-health measures.

Sections 2–4 are **existing resident-side features re-scoped to the community**, not new subsystems.
Sections 5–6 render from the #6 descriptor collection with the right-most help column wired to the
glossary panel.

### Acceptance

- [ ] A board member with one community reaches this page **in a single action from the top bar**,
      with no intermediate list (SC-001).
- [ ] All data on the page is scoped to that community, verified server-side.
- [ ] Metric rows and glossary both render from the single descriptor collection.
- [ ] Association-wide data access here writes an audit entry (actor, community, resource, timestamp).

### Testing

Cypress E2E for sign-in → board mode → community home.

---

## Issue #9 — Community Overview screen

**Depends on**: #5. See artboard `b-overview` (1360×880).

### Scope

The community's record of itself: legal name, community name, **GUID**, county, formation date,
management start date, description, status. Sub-associations surface through the parent/child
relation from #1.

### Acceptance

- [ ] All fields from the `Community` entity render.
- [ ] A sub-association shows its parent; scope does not widen to the master.
- [ ] Read is gated through the community-scope resolver.

---

## Cross-cutting requirements for every issue

- **API**: FastEndpoints, documented via Swashbuckle, `/swagger` development-only and disabled in
  production. Community-scoped collections follow existing response and error shapes with
  `limit`/`offset` pagination and documented defaults and maximums. Community addressed by **GUID** in
  paths. Timestamps **UTC**. Membership changes are additive — no existing resident endpoint changes
  shape.
- **Observability**: Sentry captures errors across both modes with **active community and role as
  tags — never homeowner personal data**. Trace context propagates from the Angular shell through
  board endpoints.
- **Rate limits**: board-side endpoints inherit existing limits.
- **Spec reconciliation**: specs 001 and 003 describe a property-scoped world. Spec 001's statement
  that board members see only their own properties **remains true of the resident dashboard** and must
  be annotated as scoped to resident mode rather than left contradictory.
- **Every acceptance scenario in `spec.md` maps to an automated test** that runs on demand and passes
  before merge. Update `spec.md` before the PR. Refresh Repowise docs for PR delivery.
- **Keep each issue a vertical slice.** Don't let one grow into two.
