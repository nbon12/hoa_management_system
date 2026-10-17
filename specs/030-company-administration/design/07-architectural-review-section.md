# 07 — Architectural review section

This brief is for the Architectural review section of the Community settings page: spec 027's ARC settings shown as a section of the page, with the "Differs from defaults" indicators, the "Company defaults" link and "Reset architectural review to company defaults". §1 is the prompt to paste into Claude Design. The other sections hold the details the prompt points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file. Brief 04 owns the page frame and already draws this section read-only for an Archived community (8.04.r).

## 1. Prompt for Claude Design

```text
Project "HOA Management CRM", canvas "NekoHOA Wireframes.html". Attached: spec.md (spec 030, the authority), 00-conventions.md (rules, dataset, vocabulary, refusal copy) and this brief, 07-architectural-review-section.md. spec.md wins, then the conventions. Brief §4 has each artboard's data and states, §5 the exact copy, §6 the interactions.

Add these to section "8 · Company administration" (create it after section 7 if missing, conventions §3). Ids s8-07a to s8-07g. Pages are 1360 wide; a dialog is drawn over its dimmed page, at the page's size. Put the artboards with brief 04's in wf-company-settings.jsx, or in a new wf-company-arc.jsx; shared data stays in wf-company.jsx. Labels exactly:
8.07.a Architectural review · differs from defaults · Company Administrator
8.07.b Architectural review · differs from defaults · Community Manager
8.07.c Architectural review · never saved · matches defaults · Community Manager
8.07.d Architectural review · editing · Community Manager
8.07.e Architectural review · values out of range · Community Manager
8.07.f Reset dialog · Keystone Crossing SF · Company Administrator
8.07.g Architectural review · reset to company defaults · Company Administrator

This is spec 027's existing ARC settings form moved into Community settings as its Architectural review section. Restyle it; add no settings. Use brief 04's page header and section nav (Option A tabs, "Architectural review" current) if section 8 has them; otherwise follow conventions §7.2 and add a .wf-note "frame set by brief 04". Company Administrator: the company-area frame, avatar "RH", breadcrumb, no violet banner. Community Manager: WFShellBoard + boardNav() with Jordan Blake's data, "Community settings" current, the violet banner unchanged (dark ink on violet). Reuse BoardOverview's label/value grid for the read view, and wf-card, wf-field, wf-field-label, wf-pill, wf-btn, wf-sub, wf-note, wf-ph.

Next to each field that differs from the company default, show the text chip "Differs from defaults" and the line "Company default: 30". Show the "Company defaults" link and the "Reset architectural review to company defaults" button with brief §5's words. Put brief §4.2's design choices and open questions on DCPostIts beside the named artboards.

Fidelity: lofi, in section 7's visual language. Layout, copy, states and who sees what are binding; colour and spacing are placeholders. Flags are words, never colour or an icon alone.

Don't draw: the Archived read-only view (8.04.r already has it), the Company defaults page (brief 08), Notifications, links to architectural applications or outcomes, a change history, a "type the name to confirm" step, a screen for the old ARC settings address, error codes, phone layouts.

When done, open every artboard and check that it renders without console errors, its data matches brief §4 and its copy matches §5 word for word. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

Each community follows its own governing documents, so its architectural review rules can differ from the company's standard setup. This section is where those rules are set, and where someone sees at a glance which ones differ from the company defaults.

- **Company Administrators** (Robin Hale) open it from the portfolio, through Community settings, with no membership in the community (US5 #1). They edit the rules and can reset them to the company defaults (FR-030, FR-031). The role never unlocks reading applications or recording outcomes (FR-003, FR-030), so the section links to neither.
- **Community Managers** (Jordan Blake) open it from "Community settings" in board mode. They edit the rules as in spec 027 and can reset them (US5 #2, FR-031). They read the company defaults through the "Company defaults" link, read-only (FR-027).
- **Board Members** keep spec 027's read access to these rules where spec 027 shows them, but can't open Community settings (FR-032; drawn in 8.04.z). Recording outcomes stays Community Manager only, in board mode (FR-030).
- **Archived community:** read-only for Company Administrators only. Brief 04 draws it (8.04.r).
- **Old location.** Spec 027's "ARC settings" page (`board/arc-settings`) now lands on this section (US5 #6, FR-030). It is behaviour, not a screen. Brief 01 removes the old "ARC Settings" nav entry. No artboard.

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| US5 #1 | Robin Hale, with no membership in Keystone Crossing SF, sees the section editable ("Edit architectural review") and is offered "Reset architectural review to company defaults". | 8.07.a |
| US5 #2 | Jordan Blake, Community Manager, can edit the same six settings as in spec 027, and reset them. | 8.07.b, d, e |
| US5 #5 | Keystone Crossing SF: the confirmation lists review period 45 → 30 and reminder 10 → 7, says the other four already match, that applications already received keep their copied rules and that application numbering doesn't change. Afterwards every setting equals the company defaults. The log entry is not on screen. | 8.07.f, g |
| US5 #6 | The old ARC settings location lands on this section. No artboard; a note on 8.07.b. | Note on 8.07.b; §6 |
| FR-027 | A "Company defaults" link in the section. For a Community Manager it says the defaults are read-only to them. Board Members never see it (they can't open the page). | 8.07.a, b, c |
| FR-030 | The six spec 027 settings, with spec 027's labels, field order and ranges, as a section of the page. Editable by a Company Administrator and the Community Manager. No outcome recording, no application links. | 8.07.a–e |
| FR-031 | Reset is offered to both roles, behind a confirmation that lists each field that changes (old → new) and states that applications already received keep their rules and numbering is unchanged. | 8.07.a, b, f, g |
| FR-033 | A reset or a save is logged with old and new values. Nothing on screen, and no change-history view. The dialog's old → new list is what the person confirms. | 8.07.f |
| US4 #2 | A community whose settings were never saved (Keystone Crossing TH) shows its own values, not a live view of the defaults. The section says that changing the company defaults never changes this community's settings or applications already received. | 8.07.c; §5 lines |
| US4 #3 (link) | Jordan opens "Company defaults" from this section. Brief 08 draws the read-only page it opens and the refusal of a change. | 8.07.b, c |
| Ranges (spec 027 FR-029, FR-028a; conventions §6.3) | Review period 1–365, reminder 0–30, time zone required, statement required and at most 1,000 characters. Out-of-range values are refused with messages that name the range, in a summary and under each field. | 8.07.d, e |

## 4. Artboards

"Now" is Monday, Oct 12, 2026, 10:00 AM EDT. People, communities and values come from conventions §4. Desktop width only.

### 4.1 The section (every artboard)

**Frame and header** are brief 04's (§4.1 there): breadcrumb for a Company Administrator only; the community name with its status chip; the legal name line; the parent line; the status actions for a Company Administrator only. Section nav Option A, "Architectural review" current. No Notifications.

**Section body, top to bottom:**

1. Heading "Architectural review" and the intro line (§5), which keeps today's sentence about applications received from now on.
2. **Defaults bar** (one row): the summary line (D2) on the left; the "Company defaults" link and its one-line explanation on the right (D6).
3. **"Review rules" card** (`wf-card`, header bar as in `BoardVendors`): title left, "Edit architectural review" right (D1). Read view: label/value grid like `BoardOverview`, two columns, in today's order: Review period (days) · Reminder to the board (days before due, 0 = off) · When the review period ends without a decision · How a decision is reached · Time zone. Then Formal disapproval statement (included in every denial email) at full width, in quotes. A differing field shows the chip "Differs from defaults" after its value and "Company default: 30" under it.
4. **Reset card** (`wf-card dashed`): the button "Reset architectural review to company defaults" and its helper (§5). When nothing differs, the button is disabled with its reason beside it (D4). Hidden while the form is open (D1).

**Form view** (D1, after "Edit architectural review"): the card becomes a form. A line "All fields are required." Then the six fields with spec 027's controls: number inputs for the two day counts, selects for the two rules, a text input for the time zone (as today), and a text area with a counter for the statement. Each field's helper gives its range or rule and "Company default: …" (D3). Buttons "Save architectural review" and "Cancel".

**Who sees what.** Both roles have the same powers here (permissions table: "Edit ARC settings, reset to company defaults").

| | Company Administrator | Community Manager |
| --- | --- | --- |
| Frame | Company area, breadcrumb, status actions | Board mode, no breadcrumb, no status actions |
| Edit, Save, Reset | Yes | Yes |
| "Company defaults" link | Yes; opens the editable page (D6) | Yes; opens the read-only page (FR-027) |
| Archived community | Read-only (8.04.r) | Can't open it (FR-012) |

### 4.2 Design choices and open questions (each on a `DCPostIt`)

| # | Question | Drawn | Other option | Post-it beside |
| --- | --- | --- | --- | --- |
| D1 | Save model. The owner chose per-field saving for Profile (2026-10-14); this section keeps one Save, as in spec 027 (conventions §7.2). | A read view, then "Edit architectural review" opens one form with one Save (one log entry per save). The reset card is hidden while the form is open, so a reset never mixes with unsaved edits. | Per-field saving like Profile, with "✓ Saved" beside each field. | 8.07.d |
| D2 | A summary line at the top as well as the per-field chips? | Yes: "2 settings differ from the company defaults: Review period and Reminder to the board." or "These settings match the company defaults." | Per-field chips only. | 8.07.a |
| D3 | In the form, does the chip follow what is typed? | No. Chips mark saved values in the read view. In the form, every field shows its company default as helper text. | The chip updates as the person types. | 8.07.d |
| D4 | Reset when every setting already matches. | Shown disabled, with the reason "These settings already match the company defaults." beside it. Still offered in the sense of US5 #1: it is there and says why it can't be used. | Always enabled; the dialog then says "Nothing will change." with "Close". | 8.07.c |
| D5 | Statement over 1,000 characters. | The text area accepts more; the counter reads "1,032 / 1,000" and the field shows the error. Pasted text is never cut off silently. | A hard stop at 1,000, as today's page does. The message then appears only if the server refuses. | 8.07.e |
| D6 | "Company defaults" link for a Company Administrator. | Shown to both roles. Its line differs by role (§5). | Community Managers only; Company Administrators use "Company defaults" in their left nav. | 8.07.a |

**Open questions** (one `DCPostIt` beside 8.07.a):

> - **Q1.** The old ARC settings address names no community; today it uses the board-mode active community. Where does it send someone with no active community, for example a Company Administrator with no membership? The safe reading is their landing page. Not drawn.
> - **Q2.** Reset uses the defaults current when it is confirmed (FR-031). If a Company Administrator changes a default while the dialog is open, should the dialog refresh its old → new list before resetting? Not drawn.

### 4.3 Artboard list

Values used below. All other settings equal the company defaults (conventions §4.1).

| Community | Review period | Reminder | Lapse rule | Decision rule | Time zone | Statement |
| --- | --- | --- | --- | --- | --- | --- |
| Keystone Crossing SF | **45** (default 30) | **10** (default 7) | Flag as overdue only | Majority of board members | America/New_York | The default statement |
| Keystone Crossing TH (never saved; spec 027 built-in values) | 30 | 7 | Flag as overdue only | Majority of board members | America/New_York | The default statement |

**8.07.a Architectural review · differs from defaults · Company Administrator**

- Person: Robin Hale. No membership in Keystone Crossing SF (US5 #1).
- Header: breadcrumb "Portfolio › Keystone Crossing SF"; "Keystone Crossing SF" with the "Active" chip; "Keystone Crossing SF Association, Inc."; "Sub-association of Keystone Crossing Master"; action "Archive".
- Defaults bar: "2 settings differ from the company defaults: Review period and Reminder to the board." and the "Company defaults" link with the Company Administrator line.
- "Review rules" read view: Review period (days) 45, chip "Differs from defaults", "Company default: 30". Reminder 10, chip, "Company default: 7". The other four with no chip.
- Reset card: button enabled.
- State: default. Post-its D2, D6, Q1, Q2.

**8.07.b Architectural review · differs from defaults · Community Manager**

- Person: Jordan Blake, in board mode. Strip "Keystone Crossing SF · Community Manager". Avatar "JB".
- Header: as 8.07.a, with no breadcrumb and no status actions.
- Body: as 8.07.a. The "Company defaults" link has the Community Manager line: the defaults are read-only to Jordan.
- State: default (US5 #2, FR-027).
- Note on canvas: "The old ARC settings page now opens this section (US5 #6, FR-030). Its nav entry is gone (brief 01)." Note: "“Company defaults” opens brief 08's read-only view (US4 #3)."

**8.07.c Architectural review · never saved · matches defaults · Community Manager**

- Person: Jordan Blake. Strip "Keystone Crossing TH · Community Manager".
- Header: "Keystone Crossing TH" with the "Active" chip; "Legal name: Missing — required"; "Sub-association of Keystone Crossing Master". No status actions.
- Defaults bar: "These settings match the company defaults." and the link with the Community Manager line.
- Read view: the TH row above. No chips.
- Reset card: button disabled, reason beside it (D4).
- No "never saved" label: the values are this community's settings either way.
- State: default, nothing differs (US4 #2).
- Note on canvas: "Never saved, so it reads spec 027's built-in values, which equal today's defaults. If a Company Administrator later changes a default, these values stay as they are and the changed field shows “Differs from defaults” (US4 #2, FR-026)." Post-it D4.

**8.07.d Architectural review · editing · Community Manager**

- 8.07.b after "Edit architectural review". Focus is on Review period.
- Form values: 45, 10, "Flag as overdue only", "Majority of board members", "America/New_York", the default statement with the counter "126 / 1,000".
- Each field's helper (§5), ending with its company default. Reset card hidden (D1).
- Buttons: "Save architectural review", "Cancel".
- State: editing. Post-its D1, D3.
- Described, not drawn: after Save, the read view returns with the toast "Architectural review saved." and focus on "Edit architectural review".

**8.07.e Architectural review · values out of range · Community Manager**

- 8.07.d after Jordan entered review period "0", reminder "31", a pasted statement of 1,032 characters (`wf-ph` lines), and pressed Save.
- Error summary at the top of the card, with focus: heading "The architectural review settings weren't saved", then three links: "Review period must be between 1 and 365 days." · "Reminder days must be between 0 and 30." · "The statement can be at most 1,000 characters." The same message under each field. The counter reads "1,032 / 1,000".
- Nothing is saved; the values stay as typed.
- State: validation error. Post-it D5.
- Note on canvas: "400 days gets the same review-period message. Empty time zone, unknown time zone and empty statement: §5."

**8.07.f Reset dialog · Keystone Crossing SF · Company Administrator**

- Drawn over 8.07.a. Opened from "Reset architectural review to company defaults".
- Title: "Reset Keystone Crossing SF's architectural review to the company defaults?"
- "These settings change:" "Review period (days): 45 → 30" · "Reminder to the board (days before due, 0 = off): 10 → 7".
- "These already match and don't change: When the review period ends without a decision, How a decision is reached, Time zone and Formal disapproval statement."
- "The new settings apply to applications received from now on."
- "What doesn't change": applications already received keep their rules; application numbering doesn't change (§5).
- Buttons: "Reset architectural review", "Cancel".
- State: confirmation (US5 #5, FR-031).
- Note on canvas: "A Community Manager gets the same dialog in board mode."

**8.07.g Architectural review · reset to company defaults · Company Administrator**

- 8.07.a after confirming 8.07.f.
- Toast: "Architectural review reset to the company defaults." Focus on the "Architectural review" heading.
- Defaults bar: "These settings match the company defaults." Review period 30 and reminder 7, no chips. Reset button disabled with its reason (D4).
- State: success (US5 #5).
- Note on canvas: "The portfolio's “Differs from defaults” flag for Keystone Crossing SF clears (brief 02)."

**Not drawn (copy in §5):** loading and load error; save failed; rate limited; the reset dialog loading, failing to load, or failing for a network reason; `COMMUNITY_ARCHIVED` (archived in another tab) for each role; `FORBIDDEN` on an action (role ended while the page was open); a reset that changes the time zone or the statement; the Board Member at the old address (the 8.04.z refusal).

## 5. Copy

Quote these exactly. "SR" means text for screen readers only.

### 5.1 Section, read view

| Element | Text |
| --- | --- |
| Section heading | "Architectural review" |
| Intro | "Set these to match your governing documents. Changes apply to applications received from now on." |
| Summary (D2) | "2 settings differ from the company defaults: Review period and Reminder to the board." · One: "1 setting differs from the company defaults: Time zone." · None: "These settings match the company defaults." |
| Link | "Company defaults" |
| Link line, Company Administrator | "New communities start with these. Changing them never changes this community's settings or applications already received." |
| Link line, Community Manager | "New communities start with these. You can read them; only a Company Administrator can change them. Changing them never changes this community's settings." |
| Card title | "Review rules" |
| Edit button | "Edit architectural review" |
| Labels | "Review period (days)" · "Reminder to the board (days before due, 0 = off)" · "When the review period ends without a decision" · "How a decision is reached" · "Time zone" · "Formal disapproval statement (included in every denial email)" |
| Values | Numbers as entered ("45"); rules as "Flag as overdue only" · "Deemed approved" · "Deemed denied" · "Majority of board members" · "Majority of votes cast (with a quorum)"; the time zone name; the statement in quotes |
| Flag | "Differs from defaults" (SR, whole row: "Review period, 45 days. Differs from the company default of 30.") |
| Default line | "Company default: 30" · "Company default: 7" · "Company default: Flag as overdue only" · "Company default: America/New_York". Statement: "Company default:" then the default statement in quotes. |
| Reset button | "Reset architectural review to company defaults" |
| Reset helper | "Sets every rule above to the current company defaults. Applications already received keep their rules, and application numbering doesn't change." |
| Reset disabled reason (D4) | "These settings already match the company defaults." |

### 5.2 Form

| Field | Helper |
| --- | --- |
| Review period (days) | "1 to 365. An application is due this many days after it's received. Company default: 30." |
| Reminder to the board (days before due, 0 = off) | "0 to 30. 0 turns reminders off. Company default: 7." |
| When the review period ends without a decision | Options "Flag as overdue only" · "Deemed approved" · "Deemed denied". "Company default: Flag as overdue only." |
| How a decision is reached | Options "Majority of board members" · "Majority of votes cast (with a quorum)". "With a quorum, more than half of the eligible board members must vote. Company default: Majority of board members." |
| Time zone | "A time zone name, such as America/New_York. Used for due dates and for the expiry times on this community's invitations. Company default: America/New_York." |
| Formal disapproval statement (included in every denial email) | "Up to 1,000 characters. You can cite your own documents, for example “§12.3 of the Declaration”." Counter "126 / 1,000". "Company default:" with the statement. |

| Element | Text |
| --- | --- |
| Required line | "All fields are required." |
| Buttons | "Save architectural review" (busy: "Saving…") · "Cancel" |
| Error summary heading | "The architectural review settings weren't saved" |
| Success toast | "Architectural review saved." |

**Validation** (in the summary and under the field): "Review period must be between 1 and 365 days." · "Reminder days must be between 0 and 30." · "Time zone is required." · "The formal disapproval statement is required." · "The statement can be at most 1,000 characters." · Unknown time zone (spec 027's server already checks it; the words are a design choice): "Time zone isn't recognized. Use a name such as America/New_York."

### 5.3 Reset dialog

| Element | Text |
| --- | --- |
| Title | "Reset Keystone Crossing SF's architectural review to the company defaults?" |
| Change list heading | "These settings change:" |
| Change rows | "Review period (days): 45 → 30" · "Reminder to the board (days before due, 0 = off): 10 → 7" (SR: "from 45 to 30"). Variants: "Time zone: America/Chicago → America/New_York"; the statement shows the old and new text in full, each in quotes. |
| Unchanged settings | "These already match and don't change: When the review period ends without a decision, How a decision is reached, Time zone and Formal disapproval statement." |
| Going forward | "The new settings apply to applications received from now on." |
| Heading | "What doesn't change" |
| Unchanged lines | "Applications already received keep their rules. Their due dates, lapse rule and decision rule don't change." · "Application numbering doesn't change. The next application gets the number it would have had." |
| Buttons | "Reset architectural review" (busy: "Resetting…") · "Cancel" |
| Success toast | "Architectural review reset to the company defaults." |
| Can't load | "Couldn't load what this will change." with "Try again". The primary button is disabled until it loads. |
| Network failure | "Couldn't reset the architectural review. Check your connection and try again." |

### 5.4 Loading, errors and refusals

| Case | Text | Where |
| --- | --- | --- |
| Loading (SR) | "Loading architectural review settings…" | `wf-ph` rows in the card |
| Load error | "Couldn't load the architectural review settings." with "Try again" | In the section |
| Save failed | "Couldn't save. Check your connection and try again." Input is kept. | Banner on the form |
| Rate limited | "Too many requests. Wait a few minutes, then try again." | Banner on the form, or in the dialog |
| `COMMUNITY_ARCHIVED`, Company Administrator | "Old Mill Commons is archived, so it can't be changed. Restore it to make changes." The section reloads read-only (8.04.r). | Banner, or in the dialog |
| `COMMUNITY_ARCHIVED`, Community Manager | "Juniper Row Community Management no longer manages Old Mill Commons, so it can't be changed." | Banner, or in the dialog |
| `FORBIDDEN`, action | "You don't have permission to do that." | Banner, or in the dialog |
| `FORBIDDEN`, page (Board Member, Accountant, Resident, also at the old address) | "You don't have access to that page." | Toast on their landing page (8.04.z) |

## 6. Interactions

- **Opening.** From the section nav, or directly at the section's own address (FR-029). The old ARC settings address opens this section for the board-mode active community and replaces the old address in history, so Back doesn't loop (US5 #6, FR-030). Q1 covers someone with no active community. Focus goes to the "Architectural review" heading.
- **"Company defaults" link.** Opens the Company defaults page (brief 08) in the same tab: read-only for a Community Manager, editable for a Company Administrator. Back returns here. A Community Manager who tries to change a default there is refused (US4 #3, brief 08).
- **Edit (D1).** "Edit architectural review" opens the form; focus moves to Review period. "Cancel" drops changes, returns to the read view, and focus returns to "Edit architectural review".
- **Save.** Client checks run first. If any fail, the summary appears at the top of the card and takes focus; each link moves focus to its field; nothing is sent. The server checks again (time zone name, ranges). On success: the read view returns with the saved values, the chips and summary are worked out again against the current company defaults, the toast shows, and focus returns to "Edit architectural review". The save is logged with old and new values (FR-033); nothing on screen shows it.
- **Reset.**
  1. The button opens the dialog. Focus moves to the title.
  2. The dialog asks the server what will change. `wf-ph` lines stand in while it loads, and the primary button is disabled.
  3. It lists only the fields that change, old → new, then the fields that already match.
  4. "Reset architectural review" sets every rule setting to the current company defaults in one action. The application-number counter is not touched (FR-031).
  5. On success, the dialog closes, the values and flags update, the toast shows, and focus moves to the section heading, because the reset button is now disabled (D4).
  6. A refusal after confirming stays in the dialog at the top (§5.4). If the person can't fix it, the primary button becomes "Close". "Cancel", "Close" and Esc change nothing and return focus to the reset button.
- **Changes elsewhere.** Every request is checked again. If the community is archived in another tab, the next save or reset shows the `COMMUNITY_ARCHIVED` banner and the section reloads read-only (8.04.r) for a Company Administrator. If the person's role ends, the next action is refused with "You don't have permission to do that."

## 7. Accessibility

Conventions §8 applies. For this section:

- **Headings.** "Architectural review" is the section's heading under the page's `h1`. "Review rules" and the dialog title are headings.
- **Read view.** Labels and values form a description list. The chip and the "Company default" line belong to their row, so a screen reader hears value, flag and default together (§5.1 SR text).
- **Words, not colour.** "Differs from defaults" is a word chip. The old → new arrow is read as "from 45 to 30".
- **Form.** Every field has a visible label; "All fields are required." is in words. Helpers, counters and errors are tied to their field. The summary takes focus; its links move focus to the field. The counter is announced when the text goes over 1,000.
- **Disabled reset (D4).** It stays focusable and is announced as unavailable, with its visible reason as its description. Not a tooltip.
- **Dialog.** Focus moves to the title and stays inside. Esc acts as "Cancel". The change rows are a real list. Refusals in the dialog are announced at once.
- **Toasts** are announced politely and need no action.
- **Contrast.** 4.5:1 for text, chips included. In board mode the banner stays dark ink on violet (spec 025 FR-038).
- **Plain language.** The section says "architectural review" in full; "ARC" doesn't appear on screen.

## 8. Out of scope for this design

- The page frame, header, section nav and the Archived read-only view (brief 04, 8.04.r).
- The Company defaults page, editable and read-only, and its refusal (brief 08).
- The portfolio's "Differs from defaults" flag (brief 02) and the board-nav changes (brief 01).
- A screen for the old ARC settings address. It is a redirect only.
- New ARC settings or controls beyond spec 027's six. A time zone picker other than today's text input.
- Architectural applications, votes, recording or resending outcomes, and links to any of them (FR-003, FR-030).
- A change history or audit view (FR-033, FR-040).
- Pushing a changed default to existing communities. The product never offers it (FR-026).
- Notifications in any form (FR-029).
- Phone and tablet layouts.

## 9. Design review checklist

- [ ] **FR-030.** The section shows spec 027's six settings with today's labels and order, inside brief 04's frame, with "Architectural review" current (8.07.a–e).
- [ ] **US5 #1.** Robin Hale, with no membership, sees "Edit architectural review" and an enabled "Reset architectural review to company defaults", with a breadcrumb and no violet banner (8.07.a).
- [ ] **US5 #2.** Jordan Blake, in board mode, can edit and reset, and sees no status actions (8.07.b, d).
- [ ] **FR-027, US4 #3.** "Company defaults" is in the section for Jordan, with the line saying only a Company Administrator can change them (8.07.b, c).
- [ ] **Flags.** Keystone Crossing SF shows "Differs from defaults" with "Company default: 30" and "Company default: 7" on exactly two fields. Keystone Crossing TH shows none (8.07.a, b, c).
- [ ] **US4 #2.** The link line says changing the defaults never changes this community's settings; the TH note explains never-saved settings (8.07.c).
- [ ] **Ranges.** "0", "31" and a 1,032-character statement give the three §5 messages, in the summary with focus and under each field. Nothing is saved (8.07.e).
- [ ] **US5 #5, FR-031.** The reset dialog lists 45 → 30 and 10 → 7 only, names the four unchanged settings, and says applications already received keep their rules and numbering doesn't change. No "type the name" step (8.07.f).
- [ ] **US5 #5, success.** After the reset, every value equals the company defaults, no chips remain, the toast shows and reset is disabled with its reason (8.07.g).
- [ ] **US5 #6.** A note says the old ARC settings page opens this section; no redirect screen is drawn (8.07.b).
- [ ] **FR-033.** No change-history view appears.
- [ ] **Not redrawn.** The Archived read-only view is left to 8.04.r.
- [ ] **Design choices.** D1–D6 and Q1–Q2 are on post-its beside the named artboards.
- [ ] **Words, not colour.** Every flag is a word; no colour or icon stands alone.
- [ ] **Data.** Only conventions §4 people, communities and values appear.
- [ ] **Canvas.** Labels and ids match §1 exactly (s8-07a to s8-07g). Sections 1–7 render as before.
