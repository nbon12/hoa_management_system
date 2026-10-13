# 09 — Company administrators

This brief is for the Company administrators page: the list of Company Administrators and their pending invitations, granting the role by email, revoking it, and the last-administrator refusal. §1 is the prompt to paste into Claude Design. The other sections hold the details the prompt points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file. Brief 01 draws the company-area frame, brief 05 the invitation table and dialog patterns this page reuses, brief 10 the acceptance pages and brief 12 the emails.

## 1. Prompt for Claude Design

```text
Project "HOA Management CRM", canvas "NekoHOA Wireframes.html". Attached: spec.md (spec 030, the authority), 00-conventions.md (rules, dataset, vocabulary, refusal copy) and this brief, 09-company-administrators.md. spec.md wins, then the conventions. Brief §4 has data and states, §5 exact copy, §6 interactions.

Add these to section "8 · Company administration" (create it after section 7 if missing, conventions §3). Ids s8-09a to s8-09m. Pages are 1360 wide; dialogs sit over their dimmed page. Shared section-8 frames and data go in wf-company.jsx; these artboards go in wf-company-admins.jsx. Labels exactly:
8.09.a Company administrators · Company Administrator
8.09.b Invite a Company Administrator · ops@example.com · Company Administrator
8.09.c Company administrators · invitation sent · Company Administrator
8.09.d Invite a Company Administrator · refused · own email · Company Administrator
8.09.e Invite a Company Administrator · refused · invitation already pending · Company Administrator
8.09.f Revoke invitation dialog · ops@example.com · Company Administrator
8.09.g Revoke role dialog · Casey Novak · Company Administrator
8.09.h Company administrators · Casey Novak revoked · Company Administrator
8.09.i Revoke role dialog · your own · refused · last Company Administrator · Company Administrator
8.09.j Revoke role dialog · Robin Hale · refused · lost a race · Company Administrator
8.09.k Board mode · role revoked · Board Member
8.09.l Appoint a board member · refused · own email · Company Administrator
8.09.m Company administrators · refused · Community Manager

The page: a note on what the role is (business setup, not platform administration, no board powers); "Current Company Administrators" (Name, Email, Granted by, Granted, Revoke role); "Invitations" (Email, Sent by, Sent, Expires, Status, Resend and Revoke). Granting is always an email invitation.

Robin Hale (avatar "RH") and Casey Novak ("CN") use the company-area frame (conventions §7.1), "Company administrators" current, no violet banner. 8.09.k is Casey in board mode: WFShellBoard + boardNav(), violet banner unchanged (dark ink on violet). 8.09.l uses brief 05's appoint dialog in Sakura Heights. 8.09.m is Jordan Blake's landing, as brief 01's 8.01.q.

Reuse BoardVendors (table card with a header bar), brief 05's dialogs and invitation table if section 8 has them, wf-card (lav for the note), wf-table, wf-btn, wf-pill, wf-field, wf-ph. Put brief §4.2's design choices and question on DCPostIts beside the named artboards.

Fidelity: lofi, in section 7's visual language. Layout, copy, states and who sees what are binding; colour and spacing are placeholders. States and refusals are words, never colour alone.

Don't draw: a name for any pending invitee; a person picker, lookup or account hint on the invite form; the command-line setup step or recovery mode (no screen exists); backup@example.com; a log or history view; former Company Administrators (post-it only); platform or deployment controls; a "type to confirm" step; error codes; phone layouts.

When done, open every artboard and check it renders without console errors, shows its stated state, and matches brief §4 data and §5 copy word for word. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

- **Company Administrators** (Robin Hale, Casey Novak) decide who else holds the role. They grant it by email, always as an invitation, and revoke it from any Company Administrator, themselves included (FR-037). The page is in the company area, under "Company administrators" in its nav (FR-036).
- **The role is business setup, not platform administration** (spec Context, FR-003). It grants no board powers. People who hold it need to see this, and so does anyone they invite, so the page and the invite dialog both say it.
- **There is always at least one.** Revoking the last Company Administrator, including yourself, is refused, also when two revokes race (FR-037). This keeps constitution §3 true for communities with no manager (FR-008). Nobody grants the role to themselves (FR-003a).
- **Community Managers and everyone else** never see the page or its nav entry. A direct request is refused with 403 `FORBIDDEN` (US7 #5, conventions §7.10).
- **The first Company Administrator and recovery mode** come from a command-line step the deployer runs (FR-038). There is no screen for it. Its only visible traces are "Setup step" in the "Granted by" column and the recovery-mode email to every Company Administrator (brief 12).

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| US7 #1 | Robin invites ops@example.com. The dialog says it's an invitation that works once, for 72 hours, only for that email. After sending, ops@example.com is a Pending row with email only. Acceptance is brief 10; the email is brief 12. | 8.09.b, c |
| US7 #2 | Robin revokes Casey. The dialog says Casey loses the role on their next request and lists the invitations Casey sent that Casey can no longer send (none). Casey's next request lands in board mode without the company entry. Casey's dialog in 8.09.j shows the list with three of Robin's invitations. | 8.09.g, h, j, k |
| US7 #3 | Robin, now the only one, tries to revoke their own role. Refused with the `LAST_COMPANY_ADMIN` self message. | 8.09.i |
| US7 #4 | Robin and Casey revoke each other at the same moment. Robin's request wins (8.09.h). Casey's is refused with the `LAST_COMPANY_ADMIN` race message, and one Company Administrator remains. | 8.09.h, j, k |
| US7 #5 | Jordan Blake, a Community Manager, has no company entry. A direct request for the page is refused with 403 `FORBIDDEN`. | 8.09.m |
| US7 #9, FR-003 | The note "What this role is" on the page, and a line in the invite dialog: no board powers, no vote, no recording of outcomes. Robin's refused board page is brief 01's 8.01.h. | 8.09.a, b |
| US7 #10, FR-003a | Self-grant refused with `SELF_GRANT_NOT_ALLOWED`: Robin's own email as a Company Administrator (page wording) and as a Board Member of Sakura Heights (roles-and-homes wording). The resident case is brief 06 (§4.6). | 8.09.d, l |
| US7 #11, FR-039 | Nothing on screen. Every grant and revoke is logged. "Granted by: Setup step" matches the log actor "setup". No log view is drawn. | 8.09.a (canvas note) |
| FR-037 | Granting by email, always an invitation, even for an existing account. Revoke from any Company Administrator, including yourself. The last one can't be revoked; the rule helper is always shown. Changes take effect on the person's next request. | 8.09.a–k |

## 4. Artboards

"Now" is Monday, Oct 12, 2026, 10:00 AM EDT, except 8.09.b and c (8:30 AM EDT). Company Administrator invitations show times in the company default time zone, America/New_York (FR-022). Desktop width only.

### 4.1 The page

**Frame.** The company-area frame (conventions §7.1): top bar with the account cluster; strip "Juniper Row Community Management · Company area"; left nav Portfolio · Add community · Company defaults · Company administrators (current). No breadcrumb; this is a top-level page. No violet banner. Robin: avatar "RH". Casey: avatar "CN", plus Casey's board-mode control as brief 01 draws it (8.01.b), with a `.wf-note` "top bar per brief 01".

**Body, top to bottom.**

1. Page title "Company administrators" (`wf-h1`) and its sub line (`wf-sub`).
2. **The note** (`wf-card lav`), full width, heading "What this role is" (§5.1).
3. **Current Company Administrators** card (`wf-card`, header bar as in `BoardVendors`): title left, "Invite a Company Administrator" right. Under the bar, the rule helper, always shown. Then the table: Name · Email · Granted by · Granted · Actions. Order (design choice): oldest grant first. The signed-in person's row adds "(you)". Every row has a visible "Revoke role" button, own row included (D1).
4. **Invitations** card: title and helper line, no button. Table: Email · Sent by · Sent · Expires · Status · Actions (D2, D3). Pending and Expired rows have visible "Resend" and "Revoke" buttons. Empty: "No invitations."

**Who sees what.** Every Company Administrator sees the same page and every control. Nobody else sees the page, its nav entry or the company area (FR-035, FR-036). Hiding is for clarity only; the server decides (§5.4 has a refusal for each hidden action).

### 4.2 Design choices and open question (each on a `DCPostIt`)

| # | Question | Drawn | Other option | Post-it beside |
| --- | --- | --- | --- | --- |
| D1 | "Revoke role" on the only Company Administrator. Follows brief 05 D8. | Offered; refused after confirming. | Disabled, with "You're the only Company Administrator." next to it. The refusal in the dialog is still needed for races (US7 #4). | 8.09.i |
| D2 | Offer column. Conventions §5.2 asks every invitation row to show the offer, and here it is always the same. | No Role column. The card helper names the offer. | A "Role" column reading "Company Administrator" on every row, as brief 05. | 8.09.a |
| D3 | Which invitation states to list. FR-022b lists states only for community role invitations; the spec is silent for this page. | Pending and Expired, because both can be resent or revoked. Revoked and accepted rows leave the list. | Also list Revoked rows, with "Revoked by Robin Hale, Oct 12, 2026" or "Sender's role ended, Oct 12, 2026", as brief 05. | 8.09.a |
| D4 | Former Company Administrators. The grant keeps who revoked it and when (Key Entities). | Not listed. A revoked person leaves the table. | A "Revoked" sub-list: "Casey Novak · Revoked by Robin Hale, Oct 12, 2026". | 8.09.h |
| D5 | "Granted by" and "Granted" for a grant made by accepting an invitation. | The invitation's sender at acceptance (a resend makes the resender the sender, FR-022); the acceptance date. | The date the invitation was sent. | 8.09.c |

**Open question Q1** (`DCPostIt` beside 8.09.a): should a recovery-mode grant read "Setup step", like the first grant, or "Setup step (recovery mode)"? The spec logs both with the actor "setup" (FR-039). Not drawn: no recovery-mode grant is on the page.

### 4.3 Data and branches

**Baseline** (conventions §4.2, §4.4, §4.5):

| Name | Email | Granted by | Granted |
| --- | --- | --- | --- |
| Robin Hale | owner@example.com | Setup step | Oct 1, 2026 |
| Casey Novak | casey@example.com | Robin Hale | Oct 2, 2026 |

| Email | Sent by | Sent | Expires | Status |
| --- | --- | --- | --- | --- |
| ops@example.com | Robin Hale | Oct 12, 2026, 8:30 AM EDT | Oct 15, 2026, 8:30 AM EDT | Pending |

Robin's other pending invitations (they matter only in the revoke dialogs, 8.09.i and j): pat@example.com (Community Manager of Keystone Park) and ann@example.com (Board Member of Briar Hollow). Casey has sent no invitations.

| Branch | Change | Artboards |
| --- | --- | --- |
| Before ops@example.com | Oct 12, 8:30 AM EDT. No invitation rows. | 8.09.b, c |
| The race | Oct 12, 10:00 AM. Robin confirms revoking Casey while Casey confirms revoking Robin. Robin's request wins. | 8.09.g, h (Robin); 8.09.j, k (Casey) |
| Robin alone | After the race. Current: Robin Hale only. ops@example.com still Pending. | 8.09.i |

### 4.4 Artboard list

**8.09.a Company administrators · Company Administrator.** Robin. Baseline. State: default. Post-its D2, D3, Q1. Canvas note: "Every grant and revoke is logged as a sensitive event (FR-039). Nothing on screen shows the log. “Setup step” is the command-line setup (FR-038)."

**8.09.b Invite a Company Administrator · ops@example.com · Company Administrator.** Robin, 8:30 AM. Dialog over the page with no invitation rows, so the dimmed Invitations card shows the empty state "No invitations.". Dialog: title and the line under it; the role line; "Email address (required)" holding "ops@example.com" with its helper; the always-an-invitation line; the summary; "Send invitation", "Cancel". No person picker, no direct grant. State: default (filled).

**8.09.c Company administrators · invitation sent · Company Administrator.** Robin, 8:30 AM, after 8.09.b. Toast "Invitation sent to ops@example.com.". The ops@example.com row as in the baseline. Focus on the Invitations heading. No name anywhere for ops@example.com. Notes: "Same toast and row whether or not ops@example.com has an account (FR-018, FR-037)." · "After acceptance (brief 10), the row leaves this list. The person appears under Current Company Administrators with their account name, Granted by Robin Hale, and can open the company area on their next request (US7 #1)." State: success. Post-it D5.

**8.09.d Invite a Company Administrator · refused · own email · Company Administrator.** Robin. Baseline. The dialog with "owner@example.com". Under the email field: "You can't invite yourself. Ask another Company Administrator." The field is marked invalid. The dialog stays open; input kept. State: refusal (`SELF_GRANT_NOT_ALLOWED`, FR-003a).

**8.09.e Invite a Company Administrator · refused · invitation already pending · Company Administrator.** Robin. Baseline. The dialog with "ops@example.com". Under the email field: the `INVITATION_PENDING` message and a "Resend invitation" button. "Send invitation" stays, so Robin can change the email. State: refusal.

**8.09.f Revoke invitation dialog · ops@example.com · Company Administrator.** Robin. Baseline. Opened from the row's "Revoke". After confirming (not drawn): toast "Invitation to ops@example.com revoked."; the row leaves the list (D3); the card reads "No invitations.". State: confirmation.

**8.09.g Revoke role dialog · Casey Novak · Company Administrator.** Robin. Baseline. Opened from Casey's "Revoke role". Copy in §5.3. The invitations line reads that Casey has none. State: confirmation (US7 #2).

**8.09.h Company administrators · Casey Novak revoked · Company Administrator.** Robin, after confirming 8.09.g (branch "The race"). Toast "Casey Novak is no longer a Company Administrator.". Current: "Robin Hale (you)" only, with "Revoke role" still shown (D1). ops@example.com still Pending. Focus on the Current Company Administrators heading. Note: "Casey loses the role on their next request (8.09.k). Casey stays a Board Member of Keystone Crossing Master." State: success (US7 #2, #4). Post-it D4.

**8.09.i Revoke role dialog · your own · refused · last Company Administrator · Company Administrator.** Robin. Branch "Robin alone". Opened from Robin's own "Revoke role". Robin pressed "Revoke my role". At the top: the `LAST_COMPANY_ADMIN` self message. Below, the consequences as they were when it opened, listing Robin's three pending invitations (§5.3). Primary button "Close". Behind: Robin's row unchanged; ops@example.com still Pending. Note: "A pending invitation doesn't count until it's accepted." State: refusal (US7 #3). Post-it D1.

**8.09.j Revoke role dialog · Robin Hale · refused · lost a race · Company Administrator.** Casey, company area. Behind: the baseline seen by Casey ("Casey Novak (you)"). Opened from Robin's "Revoke role". Casey pressed "Revoke role" at the same moment Robin confirmed 8.09.g. At the top: the `LAST_COMPANY_ADMIN` race message. Below, the consequences as they were when it opened, listing ops@example.com, pat@example.com and ann@example.com. Primary button "Close". Notes: "Both pressed at once. Exactly one wins (FR-037). Robin's won, so nothing in this dialog happened and Casey's role is gone." · "On Close, the page reloads, is refused, and Casey lands in board mode (8.09.k)." State: refusal (US7 #4).

**8.09.k Board mode · role revoked · Board Member.** Casey, after "Close" in 8.09.j. `WFShellBoard`, strip "Keystone Crossing Master · Board Member", "Community Home" current, "Community settings" and "Add community" locked (🔒, as brief 01 8.01.c). Mode control "Resident · Board" with Board on: the Option A "Company" segment is gone. Body: `wf-ph` "Community Home — section 7, unchanged". Toast "You don't have access to that page.". Notes: "Under Option B, the “Company” menu button is gone instead (brief 01)." · "Casey's Board Member membership doesn't change. The two roles are separate (FR-002, FR-003)." State: refusal, then landing (US7 #2, FR-036a step 1).

**8.09.l Appoint a board member · refused · own email · Company Administrator.** Robin in Sakura Heights' Managers and memberships (company-area frame, breadcrumb "Portfolio › Sakura Heights", "Portfolio" current, header "Sakura Heights", chip "Active", action "Archive"). Brief 05's "Appoint a board member or accountant" dialog: Role "Board Member", "Invite someone by email" selected, email "owner@example.com", no term. Under the email field: the roles-and-homes `SELF_GRANT_NOT_ALLOWED` message. State: refusal (US7 #10). Note: "Same dialog as 8.05.g. Robin isn't in Sakura Heights' people list, so only the email path can reach this."

**8.09.m Company administrators · refused · Community Manager.** Jordan Blake opened a link to this page. Draw Jordan's landing as brief 01's 8.01.q: board mode, My Communities, no company entry, toast "You don't have access to that page.". State: refusal (US7 #5). Note: "A grant or revoke request from Jordan is refused the same way (403 FORBIDDEN). Nothing on screen offers one."

### 4.5 States not drawn

Copy is in §5. Each looks like a drawn artboard with a different message.

- Loading (`wf-ph` rows in each table); load error per card; busy buttons; save failed; rate limited; invalid email.
- `ALREADY_MEMBER`: Robin invites casey@example.com in the baseline.
- Resend on ops@example.com: the row gets a new Sent and Expires and the resender as Sent by; focus stays on "Resend" (as brief 05 8.05.l).
- Resend or Revoke on an invitation just accepted or revoked: the `INVITATION_INVALID` staff toast; the row refreshes.
- Revoking your own role while another Company Administrator remains (Robin in the baseline): §5.3 copy. After confirming, Robin's next request is refused and Robin lands per FR-036a. Robin has no home and no membership, so that is "No home linked to this account" (brief 11).
- A Company Administrator whose role was revoked acts from an old tab: "You don't have permission to do that." in the dialog; primary becomes "Close".

### 4.6 Related notes and artboards

- Canvas note beside 8.09.j: "If Casey's request had won, Robin's three invitations would be revoked in the same action, each logged (FR-022): ops@example.com leaves this list (D3); pat@example.com and ann@example.com read “Revoked · Sender's role ended, Oct 12, 2026” in Keystone Park's and Briar Hollow's Invitations (brief 05 D4)."
- Robin's own email as a resident: brief 06 §4.4 (not drawn there), same message as 8.09.l.
- Robin opens a board page and is refused (US7 #9): brief 01 8.01.h.

## 5. Copy

Quote these exactly. "SR" means text for screen readers only.

### 5.1 Page

| Element | Text |
| --- | --- |
| Page title | "Company administrators" |
| Sub line | "The people who set up Juniper Row Community Management's business in this app." |
| Note heading | "What this role is" |
| Note body | "Company Administrators set up the company's business here: communities, their managers and residents, company defaults, and who holds this role. It's a business role, not a platform or technical one. It gives no board powers in any community: no vote on architectural applications, no recording of their outcomes, no reading of applications, and no homeowner or financial data beyond these company pages. To do those things in a community, you need a role there, given by another Company Administrator or that community's Community Manager." |
| Current card title | "Current Company Administrators" (SR caption: "Current Company Administrators of Juniper Row Community Management") |
| Button | "Invite a Company Administrator" |
| Rule helper | "The company always needs at least one Company Administrator. The last one can't be revoked." |
| Columns | "Name" · "Email" · "Granted by" · "Granted" · "Actions" |
| Own row | "Robin Hale (you)" |
| Granted by · Granted | "Setup step" (SR: "Granted by the setup step") · "Robin Hale"; "Oct 1, 2026" · "Oct 2, 2026" |
| Row button | "Revoke role" (SR: "Revoke Casey Novak's Company Administrator role"; own row: "Revoke your own Company Administrator role") |
| Invitations title | "Invitations" (SR caption: "Company Administrator invitations") |
| Invitations helper | "Invitations to be a Company Administrator. Each link works once, for 72 hours, and only for the email it was sent to. Times are in the company's default time zone." |
| Columns | "Email" · "Sent by" · "Sent" · "Expires" · "Status" · "Actions" |
| Status chips | "Pending" · "Expired" |
| Row buttons | "Resend" (SR: "Resend invitation to ops@example.com") · "Revoke" (SR: "Revoke invitation to ops@example.com") |
| Empty | "No invitations." |

### 5.2 Invite dialog

| Element | Text |
| --- | --- |
| Title | "Invite a Company Administrator" |
| Line under the title | "Juniper Row Community Management" |
| Role line | "A Company Administrator sets up the company's business in this app. The role gives no board powers in any community." |
| Email label | "Email address (required)" |
| Email helper | "Until they accept, the invitation shows only this email address." |
| Always-an-invitation line | "We always send an invitation, even if this email already has an account. They become a Company Administrator only if they accept." |
| Summary | "We'll email ops@example.com an invitation to be a Company Administrator of Juniper Row Community Management. The link works once, for 72 hours, and only for that email address." |
| Buttons | "Send invitation" (busy: "Sending…") · "Cancel" |

### 5.3 Confirmation dialogs

*Revoke another person's role (8.09.g)*

- Title: "Revoke Casey Novak's Company Administrator role?"
- What happens: "Casey Novak loses the Company Administrator role on their next request." · "After that, Casey Novak can't open the company area." · "Casey Novak has no scheduled or pending invitations, so none are revoked."
- What doesn't change: "Casey Novak keeps their account and their Board Member membership in Keystone Crossing Master. Community roles are separate from this role." · "The company still has a Company Administrator: Robin Hale (you)."
- Buttons: "Revoke role" · "Cancel".

*Revoke another person's role, with invitations (8.09.j, as opened by Casey)*

- Title: "Revoke Robin Hale's Company Administrator role?"
- What happens: "Robin Hale loses the Company Administrator role on their next request." · "After that, Robin Hale can't open the company area." · "3 pending invitations Robin Hale sent are revoked, because Robin Hale can no longer send them:" · "ops@example.com · Company Administrator" · "pat@example.com · Community Manager of Keystone Park" · "ann@example.com · Board Member of Briar Hollow"
- What doesn't change: "Robin Hale keeps their account." · "The company still has a Company Administrator: Casey Novak (you)."
- Buttons: "Revoke role" · "Cancel". After the refusal: "Close".

*Revoke your own role (8.09.i; also the not-drawn case with Casey still a Company Administrator)*

- Title: "Revoke your own Company Administrator role?"
- What happens: "You lose the Company Administrator role on your next request." · "After that, you can't open the company area." · "3 pending invitations you sent are revoked, because you can no longer send them:" then the three lines above.
- What doesn't change: "Your account isn't deleted." When another remains: "The company still has a Company Administrator: Casey Novak."
- Buttons: "Revoke my role" · "Cancel". After the refusal: "Close".

*Revoke invitation (8.09.f)*

- Title: "Revoke the invitation to ops@example.com?"
- What happens: "The link in the invitation email stops working at once." · "ops@example.com can't use it to become a Company Administrator."
- What doesn't change: "You can send ops@example.com a new invitation later."
- Buttons: "Revoke invitation" · "Cancel".

### 5.4 Toasts and refusals

| Case | Text | Where |
| --- | --- | --- |
| Sent (same whether or not the email has an account) | "Invitation sent to ops@example.com." | Toast |
| Resent | "Invitation sent again to ops@example.com. The earlier link no longer works." | Toast |
| Invitation revoked | "Invitation to ops@example.com revoked." | Toast |
| Role revoked | "Casey Novak is no longer a Company Administrator." | Toast |
| `SELF_GRANT_NOT_ALLOWED`, this page | "You can't invite yourself. Ask another Company Administrator." | Under the email field |
| `SELF_GRANT_NOT_ALLOWED`, roles and homes (8.09.l) | "You can't give yourself a role or a home. Ask another Company Administrator or this community's Community Manager." | Under the email field |
| `INVITATION_PENDING` | "ops@example.com already has a pending Company Administrator invitation. Resend it instead?" Button: "Resend invitation". | Under the email field |
| `ALREADY_MEMBER` (conventions pattern) | "casey@example.com is already a Company Administrator." | Under the email field |
| `LAST_COMPANY_ADMIN`, self | "You're the only Company Administrator, so your role can't be revoked. Invite another Company Administrator first. Once they accept, your role can be revoked." | Top of the dialog. Primary becomes "Close". |
| `LAST_COMPANY_ADMIN`, race | "Robin Hale is now the only Company Administrator, so the role can't be revoked." | Top of the dialog. Primary becomes "Close". |
| `INVITATION_INVALID`, staff | "This invitation was already accepted or revoked. The list is now up to date." | Toast; the row refreshes |
| `FORBIDDEN`, page | "You don't have access to that page." | Toast on the landing page |
| `FORBIDDEN`, action | "You don't have permission to do that." | Top of the dialog. "Close". |
| Rate limited | "Too many requests. Wait a few minutes, then try again." | Next to the button pressed |
| Save failed | "Couldn't save. Check your connection and try again." | Top of the dialog |
| Invalid email | "Enter a valid email address." | Under the email field |
| Load error | "Couldn't load Company Administrators." · "Couldn't load invitations." Button: "Try again". | In the card that failed |

## 6. Interactions

- **Opening.** "Company administrators" in the company-area nav. Focus goes to the page title.
- **Invite.** "Invite a Company Administrator" opens the dialog; focus moves to its title. The email field is the only input. Nothing looks up, suggests or confirms who owns the email (FR-018). The format check runs first. "Send invitation" shows "Sending…". On success the dialog closes, the toast shows, a Pending row appears with email only, and focus moves to the Invitations heading.
- **Invite refusals.** The dialog stays open with the input kept. `SELF_GRANT_NOT_ALLOWED`, `INVITATION_PENDING` and `ALREADY_MEMBER` appear under the email field and are announced at once. "Resend invitation" resends the existing invitation, closes the dialog and shows the resend toast.
- **Resend.** One press, no confirmation (brief 05 D12). New link, new 72 hours, the old link stops working; the resender becomes Sent by (FR-022). Focus stays on "Resend".
- **Revoke invitation.** Opens 8.09.f. On confirm the row leaves the list (D3); focus moves to the Invitations heading.
- **Revoke role.** Opens the confirmation. The consequence lines come from the server when it opens, including every scheduled or pending invitation the person could no longer send (FR-022). Confirming revokes the role and those invitations in one action. On success the dialog closes, the toast shows, the row leaves the table (D4), and focus moves to the Current Company Administrators heading. The person loses the role on their next request (FR-037).
- **`LAST_COMPANY_ADMIN`.** The dialog stays open; the message appears at the top and takes focus; the primary becomes "Close". For Robin (8.09.i), "Close" returns focus to Robin's "Revoke role". For Casey after a lost race (8.09.j), "Close" reloads the page; the reload is refused because Casey no longer holds the role, and the app takes Casey to the FR-036a landing with the `FORBIDDEN` toast (8.09.k).
- **After revoking your own role** (not drawn). The next request is refused and the app takes you to your FR-036a landing.
- **Not a Company Administrator.** No entry is shown. A page request is refused with 403 `FORBIDDEN`; the app takes the person to their landing page with the toast (8.09.m). A grant or revoke request is refused the same way.
- **Logging (FR-039).** Every grant, revoke and invitation change is logged with the actor, the target ID, the change and UTC time. Nothing on screen shows it.

## 7. Accessibility

Conventions §8 applies. For this page:

- **Headings.** "Company administrators" is the `h1`. "What this role is", "Current Company Administrators", "Invitations" and each dialog title are headings.
- **The note** is a labelled region, read before the tables. It stands out by position and border, not colour alone.
- **Tables** have captions (§5.1) and real header cells. "(you)", "Setup step" and status chips are text in the cell.
- **Rule helper** is tied to the Current Company Administrators table, so it is read with it.
- **Buttons** have names that make sense alone: "Revoke Casey Novak's Company Administrator role", "Resend invitation to ops@example.com".
- **Dialogs.** Focus moves to the title and stays inside. Esc is Cancel. A refusal at the top is announced at once and takes focus. Consequence lines and invitation lists are real lists.
- **Email field.** Visible label with "(required)"; helper and errors tied to the field; the field is marked invalid on error. "Resend invitation" follows the `INVITATION_PENDING` message in tab order.
- **Contrast.** 4.5:1 for text, including the note on its lav fill. In board mode (8.09.k) the banner stays dark ink on violet (spec 025 FR-038).

## 8. Out of scope for this design

- **The setup step and recovery mode** (FR-038, US7 #6–#8). They run from the command line and have no screen. The recovery-mode email to every Company Administrator is brief 12. backup@example.com (conventions §4.4) appears only there.
- The company-area frame, its entry and nav (brief 01).
- The Company Administrator invitation landing and sign-up (brief 10) and the invitation email (brief 12).
- Self-grant in Managers and memberships by a Community Manager (brief 05) and in the Residents area (brief 06).
- A log, audit or history view, and a list of former Company Administrators beyond post-it D4 (FR-039, FR-040).
- Any platform, server, deployment, feature-flag or database control (spec Out of Scope).
- Phone and tablet layouts.

## 9. Design review checklist

- [ ] **FR-003, US7 #9.** 8.09.a shows "What this role is": business role, not platform; no vote, no recording of outcomes, no reading of applications. The invite dialog repeats that it gives no board powers (8.09.b).
- [ ] **FR-037, US7 #1.** Granting is by email only, always an invitation: no person picker, no lookup, and the dialog says an existing account still gets an invitation that works once, for 72 hours, only for that email (8.09.b).
- [ ] **US7 #1.** After sending, ops@example.com is a Pending row with Sent by, Sent and Expires in EDT, and no name; the toast is "Invitation sent to ops@example.com." (8.09.c).
- [ ] **FR-003a, US7 #10.** Robin's own email is refused with "You can't invite yourself. Ask another Company Administrator." (8.09.d) and, as a Board Member of Sakura Heights, with the roles-and-homes message (8.09.l).
- [ ] **FR-022.** A duplicate invite shows the `INVITATION_PENDING` message with "Resend invitation" (8.09.e). Revoking an invitation has its confirmation (8.09.f).
- [ ] **US7 #2.** The revoke dialog says the person loses the role on their next request and lists the invitations they can no longer send, or says there are none (8.09.g, j).
- [ ] **US7 #2.** After the revoke, Casey is gone from the table (8.09.h) and lands in board mode with no company entry, still a Board Member (8.09.k).
- [ ] **US7 #3.** Robin, the only one, revoking their own role gets the `LAST_COMPANY_ADMIN` self message and "Close" (8.09.i).
- [ ] **US7 #4.** Casey's lost race shows the `LAST_COMPANY_ADMIN` race message naming Robin Hale, and Robin remains (8.09.h, j).
- [ ] **US7 #5.** Jordan Blake has no company entry and is refused with "You don't have access to that page." (8.09.m).
- [ ] **FR-037.** The rule helper "The company always needs at least one Company Administrator. The last one can't be revoked." is on every page artboard.
- [ ] **US7 #11, FR-039.** "Granted by: Setup step" appears for Robin; no log or audit view is drawn.
- [ ] **Hidden data.** No name is shown for ops@example.com or any pending invitee.
- [ ] **Frames.** Company area with "Company administrators" current and no violet banner; 8.09.k in board mode with the banner unchanged.
- [ ] **Design choices.** D1–D5 and Q1 are on post-its beside the named artboards.
- [ ] **Words and data.** Status chips, "(you)", "Setup step" and refusals are words, not colour. Only conventions §4 people, emails and dates appear; no backup@example.com.
- [ ] **Canvas.** Labels and ids match §1 exactly (s8-09a to s8-09m). Sections 1–7 render as before.
