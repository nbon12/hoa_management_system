# 03 — Add community

This brief is for the Add community form, used by Company Administrators and by Community Managers, and for what each sees after a save. §1 is the prompt to paste into Claude Design. The other sections hold the details that the prompt points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file.

## 1. Prompt for Claude Design

```text
Project: "HOA Management CRM". Canvas: NekoHOA Wireframes.html. Put every new artboard in section "8 · Company administration" (DCSection id "company-admin"); create it after section 7 if missing (00-conventions.md §3).

Attached: spec.md (feature spec 030, the authority), 00-conventions.md (shared rules, dataset, vocabulary, refusal copy) and 03-add-community.md (this brief). The brief's §4 gives each artboard's data and state, §5 the exact copy, §6 the behaviour. If files disagree, spec.md wins.

Draw the Add community form from two viewpoints:
- Company Administrator (Robin Hale): the neutral company-area frame from conventions §7.1, avatar "RH", no violet banner.
- Community Manager (Jordan Blake, Taylor Grant): WFShellBoard in board mode, "Add community" current in boardNav(), each person's data passed in.
The fields and their order are the same for both. The intro, the parent list and the name-taken message differ.

Artboards, 1360 wide, as tall as the content, ids s8-03a to s8-03o, labels exactly:
8.03.a Add community · empty · Company Administrator
8.03.b Add community · parent list “Keystone” · Company Administrator
8.03.c Add community · parent list “Old Mill” · no match · Company Administrator
8.03.d Add community · required fields missing · Company Administrator
8.03.e Add community · formation date in the future · Company Administrator
8.03.f Add community · name taken · Company Administrator
8.03.g Add community · name taken by an archived community · Company Administrator
8.03.h Community settings · Keystone Park just added · Company Administrator
8.03.i Add community · empty · Community Manager
8.03.j Add community · parent list · Community Manager
8.03.k Add community · parent list · manages one community · Community Manager
8.03.l Add community · name taken · Community Manager
8.03.m Community settings · Keystone Ridge just added · Community Manager
8.03.n Add community · refused · only managed communities archived · Community Manager
8.03.o Add community requested directly · refused · Board Member

Reuse wf-card, wf-field, wf-field-label, wf-btn, wf-pill and wf-sub. For 8.03.h and 8.03.m, use brief 04's Community settings header and section nav if section 8 has them; otherwise follow conventions §7.2, with BoardOverview's field grid for the Profile values. Sections 1–7 must render as before.

Put a DCPostIt by 8.03.a with the open questions in brief §4.4.

Fidelity: lofi, the same visual language as section 7. Layout, field order, copy, states and who sees what are binding. Colour and spacing are placeholders. Errors, chips and notices are words, never colour alone.

Do not draw: a status choice, a manager or invitation field, a confirmation dialog, a name check while typing, archived communities in a parent list, another community's name in a Community Manager's message, a link to Company defaults, status actions or Change parent for a Community Manager, Notifications, a phone layout.

When you finish, open each artboard and check that it renders with no console errors, that its data matches brief §4 and that its copy matches brief §5 word for word. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

The form adds a community the company has taken on (US1). Without it, every new HOA needs an engineer to write seed data. A new community always starts as **Onboarding**, hidden from residents, and gets the company's default settings (FR-008, FR-009).

- **Company Administrators** (Robin Hale; Casey Novak). They reach the form from the company area: its left nav, or the portfolio's "Add community" button (brief 02). The parent list offers any community that isn't archived (FR-016). A community they add has **no Community Manager** until one is appointed (FR-008, US2). So they land on the new community's Community settings page with a prompt to appoint one.
- **Community Managers** (Jordan Blake; Taylor Grant). They reach the form from board mode (FR-036). They may add a community while they hold an effective Community Manager membership (active, not past its end date) in at least one Onboarding or Active community (FR-006). They become its **first Community Manager, with no end date**, in the same action. Every Company Administrator is emailed that it is waiting to be marked Active (FR-008). The parent list offers only communities they manage (FR-016). Becoming the first manager is not a grant to oneself, so `SELF_GRANT_NOT_ALLOWED` never applies here (FR-003a).
- **A person who is both** gets the union of both roles (spec Edge Cases). Their parent list is the Company Administrator's list wherever they open the form.
- **Board Members, Accountants and Residents** can't add a community. A request is refused with 403 `FORBIDDEN` (US1 #5). So is a Community Manager whose only manager memberships are in Archived communities (US1 #6).

Only a Company Administrator can mark the new community Active (FR-010; brief 04).

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| US1 #1 | Robin adds "Keystone Park HOA, Inc.", community name "Keystone Park", county "Hamilton", formation date 03/14/2004, management start date 11/01/2026. It is saved as Onboarding with no parent and no Community Manager. The new page shows the "Onboarding" chip, no "Sub-association of" line, "No manager yet — appoint one", and that its architectural review settings were copied from the company defaults. It appears in the portfolio (drawn in brief 02, 8.02.a). | 8.03.a, h |
| US1 #2 | Robin types "keystone park " (other case, trailing space). Refused, inline and in the error summary: "Keystone Park is already the name of another community. Choose a different community name." The form keeps the input. Nothing is created. | 8.03.f |
| US1 #3 | Jordan, who doesn't manage Keystone Park, types "Keystone Park". Refused with exactly "That community name is unavailable." Nothing on the page names or describes the other community. | 8.03.l |
| US1 #4 | Jordan adds Keystone Ridge. The intro says Jordan will be its first Community Manager and a Company Administrator must mark it Active. After saving: the "Onboarding" chip; "You're its Community Manager, with no end date."; "It's waiting for a Company Administrator to mark it Active."; the Company Administrators were emailed; the board-mode strip and "My Communities" now include Keystone Ridge with "Onboarding". The email itself is brief 12. | 8.03.i, j, m |
| US1 #5 | Lee Morales, a Board Member only, requests the Add community page directly. Lee lands on board mode with "You don't have access to that page." Board-mode nav shows "Add community" disabled with a lock (spec 025 FR-040). | 8.03.o |
| US1 #6 | Morgan Ellis's only manager memberships are in Archived communities. Saving from an old tab is refused with "You don't have permission to do that." The input stays. Nothing is created. | 8.03.n |
| US1 #7 | The Company Administrator's parent list offers "Keystone Crossing Master". The helper text says a parent gives nobody access to the community. A community added with a parent shows "Sub-association of Keystone Crossing Master" in its header (§5; brief 04 draws the header for Keystone Crossing SF). | 8.03.b; copy |
| US1 #8 | A Community Manager's parent list offers only the communities they manage: Taylor sees only Sakura Heights; Jordan sees his four. If the server refuses a parent anyway, the message is "You don't have permission to do that." and never names the parent, so it reads the same whether or not that community exists (FR-004). | 8.03.j, k; copy as in 8.03.n |
| US1 #9 | All five required fields empty: an error summary at the top names each field, and each field shows its own message. A formation date in the future: "Formation date can't be in the future." | 8.03.d, e |
| US1 Independent Test | Add as a Company Administrator and as a Community Manager; Onboarding; defaults copied; first manager; Company Administrators emailed; a duplicate name refused; a non-manager refused. | 8.03.a–o |
| FR-006 | The seven fields, in this order, with the rules as helper text: legal name (required, up to 200 characters), community name (required, 2–80), county (required), formation date (required, not in the future), management start date (required), description (optional, up to 2,000, with a counter), parent (optional). Who may add: Company Administrators and effective Community Managers. | 8.03.a, i; all form artboards |
| FR-007 | Name conflicts ignore case and outer spaces, and include Archived communities. The message names the other community only to a Company Administrator. | 8.03.f, g, l |
| FR-008 | Always Onboarding. Added by a Company Administrator: no manager, so "No manager yet — appoint one". Added by a Community Manager: they are its open-ended Community Manager, and every Company Administrator is emailed. | 8.03.h, m |
| FR-009 | A small note on the form: settings are copied from the company defaults when the community is added, and later default changes don't change them. A line on the new page says they were copied. | 8.03.a–g, i–l (note); h, m |
| FR-016 | Parent is optional. A Company Administrator's list holds every community that isn't archived; archived ones never appear. A Community Manager's list holds only communities they manage. A parent changes nobody's access. Change parent later is brief 04 and Company Administrators only. | 8.03.b, c, j, k |
| FR-029 (Onboarding notice) | On the Community Manager's new page: "Keystone Ridge is being set up. A Company Administrator must mark it Active. Resident invitations are sent then." | 8.03.m |
| FR-036 | A Company Administrator reaches Add community in the company area. A Community Manager reaches it from board mode. | 8.03.a, i |
| FR-042 | Every refusal says why in plain words: each validation message names its field and the rule; the Company Administrator's name conflict names the other community. The two allowed exceptions don't explain: the Community Manager's "That community name is unavailable." (FR-007) and "You don't have permission to do that." (FR-004). | 8.03.d–g, l, n, o |
| FR-041 (rate limit) | The create uses the `board-writes` limit. Copy for the 429 refusal is in §5. Not drawn. | — |
| Edge case: a default changes while a community is being created | The note says settings are copied "when you add the community", that is, at save. | 8.03.a, i |
| Edge case: archived names stay reserved | Robin's archived-name message: "Archived communities keep their names." | 8.03.g |
| Constitution, Accessibility | The add-community form is named in the spec's accessibility list: labels, visible focus, messages tied to their fields, keyboard-only use. | All, see §7 |

## 4. Artboards

### 4.1 The form (both viewpoints)

**Body, top to bottom.**

1. **Breadcrumb**, Company Administrator only: "Portfolio › Add community".
2. **Title** "Add community".
3. **Intro.** One short paragraph (`wf-sub`). The text differs by viewpoint (§5).
4. **Community Manager callout**, Community Manager only. A `wf-card lav` above the form. It says they will be the first Community Manager and a Company Administrator must mark it Active (§5).
5. **Message area.** Empty by default. It holds the error summary (8.03.d–g, l) or a refusal banner (8.03.n). Nothing else appears here.
6. **Form card** (`wf-card`, about 720 wide, left-aligned in the body). The fields, in the FR-006 order:

| # | Label (visible, above the field) | Input | Helper text under the field |
| --- | --- | --- | --- |
| 1 | Legal name (required) | Text | "Up to 200 characters." |
| 2 | Community name (required) | Text | "The name people see in the app. 2 to 80 characters. No other community can have this name, even with different capital letters." |
| 3 | County (required) | Text | None. |
| 4 | Formation date (required) | Date, typed or picked | "MM/DD/YYYY. Can't be in the future." |
| 5 | Management start date (required) | Date, typed or picked | "MM/DD/YYYY. When Juniper Row Community Management starts managing it. This can be a future date." |
| 6 | Description (optional) | Text area, about 4 lines | "Up to 2,000 characters." The counter "0 / 2,000" sits at the right, under the field. |
| 7 | Parent association (optional) | Searchable list | Differs by viewpoint (§5). The value reads "None" until one is chosen. |

   Fields 4 and 5 sit side by side. Every other field is full width. Each date field has a calendar button at its right end.

7. **Defaults note.** Under a `wf-divider`, a small `wf-sub` line (§5). Plain text, no link. The Company defaults page arrives later (US4), and MVP item (c) copies the defaults with no page.
8. **Buttons**, left-aligned: "Add community" (primary), then "Cancel" (ghost).

**Rules for every form artboard.**

- No status field. Every new community is Onboarding (FR-008). The intro says so.
- No manager or invitation field. Appointing is brief 05.
- No preselected parent. "None" is the default, even in board mode. Design choice; the other option preselects the active community for a Community Manager. Not drawn.
- An error shows in two places: listed in the summary, and under its field. The field's border is heavier. Colour is a placeholder; the words carry the error.

**Parent list (open state).** A search field labelled "Search communities", with the placeholder "Type a community name". Under it, a list:

- "None — not a sub-association" is always first.
- Then the communities, A–Z by community name.
- An Onboarding community shows an "Onboarding" chip after its name. Active ones show no chip (conventions §5.1).
- The current value has a ✓ and the word "Selected". Keyboard focus is a visible outline.

| Viewpoint | Lists |
| --- | --- |
| Company Administrator | Every community that is Onboarding or Active (FR-016). Never an Archived one. |
| Community Manager | Only the Onboarding or Active communities where they hold an effective Community Manager membership (FR-016, FR-004 check (a)). |

**Design choice: the parent list.** Drawn: a searchable list, because a company may have 200 communities (SC-008). The other option is a plain dropdown. Not drawn.

**Design choice: required and optional.** Drawn: every label says "(required)" or "(optional)" (conventions §7.5). The other option is one line, "All fields are required unless marked optional", with only optional labels marked. Not drawn.

### 4.2 Frames

| Person | Frame | Details |
| --- | --- | --- |
| Robin Hale | Company area (conventions §7.1) | Top bar with avatar "RH". No board-mode control. Strip "Juniper Row Community Management · Company area". Left nav: Portfolio, Add community, Company defaults, Company administrators. "Add community" is current on the form. No violet banner. |
| Jordan Blake | `WFShellBoard`, board mode | Avatar "JB". Strip "Keystone Crossing Master · Community Manager". `boardNav()` with role manager and 4 communities (Oct 9) or 5 (Oct 12), so "My Communities" shows. "Add community" is current. |
| Taylor Grant | `WFShellBoard`, board mode | Avatar "TG". Strip "Sakura Heights · Community Manager". Role manager, 1 community, so no "My Communities". "Add community" is current. |
| Morgan Ellis | `WFShellBoard`, board mode (an old tab) | Avatar "ME". Strip "Old Mill Commons · Community Manager". Role manager, 2 communities. "Add community" is current. |
| Lee Morales | `WFShellBoard`, board mode | Avatar "LM". Strip "Sakura Heights · Board Member". Role board, 1 community. "Community Home" is current. "Add community" is disabled with 🔒 (spec 025 FR-040). |

- Brief 01 places "Add community" (and "Community settings") in board-mode nav, and draws the top bar for people with no home. Use brief 01's version if section 8 has it. If not, add both entries as ungrouped items after "Community Home", and add a `.wf-note`: "position set by brief 01".
- Keep the violet banner unchanged on every board-mode artboard: dark ink on violet (spec 025 FR-021, FR-038).

### 4.3 Artboard list

Times. Most artboards show "now", Oct 12, 2026 (conventions §4). Two flows happened earlier, so their artboards are drawn as of that day: Robin added Keystone Park on Oct 11, 2026, and Jordan added Keystone Ridge on Oct 9, 2026 (conventions §4.3). Nothing on the form shows the date. Only the future-date check depends on it.

**Company Administrator: Robin Hale**

**8.03.a Add community · empty · Company Administrator**

- Oct 11, 2026. Robin opened Add community from the portfolio.
- Every field is empty. Parent reads "None". The counter reads "0 / 2,000".
- The Company Administrator intro and the defaults note are shown.
- State: default.

**8.03.b Add community · parent list “Keystone” · Company Administrator**

- Oct 11, 2026. The fields hold Keystone Park's values: "Keystone Park HOA, Inc.", "Keystone Park", "Hamilton", "03/14/2004", "11/01/2026". Description empty.
- The parent list is open. Its search holds "Keystone". It shows, in order:
  - "None — not a sub-association" (Selected)
  - "Keystone Crossing Master" (keyboard focus is here)
  - "Keystone Crossing SF"
  - "Keystone Crossing TH"
  - "Keystone Ridge" with an "Onboarding" chip
- Keystone Park isn't listed, because it doesn't exist yet. Robin leaves the parent as "None" (US1 #1).
- This artboard shows that Keystone Crossing Master is offered (US1 #7) and that an Onboarding community can be a parent (FR-016).

**8.03.c Add community · parent list “Old Mill” · no match · Company Administrator**

- Same as 8.03.b, but the search holds "Old Mill".
- In place of results: "No community matches “Old Mill”. Archived communities can't be parents." "None — not a sub-association" stays above it.
- Old Mill Commons and Old Mill Commons Villas are Archived, so they never appear (FR-016, `PARENT_ARCHIVED`).

**8.03.d Add community · required fields missing · Company Administrator**

- Oct 11, 2026. Robin pressed "Add community" with every field empty.
- The error summary is at the top, with the heading "The community wasn't added" and five links, in field order (§5).
- Each required field shows its message under the field: legal name, community name, county, formation date, management start date.
- Description and parent show no error.
- Focus is on the summary's heading (§7).
- State: error.

**8.03.e Add community · formation date in the future · Company Administrator**

- Oct 11, 2026. Keystone Park's values, except formation date "11/01/2026": Robin typed the management start date in the wrong field.
- The summary has one link: "Formation date: Formation date can't be in the future."
- The same message is under Formation date. Every other field keeps its value and shows no error.
- State: error.

**8.03.f Add community · name taken · Company Administrator**

- Oct 12, 2026. Keystone Park was added yesterday. Robin starts to add it again, with Keystone Park's values, and types the community name as "keystone park " (lower case, with a trailing space). Add a `.wf-note`: "typed with a trailing space".
- The server refused it with `COMMUNITY_NAME_TAKEN` (US1 #2). The summary has one link, and the same message is under Community name: "Keystone Park is already the name of another community. Choose a different community name."
- The message shows the other community's name as stored, "Keystone Park", not what Robin typed.
- All input is kept. Nothing was created.
- State: refusal.

**8.03.g Add community · name taken by an archived community · Company Administrator**

- Same as 8.03.f, but the community name is "old mill commons". The other fields keep Keystone Park's values; only the message matters here.
- Message, in the summary and under Community name: "Old Mill Commons is the name of an archived community. Archived communities keep their names. Choose a different community name." (FR-007).
- State: refusal.

**8.03.h Community settings · Keystone Park just added · Company Administrator**

- Oct 11, 2026, right after 8.03.b was saved with parent "None".
- Company-area frame. Breadcrumb "Portfolio › Keystone Park".
- **Header** (conventions §7.2): title "Keystone Park", the chip "Onboarding", and "Keystone Park HOA, Inc." under it. No "Sub-association of" line, because it has no parent. The status actions for Onboarding: "Mark Active" and "Archive", as brief 04 draws them.
- **Section nav**: Profile (current) · Managers and memberships · Architectural review. No Notifications.
- **Success notice** at the top of Profile, with a "Dismiss" button (§5). Its second, smaller line says the architectural review settings were copied from the company defaults (FR-009).
- **Prompt card** (`wf-card lav`) under it: the title "No manager yet — appoint one", one line of body text, and the button "Appoint a manager" (§5).
- **Profile values**: Legal name "Keystone Park HOA, Inc."; Community name "Keystone Park"; County "Hamilton"; Formation date "Mar 14, 2004"; Management start date "Nov 1, 2026"; Description "—". Brief 04 decides whether Profile is a read view with an Edit button or an open form.
- State: success.

**Community Manager: Jordan Blake and Taylor Grant**

**8.03.i Add community · empty · Community Manager**

- Oct 9, 2026. Jordan, in board mode, opened "Add community" from the nav.
- Every field is empty. Parent reads "None".
- The Community Manager intro, the callout, and the defaults note are shown. The parent helper text is the Community Manager's.
- State: default.

**8.03.j Add community · parent list · Community Manager**

- Oct 9, 2026. The fields hold Keystone Ridge's values: "Keystone Ridge HOA, Inc.", "Keystone Ridge", "Durham County, NC", "06/02/2011", "12/01/2026". Description empty.
- The parent list is open, with an empty search. It shows exactly:
  - "None — not a sub-association" (Selected)
  - "Keystone Crossing Master"
  - "Keystone Crossing SF"
  - "Keystone Crossing TH"
  - "Sakura Heights"
- No other community appears: not Briar Hollow, Old Mill Commons or any filler community. Jordan leaves "None".

**8.03.k Add community · parent list · manages one community · Community Manager**

- Oct 12, 2026. Taylor Grant manages only Sakura Heights. That membership ends Dec 31, 2026, but it is effective today, so Taylor may add a community (FR-006).
- Fields empty. The parent list is open and shows exactly "None — not a sub-association" (Selected) and "Sakura Heights" (US1 #8).
- The callout reads as for Jordan. Taylor's new membership would have no end date, although Taylor's Sakura Heights membership has one.

**8.03.l Add community · name taken · Community Manager**

- Oct 12, 2026. Jordan doesn't manage Keystone Park. Jordan starts to add it with Keystone Park's values and the community name "Keystone Park".
- The server refused it with `COMMUNITY_NAME_TAKEN` (US1 #3). The summary has one link, and the same message is under Community name: exactly "That community name is unavailable."
- Nowhere on the page is there a name, a status, a link or any other hint about the existing community. There is no "archived" wording either.
- All input is kept. Nothing was created.
- State: refusal.

**8.03.m Community settings · Keystone Ridge just added · Community Manager**

- Oct 9, 2026, right after 8.03.j was saved with parent "None".
- **Frame**: board mode. The active community is now Keystone Ridge. Strip "Keystone Ridge · Community Manager" with an "Onboarding" chip after the name. `boardNav()` with 5 communities, so "My Communities" shows. Design choice, see §4.4 question 1.
- **Header**: title "Keystone Ridge", chip "Onboarding", "Keystone Ridge HOA, Inc." under it. No status actions and no Change parent: Community Managers never see them (FR-010, FR-016).
- **Section nav**: Profile (current) · Managers and memberships · Architectural review.
- **Success notice** at the top, with "Dismiss" (§5): Jordan is its Community Manager with no end date; it's waiting for a Company Administrator to mark it Active; every Company Administrator was emailed; settings were copied from the company defaults.
- **Onboarding notice** under it, a banner that stays until the community is Active (FR-029, conventions §7.2).
- **Profile values**: Legal name "Keystone Ridge HOA, Inc."; Community name "Keystone Ridge"; County "Durham County, NC"; Formation date "Jun 2, 2011"; Management start date "Dec 1, 2026"; Description "—". Editable by Jordan as brief 04 draws it (FR-017).
- No "No manager" prompt: Jordan is an open-ended manager.
- `.wf-note`: "Keystone Ridge is now in Jordan's switcher and My Communities, with “Onboarding” (brief 01)."
- State: success.

**Refusals for people who may not add**

**8.03.n Add community · refused · only managed communities archived · Community Manager**

- Sep 30, 2026. Morgan Ellis had Add community open in an old tab and had typed Keystone Park's values. (The company took on Keystone Park; Robin added it on Oct 11.)
- Robin had archived Old Mill Commons Villas on Sep 29 and Old Mill Commons earlier on Sep 30, so Morgan holds no effective manager membership in an Onboarding or Active community (US1 #6).
- Morgan pressed "Add community". The server refused it with 403 `FORBIDDEN`.
- The banner at the top of the form reads "You don't have permission to do that." No summary links, no field messages. All input is kept. Nothing was created.
- The banner names nothing from the request. A Community Manager whose chosen parent is refused (US1 #8) sees this same banner, word for word.
- State: refusal.

**8.03.o Add community requested directly · refused · Board Member**

- Oct 12, 2026. Lee Morales, a Board Member of Sakura Heights and nothing else, typed the Add community address.
- The server refused it with 403 `FORBIDDEN` (US1 #5). Lee lands on Lee's board-mode landing, Sakura Heights' Community Home, with the toast "You don't have access to that page." (conventions §6.1). If brief 01 draws a different landing for Lee, use it.
- The body is a placeholder: the title "Sakura Heights at a glance" and `wf-ph` blocks. Only the toast and the nav matter.
- The nav shows "Add community" disabled, with 🔒 and the screen-reader text "Not available to your role" (spec 025 FR-040).
- No form content appears, not even briefly.
- Accountants get the same. Residents with no board role never see board mode; a direct request lands them on their own landing page with the same toast. Not drawn.
- State: refusal.

### 4.4 Open questions (DCPostIt by 8.03.a)

> 1. **Where a Community Manager lands after adding.** Drawn (8.03.m): board mode switches to the new community and opens its Community settings. Other option: stay in the current community and show "Keystone Ridge added" with a link to its settings. The spec only says it appears in the switcher on the next request.
> 2. **Success message.** Drawn: a notice on the new page that stays until dismissed. Other option: a toast "Keystone Park added." only, with the rest left to the page.
> 3. **"No manager yet — appoint one."** Drawn on the just-added page. Should it stay on Profile until a manager accepts (brief 04 and 05 decide), or show only once?
> 4. **Cancel with typed input.** Drawn: Cancel leaves without asking. Other option: ask "Discard this community?" first.
> 5. **Archived name, Company Administrator.** Should the message also link to that community's settings, where Restore is? The spec doesn't ask for it.
> 6. **Parent archived before save, Community Manager.** If a parent the manager chose is archived before they save, the spec doesn't say whether they get `PARENT_ARCHIVED` or `COMMUNITY_ARCHIVED` (FR-004a). Not drawn.
> 7. **Over-long text.** Drawn rule: fields don't cut text off; the counter shows "2,014 / 2,000" and the save shows the error. Other option: the field stops at the limit.

### 4.5 States not drawn

Their copy is in §5. Each looks like a drawn artboard with a different message.

- **Saving.** The primary button reads "Saving…" and is disabled. Everything else stays as it was.
- **Save failed** (network or server error): banner "Couldn't save. Check your connection and try again." Input kept. Placed like 8.03.n's banner.
- **Rate limited** (FR-041): "Too many requests. Wait a few minutes, then try again." Inline, next to the buttons.
- **Length errors**: legal name over 200, community name of 1 or over 80 characters, description over 2,000. Shown like 8.03.e.
- **Invalid date**, such as 13/45/2004. Design choice, because the conventions give no message: "Enter a real date as MM/DD/YYYY."
- **Parent archived after the list loaded**, Company Administrator: inline under Parent association, "Old Mill Commons is archived, so it can't be a parent." (conventions §6.1, `PARENT_ARCHIVED`).
- **Community Manager parent search with no match**: "None of your communities match “Oak”."
- **Read-only and confirmation states:** none. The form has no read-only view, and the spec asks for no confirmation before adding (conventions §7.4 doesn't list Add community).

## 5. Copy

Exact strings. Quotes mark visible text. "SR" means text for screen readers only. Messages from conventions §6 are quoted as they are there.

**Page**

| Element | Company Administrator | Community Manager |
| --- | --- | --- |
| Breadcrumb | "Portfolio › Add community" | None |
| Title | "Add community" | "Add community" |
| Intro | "Add a community that Juniper Row Community Management has taken on. It starts as Onboarding: residents can't see it until a Company Administrator marks it Active. It has no Community Manager until one is appointed." | "Add a community that Juniper Row Community Management has taken on." |
| Callout | None | "You'll be its first Community Manager, with no end date. It starts as Onboarding: residents can't see it until a Company Administrator marks it Active. When you add it, we'll email every Company Administrator that it's waiting." |
| Nav entry | "Add community" (company-area left nav) | "Add community" (board-mode nav) |

**Fields**

| Element | Text |
| --- | --- |
| Labels | "Legal name (required)" · "Community name (required)" · "County (required)" · "Formation date (required)" · "Management start date (required)" · "Description (optional)" · "Parent association (optional)" |
| Helper, legal name | "Up to 200 characters." |
| Helper, community name | "The name people see in the app. 2 to 80 characters. No other community can have this name, even with different capital letters." |
| Helper, formation date | "MM/DD/YYYY. Can't be in the future." |
| Helper, management start date | "MM/DD/YYYY. When Juniper Row Community Management starts managing it. This can be a future date." |
| Helper, description | "Up to 2,000 characters." |
| Counter | "0 / 2,000" (SR: "0 of 2,000 characters used") |
| Date picker buttons (SR) | "Choose formation date" · "Choose management start date" |
| Helper, parent, Company Administrator | "Choose one if this community is a sub-association of a master association. Archived communities aren't listed. A parent doesn't give anyone access to this community." |
| Helper, parent, Community Manager | "Choose one if this community is a sub-association of a master association. Only communities you manage are listed. A parent doesn't give anyone access to this community." |
| Parent value, none chosen | "None" |
| Parent list search | Label "Search communities" · placeholder "Type a community name" |
| Parent list first option | "None — not a sub-association" |
| Current value marker | "✓ Selected" |
| Onboarding chip in the list | "Onboarding" |
| No match, Company Administrator | "No community matches “Old Mill”. Archived communities can't be parents." |
| No match, Community Manager | "None of your communities match “Oak”." |
| Result count (SR, polite) | "4 communities match." |

**Defaults note and buttons**

| Element | Text |
| --- | --- |
| Defaults note (FR-009) | "Architectural review settings are copied from the company defaults when you add the community. Later changes to the company defaults don't change them." |
| Primary button | "Add community" |
| Busy | "Saving…" |
| Other button | "Cancel" |

**Errors** (conventions §6.3)

| Element | Text |
| --- | --- |
| Summary heading | "The community wasn't added" |
| Summary item | "<field label without “(required)”>: <message>". Example: "Legal name: Enter the legal name." Each item is a link to its field. |
| Legal name | "Enter the legal name." · "Legal name can be at most 200 characters." |
| Community name | "Enter a community name." · "Community name must be 2 to 80 characters." |
| County | "Enter the county." |
| Formation date | "Enter the formation date." · "Formation date can't be in the future." |
| Management start date | "Enter the management start date." |
| Description | "Description can be at most 2,000 characters." |
| Invalid date (design choice) | "Enter a real date as MM/DD/YYYY." |

8.03.d's summary lists, in this order:

1. "Legal name: Enter the legal name."
2. "Community name: Enter a community name."
3. "County: Enter the county."
4. "Formation date: Enter the formation date."
5. "Management start date: Enter the management start date."

**Refusals** (conventions §6)

| Case | Text | Where |
| --- | --- | --- |
| `COMMUNITY_NAME_TAKEN`, Company Administrator | "Keystone Park is already the name of another community. Choose a different community name." | Summary and under Community name. |
| `COMMUNITY_NAME_TAKEN`, Company Administrator, archived community | "Old Mill Commons is the name of an archived community. Archived communities keep their names. Choose a different community name." | Summary and under Community name. |
| `COMMUNITY_NAME_TAKEN`, Community Manager | "That community name is unavailable." Exactly this. Nothing added. | Summary and under Community name. |
| `PARENT_ARCHIVED`, Company Administrator (not drawn) | "Old Mill Commons is archived, so it can't be a parent." | Under Parent association. |
| `FORBIDDEN`, on save (US1 #6, US1 #8) | "You don't have permission to do that." | Banner at the top of the form. |
| `FORBIDDEN`, page requested (US1 #5) | "You don't have access to that page." | Toast on the person's landing page. |
| Rate limited (not drawn) | "Too many requests. Wait a few minutes, then try again." | Next to the buttons. |
| Save failed (not drawn) | "Couldn't save. Check your connection and try again." | Banner at the top of the form. |

**After a save: Company Administrator (8.03.h)**

| Element | Text |
| --- | --- |
| Breadcrumb | "Portfolio › Keystone Park" |
| Header | "Keystone Park" · chip "Onboarding" · "Keystone Park HOA, Inc." · buttons "Mark Active", "Archive" |
| Header line when there is a parent (not in 8.03.h) | "Sub-association of Keystone Crossing Master" |
| Success notice, line 1 | "Keystone Park was added as Onboarding. Residents can't see it until it's marked Active." |
| Success notice, line 2 (smaller) | "Its architectural review settings were copied from the company defaults." |
| Dismiss button | "Dismiss" (SR: "Dismiss message") |
| Prompt title | "No manager yet — appoint one" |
| Prompt body | "Keystone Park can't be marked Active until it has a Community Manager with no end date." |
| Prompt button | "Appoint a manager" (SR: "Appoint a manager for Keystone Park") |
| Section nav | "Profile" · "Managers and memberships" · "Architectural review" |
| Empty description | "—" (SR: "No description") |

**After a save: Community Manager (8.03.m)**

| Element | Text |
| --- | --- |
| Strip | "Keystone Ridge · Community Manager" with the chip "Onboarding" |
| Header | "Keystone Ridge" · chip "Onboarding" · "Keystone Ridge HOA, Inc." |
| Success notice, line 1 | "Keystone Ridge was added. You're its Community Manager, with no end date." |
| Success notice, line 2 | "It's waiting for a Company Administrator to mark it Active. We've emailed every Company Administrator about it." |
| Success notice, line 3 (smaller) | "Its architectural review settings were copied from the company defaults." |
| Onboarding notice (FR-029; conventions §7.2) | "Keystone Ridge is being set up. A Company Administrator must mark it Active. Resident invitations are sent then." |
| Dismiss button | "Dismiss" (SR: "Dismiss message") |

**Board Member refusal (8.03.o)**

| Element | Text |
| --- | --- |
| Toast | "You don't have access to that page." |
| Locked nav entry | "Add community" with 🔒 (SR: "Not available to your role") |
| Landing title | "Sakura Heights at a glance" |

## 6. Interactions

**Opening the form.**

- Company Administrator: the left-nav entry "Add community", or the portfolio's "Add community" button (brief 02).
- Community Manager: the board-mode nav entry "Add community" (FR-036). The page doesn't belong to the active community. The strip shows where they came from.
- Focus starts at the page title, as on any page. The first Tab goes to Legal name.

**Fields.**

- Nothing is checked while typing, except the description counter, which updates as the person types.
- The community name is checked against other communities only on save, by the server. Design choice: a check while typing would make it faster to probe which names exist.
- Dates can be typed as MM/DD/YYYY or picked. The calendar button opens a date picker. Esc closes it and focus returns to the button.

**Parent association.**

- The list opens when the person types in the field, presses Alt+Down, or selects the field's button.
- Typing filters by community name, ignoring case, and matches part of a word.
- Up and Down move through the options. Enter chooses one and closes the list. Esc closes the list without changing the value.
- "None — not a sub-association" clears the parent.
- The Company Administrator's list holds every Onboarding or Active community. The Community Manager's list holds only the Onboarding or Active communities they manage (FR-016). Someone who is both gets the Company Administrator's list (spec Edge Cases: the union).
- Choosing a parent changes nobody's access. A Board Member of the parent gets nothing in the new community (US1 #7, spec 025). The helper text says so.
- `PARENT_CYCLE` can't happen here: a new community has no sub-associations yet.

**Saving ("Add community").**

1. The form first checks required fields, lengths, date format and that the formation date isn't in the future. If any fail, nothing is sent. The error summary appears at the top and takes focus, and each message appears under its field. Input is kept.
2. Otherwise the button reads "Saving…" and is disabled, so a second press can't send it twice.
3. The server decides. On success the person goes to the new community's Community settings page (below).
4. On a refusal the form stays, with the input kept:
   - `COMMUNITY_NAME_TAKEN`: in the summary (which takes focus) and under Community name.
   - `FORBIDDEN`: the banner at the top, announced at once.
   - Rate limit: the message next to the buttons.
   - Any other failure: the "Couldn't save" banner.
5. Messages update on the next save. Design choice; the other option clears a field's message as soon as it is edited.

There is no confirmation dialog before adding. The spec and conventions §7.4 don't ask for one.

**After success: Company Administrator.**

- Robin lands on Keystone Park's Community settings page, at Profile, with the breadcrumb "Portfolio › Keystone Park". Focus moves to the title "Keystone Park". The success notice is announced politely.
- "Appoint a manager" opens Managers and memberships at its Managers area (brief 05). Focus moves to that area's heading.
- "Dismiss" removes the notice. Focus moves to the page title.
- The portfolio now lists Keystone Park as Onboarding with "No manager" (brief 02).
- "Mark Active" and "Archive" behave as brief 04 draws them. Mark Active is refused with `NO_ACTIVE_MANAGER` until an open-ended manager exists (FR-010).

**After success: Community Manager.**

- In the same action, Jordan gets a Community Manager membership in Keystone Ridge with no end date, and every Company Administrator is emailed (FR-008; brief 12 draws the email).
- Jordan lands on Keystone Ridge's Community settings page, at Profile, in board mode with Keystone Ridge as the active community (design choice, §4.4 question 1). Focus moves to the title. The success notice is announced politely.
- Keystone Ridge is in Jordan's switcher and "My Communities", with "Onboarding" (US1 #4; brief 01).
- The Onboarding notice stays until a Company Administrator marks it Active. Resident invitations Jordan sends before then are saved as Scheduled and sent at activation (FR-024b; brief 06).

**After a refusal.**

- 8.03.n: Morgan stays on the form. On the next page load, the app takes Morgan to Morgan's landing page (FR-036a; brief 11).
- 8.03.o: the app never shows the form. It takes Lee to Lee's landing page and shows the toast.

**Cancel.**

- Goes back without saving and without asking: to the portfolio for a Company Administrator; to the board page they came from for a Community Manager. Design choice, §4.4 question 4.

**Who sees the entry.**

- The company area, with its "Add community", is shown only to Company Administrators (FR-035, FR-036).
- In board mode, "Add community" works for a Community Manager. For a Board Member or Accountant it is shown disabled with a lock (spec 025 FR-040). Brief 01 owns the nav.
- Hiding is for clarity only; the server decides. 8.03.n and 8.03.o show what happens when the form or page is reached anyway.

## 7. Accessibility

The conventions §8 baseline applies. For this form:

- **Headings.** One `h1`, "Add community". The error summary heading is the first thing in the form when shown.
- **Labels.** Every field has a visible label above it. "(required)" and "(optional)" are part of the label text, so screen readers read them.
- **Helper text and errors** are tied to their field, so they are read with it. A field with an error is marked invalid. The message is text, not only a red border.
- **Error summary.** On a failed save it appears at the top and takes focus. It is a list of links. Each link moves focus into its field. The summary is announced at once.
- **Refusal banner** (8.03.n): announced at once when it appears. Focus moves to it.
- **Keyboard order:** nav → breadcrumb (Company Administrator) → callout text (Community Manager, read in order, not focusable) → Legal name → Community name → County → Formation date → its calendar button → Management start date → its calendar button → Description → Parent association → "Add community" → "Cancel".
- **Dates.** Typing alone works; the picker is optional. The format is in the helper text, not only in a placeholder. The calendar buttons have names that say which date they set.
- **Counter.** It shows "0 / 2,000" and is read as "0 of 2,000 characters used". It is announced politely when typing pauses, and when the limit is passed.
- **Parent list.** It works as a combobox: the field reports whether the list is open; Up and Down move; Enter chooses; Esc closes. Each option reads its name and, where shown, "Onboarding". The selected option reads "Selected". The number of matches is announced politely ("4 communities match.").
- **Busy state.** "Saving…" is announced. The disabled button can't be pressed twice.
- **Success page.** Focus moves to the new page's title. The success notice is announced politely. "Dismiss" and "Appoint a manager" have names that make sense out of context (§5). Focus is never lost when the notice is dismissed.
- **Toast** (8.03.o): announced politely. It doesn't take focus. Nothing in it needs action.
- **Words, not colour.** "Onboarding", errors, notices and the lock all carry words. The 🔒 has the text "Not available to your role".
- **Contrast.** 4.5:1 for helper, error and notice text; 3:1 for field borders and the focus outline. The board-mode banner stays dark ink on violet (spec 025 FR-038).
- **Zoom.** At 200% the two date fields stack, and the form stays one column with no sideways scrolling.

## 8. Out of scope for this design

- Choosing a status on the form. Every new community is Onboarding (FR-008).
- Appointing or inviting a manager, or anyone else, from the form (brief 05).
- Marking Active, archiving, restoring and Change parent (brief 04). `PARENT_CYCLE` is not reachable on add.
- Editing the Profile after it is added (brief 04).
- The portfolio (brief 02), the board-mode nav placement, switcher and "My Communities" (brief 01).
- The email to Company Administrators when a Community Manager adds a community (brief 12).
- The Architectural review section and the Company defaults page (briefs 07 and 08). The form only notes that the defaults are copied.
- Loading properties. A new community has no properties until an engineer loads them (spec Assumptions; brief 06 draws the empty Residents area).
- Unique legal names. The spec makes only the community name unique, so the form doesn't warn about a repeated legal name.
- Bulk add or import of communities.
- A "type the name to confirm" step, or any confirmation before adding.
- A view of the form for Residents, Board Members or Accountants.
- Phone and tablet artboards beyond the main page's two (conventions §2).
- Notifications: nothing about it appears here.

## 9. Design review checklist

- [ ] **US1 #1.** 8.03.b holds Keystone Park's values with parent "None". 8.03.h shows "Keystone Park", "Onboarding", "Keystone Park HOA, Inc.", no "Sub-association of" line, "No manager yet — appoint one", and that the settings were copied from the company defaults.
- [ ] **US1 #2.** 8.03.f: "keystone park " is refused with "Keystone Park is already the name of another community. Choose a different community name.", in the summary and under Community name. Input kept.
- [ ] **US1 #3.** 8.03.l: Jordan sees exactly "That community name is unavailable." No name, status, link or hint about the other community appears anywhere.
- [ ] **US1 #4, before.** 8.03.i's callout says Jordan will be the first Community Manager, with no end date, and a Company Administrator must mark it Active.
- [ ] **US1 #4, after.** 8.03.m shows "Onboarding", "You're its Community Manager, with no end date.", "It's waiting for a Company Administrator to mark it Active.", that every Company Administrator was emailed, and Keystone Ridge in the strip with "Onboarding".
- [ ] **US1 #5.** 8.03.o: Lee lands on board mode with "You don't have access to that page." "Add community" is disabled with a lock. No form content shows.
- [ ] **US1 #6.** 8.03.n: Morgan's save is refused with "You don't have permission to do that." Input kept. Nothing names anything from the request.
- [ ] **US1 #7.** 8.03.b offers "Keystone Crossing Master". The parent helper text says a parent doesn't give anyone access. §5 has "Sub-association of Keystone Crossing Master".
- [ ] **US1 #8.** 8.03.k lists only "None — not a sub-association" and "Sakura Heights". 8.03.j lists only Jordan's four communities. A refused parent gets 8.03.n's banner, word for word.
- [ ] **US1 #9.** 8.03.d's summary names all five required fields, in field order, and each field shows its message. 8.03.e shows "Formation date can't be in the future."
- [ ] **FR-006.** The seven fields are in this order, each label says "(required)" or "(optional)", the helper text states each rule, and the description has a "0 / 2,000" counter.
- [ ] **FR-007.** 8.03.g's archived message: "Old Mill Commons is the name of an archived community. Archived communities keep their names. Choose a different community name."
- [ ] **FR-008.** 8.03.h has the "No manager yet — appoint one" card with "Appoint a manager". 8.03.m has no such card.
- [ ] **FR-009.** Every form artboard has the defaults note. 8.03.h and 8.03.m say the settings were copied.
- [ ] **FR-016.** No Archived community appears in any parent list. 8.03.c shows "No community matches “Old Mill”. Archived communities can't be parents." Onboarding parents carry the "Onboarding" chip.
- [ ] **FR-029.** 8.03.m shows "Keystone Ridge is being set up. A Company Administrator must mark it Active. Resident invitations are sent then."
- [ ] **FR-036.** Robin's form is in the company area with no violet banner. Jordan's and Taylor's are in board mode with the violet banner.
- [ ] **FR-042.** Every message on 8.03.d–g names its field and says why. The only messages that don't explain are "That community name is unavailable." and the two `FORBIDDEN` messages.
- [ ] **Permission variants.** Community Manager artboards have no status actions, no Change parent and no breadcrumb to the portfolio. Company Administrator artboards have "Mark Active" and "Archive" on 8.03.h.
- [ ] **No extras.** No status field, manager field, confirmation dialog, live name check or link to Company defaults.
- [ ] **Open questions.** A DCPostIt by 8.03.a lists the seven questions in §4.4. Every design choice drawn is labelled "Design choice".
- [ ] **Accessibility.** Labels sit above fields; the summary takes focus and links to fields; date picker buttons and the parent list work by keyboard; announcements are noted on the artboards or in notes.
- [ ] **Data.** Only conventions §4 people and communities appear. Dates follow §4.3 (Oct 11 for Robin's add, Oct 9 for Jordan's, Sep 30 for Morgan, Oct 12 otherwise). No names from sections 1–7 are reused.
- [ ] **Canvas.** Labels and ids match §1 exactly (s8-03a to s8-03o). Sections 1–7 render as before.
