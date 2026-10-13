# 08 — Company defaults

This brief is for the Company defaults page: the settings every new community starts with, editable by a Company Administrator and read-only to a Community Manager. §1 is the prompt to paste into Claude Design. The other sections hold the details the prompt points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file. Brief 07 draws the Architectural review section, which holds the "Company defaults" link that a Community Manager uses to reach this page.

## 1. Prompt for Claude Design

```text
Project "HOA Management CRM", canvas "NekoHOA Wireframes.html". Attached: spec.md (spec 030, the authority), 00-conventions.md (rules, dataset, vocabulary, refusal copy) and this brief, 08-company-defaults.md. spec.md wins, then the conventions. Brief §4 has each artboard's data and states, §5 the exact copy, §6 the interactions.

Add these to section "8 · Company administration" (create it after section 7 if missing, conventions §3). Ids s8-08a to s8-08h. Pages are 1360 wide; the dialog is drawn over its dimmed page, at the page's size. Shared section-8 frames and data go in wf-company.jsx; these artboards go in wf-company-defaults.jsx. Labels exactly:
8.08.a Company defaults · Company Administrator
8.08.b Company defaults · editing · Company Administrator
8.08.c Company defaults · values out of range · Company Administrator
8.08.d Save dialog · lapse rule changed · Company Administrator
8.08.e Company defaults · saved · Company Administrator
8.08.f Company defaults · read-only · Community Manager
8.08.g Company defaults · change refused · Community Manager
8.08.h Company defaults · refused · Board Member

The page has exactly one section, "Architectural review": spec 027's six settings, labels, order and ranges. At the top of the section, a prominent note: a change applies only to communities added afterwards; existing communities and applications already received never change; a change can't be applied to existing communities.

Company Administrator (Robin Hale): the company-area frame (conventions §7.1), "Company defaults" current in the left nav, avatar "RH", no violet banner. A read view, then "Edit company defaults" opens a form with an error summary; saving opens a confirmation dialog.

Community Manager (Jordan Blake): WFShellBoard + boardNav() with Jordan's data, strip "Keystone Crossing SF · Community Manager", "Community settings" current, violet banner unchanged (dark ink on violet). The same read view as plain text, with no Edit, Save or input of any kind.

Board Member (Lee Morales): his Community Home with the refusal toast.

Reuse BoardOverview's label/value grid, brief 07's form and dialog layout if section 8 has them, and wf-card (lav for the note), wf-field, wf-field-label, wf-btn, wf-sub, wf-note, wf-ph. Put brief §4.2's design choices and open question on DCPostIts beside the named artboards.

Fidelity: lofi, in section 7's visual language. Layout, copy, states and who sees what are binding; colour and spacing are placeholders. Read-only is said in words, not by greyed inputs.

Don't draw: Notifications in any form (no section, tab, placeholder or note); an "apply to existing communities" option, checkbox or button anywhere; a list or count of communities; "Differs from defaults" chips on this page; a change history or "last changed by"; a Company defaults entry in board nav; a "type to confirm" step; error codes; phone layouts.

When done, open every artboard and check that it renders without console errors, shows its stated state, and matches brief §4 data and §5 copy word for word. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

New communities should follow the company's standard setup until their own governing documents say otherwise (US4). This page holds that standard setup. Today it covers architectural review only.

- **Company Administrators** (Robin Hale) set the defaults. They open the page from "Company defaults" in the company-area nav, or from the "Company defaults" link in any community's Architectural review section (brief 07, D6). Only they can change the defaults (FR-025).
- **Community Managers** (Jordan Blake) read the defaults, read-only, through the "Company defaults" link in their community's Architectural review section (FR-027, US4 #3). They have no company-area nav, so this link is their only way in. A change they send anyway is refused.
- **Board Members, Accountants and Residents** (Lee Morales) may not read the defaults. A direct request is refused with 403 `FORBIDDEN` (FR-027, US4 #4).
- **Why the note matters.** A changed default never reaches an existing community or an application already received (FR-026). There is no "apply to existing" option; this is the owner's decision (Clarifications 2026-10-08). The page says so up front, so nobody expects a change to spread. To change one community, people use its own Architectural review section (brief 07).

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| US4 #1 | Robin changes the lapse rule from "Flag as overdue only" to "Deemed approved" and saves. The dialog says communities added from now on start with these settings and existing communities keep their own. Afterwards the page shows "Deemed approved". A canvas note says a community added afterwards starts with "Deemed approved" and Sakura Heights keeps "Flag as overdue only". | 8.08.b, d, e |
| US4 #2 | The note and the dialog say existing communities keep their settings, including communities that never changed theirs, and applications already received keep their rules. A canvas note names Keystone Crossing TH (never saved). | 8.08.a, d, e, f |
| US4 #3 | Jordan opens the page from the "Company defaults" link (8.07.b) and reads every value as plain text, with no edit control. A change request is refused with "You don't have permission to do that." and the values stay as they were. | 8.08.f, g |
| US4 #4 | Lee Morales, a Board Member, opens the page's address and lands on his Community Home with "You don't have access to that page." Accountants and Residents get the same on their own landing page. | 8.08.h |
| US4 #5 | Review period "400", reminder "31" and a 1,032-character statement are refused. Each message names the allowed range, in a summary and under each field. A review period of "0" gets the same message (canvas note). | 8.08.c |
| US4 #6 | Exactly one section, "Architectural review". No Notifications section, tab or placeholder. | every artboard |
| FR-025 | The six settings with spec 027's ranges, showing the initial values: 30 days, Flag as overdue only, Majority of board members, 7 days, America/New_York, and the standard statement. Editable by a Company Administrator only. | 8.08.a, b, f |
| FR-026 | The prominent note at the top of the section. The dialog lists what doesn't change. No option anywhere to push a change to existing communities. | 8.08.a, d, f |
| FR-027 | Read-only for a Community Manager, reached from the Architectural review section. Refused to Board Members, Accountants and Residents. | 8.08.f, g, h |
| FR-028 | One section only, "Architectural review". | every artboard |
| FR-033 | A save is logged with old and new values. Nothing on screen. The dialog's old → new list is what the person confirms. | 8.08.d |

## 4. Artboards

"Now" is Monday, Oct 12, 2026, 10:00 AM EDT, except 8.08.e, which is a snapshot after the change. Values are the company defaults in conventions §4.1. Desktop width only.

### 4.1 The page (every artboard)

**Frame.**

- **Company Administrator:** the company-area frame (conventions §7.1). Avatar "RH", no board-mode control. Strip "Juniper Row Community Management · Company area". Left nav: Portfolio · Add community · Company defaults (current) · Company administrators. No breadcrumb; this is a top-level page. No violet banner.
- **Community Manager:** `WFShellBoard` with `boardNav()` and Jordan Blake's data. Avatar "JB". Strip "Keystone Crossing SF · Community Manager" (Jordan came from 8.07.b). "Community settings" stays current in the nav (D2). Violet banner unchanged.

**Body, top to bottom.**

1. Page title "Company defaults" (`wf-h1`) and its sub line (§5). Community Manager only: a "Back to architectural review" link above the title.
2. Section heading "Architectural review". No section nav (D3).
3. **The note** (`wf-card lav`), full width, with its own heading (§5). Wording differs by role.
4. **"Review rules" card** (`wf-card`, header bar as in `BoardVendors`). Title on the left. Company Administrator: "Edit company defaults" on the right (D1). Community Manager: the words "Read-only" on the right, and the read-only line under the title (§5).
   - **Read view:** a label/value grid like `BoardOverview`, two columns, in today's order: Review period (days) · Reminder to the board (days before due, 0 = off) · When the review period ends without a decision · How a decision is reached · Time zone. Then Formal disapproval statement (included in every denial email) at full width, in quotes.
   - **Form view** (Company Administrator only): the line "All fields are required.", then the six fields with spec 027's controls: number inputs for the two day counts, selects for the two rules, a text input for the time zone, and a text area with a counter for the statement. Each field has its helper (§5). Buttons "Save company defaults" and "Cancel".

**Who sees what.**

| | Company Administrator | Community Manager | Board Member, Accountant, Resident |
| --- | --- | --- | --- |
| Open the page | Yes, from the nav or a section link | Yes, from the Architectural review section link | No: 403 `FORBIDDEN`, landing page |
| Edit, Save | Yes | No controls; a change is refused with 403 `FORBIDDEN` | — |
| The note | Yes, Company Administrator wording | Yes, Community Manager wording | — |

### 4.2 Design choices and open question (each on a `DCPostIt`)

| # | Question | Drawn | Other option | Post-it beside |
| --- | --- | --- | --- | --- |
| D1 | Save model. Follows brief 04 D2 and brief 07 D1. | A read view; "Edit company defaults" opens one form with one Save, then the confirmation dialog (conventions §7.4). | A form that is always open, like today's ARC settings page; Save opens the same dialog. | 8.08.b |
| D2 | Where does the Community Manager's read-only view open? | A page in board mode, "Community settings" still current, with a "Back to architectural review" link. | A side panel over the Architectural review section, using the `GlossaryPanel` pattern. | 8.08.f |
| D3 | Section nav for one section. | None: the page title, then the "Architectural review" heading. | A one-item section nav, matching Community settings. | 8.08.a |
| D4 | Should the save dialog say what happens to the portfolio flag? The flag compares each community with the current defaults (FR-034), so after a change, communities with the old value show "Differs from defaults". | Yes, one line, no count. | Leave the line out. | 8.08.d |
| D5 | Statement over 1,000 characters. Must match brief 07 D5. | The text area accepts more; the counter reads "1,032 / 1,000" and the field shows the error. | A hard stop at 1,000, as today's page does. | 8.08.c |

**Open question Q1** (`DCPostIt` beside 8.08.d): the spec is silent on two Company Administrators editing at once. The dialog lists changes against the values saved when it opens. Should it warn if the saved values changed after the form opened, or should the last save simply win? Not drawn.

### 4.3 Artboard list

**8.08.a Company defaults · Company Administrator**

- Person: Robin Hale. Reached from "Company defaults" in the company-area nav.
- Note: Company Administrator wording.
- Read view with the conventions §4.1 values: 30 · 7 · Flag as overdue only · Majority of board members · America/New_York · the standard statement.
- "Edit company defaults" in the card header.
- State: default (FR-025 initial values, FR-026, FR-028). Post-it D3.

**8.08.b Company defaults · editing · Company Administrator**

- 8.08.a after "Edit company defaults". Focus is on Review period.
- Robin has changed "When the review period ends without a decision" to "Deemed approved". Every other field holds its saved value. The statement counter reads "126 / 1,000".
- Buttons "Save company defaults", "Cancel".
- State: editing. Post-it D1.

**8.08.c Company defaults · values out of range · Company Administrator**

- 8.08.b after Robin entered review period "400", reminder "31", pasted a 1,032-character statement (`wf-ph` lines), and pressed "Save company defaults".
- Error summary at the top of the card, with focus: heading "The company defaults weren't saved", then three links: "Review period must be between 1 and 365 days." · "Reminder days must be between 0 and 30." · "The statement can be at most 1,000 characters." The same message under each field. Counter "1,032 / 1,000".
- No dialog opens. Nothing is saved; the values stay as typed.
- State: validation error (US4 #5). Post-it D5.
- Note on canvas: "0 days gets the same review-period message. Empty time zone, unknown time zone and empty statement: brief §5."

**8.08.d Save dialog · lapse rule changed · Company Administrator**

- Drawn over 8.08.b, opened by "Save company defaults".
- Title "Save the company defaults?"
- "These settings change:" one row: "When the review period ends without a decision: Flag as overdue only → Deemed approved".
- "What happens": "Communities added from now on start with these settings." Then the D4 line.
- "What doesn't change": the two lines in §5.3.
- Buttons "Save company defaults", "Cancel". No checkbox, toggle or second button for existing communities.
- State: confirmation (US4 #1, #2; FR-026). Post-its D4, Q1.

**8.08.e Company defaults · saved · Company Administrator**

- 8.08.a after confirming 8.08.d. Toast "Company defaults saved." Focus on "Edit company defaults".
- Read view: "When the review period ends without a decision" now reads "Deemed approved". Every other value unchanged.
- State: success.
- Notes on canvas:
  - "Snapshot after the change. Every other brief shows the defaults unchanged (conventions §4.1)."
  - "A community added after this starts with “Deemed approved” (US4 #1). Sakura Heights, Keystone Crossing TH (never saved) and every other existing community keep “Flag as overdue only”, and their applications already received keep their rules (US4 #1, #2; FR-026)."
  - "A community being added at this moment gets the defaults in effect when it is saved (spec Edge Cases)."

**8.08.f Company defaults · read-only · Community Manager**

- Person: Jordan Blake, in board mode, after choosing "Company defaults" in Keystone Crossing SF's Architectural review section (8.07.b).
- "Back to architectural review" above the title. Note: Community Manager wording. Card header: "Read-only" and the read-only line.
- Read view with the same values as 8.08.a, as plain text. No Edit button, no Save, no inputs, no disabled controls.
- State: read-only (US4 #3, FR-027). Post-it D2.

**8.08.g Company defaults · change refused · Community Manager**

- 8.08.f with the banner "You don't have permission to do that." at the top of the "Review rules" card. The values are unchanged.
- State: refusal (US4 #3).
- Note on canvas: "Jordan has no edit controls. If a change request reaches the server anyway, it is refused with 403 FORBIDDEN. The page shows this banner and reloads the saved values."

**8.08.h Company defaults · refused · Board Member**

- Person: Lee Morales, Board Member of Sakura Heights. Lee opened a link to the Company defaults page.
- Draw Lee's landing as brief 01 draws it (8.01.k): board mode, strip "Sakura Heights · Board Member", "Community Home" current, "Community settings" and "Add community" locked (🔒, "Not available to your role"). Body: "Community Home — section 7, unchanged".
- Toast: "You don't have access to that page."
- State: refusal (US4 #4, FR-027). Accountants and Residents get the same refusal on their own landing page. Not drawn.

**Not drawn (copy in §5):** loading and load error; save failed; rate limited; a refusal inside the dialog; an unknown or empty time zone; an empty statement; a change to the time zone or the statement in the dialog.

## 5. Copy

Quote these exactly. "SR" means text for screen readers only.

### 5.1 Page and read view

| Element | Text |
| --- | --- |
| Page title | "Company defaults" |
| Sub line | "Every community added from now on starts with these settings. Each community can then change its own." |
| Section heading | "Architectural review" |
| Note heading (both roles) | "Changes apply only to new communities" |
| Note, Company Administrator | "A change here applies only to communities added after you save it. Existing communities keep their own settings, including communities that never changed them. Applications already received keep their rules. A change can't be applied to existing communities. To change one community, edit its Architectural review settings in Community settings." |
| Note, Community Manager | "A change here applies only to communities added afterwards. Existing communities, including yours, keep their own settings, and applications already received keep their rules." |
| Back link (Community Manager) | "Back to architectural review" (SR: "Back to Keystone Crossing SF's architectural review") |
| Card title | "Review rules" |
| Edit button (Company Administrator) | "Edit company defaults" |
| Card header marker (Community Manager) | "Read-only" |
| Read-only line (Community Manager) | "You can read the company defaults. Only a Company Administrator can change them." |
| Labels | "Review period (days)" · "Reminder to the board (days before due, 0 = off)" · "When the review period ends without a decision" · "How a decision is reached" · "Time zone" · "Formal disapproval statement (included in every denial email)" |
| Values | "30" · "7" · "Flag as overdue only" (after the change: "Deemed approved") · "Majority of board members" · "America/New_York" · "This is a formal disapproval of the plans as submitted under the community's governing documents. You may revise and resubmit." |

### 5.2 Form

| Field | Helper |
| --- | --- |
| Review period (days) | "1 to 365. An application is due this many days after it's received." |
| Reminder to the board (days before due, 0 = off) | "0 to 30. 0 turns reminders off." |
| When the review period ends without a decision | Options "Flag as overdue only" · "Deemed approved" · "Deemed denied". |
| How a decision is reached | Options "Majority of board members" · "Majority of votes cast (with a quorum)". "With a quorum, more than half of the eligible board members must vote." |
| Time zone | "A time zone name, such as America/New_York. New communities start with it. Company Administrator invitations show their expiry time in it." (FR-022) |
| Formal disapproval statement (included in every denial email) | "Up to 1,000 characters." Counter "126 / 1,000". |

| Element | Text |
| --- | --- |
| Required line | "All fields are required." |
| Buttons | "Save company defaults" · "Cancel" |
| Error summary heading | "The company defaults weren't saved" |
| Validation (summary and under the field) | "Review period must be between 1 and 365 days." · "Reminder days must be between 0 and 30." · "Time zone is required." · "The formal disapproval statement is required." · "The statement can be at most 1,000 characters." · Unknown time zone, as brief 07: "Time zone isn't recognized. Use a name such as America/New_York." |

### 5.3 Save dialog

| Element | Text |
| --- | --- |
| Title | "Save the company defaults?" |
| Change list heading | "These settings change:" |
| Change row | "When the review period ends without a decision: Flag as overdue only → Deemed approved" (SR: "from Flag as overdue only to Deemed approved"). A changed statement shows the old and new text in full, each in quotes. |
| What happens heading | "What happens" |
| What happens | "Communities added from now on start with these settings." |
| Portfolio line (D4) | "Existing communities whose settings now differ will show “Differs from defaults” in the portfolio." |
| What doesn't change heading | "What doesn't change" |
| What doesn't change | "Existing communities keep their own architectural review settings, including communities that never changed them." · "Applications already received keep their rules." |
| Buttons | "Save company defaults" (busy: "Saving…") · "Cancel" |

### 5.4 Toasts, loading and refusals

| Case | Text | Where |
| --- | --- | --- |
| Saved | "Company defaults saved." | Toast |
| Loading (SR) | "Loading company defaults…" | `wf-ph` rows in the card |
| Load error | "Couldn't load the company defaults." with "Try again" | In the section |
| Save failed | "Couldn't save. Check your connection and try again." Input is kept. | In the dialog |
| Rate limited | "Too many requests. Wait a few minutes, then try again." | In the dialog |
| `FORBIDDEN`, action | "You don't have permission to do that." | Community Manager: banner on the card (8.08.g). Company Administrator whose role was revoked while the dialog was open (US7 #2): in the dialog, primary button becomes "Close". |
| `FORBIDDEN`, page (Board Member, Accountant, Resident) | "You don't have access to that page." | Toast on their landing page (8.08.h) |

## 6. Interactions

- **Opening.** A Company Administrator opens the page from the company-area nav or from the "Company defaults" link in any community's Architectural review section (brief 07). A Community Manager opens it only from that link, in board mode (FR-027). Focus goes to the "Company defaults" title.
- **Back (Community Manager).** "Back to architectural review" returns to the Architectural review section of the board-mode active community. Browser Back does the same.
- **Edit (D1).** "Edit company defaults" opens the form; focus moves to Review period. "Cancel" drops changes, returns to the read view, and focus returns to "Edit company defaults". If no value changed, "Save company defaults" closes the form and sends nothing (design choice).
- **Save.** Client checks run first. If any fail, the summary appears at the top of the card and takes focus; each link moves focus to its field; no dialog opens. If they pass, the dialog opens and focus moves to its title.
- **Confirm.** "Save company defaults" in the dialog sends the save. The server checks the ranges and the time zone name again. On success the dialog closes, the read view shows the saved values, the toast shows, and focus returns to "Edit company defaults". The save is logged with old and new values (FR-033); nothing on screen shows it. Nothing is sent to any existing community (FR-026).
- **Refusal after confirming.** The dialog stays open with the message at the top (§5.4). For a save failure or rate limit, the person can try again from the dialog. A server validation refusal turns the primary button into "Close", which returns to the form with the error summary (focused) and each message under its field. "Cancel" and Esc change nothing and return focus to "Save company defaults" in the form.
- **Community Manager.** There is nothing to edit. If a change request reaches the server anyway, it is refused with 403 `FORBIDDEN`; the page shows the banner and reloads the saved values. Focus moves to the banner.
- **Board Member, Accountant or Resident.** A request for the page is refused with 403 `FORBIDDEN`. The app takes them to their landing page (spec 025 FR-028) and shows the toast. Focus goes to the landing page's main heading.

## 7. Accessibility

Conventions §8 applies. For this page:

- **Headings.** "Company defaults" is the `h1`. "Architectural review" is the section heading. The note heading, "Review rules" and the dialog title are headings.
- **The note** is a labelled region with its heading, read before the settings. It stands out by position, size and border, not colour alone.
- **Read view.** Labels and values form a description list. "Read-only" and the read-only line are text, so a screen reader hears them before the values. No disabled inputs.
- **Form.** Visible labels; "All fields are required." is in words. Helpers, the counter and errors are tied to their field. The error summary takes focus; its links move focus to the field. The counter is announced when the text goes over 1,000.
- **Dialog.** Focus moves to the title and stays inside. Esc acts as "Cancel". The change rows are a real list; the arrow is read as "from … to …". Refusals in the dialog are announced at once.
- **Back link** has a name that says where it goes (§5.1 SR).
- **Toasts** are announced politely and need no action. The refusal banner on 8.08.g is announced at once.
- **Contrast.** 4.5:1 for text, including the note on its lav fill. In board mode the banner stays dark ink on violet (spec 025 FR-038).
- **Plain language.** "Architectural review" in full; "ARC" doesn't appear on screen.

## 8. Out of scope for this design

- **Notification defaults.** FR-025 and the Key Entities reserve a place for them. The Notification Settings spec adds that section and updates US4 #6 and FR-028. Until then, nothing on the page, the canvas or a post-it hints at it.
- Applying a changed default to existing communities, in any form (FR-026, owner decision).
- The Architectural review section of Community settings, its "Company defaults" link, its "Differs from defaults" chips and its reset (brief 07).
- The portfolio's "Differs from defaults" flag (brief 02).
- The company-area frame and nav, and the board-nav entries (brief 01).
- Other company defaults, such as payment policy (spec Out of Scope).
- A change history, "last changed by", or an audit view (FR-033, FR-040).
- A time zone picker other than today's text input.
- Phone and tablet layouts.

## 9. Design review checklist

- [ ] **FR-028, US4 #6.** Every artboard shows exactly one section, "Architectural review". No Notifications section, tab, placeholder or note anywhere.
- [ ] **FR-025.** 8.08.a shows the six settings with today's labels and order, and the initial values: 30, 7, Flag as overdue only, Majority of board members, America/New_York, the standard statement.
- [ ] **FR-026.** The note "Changes apply only to new communities" is at the top of the section for both roles, and says a change can't be applied to existing communities (8.08.a, f).
- [ ] **FR-026.** No option, checkbox, toggle or button to apply a change to existing communities appears on any artboard.
- [ ] **US4 #1.** The dialog lists "Flag as overdue only → Deemed approved" and says communities added from now on start with these settings; 8.08.e shows "Deemed approved" and its note says existing communities keep "Flag as overdue only" (8.08.b, d, e).
- [ ] **US4 #2.** The note and the dialog say existing communities keep their settings, including ones that never changed them, and applications already received keep their rules (8.08.a, d, e).
- [ ] **US4 #3.** Jordan Blake, in board mode, reads every value as plain text with "Read-only", the read-only line and "Back to architectural review", and has no edit control (8.08.f).
- [ ] **US4 #3.** A change by Jordan is refused with "You don't have permission to do that." and the values are unchanged (8.08.g).
- [ ] **US4 #4.** Lee Morales lands on his Community Home with "You don't have access to that page." (8.08.h).
- [ ] **US4 #5.** "400", "31" and a 1,032-character statement give the three §5 messages, each naming its range, in the summary with focus and under each field. No dialog opens and nothing is saved (8.08.c).
- [ ] **FR-027.** The Community Manager's only way in is the Architectural review link; no Company defaults entry appears in board nav.
- [ ] **Save confirmation.** The dialog has the title, change list, "What happens", "What doesn't change" and "Save company defaults" / "Cancel", with no "type to confirm" step (8.08.d).
- [ ] **Success.** Toast "Company defaults saved." and the snapshot note are on 8.08.e.
- [ ] **Frames.** Company Administrator: company area, "Company defaults" current, no violet banner. Community Manager: board mode, "Community settings" current, violet banner unchanged.
- [ ] **Design choices.** D1–D5 and Q1 are on post-its beside the named artboards.
- [ ] **Words, not colour.** "Read-only" and the note are words; nothing relies on colour or greyed inputs.
- [ ] **Data.** Only conventions §4 people, communities and values appear. No list or count of communities.
- [ ] **Canvas.** Labels and ids match §1 exactly (s8-08a to s8-08h). Sections 1–7 render as before.
