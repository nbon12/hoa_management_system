# Spec 030 design conventions (shared by briefs 01–12)

These rules apply to every Claude Design brief for spec 030, Company Administration. Attach this file with each brief. If this file and [`spec.md`](../spec.md) disagree, `spec.md` wins.

## 1. How these briefs are used

The owner opens the existing **"HOA Management CRM"** project in Claude Design (claude.ai/design). Its canvas, `NekoHOA Wireframes.html`, already holds sections 1–7, so those files are not uploaded again. For each brief, the owner attaches three files: `specs/030-company-administration/spec.md`, this file, and the brief. Then the owner pastes the brief's prompt. Every new artboard goes in one new canvas section, **"8 · Company administration"**, placed after section 7. The briefs are independent and can be run in any order. When the work is done, the owner exports the handoff bundle from Claude Design and saves it under `specs/030-company-administration/design/`, for example in `design/handoff/`, keeping the exported `README.md`, `chats/` and `project/` folders as they are (spec 006 kept its bundle the same way in `specs/006-stripe-payments/design/`). The `[no WFb]` tags in `spec.md` are then replaced with citations to artboard labels, for example `[design: 8.06.a]`.

## 2. Fidelity rules

The designs are **lofi**, with the same rules as the spec 025 board wireframes (`wireframes/HOA Management CRM v1.0.1/design_handoff_board_experience/README.md`).

**Binding.** The coding agent builds these as drawn:

- layout, information hierarchy, section order and table columns;
- copy: labels, buttons, helper text, empty states, and the refusal messages in §6;
- states: every state a brief lists is drawn;
- who sees what: which controls a Company Administrator sees and which a Community Manager sees;
- one colour pairing: the board-mode banner is dark ink on the violet fill (spec 025 FR-021, FR-038).

**Placeholder.** These are mapped later to `neko-hoa/src/styles.scss`:

- colour, spacing, type scale, borders, radii, shadows, icons and emoji;
- the sketch palette in `wireframe-styles.css`. `styles.scss` uses the same variable names (`--ink`, `--lav`, `--violet`, `--ok`, `--warn`, …). Keep using these variables and add no new colour values. There is no new palette (spec 025 FR-037).

**Other rules.**

- Designs describe screens, not code. The product is built in Angular; JSX is only the drawing medium. Do not name components, routes or endpoints in on-screen copy. Notes may cite FR numbers.
- Do not invent behaviour. Draw only what `spec.md` states. When a screen needs a decision the spec does not make, draw the option or options, label each **"Design choice"**, and put the open question on a `DCPostIt`.
- Keep sections 1–7 unchanged. Put section 8 in new files (for example `wf-company.jsx` for shared data and frames, plus one screens file per brief or per group of briefs) and add them to `NekoHOA Wireframes.html`. Prefer new props and new data over edits to shared components. If a shared component must change (for example `boardNav()` gaining items), sections 1–7 must still render as before. Say so in a note.
- Shared components hard-code sample content. In section 8, pass the §4 data in instead: the `WFShellBoard` strip ("Keystone Crossing (Master)", "KEYCROSS"), the `WFShell` and `WFShellSide` strip ("Sakura Heights · 714 Keystone Park Dr"), and the avatar initials "NB".
- Use only the people in §4. Do not reuse person names from sections 1–7.
- **Desktop-first.** Staff pages (briefs 01–09) are drawn at desktop width only; mobile layout is out of scope for staff pages (spec Assumptions). Brief 10's invitation acceptance and sign-up pages are drawn at desktop **and** phone width, because invitees open them from email. Brief 11's resident pages are drawn at desktop width, like sections 2–6. Brief 12's emails are one column, so they read on a phone.

**Reuse these by name.** New screens extend the existing wireframes. They do not restyle them.

| Reuse | Defined in | Use it for |
| --- | --- | --- |
| `DesignCanvas`, `DCSection`, `DCArtboard` | `design-canvas.jsx` | Section 8 and its artboards. |
| `DCPostIt` | `design-canvas.jsx` | Open questions and "Design choice" notes on the canvas. |
| `.wf-note` (Caveat hand-written note, optional `.arrow`) | `wireframe-styles.css` | Short notes next to an element. The annotations tweak hides them, so a rule must never live only in a note. |
| `WFShellBoard` with `boardNav()` | `wf-board.jsx` | Every Community Manager screen (board mode). Keep the violet banner (`.wfb-banner`). Add nav entries ("Community settings", "Add community") as data in `boardNav()`; do not fork the shell. Board-mode nav keeps spec 025 FR-040: entries the role does not confer are shown disabled with a lock (`.wfb-side-item.locked`, 🔒). |
| The account cluster (`.wf-user`) and mode control (`.wfb-mode`); `BoardModeSwitch` | `wf-board.jsx`, `wf-board-screens.jsx` | Where the company entry and the board-mode control sit in the top bar (brief 01). |
| `BoardNavMulti` | `wf-board-screens.jsx` | "My Communities" and the community switcher, with the "Onboarding" chip added (brief 01). |
| `BoardOverview` | `wf-board-screens.jsx` | The Profile section's read view: the field grid (legal name, community name, county, formation date, management start date, description) and the sub-associations list. |
| `BoardArchApps` | `wf-board-screens.jsx` | The list-page pattern: filter buttons with counts ("Open · 4", "Closed · 27"), a search field, a table in a card, and detail cards below. Use it for the portfolio, the Residents area and invitation lists. |
| `BoardVendors` | `wf-board-screens.jsx` | A table card with a header bar and actions, and `wfb-hero` count tiles. |
| `HeroStat` (`.wfb-hero`) | `wf-board.jsx` | Small count tiles, only if a brief asks for them. |
| `GlossaryPanel` (`.wfb-glossary`) | `wf-board.jsx` | The right-side panel pattern: focus moves in on open and back on close. Use it for the optional property drawer (brief 06). |
| `WFShell`, `WFShellSide` | `wf-shell.jsx` | Resident screens (brief 11). Resident visual language, no board banner. |
| `LoginA`, `LoginB` | `wf-auth.jsx` | "Sign in to accept" (brief 10). `LoginB`'s single column suits phone width. |
| `Registration` | `wf-auth.jsx` | The invitation landing and create-account pages (brief 10): the header bar, the step pills, and the lav "Property found" card with its `wf-pill ok` badge and property pills. |
| CSS primitives | `wireframe-styles.css` | `wf-card` (and `lav`, `pink`, `dashed`), `wf-table` (and `.num`), `wf-btn` (and `primary`, `violet`, `ghost`), `wf-pill` (and `ok`, `warn`), `wf-field` (and `dashed`), `wf-field-label`, `wf-check`, `wf-radio`, `wf-toggle`, `wf-divider`, `wf-ph`, `wf-strip`, `wf-side`, `wf-side-item`, `wfb-side-child`, `wf-h1` (with its `.hand` accent word), `wf-h2`, `wf-sub`. |

**Do not reuse these for section 8:**

- `StatusCell` and the ✓ "Status" column in `BoardNavMulti`. They show status as a glyph or colour only. Section 8 shows status as text chips (§5).
- The lock on nav entries, outside board mode. Company-area pages are not shown at all to people who cannot use them (FR-035, FR-036).

## 3. Artboard naming

Every artboard label has the form **`8.<NN>.<letter> <short name>`**.

- `NN` is the two-digit brief number, 01–12 (§9).
- `letter` is a, b, c, … in the order the brief lists its artboards. Every variant gets its own letter: each viewpoint, state, option and phone version.
- `short name` is the screen, then the state, then the viewpoint, separated by " · ". The viewpoint uses the role name: "Company Administrator" or "Community Manager".
- Alternatives say "Option A", "Option B". Phone versions end with "· phone".

Examples:

- `8.01.a Company sign-in · bookmarkable page · Company Administrator`
- `8.02.a Portfolio · first page · Company Administrator`
- `8.04.f Archive dialog · Old Mill Commons`
- `8.06.a Residents · first page · Community Manager`
- `8.10.c Invitation landing · resident · phone`
- `8.12.e Email · no longer managed · autopay ended`

The section is `<DCSection id="company-admin" title="8 · Company administration" subtitle="Company Administrators and Community Managers: communities, managers, residents, settings">`. Artboard ids are `s8-<NN><letter>`, for example `s8-06a`. Ids must be unique on the canvas.

Once handed off, artboards are not renumbered. A new artboard takes the next free letter in its brief. A dropped artboard's letter is not reused.

| Kind of artboard | Width × height |
| --- | --- |
| Staff page (board mode or company area) | 1360 × as tall as the content |
| Staff page with a side panel open | 1440 × as tall as the content |
| Dialog | Drawn over the dimmed page it belongs to, at that page's size |
| Public page, desktop (brief 10) | 1100 × 780, as in section 1 |
| Public page, phone (brief 10) | 390 × full content height |
| Email (brief 12) | 640 × as tall as the content |
| Flow diagram (brief 01) | 1100 × as needed |

## 4. Personas and sample data

Every brief uses this one dataset. Use the same names, emails, dates and counts everywhere, so artboards from different briefs agree.

- All emails are at example.com. All people are made up. Do not use any real person's name.
- Filler rows (to fill a page of 25) may use other made-up names, emails at example.com, and addresses on the same streets. Filler community names must not contain "Keystone", so the search counts below stay true.
- The spec's example people are used where the spec names them. The spec reuses some names in unrelated scenarios; here each name has one story.

**"Now" and time zone.** Every artboard shows the app on **Monday, Oct 12, 2026, 10:00 AM EDT**. Every community's time zone (its architectural review time zone setting) is America/New_York, shown as "EDT". The company default time zone is also America/New_York.

### 4.1 The management company

**Juniper Row Community Management** (fictional). It is the one management company in this deployment. It manages 31 communities.

Company defaults: spec 027's initial values, not changed yet (FR-025).

| Setting (labels as in today's ARC settings page) | Value |
| --- | --- |
| Review period (days) | 30 |
| When the review period ends without a decision | Flag as overdue only |
| How a decision is reached | Majority of board members |
| Reminder to the board (days before due, 0 = off) | 7 |
| Time zone | America/New_York |
| Formal disapproval statement | "This is a formal disapproval of the plans as submitted under the community's governing documents. You may revise and resubmit." |

### 4.2 People

| Person | Email | Roles and homes | Briefs |
| --- | --- | --- | --- |
| **Robin Hale** | owner@example.com | **Company Administrator.** Became the first one through the setup step on Oct 1, 2026 ("Granted by: Setup step"). No community membership. No home. Signs in to the portfolio. Sees no board-mode control. The main Company Administrator viewpoint. | 01–09, 12 |
| **Casey Novak** | casey@example.com | **Company Administrator**, granted by Robin Hale on Oct 2, 2026. Also a Board Member of Keystone Crossing Master, granted by Jordan Blake. No home. Sees both the company entry and the board-mode control. Signs in to board mode. | 01, 09, 12 |
| ops@example.com | ops@example.com | Pending Company Administrator invitation from Robin Hale. No name is known. | 09, 10, 12 |
| **Jordan Blake** | jordan@example.com | **Community Manager**, open-ended, of Keystone Crossing Master, Keystone Crossing SF, Keystone Crossing TH, Sakura Heights and Keystone Ridge (Jordan added Keystone Ridge). No home. Signs in to board mode. The main Community Manager viewpoint. | 01, 03–08, 12 |
| **Taylor Grant** | taylor@example.com | Community Manager of Sakura Heights, ends Dec 31, 2026. | 05, 06 |
| **Morgan Ellis** | morgan@example.com | Community Manager, open-ended, of Old Mill Commons and Old Mill Commons Villas (both Archived). Has left the company. | 04, 05 |
| **Reese Walker** | reese@example.com | Former Community Manager of Briar Hollow. The term ended Aug 31, 2026, while Briar Hollow was archived. | 02, 04 |
| **Pat Rivera** | pat@example.com | No account yet. Pending Community Manager invitation to Keystone Park from Robin Hale. The name "Pat Rivera" appears only after Pat accepts and creates an account. Then Pat lands in board mode for Keystone Park. | 02, 04, 05, 10, 12 |
| **Lee Morales** | lee@example.com | Board Member of Sakura Heights, term ends Dec 31, 2027. Is in Sakura Heights' people list, so Lee can be appointed co-manager directly, or picked in "Replace manager". | 05 |
| **Jane Alvarez** | jane@example.com | Board Member of Sakura Heights, term ends Dec 31, 2026. Resident of 711 Keystone Park Dr #29 since Mar 2, 2024 (joined with a claim code). Enrolled #29's autopay. #29 is Jane's only home. | 05, 06, 11, 12 |
| Chris Ward | chris@example.com | Board Member of Sakura Heights, term ends Dec 31, 2027. | 05 |
| **Dana Fox** | dana@example.com | Former Board Member of Sakura Heights, ended Jun 30, 2026. No home. Signs in to "No home linked to this account". | 05, 11 |
| Rowan Hayes, Jesse Lin, Skyler Diaz | rowan@, jesse@, skyler@example.com | Board Members of Old Mill Commons (Archived). | 04 |
| **Avery Quinn** | avery@example.com | Accountant of Keystone Crossing Master, Sakura Heights and Old Mill Commons. | 04, 05 |
| **Sam Ortiz** | sam@example.com | Resident of three homes: 711 Keystone Park Dr #29 (Sakura Heights, Active; joined Oct 9, 2026 by signing in and accepting an invitation), 16 Old Mill Ln (Old Mill Commons, Archived, shown as "No longer managed"; it had autopay), and 9 Briar Hollow Ct (Briar Hollow, Onboarding, left out of the home list). Did not enroll #29's autopay. Signs in to #29. | 06, 11, 12 |
| **Kim Tran** | kim@example.com | No account. Pending resident invitation to 711 Keystone Park Dr #29 from Jordan Blake. | 06, 12 |
| **Ann Brooks** | ann@example.com | Resident of 4 Briar Hollow Ct (Onboarding) only. No other role. Signs in to "Your community isn't live yet". Has a pending Board Member invitation to Briar Hollow from Robin Hale. | 10, 11 |
| **Bob Brooks** | bob@example.com | Also a resident of 4 Briar Hollow Ct. Is signed in on the shared laptop when he opens Ann's invitation link, so he sees `INVITATION_EMAIL_MISMATCH`. | 10 |

Other invitees appear only as emails (§4.5).

**Where each person lands after signing in** (FR-036a; brief 01's flow and brief 11):

| Person | Lands on |
| --- | --- |
| Robin Hale | The company area (portfolio) |
| Casey Novak | Board mode, Keystone Crossing Master |
| Jordan Blake | Board mode |
| Pat Rivera, after accepting | Board mode, Keystone Park |
| Sam Ortiz | Resident home 711 Keystone Park Dr #29 (an Active community comes first) |
| Jane Alvarez, after being removed from #29 | Board mode, Sakura Heights |
| Ann Brooks | "Your community isn't live yet" |
| Dana Fox | "No home linked to this account" |

### 4.3 Communities

| Community name | Legal name | Status | Parent | Managers | Properties | ARC settings | Used for |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Keystone Crossing Master** | Keystone Crossing Owners Association, Inc. | Active | — | Jordan Blake (open-ended) | Not used in section 8 | Company defaults | `OPEN_WORK` on archive. County Durham County, NC; formed Mar 14, 2005; managed since Jan 1, 2019 (as in section 7). |
| **Keystone Crossing SF** | Keystone Crossing SF Association, Inc. | Active | Keystone Crossing Master | Jordan Blake (open-ended) | 214 | **Differs from defaults:** review period 45 days, reminder 10 days | The "Differs from defaults" flag, per-field indicators and the reset dialog. |
| **Keystone Crossing TH** | *(empty: spec 025 backfill)* | Active | Keystone Crossing Master | Jordan Blake (open-ended) | 168 | Never saved, so it reads spec 027's built-in values. These equal today's company defaults. | Profile flags legal name, county, formation date and management start date as missing (US3 #19). |
| **Sakura Heights** | Sakura Heights Homeowners Association, Inc. | Active | — | Jordan Blake (open-ended); Taylor Grant (ends Dec 31, 2026) | **120**, 7 with no resident | Company defaults | The main community for Managers and memberships (05) and Residents (06). The resident community of sections 2–6. County Wake County, NC; formed Jun 9, 1998; managed since Jul 1, 2016. |
| **Keystone Park** | Keystone Park HOA, Inc. | Onboarding | — | None. pat@example.com · invitation pending | 0 (not loaded yet) | Company defaults, copied Oct 11, 2026 | Added by Robin Hale on Oct 11, 2026. County Hamilton; formed Mar 14, 2004; management starts Nov 1, 2026 (US1 #1). Portfolio "No manager" with "invitation pending". Residents empty state. Mark Active is refused. |
| **Keystone Ridge** | Keystone Ridge HOA, Inc. | Onboarding | — | Jordan Blake (open-ended; first manager because Jordan added it) | 64, loaded by the engineer seed; no residents yet | Company defaults, copied Oct 9, 2026 | Added by Jordan Blake on Oct 9, 2026; every Company Administrator was emailed. Waiting to be marked Active. Has 2 scheduled resident invitations. County Durham County, NC; formed Jun 2, 2011; management starts Dec 1, 2026. |
| **Briar Hollow** | Briar Hollow Homeowners Association, Inc. | Onboarding | — | None. Reese Walker's term ended Aug 31, 2026 | 48, 41 with a resident | Company defaults | Was Active, archived Jul 15, 2026, restored Oct 5, 2026. Restore returned it to Onboarding because it had no open-ended manager (US3 #14). Portfolio "No manager". Mark Active is refused. Ann's and Sam's Onboarding homes. County Wake County, NC. |
| **Old Mill Commons** | Old Mill Commons Association, Inc. | Archived (Sep 30, 2026, by Robin Hale) | — | Morgan Ellis (open-ended; confers nothing while archived) | 86, 79 residents linked | Company defaults | Archived read-only views. The archive dialog, drawn as it looked before Sep 30. The restore dialog. Sam's "No longer managed" home. County Wake County, NC. |
| **Old Mill Commons Villas** | Old Mill Commons Villas Association, Inc. | Archived (Sep 29, 2026, by Robin Hale) | Old Mill Commons | Morgan Ellis (open-ended) | 24 | Company defaults | Restore is refused with `PARENT_ARCHIVED`. |

The other **22 communities** are Active. Each has one open-ended manager and company-default ARC settings. They only fill the portfolio.

### 4.4 Numbers to use

**Portfolio**

- 31 communities: 26 Active, 3 Onboarding, 2 Archived. The first page reads "Showing 1–25 of 31".
- Filter Archived: 2 rows. Filter Onboarding: 3 rows.
- Search "Keystone": 5 rows (Keystone Crossing Master, Keystone Crossing SF, Keystone Crossing TH, Keystone Park, Keystone Ridge).
- "No manager": 2 rows (Keystone Park, which also shows "pat@example.com · invitation pending"; and Briar Hollow).
- "Differs from defaults": 1 row (Keystone Crossing SF).

**Sakura Heights Residents area**

- 120 properties. The first page reads "Showing 1–25 of 120".
- Filter "No resident": 7 properties. Two of them have invitations: 719 Keystone Park Dr #35 (Pending, noor@example.com) and 715 Keystone Park Dr #31 (Expired, max@example.com).
- Filter "Invitation pending": 2 properties (#29 for Kim, #35 for noor@example.com).
- Filter "Invitation expired": 1 property (#31).
- Search "keystone park dr #29": exactly 711 Keystone Park Dr #29.

**711 Keystone Park Dr #29**

- Residents: Jane Alvarez (resident since Mar 2, 2024) and Sam Ortiz (resident since Oct 9, 2026).
- Active autopay, enrolled by Jane.
- Pending invitation: kim@example.com, sent by Jordan Blake.
- **Remove Jane:** #29's autopay is cancelled ("resident removed"). Jane is emailed that she was removed and that autopay ended. Sam is emailed that autopay ended. Jane keeps her Board Member membership and signs in to board mode. The owner of record and the balance don't change.
- **Remove Sam:** the autopay stays, because Jane enrolled it. Sam is emailed that he was removed. His next request moves him to 16 Old Mill Ln.

**Status dialogs**

- **Mark Keystone Ridge Active:** 2 scheduled resident invitations are emailed (lena@example.com for 2 Ridgeview Ct, omar@example.com for 8 Ridgeview Ct), each valid for 72 hours from then.
- **Mark Keystone Park or Briar Hollow Active:** refused with `NO_ACTIVE_MANAGER`.
- **Archive Keystone Crossing Master:** refused with `OPEN_WORK`, listing ARC-1041 (Open), ARC-1042 (Decision Reached), Keystone Crossing SF (sub-association, Active) and Keystone Crossing TH (sub-association, Active).
- **Archive Old Mill Commons** (as on Sep 30, before archiving; Old Mill Commons Villas was already Archived, so there was no open work):
  - 3 autopay schedules are cancelled;
  - 79 residents are emailed;
  - 2 invitations are revoked (drew@example.com, Community Manager; rae@example.com, resident of 22 Old Mill Ln);
  - 5 memberships are kept but confer nothing: Morgan Ellis (Community Manager), Rowan Hayes, Jesse Lin and Skyler Diaz (Board Members), Avery Quinn (Accountant);
  - it leaves every community switcher and "My Communities";
  - everything becomes read-only; nothing is deleted.
- **Restore Old Mill Commons:** the 5 memberships above will confer access again; each can be ended first. It returns to **Active**, because it was Active before it was archived and Morgan Ellis is an open-ended manager. If Morgan's membership is ended first, it returns to **Onboarding**. Autopay does not restart. Revoked invitations stay revoked.
- **Restore Old Mill Commons Villas:** refused with `PARENT_ARCHIVED`, naming Old Mill Commons.

**Managers (Sakura Heights)**

- Jordan Blake is the only open-ended manager. Ending, downgrading or end-dating Jordan is refused with `LAST_MANAGER`.
- Taylor Grant's end date is allowed, because Jordan is open-ended.
- Replace manager (Company Administrator only): replace Jordan Blake with Lee Morales.

**Architectural review (Keystone Crossing SF)**

- Reset to company defaults changes review period 45 → 30 days and reminder 10 → 7 days. Every other field is unchanged. Applications already received keep their rules. Application numbering doesn't change.

**Company administrators**

- 2 current (Robin Hale, Casey Novak) and 1 pending invitation (ops@example.com).
- `LAST_COMPANY_ADMIN` is shown when Robin Hale tries to revoke Robin's own role after Casey Novak's role was revoked.
- Recovery-mode example (brief 12): the deployer runs recovery mode for backup@example.com on Oct 12, 2026, 7:05 AM EDT. Robin Hale and Casey Novak are emailed.

### 4.5 Invitations

Accepted invitations are not listed; the person appears as a member or resident instead. "Expires" is 72 hours after "Sent".

| Email | Offer | Community | State | Sent by | Sent | Expires or note |
| --- | --- | --- | --- | --- | --- | --- |
| pat@example.com | Community Manager | Keystone Park | Pending | Robin Hale | Oct 11, 2026, 9:15 AM EDT | Expires Oct 14, 2026, 9:15 AM EDT |
| ops@example.com | Company Administrator | Company | Pending | Robin Hale | Oct 12, 2026, 8:30 AM EDT | Expires Oct 15, 2026, 8:30 AM EDT |
| ann@example.com | Board Member | Briar Hollow | Pending | Robin Hale | Oct 11, 2026, 3:30 PM EDT | Expires Oct 14, 2026, 3:30 PM EDT |
| new@example.com | Board Member, term ends Dec 31, 2027 | Sakura Heights | Pending | Jordan Blake | Oct 11, 2026, 2:05 PM EDT | Expires Oct 14, 2026, 2:05 PM EDT |
| alex@example.com | Community Manager | Sakura Heights | Expired | Taylor Grant | Oct 5, 2026, 11:00 AM EDT | Expired Oct 8, 2026, 11:00 AM EDT |
| books@example.com | Accountant | Sakura Heights | Revoked | Jordan Blake | Oct 6, 2026, 9:40 AM EDT | Revoked by Jordan Blake, Oct 7, 2026 |
| kim@example.com | Resident of 711 Keystone Park Dr #29 | Sakura Heights | Pending | Jordan Blake | Oct 10, 2026, 4:40 PM EDT | Expires Oct 13, 2026, 4:40 PM EDT |
| noor@example.com | Resident of 719 Keystone Park Dr #35 | Sakura Heights | Pending | Taylor Grant | Oct 11, 2026, 10:10 AM EDT | Expires Oct 14, 2026, 10:10 AM EDT |
| max@example.com | Resident of 715 Keystone Park Dr #31 | Sakura Heights | Expired | Taylor Grant | Oct 6, 2026, 1:20 PM EDT | Expired Oct 9, 2026, 1:20 PM EDT |
| lena@example.com | Resident of 2 Ridgeview Ct | Keystone Ridge | Scheduled | Jordan Blake | Not sent yet (saved Oct 10, 2026) | Sent when Keystone Ridge is marked Active |
| omar@example.com | Resident of 8 Ridgeview Ct | Keystone Ridge | Scheduled | Jordan Blake | Not sent yet (saved Oct 10, 2026) | Sent when Keystone Ridge is marked Active |
| drew@example.com | Community Manager | Old Mill Commons | Revoked — community archived | Morgan Ellis | Sep 28, 2026, 2:00 PM EDT | Revoked Sep 30, 2026 |
| rae@example.com | Resident of 22 Old Mill Ln | Old Mill Commons | Revoked — community archived | Morgan Ellis | Sep 29, 2026, 11:30 AM EDT | Revoked Sep 30, 2026 |

## 5. Vocabulary

Use these words exactly. They are the spec's terms.

### 5.1 Community status

| Status | Chip text | Meaning | Where it shows |
| --- | --- | --- | --- |
| Onboarding | "Onboarding" | Being set up. Hidden from residents. Resident invitations are scheduled, not sent. | Portfolio, Community settings header, and next to the name in switchers and "My Communities". |
| Active | "Active" | Live. | Portfolio and Community settings header. In switchers and "My Communities", Active is the normal case and needs no chip (design choice; the other option is a chip on every row). |
| Archived | "Archived" | The company no longer manages it. Read-only. | Portfolio and Community settings header. Only Company Administrators see archived communities. They never appear in a switcher or "My Communities". |

- A chip always shows the word. Colour and icons are placeholders and only support the word. Never use a ✓ or a coloured dot alone.
- Residents never see these chips. They see "No longer managed" on a home, or the "Your community isn't live yet" page.

### 5.2 Invitation states

| State | Label | Second line | Actions |
| --- | --- | --- | --- |
| Scheduled | "Scheduled" | "Sent when Keystone Ridge is marked Active" | Revoke. Resend is shown disabled, with that line as the reason. |
| Pending | "Pending" | "Expires Oct 14, 2026, 9:15 AM EDT" | Resend, Revoke |
| Expired | "Expired" | "Expired Oct 8, 2026, 11:00 AM EDT" | Resend, Revoke |
| Accepted | "Accepted" | — | None. Not shown in invitation lists (FR-022b and FR-024d list only scheduled, pending, expired and revoked). |
| Revoked | "Revoked" | "Revoked by Jordan Blake, Oct 7, 2026" | None. Final. |
| Revoked by an archive | "Revoked — community archived" | "Sep 30, 2026" | None. Final. |

- An invitation revoked because its sender lost authority (FR-022) has no label in the spec. Design choice for brief 05: plain "Revoked", or "Revoked" with the second line "Sender's role ended".
- Every invitation row shows email, offer (role or property), sent by, sent at and expires at (FR-022b, FR-024d).
- An invitee is shown by **email only**, never by name, until they accept (FR-018).

### 5.3 Roles, places and actions

| Term | Notes |
| --- | --- |
| Company Administrator | The company-level business role. Not "admin", "super user" or "platform administrator" (FR-003). Plural: Company Administrators. |
| Community Manager | "Manager" alone is fine in running text after first use. A second manager is a "co-manager". |
| Board Member, Accountant, Resident | Title case, as in the spec. |
| Open-ended | A membership with no end date. Others read "Ends Dec 31, 2026" or "Ended Jun 30, 2026". |
| Places | company area · Portfolio · Add community · Company defaults · Company administrators · Community settings, with sections Profile · Managers and memberships · Architectural review · the Residents area (inside Managers and memberships) · My Communities · board mode, and the mode control's "Resident" and "Board". |
| Preferred verbs | Add community · Mark Active · Archive · Restore · Change parent · Appoint · Invite · Resend · Revoke · End membership · Downgrade to Board Member · Replace manager · Remove resident · Revoke role. Exact label: **"Reset architectural review to company defaults"** (US5). |

Architectural review field labels come from today's ARC settings page. Reuse them:

- Review period (days)
- Reminder to the board (days before due, 0 = off)
- When the review period ends without a decision: Flag as overdue only · Deemed approved · Deemed denied
- How a decision is reached: Majority of board members · Majority of votes cast (with a quorum)
- Time zone
- Formal disapproval statement (included in every denial email)

### 5.4 Flags

| Flag | Where | Meaning |
| --- | --- | --- |
| "No manager" | Portfolio row | No open-ended Community Manager. A pending invitation doesn't count (FR-020, FR-034). Showing it in the Community settings header too is a design choice. |
| "invitation pending" | Portfolio Managers column, after the invitee's email: "pat@example.com · invitation pending" | A pending Community Manager invitation. Email only, never a name. |
| "Differs from defaults" | Portfolio row; next to each differing field in the Architectural review section | The community's ARC settings differ from the current company defaults. Per field, add the default: "Company default: 30". |
| "No longer managed" | Resident home list and home switcher | The home's community is Archived (FR-012a). |
| "No resident" | Residents area row; also a filter | The property has no linked resident. |
| "Scheduled" | Invitation rows; also labelled inside the "Invitation pending" filter | See §5.2. |
| "Missing — required" | Profile section, next to an empty required field | The spec says the Profile section "flags any missing required fields" (FR-017). The words are a design choice. |
| "Read-only" | Archived community banner | See §7.3. |

## 6. Refusal copy

Every refusal says why in plain language (FR-042). The exceptions are denials that must not reveal that a community, property or account exists.

- Words in the examples come from §4. Real messages fill in the real names.
- Error codes are never shown to people.
- **Where:** *Inline* is under the field. *Dialog* is at the top of the open dialog, which stays open. *Banner* is at the top of the page or section. *Page* is a whole page. *Toast* is a short message that goes away.

### 6.1 Codes

| Code | When | Message | Where | Disclosure rule |
| --- | --- | --- | --- | --- |
| `FORBIDDEN` (403, spec 025) | A page or action the person may not use. Examples: a Board Member opens Community settings; a Community Manager of one community acts on another community or its properties; anyone but a Company Administrator opens the portfolio. | Page: "You don't have access to that page." The app then takes them to their landing page (spec 025 FR-028). Action: "You don't have permission to do that." | Page: toast on the landing page. Action: banner, or in the dialog. | Identical for a community, property or account that doesn't exist. Never names anything from the request. |
| `COMMUNITY_NAME_TAKEN` | Add or rename to a name in use, ignoring case and outer spaces (FR-007). | Company Administrator: "Keystone Park is already the name of another community. Choose a different community name." If the other community is archived: "Old Mill Commons is the name of an archived community. Archived communities keep their names. Choose a different community name." Anyone else, exactly: **"That community name is unavailable."** | Inline, under Community name (Add community; Profile). | Names the other community only for a Company Administrator. |
| `NO_ACTIVE_MANAGER` | Mark Active without an open-ended Community Manager (FR-010). | "Keystone Park can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted." | Dialog (Mark Active). Design choice: also show Mark Active disabled with this reason. | — |
| `OPEN_WORK` | Archive while work is open (FR-014). | "Keystone Crossing Master can't be archived until this work is closed:" then one line each: "ARC-1041 · Open", "ARC-1042 · Decision Reached", "Keystone Crossing SF · sub-association, Active", "Keystone Crossing TH · sub-association, Active". | Dialog (Archive). The confirm button becomes "Close". | Company Administrators only. |
| `COMMUNITY_ARCHIVED` | Any write in an Archived community, and resident reads other than the payment history (FR-012, FR-012a). | Company Administrator: "Old Mill Commons is archived, so it can't be changed. Restore it to make changes." Community Manager or Board Member (for example from an old tab): "Juniper Row Community Management no longer manages Old Mill Commons, so it can't be changed." Resident, any page except ledger, statements, receipts and transactions: "Juniper Row Community Management no longer manages Old Mill Commons. You can still view this home's ledger, statements, receipts and transactions." Resident, payments and autopay: "Payments and autopay aren't available for this home, because Juniper Row Community Management no longer manages Old Mill Commons." | Staff: banner. Resident: page notice; payment and autopay actions are not offered. | Only people authorized as if the community were Active see it; everyone else gets `FORBIDDEN` (FR-004a). |
| `COMMUNITY_NOT_ACTIVE` | Resident actions in an Onboarding community; resending a scheduled invitation; claim-code redemption in an Onboarding community. | Resend: "This invitation will be sent when Keystone Ridge is marked Active. It can't be sent before then." Resident: "This home's community isn't live yet." (The full page is brief 11.) Claim code: "This home's community isn't live yet, so the claim code can't be used now. Your code has not been used up." | Resend: shown next to the disabled Resend; toast if it happens anyway. Resident: page. Claim code: banner on the registration step (spec 017 page). | The claim-code message appears only after the code and email-verification checks pass (spec Edge Cases). |
| `PARENT_CYCLE` | A parent change that would make a community its own ancestor (FR-016). | "Keystone Crossing TH can't be the parent of Keystone Crossing Master, because Keystone Crossing TH is already under Keystone Crossing Master. Choose a different parent." | Inline, under the parent picker (Change parent dialog). | Company Administrators only. |
| `PARENT_ARCHIVED` | An archived community chosen as parent; restoring a sub-association whose parent is archived (FR-010, FR-016). | Picker: archived communities are not offered. If one is chosen anyway: "Old Mill Commons is archived, so it can't be a parent." Restore: "Old Mill Commons Villas can't be restored while its parent, Old Mill Commons, is archived. Restore Old Mill Commons first." | Picker: inline. Restore: dialog. | — |
| `INVITATION_EXPIRED` | The link is more than 72 hours old (FR-022). | Title: "This invitation has expired". Body: "Invitation links work for 72 hours. Ask the person who invited you to send a new link." | Page. | Shows no community, role, property or sender. Design choice: the spec shows invitation details only for a valid link, so the safe reading treats an expired link as not valid. |
| `INVITATION_INVALID` | The invitation was revoked, already used, or replaced by a resend; or the term end date has passed (FR-022). | Invitee: title "This invitation link can't be used". Body: "It may have been cancelled, already used, or replaced by a newer link. If you still need access, ask the person who invited you." One message for every reason. Staff, Resend or Revoke on an invitation that was just accepted or revoked: "This invitation was already accepted or revoked. The list is now up to date." | Invitee: page. Staff: toast, and the row refreshes. | Nothing about the invitation is shown for an invalid link (FR-022a). |
| `INVITATION_EMAIL_MISMATCH` | Signed in with an account whose email differs from the invited email (FR-022). | Title: "This invitation is for a different email address". Body: "You're signed in as bob@example.com. Sign out, then open the link again and sign in or create an account with the email address the invitation was sent to." Button: "Sign out". | Page. | **Never shows the invited email.** Shows nothing else about the invitation either (design choice; safe default). The invitation stays pending. |
| `INVITATION_PENDING` | Inviting an email that already has a scheduled or pending invitation for the same role or property (FR-022). | Role: "pat@example.com already has a pending Community Manager invitation for Keystone Park. Resend it instead?" with a "Resend invitation" button. Resident: "kim@example.com already has a pending invitation to 711 Keystone Park Dr #29. Resend it instead?" with the same button. Scheduled: "lena@example.com already has a scheduled invitation to 2 Ridgeview Ct. It will be sent when Keystone Ridge is marked Active." (No Resend.) | Inline, under the email field. | The sender can already see this invitation in the list. |
| `ALREADY_MEMBER` | Inviting an email that already holds that role in the community (FR-022). | "lee@example.com is already a Board Member of Sakura Heights." | Inline, under the email field. | Reveals only a role in the sender's own community. |
| `ALREADY_RESIDENT` | Inviting an email already linked to that property (FR-024). | "jane@example.com is already a resident of 711 Keystone Park Dr #29." | Inline, under the email field. | The Residents area already shows residents' emails. |
| `SELF_GRANT_NOT_ALLOWED` | Granting yourself a membership, appointment, invitation, resident link or the Company Administrator role (FR-003a). | Roles and homes: "You can't give yourself a role or a home. Ask another Company Administrator or this community's Community Manager." Company administrators page: "You can't invite yourself. Ask another Company Administrator." | Inline, under the email field or the person picker. Design choice: leave the signed-in person out of the person picker, so only the email path can reach this. | — |
| `LAST_COMPANY_ADMIN` | Revoking the last Company Administrator, including yourself, or losing a race to do so (FR-037). | Self: "You're the only Company Administrator, so your role can't be revoked. Invite another Company Administrator first. Once they accept, your role can be revoked." Race: "Robin Hale is now the only Company Administrator, so the role can't be revoked." | Dialog (Revoke role). | — |
| `LAST_MANAGER` (422, spec 025) | Ending, downgrading or end-dating the last open-ended Community Manager, or losing a race to do so (FR-020). | End: "Jordan Blake is the only Community Manager of Sakura Heights with no end date. A community must always have one. Appoint another manager first." A Company Administrator also sees: "Or use Replace manager." Downgrade: "Jordan Blake can't be downgraded. They're the only Community Manager of Sakura Heights with no end date. Appoint another manager first." End date: "Jordan Blake can't be given an end date while they're the only Community Manager of Sakura Heights with no end date." | Dialog. Helper text always shown in the Managers area: "A community always needs at least one Community Manager with no end date. Managers with an end date don't count toward this." | — |
| `PROPERTY_ACCESS_DENIED` | A resident's next request after their link to the selected home is gone, or a person with no home reaching a resident page (FR-024g). | Has another usable home: "You no longer have access to that home. Now showing 16 Old Mill Ln." No other usable home: "You no longer have access to that home." on their landing page (FR-036a), for example board mode for Jane. | Notice at the top of the page the app moves them to. It stays until dismissed (design choice: a notice rather than a toast, so it isn't missed). | Says "that home", not the address. |

### 6.2 Other refusals (no new code)

| Case | Message | Where |
| --- | --- | --- |
| Rate limited (existing 429) | "Too many requests. Wait a few minutes, then try again." Use this one message for every limit, including the per-recipient invitation cap (design choice: a recipient-specific message could hint that someone else invited that email). | Inline, at the form or action. |
| Account creation failed (spec 017 FR-A5) | "We couldn't create an account with these details. If you already have an account, sign in to accept the invitation." Generic. Never says the email is taken. | Banner on the create-account form (brief 10). |
| Save failed for another reason | "Couldn't save. Check your connection and try again." The person's input is kept. | Banner on the form. |

### 6.3 Validation messages

On save, a form shows an error summary at the top that names each field at fault (US1 #9), and the same message under each field.

| Field | Rule | Message |
| --- | --- | --- |
| Legal name | Required, up to 200 characters | "Enter the legal name." · "Legal name can be at most 200 characters." |
| Community name | Required, 2–80 characters | "Enter a community name." · "Community name must be 2 to 80 characters." |
| County | Required | "Enter the county." |
| Formation date | Required, not in the future | "Enter the formation date." · "Formation date can't be in the future." |
| Management start date | Required | "Enter the management start date." |
| Description | Optional, up to 2,000 characters | "Description can be at most 2,000 characters." |
| A required profile field that already has a value (FR-017) | Can't be cleared | "Legal name is required and can't be left empty." (same pattern for each field) |
| Review period (days) | 1–365 | "Review period must be between 1 and 365 days." (today's app copy) |
| Reminder to the board | 0–30 | "Reminder days must be between 0 and 30." (today's app copy) |
| Time zone | Required | "Time zone is required." (today's app copy) |
| Formal disapproval statement | Required, up to 1,000 characters | "The formal disapproval statement is required." · "The statement can be at most 1,000 characters." |
| Email (any invite) | A valid email | "Enter a valid email address." |
| Name and password (account creation) | Spec 017 registration rules | Reuse the existing registration messages. |

## 7. Shared interaction patterns

### 7.1 Frames

- **Community Manager screens** sit inside `WFShellBoard`: board mode, violet banner, board nav. Community settings and Add community are reached from board mode (FR-029, FR-036).
- **Company Administrator screens** sit inside the company area (owner decision 2026-10-14, brief 01). Company Administrators reach it from their own bookmarkable company sign-in page, or from a "Company" menu in the top-bar account cluster. The frame: the top bar with the account cluster and the "Company" button shown as current; a `wf-strip` reading "Juniper Row Community Management · Company area"; and a left nav with Portfolio, Add community, Company defaults and Company administrators. No violet banner, because the violet banner means board mode.
- **Resident screens** (brief 11) use `WFShell` or `WFShellSide`.
- **Public screens** (brief 10) use the `wf-auth.jsx` look with no app nav.

### 7.2 Community settings page

- **Header:** the community name as the page title; its status chip; the legal name on the line below (or "Missing — required" for a backfilled community); and "Sub-association of Keystone Crossing Master" when it has a parent. For a Company Administrator, the header also holds the status actions for the current status: Onboarding shows Mark Active and Archive; Active shows Archive; Archived shows Restore. Community Managers never see status actions or Change parent.
- **Address:** the page belongs to one community, not to the board-mode active community. It works for Onboarding and Archived communities that are in no switcher (FR-029). A Company Administrator reaches it from the portfolio, with a breadcrumb "Portfolio › Keystone Park". A Community Manager reaches it from the "Community settings" nav entry in board mode.
- **Section nav (owner decision 2026-10-14):** a **side list** on the left of the content, with a **search box** at its top: Profile · Managers and memberships · Architectural review, in this order. Typing in the search box filters the list to matching sections and settings by their labels (for example "review" shows "Architectural review › Review period"); choosing a result opens that section and moves focus to that field. No match: "No settings match \"…\"." Search only finds setting labels on this page, never people or data. **Notifications is not drawn**: no tab, no disabled item, no placeholder (FR-029). Each section can be opened directly. The old ARC settings page leads to Architectural review (FR-030).
- **Saving (owner decision 2026-10-14):** Profile saves **per field**. When the person finishes a field (leaves it, or picks a value), that field saves on its own, and a small "✓ Saved" appears next to it for a few seconds. A field that fails validation shows its error under it, is not saved, and keeps what was typed. Architectural review keeps one Save for the section, as in spec 027, because its rules are reviewed together. Managers, memberships and residents change through their own actions and dialogs.
- **Managers and memberships** has four areas: Managers; Board members and accountants; Residents; Invitations (for the Community Manager, Board Member and Accountant roles). Brief 05 chooses their order and whether they are sub-tabs or stacked blocks; brief 06 follows it.
- **Onboarding notice.** A Community Manager of an Onboarding community sees this at the top of Profile (FR-029): "Keystone Ridge is being set up. A Company Administrator must mark it Active. Resident invitations are sent then."

### 7.3 Read-only mode (Archived)

- Only Company Administrators can open an archived community's settings (FR-012).
- A banner at the top of every section reads: "Old Mill Commons is archived. Juniper Row Community Management no longer manages it. Everything here is read-only. Nothing has been deleted."
- Every section is shown, including Residents (US5 #7).
- Values are shown as plain text, not disabled inputs (design choice; the other option is disabled inputs. Plain text reads better and shows no controls that look usable).
- Only two actions remain: **Restore** in the header and **End membership** on each active membership. Every other action is removed, not disabled.
- Invitations revoked by the archive read "Revoked — community archived".

### 7.4 Confirmation dialogs

Use a confirmation dialog for: Mark Active, Archive, Restore, Change parent, End membership, Downgrade to Board Member, Replace manager, Revoke invitation, Remove resident, Revoke role (Company Administrator), "Reset architectural review to company defaults", and saving company defaults. Whether a direct appointment needs a confirmation is a design choice for brief 05.

Every confirmation dialog has:

1. **A title** that asks a question and names the object: "Archive Old Mill Commons?"
2. **What happens:** each consequence on its own line, with the counts and names the server gives when the dialog opens. Example: "3 autopay schedules are cancelled."
3. **What doesn't change,** where the spec states it. Examples: "The owner of record and the balance don't change." "Applications already received keep their rules."
4. **Choices the spec gives,** for example Restore's option to end each membership first.
5. **Buttons:** the primary button repeats the verb and object ("Archive community"); the other is "Cancel". A destructive action is marked by its words and position, not only by colour.
6. **Refusals:** if the server refuses after the person confirms, the dialog stays open and shows the message from §6 at the top. If the person can't fix it in the dialog, the primary button becomes "Close".

The spec asks for no "type the name to confirm" step. Don't add one.

### 7.5 Forms

- Every field has a visible label above it (`wf-field-label`). Required and optional fields say so in words.
- Helper text under a field states the spec's rule: ranges, character limits, "Invitations are valid for 72 hours".
- Long text fields show a character count: description (2,000) and formal disapproval statement (1,000).
- Dates are typed as MM/DD/YYYY or picked from a date picker.
- **Inviting by email:** an email field only. Nothing looks up or suggests names (FR-018). After sending, the invitee is shown by email only.
- **Choosing a person directly:** a picker over the community's people list, meaning people with an effective membership or resident link in that community (FR-018). It is searchable by name.

### 7.6 Lists, search, filters and pagination

- A table in a card (`wf-card` with `wf-table`), as in `BoardArchApps` and `BoardVendors`.
- Filters are a row of buttons with counts: "All · 31", "Onboarding · 3", "Active · 26", "Archived · 2".
- The search field's placeholder says what it searches: "Search by community or legal name"; "Search by address, resident name or email". Matching ignores case and matches part of a word.
- **Pagination:** 25 rows per page by default (the API allows at most 100). The footer reads "Showing 1–25 of 120", with Previous, page numbers and Next. Changing a filter or the search returns to page 1. A page-size choice (25, 50, 100) is optional (design choice).
- Row actions are visible buttons or a labelled "Actions" menu. They never appear only on hover.
- The spec sets no sort order. Each brief states its default order as a design choice.

### 7.7 Empty, loading and error states

- **Empty:** one plain sentence saying what is missing, plus the next action if the person can take one. Never a table with headers and no rows. Examples:
  - Residents, no properties: "Properties haven't been loaded for Keystone Park yet." No invite action (FR-024d).
  - Managers, none: "Keystone Park has no Community Manager yet." Company Administrator action: "Appoint a manager".
  - Search, no results: "No communities match “Keystone Pk”." Action: "Clear search".
  - No invitations: "No invitations."
- **Loading:** the page frame stays. Placeholder rows (`wf-ph`) stand where data will appear. A busy button reads "Saving…" and is disabled.
- **Load error:** shown in the area that failed: "Couldn't load residents." with "Try again". Other areas keep working.

### 7.8 Toasts, inline messages and banners

| Kind | Use for | Example |
| --- | --- | --- |
| Toast | A finished action whose result is also visible on the page | "Invitation sent to kim@example.com." · "Restore complete." |
| Inline | Validation, and refusals about one field | `COMMUNITY_NAME_TAKEN`, `ALREADY_RESIDENT` |
| Banner | The state of a page or section | Archived read-only; Onboarding notice; load errors |
| In the dialog | Refusals after the person confirms | `LAST_MANAGER`, `OPEN_WORK` |
| Page | States that replace the whole page | Invitation link refusals; "No home linked to this account" |

- A toast never carries a refusal or anything the person must act on.
- A send confirmation is the same whether or not the email has an account (FR-018): "Invitation sent to pat@example.com."
- A scheduled invitation is saved, not sent: "Invitation saved. It will be sent when Keystone Ridge is marked Active."

### 7.9 Dates, times and people

- **Times** use the community's time zone, with its abbreviation: "Oct 14, 2026, 9:15 AM EDT". Company Administrator invitations use the company default time zone (FR-022).
- **Dates** read "Oct 14, 2026". Formation and management start dates are plain dates with no time zone.
- A relative hint ("in 2 days") may be added, but the full date and time are always shown.
- **Invitees** are shown by email until they accept. **Senders** are shown by name: "Sent by Jordan Blake". **Members and residents** are shown by name, with email where the spec lists it (the Residents area; the Company administrators page).

### 7.10 Showing and hiding controls

- Hide controls the person can't use. Disable a control only when the reason helps, and show the reason next to it (example: Resend on a Scheduled invitation).
- **Disabled with a reason (owner decision 2026-10-14, example: Mark Active with no manager):** the button stays visible but disabled. Hovering it **or focusing it with the keyboard** shows a tooltip with the reason, and screen readers read the reason as the button's description. The button must still be reachable by Tab, so keyboard users can find out why.
- Company-area pages are never shown to people who can't use them (FR-035, FR-036). Holding the Company Administrator role alone does not show the board-mode control (FR-036).
- Board-mode nav keeps spec 025 FR-040: entries the role does not confer are shown disabled with a lock.
- Hiding is only for clarity; the server decides. Every hidden action still has a refusal message in §6 for when it is reached anyway (spec Constitution Requirements, Authorization).

## 8. Accessibility baseline

Every artboard meets **WCAG 2.1 AA** (spec Constitution Requirements, Accessibility).

- **Keyboard:** everything works by keyboard, in a logical order. Focus is always visible. Nothing depends on hover.
- **Labels:** every field has a visible label. Helper text and error messages are tied to their field. Buttons and links have names that make sense out of context, for example "Resend invitation to kim@example.com", even when the visible text is shorter.
- **Errors:** on save, the error summary appears at the top and takes focus. Each error also appears under its field.
- **Dialogs:** on open, focus moves to the dialog's title. Focus stays inside until the dialog closes. Esc acts as Cancel. On close, focus returns to the control that opened it.
- **Side panels** (such as the optional property drawer) follow the spec 025 glossary panel: focus moves in on open and returns on close.
- **Text, not colour:** statuses, flags, invitation states, "Differs from defaults", "Missing — required" and destructive buttons all carry words. Icons only add to words.
- **Tables:** real header cells, and a heading that names each table. Filter, search and page changes are announced.
- **Announcements:** toasts are announced politely. Refusals after a save are announced at once.
- **Contrast:** 4.5:1 for text; 3:1 for large text and control borders. The board banner stays dark ink on violet (spec 025 FR-038).
- **Phone width (brief 10):** content reflows at 320 CSS px with no sideways scrolling and works at 200% zoom. Touch targets aim for about 44 × 44 px. The email field brings up the email keyboard. Whether the password field can show the password is a design choice.
- **Time limits:** expiry is shown as a date and time, never only as a countdown.
- **Plain language:** write "architectural review" in full first; "ARC" may follow.

## 9. Brief index

Brief files start with their number (`01-…md` to `12-…md`).

| Brief | Title | Scope | Main requirements |
| --- | --- | --- | --- |
| 01 | Company area and navigation | How Company Administrators and Community Managers reach the new pages: the company entry (two options), board-nav additions, switchers that show Onboarding, and the sign-in landing flow. | FR-012, FR-029, FR-036, FR-036a, FR-036b |
| 02 | Portfolio | The Company Administrator's list of every community, with flags, status filter, search and pagination. | US6; FR-034, FR-035 |
| 03 | Add community | The add-community form for both viewpoints, with validation, name conflicts and success states. | US1; FR-006–FR-009, FR-016 |
| 04 | Community settings page, Profile and status | The page frame, the Profile section, the status actions and dialogs (Mark Active, Archive, Restore, Change parent), and the archived read-only view. | US3, US5; FR-010–FR-017, FR-029, FR-032 |
| 05 | Managers and memberships | Managers, Board members and accountants, and role invitations: appoint, end, downgrade and replace, with their refusals. | US2, US5; FR-018–FR-023, FR-022b |
| 06 | Residents | The Residents area: property list, search, filters, invite, remove, resend and revoke, with empty, no-results and archived states. | US8; FR-024–FR-024g |
| 07 | Architectural review section | Spec 027's ARC settings as a section of Community settings, with differs-from-default indicators, the Company defaults link and the reset. | US5; FR-030, FR-031 |
| 08 | Company defaults | The company defaults page with one Architectural review section, in editable and read-only views. | US4; FR-025–FR-028 |
| 09 | Company administrators | The list of Company Administrators, granting by invitation, revoking, and the last-administrator refusal. | US7; FR-037–FR-039 |
| 10 | Invitation acceptance and sign-up | The public pages an invitee reaches from an invitation email, at desktop and phone width, with every link state and refusal. | FR-022, FR-022a |
| 11 | Sign-in landing and resident-side states | "No home linked", "isn't live yet", "No longer managed", the archived home view, the removed-resident redirect, and the board landing. | US9; FR-012a, FR-024g, FR-036a |
| 12 | Emails | Eight transactional email templates. | FR-008, FR-013, FR-022, FR-024c, FR-038 |
