# Manager / Board login — user stories, one per Claude Code session

This file expands **Issue #4 (Mode switching)** from `ISSUES.md` into nine stories. Issue #4 as
written is a comfortable *feature* but a bad *session* — it mixes a backend read model, an Angular
top-bar control, persisted user preference, route resolution, term-expiry demotion, and a security
negative-test suite. That is five different files-of-mind. Claude Code does its best work when the
session has one.

**Prerequisites.** Stories S1–S9 assume `ISSUES.md` **#1 (Community entity)**, **#2
(CommunityMembership)**, and **#3 (community-scope resolver)** have merged. S1 is the first story
that can start after them. Do not start any story here before #3 is on `main` — every one of them
reads membership, and if membership resolution is still moving underneath you the session will spend
its context on merge conflicts instead of the story.

---

## What makes a story "one session"

Each story below is sized so a single Claude Code session can go start to end: read the relevant
code, implement, write the tests, run them green, and open a PR. The constraints I held each story to:

- **One branch, one PR, one merge.** No story leaves a half-built thing that another story must
  finish.
- **One layer of the stack, with one exception.** S2 and S4 touch the Angular shell; the rest are
  either backend-only or frontend-only. Crossing layers mid-session is what makes a session sprawl.
- **A demo sentence.** Every story ends with something you can *show* — a curl response, a control
  appearing, a redirect happening. If you can't demo it, it isn't a story, it's a refactor.
- **Under ~10 files touched.** Beyond that, a session starts losing the thread of its own plan.
- **No open decisions inside it.** Where a decision is unresolved, it is called out at the top of the
  story rather than buried — go get the answer before opening the session, not during it.
- **A single test command.** Stated per story, so the session knows what "done" sounds like.

Stories are in dependency order. S3 through S9 can each be picked up independently once S1 and S2
have merged.

### Suggested session opener

```
Read design/board-experience/README.md and USER-STORIES-login.md, then implement story S<n> only.

This is an Angular + .NET codebase. The .jsx files in that folder are design references drawn in
React — read them for layout, hierarchy, and copy. Do not port them; do not add React.

Stay inside S<n>'s scope. If you find work that belongs to another story, note it in the PR
description and leave it alone.
```

---

## S1 — A signed-in user can ask what memberships they hold

> **As** a signed-in user, **I want** the application to know which communities I belong to and in
> what role, **so that** every later decision about what I'm shown has one authoritative source.

**Layer**: backend only. **Depends on**: #3. **Blocks**: S2, S4.

### Why this is its own session

Everything else in the login reads this. If it's built inline inside the top-bar work, the rule about
what counts as an active membership ends up expressed in a component, and S5's term-expiry work will
have to go find it there.

### Scope

- One endpoint returning the current user's memberships: community GUID, community name, role, status.
- **Only active memberships.** A membership whose end date has passed, or whose status is not active,
  is absent from the response entirely — not present-with-a-flag. (FR-010)
- Resolved from **persisted membership**, server-side. No client input participates. (FR-014)
- Derives community name from the `Community` relation, not a stored copy. (FR-004)
- Includes a derived answer to *"do I hold at least one active non-resident membership?"* so S2 does
  not have to reimplement the predicate.
- Returns memberships in a **sub-association** as themselves — never widened to the parent.

### Out of scope

The mode control, anything visual, mode persistence, the My Communities list surface.

### Acceptance

- **Given** I hold an active Board Member membership in one community, **when** I call the endpoint,
  **then** I get that one membership with its role and community GUID.
- **Given** I hold Board Member in A and Community Manager in B, **when** I call it, **then** I get
  both, each with its own role, and no permission bleed between them.
- **Given** my only membership ended yesterday, **when** I call it, **then** it is absent and the
  non-resident predicate is false.
- **Given** my only membership is in an offboarded/inactive community, **when** I call it, **then** I
  am treated as holding zero board-accessible communities.
- **Given** I am resident-only, **when** I call it, **then** the non-resident predicate is false.

### Demo

`curl` the endpoint as each of four seeded users — board, manager with two communities, expired-term,
resident-only — and read the four different responses.

### Done when

Endpoint is a FastEndpoint, documented via Swashbuckle, UTC timestamps, existing response and error
shapes. xUnit + Testcontainers against the seeded two-community dataset, theory data varying role ×
status.

**Test command**: backend unit + integration suite.

---

## S2 — A board member enters and leaves board mode

> **As** a homeowner who also serves on my HOA board, **I want** an "Enter board mode" control
> alongside my usual account controls, **so that** I can see association-wide information without a
> second login.

**Layer**: Angular. **Depends on**: S1. **Blocks**: S3, S4.

### Why this is its own session

This is the entry point to every board feature — without it nothing else is reachable. It is also the
story most likely to grow: resist adding persistence (S3), the landing-route rule (S4), or the
sidebar's role gating (#5) into it. This session's job is the control, the state flip, and the banner.

### Scope

- **One login for all personas.** No separate portal, URL, or credential set. (FR-018)
- The control renders in the **top bar, inside the account-controls cluster, to the left of the alerts
  and avatar controls**, and must not occupy space in the page body. (FR-019)
- Rendered **only** for users whose S1 predicate is true. A resident-only user sees no trace of it
  anywhere in the application — not disabled, not hidden-but-present. (FR-020)
- Entering board mode: a **visually distinct violet banner** identifies the mode and states that
  association-wide data is shown. The banner carries **no control**. (FR-021)
- A Resident/Board toggle appears in the top bar while in board mode; leaving returns the resident
  view **intact**.
- Mode is client UX state in this story — it is **never** sent as or used as an authorization input.
  (FR-014)

### Out of scope

Persistence across sessions (S3). Landing route and My Communities (S4). Sidebar contents and role
gating (#5). Accessibility polish (S8) — though don't actively make S8 harder.

### Acceptance

- **Given** I hold an active non-resident membership, **when** I sign in, **then** the control appears
  in the correct top-bar slot.
- **Given** I hold no non-resident membership, **when** I sign in, **then** the control is rendered
  nowhere.
- **Given** I am in resident mode, **when** I enter board mode, **then** the banner appears and the
  Resident/Board toggle appears in the top bar.
- **Given** I am in board mode, **when** I return to resident mode, **then** the resident view is
  intact.

### Demo

Sign in as the board user, click the control, watch the banner appear. Sign in as the resident-only
user, confirm there is nothing to click.

### Done when

Banner is **dark ink on the violet fill** — white-on-violet measures 2.51:1 and fails AA. Use the
existing `styles.scss` tokens; introduce no board palette. (FR-037, FR-038)

**Test command**: Jasmine/Karma for the mode service; Angular Testing Library for the top bar.
Storybook entry for the banner.

---

## S3 — My mode is still my mode tomorrow

> **As** a community manager who lives in board mode all day, **I want** the application to remember
> which mode I was last in, **so that** signing in doesn't cost me a click every morning.

**Layer**: backend write + thin frontend read. **Depends on**: S2.

### Why this is its own session

It looks like two lines bolted onto S2, and it isn't: last-used mode is **persisted per user**, which
means a migration, an endpoint, and a decision about what happens when the persisted mode is no longer
permitted. That last part is the whole reason to separate it — the interesting case is a persisted
"board" for a user who has since lost their membership, and it deserves its own tests rather than
being an afterthought in a UI session.

### Scope

- Last-used mode persists across sessions. (FR-022)
- On sign-in, mode is restored **only if still permitted** — a persisted board mode for a user whose
  S1 predicate is now false resolves to resident mode, silently and without error.
- Mode is a **preference**, not a grant. Persisting "board" must never widen what the server allows.

### Acceptance

- **Given** I am in board mode, **when** I sign out and sign in again, **then** I return to board mode.
- **Given** I am in resident mode, **when** I sign out and sign in again, **then** I return to resident
  mode.
- **Given** my persisted mode is board but my last non-resident membership has gone inactive, **when**
  I sign in, **then** I land in resident mode with no error surfaced.

### Demo

Enter board mode, sign out, sign back in, land in board mode. Then expire the seeded membership and
sign in again — land in resident mode.

### Done when

Migration is reversible and idempotent. Mode persistence is proven not to affect any authorization
decision (one test asserting exactly that).

---

## S4 — I land where I should: one community, or a portfolio

> **As** a board member with one community, **I want** to land directly on that community's home;
> **as** a manager with a portfolio, **I want** to land on my list of communities.

**Layer**: Angular routing + shell nav derivation. **Depends on**: S1, S2.

### Why this is its own session

This is the story with a real rule in it, and the rule has three branches (0, 1, 2+) that are easy to
get subtly wrong. It deserves a session where the tests for those three branches are the main event.
It's also the only place in the login where **manager and board diverge** — worth isolating for that
reason alone.

### Scope

- **"My Communities" renders if and only if the user holds more than one active community membership**,
  independent of role. One community means the nav item **is not drawn at all**. (FR-025)
- A user holding exactly one active membership lands **directly on that community's home** — no
  intermediate list to traverse. (FR-026, SC-001)
- Two or more memberships of any role → the My Communities list is rendered and is the landing route.
- The list is the effort's **only intentional cross-community surface**: it returns only communities
  where the caller holds an active membership, and exposes **summary fields only**. (FR-013)
- The shell **accepts its navigation set as data** so sibling features add sections without editing the
  shell. (FR-024) Reference `boardNav()` in `wf-board.jsx` — that function is the shape to translate,
  not the code to copy.
- Zero active memberships → the board route is unreachable and the user stays a resident.

### Acceptance

- **Given** exactly one active membership, **when** I enter board mode, **then** no My Communities item
  is rendered and I land on that community's home.
- **Given** two or more active memberships of any role, **when** I enter board mode, **then** My
  Communities is rendered and I land on that list.
- **Given** a portfolio, **when** I open the list, **then** it shows only my communities, summary
  fields only.

### Demo

Sign in as the one-community board member → straight to community home. Sign in as the two-community
manager → the list.

### Done when

Nav derivation is unit-tested at **0, 1, and 2+** explicitly. Cypress E2E covers sign-in → board mode
→ community home for the single-community case.

### Note

The community home page itself is `ISSUES.md` **#8** — this story routes to it and can land against a
placeholder page.

---

## S5 — When my term ends, my access ends

> **As** the association, **I want** a board member's access to lapse when their term ends, **so that**
> nobody keeps association-wide visibility after they stop serving.

**Layer**: backend, with one frontend consequence. **Depends on**: S1, S2.

### Why this is its own session

The naive implementation checks membership at sign-in and mints a token. That passes every happy-path
test and is wrong: a term that expires **mid-session** must be caught on the **next request**, not
trusted from a token minted while the term was active. Getting that right means touching request-time
evaluation, and it wants a session focused on exactly that.

### Scope

- Authorization is evaluated **per request, from persisted membership** — never from a token minted
  earlier. (FR-014)
- When a user's last non-resident membership becomes inactive, the application returns them to resident
  mode on their **next request** and stops rendering the mode control. (FR-023)
- No administrator action is required for the lapse to take effect. (SC-005)
- **Board membership is independent of property ownership** — a board member who sells their home
  keeps their term; losing `UserProperty` must not revoke board access, and vice versa. (FR-011)

### Acceptance

- **Given** I am in board mode, **when** my membership's end date passes, **then** my next request is
  served in resident mode and the control is no longer rendered.
- **Given** my term ends, **when** no administrator does anything, **then** my board access is gone.
- **Given** I sell my home but my term has not ended, **when** I sign in, **then** I still have board
  access.
- **Given** my term ends but I still own my home, **when** I sign in, **then** my resident access is
  untouched.

### Demo

With the app open in board mode, expire the membership in the database, refresh — land in resident
mode.

### Done when

Tests cover expiry **mid-session**, not just at sign-in. Theory data varies role × membership status.

---

## S6 — Client mode can never grant access

> **As** the association, **I want** authorization decided entirely server-side, **so that** nothing a
> browser says can widen what a user sees.

**Layer**: backend, test-heavy. **Depends on**: S1–S5 as they land; can start after S2.

### Why this is its own session

This is the security boundary of the login, and it is almost entirely **negative** testing — proving
things *don't* work. That's a different mode of work from building a control, and mixing the two means
the negative cases get written last, tired, and thin. Board members see other homeowners' delinquency
data; this session is the one that proves the boundary holds.

### Scope

- The client's current mode is **not an input to any authorization decision**. (FR-014)
- Resident-scoped endpoints **retain** "own properties only" semantics and must not widen because the
  user also holds a board membership. (FR-015)
- A resident-only user crafting a request for a board-only resource is denied.
- A board member of community A substituting community B's identifier is **denied server-side
  regardless of what the interface offered**, and the response must **not disclose whether B exists**.
  (FR-016)
- A **stale mode in a second browser tab** can never grant access.
- Scope must not widen from a **sub-association to its master**.
- All community identifiers from the client are treated as untrusted and **re-resolved** against
  membership.
- Every board-scoped path added by S1–S5 resolves through the **shared resolver** from #3 — none
  implements its own check. (FR-012, SC-003)

### Acceptance

Each bullet above is a test. Additionally:

- **Given** any board-scoped endpoint reachable after S1–S5, **when** called with an out-of-scope
  community identifier, **then** it denies. Verified by a test that attempts **every** such endpoint,
  not a sample. (SC-002)
- **Given** static analysis over the board-side endpoints, **when** it runs, **then** no endpoint
  implements its own scope check. (SC-003)

### Demo

The test report. This story's deliverable is confidence, and the artifact is a green negative suite.

### Done when

Serilog records authorization denials. Denials fail closed. Parallel-safe tests with no dependence on
prior run artifacts.

---

## S7 — A route I may not use refuses me politely

> **As** a board member who pasted a manager's URL, **I want** to be redirected somewhere I can
> actually use, **so that** I don't stare at a broken empty screen.

**Layer**: Angular routing guards. **Depends on**: S4.
**⚠️ Decision needed first**: FR-040 — see below.

### Why this is its own session

`app.routes.ts` currently guards everything behind `authGuard` alone. Adding role-aware route
resolution is a self-contained change to the routing layer with its own failure mode — the redirect
target — and it pairs naturally with the nav-rendering decision.

### Scope

- Navigation entries for capabilities the role does not confer are **not offered as working links**.
  Finance sections restricted to managers and accountants are not usable by a board member; the same
  sections are present and usable for a community manager in the same community. (FR-027)
- Direct navigation to a route the role cannot use is **refused and redirected to a permitted page** —
  never an empty screen. (FR-028)
- Frontend role checks exist **solely** to avoid presenting unusable controls. They are not a security
  boundary; S6 owns that.

### ⚠️ Decision needed before opening the session

**FR-040**: are role-gated nav entries **hidden entirely**, or **rendered disabled with a lock
affordance**? Hiding avoids support noise; a lock helps a board member understand what a manager does.
**The wireframes currently show a lock** — default to that if no other answer arrives. This changes the
markup, so settle it first.

### Acceptance

- **Given** I am a board member, **when** I view the sidebar, **then** manager/accountant finance
  sections are not working links.
- **Given** I am a community manager in the same community, **when** I view the sidebar, **then** those
  sections are present and usable.
- **Given** I navigate directly to a route my role cannot use, **when** it resolves, **then** I am
  refused and redirected to a permitted page.

### Demo

Sign in as the board member, paste the manager route, get redirected.

### Done when

Playwright covers role-gated route refusal. Jasmine/Karma covers the guard logic.

---

## S8 — The login works without a mouse, and reads at AA

> **As** a board member using a keyboard and a screen reader, **I want** the mode control, banner, and
> toggle to be fully operable and legible, **so that** the board side is usable by every board member.

**Layer**: Angular, polish. **Depends on**: S2, S4.

### Why this is its own session

Accessibility folded into a feature session gets the last twenty minutes. Given its own session it
gets tests. It also has a **known, measured** defect to prevent rather than a vague aspiration, which
makes it concrete work: white-on-violet measures **2.51:1**.

### Scope

- The mode toggle and sidebar are **fully keyboard operable with visible focus**.
- All text and interactive controls introduced by the login meet **WCAG 2.1 AA contrast**, explicitly
  including the board mode banner. (FR-038)
- The banner is **dark ink on the violet fill**, matching the house pattern.
- Board side uses **existing design tokens**; no separate board palette. (FR-037)

### Out of scope

The glossary panel's focus management — that belongs to `ISSUES.md` #6, which owns the panel.

### Acceptance

- **Given** a keyboard alone, **when** I traverse the top bar, **then** I can enter and leave board
  mode with visible focus throughout.
- **Given** the banner, **when** contrast is measured, **then** it passes AA.
- **Given** every control this login introduced, **when** audited, **then** all pass AA and all are
  reachable by keyboard alone. (SC-008)

### Done when

Storybook visual regression entries for the shell and banner. Contrast values recorded in the PR.

---

## S9 — Board access to association-wide data leaves a trail

> **As** the association, **I want** a record of who looked at association-wide homeowner data, **so
> that** access is accountable after the fact.

**Layer**: backend + observability config. **Depends on**: S1, S4.

### Why this is its own session

Audit and observability are cross-cutting, which is exactly why they get skipped inside feature
sessions. One session, applied to the surfaces the login exposes, with an explicit rule about what must
never be logged.

### Scope

- An audit entry when a user accesses association-wide personal or financial data: **actor, community,
  resource, UTC timestamp**. (FR-017)
- **Membership grants and revocations are logged as sensitive events.**
- Sentry captures errors across **both modes**, with **active community and role attached as tags** —
  and **never homeowner personal data**.
- Trace context propagates from the Angular shell through board endpoints. Environment and release tags
  unchanged.

### Acceptance

- **Given** I view association-wide homeowner data, **when** the request completes, **then** an audit
  entry records actor, community, resource, and UTC timestamp.
- **Given** any audit or Sentry payload, **when** inspected, **then** it contains no homeowner personal
  data.
- **Given** an error in board mode, **when** it reaches Sentry, **then** the active community and role
  are present as tags.

### Demo

Trigger a board-mode read, show the audit row. Show a Sentry event carrying role and community and no
personal data.

---

## Suggested order

| Session | Story | Layer | Can run parallel with |
| --- | --- | --- | --- |
| 1 | S1 Membership read model | backend | — |
| 2 | S2 Enter / leave board mode | Angular | — |
| 3 | S4 Landing route: 0 / 1 / 2+ | Angular | S3, S5 |
| 4 | S3 Mode persistence | backend + thin FE | S4, S5 |
| 5 | S5 Term expiry demotes | backend | S3, S4 |
| 6 | S6 Client mode is not authorization | backend tests | S7, S8 |
| 7 | S7 Route refusal | Angular | S6, S8 |
| 8 | S8 Accessibility + contrast | Angular | S6, S7 |
| 9 | S9 Audit + observability | backend | S7, S8 |

S1 and S2 are the spine — everything else attaches to them. After session 2 you have a demoable
slice: a board member signs in, enters board mode, and sees the banner.

**Ship S6 before the board side reaches anyone real.** S1–S5 produce a working login; S6 is what makes
it safe to point at production data.
