# Handoff: Board Member Experience (NekoHOA / HOA Management CRM)

## Overview

This bundle hands off the design for the **board member / community manager side** of the HOA
management product. Today the product serves exactly one persona — a homeowner looking at their own
property. This work introduces the second persona and, underneath it, the community-scoped
foundations that five further features depend on.

The design work is already reconciled against the real repository (`nbon12/hoa_management_system`,
branch `main`). The written specification lives at `specs/021-board-overall-design/spec.md` and is
included in this bundle.

---

## ⚠️ About the design files — read this first

**The files in this bundle are design references created in HTML. They are not production code.**

They were authored as React + JSX running through in-browser Babel, because that is the medium the
design tool draws in. **That is not a recommendation to use React.** The target codebase is:

| Layer | Actual stack |
| --- | --- |
| Frontend | **Angular** — `neko-hoa/src/app/**`, standalone components, `app.routes.ts`, `styles.scss` |
| Backend | **.NET / ASP.NET Core** — `Domain/Entities/*.cs`, FastEndpoints under `HOAManagementCompany/Features/` |
| Data | **EF Core + PostgreSQL (Neon)**, strict reversible migrations |
| Storage | **Cloudflare R2** (MinIO locally) |

**The task is to recreate these designs as Angular components inside the existing app**, using its
established patterns, its `styles.scss` tokens, and its existing shell. Do not port the JSX. Do not
introduce React, a React bridge, or a second component model into the Angular application. Read the
`.jsx` files the way you would read a Figma frame: for layout, hierarchy, copy, and behavior.

The one thing that *should* survive the translation literally is the **data-shape ideas** — the
single `METRICS` registry and the `boardNav()` derivation function. Those are architectural
decisions expressed in the wireframe, not styling. See "Two patterns that must survive" below.

---

## Fidelity

**Low-fidelity (lofi).** These are wireframes.

- Layout, information hierarchy, section order, table columns, and copy are **intentional** — follow them.
- Color, spacing, type scale, and component chrome are **placeholder** — the wireframe uses its own
  sketch palette (`wireframe-styles.css`). Apply the app's existing design system from
  `neko-hoa/src/styles.scss` instead. Spec FR-037: no new board palette is introduced; the board
  side reuses resident-side tokens.
- The one color decision that is **binding**: the board-mode banner is **dark ink on a violet fill**,
  not white on violet. White-on-violet measured 2.51:1 and fails WCAG 2.1 AA (FR-038).

---

## The dependency that shapes everything

Verified against `main`:

- **There is no `Community` entity.** `Property.CommunityId` is a denormalized `string`;
  `Property.CommunityName` is a second denormalized copy. `Violation.CommunityId` repeats the string.
- **There is no role model.** `ApplicationUser` has `FirstName`, `LastName`, `UserProperties`,
  `RefreshTokens` — nothing HOA-scoped.
- **Authorization is property-scoped only.** Every protected read walks `User → UserProperty →
  Property`. No primitive answers *"may this user see all properties in community X?"*
- **The Angular shell is single-mode.** `ShellComponent` renders a fixed `navGroups` array with no
  role input; `app.routes.ts` guards everything behind `authGuard` alone.

**So the first chunk of work is not UI.** It is four backend primitives: the `Community` entity, the
`CommunityMembership` relationship, one shared community-scope resolver, and the metric descriptor
contract. Build those before any board screen. `ISSUES.md` sequences this.

Why it can't be parallelized away: community scope is a **security primitive**. Board members see
other homeowners' delinquency and personal data. If five features each implement their own scope
check, they will diverge, and the failure mode is one association's board reading another
association's homeowner data. One resolver, used by everything (FR-012, SC-003).

---

## Two patterns that must survive translation

### 1. The metric registry (`METRICS` in `wf-board.jsx`)

Every metric surface — the hero statistics, the "Work Processed" table, the "Community Metrics"
table, and the help glossary — renders from **one array of descriptors**. Nothing is hand-positioned.

```js
{ id:'status', surface:'community', label:'Community Status', value:'Live', status:'ok',
  help:'Whether the association is actively managed. "Live" means billing, collections, and
        reporting are all running.' }
```

`surface` routes the row to a page section, `hero` promotes it to a summary card, `help` is the
glossary copy, `status` drives the status cell, `tone` drives emphasis.

The rule (FR-030, FR-031, SC-004): **adding or retiring a metric is a one-line data change** — no
layout, component, or template file may need editing. The glossary is derived from the same
descriptors as the rows, so a definition can never drift from its metric (FR-034).

In Angular this becomes a typed `MetricDescriptor[]` served by the backend and consumed by a
`MetricTableComponent` + `GlossaryPanelComponent`. Descriptors the user's role doesn't permit are
filtered server-side and never render (FR-035). A metric that fails to resolve renders an explicit
unavailable state without blanking its siblings (FR-036).

### 2. Navigation derived from data (`boardNav()` in `wf-board.jsx`)

```js
function boardNav({ role = 'board', communities = 1 }) {
  const g = [];
  if (communities > 1) g.push({ group: null, items: [{ label: 'My Communities' }] });
  ...
}
```

The shell accepts its navigation set **as data** so sibling features add sections without modifying
the shell (FR-024). "My Communities" is rendered **if and only if** the user holds more than one
active membership, regardless of role — one community means the item is not drawn at all and the
user lands directly on that community's home (FR-025, FR-026, SC-001).

---

## Screens

Six board artboards, in section `7 · Board & manager side` of the wireframe canvas.

### 1. Sign-in / board mode switch — `b-switch` (1280×800)

**Purpose**: the entry point to every board feature.

One login for all personas — no separate portal, URL, or credential set (FR-018). The **"Enter board
mode" control sits in the top bar**, inside the account-controls cluster, **to the left of the alerts
and avatar controls**. It never occupies space in the page body (FR-019).

- Rendered only for users holding at least one *active non-resident* membership (FR-020). Everyone
  else sees no trace of it anywhere in the app.
- In board mode, a visually distinct **violet banner** identifies the mode and states that
  association-wide data is shown (FR-021). The banner carries **no control** — the Resident/Board
  toggle lives in the top bar.
- Last-used mode persists across sessions (FR-022).
- When the last non-resident membership goes inactive, the next request serves resident mode and the
  control disappears (FR-023).

### 2. Community Home — `b-home` (1360×1860)

**Purpose**: the board member's landing page. One long scrolling page, not a set of sub-tabs.

Top to bottom: hero statistics (the `hero` metrics from the registry) → open votes → community photos
→ calendar → **Work Processed (last 30 days)** table → **Community Metrics** table.

Work Processed rows include *Assessment Payments Processed* (302), work orders, and violation
notices issued after inspection. Community Metrics rows include *Community Status* (Live), and the
delinquency and financial-health measures.

Community metrics were **merged into this page deliberately** — they are not a separate screen.

### 3. Community Home scrolled to metrics, glossary open — `b-metrics` (1440×880)

**Purpose**: shows the help affordance and glossary panel in their open state.

- The help affordance is the **right-most column of every metric row** (FR-032).
- Clicking it opens a **right-side glossary panel scrolled to that term**, with the targeted entry
  visually distinguished (FR-033).
- Rationale: board members are volunteers, not operators. An undefined metric is a metric that gets
  misread in a meeting — "over 60 days delinquent" needs to say what it actually counts.
- Accessibility: the panel must move focus to the targeted definition on open and return focus on
  close; fully keyboard operable with visible focus.

### 4. Community Overview — `b-overview` (1360×880)

**Purpose**: the community's record of itself.

Legal name, community name (the management company's human-readable handle), the community **GUID**,
county, formation date, management start date, description, status. Sub-associations appear here via
the parent/child relation (Keystone Crossing SF / TH under the Master).

### 5. Architectural Applications — ARC review

**Purpose**: board members review and vote on architectural change requests.

**Board voting is inline on the application row**, with vote tallies visible. Homeowner attachments
(plans, photos, surveys) are private documents — they must be served through the **short-lived
pre-signed URL primitive** (FR-039). Durable public object URLs are not acceptable for these; they
contain property details and homeowner personal information.

### 6. Vendor Management

**Purpose**: manager/accountant-facing vendor and invoice surface.

Gated to Community Manager and Accountant roles. A board member viewing the same sidebar does not
get these as working links (FR-003 of User Story 3 / FR-027).

---

## Interactions & behavior

- **Mode switch**: top-bar control → application enters board mode → violet banner appears, sidebar
  swaps to the board navigation set, landing route resolves. Reversible at any time; resident view
  must be intact on return.
- **Role gating**: navigation entries for capabilities the role does not confer are not offered as
  working links (FR-027). Direct navigation to a forbidden route is **refused and redirected to a
  permitted page** — never an empty screen (FR-028).
- **Glossary**: row help link → right panel opens positioned at that definition.
- **Authorization is always server-side.** The client's current mode is **never** an input to any
  authorization decision (FR-014). Frontend role checks exist *solely* to avoid presenting unusable
  controls. A stale mode in a second browser tab can never grant access.

### Edge cases the design accounts for

- A user holds different roles in different communities (board member at home, manager
  professionally). Role resolves **per community**, never globally.
- A board term expires mid-session — re-evaluate per request, don't trust a token minted while the
  term was active.
- A board member sells their home but their term hasn't ended. **Board membership is independent of
  property ownership**; losing `UserProperty` must not revoke board access, or vice versa.
- A board member of a sub-association only — scope must not silently widen to the master association.
- A community with no metrics configured → explicit empty state, not a zero-row table with headers.
- A single metric fails upstream → that row shows unavailable; the page still renders.
- A user's one community is inactive/offboarded → treated as holding zero board-accessible communities.

---

## State management

| State | Where it lives | Notes |
| --- | --- | --- |
| Current mode (resident/board) | Client + persisted per user | UX state only. **Never** an authorization input. Survives sign-out/sign-in (FR-022). |
| Active community | Route parameter (GUID) | Always re-resolved server-side against membership; treated as untrusted client input. |
| Memberships | Server, `CommunityMembership` | The sole source of board-side authorization. |
| Navigation set | Derived from role + membership count | Data-driven; see `boardNav()`. |
| Metric descriptors | Server-provided collection | Drives rows, hero stats, and glossary alike. |
| Glossary panel | Local UI state | Open/closed + targeted descriptor id. |

---

## Design tokens

**Do not use these.** They are the wireframe's sketch palette, listed only so you can recognize them
in the `.jsx` and map them to real tokens in `neko-hoa/src/styles.scss`.

```css
--paper: #fdfcfd;   --ink: #3a2f3f;     --ink-soft: #6b5a72;  --ink-mute: #9a8ea1;
--pink: oklch(0.94 0.03 350);   --pink-2: oklch(0.88 0.05 350);  --rose: oklch(0.78 0.10 350);
--lav:  oklch(0.92 0.04 300);   --lav-2: oklch(0.86 0.06 300);   --violet: oklch(0.72 0.10 300);
--line: #d9cfdc;    --line-soft: #ece4ee;
--warn: oklch(0.80 0.12 30);    --ok: oklch(0.78 0.10 160);
```

`--violet` is the board-mode banner fill; `--ink` is its text (see Fidelity, above). Status cells use
`--ok` for pass, `--warn` for the "watch" pill.

---

## Assets

None. The wireframes use no images, icons, or fonts beyond system defaults. Community photos on the
board home are represented as placeholder blocks — they are an existing resident-side feature
re-scoped to the community, not a new subsystem.

---

## Open questions — resolve before implementing the affected issue

Three items are marked `[NEEDS CLARIFICATION]` in the spec. They block specific issues, noted in
`ISSUES.md`:

1. **FR-040 — role-gated nav entries**: hidden entirely, or rendered disabled with a lock affordance?
   Hiding avoids support noise; a lock helps a board member understand what a manager does. *The
   wireframes currently show a lock.* Blocks the shell issue.
2. **FR-041 — committee member read scope**: same as board members, or narrower and limited to the
   committee's subject area? Blocks the membership/roles issue.
3. **FR-042 — who administers membership terms**: community managers in-product, or seeded/imported
   from the management company's system of record? **No admin surface is specified in this effort** —
   if the answer is "in-product," that is additional scope not covered by these designs.

---

## Files in this bundle

| File | What it is |
| --- | --- |
| `NekoHOA Wireframes (standalone).html` | **Start here.** Self-contained — open it in a browser, no server or dependencies. All wireframe sections; board work is section 7. |
| `NekoHOA Wireframes.html` | Source version; loads the `.jsx` files below. |
| `wf-board.jsx` | Board shell, `METRICS` registry, `boardNav()`, `GlossaryPanel`, `MetricTable`, `HeroStat`, `StatusCell`. **The most important file to read.** |
| `wf-board-screens.jsx` | The six board artboards. |
| `wf-shell.jsx`, `wf-auth.jsx` | Resident shell and sign-in, for context on existing patterns. |
| `wireframe-styles.css` | Sketch palette. Reference only — do not port. |
| `spec.md` | The full specification: 42 functional requirements, acceptance scenarios, constitution requirements, success criteria. **Authoritative where this README and it disagree.** |
| `ISSUES.md` | The work broken into GitHub issues, with dependencies and suggested sequencing. |
| `USER-STORIES-login.md` | Issue #4 (the Manager/Board login) split into **nine stories, one per Claude Code session** — each with scope, acceptance, a demo sentence, and a test command. |

---

## Quality gates (from the spec's constitution requirements)

The community-scope resolver, membership evaluation, and the migration are **95%-coverage-critical**.

- **Backend**: xUnit + Testcontainers against a seeded **two-community** dataset. Theory data must
  vary role × membership status × target community, and cover expired terms, sub-association
  boundaries, and role-mismatch denials. Tests parallel-safe, no dependence on prior run artifacts.
- **Frontend**: Jasmine/Karma for the mode service and nav derivation (**test the "more than one
  community" rule at 0, 1, and 2+**). Angular Testing Library for shell, metric table, glossary
  panel. Playwright for the mode-switch journey and role-gated route refusal. Cypress E2E for
  sign-in → board mode → community home. Storybook visual regression for shell, banner, metric table,
  hero statistics, glossary panel.
- **Migration**: reversible, idempotent, safe to run at Cloud Run startup, verified against a seeded
  multi-community dataset.
- Serilog records authorization denials. Sentry tags active community and role — **never** homeowner
  personal data.
- Every acceptance scenario in `spec.md` maps to an automated test that passes before merge.
- `spec.md` is updated before the PR.
