# 12 — Emails

This brief is for the eight transactional emails spec 030 sends: role, resident and Company Administrator invitations; the "community added" notice; the archive notice; the removal and "autopay ended" notices; and the recovery-mode notice. §1 is the prompt to paste into Claude Design. The other sections hold the details the prompt points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file. None of these emails exist yet. Until they are built from this design, the app sends the same words as plain text (spec Assumptions, Emails). The pages the invitation links open are brief 10.

## 1. Prompt for Claude Design

```text
Project "HOA Management CRM", canvas "NekoHOA Wireframes.html". Attached: spec.md (spec 030, the authority), 00-conventions.md (rules, dataset, vocabulary) and this brief, 12-emails.md. spec.md wins, then the conventions. Brief §4 has each email's data and what it must leave out, §5 the exact copy, §6 where each button goes.

Add email artboards to section "8 · Company administration" (after section 7; conventions §3). Ids s8-12a to s8-12s. Each is 640 wide × content height; 8.12.s is 390 wide. Put them in a new file, wf-emails.jsx, with one shared email frame. Labels exactly:
8.12.a Email · role invitation · Community Manager
8.12.b Email · resident invitation
8.12.c Email · Company Administrator invitation
8.12.d Email · community added · waiting to be marked Active
8.12.e Email · no longer managed · autopay ended
8.12.f Email · removed from home · autopay ended
8.12.g Email · autopay ended · other resident
8.12.h Email · recovery mode · invitation sent
8.12.i Email · role invitation · Board Member with term end
8.12.j Email · role invitation · Accountant
8.12.k Email · role invitation · has an account · Board Member
8.12.l Email · resident invitation · has an account
8.12.m Email · resident invitation · resent
8.12.n Email · Company Administrator invitation · setup step
8.12.o Email · no longer managed
8.12.p Email · removed from home
8.12.q Email · recovery mode · role granted
8.12.r Email · resident invitation · plain text
8.12.s Email · resident invitation · phone
Place them by template (brief §4.3): a i j k · b l m r s · c n · d · e o · f p · g · h q.

Every email, top to bottom: an "Inbox preview" strip (From, To, Subject, Preheader) outside the email; a header with the NekoHOA wordmark from wf-auth.jsx and a context line; greeting; the lead sentence as the heading; label and value rows in a wf-card, like brief 10's invitation card; short paragraphs; one wf-btn primary with the link as text under it; a wf-sub footer. One column, no images, no violet banner. 8.12.r is the plain-text form, in a monospace block.

Also reuse wf-divider, wf-ph (every link and unknown value) and .wf-note. Put brief §4.2's design choices and questions on DCPostIts beside the named artboards.

Fidelity: lofi, section 7's visual language. Copy, order, and what is shown or left out are binding; colour, type and spacing are placeholders.

Don't draw: emails this brief doesn't list; an invitee's name; other residents' names; who removed a resident; any sender's email address; amounts, balances or payment methods; a token in a URL; images, social links or an unsubscribe link; error codes; HTML code.

When done, open every artboard and check it renders without console errors, shows its stated variant, has no sideways scroll at 390, and matches brief §4 and §5 word for word. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

- **Invitees** (Pat Rivera, new@, books@, Kim Tran, ops@, backup@, Ann Brooks, Sam Ortiz, max@) reach brief 10's pages from these emails, often on a phone. Many have no account. The email says what is offered, who sent it, when the link expires, and that only this address can accept (FR-022, FR-022a, FR-024, FR-037).
- **Company Administrators** (Robin Hale, Casey Novak) are told when a Community Manager adds a community, because only they can mark it Active (FR-008). They are told when recovery mode is used, so they notice misuse (FR-038).
- **Residents** are told when the company stops managing their community (FR-013), when they are removed from a home, and when a home's autopay ends (FR-024c). Autopay must never stop without notice.
- **Senders.** Company Administrators and Community Managers send the same invitation emails. Only the name after "Sent by" changes. No email shows the sender's role or email address.
- **Today's emails.** Today's transactional emails are plain text, sent from "NekoHOA". Spec 027's architectural review emails put the community name first, then "Hello [first name],", one lead sentence, "Label: value" lines and "Action: link". These designs keep that order and add a header, one button and a footer.

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| FR-008, US1 #4 | Every Company Administrator (Robin, Casey) is emailed that Keystone Ridge was added by Jordan Blake and is waiting to be marked Active, with "Open Community settings" linking to its Community settings page. | 8.12.d |
| FR-013, US3 #7 | Each resident linked to Old Mill Commons is emailed once: Juniper Row Community Management no longer manages it; the home stays, marked "No longer managed"; its payment history stays readable. The "Autopay has ended" block appears only where that resident's home had autopay. | 8.12.e, o |
| FR-022, validity | Each invitation shows "Expires" with date, time and zone: the community's time zone, or the company default for Company Administrator invitations. It says the link works once, for 72 hours. | a, b, c, i–n, s |
| FR-022, bound to the email | "The link works once, and only for [invited email]." | a, b, c, i–n, s |
| FR-022, terms | A role with a term: "Board Member, until Dec 31, 2027" and "The role starts when you accept and ends Dec 31, 2027." | 8.12.i |
| FR-022, resend | The resender is the sender. The earlier link no longer works. | 8.12.m |
| FR-022a | No claim code. The invitee creates an account with a name and a password. The link confirms the email. | a, b, c, i, j, n, s |
| FR-024 | The resident invitation names the home, the community, the sender and the expiry. It names no other resident. | b, r, s |
| FR-024 (FR-024a) | An email that has an account is asked to sign in and accept. Nothing changes until they accept. | 8.12.l |
| FR-024c, US8 #6 | Jane is emailed that she was removed from #29 and that its autopay ended. Sam, the other resident, is emailed that autopay ended, without naming Jane. A removal that cancels no autopay sends only the removal email. | f, g, p |
| FR-037, US7 #1 | ops@'s Company Administrator invitation: 72 hours, single use, bound to that email, company default time zone. | 8.12.c |
| FR-038, US7 #6 | The setup step's invitation, when the email has no account: "Sent by: Setup step". | 8.12.n |
| FR-038, US7 #8 | Robin and Casey are each emailed that recovery mode invited, or granted the role to, backup@example.com, with the time and what to do. | h, q |
| Assumptions (Emails) | All eight templates. The plain-text form used until these designs are built. | a–h, r |

## 4. Artboards

### 4.1 Frame and data

- **Size.** 640 wide, as tall as the content. 8.12.s is 390 wide.
- **Inbox preview strip** (above the email, outside its border, labelled "Inbox preview"): From "NekoHOA" (D1), To, Subject, Preheader. The preheader is the inbox's preview line; it is not shown in the body.
- **Header.** The `wf-logo-mark` and "NekoHOA" wordmark from `wf-auth.jsx`, then a context line: the community name for community emails, "Juniper Row Community Management" for company emails (templates 3, 4 and 8).
- **Body order.** Greeting (D2) · lead sentence as the heading · details block (label and value rows) · paragraphs, with "Autopay has ended" as a sub-heading where it applies · one primary button · "If the button doesn't work, copy this link into your browser:" and the link as a `wf-ph` · the "ignore" line (invitations only) · `wf-divider` · footer.
- **Footer.** "Sent by NekoHOA for Juniper Row Community Management." and one line saying why the reader got it (§5). No unsubscribe link (D8).
- **Snapshots.** Each email is drawn as received at its send time, given per artboard, not at the conventions' "now".
- **Time zone.** Every community and the company default use America/New_York, so every time reads EDT (FR-022).
- **States.** An email has no loading, empty, error or refusal state. Its states are the variants below. What a reader sees when a link no longer works is brief 10 (8.10.s, u, w).
- **Leave out**, in every email: anything §4.3 lists under "Leave out", every invitation token, and every value the dataset doesn't give (use `wf-ph`).
- **Dataset note.** Brief 09's page shows no backup@example.com. Treat 8.12.h, n and q as a standalone example on Oct 12, 7:05 AM EDT.

### 4.2 Design choices and open questions (each on a `DCPostIt`)

| # | Question | Drawn | Other option | Post-it beside |
| --- | --- | --- | --- | --- |
| D1 | From name and header | From "NekoHOA", as every email today. Context line under the wordmark. | From "Juniper Row Community Management". | 8.12.a |
| D2 | Greeting | Invitations: "Hello," (the invitee is known by email only, FR-018). Everyone else: "Hello [first name],", as today's architectural review emails. | "Hello," everywhere. | 8.12.a |
| D3 | Do role and Company Administrator invitations change when the email has an account? The spec says so only for residents (FR-024a). | Yes, like residents: "Sign in to accept" (8.12.k). | One body for everyone that names both paths. Then 8.12.k is dropped. | 8.12.k |
| D4 | Does a resent invitation say so? | Yes, one line: the earlier link no longer works. | Same as the first email. | 8.12.m |
| D5 | Sender of an invitation from the setup step | "Sent by: Setup step", the words brief 09's "Granted by" column uses. | No "Sent by" row. | 8.12.n |
| D6 | Does the removal email say who removed the resident? | No. It says who to contact. | Name the person who removed them. | 8.12.f |
| D7 | Does the "autopay ended" email say why? | Yes, without a name: "a resident was removed from this home". | No reason. | 8.12.g |
| D8 | Unsubscribe link | None. These are service emails about the reader's account, roles or homes. | A link to notification settings once that spec lands. | 8.12.e |
| D9 | Time zone of company emails | The company default time zone, as FR-022 sets for Company Administrator invitations. | — | 8.12.h |
| D10 | Button of the archive email | "View payment history" (the home's ledger). | "Sign in". | 8.12.e |
| D11 | A signed-out reader presses a notice's button | Sign-in first, then the button's page. | Sign-in, then the usual landing (FR-036a). | 8.12.d |

**Open question Q1** (beside 8.12.e): the spec doesn't say whom residents of an archived community should contact about dues or payments. Not drawn.
**Open question Q2** (beside 8.12.q): "every current Company Administrator" (FR-038). Drawn: those who held the role before the run (Robin, Casey). Does the person just granted the role also get the notice?

### 4.3 Templates

**Template 1 — Role invitation** (FR-021, FR-022, FR-022a). Sent when a Company Administrator or Community Manager invites by email, whatever the community's status, and on resend. Leave out: the invitee's name, the community's status, other managers or members, the sender's role and email.

- **8.12.a Email · role invitation · Community Manager.** To pat@example.com (no account). Keystone Park. Sent by Robin Hale, Oct 11, 2026, 9:15 AM EDT. Expires Oct 14, 2026, 9:15 AM EDT. No term: "It has no end date." Button "Accept invitation". `.wf-note`: "Keystone Park is Onboarding. Role invitations are still emailed at once (FR-022)." Variant: default, no account. Post-its D1, D2.
- **8.12.i Email · role invitation · Board Member with term end.** To new@example.com. Sakura Heights. Sent by Jordan Blake, Oct 11, 2026, 2:05 PM EDT. Expires Oct 14, 2026, 2:05 PM EDT. "You're invited as: Board Member, until Dec 31, 2027" and the term paragraph. Variant: term end.
- **8.12.j Email · role invitation · Accountant.** To books@example.com, as sent Oct 6, 2026, 9:40 AM EDT, by Jordan Blake. Sakura Heights. Expires Oct 9, 2026, 9:40 AM EDT. `.wf-note`: "Revoked Oct 7. Its link now opens 8.10.u." Variant: Accountant.
- **8.12.k Email · role invitation · has an account · Board Member.** To ann@example.com (has an account). Briar Hollow. Sent by Robin Hale, Oct 11, 2026, 3:30 PM EDT. Expires Oct 14, 2026, 3:30 PM EDT. The "has an account" paragraph replaces "To accept, create an account…". Button "Sign in to accept". Variant: has an account. Label the artboard "Design choice"; post-it D3.

**Template 2 — Resident invitation** (FR-022, FR-022a, FR-024, FR-024a, FR-024b). Sent when a Company Administrator or Community Manager invites in an Active community; to every scheduled invitation whose sender still has authority when a community is marked Active; and on resend. Never while the community is Onboarding. Leave out: the invitee's name, other residents, the owner of record, balance and autopay.

- **8.12.b Email · resident invitation.** To kim@example.com (no account). 711 Keystone Park Dr #29, Sakura Heights. Sent by Jordan Blake, Oct 10, 2026, 4:40 PM EDT. Expires Oct 13, 2026, 4:40 PM EDT. Button "Accept invitation". Notes: "#29 already has two residents. The email names neither." · "lena@example.com's email, sent when Keystone Ridge is marked Active, is this template: 2 Ridgeview Ct · Keystone Ridge · Sent by Jordan Blake · Expires Oct 15, 2026, 10:00 AM EDT (FR-024b)." · "In another time zone the zone changes: Oct 13, 2026, 3:40 PM CDT for America/Chicago." Variant: default, no account.
- **8.12.l Email · resident invitation · has an account.** To sam@example.com (has an account), as sent before he accepted on Oct 9, 2026. #29, Sakura Heights. "Sent by" and "Expires" values are `wf-ph`; the dataset doesn't give them. The "has an account" paragraph, with "Your other homes don't change." Button "Sign in to accept". Variant: has an account (US8 #3).
- **8.12.m Email · resident invitation · resent.** To max@example.com. 715 Keystone Park Dr #31, Sakura Heights. Resent by Robin Hale, Oct 12, 2026, 10:00 AM EDT, so "Sent by: Robin Hale". Expires Oct 15, 2026, 10:00 AM EDT. The resent line sits above the button. Variant: resent. Label "Design choice"; post-it D4.
- **8.12.r Email · resident invitation · plain text.** 8.12.b as plain text in a monospace block, in §5.4's form. Note: "Until the designed emails are built, every email in this brief is sent like this. Afterwards, this stays as each email's plain-text part." Variant: plain text.
- **8.12.s Email · resident invitation · phone.** 8.12.b at 390 wide. Labels sit above values; the button is full width, about 44 px tall. Variant: phone.

**Template 3 — Company Administrator invitation** (FR-022, FR-037, FR-038). Sent when a Company Administrator grants the role, and when the setup step targets an email with no account (first run or recovery mode). Times use the company default time zone. Leave out: other Company Administrators.

- **8.12.c Email · Company Administrator invitation.** To ops@example.com. Sent by Robin Hale, Oct 12, 2026, 8:30 AM EDT. Expires Oct 15, 2026, 8:30 AM EDT. `.wf-note`: "Company default time zone (FR-022)." Variant: default (US7 #1).
- **8.12.n Email · Company Administrator invitation · setup step.** To backup@example.com, from recovery mode, Oct 12, 2026, 7:05 AM EDT. "Sent by: Setup step". Expires Oct 15, 2026, 7:05 AM EDT. The setup-step lead (§5.2). `.wf-note`: "The first run of the setup step sends this same email when the target email has no account (US7 #6)." Variant: setup step. Post-it D5.

**Template 4 — Community added** (FR-008, US1 #4). Sent once to each current Company Administrator when a Community Manager adds a community. Not sent when a Company Administrator adds one (Keystone Park).

- **8.12.d Email · community added · waiting to be marked Active.** To Robin Hale (owner@example.com), Oct 9, 2026. Casey Novak gets the same email. Keystone Ridge, Keystone Ridge HOA, Inc., Onboarding, added by Jordan Blake, Jordan Blake as Community Manager with no end date, management starts Dec 1, 2026. No parent, so no parent row. Button "Open Community settings". Leave out: the names of other Company Administrators, county and formation date (on the settings page). Variant: default.

**Template 5 — No longer managed** (FR-013, US3 #7). Sent once to each resident linked to the community's properties, in the archive action. One email per resident: a resident with two homes there gets one email that lists both. Leave out: who archived it, other residents, counts, memberships, invitations, amounts.

- **8.12.e Email · no longer managed · autopay ended.** To Sam Ortiz (sam@example.com). Old Mill Commons, archived Sep 30, 2026. "Your home: 16 Old Mill Ln". It had autopay, so the "Autopay has ended" block shows. "Your other homes don't change." shows because Sam has other homes. Button "View payment history". Variant: autopay ended. Post-its D8, D10, Q1.
- **8.12.o Email · no longer managed.** The same email to a resident whose home had no autopay. Name and address are `wf-ph` (the dataset names no such resident). No autopay block; no "other homes" line. Variant: no autopay.

**Template 6 — Removed from home** (FR-024c, US8 #6). Sent to the removed resident. Leave out: who removed them (D6), other residents, the owner of record, the balance.

- **8.12.f Email · removed from home · autopay ended.** To Jane Alvarez (jane@example.com), Oct 12, 2026. 711 Keystone Park Dr #29, Sakura Heights. Jane enrolled #29's autopay, so the "Autopay has ended" block shows. Button "Sign in". `.wf-note`: "Jane is still a Board Member and lands in board mode (US8 #8)." Variant: autopay ended. Post-it D6.
- **8.12.p Email · removed from home.** To Sam Ortiz, Oct 12, 2026 (the other timeline: Sam is removed, not Jane). No autopay block: Jane enrolled it, so it stays. `.wf-note`: "No 'autopay ended' email goes to Jane (US8 #7)." Variant: no autopay change.

**Template 7 — Autopay ended** (FR-024c). Sent to every other resident of the property when a removal cancels its autopay. Leave out: the removed resident's name (D7), amounts, the payment method.

- **8.12.g Email · autopay ended · other resident.** To Sam Ortiz, Oct 12, 2026, after Jane's removal. #29, Sakura Heights. Button "Set up autopay". Variant: default. Post-it D7.

**Template 8 — Recovery-mode notice** (FR-038, US7 #8). Sent once to each current Company Administrator when recovery mode invites or grants. Times use the company default time zone. Leave out: who ran the command (the log actor is "setup"), other Company Administrators' names.

- **8.12.h Email · recovery mode · invitation sent.** To Robin Hale, Oct 12, 2026, 7:05 AM EDT. Casey Novak gets the same email; ops@example.com (pending) does not. backup@example.com had no account, so an invitation was sent (8.12.n). Button "Open Company administrators". Variant: invited. Post-it D9.
- **8.12.q Email · recovery mode · role granted.** The same, if backup@example.com had an account: the role was granted. Variant: granted. Post-it Q2.

## 5. Copy

Quote these exactly. Values in brackets change per email. Each block lists its parts in body order.

### 5.1 Shared

| Element | Text |
| --- | --- |
| Inbox labels | "Inbox preview" · "From" · "To" · "Subject" · "Preheader" |
| From | "NekoHOA" |
| Greeting | Invitations: "Hello," · Others: "Hello [Robin]," |
| Link fallback | "If the button doesn't work, copy this link into your browser:" then the link (`wf-ph`: "[invitation link]" or "[link]") |
| Footer line 1 | "Sent by NekoHOA for Juniper Row Community Management." |
| Autopay sub-heading | "Autopay has ended" |
| Dates | "Oct 14, 2026, 9:15 AM EDT" for times; "Oct 9, 2026" for days. Never a countdown alone. |

### 5.2 Invitations (templates 1–3)

| Element | Text |
| --- | --- |
| Subject, role | "You're invited to be a [Community Manager] of [Keystone Park]" · Accountant: "You're invited to be an Accountant of Sakura Heights" |
| Subject, resident | "You're invited to be a resident of [711 Keystone Park Dr #29]" |
| Subject, Company Administrator | "You're invited to be a Company Administrator of Juniper Row Community Management" |
| Preheader | "[Robin Hale] invited you. The link expires [Oct 14, 2026, 9:15 AM EDT]." · Has an account: "Sign in to accept. The link expires [date and time]." · Setup step: "The link expires [Oct 15, 2026, 7:05 AM EDT]." |
| Lead, role | "[Robin Hale] invited you to be a [Community Manager] of [Keystone Park]." |
| Lead, resident | "[Jordan Blake] invited you to be a resident of [711 Keystone Park Dr #29] in [Sakura Heights]." |
| Lead, Company Administrator | "[Robin Hale] invited you to be a Company Administrator of Juniper Row Community Management." |
| Lead, setup step | "You're invited to be a Company Administrator of Juniper Row Community Management." Then: "The setup step sent this invitation. Whoever deploys NekoHOA runs that step." |
| Details rows | "You're invited as: [Community Manager / Board Member, until Dec 31, 2027 / Accountant / Resident of this home / Company Administrator]" · "Community: [Keystone Park]" (resident: "Home: [711 Keystone Park Dr #29]" then "Community: [Sakura Heights]"; Company Administrator: "Company: Juniper Row Community Management") · "Sent by: [Robin Hale / Setup step]" · "For: [pat@example.com]" · "Expires: [Oct 14, 2026, 9:15 AM EDT]" |
| What it gives, role | No term: "The role starts when you accept. It has no end date." · Term: "The role starts when you accept and ends [Dec 31, 2027]." |
| What it gives, resident | "After you accept, you'll see this home when you sign in to NekoHOA." |
| What it gives, Company Administrator | "After you accept, you can open the company area for Juniper Row Community Management when you sign in." |
| How to accept, no account | "To accept, create an account with your name and a password. You don't need a claim code. This link confirms your email address." |
| How to accept, has an account | "You already have a NekoHOA account for [sam@example.com]. Sign in with it, then accept. Nothing changes until you accept." Resident adds: "Your other homes don't change." |
| Validity | "The link works once, and only for [pat@example.com]. It expires [Oct 14, 2026, 9:15 AM EDT], 72 hours after it was sent." |
| Resent (D4) | "This invitation replaces an earlier one. The link in the earlier email no longer works." |
| Button | "Accept invitation" · Has an account: "Sign in to accept" |
| Ignore line | "If you weren't expecting this, you can ignore this email. Nothing happens unless you accept." |
| Footer line 2 | "You're getting this because [Robin Hale] invited [pat@example.com]." · Setup step: "You're getting this because the setup step invited [backup@example.com]." |

### 5.3 Notices (templates 4–8)

**Template 4 — Community added (8.12.d)**

| Element | Text |
| --- | --- |
| Subject | "[Keystone Ridge] was added and is waiting to be marked Active" |
| Preheader | "[Jordan Blake] added it on [Oct 9, 2026]. Only a Company Administrator can mark it Active." |
| Lead | "[Jordan Blake] added a new community, [Keystone Ridge]. It's waiting for a Company Administrator to mark it Active." |
| Details rows | "Community name: Keystone Ridge" · "Legal name: Keystone Ridge HOA, Inc." · "Status: Onboarding" · "Added by: Jordan Blake, Oct 9, 2026" · "Community Manager: Jordan Blake, no end date" · "Management starts: Dec 1, 2026" · Only with a parent: "Sub-association of: [name]" |
| Body | "Residents can't see [Keystone Ridge] until it's marked Active. Resident invitations are sent then." · "Review it and mark it Active from its Community settings page." |
| Button | "Open Community settings" |
| Footer line 2 | "You're getting this because you're a Company Administrator of Juniper Row Community Management. Every Company Administrator gets this email." |

**Template 5 — No longer managed (8.12.e, o)**

| Element | Text |
| --- | --- |
| Subject | "[Old Mill Commons] is no longer managed by Juniper Row Community Management" |
| Preheader | "Your home stays in your account. You can still view its payment history." |
| Lead | "As of [Sep 30, 2026], Juniper Row Community Management no longer manages [Old Mill Commons]." |
| Details rows | "Your home: [16 Old Mill Ln]" (one row per home in this community) |
| Body | "Your home stays in your NekoHOA account, marked “No longer managed”. You can still view its ledger, statements, receipts and transactions." · "Everything else for this home, including payments and autopay, is no longer available in NekoHOA." |
| Autopay block (only where autopay was cancelled) | "Autopay has ended" · "Autopay for [16 Old Mill Ln] was cancelled on [Sep 30, 2026]. No more automatic payments will be made for it. A payment that had already started will finish normally." |
| Other homes (only if the resident has one) | "Your other homes don't change." |
| Button | "View payment history" |
| Footer line 2 | "You're getting this because you're a resident of a home in [Old Mill Commons]." |

**Template 6 — Removed from home (8.12.f, p)**

| Element | Text |
| --- | --- |
| Subject | "You were removed from [711 Keystone Park Dr #29]" |
| Preheader | "You can no longer see this home in NekoHOA. Autopay for it has ended." · No autopay change: "You can no longer see this home in NekoHOA. Your account stays open." |
| Lead | "You were removed as a resident of [711 Keystone Park Dr #29] in [Sakura Heights] on [Oct 12, 2026]." |
| Body | "You can no longer see this home in NekoHOA." |
| Autopay block (only where autopay was cancelled) | "Autopay has ended" · "Because you were removed, autopay for [711 Keystone Park Dr #29] was cancelled. No more automatic payments will be made for it. A payment that had already started will finish normally." |
| What doesn't change | "Your NekoHOA account stays open. Your other homes and roles don't change, and you can still sign in." |
| Contact | "If you think this is a mistake, contact the Community Manager of [Sakura Heights]." |
| Button | "Sign in" |
| Footer line 2 | "You're getting this because you were a resident of [711 Keystone Park Dr #29]." |

**Template 7 — Autopay ended (8.12.g)**

| Element | Text |
| --- | --- |
| Subject | "Autopay ended for [711 Keystone Park Dr #29]" |
| Preheader | "No more automatic payments will be made for this home. You can set up autopay again." |
| Lead | "Autopay for [711 Keystone Park Dr #29] in [Sakura Heights] was cancelled on [Oct 12, 2026]." |
| Body | "No more automatic payments will be made for this home. A payment that had already started will finish normally." · D7: "It was cancelled because a resident was removed from this home." · "Your access to this home doesn't change, and neither does its balance." · "To keep paying automatically, set up autopay again." |
| Button | "Set up autopay" |
| Footer line 2 | "You're getting this because you're a resident of [711 Keystone Park Dr #29]." |

**Template 8 — Recovery-mode notice (8.12.h, q)**

| Element | Text |
| --- | --- |
| Subject | Invited: "Recovery mode was used to invite a Company Administrator" · Granted: "Recovery mode was used to add a Company Administrator" |
| Preheader | "[backup@example.com] was [invited / made a Company Administrator] on [Oct 12, 2026, 7:05 AM EDT]. Check that you expected this." |
| Lead | Invited: "Recovery mode was used to invite [backup@example.com] to be a Company Administrator of Juniper Row Community Management." · Granted: "Recovery mode was used to make [backup@example.com] a Company Administrator of Juniper Row Community Management." |
| Details rows | "Email: [backup@example.com]" · "Result: Invitation sent. It expires [Oct 15, 2026, 7:05 AM EDT]." or "Result: Role granted. This email already had an account." · "When: [Oct 12, 2026, 7:05 AM EDT]" · "Run by: The setup step, in recovery mode" |
| Body | "Recovery mode is run from the command line by whoever deploys NekoHOA. It's for when every Company Administrator is unavailable or compromised." · "If you expected this, you don't need to do anything. If you didn't, open Company administrators to check who holds the role. You can revoke the [invitation / role] there." · "Every current Company Administrator gets this email." |
| Button | "Open Company administrators" |
| Footer line 2 | "You're getting this because you're a Company Administrator of Juniper Row Community Management." |

### 5.4 Plain text (8.12.r)

The same words in the same order. The header is the context line. Each details row is "Label: value". The button becomes "[Button label]: [link]", so the link-fallback line is dropped. The divider is a blank line.

```text
Sakura Heights

Hello,

Jordan Blake invited you to be a resident of 711 Keystone Park Dr #29 in Sakura Heights.

You're invited as: Resident of this home
Home: 711 Keystone Park Dr #29
Community: Sakura Heights
Sent by: Jordan Blake
For: kim@example.com
Expires: Oct 13, 2026, 4:40 PM EDT

After you accept, you'll see this home when you sign in to NekoHOA.

To accept, create an account with your name and a password. You don't need a claim code. This link confirms your email address.

The link works once, and only for kim@example.com. It expires Oct 13, 2026, 4:40 PM EDT, 72 hours after it was sent.

Accept invitation: [invitation link]

If you weren't expecting this, you can ignore this email. Nothing happens unless you accept.

Sent by NekoHOA for Juniper Row Community Management.
You're getting this because Jordan Blake invited kim@example.com.
```

## 6. Interactions

- **When each email is sent.** Once, at the moment of the action, as §4.3 lists per template. A scheduled resident invitation is emailed only when its community is marked Active, and its 72 hours start then (FR-024b). Scheduled invitations revoked by an archive are never emailed. Communities moved from Inactive to Archived by the migration send no archive email (spec Database/runtime). The per-recipient cap on invitation emails is shown to the sender (briefs 05, 06, 09), not in any email (FR-041 c).
- **Invitation buttons** open the invitation link, which leads to brief 10. A valid link while signed out shows the landing (8.10.a–e). A valid link while signed in with the invited email goes to "Accept this invitation?" (8.10.n). An expired link shows 8.10.s. A revoked, used or replaced link, or one whose sender lost authority, shows 8.10.u. A different signed-in account shows 8.10.w. The email is the same in every case.
- **An old invitation email after a resend** still has its old link, which shows 8.10.u. 8.12.m's line tells the reader to use the newest email.
- **"Open Community settings"** (8.12.d) opens Keystone Ridge's Community settings, Profile section, in the company area (brief 04), where Mark Active is offered. Someone who is no longer a Company Administrator gets "You don't have access to that page." on their landing page.
- **"View payment history"** (8.12.e, o) opens the archived home's ledger (brief 11).
- **"Sign in"** (8.12.f, p) opens sign-in. The person then lands per FR-036a: Jane in board mode for Sakura Heights; Sam on 16 Old Mill Ln.
- **"Set up autopay"** (8.12.g) opens #29's autopay page (section 3).
- **"Open Company administrators"** (8.12.h, q) opens the Company administrators page (brief 09), where the role or the invitation can be revoked. Someone who is no longer a Company Administrator gets "You don't have access to that page."
- **Signed out.** Every non-invitation button asks the reader to sign in first, then opens the button's page (D11).
- **Nothing is logged in the email.** Sends, revokes and cancellations are logged by the app (FR-023, FR-024e, FR-039, FR-040). No email shows a log entry or an ID.

## 7. Accessibility

Conventions §8 applies, read for email:

- **Words carry the news.** The subject and the heading each say what happened. The preheader adds to them; nothing the reader must act on lives only there.
- **Real text.** No text in images. The wordmark has the text alternative "NekoHOA".
- **Reading order.** One column, so screen readers, phones and the plain-text part read in the same order as the design.
- **Headings.** One main heading (the lead). "Autopay has ended" is a sub-heading, not only a coloured box.
- **Details rows** read as "Label: value" when read aloud or linearised.
- **Button.** A real link whose text says the action ("Accept invitation", never "Click here"). About 44 px tall; full width at phone width. The link is also given as text below it.
- **Contrast.** 4.5:1 for all text, the button label and the footer.
- **Phone.** Reflows at 320 CSS px with no sideways scrolling; works at 200% zoom (8.12.s).
- **Time.** Every expiry is a full date, time and zone, never only a countdown.
- **Plain text.** Every email has a plain-text part with the same words in the same order (8.12.r).

## 8. Out of scope for this design

- Emails the spec doesn't call for: invitation accepted, revoked or expiring; a direct appointment; a membership ended, downgraded or replaced; a community marked Active or restored; a Company Administrator role revoked; profile or settings changes; a welcome email after an account is created; the setup step granting the role to an existing account.
- Notifying the owner of record when a resident joins by invitation (spec Out of Scope).
- Existing emails: claim codes (specs 016, 017), architectural review (spec 027), payment alerts and notices (spec 006). SMS.
- The pages the links open (briefs 04, 09, 10, 11, section 3).
- Notification settings and unsubscribe handling (Notification Settings spec).
- HTML, CSS and email-client code; sender domain and reply-to settings.

## 9. Design review checklist

- [ ] **Assumptions (Emails).** Eight templates are drawn (a–h, with variants i–s), each with subject, preheader, body, one primary button and footer.
- [ ] **FR-022 validity.** Every invitation (a, b, c, i–n, s) shows "Expires" as date, time and zone, and "works once, and only for [email]… 72 hours after it was sent". ops@ and backup@ use the company default time zone.
- [ ] **FR-022 terms.** 8.12.i shows "Board Member, until Dec 31, 2027" and "ends Dec 31, 2027".
- [ ] **FR-022 resend.** 8.12.m shows "Sent by: Robin Hale" and the earlier-link line (D4).
- [ ] **FR-022a.** No-account invitations say "You don't need a claim code." and "This link confirms your email address."
- [ ] **FR-024.** 8.12.b names #29, Sakura Heights, Jordan Blake and the expiry, and no other resident.
- [ ] **FR-024a, US8 #3.** 8.12.l asks Sam to sign in and accept, and says nothing changes until he accepts.
- [ ] **US7 #1, FR-037.** 8.12.c offers the Company Administrator role to ops@example.com, sent by Robin Hale, expiring Oct 15, 2026, 8:30 AM EDT.
- [ ] **US7 #6, FR-038.** 8.12.n reads "Sent by: Setup step".
- [ ] **US7 #8, FR-038.** 8.12.h and q go to Robin (Casey gets the same), name backup@example.com, the time and the result, and link to Company administrators.
- [ ] **US1 #4, FR-008.** 8.12.d says Jordan Blake added Keystone Ridge, it is Onboarding and waiting to be marked Active, and links to its Community settings page.
- [ ] **US3 #7, FR-013.** 8.12.e and o: no longer managed, "No longer managed" home, payment history readable; "Autopay has ended" only on e.
- [ ] **US8 #6, FR-024c.** 8.12.f tells Jane she was removed and autopay ended, and that her account, other homes and roles don't change. 8.12.g tells Sam autopay ended without naming Jane. 8.12.p has no autopay block.
- [ ] **Hidden data.** No invitee name, other resident, remover, sender email, amount, balance, payment method, owner of record or token appears anywhere.
- [ ] **Plain text.** 8.12.r has 8.12.b's words in §5.4's form.
- [ ] **Phone.** 8.12.s is 390 wide, one column, full-width button, no sideways scroll.
- [ ] **Design choices.** D1–D11, Q1 and Q2 are on post-its beside the named artboards; 8.12.k and m are labelled "Design choice".
- [ ] **Data.** Only conventions §4 people, emails, communities and times; unknown values are `wf-ph`.
- [ ] **Canvas.** Labels and ids match §1 exactly (s8-12a to s8-12s). Sections 1–7 render as before.
