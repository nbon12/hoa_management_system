# 04 — Community settings page, Profile and status

This brief is for the Community settings page frame, its Profile section, the status actions and their dialogs (Mark Active, Archive, Restore, Change parent), and the archived read-only view. §1 is the prompt to paste into Claude Design. The other sections hold the details the prompt points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file.

## 1. Prompt for Claude Design

```text
Project "HOA Management CRM", canvas "NekoHOA Wireframes.html". Attached: spec.md (spec 030, the authority), 00-conventions.md (rules, dataset, vocabulary, refusal copy) and this brief, 04-community-settings-profile-status.md. spec.md wins, then the conventions. Brief §4 has each artboard's data and states, §5 the exact copy, §6 the interactions.

Add these to section "8 · Company administration" (create it after section 7 if missing, conventions §3). Ids s8-04a to s8-04z. Pages are 1360 wide; a dialog is drawn over its dimmed page, at the page's size. Shared section-8 frames and data go in wf-company.jsx; these artboards in wf-company-settings.jsx.

8.04.a Community settings · Profile · Option A tabs · Company Administrator
8.04.b Community settings · Profile · Option B side list · Company Administrator
8.04.c Profile · editing · Option A one Save · Community Manager
8.04.d Profile · editing · Option B save per field · Community Manager
8.04.e Profile · required field cleared · Community Manager
8.04.f Profile · Onboarding notice · Community Manager
8.04.g Profile · name taken · Community Manager
8.04.h Profile · missing required fields · Community Manager
8.04.i Profile · Onboarding · Company Administrator
8.04.j Mark Active dialog · Keystone Ridge
8.04.k Profile · marked Active · Company Administrator
8.04.l Mark Active dialog · no manager · Option A · Keystone Park
8.04.m Profile · Mark Active disabled · Option B · Company Administrator
8.04.n Archive dialog · Old Mill Commons · Sep 30
8.04.o Archive dialog · open work · Keystone Crossing Master
8.04.p Profile · Archived · read-only · Company Administrator
8.04.q Managers and memberships · Archived · read-only · Company Administrator
8.04.r Architectural review · Archived · read-only · Company Administrator
8.04.s End membership dialog · Archived · Morgan Ellis
8.04.t Restore dialog · returns to Active · Old Mill Commons
8.04.u Restore dialog · Morgan Ellis ended · returns to Onboarding · Old Mill Commons
8.04.v Restore dialog · parent archived · Old Mill Commons Villas
8.04.w Change parent dialog · cycle · Keystone Crossing Master
8.04.x Change parent dialog · archived parent · Keystone Crossing Master
8.04.y Community settings · not their community · refused · Community Manager
8.04.z Community settings · refused · Board Member

Build the page frame once; briefs 05–07 reuse it. Company Administrator pages use the company-area frame (conventions §7.1). Community Manager pages use WFShellBoard + boardNav() with Jordan Blake's data. Reuse BoardOverview's field grid and sub-associations list, and wf-card, wf-field, wf-pill, wf-btn. Sections 1–7 must render as before.

Pairs a/b, c/d and l/m are alternatives: draw both. Other artboards use Option A. Put brief §4.3's design choices and open questions on DCPostIts.

Lofi, in section 7's visual language. Layout, copy, states and who sees what are binding; colour and spacing are placeholders. Statuses and flags are words, never colour alone.

Don't draw: Notifications in any form, a type-the-name step, status actions or Change parent for a Community Manager, archived-community actions besides Restore and End membership, links to architectural applications, a change history, phone layouts, or details briefs 05–07 own.

When done, open every artboard and check that it renders without console errors, its data matches brief §4 and its copy matches §5 word for word. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

- **Company Administrators** (Robin Hale). They open any community's Community settings page from the portfolio, including Onboarding and Archived ones that are in no switcher (FR-029). They hold no membership in most of these communities (US5 #1). They use the page to:
  - fill in or fix a community's profile, including its parent (FR-016, FR-017);
  - take an Onboarding community live with **Mark Active** (FR-010);
  - **Archive** a community the company stops managing, knowing exactly what it will cancel, email and revoke (FR-012–FR-014);
  - **Restore** an archived community, and choose which memberships to end first (FR-010, FR-015);
  - read an archived community's records, read-only (FR-012, US5 #7).
- **Community Managers** (Jordan Blake). They reach the page from the "Community settings" entry in board mode, for the active community (FR-029). They edit the profile but never the status or the parent (FR-017, US3 #20). While their community is Onboarding, the page tells them that a Company Administrator must mark it Active (FR-029).
- **Board Members, Accountants and Residents** (Lee Morales) are refused the page and taken to their landing page (FR-032, US5 #3).

The page frame drawn here (header, status actions, section nav, archived banner) is the frame briefs 05–07 draw inside.

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| US3 #3 | Marking an Onboarding community Active, when it has an open-ended Community Manager and two scheduled resident invitations, makes it Active and emails both invitations. The dialog names the 2 invitations (lena@example.com, omar@example.com) and when their links stop working. Afterwards the chip reads "Active". | 8.04.j, 8.04.k |
| US3 #4 | Mark Active on an Onboarding community with no open-ended Community Manager is refused with `NO_ACTIVE_MANAGER`, and it stays Onboarding. Keystone Park has only a pending invitation, which doesn't count. | 8.04.l (Option A), 8.04.m (Option B) |
| US3 #5 | Archiving revokes the pending manager invitation and the pending resident invitation, and their links stop working. The community leaves every switcher. Its profile, settings and Managers and memberships stay readable to the Company Administrator. Nothing is deleted. | 8.04.n (the consequences); 8.04.p, q, r (readable afterwards) |
| US3 #7 | Archiving cancels the 3 active autopay schedules and emails every linked resident once. The dialog states both counts before the person confirms. | 8.04.n |
| US3 #10 | Archive is refused with `OPEN_WORK`, and the message lists ARC-1041 and ARC-1042 with their statuses. | 8.04.o |
| US3 #11 | Archive of a master is refused with `OPEN_WORK` while a sub-association isn't archived, and the message lists it. | 8.04.o |
| US3 #12 | Restoring a community that was Active, with an open-ended manager, returns it to Active. Memberships and settings don't change. Autopay doesn't restart. Invitations revoked by the archive stay revoked. | 8.04.t |
| US3 #13 | A community archived while still Onboarding returns to Onboarding and stays hidden from residents. No dataset community fits, so this is the restore outcome line in §5, also listed on the DCPostIt beside 8.04.t. | §5 Restore dialog; DCPostIt by 8.04.t |
| US3 #14 | A restored community with no open-ended manager returns to Onboarding. Mark Active is then refused with `NO_ACTIVE_MANAGER`. 8.04.u shows that outcome and its reason. 8.04.l shows the refusal; Briar Hollow gets the same message with its name (§5). Brief 02 draws the portfolio's "No manager" flag. | 8.04.u, 8.04.l; §5 |
| US3 #15 | In an archived community, the Company Administrator ends Morgan Ellis's manager membership. The last-manager rule doesn't apply while archived. A later restore returns the community to Onboarding, and Morgan has no access. | 8.04.q (End membership offered), 8.04.s (it ends), 8.04.u (restore then returns to Onboarding; Morgan listed as ended) |
| US3 #16 | Restoring an archived sub-association whose parent is archived is refused with `PARENT_ARCHIVED`, naming the parent. | 8.04.v |
| US3 #17 | A Community Manager of an Active community edits the profile fields and saves. The toast reads "Profile saved." The log entry is not shown on screen. | 8.04.c, 8.04.d |
| US3 #18 | A Community Manager renames Keystone Ridge to " KEYSTONE PARK". It is refused with `COMMUNITY_NAME_TAKEN` as "That community name is unavailable.", and the name is unchanged. A Company Administrator would see the other community named (§5). | 8.04.g |
| US3 #19 | A backfilled community (Keystone Crossing TH): changing only the description saves, and the Profile section still flags the missing required fields. | 8.04.h |
| US3 #20 | A Community Manager never sees Mark Active, Archive, Restore or Change parent. Status and parent are plain text. If one is reached anyway, the server refuses with "You don't have permission to do that." (§5). | 8.04.c–h |
| US3 #21 | A Community Manager who opens another community's Community settings is refused with 403 `FORBIDDEN`, exactly as for a community that doesn't exist. The message names nothing. | 8.04.y |
| US5 #1 | A Company Administrator with no membership sees exactly Profile, Managers and memberships, and Architectural review. Profile is editable, and the status actions for the current status are in the header. The other sections are editable in briefs 05–07. "Reset architectural review to company defaults" is offered in brief 07's section (named in a note on 8.04.a). | 8.04.a, b, i, k |
| US5 #2 | A Community Manager sees the same three sections. Profile is editable except status and parent. Architectural review and Managers and memberships are briefs 07 and 05–06. | 8.04.c–h |
| US5 #3 | A Board Member who opens Community settings is refused with 403 `FORBIDDEN` and taken to their landing page. | 8.04.z |
| US5 #4 | Every save is logged with old and new values. Nothing on screen; the design adds no change-history view. | — |
| US5 #7 | An archived community's settings are shown read-only in every section, including Residents. Invitations revoked by the archive read "Revoked — community archived". Only Restore and End membership are offered. | 8.04.p, q, r |
| FR-010 | Three statuses, shown as text chips. The Company Administrator's header offers only the changes allowed from the current status: Onboarding → Mark Active, Archive; Active → Archive; Archived → Restore. Restore says whether the community returns to Active or Onboarding, and why. Restoring a master doesn't restore its sub-associations. | 8.04.a, i, k, l, m, p, t, u, v |
| FR-012 | Archived: out of every switcher; all writes refused except Restore and ending memberships; memberships kept but give no access; invitations revoked; every section readable, read-only, to Company Administrators; residents keep read access to payment history; nothing deleted. | 8.04.n, p, q, r, s |
| FR-013 | Archive cancels every active autopay schedule and emails every linked resident once. | 8.04.n |
| FR-014 | `OPEN_WORK` lists each item: architectural applications that are Open or Decision Reached, and sub-associations that aren't archived. | 8.04.o |
| FR-015 | Before restoring, the dialog lists the memberships that will give access again, with an option to end each one first. Memberships keep their dates. Autopay doesn't restart. Revoked invitations stay revoked. | 8.04.t, u |
| FR-016 | Change parent is Company Administrator only. Archived communities aren't offered. `PARENT_CYCLE` and `PARENT_ARCHIVED` are refused inline. The dialog says the parent link changes nobody's access. | 8.04.w, x; absent in 8.04.c–h |
| FR-017 | Community Manager: all six profile fields editable; status and parent read-only. Company Administrator: all six, plus parent (Change parent) and status (header actions). A filled required field can't be cleared. A field left empty by the spec 025 backfill may stay empty, and Profile flags it. | 8.04.a, c, d, e, h, i |
| FR-029 | One page per community, with sections Profile, Managers and memberships and Architectural review, and no Notifications. It works for Onboarding and Archived communities. Company Administrators come from the portfolio (breadcrumb). Community Managers come from board mode. A Community Manager of an Onboarding community sees the notice on Profile. | 8.04.a, b, f, i, p |
| FR-032 | A Board Member, Accountant or Resident is refused the page and taken to their landing page. | 8.04.z |
| FR-033 | Every settings change is logged with old and new values. Nothing on screen. | — |
| FR-042 | Every refusal says why in plain language. Denials that must not reveal a community say only "You don't have access to that page." | 8.04.e, g, l, m, o, v, w, x, y, z; §5 |

## 4. Artboards

"Now" is Monday, Oct 12, 2026, 10:00 AM EDT, except where an artboard says it is a snapshot. People and communities come from conventions §4. Every artboard is desktop width only.

### 4.1 The page frame (shared with briefs 05–07)

**Outer frame.**

- **Company Administrator:** the neutral company-area frame (conventions §7.1). Top bar with the account cluster and avatar "RH", and no board-mode control. Strip "Juniper Row Community Management · Company area". Left nav: Portfolio · Add community · Company defaults · Company administrators. "Portfolio" stays current while a community is open (brief 01, D6). No violet banner.
- **Community Manager:** `WFShellBoard`, with the violet banner unchanged (dark ink on violet). Avatar "JB". The strip shows the active community, as brief 01 draws it: "Sakura Heights · Community Manager", or "Keystone Ridge", then the "Onboarding" chip, then "· Community Manager". Nav from `boardNav()` with role manager and 5 communities. "Community settings" is current.

**Page header** (the same in every section of the page):

1. **Breadcrumb**, Company Administrator only: "Portfolio › Sakura Heights". A Community Manager has no breadcrumb; the nav entry is enough.
2. **Title row:** the community name as the page title (`wf-h1`), then its status chip: "Onboarding", "Active" or "Archived". For a Company Administrator, a "No manager" chip follows when the community has no open-ended Community Manager (design choice D6).
3. **Legal name** on the next line. When it is empty (spec 025 backfill): "Legal name: Missing — required".
4. **Parent line,** only when there is a parent: "Sub-association of Keystone Crossing Master".
5. **Status actions,** on the right of the title row, Company Administrator only. They change with the status:

   | Status | Actions, left to right |
   | --- | --- |
   | Onboarding | "Mark Active" (primary), "Archive" |
   | Active | "Archive" |
   | Archived | "Restore" (primary) |

   A Community Manager sees no status actions, not even disabled ones (US3 #20; conventions §7.10).

**Section nav,** directly under the header: "Profile" · "Managers and memberships" · "Architectural review", in that order. The current section is marked by more than colour (for example bold and an underline or a bar). **Notifications is not drawn**: no tab, no locked item, no placeholder (FR-029). Each section has its own address, so it can be opened directly (FR-029; brief 07 sends the old ARC settings location to "Architectural review", FR-030).

- **Option A (8.04.a):** tabs in a row across the top of the content, under the header.
- **Option B (8.04.b):** a short vertical list on the left of the content, beside the page's main nav.

**Archived banner.** In an Archived community, the first thing in every section is this banner (conventions §7.3): "Old Mill Commons is archived. Juniper Row Community Management no longer manages it. Everything here is read-only. Nothing has been deleted."

### 4.2 The Profile section

Top to bottom:

1. **Banners,** when they apply, in this order: the archived banner; the Onboarding notice (Community Manager only, FR-029); the missing-fields notice (when a required field is empty, FR-017).
2. **"Details" card.** `BoardOverview`'s field grid, as a list of labels and values: Legal name · Community name · County · Formation date · Management start date, then Description at full width. Dates read "Jun 9, 1998". An empty description reads "No description". An empty required field reads "Missing — required". Section 7's "Community GUID" line and the "Amenities" card are not drawn.
   - Option A (one Save per section): an "Edit profile" button on the card's header bar. It turns the grid into a form with "Save profile" and "Cancel".
   - Option B (save per field): each row has its own "Edit" button. Editing one row shows its input with "Save" and "Cancel".
3. **"Status and parent" card.**
   - "Status": the chip, then one sentence on what the status means (§5).
   - "Parent association": the parent's community name, or "None". For a Company Administrator, a "Change parent" button sits beside it (not in an Archived community).
   - "Sub-associations": each sub-association's community name with its status chip, or "None". Plain text, not links (open question Q5).
   - For a Community Manager, a last line: "Only a Company Administrator can change the status or the parent association."

**Who can do what on this page.**

| | Company Administrator | Community Manager |
| --- | --- | --- |
| Frame | Company area, breadcrumb | Board mode, "Community settings" current |
| Edit the six profile fields | Yes | Yes |
| Change parent | Yes ("Change parent" button) | No. Parent is plain text. |
| Status actions | Yes, for the current status | No. Status is plain text. |
| "No manager" chip in header | Yes (D6) | Not shown |
| Onboarding notice | Not shown. The status sentence says the same thing. | Shown on Profile (FR-029) |
| Name taken message | Names the other community | "That community name is unavailable." |
| Archived community | Opens read-only. Restore and End membership only. | Can't open it (FR-012). |

### 4.3 Design choices and open questions (each on a `DCPostIt`)

The spec doesn't decide these. Each post-it says "Design choice", names the drawn option and the others.

| # | Question | Drawn | Other options | Post-it beside |
| --- | --- | --- | --- | --- |
| D1 | Section nav: tabs across the top, or a list on the left? (conventions §7.2) | Option A tabs (8.04.a). Option B side list (8.04.b). Briefs 05–07 follow the owner's pick. | — | 8.04.a |
| D2 | Save: one Save per section, or save per field? (conventions §7.2) | Option A, "Edit profile" then "Save profile" (8.04.c). Option B, "Edit" on each row (8.04.d). Option A gives one error summary and one log entry per save. | — | 8.04.c |
| D3 | Mark Active when there is no open-ended manager. | Option A: "Mark Active" stays enabled, and the dialog shows the refusal (8.04.l). Option B: "Mark Active" is disabled, with the refusal text beside it (8.04.m). | — | 8.04.l |
| D4 | When a dialog already knows the action will be refused (open work, archived parent, no manager), does it say so on opening? | Yes. The dialog asks the server when it opens, for its counts and names (conventions §7.4). If the answer is a refusal, the dialog shows only the refusal and "Close". | Show the consequences first and refuse only after "confirm". A refusal after confirming (for example a race) looks the same either way. | 8.04.o |
| D5 | Restore's "end a membership first": how is the ending confirmed? | A step inside the restore dialog: the row's "End membership" swaps the dialog to a short confirmation with "End membership" and "Back", then returns to the refreshed restore view (copy in §5; result in 8.04.u). One dialog is open at a time. | Open the End membership dialog (8.04.s) on top of the restore dialog. | 8.04.t |
| D6 | "No manager" chip in the Community settings header. (conventions §5.4) | Shown to Company Administrators, after the status chip (8.04.l, 8.04.m). | Portfolio only. | 8.04.m |
| D7 | Change parent picker: are communities that would make a loop listed? Is "No parent" offered? | Both listed. Choosing a descendant is refused inline with `PARENT_CYCLE`. "No parent" is the first option. | Leave descendants out (then `PARENT_CYCLE` shows only from out-of-date data). Leave out "No parent", if a parent can't be removed (Q2). | 8.04.w |
| D8 | Missing required fields: per-field flags only, or also a notice at the top of Profile? | Both. The notice lists the empty fields. The words "Missing — required" are themselves a design choice (conventions §5.4). | Per-field flags only. | 8.04.h |
| D9 | Links inside refusal dialogs. | `NO_ACTIVE_MANAGER` offers "Open Managers and memberships". `PARENT_ARCHIVED` offers "Open Old Mill Commons". `OPEN_WORK` offers none, because a Company Administrator can't read architectural applications (FR-003). | No links. | 8.04.l |
| D10 | In an archived community, the Managers area's last-manager helper text would be wrong (FR-020 doesn't apply). | Replaced by "Memberships give no access while Old Mill Commons is archived. You can still end them." | Leave the helper out. | 8.04.q |

**Open questions** (one `DCPostIt` beside 8.04.a):

> - **Q1.** Should a Community Manager see the name of a parent or sub-association they don't manage? Spec 025's overview shows sub-associations. Spec 030's tenant boundary lists only the portfolio, the parent picker and "My Communities" as cross-community surfaces. In the dataset, Jordan Blake manages all three Keystone Crossing communities, so the drawn artboards don't need an answer.
> - **Q2.** Can a Company Administrator remove a parent (choose "No parent")? FR-016 says "changing the parent" without saying.
> - **Q3.** Should the Company Administrator see who changed the status and when ("Archived Sep 30, 2026 by Robin Hale")? The record exists (Key Entities, Community). The spec doesn't ask for it on screen. Not drawn.
> - **Q4.** Leaving a section with unsaved profile changes: warn first? The spec is silent. Not drawn.
> - **Q5.** Should sub-association names link to their Community settings for a Company Administrator? Drawn as plain text.

### 4.4 Artboard list

Data that conventions §4 doesn't give is drawn as a short `wf-ph` bar with the note "not in the dataset". This applies to Old Mill Commons' and Old Mill Commons Villas' formation dates, management start dates and descriptions, and to the start and end dates of Old Mill Commons' Board Member and Accountant memberships.

**Profile values used below**

| Community | Legal name | County | Formation date | Management start date | Description | Parent | Sub-associations |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Sakura Heights | Sakura Heights Homeowners Association, Inc. | Wake County, NC | Jun 9, 1998 | Jul 1, 2016 | No description (8.04.c adds one) | None | None |
| Keystone Ridge | Keystone Ridge HOA, Inc. | Durham County, NC | Jun 2, 2011 | Dec 1, 2026 | No description | None | None |
| Keystone Park | Keystone Park HOA, Inc. | Hamilton | Mar 14, 2004 | Nov 1, 2026 | No description | None | None |
| Keystone Crossing Master | Keystone Crossing Owners Association, Inc. | Durham County, NC | Mar 14, 2005 | Jan 1, 2019 | Section 7's description, unchanged | None | Keystone Crossing SF · Active; Keystone Crossing TH · Active |
| Keystone Crossing TH | Missing — required | Missing — required | Missing — required | Missing — required | No description (8.04.h adds one) | Keystone Crossing Master | None |
| Old Mill Commons | Old Mill Commons Association, Inc. | Wake County, NC | `wf-ph` | `wf-ph` | `wf-ph` | None | Old Mill Commons Villas · Archived |
| Old Mill Commons Villas | Old Mill Commons Villas Association, Inc. | `wf-ph` | `wf-ph` | `wf-ph` | `wf-ph` | Old Mill Commons | None |

#### Frame and Profile, Company Administrator

**8.04.a Community settings · Profile · Option A tabs · Company Administrator**

- Person: Robin Hale. No membership in Sakura Heights (US5 #1).
- Community: Sakura Heights, Active.
- Header: breadcrumb "Portfolio › Sakura Heights"; "Sakura Heights" with the "Active" chip; "Sakura Heights Homeowners Association, Inc."; no parent line; action "Archive".
- Section nav Option A, "Profile" current.
- Profile: the "Details" card in read view with "Edit profile"; the "Status and parent" card with "Active" and its sentence, parent "None" with "Change parent", sub-associations "None".
- State: default.
- Notes on canvas: "Managers and memberships: briefs 05–06. Architectural review: brief 07, which offers “Reset architectural review to company defaults” (US5 #1, FR-031)." "No Notifications section until the Notification Settings spec lands (FR-029)." Post-its D1 and Q1–Q5.

**8.04.b Community settings · Profile · Option B side list · Company Administrator**

- Same person, community and content as 8.04.a. The only difference is the section nav: a vertical list on the left of the content.
- State: default.

#### Profile, Community Manager

**8.04.c Profile · editing · Option A one Save · Community Manager**

- Person: Jordan Blake, in board mode. Community: Sakura Heights, Active.
- Header: "Sakura Heights" with the "Active" chip and the legal name. No breadcrumb and no status actions.
- The "Details" card is a form. Values: legal name, "Sakura Heights", "Wake County, NC", "06/09/1998", "07/01/2016". Jordan has typed a description: "Sakura Heights is a community of 120 homes in Wake County, NC." The counter reads "62 / 2,000".
- Buttons: "Save profile", "Cancel".
- The "Status and parent" card stays below in read view: "Active", parent "None", sub-associations "None", and the line "Only a Company Administrator can change the status or the parent association."
- State: editing. After "Save profile", the read view returns with the new description, and the toast "Profile saved." appears (US3 #17). The success is drawn in 8.04.h.
- Post-it D2.

**8.04.d Profile · editing · Option B save per field · Community Manager**

- Same person and community as 8.04.c.
- The "Details" card is in read view. Each row has an "Edit" button. The Description row is open: the text area holds the same text as 8.04.c, with the counter "62 / 2,000", and "Save" and "Cancel" buttons. Only one row is open at a time.
- After "Save", the toast reads "Description saved."
- State: editing one field.

**8.04.e Profile · required field cleared · Community Manager**

- Same as 8.04.c (Option A form), but Jordan has cleared County and pressed "Save profile".
- Error summary at the top of the card: heading "The profile wasn't saved", then one link: "County is required and can't be left empty." The same message is under the County field. Other fields keep what Jordan typed.
- Focus is on the summary.
- State: validation error (FR-017).

**8.04.f Profile · Onboarding notice · Community Manager**

- Person: Jordan Blake. Community: Keystone Ridge, Onboarding. Jordan added it on Oct 9, 2026.
- Strip: "Keystone Ridge", the "Onboarding" chip, "· Community Manager".
- Header: "Keystone Ridge" with the "Onboarding" chip; "Keystone Ridge HOA, Inc.". No status actions.
- The first thing in Profile is the Onboarding notice: "Keystone Ridge is being set up. A Company Administrator must mark it Active. Resident invitations are sent then." It is a banner (`wf-card lav`), not a toast.
- "Details" in read view with "Edit profile". "Status and parent": "Onboarding" with its sentence, parent "None", sub-associations "None", the Community Manager line.
- State: default (FR-029).

**8.04.g Profile · name taken · Community Manager**

- Same as 8.04.f, with the "Details" card as a form. Jordan changed Community name to " KEYSTONE PARK" (leading space, capitals) and pressed "Save profile".
- The Onboarding notice stays at the top.
- Error summary: heading "The profile wasn't saved", then the link "That community name is unavailable." The same message is under Community name.
- The header still reads "Keystone Ridge": the name is unchanged (US3 #18).
- The message doesn't name Keystone Park and says nothing about it (FR-007).
- State: refusal.
- Note on canvas: "A Company Administrator sees: “Keystone Park is already the name of another community. Choose a different community name.”"

**8.04.h Profile · missing required fields · Community Manager**

- Person: Jordan Blake. Community: Keystone Crossing TH, Active, from the spec 025 backfill.
- Strip: "Keystone Crossing TH · Community Manager".
- Header: "Keystone Crossing TH" with the "Active" chip; "Legal name: Missing — required"; "Sub-association of Keystone Crossing Master".
- Toast: "Profile saved." Jordan has just changed only the description.
- Missing-fields notice at the top of Profile: "4 required fields are empty: Legal name, County, Formation date and Management start date. Add them when you have them."
- "Details" in read view. Legal name, County, Formation date and Management start date each read "Missing — required". Community name "Keystone Crossing TH". Description: "Townhome sub-association of Keystone Crossing. 168 homes."
- "Status and parent": "Active", parent "Keystone Crossing Master", sub-associations "None", the Community Manager line.
- State: success. The save went through although required fields are still empty (US3 #19, FR-017).
- Described, not drawn: in the form before saving, each empty required field shows "Missing — required" next to its label and the helper "Still empty. You can save other changes without it. Once it has a value, it can't be cleared."
- Post-it D8.

#### Status: Mark Active

**8.04.i Profile · Onboarding · Company Administrator**

- Person: Robin Hale. Community: Keystone Ridge, Onboarding, with Jordan Blake as open-ended manager and 2 scheduled resident invitations.
- Header: breadcrumb "Portfolio › Keystone Ridge"; "Keystone Ridge" with the "Onboarding" chip; "Keystone Ridge HOA, Inc."; actions "Mark Active" and "Archive".
- Profile: no Onboarding notice (it is for Community Managers). "Details" as in 8.04.f, with "Edit profile". "Status and parent": "Onboarding" with its sentence, parent "None" with "Change parent", sub-associations "None".
- State: default.

**8.04.j Mark Active dialog · Keystone Ridge**

- Drawn over 8.04.i.
- Title: "Mark Keystone Ridge Active?"
- What happens (§5): it goes live for residents, with "No residents are linked yet."; the 2 scheduled resident invitations are emailed now, each listed by email and property, with the link expiry "Oct 15, 2026, 10:00 AM EDT".
- Buttons: "Mark community Active", "Cancel".
- State: confirmation (US3 #3).

**8.04.k Profile · marked Active · Company Administrator**

- 8.04.i after confirming 8.04.j.
- The header chip reads "Active". The only action is "Archive". The "Status" sentence is the Active one.
- Toast: "Keystone Ridge is now Active. 2 resident invitations were emailed."
- Focus is on the page title.
- State: success.
- Note on canvas: "In Residents (brief 06), lena@example.com and omar@example.com now read Pending, expires Oct 15, 2026, 10:00 AM EDT."

**8.04.l Mark Active dialog · no manager · Option A · Keystone Park**

- Drawn over Keystone Park's Profile, as Robin Hale sees it. Header: breadcrumb "Portfolio › Keystone Park"; "Keystone Park" with the "Onboarding" chip and the "No manager" chip; "Keystone Park HOA, Inc."; actions "Mark Active" (enabled) and "Archive".
- Title: "Mark Keystone Park Active?"
- At the top of the dialog, the refusal: "Keystone Park can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted."
- Link "Open Managers and memberships" (D9). Button: "Close". No consequence list, because nothing will happen (D4).
- Keystone Park stays Onboarding.
- State: refusal (US3 #4, `NO_ACTIVE_MANAGER`).
- Note on canvas: "Briar Hollow gets the same refusal with its own name (US3 #14)."
- Post-its D3 and D9.

**8.04.m Profile · Mark Active disabled · Option B · Company Administrator**

- Keystone Park's Profile, as in 8.04.l, with no dialog.
- "Mark Active" is shown disabled. Right under the actions, its reason: "Keystone Park can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted." "Archive" stays enabled.
- State: refusal shown in advance (Design choice D3, Option B).
- Post-it D6.

#### Status: Archive

**8.04.n Archive dialog · Old Mill Commons · Sep 30**

- **Snapshot:** Sep 30, 2026, before Robin Hale archived it. Old Mill Commons Villas was already archived, so there was no open work.
- Drawn over Old Mill Commons' Profile as it was then: "Old Mill Commons" with the "Active" chip; "Old Mill Commons Association, Inc."; action "Archive". "Details" values as in the table above. "Status and parent": "Active", parent "None" with "Change parent", sub-associations "Old Mill Commons Villas · Archived".
- Title: "Archive Old Mill Commons?"
- What happens (§5, one line each): 3 autopay schedules cancelled; 79 residents emailed; residents keep their homes as "No longer managed", with payment history readable; 2 invitations revoked, each named; 5 memberships kept but giving no access, each named; it leaves every switcher; read-only, Company Administrators only.
- What doesn't change (§5): nothing deleted; processor updates keep applying; restore is possible, without autopay or revoked invitations.
- Buttons: "Archive community", "Cancel".
- State: confirmation (US3 #5, US3 #7, FR-012, FR-013).
- Afterwards the page becomes 8.04.p, with the toast "Old Mill Commons is archived." Not drawn separately.

**8.04.o Archive dialog · open work · Keystone Crossing Master**

- Drawn over Keystone Crossing Master's Profile, as Robin Hale sees it: "Keystone Crossing Master" with the "Active" chip; "Keystone Crossing Owners Association, Inc."; action "Archive". "Status and parent": "Active", parent "None" with "Change parent", sub-associations "Keystone Crossing SF · Active" and "Keystone Crossing TH · Active".
- Title: "Archive Keystone Crossing Master?"
- At the top, the refusal: "Keystone Crossing Master can't be archived until this work is closed:" then a list:
  - "ARC-1041 · Open"
  - "ARC-1042 · Decision Reached"
  - "Keystone Crossing SF · sub-association, Active"
  - "Keystone Crossing TH · sub-association, Active"
- Helper under the list: "The board decides open architectural applications, and the Community Manager records each outcome in board mode. Archive each sub-association first."
- The ARC items are plain text, not links (FR-003). Button: "Close". No consequence list (D4).
- State: refusal (US3 #10, US3 #11, FR-014).
- Post-it D4.

#### Archived read-only view

**8.04.p Profile · Archived · read-only · Company Administrator**

- Person: Robin Hale. Community: Old Mill Commons, Archived since Sep 30, 2026.
- Header: breadcrumb "Portfolio › Old Mill Commons"; "Old Mill Commons" with the "Archived" chip; "Old Mill Commons Association, Inc."; one action, "Restore".
- Section nav as usual. All three sections can be opened (US5 #7).
- Profile: the archived banner first. "Details" values as plain text, with no "Edit profile" and no "Edit" buttons. "Status and parent": "Archived" with its sentence, parent "None" with no "Change parent", sub-associations "Old Mill Commons Villas · Archived".
- State: read-only.

**8.04.q Managers and memberships · Archived · read-only · Company Administrator**

- Same person, community and header as 8.04.p. "Managers and memberships" current.
- If brief 05's Managers and memberships layout is on the canvas, use it. If not, stack the four areas in this order with the note "Layout follows brief 05": Managers; Board members and accountants; Residents; Invitations.
- The archived banner first.
- **Managers:** Morgan Ellis · Community Manager · Open-ended, with one action, "End membership". Under the area heading: "Memberships give no access while Old Mill Commons is archived. You can still end them." (D10). No "Appoint" or "Replace manager".
- **Board members and accountants:** Rowan Hayes, Jesse Lin and Skyler Diaz, each "Board Member"; Avery Quinn, "Accountant". Their start and end dates are `wf-ph` bars. Each row has one action, "End membership". No "Appoint", "Invite" or "Downgrade to Board Member".
- **Residents:** the heading "Residents", the footer "Showing 1–25 of 86", and `wf-ph` rows labelled "Residents rows — brief 06". Search, filters and pages still work, because they only read. No "Invite resident", "Remove resident", "Resend" or "Revoke". rae@example.com's invitation to 22 Old Mill Ln reads "Revoked — community archived" wherever brief 06 shows it.
- **Invitations:** one row: drew@example.com · Community Manager · Sent by Morgan Ellis · Sent Sep 28, 2026, 2:00 PM EDT · Expires Oct 1, 2026, 2:00 PM EDT · state "Revoked — community archived" with the second line "Sep 30, 2026". No actions.
- State: read-only (US5 #7, FR-012).
- Post-it D10.

**8.04.r Architectural review · Archived · read-only · Company Administrator**

- Same person, community and header as 8.04.p. "Architectural review" current.
- The archived banner first.
- Values as plain text, with the labels from today's ARC settings page:
  - Review period (days): 30
  - Reminder to the board (days before due, 0 = off): 7
  - When the review period ends without a decision: Flag as overdue only
  - How a decision is reached: Majority of board members
  - Time zone: America/New_York
  - Formal disapproval statement (included in every denial email): "This is a formal disapproval of the plans as submitted under the community's governing documents. You may revise and resubmit."
- No "Save", no "Reset architectural review to company defaults", and no "Company defaults" link. No "Differs from defaults" flags, because the values equal the defaults.
- State: read-only.

**8.04.s End membership dialog · Archived · Morgan Ellis**

- Drawn over 8.04.q. Opened from Morgan Ellis's "End membership".
- Title: "End Morgan Ellis's Community Manager membership?"
- What happens (§5): it ends today, Oct 12, 2026; Morgan won't get access back on restore; Old Mill Commons will have no open-ended manager, which is allowed while archived, and a restore then returns it to Onboarding.
- What doesn't change: Morgan's account and other memberships, including Old Mill Commons Villas.
- Buttons: "End membership", "Cancel".
- State: confirmation (US3 #15).
- Afterwards: the toast "Morgan Ellis's Community Manager membership in Old Mill Commons ended." Morgan's row reads "Ended Oct 12, 2026", with no action.

#### Status: Restore

**8.04.t Restore dialog · returns to Active · Old Mill Commons**

- Drawn over 8.04.p.
- Title: "Restore Old Mill Commons?"
- First, in bold, the outcome: "Old Mill Commons returns to Active, because it was Active before it was archived and it has a Community Manager with no end date: Morgan Ellis."
- "These memberships will give access again:" a list of 5 rows, each with an "End membership" button:
  - Morgan Ellis · Community Manager · no end date
  - Rowan Hayes · Board Member
  - Jesse Lin · Board Member
  - Skyler Diaz · Board Member
  - Avery Quinn · Accountant
- What else happens and what doesn't change (§5).
- Buttons: "Restore community", "Cancel".
- State: confirmation (US3 #12, FR-015).
- Post-its: D5, and "Restore outcome lines", which lists the three outcome lines from §5 (US3 #12, #13, #14).

**8.04.u Restore dialog · Morgan Ellis ended · returns to Onboarding · Old Mill Commons**

- 8.04.t after Robin chose "End membership" on Morgan's row and confirmed the step (D5).
- At the top, a status line: "Morgan Ellis's Community Manager membership ended."
- Outcome, in bold: "Old Mill Commons returns to Onboarding, because it has no Community Manager with no end date. It can be marked Active once it has one."
- "These memberships will give access again:" 4 rows (Rowan Hayes, Jesse Lin, Skyler Diaz, Avery Quinn), each with "End membership".
- "These memberships have ended and stay ended:" Morgan Ellis · Community Manager · Ended Oct 12, 2026.
- The switchers line and the resident line change to their Onboarding versions (§5): "It appears again in the community switchers and My Communities of the people above, marked Onboarding." and "79 residents can't see their homes, including payment history, until it is marked Active."
- Buttons: "Restore community", "Cancel".
- State: confirmation after a change (US3 #14, US3 #15).

**8.04.v Restore dialog · parent archived · Old Mill Commons Villas**

- Drawn over Old Mill Commons Villas' archived Profile: "Old Mill Commons Villas" with the "Archived" chip; "Old Mill Commons Villas Association, Inc."; "Sub-association of Old Mill Commons"; action "Restore"; the archived banner.
- Title: "Restore Old Mill Commons Villas?"
- At the top, the refusal: "Old Mill Commons Villas can't be restored while its parent, Old Mill Commons, is archived. Restore Old Mill Commons first."
- Link "Open Old Mill Commons" (D9). Button: "Close". No membership list (D4).
- State: refusal (US3 #16, `PARENT_ARCHIVED`).

#### Change parent

**8.04.w Change parent dialog · cycle · Keystone Crossing Master**

- Drawn over Keystone Crossing Master's Profile (as in 8.04.o). Opened from "Change parent".
- Title: "Change the parent of Keystone Crossing Master?"
- Line: "Current parent association: None."
- Field "New parent association": a searchable list, with the placeholder "Search communities" and the helper "Archived communities can't be a parent, so they aren't listed." Options: "No parent" first (D7), then every non-archived community except this one, A–Z: Briar Hollow, Keystone Crossing SF, Keystone Crossing TH, Keystone Park, Keystone Ridge, Sakura Heights, then filler names. Old Mill Commons and Old Mill Commons Villas are not listed.
- Chosen: "Keystone Crossing TH". After "Change parent", the refusal appears under the field: "Keystone Crossing TH can't be the parent of Keystone Crossing Master, because Keystone Crossing TH is already under Keystone Crossing Master. Choose a different parent."
- Lines: "Keystone Crossing Master becomes a sub-association of the community you choose." "No one gains or loses access. Access is set for each community on its own, never through the parent."
- Buttons: "Change parent", "Cancel". The person can pick another community and try again.
- State: refusal (FR-016, `PARENT_CYCLE`).
- Post-it D7.

**8.04.x Change parent dialog · archived parent · Keystone Crossing Master**

- As 8.04.w, but the chosen value is "Old Mill Commons". This happens only when the list is out of date, for example in a dialog opened before Old Mill Commons was archived. The server checks again on "Change parent".
- Refusal under the field: "Old Mill Commons is archived, so it can't be a parent."
- The list reloads without Old Mill Commons. The field is empty again.
- State: refusal (FR-016, `PARENT_ARCHIVED`).
- Note on canvas: "Reached only with an out-of-date list. Archived communities are never offered."

#### Page refused

**8.04.y Community settings · not their community · refused · Community Manager**

- Person: Jordan Blake. Jordan opened a link to Keystone Park's Community settings. Jordan doesn't manage Keystone Park.
- Draw Jordan's landing page as brief 01 draws it (8.01.q): board mode, "My Communities" current, strip "No community chosen yet", Jordan's five communities. If brief 01 isn't drawn yet, use `BoardNavMulti`'s layout with the columns "Community" and "Your role".
- Toast: "You don't have access to that page."
- Nothing from Keystone Park appears, not even briefly. The response is the same for a community that doesn't exist (US3 #21, FR-004a).
- State: refusal.

**8.04.z Community settings · refused · Board Member**

- Person: Lee Morales, Board Member of Sakura Heights. Lee opened a link to Sakura Heights' Community settings.
- Draw Lee's landing as brief 01 draws it (8.01.k): board mode, strip "Sakura Heights · Board Member", "Community Home" current, "Community settings" and "Add community" locked (🔒, "Not available to your role"). Body: "Community Home — section 7, unchanged".
- Toast: "You don't have access to that page."
- State: refusal (US5 #3, FR-032). Accountants and Residents get the same refusal on their own landing page. Not drawn.

#### States with copy only (not drawn)

These follow the conventions' patterns. Their exact words are in §5.

- Page loading and page load error (conventions §7.7).
- Save failed, and rate limited (conventions §6.2).
- A dialog that can't load what will change.
- A dialog action that fails for a network reason.
- A save in a community that was archived in another tab (`COMMUNITY_ARCHIVED`), for each role.
- An action refused because the person's role changed (`FORBIDDEN`, action form).
- The Company Administrator's name-taken messages, including an archived community's name.
- Mark Active when a scheduled invitation's sender has lost authority.
- Archive of an Onboarding community, whose scheduled invitations are revoked unsent.
- The restore outcome for a community archived while Onboarding (US3 #13), and Briar Hollow's variants (US3 #14).
- Restore with no memberships to list.
- Success toasts for Restore and Change parent.

## 5. Copy

Quote these exactly. "SR" means text for screen readers only. Names and counts come from conventions §4; real messages fill in the real names.

### 5.1 Page frame

| Element | Text |
| --- | --- |
| Breadcrumb (Company Administrator) | "Portfolio › Sakura Heights" (SR landmark name: "Breadcrumb") |
| Page title | The community name, for example "Sakura Heights" |
| Status chips | "Onboarding" · "Active" · "Archived" |
| No-manager chip (Company Administrator, D6) | "No manager" |
| Legal name line | The legal name, or "Legal name: Missing — required" |
| Parent line | "Sub-association of Keystone Crossing Master" |
| Status actions | "Mark Active" · "Archive" · "Restore" (SR: "Mark Keystone Ridge Active", "Archive Keystone Ridge", "Restore Old Mill Commons") |
| Section nav | "Profile" · "Managers and memberships" · "Architectural review" (SR landmark name: "Community settings sections") |
| Archived banner | "Old Mill Commons is archived. Juniper Row Community Management no longer manages it. Everything here is read-only. Nothing has been deleted." |
| Onboarding notice (Community Manager, Profile) | "Keystone Ridge is being set up. A Company Administrator must mark it Active. Resident invitations are sent then." |
| Missing-fields notice (D8) | "4 required fields are empty: Legal name, County, Formation date and Management start date. Add them when you have them." With one field: "1 required field is empty: County. Add it when you have it." |

### 5.2 Profile, read view

| Element | Text |
| --- | --- |
| Card heading | "Details" |
| Field labels | "Legal name" · "Community name" · "County" · "Formation date" · "Management start date" · "Description" |
| Empty description | "No description" |
| Empty required field | "Missing — required" (SR: "Legal name: missing, required") |
| Option A button | "Edit profile" |
| Option B row button | "Edit" (SR: "Edit county") |
| Card heading | "Status and parent" |
| Labels | "Status" · "Parent association" · "Sub-associations" |
| No parent, no sub-associations | "None" |
| Parent button (Company Administrator) | "Change parent" (SR: "Change parent of Keystone Crossing Master") |
| Onboarding sentence | "Being set up. Hidden from residents. Resident invitations are scheduled and sent when it is marked Active." |
| Active sentence | "Live. Residents can use their homes." |
| Archived sentence | "No longer managed by Juniper Row Community Management. Read-only." |
| Community Manager line | "Only a Company Administrator can change the status or the parent association." |

### 5.3 Profile, editing

| Field | Label | Helper |
| --- | --- | --- |
| Legal name | "Legal name (required)" | "Up to 200 characters." |
| Community name | "Community name (required)" | "2 to 80 characters. It must differ from every other community's name, including archived ones. Capital letters and spaces at the start or end don't make a name different." |
| County | "County (required)" | — |
| Formation date | "Formation date (required)" | "MM/DD/YYYY. Can't be in the future." |
| Management start date | "Management start date (required)" | "MM/DD/YYYY." |
| Description | "Description (optional)" | "Up to 2,000 characters." Counter: "62 / 2,000". |
| A required field still empty from the backfill | Label, then "Missing — required" | "Still empty. You can save other changes without it. Once it has a value, it can't be cleared." |

| Element | Text |
| --- | --- |
| Option A buttons | "Save profile" (busy: "Saving…") · "Cancel" |
| Option B buttons | "Save" (busy: "Saving…") · "Cancel" |
| Error summary heading | "The profile wasn't saved" |
| Success toast, Option A | "Profile saved." |
| Success toast, Option B | "Description saved." (the field's name) |

**Validation** (conventions §6.3; each appears in the summary and under its field):

- "Enter the legal name." · "Legal name can be at most 200 characters."
- "Enter a community name." · "Community name must be 2 to 80 characters."
- "Formation date can't be in the future."
- "Description can be at most 2,000 characters."
- A filled required field cleared (FR-017): "Legal name is required and can't be left empty." · "Community name is required and can't be left empty." · "County is required and can't be left empty." · "Formation date is required and can't be left empty." · "Management start date is required and can't be left empty."

**Refusals on save**

| Case | Text | Where |
| --- | --- | --- |
| `COMMUNITY_NAME_TAKEN`, Community Manager | "That community name is unavailable." | Summary and inline under Community name |
| `COMMUNITY_NAME_TAKEN`, Company Administrator | "Keystone Park is already the name of another community. Choose a different community name." | Same |
| `COMMUNITY_NAME_TAKEN`, Company Administrator, archived name | "Old Mill Commons is the name of an archived community. Archived communities keep their names. Choose a different community name." | Same |
| `COMMUNITY_ARCHIVED` (archived in another tab), Company Administrator | "Old Mill Commons is archived, so it can't be changed. Restore it to make changes." | Banner; the page reloads read-only |
| `COMMUNITY_ARCHIVED`, Community Manager | "Juniper Row Community Management no longer manages Old Mill Commons, so it can't be changed." | Banner |
| `FORBIDDEN`, action (for example, a role revoked while the page was open) | "You don't have permission to do that." | Banner, or in the open dialog |
| Save failed | "Couldn't save. Check your connection and try again." Input is kept. | Banner on the form |
| Rate limited | "Too many requests. Wait a few minutes, then try again." | Banner on the form, or in the dialog |

**Loading and errors**

| State | Text | Action |
| --- | --- | --- |
| Page loading (SR) | "Loading Community settings…" | — |
| Page load error | "Couldn't load Community settings." | "Try again" |
| Section load error | "Couldn't load the profile." | "Try again" |

### 5.4 Status dialogs, shared

| Element | Text |
| --- | --- |
| Can't load what will change | "Couldn't load what this will change." with "Try again". The primary button is disabled until it loads. |
| Action failed (network) | "Couldn't mark Keystone Ridge Active. Check your connection and try again." Same pattern: "Couldn't archive Old Mill Commons.", "Couldn't restore Old Mill Commons.", "Couldn't change the parent.", "Couldn't end the membership.", each followed by "Check your connection and try again." |
| Close button on a refusal | "Close" |
| Cancel | "Cancel" |

### 5.5 Mark Active

| Element | Text |
| --- | --- |
| Title | "Mark Keystone Ridge Active?" |
| Line 1 | "Keystone Ridge goes live. Residents linked to its homes can use them from their next visit. No residents are linked yet." |
| Line 2 | "2 scheduled resident invitations are emailed now. Each link works for 72 hours, until Oct 15, 2026, 10:00 AM EDT:" |
| Invitation list | "lena@example.com · 2 Ridgeview Ct" · "omar@example.com · 8 Ridgeview Ct" |
| No scheduled invitations (variant) | "No resident invitations are scheduled." |
| Sender lost authority (variant, not drawn) | "1 scheduled invitation is revoked instead, because the person who sent it can no longer send it: <email> · <property>." |
| Primary button | "Mark community Active" (busy: "Marking Active…") |
| Success toast | "Keystone Ridge is now Active. 2 resident invitations were emailed." With none: "Keystone Ridge is now Active." |
| `NO_ACTIVE_MANAGER` | "Keystone Park can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted." Briar Hollow: "Briar Hollow can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted." |
| Link in the refusal (D9) | "Open Managers and memberships" |

### 5.6 Archive

| Element | Text |
| --- | --- |
| Title | "Archive Old Mill Commons?" |
| Lead | "Juniper Row Community Management will no longer manage Old Mill Commons. This is what happens:" |
| Autopay | "3 autopay schedules are cancelled. No new automatic charge starts. A charge already submitted settles normally." |
| Residents emailed | "79 residents are emailed once. The email says Juniper Row Community Management no longer manages Old Mill Commons, that autopay has ended where it applied, and that their payment history stays readable." |
| Residents' access | "Residents keep their homes, marked “No longer managed”. They can still read the ledger, statements, receipts and transactions. Nothing else is open to them." |
| Invitations | "2 invitations are revoked, and their links stop working: drew@example.com (Community Manager) and rae@example.com (resident of 22 Old Mill Ln)." |
| Memberships | "5 memberships are kept but give no access while it is archived: Morgan Ellis (Community Manager), Rowan Hayes, Jesse Lin and Skyler Diaz (Board Members), and Avery Quinn (Accountant)." |
| Switchers | "It leaves every community switcher and My Communities." |
| Read-only | "Its Community settings become read-only. Only Company Administrators can open them." |
| Heading for what doesn't change | "What doesn't change" |
| Nothing deleted | "Nothing is deleted. The profile, settings, memberships, residents and invitations are kept." |
| Processor updates | "Refunds, bank returns and disputes on earlier payments still go through." |
| Restore later | "You can restore it later. Autopay won't restart, and revoked invitations stay revoked." |
| Onboarding community (variant, not drawn) | "2 scheduled resident invitations are revoked without being sent." |
| Zero counts (variants) | "No autopay schedules are active." · "No residents are linked, so no one is emailed." · "No invitations are pending." · "No memberships are kept." |
| Primary button | "Archive community" (busy: "Archiving…") |
| Success toast | "Old Mill Commons is archived." |
| `OPEN_WORK` | "Keystone Crossing Master can't be archived until this work is closed:" then "ARC-1041 · Open" · "ARC-1042 · Decision Reached" · "Keystone Crossing SF · sub-association, Active" · "Keystone Crossing TH · sub-association, Active" |
| `OPEN_WORK` helper | "The board decides open architectural applications, and the Community Manager records each outcome in board mode. Archive each sub-association first." |

### 5.7 Restore

| Element | Text |
| --- | --- |
| Title | "Restore Old Mill Commons?" |
| Outcome, Active (US3 #12) | "Old Mill Commons returns to Active, because it was Active before it was archived and it has a Community Manager with no end date: Morgan Ellis." |
| Outcome, no open-ended manager (US3 #14, #15) | "Old Mill Commons returns to Onboarding, because it has no Community Manager with no end date. It can be marked Active once it has one." Briar Hollow (as on Oct 5): "Briar Hollow returns to Onboarding, because it has no Community Manager with no end date. It can be marked Active once it has one." |
| Outcome, never Active (US3 #13, not drawn) | "<Community> returns to Onboarding, because it had never been Active before it was archived. Residents won't see it until it is marked Active." |
| List heading | "These memberships will give access again:" |
| Rows | "Morgan Ellis · Community Manager · no end date" · "Rowan Hayes · Board Member" · "Jesse Lin · Board Member" · "Skyler Diaz · Board Member" · "Avery Quinn · Accountant" |
| Row button | "End membership" (SR: "End Morgan Ellis's Community Manager membership before restoring") |
| Empty list | "No memberships will give access again." |
| Ended list heading | "These memberships have ended and stay ended:" |
| Ended row | "Morgan Ellis · Community Manager · Ended Oct 12, 2026". Briar Hollow (as on Oct 5): "Reese Walker · Community Manager · Ended Aug 31, 2026" |
| Status line after ending | "Morgan Ellis's Community Manager membership ended." |
| Switchers, Active | "It appears again in the community switchers and My Communities of the people above." |
| Switchers, Onboarding | "It appears again in the community switchers and My Communities of the people above, marked Onboarding." |
| Residents, Active | "79 residents can use their homes again. The “No longer managed” label goes away." |
| Residents, Onboarding | "79 residents can't see their homes, including payment history, until it is marked Active." |
| Heading for what doesn't change | "What doesn't change" |
| Unchanged lines | "Memberships keep their dates. The profile and settings don't change." · "Autopay cancelled by the archive doesn't restart. Residents enroll again themselves." · "Invitations revoked by the archive stay revoked." · "Old Mill Commons Villas stays archived. Restore it on its own afterwards." |
| Primary button | "Restore community" (busy: "Restoring…") |
| Success toasts | "Old Mill Commons is restored and Active." · "Old Mill Commons is restored. It stays Onboarding until it has a Community Manager with no end date." |
| `PARENT_ARCHIVED` | "Old Mill Commons Villas can't be restored while its parent, Old Mill Commons, is archived. Restore Old Mill Commons first." |
| Link in the refusal (D9) | "Open Old Mill Commons" |

**The end-first step inside the restore dialog (D5).**

| Element | Text |
| --- | --- |
| Title | "End Morgan Ellis's Community Manager membership first?" |
| Lines | "It ends today, Oct 12, 2026." · "Morgan Ellis won't get access back when Old Mill Commons is restored." · "Old Mill Commons will then have no Community Manager with no end date, so it returns to Onboarding when restored." (The last line appears only when ending the last open-ended manager.) |
| Unchanged line | "Morgan Ellis's account and other memberships don't change, including Old Mill Commons Villas." For a Board Member: "Rowan Hayes's account and other memberships don't change." |
| Buttons | "End membership" (busy: "Ending…") · "Back" |

### 5.8 End membership in an archived community

| Element | Text |
| --- | --- |
| Title | "End Morgan Ellis's Community Manager membership?" |
| Lines | "It ends today, Oct 12, 2026." · "Morgan Ellis won't get access back if Old Mill Commons is restored." · "Old Mill Commons will have no Community Manager with no end date. That's allowed while it's archived. If it's restored, it returns to Onboarding until it has one." |
| Heading for what doesn't change | "What doesn't change" |
| Unchanged line | "Morgan Ellis's account and other memberships don't change, including Old Mill Commons Villas." |
| Primary button | "End membership" (busy: "Ending…") |
| Success toast | "Morgan Ellis's Community Manager membership in Old Mill Commons ended." |
| Row afterwards | "Ended Oct 12, 2026" |
| Managers area helper while archived (D10) | "Memberships give no access while Old Mill Commons is archived. You can still end them." |

### 5.9 Change parent

| Element | Text |
| --- | --- |
| Title | "Change the parent of Keystone Crossing Master?" |
| Current | "Current parent association: None." |
| Field label | "New parent association" |
| Placeholder | "Search communities" |
| Helper | "Archived communities can't be a parent, so they aren't listed." |
| First option (D7) | "No parent" |
| Lines | "Keystone Crossing Master becomes a sub-association of the community you choose." · "No one gains or loses access. Access is set for each community on its own, never through the parent." |
| Primary button | "Change parent" (busy: "Changing…") |
| `PARENT_CYCLE` | "Keystone Crossing TH can't be the parent of Keystone Crossing Master, because Keystone Crossing TH is already under Keystone Crossing Master. Choose a different parent." |
| `PARENT_ARCHIVED` | "Old Mill Commons is archived, so it can't be a parent." |
| Success toasts | "Keystone Crossing Master is now a sub-association of Sakura Heights." · "Keystone Crossing Master no longer has a parent association." |

### 5.10 Page refusals

| Case | Text | Where |
| --- | --- | --- |
| `FORBIDDEN`, page: a Community Manager opens another community's settings, or one that doesn't exist; a Board Member, Accountant or Resident opens Community settings | "You don't have access to that page." | Toast on the landing page the app moves them to (spec 025 FR-028). Never names the community. |

Conventions §6.1 names a toast for this case, while §7.8 says a toast never carries a refusal. This brief follows §6.1, as brief 02 does: it names this exact case, and the person has nothing to act on.

## 6. Interactions

**Opening the page**

- A Company Administrator opens it from a portfolio row's "Community settings" link (brief 02). It opens on Profile, with the breadcrumb. "Portfolio" in the breadcrumb goes back to the portfolio.
- A Community Manager opens it from "Community settings" in the board nav. It opens the active community's page on Profile (FR-029).
- The page is addressed by the community, not by board mode's active community. So it opens for Onboarding and Archived communities that are in no switcher (FR-029).
- Every section has its own address. The old ARC settings location opens Architectural review (FR-030; brief 07).

**Section nav.** Choosing a section opens it. The header stays the same. Focus moves to the section's first heading. The browser's Back returns to the previous section.

**Editing the profile, Option A**

- "Edit profile" turns the "Details" card into a form. Focus moves to the first field.
- "Cancel" drops the changes and returns to the read view. Focus returns to "Edit profile".
- "Save profile" checks the fields. If any fail, the error summary appears at the top of the card and takes focus. Each link in it moves focus to its field. Nothing is saved.
- On success: the read view returns with the new values, the toast "Profile saved." is shown, and focus returns to "Edit profile". If the community name changed, the page title and the breadcrumb change with it.
- A server refusal (`COMMUNITY_NAME_TAKEN`) shows like a validation error. The name and the title stay as they were.

**Editing the profile, Option B.** "Edit" on a row opens that row only. "Save" saves that field alone. "Cancel" closes the row. Errors appear under the field. The toast names the field: "Description saved."

**Status actions, all dialogs**

1. Pressing the action opens its dialog. Focus moves to the dialog title.
2. The dialog asks the server what will happen. While it waits, placeholder lines (`wf-ph`) stand where the lines will be, and the primary button is disabled.
3. If the server says the action will be refused, the dialog shows only the refusal and "Close" (D4).
4. Otherwise it shows the consequences. The primary button repeats the verb.
5. While the action runs, the primary button shows its busy label and is disabled. "Cancel" stays enabled.
6. On success the dialog closes, the page updates, and a toast confirms. The button that opened the dialog may be gone (Mark Active after success; Archive and Restore swap). Focus then goes to the page title.
7. On a refusal after confirming, the dialog stays open and shows the refusal at the top. If the person can't fix it in the dialog, the primary button becomes "Close".
8. On a network failure, the dialog stays open with the §5.4 message. The person can confirm again.
9. "Cancel", "Close" and Esc close the dialog and change nothing. Focus returns to the button that opened it.

**Mark Active.** On success: the chip becomes "Active", "Mark Active" goes away, and the 2 scheduled invitations are emailed and become Pending with new 72-hour links (FR-024b). Residents linked to the community's homes see it on their next request (US3 #3).

**Archive.** On success the page re-renders read-only (8.04.p) and the toast "Old Mill Commons is archived." is shown. The community leaves every switcher on each person's next request (FR-012).

**Restore**

- Each membership row has "End membership". It swaps the dialog to the end-first step (D5). "End membership" there ends it today and returns to the restore view. The list, the outcome line and the resident line refresh. The status line "Morgan Ellis's Community Manager membership ended." is announced. "Back" returns without ending anything, and focus returns to the row's button.
- "Restore community" restores to the status the outcome line states. On success, the archived banner goes away, the status actions match the new status, and the toast confirms.
- `PARENT_ARCHIVED`: "Open Old Mill Commons" closes the dialog and opens Old Mill Commons' Community settings.

**End membership in an archived community.** From the Managers area, or from Board members and accountants. On success, the row reads "Ended Oct 12, 2026" with no action, and the toast confirms. The last-manager rule doesn't apply while archived (FR-012, FR-020).

**Change parent**

- The field filters the list as the person types. Matching ignores case and matches part of a word.
- "Change parent" is enabled once a value is chosen.
- `PARENT_CYCLE` and `PARENT_ARCHIVED` appear under the field. The dialog stays open with "Change parent" enabled, because the person can choose another community. Focus moves to the field. After `PARENT_ARCHIVED`, the list reloads and the field is cleared.
- On success: the dialog closes, the parent line in the header and the "Parent association" value update, and the toast confirms. Nobody's access changes (FR-016).

**Refused page (8.04.y, 8.04.z).** The server refuses with 403 `FORBIDDEN`. The app takes the person to their landing page (spec 025 FR-028; FR-036a) and shows the toast. Focus goes to the landing page's main heading. The client never shows the Community settings entry unlocked to people who can't use it, but the server decides (conventions §7.10).

**After a role or status change elsewhere.** Each request is checked again. If Robin's role is revoked while the page is open, the next action is refused with "You don't have permission to do that." If another Company Administrator archives the community while a form is open, the next save shows the `COMMUNITY_ARCHIVED` banner and the page reloads read-only.

## 7. Accessibility

Conventions §8 applies. For this page:

- **Headings.** One `h1`, the community name. Its chip is read with it: "Sakura Heights, Active". Each card and each dialog has a heading.
- **Landmarks.** The breadcrumb is a navigation landmark named "Breadcrumb". The section nav is a navigation landmark named "Community settings sections". Its items are links, because each section has its own address. The current one is marked as current, in text and by more than colour.
- **Keyboard order:** main nav → breadcrumb → status actions → section nav → section content. Status actions come before the sections because they belong to the whole community.
- **Status actions.** Button names say the community: "Archive Keystone Ridge". "Archive" is marked as destructive by its words and its place, not only by colour.
- **Disabled Mark Active (Option B).** It stays focusable and is announced as unavailable, with its reason as its description. The reason is visible text next to it, not a tooltip.
- **Read views.** Labels and values form a description list. Archived values are plain text, not disabled inputs, so nothing looks usable.
- **Forms.** Every field has a visible label with "(required)" or "(optional)" in words. Helper text, errors and the description counter are tied to their field. The error summary takes focus, and its links move focus to the field.
- **Words, not colour.** Status chips, "No manager", "Missing — required", "Revoked — community archived" and "Ended Oct 12, 2026" are text. Icons only add to them.
- **Banners.** The archived banner, the Onboarding notice and the missing-fields notice are static text at the top of the section. They are not live regions.
- **Dialogs.** Focus moves to the title on open and stays inside. Esc acts as "Cancel" or "Close". On close, focus returns to the opening button, or to the page title if that button is gone. Consequence lines are a real list.
- **Refusals in dialogs** are announced at once. Focus moves to the refusal. Inline refusals in Change parent are tied to the field.
- **End-first step (D5).** Focus moves to the step's title. "Back" returns focus to the row's "End membership" button. After ending, the status line is announced politely and focus moves to it.
- **Toasts** are announced politely, don't take focus, and stay long enough to read. Nothing in a toast needs action.
- **Contrast:** 4.5:1 for text, including chips and placeholder text; 3:1 for control borders and the current-section marker. In board mode, the banner stays dark ink on violet (spec 025 FR-038).
- **Zoom.** At 200%, the status actions wrap under the title, and Option A's tabs wrap or scroll inside their row. The page itself doesn't scroll sideways.

## 8. Out of scope for this design

- The bodies of Managers and memberships (brief 05), Residents (brief 06) and Architectural review (brief 07) outside the archived read-only view. That includes Appoint, Replace manager, the LAST_MANAGER refusals, invitations, the reset dialog and "Differs from defaults".
- The portfolio (brief 02), Add community (brief 03), the board-nav entries (brief 01), the Company defaults page (brief 08).
- The emails that archive and activation send (brief 12), and the resident-side "No longer managed" and "isn't live yet" pages (brief 11).
- Notifications, anywhere on the page.
- A "type the name to confirm" step.
- A change history or audit view. Changes are logged only (FR-033, FR-040).
- Who changed the status and when (open question Q3).
- Section 7's "Community GUID" line and "Amenities" card.
- Status actions anywhere but this header: none in the portfolio, none in board mode.
- An archived community seen by a Community Manager, Board Member or Accountant. They can't open it (FR-012). The `COMMUNITY_ARCHIVED` copy in §5 covers an old tab.
- Phone and tablet layouts (spec Assumptions).

## 9. Design review checklist

- [ ] **FR-029, frame.** The page shows exactly Profile, Managers and memberships and Architectural review, in that order. There is no Notifications tab, locked item or placeholder (8.04.a, b).
- [ ] **D1.** Section nav is drawn as tabs (8.04.a) and as a side list (8.04.b), each labelled "Design choice".
- [ ] **FR-029, addressing.** A Company Administrator's page has the breadcrumb "Portfolio › <community>" and opens for Onboarding (8.04.i) and Archived (8.04.p) communities. A Community Manager's page is in board mode with "Community settings" current (8.04.c–h).
- [ ] **US5 #1.** Robin Hale, with no membership, sees an editable Profile, the status actions for the current status, and a note that brief 07's section offers "Reset architectural review to company defaults" (8.04.a, i).
- [ ] **US5 #2, US3 #20.** No Community Manager artboard shows Mark Active, Archive, Restore or Change parent, not even disabled. Status and parent are plain text with "Only a Company Administrator can change the status or the parent association." (8.04.c–h).
- [ ] **US3 #17.** A Community Manager edits the six fields of an Active community and saves (8.04.c, d).
- [ ] **D2.** Saving is drawn as one Save per section (8.04.c) and save per field (8.04.d), each labelled "Design choice".
- [ ] **FR-017.** Clearing a filled required field shows "County is required and can't be left empty." in the summary and under the field (8.04.e).
- [ ] **US3 #18.** Renaming Keystone Ridge to " KEYSTONE PARK" shows "That community name is unavailable." and names nothing. The title still reads "Keystone Ridge". The Company Administrator's wording is in a note (8.04.g).
- [ ] **US3 #19.** Keystone Crossing TH saves a description change. The header, the notice and the fields still say "Missing — required" (8.04.h).
- [ ] **FR-029, notice.** Jordan sees "Keystone Ridge is being set up. A Company Administrator must mark it Active. Resident invitations are sent then." on Profile (8.04.f, g).
- [ ] **FR-010, actions.** Onboarding shows Mark Active and Archive; Active shows Archive; Archived shows Restore (8.04.a, i, k, p).
- [ ] **US3 #3.** The Mark Active dialog names lena@example.com and omar@example.com and the expiry "Oct 15, 2026, 10:00 AM EDT". Afterwards the chip reads "Active" and the toast confirms 2 invitations emailed (8.04.j, k).
- [ ] **US3 #4, D3.** Keystone Park's Mark Active is refused with the `NO_ACTIVE_MANAGER` message, in the dialog (8.04.l) and as a disabled button with the same reason (8.04.m). The pending invitation shows as an email only.
- [ ] **US3 #5, #7, FR-012, FR-013.** The archive dialog shows 3 autopay schedules cancelled, 79 residents emailed, residents' "No longer managed" read access, 2 named invitations revoked, 5 named memberships kept with no access, leaving the switchers, read-only, nothing deleted, and restore's limits (8.04.n).
- [ ] **US3 #10, #11, FR-014.** The open-work refusal lists ARC-1041 · Open, ARC-1042 · Decision Reached and both sub-associations. ARC items aren't links. The only button is "Close" (8.04.o).
- [ ] **US5 #7, FR-012.** Every section of Old Mill Commons is read-only, with the archived banner. Values are plain text. The only actions are Restore and End membership. drew@example.com reads "Revoked — community archived". Residents are shown, read-only (8.04.p, q, r).
- [ ] **US3 #15.** Ending Morgan Ellis's membership in archived Old Mill Commons is allowed, and the dialog says it is allowed while archived (8.04.s).
- [ ] **US3 #12, FR-015.** The restore dialog says Old Mill Commons returns to Active and why. It lists the 5 memberships with "End membership" each, and states that memberships keep their dates, autopay doesn't restart, revoked invitations stay revoked and Old Mill Commons Villas stays archived (8.04.t).
- [ ] **US3 #14, #15.** After ending Morgan first, the outcome changes to Onboarding with its reason, and Morgan is listed as ended (8.04.u).
- [ ] **US3 #13.** The "never Active" outcome line is on the "Restore outcome lines" post-it beside 8.04.t, with the §5 wording.
- [ ] **US3 #16.** Restoring Old Mill Commons Villas is refused, naming Old Mill Commons, with "Close" (8.04.v).
- [ ] **FR-016.** Change parent lists no archived community. `PARENT_CYCLE` (8.04.w) and `PARENT_ARCHIVED` (8.04.x) appear under the field with the §5 wording. The dialog says nobody's access changes.
- [ ] **US3 #21.** Jordan opening Keystone Park's settings lands on My Communities with "You don't have access to that page." and nothing about Keystone Park (8.04.y).
- [ ] **US5 #3, FR-032.** Lee Morales opening Sakura Heights' settings lands on Community Home with the same toast. "Community settings" is locked in his nav (8.04.z).
- [ ] **FR-042.** Every refusal drawn says why in plain language, except the two page refusals, which name nothing.
- [ ] **US5 #4, FR-033.** No change-history view or log appears anywhere.
- [ ] **Design choices.** D1–D10 and Q1–Q5 are on post-its beside the named artboards.
- [ ] **Words, not colour.** Every status, flag and invitation state is a word. No ✓ or coloured dot stands alone.
- [ ] **Accessibility.** Notes on the artboards, or §7, cover dialog focus, the disabled Mark Active's reason, the error summary focus and the section-nav landmark.
- [ ] **Frames.** Company Administrator pages have no violet banner and no board-mode control. Board-mode pages keep dark ink on violet.
- [ ] **Data.** Only conventions §4 people, communities, dates and counts appear. Data the dataset lacks is a `wf-ph` bar. No pending invitee is shown by name.
- [ ] **Canvas.** Labels and ids match §1 exactly (s8-04a to s8-04z). Sections 1–7 render as before.
