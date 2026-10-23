# 06 — Residents

This brief is for the Residents area of Managers and memberships: the property list, search, filters, invite, remove, resend and revoke, for Community Managers and Company Administrators. §1 is the prompt to paste into Claude Design. The other sections hold the details the prompt points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file. Brief 04 sets the page frame and brief 05 the section layout, dialogs and invitation patterns. This brief refers to them and does not repeat them.

## 1. Prompt for Claude Design

```text
Project "HOA Management CRM", canvas NekoHOA Wireframes.html. Add to section "8 · Company administration" (DCSection id "company-admin"; create it after section 7 if missing).

Attached: spec.md (the authority), 00-conventions.md (rules, dataset, refusal copy) and 06-residents.md (§4 data and states, §5 copy, §6 behaviour).

Draw the Residents area, last in Community settings › Managers and memberships, inside brief 04's header and brief 05's layout (conventions §7.2 if absent). Show the areas above it as wf-ph bars. Viewpoints: Community Manager (Jordan Blake, WFShellBoard board mode); Company Administrator (Robin Hale, company-area frame, breadcrumb, no violet banner).

File wf-company-residents.jsx; ids s8-06a to s8-06y; 1360 wide (8.06.y 1440); dialogs over their dimmed page. Labels exactly:
8.06.a Residents · first page · Community Manager
8.06.b Residents · first page · Company Administrator
8.06.c Residents · search · keystone park dr #29 · Community Manager
8.06.d Residents · filter No resident · Community Manager
8.06.e Residents · no results · Community Manager
8.06.f Invite resident · from the drawer · Community Manager
8.06.g Residents · invitation sent · Community Manager
8.06.h Invite resident · refused · already a resident · Company Administrator
8.06.i Invite resident · refused · invitation already pending · Community Manager
8.06.j Invite resident · refused · own email · Community Manager
8.06.k Invite resident · refused · too many requests · Community Manager
8.06.l Invite resident · Keystone Ridge · Onboarding · Community Manager
8.06.m Residents · Keystone Ridge · invitation scheduled · Community Manager
8.06.n Residents · Keystone Ridge · filter Invitation pending · Community Manager
8.06.o Residents · Keystone Ridge · after Mark Active · Company Administrator
8.06.p Residents · expired invitation resent · Company Administrator
8.06.q Revoke invitation dialog · kim@example.com · Community Manager
8.06.r Remove resident dialog · Jane Alvarez · autopay cancelled · Company Administrator
8.06.s Residents · Jane Alvarez removed · Company Administrator
8.06.t Remove resident dialog · Sam Ortiz · autopay stays · Community Manager
8.06.u Residents · Keystone Park · properties not loaded · Company Administrator
8.06.v Residents · Old Mill Commons · archived · Company Administrator
8.06.w Remove resident dialog · refused · no longer a manager · Board Member
8.06.x Another community's Residents · refused · Community Manager
8.06.y Residents · property drawer open · #29 · Community Manager

Decision (2026-10-17): every action lives in a property drawer. List rows are short and have no buttons. Clicking a property opens its drawer on the right (8.06.y); invite, resend, revoke and remove start there, and their dialogs are drawn over the open drawer. The header's "Invite a resident" stays.

Reuse BoardArchApps (counted filters, search, table card), BoardVendors (header bar), GlossaryPanel (drawer), wf-card, wf-table, wf-btn, wf-pill, wf-field, wf-ph. Sections 1–7 stay unchanged. Brief §4.5 goes on a DCPostIt by 8.06.a.

Lofi, section 7's visual language. Layout, columns, copy, states and who sees what are binding; colour and spacing are placeholders. States and flags are words, never colour alone.

Don't draw: invitee names; email lookups or account hints; balances, owners, autopay or payments in the list; other communities' names or homes; resident-rights controls; Accepted invitations; an invite action with no properties; any action in the archived view; error codes; Notifications; phone layouts.

Then open each artboard: check it renders without console errors, its rows match §4 and its copy matches §5 word for word. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

A new HOA isn't useful until its residents are on it (US8). Today a resident can only join with a claim code mailed to the owner's contact on file, and only while the home has no resident. The Residents area lets staff invite residents by email and remove them after a sale or move-out.

- **Community Managers** (Jordan Blake) reach it in board mode: "Community settings", then "Managers and memberships", then the "Residents" jump link (brief 05). They see every property of the community they manage, invite and remove residents, and resend or revoke resident invitations (US5 #2, FR-024, FR-024c).
- **Company Administrators** (Robin Hale) reach it from the portfolio, through the community's Community settings page, with no membership in the community (US8 #16). They have **the same controls** in this area as a Community Manager (spec permissions table). Only the frame differs. In an Archived community the area is read-only (US5 #7).
- **Nobody invites themselves** (FR-003a). **Board Members, Accountants and Residents** can't open the page (US5 #3) and are refused any invite or removal (US8 #12).

What the design must make obvious:

- An invitee is an email address until they accept. There is no name and no hint whether the email has an account (FR-018, FR-024a).
- In an **Onboarding** community nothing is emailed. Invitations wait as **Scheduled** until a Company Administrator marks it Active (FR-024b, SC-012).
- **Remove resident** takes effect at once, and the dialog says exactly what happens to that property's autopay (FR-024c, SC-011).

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| US8 #1 | Jordan invites kim@example.com from #29's row. The property is fixed and the email is the only field. Then the toast "Invitation sent to kim@example.com." and a Pending row showing the email only. Acceptance is brief 10; seeing the home is brief 11. | 8.06.f, g |
| US8 #2 | #29 lists two residents, Jane Alvarez and Sam Ortiz. The invite dialog says the invitee is added next to the residents already there. | 8.06.a, f |
| US8 #3 | The toast and the Pending row are the same whether or not the email has an account. The dialog says an existing account signs in to accept and nothing changes until then. Sam Ortiz joined #29 on Oct 9 by signing in and accepting: he is listed as a resident and has no invitation row left. | 8.06.a, f, g |
| US8 #4 | Keystone Ridge (Onboarding): the dialog says nothing is emailed yet and its button reads "Save invitation"; the toast "Invitation saved. It will be sent when Keystone Ridge is marked Active."; Scheduled rows with Resend disabled and the reason beside it. After Mark Active, both rows are Pending, sent Oct 12, 2026, 10:00 AM EDT, expiring Oct 15, 2026, 10:00 AM EDT. | 8.06.l, m, n, o |
| US8 #5 | max@example.com's Expired row has Resend. After the resend: Pending, a new 72 hours, "Sent by Robin Hale", and the toast says the earlier link no longer works. Link refusals are brief 10. | 8.06.a, p |
| US8 #6 | The Remove Jane dialog says: access to #29 ends at once, even if Jane is signed in; #29's autopay is cancelled because Jane enrolled it, and no new automatic payment starts; Jane is emailed about the removal and that autopay ended; Sam is emailed that autopay ended; Jane's account, any other homes and roles don't change. | 8.06.r, s |
| US8 #7 | The mirror case in the dataset: Remove Sam. #29's autopay stays on, because Sam didn't enroll it. | 8.06.t |
| US8 #8 | The Remove Jane dialog says Jane stays a Board Member of Sakura Heights and can still sign in. Afterwards, Board members and accountants is unchanged. | 8.06.r, s |
| US8 #9 | `ALREADY_RESIDENT` under the email field. | 8.06.h |
| US8 #10 | `INVITATION_PENDING` with a "Resend invitation" button. The Scheduled version (no Resend) is copy in §5. | 8.06.i |
| US8 #11 | Jordan opens a link to Briar Hollow's Residents. He lands in board mode with "You don't have access to that page." Briar Hollow is named nowhere; a property or community that doesn't exist looks the same. The property picker offers only this community's properties. | 8.06.x, h |
| US8 #12 | Lee Morales, now a Board Member only, presses Remove from an old tab and gets "You don't have permission to do that." | 8.06.w |
| US8 #13 | First page "Showing 1–25 of 120": address; residents with name, email and resident since; invitations with sent by. Search "keystone park dr #29" gives exactly that row. Filter "No resident" gives exactly 7. | 8.06.a, c, d |
| US8 #14 | Keystone Park: "Properties haven't been loaded for Keystone Park yet." No invite button, search, filters or table. | 8.06.u |
| US8 #15 | Revoke confirmation for kim@example.com: the link stops working and no link to the property is made. Afterwards the row reads "Revoked" with who and when, and has no actions. | 8.06.q |
| US8 #16 | Robin, from the portfolio (breadcrumb), sees the same list and controls, resends an invitation (emailed) and removes Jane. Robin's first send looks like 8.06.g in the company-area frame. | 8.06.b, p, r, s |
| US8 #17 | Jordan types jordan@example.com: `SELF_GRANT_NOT_ALLOWED` under the email field. | 8.06.j |
| US8 #18, #19 | Nothing visible. Each load and search, and each change, is logged (FR-024e, spec 025 FR-017). The design adds no history view. | — |
| FR-024 | Invite by email to one property of this community; `ALREADY_RESIDENT`; another community's property can't be chosen and is refused with `FORBIDDEN`. | 8.06.f, h, x |
| FR-024a | Generic helper text about existing accounts; identical toast and row. | 8.06.f, g |
| FR-024b, SC-012 | No email while Onboarding: "Save invitation", Scheduled rows, disabled Resend. All Scheduled rows are emailed at activation, each with 72 hours from then. | 8.06.l, m, n, o |
| FR-024c, SC-011 | Remove: access ends at once; autopay cancelled only when the removed resident enrolled it, or no enroller is recorded (§5 variant); who is emailed; owner of record and balance unchanged; account, other homes and memberships unchanged. | 8.06.r, s, t |
| FR-024d | Columns; search by address, resident name or email; filters "No resident", "Invitation pending" (Scheduled rows labelled "Scheduled"), "Invitation expired"; 25 per page; empty state with no invite action. | 8.06.a, c, d, e, n, u |
| FR-024e | Logging is not visible. | — |
| FR-024f | No control restricts resident rights (for example poll voting). Claim codes are unchanged; the area's helper line mentions them (design choice §4.5). | 8.06.a |
| FR-022 (resident states) | "Scheduled", "Pending", "Expired", "Revoked", "Revoked — community archived" as text chips. Accepted invitations are not listed; the person shows as a resident. A resend makes the resender the sender. | 8.06.a, m, n, p, v |
| FR-003a | Own email refused. A Company Administrator inviting their own email gets the same message (US7 #10, §5). | 8.06.j |
| US5 #7 | Old Mill Commons: banner, plain text, search, filters and pages still work, no actions. rae@example.com reads "Revoked — community archived" / "Sep 30, 2026". | 8.06.v |
| SC-010 | Inviting from a property's drawer needs one field and one button. | 8.06.f |
| FR-042 | Every refusal says why, except the two `FORBIDDEN` messages. | 8.06.h–k, w, x |

## 4. Artboards

### 4.1 The Residents area (every page artboard)

**Page.** Brief 04's header and searchable side-list section nav, "Managers and memberships" current. Brief 05's section heading, intro and jump links. Then Managers, Board members and accountants, and Invitations, each drawn as one `wf-ph` bar ("Managers — as 8.05.a", and so on; archived: "as 8.05.x"). Then the Residents card, in full.

**Residents card,** top to bottom:

1. **Header bar** (`BoardVendors`): title "Residents" left; button "Invite a resident" right. No button when archived or when no properties are loaded.
2. **Helper line** (`wf-sub`, §5). An Onboarding community adds the Onboarding line.
3. **Search and filters** (`BoardArchApps`): a labelled search field, then one row of filter buttons with counts. One filter is on at a time; "All" is the default.
4. **Table** (`wf-table`), one row per property. Columns: **Property · Residents · Invitations**. No buttons in rows; the whole row, and the address as its button, opens the property drawer.
   - **Property:** the address, as the row header. A "No resident" chip under it when the property has no resident.
   - **Residents:** the residents' names, comma-separated ("Jane Alvarez, Sam Ortiz"). Empty when there is none. When a search matched a resident's email, that email shows under the name.
   - **Invitations:** a short summary in words, for example "1 pending" or "1 scheduled · 1 expired"; "None" when there are no open invitations. Revoked invitations aren't counted here; the drawer lists them.
5. **Footer:** "Showing 1–25 of 120", then Previous, page numbers, Next. Previous is disabled on page 1.

**Property drawer** (`GlossaryPanel` pattern, right side, the list stays visible behind it). Top to bottom:

1. Heading: the address, with "No resident" chip if it applies; "Close" button.
2. **Residents:** one block per resident: name; email; "Resident since Mar 2, 2024"; a "Remove" button. "No residents yet." when empty.
3. **Invitations:** one block per scheduled, pending, expired or revoked invitation, newest first, revoked last: the email and its state chip; "Sent by Jordan Blake · Oct 10, 2026, 4:40 PM EDT"; the state's second line (conventions §5.2); "Resend" and "Revoke" where allowed. "No invitations." when empty.
4. Button "Invite resident" (property fixed). Not shown when archived.

In an archived community the drawer opens read-only: same content, no buttons.

**Default order** (design choice): by address, street A–Z, then house and unit numbers in number order (#9 before #10).

**Frames.** As brief 05 §4.2. Jordan: strip "Sakura Heights · Community Manager", or "Keystone Ridge", the "Onboarding" chip, "· Community Manager". Robin: breadcrumb "Portfolio › Sakura Heights" (or the community shown) and the header's status actions. Lee Morales (8.06.w): brief 05's old-tab strip "Sakura Heights · Board Member · Community Manager".

### 4.2 Data

"Now" is Oct 12, 2026, 10:00 AM EDT unless an artboard says otherwise. All times are EDT. Use only conventions §4.

**Sakura Heights, baseline.** Filters "All · 120", "No resident · 7", "Invitation pending · 2", "Invitation expired · 1". Page 1 holds the three rows below and 22 filler rows on Keystone Park Dr, in address order. Each filler row has one or two residents (made-up names, emails at example.com) and no invitation, so #31 and #35 are page 1's only "No resident" rows.

| Property | Residents | Invitations |
| --- | --- | --- |
| 711 Keystone Park Dr #29 | Jane Alvarez · jane@example.com · Resident since Mar 2, 2024. Sam Ortiz · sam@example.com · Resident since Oct 9, 2026. | kim@example.com · Pending · Sent by Jordan Blake · Oct 10, 2026, 4:40 PM EDT · "Expires Oct 13, 2026, 4:40 PM EDT" · Resend, Revoke |
| 715 Keystone Park Dr #31 | No resident | max@example.com · Expired · Sent by Taylor Grant · Oct 6, 2026, 1:20 PM EDT · "Expired Oct 9, 2026, 1:20 PM EDT" · Resend, Revoke |
| 719 Keystone Park Dr #35 | No resident | noor@example.com · Pending · Sent by Taylor Grant · Oct 11, 2026, 10:10 AM EDT · "Expires Oct 14, 2026, 10:10 AM EDT" · Resend, Revoke |

#29 has active autopay, enrolled by Jane. The list doesn't show it; only the Remove dialog does.

**Other filters (not drawn):** "Invitation pending" lists #29 and #35. "Invitation expired" lists #31.

**Keystone Ridge** (Onboarding, 64 properties on Ridgeview Ct, no residents). Filters "All · 64", "No resident · 64", "Invitation pending · 2", "Invitation expired · 0". Page 1 runs from 1 Ridgeview Ct to 25 Ridgeview Ct, each with "No resident". Two rows carry invitations: 2 Ridgeview Ct, lena@example.com, and 8 Ridgeview Ct, omar@example.com. Both are Scheduled, "Sent by Jordan Blake · Not sent yet (saved Oct 10, 2026)", "Sent when Keystone Ridge is marked Active", Resend disabled, Revoke.

**Keystone Park** (Onboarding, 0 properties). Robin's header has the "No manager" chip (brief 04 D6) and the actions "Mark Active" and "Archive".

**Old Mill Commons** (Archived, 86 properties, 79 residents linked). Filters "All · 86", "No resident" with a `wf-ph` count (not in the dataset), "Invitation pending · 0", "Invitation expired · 0". Footer "Showing 1–25 of 86". Page 1 includes 16 Old Mill Ln (Sam Ortiz · sam@example.com · Resident since `wf-ph`) and 22 Old Mill Ln (filler residents; rae@example.com · "Revoked — community archived" / "Sep 30, 2026" · Sent by Morgan Ellis · Sep 29, 2026, 11:30 AM EDT · "Expires Oct 2, 2026, 11:30 AM EDT"). Filler rows are on Old Mill Ln.

**Snapshots** (each starts from the baseline):

| Snapshot | Change | Artboards |
| --- | --- | --- |
| Oct 10, 4:40 PM | Now is Oct 10, 2026, 4:40 PM EDT. No noor@example.com invitation yet. max@example.com is already Expired. Filters: "Invitation pending · 0" before the send, "· 1" after; "Invitation expired · 1". | 8.06.f, g, k |
| Keystone Ridge, Oct 10 | Now is Oct 10, 2026. lena@example.com is already saved; Jordan saves omar@example.com. | 8.06.l, m |
| Keystone Ridge, live | Right after brief 04's 8.04.j and 8.04.k, Oct 12, 2026, 10:00 AM EDT. | 8.06.o |
| Lee, old tab | After brief 05's 8.05.r: Lee Morales is a Board Member only, with the page still open. | 8.06.w |

### 4.3 Artboard list

**Drawer rule.** Every action and every invitation or resident "block" mentioned below is in that property's drawer. Draw the drawer open on the property named, with any dialog over it. Rows themselves only change their summary text.

**8.06.a Residents · first page · Community Manager.** Jordan Blake. Baseline. Header without status actions. State: default.

**8.06.b Residents · first page · Company Administrator.** Robin Hale. Baseline. Breadcrumb "Portfolio › Sakura Heights"; header action "Archive". Rows and controls exactly as 8.06.a. `.wf-note`: "Same controls as a Community Manager (spec permissions table)". State: default.

**8.06.c Residents · search · keystone park dr #29 · Community Manager.** Jordan. The search field holds "keystone park dr #29". One row, #29 as in the baseline. Footer "Showing 1–1 of 1". Filters keep their counts; "All" stays on. State: search result.

**8.06.d Residents · filter No resident · Community Manager.** Jordan. "No resident · 7" on. Seven rows: #31 (max@, Expired), #35 (noor@, Pending) and 5 filler addresses with no invitations. Footer "Showing 1–7 of 7"; no page numbers. State: filtered.

**8.06.e Residents · no results · Community Manager.** Jordan. Search holds "jane alvarex". In place of the table: "No properties match “jane alvarex”." and the button "Clear search". Filters stay. No footer. State: no results.

**8.06.f Invite resident · from a row · Community Manager.** Jordan. Snapshot "Oct 10, 4:40 PM". Opened from "Invite resident" in #29's drawer. Title "Invite a resident" with "Sakura Heights" under it. Property shown as fixed text: "711 Keystone Park Dr #29", with the line "This property has 2 residents. Whoever accepts is added next to them." Email address: "kim@example.com". Helper and summary (§5). Buttons "Send invitation", "Cancel". State: default (filled).

**8.06.g Residents · invitation sent · Community Manager.** Jordan. Same snapshot, right after 8.06.f. Toast "Invitation sent to kim@example.com." #29's drawer lists kim@example.com as Pending, and #29's row now reads "1 pending". "Invitation pending · 1". Focus on the #29 address. `.wf-note`: "Same toast and row whether or not kim@ has an account (FR-018, FR-024a)." State: success.

**8.06.h Invite resident · refused · already a resident · Company Administrator.** Robin. Baseline. Opened from the header's "Invite a resident". "Property (required)" is a combobox; search "#29"; one option "711 Keystone Park Dr #29", "✓ Selected". Email "jane@example.com". Under the email field: "jane@example.com is already a resident of 711 Keystone Park Dr #29." Input kept; dialog open. State: refusal (`ALREADY_RESIDENT`).

**8.06.i Invite resident · refused · invitation already pending · Community Manager.** Jordan. Baseline. From #29's drawer, email "kim@example.com". Under the field: the `INVITATION_PENDING` message, then the button "Resend invitation". "Send invitation" stays. State: refusal.

**8.06.j Invite resident · refused · own email · Community Manager.** Jordan. Baseline. From #35's drawer, email "jordan@example.com". Under the field: the `SELF_GRANT_NOT_ALLOWED` message. State: refusal.

**8.06.k Invite resident · refused · too many requests · Community Manager.** As 8.06.f. Jordan pressed "Send invitation". Next to the button: "Too many requests. Wait a few minutes, then try again." Input kept. State: refusal (rate limit, FR-041).

**8.06.l Invite resident · Keystone Ridge · Onboarding · Community Manager.** Jordan. Snapshot "Keystone Ridge, Oct 10". Opened from 8 Ridgeview Ct's drawer. Email "omar@example.com". The Onboarding helper in the dialog, the scheduled summary, and the buttons "Save invitation", "Cancel" (§5). The page behind shows lena@example.com Scheduled on 2 Ridgeview Ct. State: default (Onboarding).

**8.06.m Residents · Keystone Ridge · invitation scheduled · Community Manager.** Jordan. Same snapshot, after 8.06.l. Toast "Invitation saved. It will be sent when Keystone Ridge is marked Active." The Onboarding helper line under the header. 2 and 8 Ridgeview Ct show their Scheduled blocks. Each Resend is disabled with "Sent when Keystone Ridge is marked Active" as its reason; Revoke is enabled. `.wf-note`: "Nothing emailed while Onboarding (FR-024b, SC-012)." State: success (scheduled).

**8.06.n Residents · Keystone Ridge · filter Invitation pending · Community Manager.** Jordan. Oct 12. "Invitation pending · 2" on, with the line "Includes scheduled invitations." under the filters. Rows 2 and 8 Ridgeview Ct, chips reading "Scheduled". Footer "Showing 1–2 of 2". State: filtered (FR-024d label).

**8.06.o Residents · Keystone Ridge · after Mark Active · Company Administrator.** Robin. Snapshot "Keystone Ridge, live". Header chip "Active", action "Archive". No Onboarding line. lena@ and omar@ read Pending, "Sent by Jordan Blake · Oct 12, 2026, 10:00 AM EDT", "Expires Oct 15, 2026, 10:00 AM EDT", with Resend and Revoke enabled. `.wf-note`: "Emailed at activation; 72 hours from then. Jordan still had authority, so they were sent (FR-022, FR-024b)." State: success.

**8.06.p Residents · expired invitation resent · Company Administrator.** Robin. Baseline, right after "Resend" on max@example.com. Toast "Invitation sent again to max@example.com. The earlier link no longer works." #31's block: Pending, "Sent by Robin Hale · Oct 12, 2026, 10:00 AM EDT", "Expires Oct 15, 2026, 10:00 AM EDT". Filters "Invitation pending · 3", "Invitation expired · 0". Focus stays on that "Resend". `.wf-note`: "A resend makes the resender its sender (FR-022)." State: success.

**8.06.q Revoke invitation dialog · kim@example.com · Community Manager.** Jordan. Baseline, over 8.06.a. Copy in §5. Afterwards (not drawn): toast "Invitation to kim@example.com revoked."; the block reads "Revoked" / "Revoked by Jordan Blake, Oct 12, 2026" with no buttons. State: confirmation.

**8.06.r Remove resident dialog · Jane Alvarez · autopay cancelled · Company Administrator.** Robin. Baseline, over 8.06.b with #29's drawer open, from Jane's "Remove". Copy in §5. Buttons "Remove resident", "Cancel". State: confirmation.

**8.06.s Residents · Jane Alvarez removed · Company Administrator.** Robin, right after 8.06.r. Toast "Jane Alvarez was removed from 711 Keystone Park Dr #29. Its autopay was cancelled." #29 shows Sam Ortiz and the kim@ invitation. Counts unchanged. Focus on the #29 address. `.wf-note`: "Jane is still a Board Member in the area above. Her next request for #29 is refused (brief 11)." State: success.

**8.06.t Remove resident dialog · Sam Ortiz · autopay stays · Community Manager.** Jordan. Baseline, over 8.06.a, from Sam's "Remove". Copy in §5. `.wf-note`: "Sam's other homes are in other communities. The dialog doesn't name them (FR-004)." State: confirmation.

**8.06.u Residents · Keystone Park · properties not loaded · Company Administrator.** Robin. Oct 12. The Residents card holds only its title and the empty-state lines (§5). No "Invite a resident", search, filters, table or footer. State: empty (US8 #14).

**8.06.v Residents · Old Mill Commons · archived · Company Administrator.** Robin. Breadcrumb "Portfolio › Old Mill Commons"; header chip "Archived"; action "Restore". The archived banner first (conventions §7.3). Search, filters and pages work. No Actions column, and no Invite, Remove, Resend or Revoke: removed, not disabled. Values are plain text. State: read-only (US5 #7).

**8.06.w Remove resident dialog · refused · no longer a manager · Board Member.** Lee Morales. Snapshot "Lee, old tab". Lee pressed "Remove" on Sam Ortiz. The dialog shows its title, then at the top "You don't have permission to do that." No consequences. Button "Close". State: refusal (`FORBIDDEN`, US8 #12).

**8.06.x Another community's Residents · refused · Community Manager.** Jordan opened a link to Briar Hollow's Residents area. He lands in board mode, Sakura Heights, "Community Home" current, with the toast "You don't have access to that page." Body: "Community Home — section 7, unchanged". Nothing names Briar Hollow. State: refusal (`FORBIDDEN`, US8 #11).

**8.06.y Residents · property drawer open · #29 · Community Manager.** Jordan. Baseline, 1440 wide. #29's row is marked as selected; the drawer is open on the right: heading "711 Keystone Park Dr #29"; "Residents" with Jane Alvarez and Sam Ortiz, each with email, "Resident since" and "Remove"; "Invitations" with kim@example.com (Pending) and "Resend", "Revoke"; button "Invite resident"; "Close". State: default.

### 4.4 States not drawn

Each looks like a drawn artboard with a different message (§5).

- **Loading:** the card frame stays, with `wf-ph` rows in the table. **Load error:** "Couldn't load residents." with "Try again".
- **Filter with no rows:** each filter's empty line (§5).
- **Busy:** "Sending…", "Saving…" or "Removing…" on the primary button, disabled.
- **Invalid email**, **no property chosen**, **picker with no match**, **save failed**: §5.
- **`INVITATION_PENDING` for a Scheduled invitation** (lena@example.com to 2 Ridgeview Ct): no "Resend invitation" button.
- **Resend on a Scheduled invitation reached anyway:** the `COMMUNITY_NOT_ACTIVE` toast.
- **Resend or Revoke on an invitation just accepted or revoked:** the `INVITATION_INVALID` staff toast; the row refreshes.
- **Kim accepts:** the kim@example.com block leaves the list and "Kim Tran · kim@example.com · Resident since <the acceptance date>" joins #29.
- **Revoked because the sender lost authority** (brief 05 8.05.q, 8.05.w): noor@ or kim@ read "Revoked" / "Sender's role ended, Oct 12, 2026".
- **Keystone Ridge archived instead of activated:** lena@ and omar@ read "Revoked — community archived", never sent.
- **Last resident removed:** the property shows "No resident". The dialog says so (§5).
- **Old-tab write in an archived community:** the `COMMUNITY_ARCHIVED` banner in its Company Administrator or Community Manager wording.
- **Robin invites owner@example.com:** the `SELF_GRANT_NOT_ALLOWED` message (US7 #10).

### 4.5 Design choices and open questions (DCPostIt by 8.06.a)

> 1. **Where actions live (decided 2026-10-17).** In the property drawer (8.06.y). Rows are short and have no buttons.
> 2. **Order.** By address. Other option: properties that need attention first ("No resident", then expired invitations).
> 3. **Filters.** One at a time; counts cover the whole community and don't follow the search. Other options: combinable filters; counts that follow the search.
> 4. **Search** runs as you type, after a short pause. It matches addresses, resident names and resident emails. Open question: should it also match invited emails? FR-024d says "resident name or email".
> 5. **Revoked invitations** are always shown in the drawer, last in the list. Other option: hidden behind "Show revoked (1)".
> 6. **Two ways to invite:** the drawer's "Invite resident" fixes the property; the header's "Invite a resident" asks for it. Other option: drawer only.
> 7. **Onboarding:** the button reads "Save invitation", because nothing is emailed yet, and the area has an Onboarding helper line.
> 8. **Other homes.** The Remove dialog never says whether the resident has other homes, because they may be in other communities (FR-004). It says "any other homes" don't change.
> 9. **Autopay line** names only the person being removed ("Sam Ortiz didn't enroll it"). Other option: name who enrolled it.
> 10. **Claim codes.** The helper line mentions them (FR-024f). Other option: no mention.
> 11. **Resend** has no confirmation, as in brief 05.
> 12. **Removing someone already removed** by another person: the spec gives no code. Suggested toast in §5.
> 13. **Expired invitation, same email and property.** The spec refuses duplicates only for scheduled or pending invitations (FR-022). Open question: should the dialog point to Resend instead of creating a second invitation?
> 14. **Reason for the disabled Resend.** Drawn: the drawer's line "Sent when Keystone Ridge is marked Active" (conventions §5.2). Other option: the longer `COMMUNITY_NOT_ACTIVE` message beside the button (conventions §6.1).

## 5. Copy

Exact strings. "SR" means text for screen readers only. Conventions §6 messages are quoted as they are there.

**Area**

| Element | Text |
| --- | --- |
| Title | "Residents" |
| Header button | "Invite a resident" (SR: "Invite a resident to a property in Sakura Heights") |
| Helper | "Every property in Sakura Heights, its residents and its resident invitations. Invitation links work for 72 hours. Times are in Sakura Heights' time zone. A property with no resident can also be claimed with the claim code mailed to its owner." |
| Onboarding helper | "Keystone Ridge is Onboarding. Resident invitations are saved as Scheduled and emailed when it is marked Active." |
| Search label · placeholder | "Search residents" · "Search by address, resident name or email" |
| Filters | "All · 120" · "No resident · 7" · "Invitation pending · 2" · "Invitation expired · 1" (SR group: "Filter properties") |
| Invitation pending note | "Includes scheduled invitations." |
| Table caption (SR) | "Properties in Sakura Heights, with residents and invitations" |
| Columns | "Property" · "Residents" · "Invitations" · "Actions" |
| Flag | "No resident" |
| Resident | "Jane Alvarez" · "jane@example.com" · "Resident since Mar 2, 2024" |
| Invitation lines | "Sent by Jordan Blake · Oct 10, 2026, 4:40 PM EDT" · Scheduled: "Sent by Jordan Blake · Not sent yet (saved Oct 10, 2026)" |
| Second lines | "Expires Oct 13, 2026, 4:40 PM EDT" · "Expired Oct 9, 2026, 1:20 PM EDT" · "Sent when Keystone Ridge is marked Active" · "Revoked by Jordan Blake, Oct 12, 2026" · "Sep 30, 2026" · "Sender's role ended, Oct 12, 2026" |
| State chips | "Scheduled" · "Pending" · "Expired" · "Revoked" · "Revoked — community archived" |
| Buttons | "Remove" (SR: "Remove Jane Alvarez from 711 Keystone Park Dr #29") · "Resend" (SR: "Resend invitation to kim@example.com") · "Revoke" (SR: "Revoke invitation to kim@example.com") · "Invite resident" (SR: "Invite a resident to 711 Keystone Park Dr #29") |
| Footer | "Showing 1–25 of 120" · "Previous" · "Next" (SR nav label: "Pages") |
| No results | "No properties match “jane alvarex”." · button "Clear search" |
| Filter empty | "Every property has a resident." · "No properties have a pending or scheduled invitation." · "No properties have an expired invitation." |
| Empty, no properties | "Properties haven't been loaded for Keystone Park yet." then "Residents can be invited once its properties are loaded." |
| Load error | "Couldn't load residents." · "Try again" |

**Invite dialog**

| Element | Text |
| --- | --- |
| Title · line under it | "Invite a resident" · "Sakura Heights" |
| Fixed property | "Property: 711 Keystone Park Dr #29" |
| Existing residents | "This property has 2 residents. Whoever accepts is added next to them." |
| Property picker | "Property (required)" · placeholder "Search by address" · note "Only properties in Sakura Heights are listed." · result count (SR, polite): "1 property matches." |
| Email | "Email address (required)" · helper "Until they accept, the invitation shows only this email address." |
| Summary, Active | "We'll email kim@example.com an invitation to be a resident of 711 Keystone Park Dr #29. It's valid for 72 hours. If they already have an account, they sign in to accept. Nothing changes until they accept." |
| Onboarding helper | "Keystone Ridge is Onboarding, so nothing is emailed yet. The invitation is saved as Scheduled and emailed when a Company Administrator marks Keystone Ridge Active. The link then works for 72 hours." |
| Summary, Onboarding | "We'll save an invitation for omar@example.com to be a resident of 8 Ridgeview Ct. It's emailed when Keystone Ridge is marked Active." |
| Buttons | "Send invitation" · Onboarding: "Save invitation" · "Cancel" · busy: "Sending…" / "Saving…" |

**Remove resident dialog** (conventions §7.4)

*Jane Alvarez (8.06.r)*

- Title: "Remove Jane Alvarez from 711 Keystone Park Dr #29?"
- What happens:
  - "Jane Alvarez loses access to 711 Keystone Park Dr #29 at once, even if they're signed in right now."
  - "This property's autopay is cancelled, because Jane Alvarez enrolled it. No new automatic payment starts. A payment already in progress finishes normally."
  - "Jane Alvarez is emailed that they were removed and that autopay ended."
  - "The other resident, Sam Ortiz, is emailed that autopay ended."
- What doesn't change:
  - "The owner of record and the balance of 711 Keystone Park Dr #29 don't change."
  - "Jane Alvarez's account, any other homes and roles don't change. They're still a Board Member of Sakura Heights and can still sign in."
- Buttons: "Remove resident" · "Cancel".

*Sam Ortiz (8.06.t)*

- Title: "Remove Sam Ortiz from 711 Keystone Park Dr #29?"
- What happens: "Sam Ortiz loses access to 711 Keystone Park Dr #29 at once, even if they're signed in right now." · "Sam Ortiz is emailed that they were removed."
- What doesn't change: "This property's autopay stays on. Sam Ortiz didn't enroll it." · "The owner of record and the balance of 711 Keystone Park Dr #29 don't change." · "Sam Ortiz's account, any other homes and roles don't change. They can still sign in."
- Buttons: "Remove resident" · "Cancel".

*Variants (not drawn)*

- No recorded enroller (enrolled before this feature): "This property's autopay is cancelled. It was set up before the app recorded who enrolled it, so removing any resident cancels it." The email lines are as for Jane.
- No autopay: "This property has no active autopay."
- Last resident: "711 Keystone Park Dr #29 will have no resident and shows “No resident”."

**Revoke invitation dialog (8.06.q)**

- Title: "Revoke the invitation to kim@example.com?"
- What happens: "The link in the invitation email stops working at once." · "kim@example.com can't use it to become a resident of 711 Keystone Park Dr #29." Scheduled: "It won't be emailed when Keystone Ridge is marked Active."
- What doesn't change: "You can send kim@example.com a new invitation later."
- Buttons: "Revoke invitation" · "Cancel".

**Toasts**

| Action | Text |
| --- | --- |
| Sent (same with or without an account) | "Invitation sent to kim@example.com." |
| Saved while Onboarding | "Invitation saved. It will be sent when Keystone Ridge is marked Active." |
| Resend | "Invitation sent again to max@example.com. The earlier link no longer works." |
| Revoke | "Invitation to kim@example.com revoked." |
| Remove, autopay cancelled | "Jane Alvarez was removed from 711 Keystone Park Dr #29. Its autopay was cancelled." |
| Remove | "Sam Ortiz was removed from 711 Keystone Park Dr #29." |
| Already removed (design choice, §4.5 item 12) | "Jane Alvarez is no longer a resident of 711 Keystone Park Dr #29. The list is now up to date." |

**Refusals and errors** (conventions §6)

| Case | Text | Where |
| --- | --- | --- |
| `ALREADY_RESIDENT` | "jane@example.com is already a resident of 711 Keystone Park Dr #29." | Under the email field. |
| `INVITATION_PENDING`, pending | "kim@example.com already has a pending invitation to 711 Keystone Park Dr #29. Resend it instead?" Button "Resend invitation". | Under the email field. |
| `INVITATION_PENDING`, scheduled | "lena@example.com already has a scheduled invitation to 2 Ridgeview Ct. It will be sent when Keystone Ridge is marked Active." No button. | Under the email field. |
| `SELF_GRANT_NOT_ALLOWED` | "You can't give yourself a role or a home. Ask another Company Administrator or this community's Community Manager." | Under the email field. |
| `COMMUNITY_NOT_ACTIVE`, resend | "This invitation will be sent when Keystone Ridge is marked Active. It can't be sent before then." | Toast, if reached anyway. |
| `INVITATION_INVALID`, staff | "This invitation was already accepted or revoked. The list is now up to date." | Toast; the row refreshes. |
| `FORBIDDEN`, action · page | "You don't have permission to do that." · "You don't have access to that page." | Top of the dialog with "Close" · toast on the landing page. |
| `COMMUNITY_ARCHIVED` | Company Administrator: "Old Mill Commons is archived, so it can't be changed. Restore it to make changes." Community Manager: "Juniper Row Community Management no longer manages Old Mill Commons, so it can't be changed." | Banner. |
| Rate limited | "Too many requests. Wait a few minutes, then try again." | Next to the button pressed. |
| Invalid email | "Enter a valid email address." | Under the email field. |
| No property chosen (design choice) | "Choose a property." | Under the picker. |
| Picker, no match | "No property in Sakura Heights matches “#290”." | In the picker list. |
| Save failed | "Couldn't save. Check your connection and try again." | Top of the dialog. |

**Archived banner:** conventions §7.3, unchanged.

## 6. Interactions

**Opening.** Community Manager: board mode › "Community settings" › "Managers and memberships" › "Residents" jump link. Company Administrator: the portfolio › the community › the same. The jump link moves focus to the "Residents" heading. Each load and search is logged (not visible).

**Search and filters.** Typing updates the list after a short pause (§4.5 item 4). Changing the search or the filter returns to page 1 and announces the new count. "Clear search" empties the field, restores the list and returns focus to the field.

**Invite.**

1. The drawer's "Invite resident" opens the dialog with that property fixed. The header's "Invite a resident" opens it with the property picker, which searches this community's properties by address and lists no other community's. Focus moves to the dialog title.
2. Email field only: no lookup, no suggestions, no account hint (FR-018).
3. Submit checks the email format and the property first. Then the button shows "Sending…" (or "Saving…" while Onboarding).
4. **Success:** the dialog closes, the toast shows, and focus moves to the property's address in the list. The new block is Pending (Active) or Scheduled (Onboarding), with the email only.
5. **Refusal:** the dialog stays open with the input kept. Inline refusals are announced at once. "Resend invitation" resends the existing invitation, closes the dialog and shows the resend toast. Duplicates are checked per property: the same email may be invited to two properties.

**Resend.** One press, no confirmation. New link and new 72 hours; the resender becomes the sender; the old link stops working (FR-022). The block updates in place; focus stays on "Resend". On a Scheduled invitation, Resend is disabled with its reason beside it. It stays reachable by keyboard, so the reason is read.

**Revoke.** Opens 8.06.q. On confirm, the block reads "Revoked" with who and when, loses its buttons, and focus moves to the property's address.

**Remove resident.** Opens the dialog. Its lines come from the server when it opens: who is emailed, and whether autopay is cancelled (FR-024c). On confirm, the change is made in one action. The resident leaves the row (or the row shows "No resident"), the toast shows, and focus moves to the property's address. On `FORBIDDEN` the message shows at the top and the primary becomes "Close"; closing reloads the page, which is refused, and the app moves the person to their landing page with "You don't have access to that page."

**Archived.** Search, filters and pages work; nothing else is offered.

**Who sees what.**

| Control | Community Manager | Company Administrator | Archived (Company Administrator) |
| --- | --- | --- | --- |
| List, search, filters, pages | Yes | Yes | Yes |
| Invite a resident, Invite resident | Yes | Yes | No |
| Remove | Yes | Yes | No |
| Resend, Revoke | Yes | Yes | No |

Hiding is for clarity only. The server decides, and each hidden action has a refusal in §5.

## 7. Accessibility

The conventions §8 baseline applies. For this area:

- **Headings.** "Residents" is an `h3` under the "Managers and memberships" `h2`. The drawer's heading is the address.
- **Table.** A caption (§5), real column headers, and the address as each row's header, so each resident and invitation is read with its property. The drawer's residents and invitations are lists, and the drawer's heading names the property.
- **Buttons** carry the person, email or address in their names (§5): "Remove Jane Alvarez from 711 Keystone Park Dr #29".
- **Disabled Resend** stays focusable and has its reason tied to it.
- **Search** has a visible label. **Filters** are a labelled group; the one that is on is marked as selected, not by colour alone. The result count and "No properties match …" are announced politely.
- **Pagination** is a navigation region named "Pages", with the current page marked.
- **Dialogs** follow conventions §8. In the invite dialog: title → property (picker or fixed text) → email → primary button → "Cancel". The picker is a combobox, as in brief 05; its match count is announced.
- **Refusals** under the email field are announced at once and tied to the field. A refusal at the top of a dialog takes focus.
- **Drawer** follows the glossary panel: focus moves in on open and returns to the address button on close; Esc closes it.
- **Text, not colour:** "No resident", every state chip and "Revoked — community archived" are words.
- **Zoom:** at 200% the table may scroll inside its card; the page never scrolls sideways.

## 8. Out of scope for this design

- Managers, Board members and accountants, and role Invitations (brief 05); the page header, status actions, and the Mark Active and Archive dialogs (brief 04).
- The invitation email, the acceptance and sign-up pages, and link refusals (briefs 10 and 12). The resident's view after removal, including `PROPERTY_ACCESS_DENIED` (brief 11).
- Restricting resident rights, such as poll voting (FR-024f). Claim-code issuing or management.
- Loading, adding or editing properties. Owner of record, balances and autopay status in the list.
- A history or audit view (FR-024e is logging only). Bulk invitations, export, and a page-size picker.
- Notifications. Phone and tablet artboards beyond the main page's two (conventions §2).

## 9. Design review checklist

- [ ] **US8 #1, SC-010.** 8.06.f has the property fixed and only the email to fill; 8.06.g shows "Invitation sent to kim@example.com." and a Pending block with no name.
- [ ] **US8 #2.** 8.06.a lists Jane Alvarez and Sam Ortiz on #29; 8.06.f says whoever accepts is added next to them.
- [ ] **US8 #3, FR-024a.** The dialog's summary covers existing accounts generically; nothing on screen tells an existing account apart.
- [ ] **US8 #4, FR-024b, SC-012.** 8.06.l reads "Save invitation"; 8.06.m has the saved toast and Scheduled blocks with Resend disabled and its reason; 8.06.o shows both Pending, expiring Oct 15, 2026, 10:00 AM EDT.
- [ ] **US8 #5.** 8.06.a's max@ block reads Expired with Resend; 8.06.p shows Pending, "Sent by Robin Hale", expiry Oct 15, 2026, 10:00 AM EDT, and the earlier-link toast.
- [ ] **US8 #6, FR-024c, SC-011.** 8.06.r lists: access ends at once; autopay cancelled because Jane enrolled it; no new automatic payment; who is emailed; owner of record and balance unchanged; account, other homes and roles unchanged.
- [ ] **US8 #7.** 8.06.t says autopay stays on because Sam didn't enroll it.
- [ ] **US8 #8.** 8.06.r says Jane stays a Board Member and can still sign in; 8.06.s notes the Board Member row is unchanged.
- [ ] **US8 #9.** 8.06.h shows `ALREADY_RESIDENT` under the email field.
- [ ] **US8 #10.** 8.06.i shows `INVITATION_PENDING` with "Resend invitation"; §5 has the Scheduled version without it.
- [ ] **US8 #11.** 8.06.x names nothing from the request; the picker in 8.06.h lists only Sakura Heights properties.
- [ ] **US8 #12.** 8.06.w shows "You don't have permission to do that." with "Close".
- [ ] **US8 #13, FR-024d.** 8.06.a reads "Showing 1–25 of 120" with Property, Residents (name, email, resident since), Invitations (with sent by) and Actions; 8.06.c returns exactly #29; 8.06.d returns exactly 7 rows.
- [ ] **FR-024d filters.** 8.06.n labels Scheduled rows "Scheduled" under "Invitation pending"; 8.06.e has "Clear search".
- [ ] **US8 #14.** 8.06.u shows "Properties haven't been loaded for Keystone Park yet." and no invite button, search, filters or table.
- [ ] **US8 #15.** 8.06.q says the link stops working at once; §4.3 gives the revoked block's text.
- [ ] **US8 #16.** 8.06.b, p, r and s are Robin's, with the breadcrumb and the same controls as Jordan's.
- [ ] **US8 #17, FR-003a.** 8.06.j shows the `SELF_GRANT_NOT_ALLOWED` message under the email field.
- [ ] **US8 #18, #19, FR-024e.** No history view is drawn.
- [ ] **FR-024f.** No control restricts resident rights.
- [ ] **FR-022.** Scheduled, Pending, Expired, Revoked and "Revoked — community archived" are words; no Accepted invitation is listed.
- [ ] **US5 #7.** 8.06.v has the archived banner, plain text, working search and filters, no actions, and rae@example.com as "Revoked — community archived" / "Sep 30, 2026".
- [ ] **Permission variants.** Company Administrator artboards have the breadcrumb and status actions; Community Manager artboards have neither. Residents controls are the same for both.
- [ ] **No hidden data.** No invitee has a name; the list shows no balance, owner or autopay; no other community or home is named to Jordan.
- [ ] **Drawer.** Rows have no buttons; clicking a property opens its drawer with residents, invitations and every action (8.06.y); dialogs are drawn over the open drawer.
- [ ] **Design choices.** A DCPostIt by 8.06.a lists the 14 items in §4.5; each drawn choice is labelled "Design choice".
- [ ] **Accessibility.** Caption, row headers, named buttons, labelled search and filters, focusable disabled Resend, and focus on dialog titles are noted.
- [ ] **Data and canvas.** Only conventions §4 data; times in EDT; labels and ids match §1 (s8-06a to s8-06y); sections 1–7 render as before.
