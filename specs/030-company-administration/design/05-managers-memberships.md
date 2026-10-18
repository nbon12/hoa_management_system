# 05 — Managers and memberships

This brief is for the Managers and memberships section of the Community settings page: managers, board members and accountants, and role invitations, seen by Company Administrators and Community Managers. Residents are brief 06. §1 is the prompt to paste into Claude Design. The other sections hold the details that the prompt points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file.

## 1. Prompt for Claude Design

```text
Project: "HOA Management CRM". Canvas: NekoHOA Wireframes.html. Put every new artboard in section "8 · Company administration" (DCSection id "company-admin"); create it after section 7 if missing (00-conventions.md §3).

Attached: spec.md (feature spec 030, the authority), 00-conventions.md (shared rules, dataset, vocabulary, refusal copy) and 05-managers-memberships.md (this brief: §4 layout, data and artboards, §5 exact copy, §6 behaviour). If files disagree, spec.md wins.

Draw the Managers and memberships section of Community settings: three stacked areas, Managers, then Board members and accountants, then Invitations, above a Residents placeholder (brief 06 draws Residents). Two viewpoints:
- Company Administrator (Robin Hale): the neutral company-area frame (conventions §7.1), avatar "RH", breadcrumb, no violet banner.
- Community Manager (Jordan Blake, Taylor Grant, Lee Morales): WFShellBoard in board mode, "Community settings" current in boardNav(), each person's data passed in.
Use brief 04's page header and section nav if section 8 has them; otherwise follow conventions §7.2.

Artboards, 1360 wide, as tall as the content, ids s8-05a to s8-05z. Dialogs sit over their dimmed page. Labels exactly:
8.05.a Managers and memberships · Sakura Heights · Community Manager
8.05.b Managers and memberships · Sakura Heights · actions menu open · Company Administrator
8.05.c Appoint a manager · choose Lee Morales · Community Manager
8.05.d Managers and memberships · Lee Morales appointed · Community Manager
8.05.e Managers and memberships · two manager invitations sent · Community Manager
8.05.f Appoint a board member · invite with a term end date · Community Manager
8.05.g Appoint a board member · refused · own email · Community Manager
8.05.h Appoint a board member · refused · already a Board Member · Community Manager
8.05.i Appoint a manager · Keystone Park · invite by email · Company Administrator
8.05.j Managers and memberships · Keystone Park · invitation pending · Company Administrator
8.05.k Appoint a manager · refused · invitation already pending · Company Administrator
8.05.l Invitations · expired invitation resent · Community Manager
8.05.m Revoke invitation dialog · new@example.com · Community Manager
8.05.n End membership dialog · Jordan Blake · refused · last manager · Company Administrator
8.05.o Change end date dialog · Jordan Blake · refused · last manager · Company Administrator
8.05.p Downgrade dialog · Jordan Blake · refused · last manager · Community Manager
8.05.q End membership dialog · Taylor Grant · invitations revoked · Community Manager
8.05.r Downgrade dialog · Lee Morales · keeps Board Member · Community Manager
8.05.s Downgrade dialog · Taylor Grant · becomes Board Member · Company Administrator
8.05.t End membership dialog · your own · Community Manager
8.05.u End membership dialog · Jordan Blake · refused · lost a race · Community Manager
8.05.v Replace manager dialog · Jordan Blake with Lee Morales · Company Administrator
8.05.w Managers and memberships · Jordan Blake replaced · Company Administrator
8.05.x Managers and memberships · Old Mill Commons · archived · Company Administrator
8.05.y End membership dialog · refused · no longer a manager · Board Member
8.05.z Another community's settings · refused · Community Manager

Reuse BoardVendors (table card with a header bar), wf-card, wf-table, wf-btn, wf-pill, wf-field, wf-field-label, wf-radio, wf-sub, wf-ph. Sections 1–7 must render as before.

Put a DCPostIt by 8.05.a with the design choices and open questions in brief §4.5.

Fidelity: lofi, the same visual language as section 7. Layout, columns, copy, states and who sees what are binding. Colour and spacing are placeholders. "Open-ended", invitation states and refusals are words, never colour or an icon alone.

Do not draw: a name for a pending invitee; any lookup, suggestion or account hint on an email field; Replace manager or status actions for a Community Manager; Scheduled or Accepted role invitations; a Residents table; a "Since" column; Notifications; a "type the name to confirm" step; error codes; a phone layout.

When you finish, open each artboard and check that it renders with no console errors, that its rows match brief §4 and that its copy matches brief §5 word for word. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

Managers and memberships says who runs a community and who holds a role in it. A community must never silently lose its last manager (US2). The section replaces today's "Memberships" page, which takes a user ID and so only works for people who already have an account (spec Grounding).

- **Company Administrators** (Robin Hale). They reach it from the portfolio, through the community's Community settings page (FR-029). They appoint a community's first Community Manager, appoint, end and downgrade any manager, manage Board Member and Accountant memberships, and resend or revoke any role invitation (FR-018, FR-019, FR-021, FR-022). Only they can **replace the last manager in one action** (FR-020). In an Archived community they can only **end memberships** (FR-012).
- **Community Managers** (Jordan Blake; Taylor Grant; Lee Morales once appointed). They reach it from board mode, through the "Community settings" entry (brief 01). In a community they manage, they appoint and remove co-managers, end or downgrade their own manager membership, manage Board Member and Accountant memberships, and resend or revoke role invitations (US5 #2, FR-019, FR-021). Never the last manager (FR-020). They never see Replace manager.
- **Nobody grants a role to themselves** (FR-003a). A person leaves themselves out of every person list and gets `SELF_GRANT_NOT_ALLOWED` if they type their own email.
- **Board Members, Accountants and Residents** can't open the page. A request is refused with 403 `FORBIDDEN` (US2 #18, US5 #3).

Two ways to appoint (FR-018):

- **Choose someone already in the community** from its people list: anyone with an effective membership or resident link there. It takes effect at once. No invitation.
- **Invite by email**, for anyone else. This always sends an invitation, whether or not the email has an account. The sender sees the same thing either way: the email, "Pending", and no name, until the person accepts.

The rule the design must make obvious (FR-020): a community **has a manager** only while at least one Community Manager membership is **open-ended** (no end date). Managers with an end date don't count. A pending invitation doesn't count.

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| US2 #1 | Keystone Park has no manager. Robin appoints pat@example.com by email. The dialog offers only the email path, because no one is in Keystone Park yet. After sending: the toast "Invitation sent to pat@example.com.", the Managers area still says "Keystone Park has no Community Manager yet." with "Invitation pending: pat@example.com. It doesn't count until it's accepted.", and the Invitations area lists pat@example.com as Pending, with no name. After Pat accepts (brief 10), Managers lists Pat Rivera as Open-ended and the invitation leaves the list (§4.6). | 8.05.i, j |
| US2 #3 | Jordan chooses Lee Morales from Sakura Heights' people list. The summary says it takes effect now and no invitation is sent. After: Lee is in Managers as Open-ended at once, with no invitation row. Sakura Heights now has two open-ended managers. Lee keeps the Board Member membership. | 8.05.c, d |
| US2 #4 | Taylor invites has-account@example.com (an account exists) and no-account@example.com (none) as Community Managers. The two toasts read the same apart from the email. The two Invitations rows look the same: email, "Community Manager", "Pending", no name. Nothing on screen tells them apart. | 8.05.e |
| US2 #6 | Jordan resends alex@example.com's expired Community Manager invitation. It becomes Pending with a new 72 hours, "Sent by" becomes Jordan Blake, and the toast says the earlier link no longer works. Resend works the same on a Pending invitation (§4.6). The old link's refusal is brief 10. | 8.05.l |
| US2 #9 | Robin ends Jordan Blake, Sakura Heights' only open-ended manager. Refused in the dialog: "Jordan Blake is the only Community Manager of Sakura Heights with no end date. A community must always have one. Appoint another manager first." plus, for a Company Administrator, "Or use Replace manager." Jordan's row still reads "Open-ended". A Community Manager gets the same text without the last line (§5). | 8.05.n |
| US2 #10 | Robin sets Jordan's end date to 12/31/2026. Refused in the dialog: "Jordan Blake can't be given an end date while they're the only Community Manager of Sakura Heights with no end date." Jordan's row still reads "Open-ended". | 8.05.o |
| US2 #11 | Robin replaces Jordan Blake with Lee Morales in one action. The dialog's picker offers only Sakura Heights' people. It says both changes happen together. After: Lee is Open-ended; Jordan's manager membership is under "Ended" with today's date. A replacement who is not in Sakura Heights can't be chosen; if the server refuses one anyway, Jordan stays active (§5, design choice). | 8.05.b, v, w |
| US2 #12 | Jordan ends his own manager membership while Lee is also open-ended. The dialog says it ends today, Oct 12, 2026, and that Jordan loses access to Sakura Heights' Community settings at once. After confirming, the app takes Jordan out of the page (§6). | 8.05.t |
| US2 #13 | Jordan downgrades co-manager Lee, who is also a Board Member. The dialog says the manager membership ends today and Lee keeps the Board Member membership (ends Dec 31, 2027). Lee's next manager action, from an old tab, is refused. | 8.05.r, y |
| US2 #14 | Jordan and Lee, the two open-ended managers, end each other at the same moment. Jordan's wins. Lee's dialog shows `LAST_MANAGER`: "Jordan Blake is the only Community Manager of Sakura Heights with no end date. …". | 8.05.u |
| US2 #15 | Taylor sent two Community Manager invitations (8.05.e). Jordan ends Taylor's membership. The dialog lists each pending invitation Taylor sent as revoked "because Taylor Grant can no longer send them". An invitation revoked this way reads "Revoked" with "Sender's role ended, Oct 12, 2026" (design choice). The link's refusal is brief 10. | 8.05.q, w |
| US2 #16 | Jordan invites new@example.com as a Board Member with term end date 12/31/2027. The summary says the membership starts when they accept and ends Dec 31, 2027. The Invitations row reads "Board Member" / "Term ends Dec 31, 2027". | 8.05.f, a |
| US2 #17 | Jordan types jordan@example.com in the Board Member invitation. Refused under the email field: "You can't give yourself a role or a home. Ask another Company Administrator or this community's Community Manager." Jordan is never in a person picker. | 8.05.g; c (note) |
| US2 #18 | Lee, now a Board Member only, tries to end Taylor's manager membership from an old tab. Refused: "You don't have permission to do that." Then the app takes Lee to the landing page with "You don't have access to that page." | 8.05.y |
| US5 #2 | Jordan's view: Appoint a manager, Appoint a board member or accountant, Change end date, Downgrade and End on co-managers, Resend and Revoke on invitations, and the Residents area below. No Replace manager. No status actions or Change parent in the header. | 8.05.a |
| US5 #7 | Old Mill Commons, archived: the read-only banner; every area shown, values as plain text, including the Residents placeholder; drew@example.com labelled "Revoked — community archived"; the only action in this section is "End membership" on each active membership (Restore sits in the header). | 8.05.x |
| US5 #8 | Sakura Heights has a pending Board Member invitation (new@example.com) and an expired Community Manager invitation (alex@example.com). Both are listed with email, role, sent by, sent at and expires at, each with "Resend" and "Revoke". | 8.05.a, b |
| FR-018 | Two paths in one dialog. Direct: a person picker over the community's people list, takes effect at once, no invitation. By email: an email field only, no lookup, always an invitation, identical response. | 8.05.c, e, f, i |
| FR-019 | End and downgrade for any manager (Company Administrator) or for oneself and co-managers (Community Manager). Downgrade keeps an existing Board Member membership (Lee) or gives one (Taylor). Re-appointing a former member gives an active membership again (§6). | 8.05.q, r, s, t |
| FR-020 | The rule, as helper text always shown in Managers. Open-ended managers marked "Open-ended"; end-dated ones marked "Doesn't count toward the rule above". `LAST_MANAGER` on end, downgrade and end-dating, and on a race. The first manager can't have an end date; a later one may only while another open-ended manager remains. Replace for the last manager, Company Administrator only. The rule doesn't apply while archived. | 8.05.a, c, i, n, o, p, u, v, w, x |
| FR-021 | Board Member and Accountant memberships are managed by both viewpoints, directly or by invitation, with an optional term end date. | 8.05.a, b, f, h |
| FR-022, states | Pending, Expired, Revoked and "Revoked — community archived" rows, as text. Role invitations are emailed at once in any status, so a role invitation is never Scheduled; Accepted ones are not listed (§4.5 question 5). | 8.05.a, j, w, x |
| FR-022, resend | Resend on Pending and Expired rows, with a new 72 hours and the resender as sender. | 8.05.a, l, k |
| FR-022, revoke | Revoke on Pending and Expired rows, with a confirmation. Revoked rows have no actions. Invitations die with their sender's authority: listed in every End, Downgrade and Replace dialog that causes it. | 8.05.m, q, s, t, v, w |
| FR-022, duplicates | `INVITATION_PENDING` with a "Resend invitation" button; `ALREADY_MEMBER`. | 8.05.k, h |
| FR-022, terms | "Term end date (optional)" on Board Member and Accountant invitations; the membership starts when they accept. | 8.05.f |
| FR-022b | One Invitations area for the three roles, next to active and ended memberships. Columns: Email, Role, Sent by, Sent, Expires, Status, Actions. Times in the community's time zone. | 8.05.a, b, j, x |
| FR-003a | Nobody is offered themselves in a person picker; typing one's own email is refused with `SELF_GRANT_NOT_ALLOWED`. | 8.05.c, g |
| FR-012 (this section) | Archived: read-only; End membership is the only action here, allowed even for the last manager. | 8.05.x |
| FR-042 | Every refusal says why in plain words. The exceptions are the two `FORBIDDEN` messages, which must not reveal whether a community exists. | 8.05.g, h, k, n, o, p, u, y, z |
| US2 #8 (out-of-scope `FORBIDDEN`) | Jordan opens a link to Briar Hollow's Managers and memberships. He lands in board mode with "You don't have access to that page." Briar Hollow's name appears nowhere. The same happens for a community that doesn't exist. | 8.05.z |
| Edge case: a manager's term would end with no open-ended manager left | The end-date helper text and 8.05.o. | 8.05.c, o |
| Edge case: the sender lost their authority | Every End, Downgrade and Replace dialog lists the invitations it revokes. | 8.05.q, s, t, v |
| FR-023, FR-041 | Logging is not visible. The rate-limit refusal is copy only (§5). | — |
| Constitution, Accessibility | The section and its invitation list are keyboard operable, with labels, visible focus and messages tied to fields. "Open-ended" and invitation states are text. | All, see §7 |

## 4. Artboards

### 4.1 The section (every page artboard)

**Page.** Community settings for one community (conventions §7.2). If section 8 has brief 04's header and section nav, use them. Otherwise:

- **Header:** the community name as the page title; its status chip; the legal name on the line below. A Company Administrator also sees the status actions for that status: Active shows "Archive"; Onboarding shows "Mark Active" and "Archive"; Archived shows "Restore". A Community Manager sees no status actions and no Change parent.
- **Section nav:** brief 04's searchable side list on the left (owner decision 2026-10-14): search box, then Profile · Managers and memberships (current) · Architectural review. Add a `.wf-note`: "section nav set by brief 04". No Notifications.

**Section body, top to bottom.**

1. Section heading "Managers and memberships" and one intro line (`wf-sub`, §5).
2. **Jump links**, one row: "Managers · Board members and accountants · Invitations · Residents". Each moves to its area.
3. **Managers** card (`wf-card`, header bar as in `BoardVendors`): the title "Managers" left, the button "Appoint a manager" right. Under the bar, the rule as helper text (§5), always shown. Then the table.
4. **Board members and accountants** card: the title left, the button "Appoint a board member or accountant" right. Then the table.
5. **Invitations** card: the title "Invitations" and one helper line. No button: invitations start from the two Appoint buttons. Then the table.
6. **Residents** card: the title "Residents" and a `wf-ph` block reading "Residents — brief 06". Do not draw the table.

**Tables.**

| Area | Columns | Order (design choice) |
| --- | --- | --- |
| Managers | Name · Term · Actions | Open-ended first, then by end date; A–Z by name within each. Ended rows below the sub-heading "Ended". |
| Board members and accountants | Name · Role · Term · Actions | Board Members, then Accountants; A–Z by name within each. Ended rows below "Ended". |
| Invitations | Email · Role · Sent by · Sent · Expires · Status · Actions | Pending and Expired first, newest sent first; then Revoked, newest first. |

- **Name** shows the person's name. The signed-in person's row adds "(you)". Members are shown by name only (conventions §7.9).
- **Term, Managers:** an open-ended manager shows the chip "Open-ended" and, under it, "No end date". An end-dated manager shows "Ends Dec 31, 2026" and, under it, "Doesn't count toward the rule above". Ended: "Ended Oct 12, 2026".
- **Term, Board members and accountants:** "Ends Dec 31, 2027", "No end date" or "Ended Jun 30, 2026". No chip: the open-ended rule is only about managers.
- **Actions on memberships:** a labelled "Actions" button per active row opens a menu (design choice). Ended rows show "—".
- **Invitation Role** shows the offer: "Community Manager", "Board Member" or "Accountant". A term adds a second line: "Term ends Dec 31, 2027".
- **Invitation Expires** always shows the 72-hour mark, date and time with "EDT". **Status** is a text chip: "Pending", "Expired", "Revoked" or "Revoked — community archived". A revoked row adds a second line in Status (conventions §5.2): "Revoked by Jordan Blake, Oct 7, 2026", "Sep 30, 2026" or "Sender's role ended, Oct 12, 2026".
- **Actions on invitations:** visible buttons "Resend" and "Revoke" on Pending and Expired rows. Revoked rows show "—".

**Menu items on a membership row** (only the items the viewer may use are shown; the server decides in every case):

| Row | Company Administrator | Community Manager of that community |
| --- | --- | --- |
| A manager who is the only open-ended one | Change end date · Downgrade to Board Member · End membership · Replace manager | Change end date · Downgrade to Board Member · End membership |
| Any other manager | Change end date · Downgrade to Board Member · End membership | Same |
| Your own manager row | — (a Company Administrator is never offered themselves) | Change end date · End membership. "Downgrade to Board Member" only if you already hold a Board Member membership there (§4.5 question 7). |
| Board Member or Accountant | Change end date · End membership | Same |
| Ended | — | — |

Last-manager actions are offered and refused after confirming (§4.5 question 8). Replace manager appears only on the row of the only open-ended manager.

### 4.2 Frames

| Person | Frame | Details |
| --- | --- | --- |
| Robin Hale | Company area (conventions §7.1) | Avatar "RH". No board-mode control. Strip "Juniper Row Community Management · Company area". Left nav: Portfolio (current, brief 01 D6), Add community, Company defaults, Company administrators. Breadcrumb "Portfolio › Sakura Heights" (or the community shown). No violet banner. |
| Jordan Blake | `WFShellBoard`, board mode | Avatar "JB". Mode control "Resident · Board", Board on. Strip "Sakura Heights · Community Manager". Role manager, 5 communities, so "My Communities" shows. "Add community" in the top block; "Community settings" last in "Community management", current (brief 01). |
| Taylor Grant | `WFShellBoard`, board mode | Avatar "TG". Strip "Sakura Heights · Community Manager". 1 community, so no "My Communities". "Community settings" current. |
| Lee Morales, after 8.05.d | `WFShellBoard`, board mode | Avatar "LM". Strip "Sakura Heights · Board Member · Community Manager". 1 community. "Community settings" current. |

- If section 8 has brief 01's nav, use it. If not, add "Community settings" as the last item of "Community management" and add a `.wf-note`: "position set by brief 01".
- Keep the violet banner unchanged on every board-mode artboard: dark ink on violet (spec 025 FR-021, FR-038).

### 4.3 Data

"Now" is Monday, Oct 12, 2026, 10:00 AM EDT. All times are EDT, Sakura Heights' time zone (conventions §4). Each artboard starts from the **baseline** below unless its entry says otherwise.

**Sakura Heights, baseline.** Header: "Sakura Heights", chip "Active", "Sakura Heights Homeowners Association, Inc.", no parent line.

Managers:

| Name | Term |
| --- | --- |
| Jordan Blake | Open-ended · No end date |
| Taylor Grant | Ends Dec 31, 2026 · Doesn't count toward the rule above |

Board members and accountants:

| Name | Role | Term |
| --- | --- | --- |
| Chris Ward | Board Member | Ends Dec 31, 2027 |
| Jane Alvarez | Board Member | Ends Dec 31, 2026 |
| Lee Morales | Board Member | Ends Dec 31, 2027 |
| Avery Quinn | Accountant | No end date |
| *Ended:* Dana Fox | Board Member | Ended Jun 30, 2026 |

Invitations (conventions §4.5):

| Email | Role | Sent by | Sent | Expires | Status |
| --- | --- | --- | --- | --- | --- |
| new@example.com | Board Member / Term ends Dec 31, 2027 | Jordan Blake | Oct 11, 2026, 2:05 PM EDT | Oct 14, 2026, 2:05 PM EDT | Pending |
| alex@example.com | Community Manager | Taylor Grant | Oct 5, 2026, 11:00 AM EDT | Oct 8, 2026, 11:00 AM EDT | Expired |
| books@example.com | Accountant | Jordan Blake | Oct 6, 2026, 9:40 AM EDT | Oct 9, 2026, 9:40 AM EDT | Revoked / "Revoked by Jordan Blake, Oct 7, 2026" |

**Sakura Heights' people list** (who a person picker can offer, FR-018): everyone with an effective membership or resident link there. Jordan Blake (Community Manager), Taylor Grant (Community Manager), Chris Ward (Board Member), Jane Alvarez (Board Member · Resident), Lee Morales (Board Member), Avery Quinn (Accountant), Sam Ortiz (Resident), and the community's other residents (filler names). Not in it: Dana Fox (her membership ended and she has no home there), and every pending invitee.

**Keystone Park** (Robin added it on Oct 11, 2026): "Keystone Park", chip "Onboarding", "Keystone Park HOA, Inc.". No memberships. No people, so the person picker has nothing to offer. 0 properties.

**Old Mill Commons** (archived Sep 30, 2026): "Old Mill Commons", chip "Archived", "Old Mill Commons Association, Inc.".

| Area | Rows |
| --- | --- |
| Managers | Morgan Ellis · Open-ended · No end date |
| Board members and accountants | Jesse Lin, Rowan Hayes, Skyler Diaz · Board Member · No end date (the conventions give no terms); Avery Quinn · Accountant · No end date |
| Invitations | drew@example.com · Community Manager · Morgan Ellis · Sep 28, 2026, 2:00 PM EDT · Oct 1, 2026, 2:00 PM EDT · "Revoked — community archived" / "Sep 30, 2026" |

rae@example.com's resident invitation belongs to Residents (brief 06).

**US2 #4 addresses.** has-account@example.com and no-account@example.com are the spec's own example addresses (US2 #4; conventions §4: "the spec's example people are used where the spec names them"). The first belongs to an account; the second doesn't. They appear only on 8.05.e and 8.05.q.

**Branches.** Some artboards follow an earlier change. Each branch starts from the baseline.

| Branch | Change | Artboards |
| --- | --- | --- |
| Oct 11, before new@example.com was invited | The baseline without the new@example.com row. Now is Oct 11, 2026, 2:05 PM EDT. | 8.05.f |
| Keystone Park, Oct 11 | Now is Oct 11, 2026, 9:15 AM EDT. | 8.05.i, j |
| Lee appointed | Oct 12, 10:00 AM: Jordan appoints Lee Morales directly (8.05.c). Managers: Jordan Blake and Lee Morales, both Open-ended; Taylor Grant, Ends Dec 31, 2026. Lee is still a Board Member too. | 8.05.d, r, t, u; 8.05.y also follows 8.05.r |
| Taylor's invitations | Oct 12: Taylor invites has-account@example.com at 10:00 AM and no-account@example.com at 10:01 AM, both as Community Managers. | 8.05.e, q |
| Replaced | Oct 12, 10:00 AM: Robin replaces Jordan Blake with Lee Morales (8.05.v). | 8.05.w |

### 4.4 Artboard list

**Sakura Heights: the section in both viewpoints**

**8.05.a Managers and memberships · Sakura Heights · Community Manager**

- Jordan Blake. Baseline.
- Managers: "Jordan Blake (you)" Open-ended; Taylor Grant, Ends Dec 31, 2026. Rule helper text under the header.
- Board members and accountants and Invitations as in §4.3. new@example.com and alex@example.com each have "Resend" and "Revoke" (US5 #8). books@example.com has "—".
- Header: no status actions, no Change parent (US5 #2).
- No menu open. Jordan's own row's menu holds "Change end date" and "End membership" only; Taylor's holds all three manager items (§4.1).
- Residents placeholder at the bottom.
- State: default.

**8.05.b Managers and memberships · Sakura Heights · actions menu open · Company Administrator**

- Robin Hale. Baseline. Breadcrumb "Portfolio › Sakura Heights". Header action "Archive".
- Same areas and rows as 8.05.a, with no "(you)" marker.
- Jordan Blake's "Actions" menu is open, showing: "Change end date", "Downgrade to Board Member", "End membership", "Replace manager". Keyboard focus is on "Change end date".
- `.wf-note` by "Replace manager": "Company Administrators only. Shown only on the row of the only open-ended manager (FR-020)."
- State: default.

**Appointing directly and by email**

**8.05.c Appoint a manager · choose Lee Morales · Community Manager**

- Jordan Blake, Oct 12, 10:00 AM. The dialog over 8.05.a.
- Title "Appoint a Community Manager", with "Sakura Heights" under it.
- The path choice (radio group) "How do you want to appoint them?": "Choose someone already in Sakura Heights" is selected; "Invite someone by email" is not. Each option has its helper line.
- Person picker, open. Search holds "Lee". One result: "Lee Morales", second line "Board Member", marked "✓ Selected". The note under the picker: "You, and people who are already Community Managers, aren't listed."
- End date (optional): empty, with the manager end-date helper (§5).
- Summary line: "Lee Morales becomes a Community Manager of Sakura Heights now, with no end date. No invitation is sent."
- Buttons: "Appoint Lee Morales" (primary), "Cancel".
- `.wf-note`: "Jordan and Taylor aren't offered: Jordan is the signed-in person (FR-003a), Taylor is already a manager".
- State: default (filled).

**8.05.d Managers and memberships · Lee Morales appointed · Community Manager**

- Jordan Blake. Branch "Lee appointed", right after 8.05.c.
- Toast: "Lee Morales is now a Community Manager of Sakura Heights."
- Managers: "Jordan Blake (you)" Open-ended; "Lee Morales" Open-ended; "Taylor Grant" Ends Dec 31, 2026.
- Board members and accountants: unchanged. Lee Morales is still listed as Board Member, Ends Dec 31, 2027.
- Invitations: unchanged. No row for Lee.
- `.wf-note`: "At once, no invitation (US2 #3). Two open-ended managers now."
- State: success.

**8.05.e Managers and memberships · two manager invitations sent · Community Manager**

- Taylor Grant. Branch "Taylor's invitations", right after the second send at 10:01 AM.
- Toast: "Invitation sent to no-account@example.com."
- Managers: Jordan Blake, Open-ended; "Taylor Grant (you)", Ends Dec 31, 2026. Unchanged: neither invitation counts as a manager.
- Invitations, top two rows:
  - no-account@example.com · Community Manager · Taylor Grant · Oct 12, 2026, 10:01 AM EDT · Oct 15, 2026, 10:01 AM EDT · Pending · Resend, Revoke
  - has-account@example.com · Community Manager · Taylor Grant · Oct 12, 2026, 10:00 AM EDT · Oct 15, 2026, 10:00 AM EDT · Pending · Resend, Revoke
  - then the baseline rows.
- `.wf-note` by the two rows: "has-account@ has an account; no-account@ doesn't. Same toast, same row, no name (FR-018, US2 #4)."
- State: success.

**8.05.f Appoint a board member · invite with a term end date · Community Manager**

- Jordan Blake. Oct 11, 2026, 2:05 PM EDT. The page behind is the baseline without the new@example.com row.
- Title "Appoint a board member or accountant", with "Sakura Heights" under it.
- Role: "Board Member" selected, "Accountant" not.
- Path: "Invite someone by email" selected.
- Email address: "new@example.com". Helper: "Until they accept, the invitation shows only this email address."
- Term end date (optional): "12/31/2027". Helper for the email path (§5).
- Summary: "We'll email new@example.com an invitation to be a Board Member of Sakura Heights, with a term ending Dec 31, 2027. It's valid for 72 hours."
- Buttons: "Send invitation" (primary), "Cancel".
- After sending, the row is 8.05.a's new@example.com row. The toast is "Invitation sent to new@example.com." (not drawn).
- State: default (filled).

**8.05.g Appoint a board member · refused · own email · Community Manager**

- Jordan Blake. As 8.05.f, but on Oct 12, from the baseline, with email "jordan@example.com" and no term.
- Under the email field: "You can't give yourself a role or a home. Ask another Company Administrator or this community's Community Manager." The field is marked invalid.
- The dialog stays open. Input kept.
- State: refusal (`SELF_GRANT_NOT_ALLOWED`, US2 #17).

**8.05.h Appoint a board member · refused · already a Board Member · Community Manager**

- Jordan Blake. As 8.05.g, with email "lee@example.com".
- Under the email field: "lee@example.com is already a Board Member of Sakura Heights."
- State: refusal (`ALREADY_MEMBER`).

**Keystone Park: the first manager**

**8.05.i Appoint a manager · Keystone Park · invite by email · Company Administrator**

- Robin Hale. Oct 11, 2026, 9:15 AM EDT. Breadcrumb "Portfolio › Keystone Park". Header actions "Mark Active", "Archive".
- The page behind, dimmed: Managers reads "Keystone Park has no Community Manager yet." and "Keystone Park can't be marked Active until it has a Community Manager with no end date." with "Appoint a manager"; Board members and accountants reads "No board members or accountants yet."; Invitations reads "No invitations."; Residents placeholder.
- Dialog title "Appoint a Community Manager", with "Keystone Park" under it.
- Path: "Choose someone already in Keystone Park" is disabled, with its reason under it: "No one is in Keystone Park yet, so you can only invite by email." "Invite someone by email" is selected.
- Email address: "pat@example.com".
- In place of the end date field, one line: "No end date. Keystone Park has no other Community Manager without an end date, so this one can't have one."
- Onboarding helper: "Keystone Park is Onboarding. Role invitations are still emailed now. Only resident invitations wait until it's marked Active."
- Summary: "We'll email pat@example.com an invitation to be a Community Manager of Keystone Park. It's valid for 72 hours."
- Buttons: "Send invitation", "Cancel".
- State: default (filled).

**8.05.j Managers and memberships · Keystone Park · invitation pending · Company Administrator**

- Robin Hale, right after 8.05.i.
- Toast: "Invitation sent to pat@example.com."
- Managers: still "Keystone Park has no Community Manager yet." and "Keystone Park can't be marked Active until it has a Community Manager with no end date.", then "Invitation pending: pat@example.com. It doesn't count until it's accepted." The rule helper text and "Appoint a manager" stay.
- Board members and accountants: "No board members or accountants yet."
- Invitations: pat@example.com · Community Manager · Robin Hale · Oct 11, 2026, 9:15 AM EDT · Oct 14, 2026, 9:15 AM EDT · Pending · Resend, Revoke.
- Nowhere does "Pat Rivera" appear.
- State: success (US2 #1).

**8.05.k Appoint a manager · refused · invitation already pending · Company Administrator**

- Robin Hale. Oct 12, 2026, 10:00 AM. Keystone Park as in 8.05.j. Robin opens "Appoint a manager" again and types "pat@example.com".
- Under the email field: "pat@example.com already has a pending Community Manager invitation for Keystone Park. Resend it instead?" with the button "Resend invitation" under the message.
- "Send invitation" stays, so Robin can change the email. Input kept.
- State: refusal (`INVITATION_PENDING`).

**Resend and revoke**

**8.05.l Invitations · expired invitation resent · Community Manager**

- Jordan Blake. Baseline, right after Jordan pressed "Resend" on alex@example.com.
- Toast: "Invitation sent again to alex@example.com. The earlier link no longer works."
- alex@example.com's row moves to the top: Community Manager · **Jordan Blake** · Oct 12, 2026, 10:00 AM EDT · Oct 15, 2026, 10:00 AM EDT · Pending · Resend, Revoke.
- Focus stays on that row's "Resend" button.
- `.wf-note`: "Sent by changed from Taylor Grant to Jordan Blake. A resend makes the resender its sender (FR-022)."
- Only the Invitations card needs full detail; the other areas are as in 8.05.a.
- State: success (US2 #6).

**8.05.m Revoke invitation dialog · new@example.com · Community Manager**

- Jordan Blake. Baseline. Dialog over 8.05.a, opened from new@example.com's "Revoke".
- Title, consequences and buttons as in §5.
- After confirming (not drawn): the toast "Invitation to new@example.com revoked."; the row's Status reads "Revoked" / "Revoked by Jordan Blake, Oct 12, 2026"; its actions read "—".
- State: confirmation.

**Last-manager refusals**

**8.05.n End membership dialog · Jordan Blake · refused · last manager · Company Administrator**

- Robin Hale. Baseline. Dialog over 8.05.b, opened from Jordan's "End membership".
- Robin pressed "End membership". The dialog stays open. At the top:
  - "Jordan Blake is the only Community Manager of Sakura Heights with no end date. A community must always have one. Appoint another manager first."
  - "Or use Replace manager."
- Below, the dialog's consequences as they were when it opened (§5).
- Buttons: "Close" (primary) and "Replace manager" (design choice: a shortcut to 8.05.v).
- Behind: Jordan's row still reads "Open-ended".
- A Community Manager (for example Taylor ending Jordan) gets the same dialog without "Or use Replace manager." and without the "Replace manager" button. Not drawn.
- State: refusal (US2 #9).

**8.05.o Change end date dialog · Jordan Blake · refused · last manager · Company Administrator**

- Robin Hale. Baseline. Dialog over 8.05.b, opened from Jordan's "Change end date".
- Title "Change end date", with "Jordan Blake · Community Manager · Sakura Heights" under it.
- "Now: no end date". End date (optional): "12/31/2026". The manager end-date helper.
- Robin pressed "Save end date". At the top: "Jordan Blake can't be given an end date while they're the only Community Manager of Sakura Heights with no end date."
- Buttons: "Close" (primary). The typed date stays in the field. Behind: Jordan's row still reads "Open-ended".
- State: refusal (US2 #10).

**8.05.p Downgrade dialog · Jordan Blake · refused · last manager · Community Manager**

- Taylor Grant. Baseline. Dialog over Taylor's view of the section, opened from Jordan's "Downgrade to Board Member".
- Taylor pressed "Downgrade to Board Member". At the top: "Jordan Blake can't be downgraded. They're the only Community Manager of Sakura Heights with no end date. Appoint another manager first."
- Below, the consequences as they were when it opened (§5).
- Buttons: "Close" (primary).
- State: refusal.

**Ending and downgrading**

**8.05.q End membership dialog · Taylor Grant · invitations revoked · Community Manager**

- Jordan Blake. Branch "Taylor's invitations", after 8.05.e. Dialog over Jordan's view, opened from Taylor's "End membership".
- What happens: Taylor's manager membership ends today; Taylor can't act as a Community Manager of Sakura Heights from now on; 3 pending invitations Taylor sent are revoked, each on its own line: has-account@example.com (Community Manager), no-account@example.com (Community Manager), noor@example.com (resident of 719 Keystone Park Dr #35).
- What doesn't change: Sakura Heights still has an open-ended manager, Jordan Blake; nothing is deleted.
- Buttons: "End membership" (primary), "Cancel".
- After confirming (not drawn): toast "Taylor Grant's Community Manager membership ended."; Managers shows "Jordan Blake (you)" Open-ended and, under "Ended", "Taylor Grant · Ended Oct 12, 2026"; the two invitations read "Revoked" / "Sender's role ended, Oct 12, 2026". alex@example.com stays Expired: only scheduled and pending invitations are revoked (FR-022).
- State: confirmation (US2 #15).

**8.05.r Downgrade dialog · Lee Morales · keeps Board Member · Community Manager**

- Jordan Blake. Branch "Lee appointed". Dialog over 8.05.d, opened from Lee's "Downgrade to Board Member".
- What happens: Lee's manager membership ends today; Lee can't act as a Community Manager of Sakura Heights from now on.
- What doesn't change: Lee keeps the Board Member membership, which ends Dec 31, 2027, as before; Sakura Heights still has an open-ended manager, Jordan Blake.
- No invitation line: Lee sent none.
- Buttons: "Downgrade to Board Member" (primary), "Cancel".
- After confirming (not drawn): toast "Lee Morales is now a Board Member only."; Managers back to the baseline plus "Lee Morales · Ended Oct 12, 2026" under "Ended"; Board members unchanged.
- State: confirmation (US2 #13).

**8.05.s Downgrade dialog · Taylor Grant · becomes Board Member · Company Administrator**

- Robin Hale. Baseline. Dialog over 8.05.b, opened from Taylor's "Downgrade to Board Member".
- What happens: Taylor's manager membership ends today; Taylor becomes a Board Member of Sakura Heights today, with no end date (design choice, §4.5 question 6); Taylor can't act as a Community Manager from now on; 1 pending invitation Taylor sent is revoked, because a Board Member can't send it: noor@example.com (resident of 719 Keystone Park Dr #35).
- What doesn't change: Sakura Heights still has an open-ended manager, Jordan Blake.
- Buttons: "Downgrade to Board Member" (primary), "Cancel".
- State: confirmation (FR-019, a downgrade that gives a Board Member membership).

**8.05.t End membership dialog · your own · Community Manager**

- Jordan Blake. Branch "Lee appointed". Dialog over 8.05.d, opened from Jordan's own "End membership".
- Title "End your Community Manager membership in Sakura Heights?"
- What happens: it ends today, Oct 12, 2026; Jordan loses access to Sakura Heights' Community settings at once; Sakura Heights leaves Jordan's community switcher and "My Communities", because Jordan has no other role there; 2 pending invitations Jordan sent are revoked: new@example.com (Board Member), kim@example.com (resident of 711 Keystone Park Dr #29).
- What doesn't change: Sakura Heights still has an open-ended manager, Lee Morales; Jordan's roles in other communities.
- Buttons: "End my membership" (primary), "Cancel".
- After confirming: §6.
- State: confirmation (US2 #12).

**8.05.u End membership dialog · Jordan Blake · refused · lost a race · Community Manager**

- Lee Morales (frame in §4.2). Branch "Lee appointed". Dialog opened from Jordan's "End membership".
- At the same moment, Jordan confirmed ending Lee's manager membership. Jordan's request won.
- Lee pressed "End membership". At the top: "Jordan Blake is the only Community Manager of Sakura Heights with no end date. A community must always have one. Appoint another manager first."
- Below, the consequences as they were when the dialog opened, including "Sakura Heights still has a Community Manager with no end date: Lee Morales (you)." The refusal explains why that no longer holds.
- Buttons: "Close" (primary).
- `.wf-note`: "Both pressed at once. Exactly one wins (FR-020). On Close, Lee is no longer a manager here and goes to the landing page (§6)."
- State: refusal (US2 #14).

**Replace (Company Administrator only)**

**8.05.v Replace manager dialog · Jordan Blake with Lee Morales · Company Administrator**

- Robin Hale. Baseline. Dialog over 8.05.b, opened from "Replace manager" in Jordan's menu.
- Title "Replace Jordan Blake as Community Manager of Sakura Heights?"
- "Replacement (required)": a person picker. Search holds "Lee". Result "Lee Morales" / "Board Member", "✓ Selected". Helper under it (§5).
- What happens: Lee becomes a Community Manager now, with no end date; Jordan's manager membership ends today, Oct 12, 2026; both happen together, or neither does; 2 pending invitations Jordan sent are revoked: new@example.com (Board Member), kim@example.com (resident of 711 Keystone Park Dr #29).
- What doesn't change: Lee keeps the Board Member membership; Taylor Grant's membership (ends Dec 31, 2026); Jordan's roles in other communities.
- Buttons: "Replace manager" (primary), "Cancel".
- State: confirmation (US2 #11).

**8.05.w Managers and memberships · Jordan Blake replaced · Company Administrator**

- Robin Hale. Branch "Replaced", right after 8.05.v.
- Toast: "Lee Morales replaced Jordan Blake as Community Manager of Sakura Heights."
- Managers: "Lee Morales" Open-ended; "Taylor Grant" Ends Dec 31, 2026; under "Ended", "Jordan Blake · Ended Oct 12, 2026".
- Board members and accountants: unchanged. Lee is still a Board Member.
- Invitations: new@example.com now reads "Revoked" / "Sender's role ended, Oct 12, 2026", with "—" for actions. alex@example.com (Expired) and books@example.com (Revoked) unchanged.
- `.wf-note`: "kim@example.com's resident invitation was revoked too. It shows in Residents (brief 06)."
- "Replace manager" is now in Lee's menu, because Lee is the only open-ended manager.
- State: success (US2 #11, US2 #15 label).

**Archived, refusals for people who may not act, and other communities**

**8.05.x Managers and memberships · Old Mill Commons · archived · Company Administrator**

- Robin Hale. Oct 12. Breadcrumb "Portfolio › Old Mill Commons". Header: "Old Mill Commons", chip "Archived", "Old Mill Commons Association, Inc.", and the one action "Restore".
- The read-only banner at the top of the section (conventions §7.3).
- Managers: in place of the rule helper, the archived helper (§5). Row "Morgan Ellis" Open-ended, with a visible "End membership" button (no menu: it is the only action).
- Board members and accountants: Jesse Lin, Rowan Hayes, Skyler Diaz (Board Member, No end date) and Avery Quinn (Accountant, No end date), each with "End membership".
- Invitations: drew@example.com, "Revoked — community archived" / "Sep 30, 2026", actions "—".
- Residents: the placeholder "Residents — read-only, brief 06".
- No Appoint buttons, no Change end date, no Downgrade, no Replace, no Resend, no Revoke. Values are plain text. These are removed, not disabled.
- State: read-only (US5 #7).

**8.05.y End membership dialog · refused · no longer a manager · Board Member**

- Lee Morales, after 8.05.r. Lee still has the page open in an old tab, so the strip still reads "Sakura Heights · Board Member · Community Manager".
- Lee chose "End membership" on Taylor Grant's row. The dialog opens with the title "End Taylor Grant's Community Manager membership?" and, at the top, "You don't have permission to do that." No consequences list. Button: "Close".
- State: refusal (`FORBIDDEN`, US2 #18, US2 #13 "on their next request").

**8.05.z Another community's settings · refused · Community Manager**

- Jordan Blake. Oct 12. Jordan opened a link to Briar Hollow's Managers and memberships. Jordan has no role in Briar Hollow.
- Jordan lands in board mode, Sakura Heights, "Community Home" current, with the toast "You don't have access to that page." The body is a placeholder: "Community Home — section 7, unchanged".
- Nothing names Briar Hollow. A link to a community that doesn't exist looks exactly the same.
- State: refusal (`FORBIDDEN`, out of scope).

### 4.5 Design choices and open questions (DCPostIt by 8.05.a)

> 1. **Layout.** Drawn: the four areas stacked, in the order Managers, Board members and accountants, Invitations, Residents, with jump links at the top. Residents is last because it is long and paginated. Other option: sub-tabs inside the section. Brief 06 follows this choice.
> 2. **Direct appointment.** Drawn: no second confirmation. The dialog's summary line and the "Appoint Lee Morales" button are the confirmation. Other option: a separate "Appoint Lee Morales?" step.
> 3. **Row actions.** Drawn: a labelled "Actions" menu on each membership row; visible "Resend" and "Revoke" on invitation rows. Other option: visible buttons on membership rows too.
> 4. **Invitation revoked because its sender lost authority.** Drawn: "Revoked" with the second line "Sender's role ended, Oct 12, 2026". Other option: plain "Revoked" with no reason.
> 5. **Scheduled role invitations.** FR-022b lists scheduled invitations in this section, but FR-022 emails role invitations at once in every status, so a role invitation is never Scheduled. Drawn: no Scheduled row. If one ever exists, it uses conventions §5.2. Accepted invitations are not listed; the person appears as a member.
> 6. **Downgrade without a Board Member membership** (Taylor). The spec says the person gets an active Board Member membership but not its term. Drawn: starts today, no end date. Other options: carry the manager membership's end date (Dec 31, 2026); or ask for a term end date in the dialog.
> 7. **Downgrading yourself.** FR-019 allows downgrading your own manager membership, but FR-003a forbids giving yourself a membership. Drawn: "Downgrade to Board Member" on your own row only when you already hold a Board Member membership there, so nothing is granted. Other option: always offer it and let the server decide.
> 8. **Last-manager actions.** Drawn: End, Downgrade and Change end date stay offered on the last open-ended manager, and the refusal shows after confirming. Other options: the dialog opens already showing the refusal with only "Close"; or the items are disabled with the reason next to them.
> 9. **Replace refused because the replacement left the community** between opening and confirming. The spec gives no code or wording. Drawn as copy only (§5).
> 10. **Ended memberships.** Drawn: always listed, under an "Ended" sub-heading in each area. Other option: hidden behind "Show ended (1)".
> 11. **Start dates.** No "Since" column, because the conventions dataset has no start dates. Add start dates to the conventions first if the column is wanted.
> 12. **Resend.** Drawn: no confirmation (conventions §7.4 doesn't list it). The toast says the earlier link no longer works.
> 13. **After ending your own membership** (8.05.t). Drawn (§6): the app takes you to your board-mode landing with a notice. Other option: a toast only.

### 4.6 States not drawn

Their copy is in §5. Each looks like a drawn artboard with a different message.

- **Loading.** The section frame, headings and buttons stay. `wf-ph` rows stand in each table.
- **Load error,** per area: "Couldn't load managers.", "Couldn't load board members and accountants." or "Couldn't load invitations.", each with "Try again". Other areas keep working.
- **Busy.** The dialog's primary button reads "Saving…" or "Sending…" and is disabled.
- **Save failed:** "Couldn't save. Check your connection and try again." at the top of the dialog. Input kept.
- **Rate limited** (FR-041), on send, resend or any write: "Too many requests. Wait a few minutes, then try again." Inline, next to the button pressed.
- **Invalid email:** "Enter a valid email address." under the email field.
- **No person chosen** (direct path): "Choose a person." under the picker (design choice; not in the conventions).
- **Invalid date:** "Enter a real date as MM/DD/YYYY." (as brief 03).
- **Date before today** in Change end date or a term end date: "Choose a date after today. To end a membership today, use End membership." (design choice; the spec sets no rule).
- **Picker, no match:** "No one in Sakura Heights matches “Dana”." with the link "Invite by email instead".
- **Resend or Revoke on an invitation that was just accepted or revoked:** toast "This invitation was already accepted or revoked. The list is now up to date." and the row refreshes (`INVITATION_INVALID`).
- **`INVITATION_PENDING` for a board role:** "new@example.com already has a pending Board Member invitation for Sakura Heights. Resend it instead?" with "Resend invitation".
- **`ALREADY_MEMBER` for a manager:** "taylor@example.com is already a Community Manager of Sakura Heights."
- **Company Administrator invites their own email** (US7 #10): Robin types owner@example.com. Same `SELF_GRANT_NOT_ALLOWED` message as 8.05.g.
- **Taylor's end date changed** (allowed, because Jordan is open-ended): toast "Taylor Grant's end date is now Nov 30, 2026." Clearing it: "Taylor Grant now has no end date."
- **End a Board Member** (Jane Alvarez): §5 dialog copy. Jane keeps her home.
- **Re-appointing a former member** (Dana Fox): Dana isn't in the people list, so she is invited by email. After she accepts, she is listed as a Board Member again and her old row stays under "Ended" (FR-019).
- **Keystone Park after Pat accepts** (US2 #1, brief 10): Managers lists "Pat Rivera" with "Open-ended" and "No end date". "Keystone Park has no Community Manager yet." and the pending line are gone. The pat@example.com row leaves Invitations.
- **new@example.com after accepting** (US2 #16): listed in Board members and accountants under the name they gave, "Board Member", "Ends Dec 31, 2027". The invitation row leaves the list.
- **End Morgan Ellis in Old Mill Commons** (archived): §5 dialog copy. Allowed, although Morgan is the only manager (FR-012, FR-020).
- **Old-tab write in an archived community:** Company Administrator: banner "Old Mill Commons is archived, so it can't be changed. Restore it to make changes." Community Manager (Morgan): "Juniper Row Community Management no longer manages Old Mill Commons, so it can't be changed."

## 5. Copy

Exact strings. Quotes mark visible text. "SR" means text for screen readers only. Messages from conventions §6 are quoted as they are there; real messages fill in real names.

**Section**

| Element | Text |
| --- | --- |
| Section heading | "Managers and memberships" |
| Intro | "Who runs Sakura Heights, who holds a role, and the invitations waiting for an answer. Residents are at the end of this section." |
| Jump links | "Managers" · "Board members and accountants" · "Invitations" · "Residents" (SR group label: "Jump to") |
| Residents placeholder | "Residents — brief 06" (archived: "Residents — read-only, brief 06") |

**Managers area**

| Element | Text |
| --- | --- |
| Title | "Managers" |
| Button | "Appoint a manager" (SR: "Appoint a Community Manager for Sakura Heights") |
| Rule helper (always shown, except when archived) | "A community always needs at least one Community Manager with no end date. Managers with an end date don't count toward this." |
| Table caption (SR) | "Community Managers of Sakura Heights" |
| Columns | "Name" · "Term" · "Actions" |
| Your own row | "Jordan Blake (you)" |
| Open-ended term | chip "Open-ended", under it "No end date" |
| End-dated term | "Ends Dec 31, 2026", under it "Doesn't count toward the rule above" |
| Ended sub-heading | "Ended" (SR: "Ended Community Manager memberships") |
| Ended term | "Ended Oct 12, 2026" |
| Empty | "Keystone Park has no Community Manager yet." then "Keystone Park can't be marked Active until it has a Community Manager with no end date." |
| Pending manager invitation, shown only when there is no manager | "Invitation pending: pat@example.com. It doesn't count until it's accepted." |
| Archived helper (replaces the rule helper) | "The rule that a community needs a Community Manager with no end date doesn't apply while it's archived. These memberships give no access while Old Mill Commons is archived. You can end them. Nothing else here can change." |

**Board members and accountants area**

| Element | Text |
| --- | --- |
| Title | "Board members and accountants" |
| Button | "Appoint a board member or accountant" (SR: "Appoint a board member or accountant for Sakura Heights") |
| Table caption (SR) | "Board members and accountants of Sakura Heights" |
| Columns | "Name" · "Role" · "Term" · "Actions" |
| Roles | "Board Member" · "Accountant" |
| Terms | "Ends Dec 31, 2027" · "No end date" · "Ended Jun 30, 2026" |
| Ended sub-heading | "Ended" (SR: "Ended board and accountant memberships") |
| Empty | "No board members or accountants yet." |

**Row actions**

| Element | Text |
| --- | --- |
| Menu button | "Actions" (SR: "Actions for Jordan Blake") |
| Menu items | "Change end date" · "Downgrade to Board Member" · "End membership" · "Replace manager" |
| Archived row button | "End membership" (SR: "End Morgan Ellis's Community Manager membership") |
| Ended row, revoked invitation | "—" (SR: "No actions") |

**Invitations area**

| Element | Text |
| --- | --- |
| Title | "Invitations" |
| Helper | "Invitations to be a Community Manager, Board Member or Accountant. Each link works for 72 hours. Times are in Sakura Heights' time zone. Resident invitations are under Residents." |
| Table caption (SR) | "Role invitations for Sakura Heights" |
| Columns | "Email" · "Role" · "Sent by" · "Sent" · "Expires" · "Status" · "Actions" |
| Role with a term | "Board Member", second line "Term ends Dec 31, 2027" |
| Status chips | "Pending" · "Expired" · "Revoked" · "Revoked — community archived" |
| Revoked second lines | "Revoked by Jordan Blake, Oct 7, 2026" · "Sender's role ended, Oct 12, 2026" (design choice) · "Sep 30, 2026" |
| Buttons | "Resend" (SR: "Resend invitation to alex@example.com") · "Revoke" (SR: "Revoke invitation to alex@example.com") |
| Empty | "No invitations." |

**Appoint dialog: Community Manager**

| Element | Text |
| --- | --- |
| Title | "Appoint a Community Manager" |
| Line under the title | "Sakura Heights" |
| Path legend | "How do you want to appoint them?" |
| Path 1 | "Choose someone already in Sakura Heights" — helper: "For people who already have a role or a home in Sakura Heights. It takes effect now. No invitation is sent." |
| Path 1 disabled reason | "No one is in Keystone Park yet, so you can only invite by email." |
| Path 2 | "Invite someone by email" — helper: "For anyone else. We email an invitation that's valid for 72 hours. They become a Community Manager only if they accept." |
| Person label | "Person (required)" · search placeholder "Search by name" (SR: "Search people in Sakura Heights") |
| Person option | "Lee Morales", second line "Board Member" · selected: "✓ Selected" |
| Person note | "You, and people who are already Community Managers, aren't listed." |
| Picker result count (SR, polite) | "1 person matches." |
| Email label | "Email address (required)" |
| Email helper | "Until they accept, the invitation shows only this email address." |
| End date label | "End date (optional)" |
| End date helper | "MM/DD/YYYY. Leave empty for no end date. A Community Manager can have an end date only while another Community Manager has none." |
| No end date allowed | "No end date. Keystone Park has no other Community Manager without an end date, so this one can't have one." |
| Onboarding helper (Onboarding community only) | "Keystone Park is Onboarding. Role invitations are still emailed now. Only resident invitations wait until it's marked Active." |
| Summary, direct | "Lee Morales becomes a Community Manager of Sakura Heights now, with no end date. No invitation is sent." |
| Summary, direct with an end date (not drawn) | "Sam Ortiz becomes a Community Manager of Sakura Heights now, until Mar 31, 2027. No invitation is sent." |
| Summary, email | "We'll email pat@example.com an invitation to be a Community Manager of Keystone Park. It's valid for 72 hours." |
| Buttons | "Appoint Lee Morales" · "Send invitation" · "Cancel" · busy: "Saving…" / "Sending…" |

**Appoint dialog: board member or accountant**

| Element | Text |
| --- | --- |
| Title | "Appoint a board member or accountant" |
| Line under the title | "Sakura Heights" |
| Role legend | "Role (required)" · options "Board Member" · "Accountant" |
| Path 1 helper | "For people who already have a role or a home in Sakura Heights. It takes effect now. No invitation is sent." |
| Path 2 helper | "For anyone else. We email an invitation that's valid for 72 hours. They get the role only if they accept." |
| Person note | "You, and people who already hold this role, aren't listed." |
| Term label | "Term end date (optional)" |
| Term helper, direct | "MM/DD/YYYY. Leave empty for no end date. The membership starts today." |
| Term helper, email | "MM/DD/YYYY. Leave empty for no end date. The membership starts when they accept." |
| Summary, email | "We'll email new@example.com an invitation to be a Board Member of Sakura Heights, with a term ending Dec 31, 2027. It's valid for 72 hours." |
| Summary, direct (not drawn) | "Sam Ortiz becomes an Accountant of Sakura Heights now, with no end date. No invitation is sent." |
| Buttons | "Appoint <name>" · "Send invitation" · "Cancel" |

**Change end date dialog**

| Element | Text |
| --- | --- |
| Title | "Change end date" |
| Line under the title | "Jordan Blake · Community Manager · Sakura Heights" |
| Current value | "Now: no end date" · or "Now: ends Dec 31, 2026" |
| Field | "End date (optional)" |
| Helper, manager | "MM/DD/YYYY. Leave empty for no end date. A Community Manager can have an end date only while another Community Manager has none." |
| Helper, Board Member or Accountant | "MM/DD/YYYY. Leave empty for no end date." |
| Buttons | "Save end date" · "Cancel" |

**Confirmation dialogs** (conventions §7.4: title, what happens, what doesn't change, buttons)

*End membership, a co-manager (8.05.q)*

- Title: "End Taylor Grant's Community Manager membership?"
- What happens:
  - "It ends today, Oct 12, 2026. It was set to end Dec 31, 2026."
  - "Taylor Grant can't act as a Community Manager of Sakura Heights from now on."
  - "3 pending invitations Taylor Grant sent are revoked, because Taylor Grant can no longer send them:"
    - "has-account@example.com · Community Manager"
    - "no-account@example.com · Community Manager"
    - "noor@example.com · resident of 719 Keystone Park Dr #35"
- What doesn't change:
  - "Sakura Heights still has a Community Manager with no end date: Jordan Blake (you)."
  - "Nothing is deleted. The ended membership stays in the list under Ended."
- Buttons: "End membership" · "Cancel".

*End membership, the only open-ended manager (8.05.n, before the refusal)*

- Title: "End Jordan Blake's Community Manager membership?"
- What happens: "It ends today, Oct 12, 2026." · "Jordan Blake can't act as a Community Manager of Sakura Heights from now on." · "2 pending invitations Jordan Blake sent are revoked, because Jordan Blake can no longer send them:" · "new@example.com · Board Member" · "kim@example.com · resident of 711 Keystone Park Dr #29"
- What doesn't change: "Nothing is deleted. The ended membership stays in the list under Ended."
- Buttons: "End membership" · "Cancel". After the refusal: "Close" (and, Company Administrator only, "Replace manager").

*End your own membership (8.05.t)*

- Title: "End your Community Manager membership in Sakura Heights?"
- What happens:
  - "It ends today, Oct 12, 2026."
  - "You lose access to Sakura Heights' Community settings at once."
  - "Sakura Heights leaves your community switcher and My Communities, because you have no other role there."
  - "2 pending invitations you sent are revoked, because you can no longer send them:" · "new@example.com · Board Member" · "kim@example.com · resident of 711 Keystone Park Dr #29"
- What doesn't change:
  - "Sakura Heights still has a Community Manager with no end date: Lee Morales."
  - "Your roles in your other communities don't change."
- Buttons: "End my membership" · "Cancel".

*End membership, a Board Member (not drawn)*

- Title: "End Jane Alvarez's Board Member membership?"
- What happens: "It ends today, Oct 12, 2026. It was set to end Dec 31, 2026." · "Jane Alvarez can't act as a Board Member of Sakura Heights from now on."
- What doesn't change: "Jane Alvarez's home, 711 Keystone Park Dr #29, doesn't change." · "Nothing is deleted. The ended membership stays in the list under Ended."
- Buttons: "End membership" · "Cancel".

*End membership in an archived community (not drawn)*

- Title: "End Morgan Ellis's Community Manager membership?"
- What happens: "It ends today, Oct 12, 2026." · "Old Mill Commons is archived, so it can be left with no Community Manager." · "If Old Mill Commons is restored, it returns to Onboarding until a Community Manager with no end date is appointed."
- What doesn't change: "Nothing is deleted. The ended membership stays in the list under Ended."
- Buttons: "End membership" · "Cancel".

*Downgrade, keeps Board Member (8.05.r)*

- Title: "Downgrade Lee Morales to Board Member?"
- What happens: "Lee Morales's Community Manager membership ends today, Oct 12, 2026." · "Lee Morales can't act as a Community Manager of Sakura Heights from now on."
- What doesn't change: "Lee Morales keeps their Board Member membership. It ends Dec 31, 2027, as before." · "Sakura Heights still has a Community Manager with no end date: Jordan Blake (you)."
- Buttons: "Downgrade to Board Member" · "Cancel".

*Downgrade, becomes Board Member (8.05.s)*

- Title: "Downgrade Taylor Grant to Board Member?"
- What happens: "Taylor Grant's Community Manager membership ends today, Oct 12, 2026." · "Taylor Grant becomes a Board Member of Sakura Heights today, with no end date." (design choice) · "Taylor Grant can't act as a Community Manager of Sakura Heights from now on." · "1 pending invitation Taylor Grant sent is revoked, because a Board Member can't send it:" · "noor@example.com · resident of 719 Keystone Park Dr #35"
- What doesn't change: "Sakura Heights still has a Community Manager with no end date: Jordan Blake."
- Buttons: "Downgrade to Board Member" · "Cancel".

*Downgrade, the only open-ended manager (8.05.p, before the refusal)*

- Title: "Downgrade Jordan Blake to Board Member?"
- What happens: "Jordan Blake's Community Manager membership ends today, Oct 12, 2026." · "Jordan Blake becomes a Board Member of Sakura Heights today, with no end date." · "2 pending invitations Jordan Blake sent are revoked …" (as above)
- Buttons: "Downgrade to Board Member" · "Cancel". After the refusal: "Close".

*Replace manager (8.05.v)*

- Title: "Replace Jordan Blake as Community Manager of Sakura Heights?"
- Picker label: "Replacement (required)". Helper: "Only people already in Sakura Heights can be chosen. An invitation can't replace a manager until it's accepted. People who are already Community Managers aren't listed."
- What happens:
  - "Lee Morales becomes a Community Manager of Sakura Heights now, with no end date."
  - "Jordan Blake's Community Manager membership ends today, Oct 12, 2026."
  - "Both happen together. If one can't happen, neither does."
  - "2 pending invitations Jordan Blake sent are revoked, because Jordan Blake can no longer send them:" · "new@example.com · Board Member" · "kim@example.com · resident of 711 Keystone Park Dr #29"
- What doesn't change: "Lee Morales keeps their Board Member membership." · "Taylor Grant's membership doesn't change. It ends Dec 31, 2026." · "Jordan Blake's roles in other communities don't change."
- Buttons: "Replace manager" · "Cancel".

*Revoke invitation (8.05.m)*

- Title: "Revoke the invitation to new@example.com?"
- What happens: "The link in the invitation email stops working at once." · "new@example.com can't use it to become a Board Member of Sakura Heights."
- What doesn't change: "You can send new@example.com a new invitation later."
- Buttons: "Revoke invitation" · "Cancel".

**Toasts**

| Action | Text |
| --- | --- |
| Direct appointment | "Lee Morales is now a Community Manager of Sakura Heights." · board role: "Sam Ortiz is now an Accountant of Sakura Heights." |
| Invitation sent (same whether or not the email has an account) | "Invitation sent to pat@example.com." |
| Resend | "Invitation sent again to alex@example.com. The earlier link no longer works." |
| Revoke | "Invitation to new@example.com revoked." |
| End | "Taylor Grant's Community Manager membership ended." |
| Downgrade | "Lee Morales is now a Board Member only." |
| Replace | "Lee Morales replaced Jordan Blake as Community Manager of Sakura Heights." |
| End date changed | "Taylor Grant's end date is now Nov 30, 2026." · cleared: "Taylor Grant now has no end date." |
| After ending your own (notice on the landing page, stays until dismissed) | "You're no longer a Community Manager of Sakura Heights." |

**Refusals** (conventions §6)

| Case | Text | Where |
| --- | --- | --- |
| `LAST_MANAGER`, end | "Jordan Blake is the only Community Manager of Sakura Heights with no end date. A community must always have one. Appoint another manager first." Company Administrator also: "Or use Replace manager." | Top of the dialog. Primary becomes "Close". |
| `LAST_MANAGER`, downgrade | "Jordan Blake can't be downgraded. They're the only Community Manager of Sakura Heights with no end date. Appoint another manager first." | Top of the dialog. "Close". |
| `LAST_MANAGER`, end date | "Jordan Blake can't be given an end date while they're the only Community Manager of Sakura Heights with no end date." | Top of the dialog. "Close". |
| `LAST_MANAGER`, lost a race | The end or downgrade message above, naming the manager who remains. | Top of the dialog. "Close". |
| `SELF_GRANT_NOT_ALLOWED` | "You can't give yourself a role or a home. Ask another Company Administrator or this community's Community Manager." | Under the email field. |
| `INVITATION_PENDING` | "pat@example.com already has a pending Community Manager invitation for Keystone Park. Resend it instead?" Button: "Resend invitation". | Under the email field. |
| `ALREADY_MEMBER` | "lee@example.com is already a Board Member of Sakura Heights." | Under the email field. |
| `INVITATION_INVALID`, staff | "This invitation was already accepted or revoked. The list is now up to date." | Toast; the row refreshes. |
| `FORBIDDEN`, action | "You don't have permission to do that." | Top of the dialog, or banner. "Close". |
| `FORBIDDEN`, page | "You don't have access to that page." | Toast on the landing page. |
| `COMMUNITY_ARCHIVED`, Company Administrator | "Old Mill Commons is archived, so it can't be changed. Restore it to make changes." | Banner. |
| `COMMUNITY_ARCHIVED`, Community Manager (old tab) | "Juniper Row Community Management no longer manages Old Mill Commons, so it can't be changed." | Banner. |
| Replace refused, replacement no longer in the community (design choice; the spec gives no code) | "The person you chose is no longer in Sakura Heights, so they can't replace Jordan Blake. Choose someone else. Jordan Blake is still the Community Manager." | Top of the dialog. The picker stays. |
| Rate limited | "Too many requests. Wait a few minutes, then try again." | Next to the button pressed. |
| Save failed | "Couldn't save. Check your connection and try again." | Top of the dialog. |
| Invalid email | "Enter a valid email address." | Under the email field. |
| No person chosen (design choice) | "Choose a person." | Under the picker. |
| Invalid date | "Enter a real date as MM/DD/YYYY." | Under the date field. |
| Date before today (design choice) | "Choose a date after today. To end a membership today, use End membership." | Under the date field. |
| Load error | "Couldn't load managers." · "Couldn't load board members and accountants." · "Couldn't load invitations." Button: "Try again". | In the area that failed. |

**Archived banner** (conventions §7.3): "Old Mill Commons is archived. Juniper Row Community Management no longer manages it. Everything here is read-only. Nothing has been deleted."

## 6. Interactions

**Opening the section.**

- Company Administrator: the portfolio's "Community settings" link, then "Managers and memberships" in the side list. Brief 03's "Appoint a manager" prompt opens this section at Managers.
- Community Manager: "Community settings" in board mode, then "Managers and memberships" in the side list.
- Focus starts at the page title. Jump links move focus to the area heading.

**Appoint a manager / Appoint a board member or accountant.**

1. The button opens the dialog. Focus moves to its title.
2. The path choice starts on "Choose someone already in …" when the people list has anyone to offer; otherwise "Invite someone by email" is selected and the first option is disabled with its reason (8.05.i).
3. **Direct path.** The picker searches the community's people list by name, ignoring case, matching part of a word. It leaves out the signed-in person (FR-003a) and anyone who already holds that role. Up and Down move; Enter chooses; Esc closes the list. A former member who has no current role or home there is not in the list; they are invited by email.
4. **Email path.** An email field only. Nothing looks up, suggests or confirms who owns the email (FR-018).
5. **Dates.** A Community Manager end date shows only while the community has another open-ended manager. Otherwise the "No end date" line replaces it (FR-020). Board Member and Accountant dialogs always show the optional term end date (FR-021).
6. **Submit.** The form checks email format, a chosen person and dates first; errors show under their field and nothing is sent. Then the button shows "Saving…" or "Sending…".
7. **Success.** The dialog closes. A toast confirms. Focus moves to the heading of the area that changed: Managers or Board members and accountants after a direct appointment; Invitations after an email invitation. A direct appointment adds the row at once. An email invitation adds a Pending row with no name, the same whether or not the email has an account.
8. **Refusal.** The dialog stays open with the input kept. `SELF_GRANT_NOT_ALLOWED`, `INVITATION_PENDING` and `ALREADY_MEMBER` appear under the email field and are announced at once. "Resend invitation" resends the existing invitation (as Resend below), closes the dialog and shows the resend toast.

**Change end date.**

- Opens a form dialog. Clearing the field removes the end date. "Save end date" saves. `LAST_MANAGER` appears at the top and the primary becomes "Close"; the stored end date is unchanged.
- Making Taylor open-ended, then ending Jordan, is how a Community Manager hands over to an end-dated co-manager.

**End membership, Downgrade to Board Member, Replace manager.**

- Each opens a confirmation dialog. The consequence lines come from the server when it opens: the dates, the role kept or given, and every scheduled or pending invitation the person will no longer be able to send (FR-022).
- Confirming applies the change in one action. On success the dialog closes, the toast shows, focus moves to the Managers heading (or Board members and accountants), and the ended row moves under "Ended". Revoked invitations show their new status at once.
- On `LAST_MANAGER` or `FORBIDDEN` the dialog stays open, the message appears at the top and takes focus, and the primary becomes "Close". The Company Administrator's "Replace manager" button closes this dialog and opens 8.05.v for the same manager.
- Esc and "Cancel" close the dialog without change. Focus returns to the "Actions" button that opened it.

**After ending your own membership (8.05.t).** Jordan is no longer a manager of Sakura Heights, so the next request for this page would be refused (US2 #12). The app takes Jordan to his board-mode landing in another community and shows the notice "You're no longer a Community Manager of Sakura Heights." (design choice, §4.5 question 13). Pressing Back to the old page is refused with "You don't have access to that page."

**After losing a race (8.05.u) or a downgrade seen from an old tab (8.05.y).** "Close" reloads the section. The reload is refused, because the person is no longer a manager, and the app takes them to their landing page with "You don't have access to that page." For Lee, that is board mode, Sakura Heights, as a Board Member.

**Resend.** One press, no confirmation. The invitation gets a new link and a new 72 hours; the resender becomes its sender; the old link is refused (FR-022). The row updates in place and focus stays on its "Resend" button. If the per-recipient cap or the resend cooldown applies, the rate-limit message shows next to the button.

**Revoke.** Opens the confirmation (8.05.m). On confirm, the row reads "Revoked" with who and when, its actions become "—", and focus moves to the Invitations heading.

**Who sees what.**

| Control | Company Administrator | Community Manager of the community | Archived (Company Administrator) |
| --- | --- | --- | --- |
| Appoint a manager | Yes | Yes | No |
| Appoint a board member or accountant | Yes | Yes | No |
| Change end date, Downgrade to Board Member | Yes | Yes | No |
| End membership | Yes | Yes | Yes, the only action |
| Replace manager | Yes, on the only open-ended manager | No | No |
| Resend, Revoke | Yes | Yes | No |
| Status actions in the header | Yes | No | "Restore" only |

Hiding is for clarity only. The server decides every action, and each hidden action has a refusal in §5 for when it is reached anyway.

## 7. Accessibility

The conventions §8 baseline applies. For this section:

- **Headings.** The page `h1` is the community name. "Managers and memberships" is an `h2`. Each area title is an `h3`. Jump links move focus to the area's `h3`.
- **Tables.** Each table has a caption naming it (§5) and real header cells. "(you)", "Open-ended", "No end date", "Doesn't count toward the rule above" and every status chip are text in the cell, so they are read with the row.
- **Rule helper.** The Managers helper text is tied to the Managers table, so it is read with it.
- **Actions menu.** A menu button: Enter or Down opens it; Up and Down move; Enter chooses; Esc closes and returns focus to the button. Each button's name includes the person: "Actions for Taylor Grant".
- **Invitation buttons** have names that make sense alone: "Resend invitation to alex@example.com", "Revoke invitation to new@example.com".
- **Dialogs.** Focus moves to the title on open and stays inside. Esc is Cancel. On close, focus returns to the control that opened it, or to the area heading when that control no longer exists.
- **Path choice.** A radio group with the legend "How do you want to appoint them?". Each option's helper is tied to it. A disabled option keeps its reason as visible text, tied to the option.
- **Person picker.** A combobox, as brief 03's parent list. The number of matches is announced politely. The selected option reads "Selected".
- **Fields.** Labels sit above fields and say "(required)" or "(optional)". Helper text and errors are tied to their field. A field with an error is marked invalid.
- **Refusals.** Inline refusals are announced at once. A refusal at the top of a dialog is announced at once and takes focus. The "Resend invitation" button follows the `INVITATION_PENDING` message in tab order.
- **Toasts** are announced politely and never take focus. The notice after ending your own membership stays until dismissed.
- **Times.** Expiry is a date and time with the time zone abbreviation, never only a countdown.
- **Keyboard order in the appoint dialog:** title → role (board dialog) → path choice → person picker or email → end date and its calendar button → primary button → "Cancel".
- **Contrast.** 4.5:1 for text, 3:1 for control borders and the focus outline. The board-mode banner stays dark ink on violet (spec 025 FR-038).
- **Zoom.** At 200% the invitation table may scroll inside its card, but the page itself never scrolls sideways. Dialogs stay usable at 200%.

## 8. Out of scope for this design

- The Residents area: its table, invite, remove, resend and revoke (brief 06). Here it is a placeholder.
- The invitation email and the acceptance, sign-up and refusal pages an invitee sees, including `INVITATION_EXPIRED`, `INVITATION_INVALID` and `INVITATION_EMAIL_MISMATCH` (briefs 10 and 12).
- The page header, Profile, status actions and their dialogs, including Restore with its option to end memberships first (brief 04).
- Granting and revoking the Company Administrator role (brief 09).
- Architectural review (brief 07).
- Changing a membership's role other than by Downgrade. Editing a sent invitation: revoke it and send a new one.
- A history or audit view of changes. Logging (FR-023) is not visible.
- Start dates ("Since"), bulk invitations, and a page-size picker.
- Scheduled and Accepted role invitations (§4.5 question 5).
- Notifications.
- Phone and tablet artboards beyond the main page's two (conventions §2).

## 9. Design review checklist

- [ ] **US2 #1.** 8.05.i offers only the email path for Keystone Park and says the manager will have no end date. 8.05.j shows "Invitation sent to pat@example.com.", "Keystone Park has no Community Manager yet.", "Invitation pending: pat@example.com. It doesn't count until it's accepted." and the Pending row. "Pat Rivera" appears nowhere.
- [ ] **US2 #3.** 8.05.c's summary says "No invitation is sent." 8.05.d lists Lee Morales as Open-ended at once, with Jordan; there is no invitation row for Lee; Lee is still a Board Member.
- [ ] **US2 #4.** 8.05.e shows two Pending rows for has-account@example.com and no-account@example.com that differ only in email and time. No name, no "account" hint.
- [ ] **US2 #6.** 8.05.l: alex@example.com is Pending, "Sent by" reads Jordan Blake, expires Oct 15, 2026, 10:00 AM EDT, and the toast says the earlier link no longer works.
- [ ] **US2 #9.** 8.05.n shows the `LAST_MANAGER` end message and "Or use Replace manager.", with "Close". Jordan stays "Open-ended".
- [ ] **US2 #10.** 8.05.o shows "Jordan Blake can't be given an end date while they're the only Community Manager of Sakura Heights with no end date." Jordan stays "Open-ended".
- [ ] **US2 #11.** 8.05.v's picker offers only people in Sakura Heights and says both changes happen together. 8.05.w shows Lee Open-ended and Jordan under "Ended" with Oct 12, 2026. §5 has the refusal for a replacement who left.
- [ ] **US2 #12.** 8.05.t says the membership ends today, Oct 12, 2026, and that Jordan loses access at once; Lee remains the open-ended manager. §6 describes the move to the landing page.
- [ ] **US2 #13.** 8.05.r says Lee keeps the Board Member membership (ends Dec 31, 2027) and can't act as a manager from now on. 8.05.y shows Lee's next manager action refused.
- [ ] **US2 #14.** 8.05.u shows Lee's end of Jordan refused with the `LAST_MANAGER` message, and a note that Jordan's request won.
- [ ] **US2 #15.** 8.05.q lists both Community Manager invitations Taylor sent as revoked. 8.05.w shows a row reading "Revoked" / "Sender's role ended, Oct 12, 2026".
- [ ] **US2 #16.** 8.05.f has "Term end date (optional)" set to 12/31/2027 and the summary "…with a term ending Dec 31, 2027…". 8.05.a's row reads "Board Member" / "Term ends Dec 31, 2027".
- [ ] **US2 #17.** 8.05.g shows the `SELF_GRANT_NOT_ALLOWED` message under the email field. 8.05.c's picker doesn't list Jordan.
- [ ] **US2 #18.** 8.05.y: a Board Member's attempt to end a manager membership shows "You don't have permission to do that." and "Close".
- [ ] **US5 #2.** 8.05.a (Jordan) has both Appoint buttons, Downgrade and End on Taylor, Resend and Revoke on invitations, and the Residents placeholder. No Replace manager, no status actions, no Change parent.
- [ ] **US5 #7.** 8.05.x has the archived banner; all four areas; plain-text values; "End membership" as the only action in the section; "Restore" in the header; drew@example.com reads "Revoked — community archived" / "Sep 30, 2026".
- [ ] **US5 #8.** 8.05.a and 8.05.b list new@example.com (Pending, Board Member) and alex@example.com (Expired, Community Manager) with email, role, sent by, sent at and expires at, each with "Resend" and "Revoke".
- [ ] **FR-018.** One dialog with two paths. The direct path lists only people in the community and takes effect now; the email path has no lookup and always sends an invitation.
- [ ] **FR-019.** End and downgrade work for co-managers and for oneself. 8.05.r keeps a Board Member membership; 8.05.s gives one, labelled as a design choice.
- [ ] **FR-020.** The rule helper text is on every Managers area except the archived one. Open-ended managers show "Open-ended"; end-dated ones show "Doesn't count toward the rule above". 8.05.i shows the "No end date" line. 8.05.c has the end-date helper. Replace manager appears only for a Company Administrator, only on the only open-ended manager.
- [ ] **FR-021.** Both viewpoints have "Appoint a board member or accountant", with Role and "Term end date (optional)".
- [ ] **FR-022, states.** Pending, Expired, Revoked and "Revoked — community archived" appear as words. No Scheduled or Accepted role invitation is drawn.
- [ ] **FR-022, resend and revoke.** Pending and Expired rows have both buttons; revoked rows have "—"; 8.05.m is the revoke confirmation.
- [ ] **FR-022, duplicates.** 8.05.k shows `INVITATION_PENDING` with "Resend invitation". 8.05.h shows `ALREADY_MEMBER`.
- [ ] **FR-022, sender authority.** 8.05.q, s, t and v each list the invitations they revoke.
- [ ] **FR-022b.** The Invitations table has the columns Email, Role, Sent by, Sent, Expires, Status, Actions, with times in EDT, next to the active and ended memberships.
- [ ] **FR-003a.** No picker offers the signed-in person; 8.05.g refuses one's own email.
- [ ] **FR-042.** Every refusal says why, except the two `FORBIDDEN` messages. 8.05.z names nothing from the request.
- [ ] **Permission variants.** Company Administrator artboards have the breadcrumb, status actions and Replace manager. Community Manager artboards have none of them.
- [ ] **No hidden data shown.** No pending invitee has a name. No email field suggests a person. 8.05.z doesn't name Briar Hollow.
- [ ] **Design choices.** A DCPostIt by 8.05.a lists the 13 items in §4.5. Every drawn design choice is labelled "Design choice".
- [ ] **Accessibility.** Table captions, labelled Actions menus, radio legend, picker as a combobox, focus on dialog titles and refusals, and toasts that don't take focus are noted on the artboards or in notes.
- [ ] **Data.** Only conventions §4 people, communities and invitations, plus the two US2 #4 addresses. Times are EDT. Branches follow §4.3.
- [ ] **Canvas.** Labels and ids match §1 exactly (s8-05a to s8-05z). Sections 1–7 render as before.
