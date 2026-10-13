# 11 — Sign-in landing and resident-side states

This brief is for what people see because of a community's status or a missing home. §1 is the prompt to paste into Claude Design; §2–§9 hold the details it points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file. Brief 01 owns the landing-order flow (8.01.r) and the company and board landings; this brief does not repeat them.

## 1. Prompt for Claude Design

```text
Project "HOA Management CRM", canvas "NekoHOA Wireframes.html". Add to section "8 · Company administration" (DCSection id "company-admin"; create it after section 7 if missing).

Attached: spec.md (the authority), 00-conventions.md (rules, §4 dataset, §6 refusal copy) and 11-sign-in-and-resident-states.md (this brief: §4 artboards and states, §5 exact copy, §6 behaviour). Follow the conventions exactly.

Put the artboards in wf-company-resident.jsx; shared section-8 data goes in wf-company.jsx. Ids s8-11a to s8-11k. Labels exactly:
8.11.a No home linked to this account · Dana Fox
8.11.b Your community isn't live yet · Resident · Ann Brooks
8.11.c Home switcher · open · Resident · Sam Ortiz
8.11.d Statement · No longer managed · Resident · Sam Ortiz
8.11.e Dashboard · No longer managed · refused · Resident · Sam Ortiz
8.11.f Auto-pay · No longer managed · refused · Resident · Sam Ortiz
8.11.g One-time payment · refused after archive · old tab · Resident · Sam Ortiz
8.11.h Moved to another home · after removal · Resident · Sam Ortiz
8.11.i Moved to board mode · after removal · Board Member · Jane Alvarez
8.11.j Sign-in landing · board mode · no home · Board Member · Jane Alvarez
8.11.k Create your login · claim code · community isn't live yet

Frames. 8.11.c to 8.11.h use WFShellSide, the resident visual language of sections 2–6, 1280 wide, with no violet banner. Pass the §4 data into the strip and avatar; don't show "Nicholas", "NB", 714 Keystone Park Dr or the account number. 8.11.a and 8.11.b use a signed-in top bar only (logo and avatar), 1280 wide. 8.11.i and 8.11.j use WFShellBoard with boardNav(), 1360 wide; the banner stays dark ink on violet. 8.11.k is the "Create your login" registration step at 1100 × 780.

Reuse WFShellSide, WFShellBoard, boardNav(), Statement, the Auto-pay and one-time pages of sections 3 and 3b, wf-card, wf-btn, wf-pill, wf-ph, wf-note and DCPostIt. Add props with defaults so sections 1–7 render as before.

Lofi, the visual language of sections 2–7. Layout, copy, states and who sees what are binding; colour and spacing are placeholders. "No longer managed" and every notice are words, never colour alone.

Put design choices D1–D9 (§4.1) on DCPostIts beside the artboards they name.

Don't draw: any Onboarding home (9 Briar Hollow Ct) in a resident's list; the words "Onboarding", "Active" or "Archived" on resident pages; the removed home's address in the "no longer have access" notice; any Pay, autopay or "Make a payment" control on a "No longer managed" home; the community name or address on the "isn't live yet" page; a claim-code field on the "No home linked" page; a promise of an email when a community goes live; error codes; emails; phone layouts.

When done, open the canvas and check that each artboard renders in its stated state without console errors, copy matches §5 word for word, sections 1–7 are unchanged and ids are unique. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

- **People with no home and no role** (Dana Fox) can still sign in (FR-036a). They need to know why there is nothing to see and what to do: use an invitation link or contact their community manager (US9 #3). Morgan Ellis (brief 03) and Robin Hale after revoking Robin's own role (brief 09) land on the same page.
- **Residents whose only homes are in Onboarding communities** (Ann Brooks) sign in to a plain "isn't live yet" page, not an error (US9 #5). Onboarding homes are hidden from residents (FR-011, FR-012a).
- **Residents with homes in several communities** (Sam Ortiz) land on a home in an Active community first (US9 #6). A home in an Archived community stays in their list as "No longer managed". Its ledger, statements, receipts and transactions stay readable. Everything else, including payments and autopay, is refused (US3 #8, #9, FR-012a).
- **Removed residents** lose the home on their very next request, even with a valid token (FR-024g). The app moves them to another home or to their landing, with a short explanation that doesn't name the home.
- **A Board Member whose only home was removed** (Jane Alvarez) keeps her board role and signs in to board mode (US8 #8, FR-036a step 1).

No artboard here differs between a Company Administrator and a Community Manager. Their staff views are briefs 01–09.

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| US9 #1 | A Company Administrator with no home and no membership lands on the portfolio. Drawn in brief 01, not redrawn here. | 8.01.a, 8.01.f |
| US9 #2 | A Community Manager with no home lands in board mode for that community. Drawn in brief 01. | 8.01.j (Pat), 8.01.m (Jordan) |
| US9 #3 | Dana lands on "No home linked to this account", which tells her to use an invitation link or contact her community manager. | 8.11.a |
| US9 #4 | A person with no linked home gets nothing from a resident page. Jane, after removal, has no home: her resident request is refused and she is moved to board mode. Dana's old resident links land on 8.11.a. | 8.11.i, 8.11.a |
| US9 #5 | Ann, whose only home is in an Onboarding community, lands on "Your community isn't live yet", not an error. | 8.11.b |
| US9 #6 | Sam has homes in an Active and an Archived community. He lands on 711 Keystone Park Dr #29 (Active). | 8.11.c |
| US3 #1 | Ann's Onboarding home is not in any home list. Sam's 9 Briar Hollow Ct is absent from his switcher. Claim-code redemption for an Onboarding home is refused with `COMMUNITY_NOT_ACTIVE`. | 8.11.b, 8.11.c, 8.11.k |
| US3 #8 | 16 Old Mill Ln is in Sam's list labelled "No longer managed". Its ledger, statements, receipts and transactions are readable. Every other resident page shows the `COMMUNITY_ARCHIVED` notice. | 8.11.c, 8.11.d, 8.11.e, 8.11.f |
| US3 #9 | A payment from a page loaded before the archive is refused with `COMMUNITY_ARCHIVED`, and nothing is created. | 8.11.g |
| US8 #6 | Jane's very next request for #29, with an unexpired token, is refused with `PROPERTY_ACCESS_DENIED`. Her account is unchanged. The removal dialog is brief 06; the emails are brief 12. | 8.11.i, 8.11.j |
| US8 #8 | Jane's Board Member membership is unchanged. She can still sign in, landing in board mode with no error. | 8.11.i, 8.11.j |
| FR-012a | Onboarding home: left out of the home list; every request refused. Archived home: in the list as "No longer managed"; ledger, statements, receipts and transactions readable; other reads and every write refused. Active home first, then Archived; Onboarding never chosen. | 8.11.b–8.11.g, 8.11.k |
| FR-024g | A refused resident request moves the person to another linked home (Sam), or to their FR-036a landing (Jane, Dana). | 8.11.h, 8.11.i, 8.11.a |
| FR-036a | Landing steps 1, 3 and 4 for people with no usable home: board mode (Jane), "isn't live yet" (Ann), "No home linked" (Dana). Step 2 is brief 01. Removing the last home doesn't stop sign-in. | 8.11.j, 8.11.b, 8.11.a |
| SC-013 | Everyone with no home who holds a membership or the Company Administrator role lands where their work is: Jane here; Robin, Casey, Jordan and Pat in brief 01. | 8.11.j; 8.01.a, 8.01.c, 8.01.j, 8.01.m |

## 4. Artboards

"Now" is Monday, Oct 12, 2026, 10:00 AM EDT, unless an artboard says it is a snapshot. Resident artboards are 1280 wide and as tall as the content, like sections 2–6. Board artboards are 1360 wide. Avatars: Dana "DF", Ann "AB", Sam "SO", Jane "JA". Nobody here except Jane holds a board role, so only Jane sees a mode control.

**Resident strip** (`WFShellSide`). The community name in bold, then " · " and the home's address. For a home in an Archived community, the text chip "No longer managed" follows the address (D6). With two or more homes in the list, the address is the switcher button with "▾" (D2). The mono account number is left out: the dataset has none.

### 4.1 Design choices (each goes on a `DCPostIt`)

| # | Question | Drawn | Other options | Post-it beside |
| --- | --- | --- | --- | --- |
| D1 | Frame for "No home linked" and "isn't live yet". The person is signed in but has no home to show. | Top bar with logo and avatar only. No tabs, side nav, strip, alerts or mode control. One centred card. | Section 1's public-page look, with no app top bar. | 8.11.a |
| D2 | Where is the home switcher? Today's app and sections 2–6 have none. The spec names a "home list" (FR-012a). | The strip's address is a menu button, as in brief 01's D5. Shown only when the list holds two or more homes. | A "My homes" item in the side nav that opens a list page. | 8.11.c |
| D3 | Which page opens for a "No longer managed" home: from the switcher, at sign-in, or after a move? | Statement, the page it can still show. | Dashboard, showing the notice. | 8.11.d |
| D4 | Side nav for a "No longer managed" home. | Unchanged. Each refused page shows the notice. | Show only Dashboard and Statement. | 8.11.e |
| D5 | What stays on Statement for a "No longer managed" home? Unpaid assessments are refused (FR-012a). | Billing history with "View receipt" links, Print and Export. No status header, metric cards, upcoming rows, or pay and autopay actions. | Add a read-only ledger balance card. | 8.11.d |
| D6 | Does the strip carry "No longer managed"? Conventions §5.4 names only the home list and switcher. | Yes, after the address. | Switcher only. | 8.11.d |
| D7 | What does "isn't live yet" say about the home? The spec hides Onboarding homes from residents. | Names neither the home nor the community. Adds the invitation line (Ann has a pending Board Member invitation). | Show the home's address. Leave out the invitation line. | 8.11.b |
| D8 | A person who never had a home opens an old resident link. "No longer have access" would be untrue for them. | Dana lands on 8.11.a with no extra notice. The page says it already. | Add the notice "You don't have access to that page." | 8.11.a |
| D9 | The selected home's community becomes Onboarding while it is in use (a restore that returns to Onboarding, FR-010). The spec doesn't say what the app does. Not drawn. | — | (1) As 8.11.h: next usable home or landing, with the notice "This home's community isn't live yet." (2) Show 8.11.b's page. | 8.11.b |

### 4.2 Landings with no usable home

**8.11.a No home linked to this account · Dana Fox**

- **Person:** Dana Fox. Former Board Member of Sakura Heights, ended Jun 30, 2026. No home, no effective membership, not a Company Administrator.
- **Shows:** D1 frame. One card: title, three short lines (§5), the "Sign out" button. Nothing names Sakura Heights or any other community.
- **Why here:** FR-036a step 4, US9 #3. Any resident link Dana opens, for example an old bookmark to Statement, lands here and shows nothing from that page (US9 #4, D8).
- **States:** default.

**8.11.b Your community isn't live yet · Resident · Ann Brooks**

- **Person:** Ann Brooks. Resident of 4 Briar Hollow Ct (Briar Hollow, Onboarding) only. No other role. Her pending Board Member invitation to Briar Hollow doesn't count (FR-022).
- **Shows:** D1 frame. One card: title, body lines (§5), the invitation line (D7), "Sign out". No address, no community name, no status chip, no error styling.
- **Why here:** FR-036a step 3, US9 #5, US3 #1.
- **States:** default.

### 4.3 Home list and a home in an Archived community

**8.11.c Home switcher · open · Resident · Sam Ortiz**

- **Person:** Sam Ortiz, signed in. Homes: 711 Keystone Park Dr #29 (Sakura Heights, Active), 16 Old Mill Ln (Old Mill Commons, Archived), 9 Briar Hollow Ct (Briar Hollow, Onboarding).
- **Shows:** `WFShellSide`, "Dashboard" current. Strip "Sakura Heights · 711 Keystone Park Dr #29 ▾", open. Menu heading "Your homes". Rows, Active first, then Archived (FR-012a):
  - "711 Keystone Park Dr #29", second line "Sakura Heights", marker "Current";
  - "16 Old Mill Ln", second line "Old Mill Commons", chip "No longer managed".
- 9 Briar Hollow Ct is not listed, not even disabled. Body: "Hi Sam" and a `wf-ph` "Dashboard — section 2, unchanged".
- **Why here:** Sam's sign-in landing (US9 #6).
- **States:** menu open.
- **Notes on canvas:** "9 Briar Hollow Ct is left out: its community isn't live (FR-012a)". Post-it D2.

**8.11.d Statement · No longer managed · Resident · Sam Ortiz**

- **Shows:** `WFShellSide`, "Statement" current. Strip "Old Mill Commons · 16 Old Mill Ln ▾" with "No longer managed". The archived banner (§5) above the title "Billing & payments". Print and Export stay; "Make a payment" is gone. The "Billing history" card from section 3 with rows "September 2026 Dues", "August 2026 Dues" and "July 2026 Dues", due the 1st of each month, each "Paid In Full" with "View receipt". Amounts are `wf-ph` (the dataset sets none).
- **Not shown:** the status header, metric cards, "Upcoming" rows, "Pay Balance" and "Manage Auto-Pay" (D5). In today's app, the "Open balance" view reads unpaid assessments, so it is refused too.
- **States:** read-only.
- **Notes on canvas:** "Readable for every resident linked to this home (FR-012a)". Post-its D3, D5, D6.

**8.11.e Dashboard · No longer managed · refused · Resident · Sam Ortiz**

- **Shows:** as 8.11.d, "Dashboard" current. Title "Dashboard", then one card with the archived notice (§5) and the button "View billing history". No KPI cards or placeholders.
- **States:** refusal (`COMMUNITY_ARCHIVED`).
- **Notes on canvas:** "Same notice on Info, Owner, Directory, Announcements, Calendar, Violations and Documents". Post-it D4.

**8.11.f Auto-pay · No longer managed · refused · Resident · Sam Ortiz**

- **Shows:** as 8.11.d, "Recurring" current. Title "Auto-pay", then one card with the payments notice (§5) and "View billing history". No enroll, change or cancel control. No method on file.
- **States:** refusal (`COMMUNITY_ARCHIVED`).
- **Notes on canvas:** "One-time shows the same notice. 16 Old Mill Ln's autopay was cancelled at the archive and doesn't restart (FR-013, FR-015)".

**8.11.g One-time payment · refused after archive · old tab · Resident · Sam Ortiz**

- **Snapshot:** Sep 30, 2026. Sam loaded "Pay your dues" (section 3b) for 16 Old Mill Ln before Robin Hale archived Old Mill Commons. He then pressed Pay, with a token issued before the archive.
- **Shows:** title "Pay your dues". In place of the payment form, one card: the payments notice, then "No payment was made.", then "View billing history". The strip now shows "No longer managed".
- **States:** refusal (`COMMUNITY_ARCHIVED`, US3 #9).
- **Notes on canvas:** "Enrolling autopay from an old tab gets the same card on Auto-pay".

### 4.4 After a removal (FR-024g)

**8.11.h Moved to another home · after removal · Resident · Sam Ortiz**

- **Snapshot:** Oct 12, 2026, 10:00 AM EDT. Jordan Blake has just removed Sam from #29 (conventions §4.4, "Remove Sam"). Sam was on #29's Dashboard and chose "Statement".
- **Shows:** 8.11.d for 16 Old Mill Ln, with the access notice (§5) at the very top of the body, above the archived banner, with "Dismiss". The strip is "Old Mill Commons · 16 Old Mill Ln" with "No longer managed" and no "▾": one home is left in the list (D2).
- **States:** refusal, then moved.
- **Notes on canvas:** "The notice says 'that home', not #29's address (conventions §6.1)".

**8.11.i Moved to board mode · after removal · Board Member · Jane Alvarez**

- **Snapshot:** Oct 12, 2026. Robin Hale has just removed Jane from #29 (brief 06, 8.06.r). Jane was in resident mode on #29's Dashboard, with an unexpired token, and chose "Statement".
- **Shows:** `WFShellBoard`. Mode control "Resident · Board", "Board" on. Strip "Sakura Heights · Board Member". Nav as a Board Member with one community: no "My Communities"; "Add community", "Community settings", "AP Ledger" and "Vendor Aging" locked (brief 01, 8.01.k). "Community Home" current. The access notice "You no longer have access to that home." with "Dismiss" at the top of the body. Body: `wf-ph` "Community Home — section 7, unchanged".
- **Why here:** Jane has no other home, so the app takes her to her FR-036a landing: board mode (US8 #6, US9 #4).
- **States:** refusal, then moved.

**8.11.j Sign-in landing · board mode · no home · Board Member · Jane Alvarez**

- **Person:** Jane Alvarez, signing in after the removal. No home. Board Member of Sakura Heights until Dec 31, 2026.
- **Shows:** 8.11.i without the notice. No toast, no error.
- **Why here:** US8 #8, FR-036a step 1, SC-013.
- **States:** default.

### 4.5 Claim code for an Onboarding home

**8.11.k Create your login · claim code · community isn't live yet**

- **Shows:** today's registration page (header "NekoHOA · Resident registration"; pills "1 verify email", "2 enter code", "3 create login", step 3 on). The error banner (§5) above the title "Create your login". First name, Last name, Password and Property claim code keep what was typed, drawn as `wf-ph` bars: no §4 person registers here. Button "Create account →".
- **Why here:** US3 #1 and the spec Edge Case. The banner shows only after the code and the email-verification proof pass. No account or link is created, and the code is not used up.
- **States:** refusal (`COMMUNITY_NOT_ACTIVE`).

## 5. Copy

Quote these exactly. Real messages fill in real names.

**Landings (8.11.a, 8.11.b)**

| Element | Text |
| --- | --- |
| 8.11.a title | "No home linked to this account" |
| 8.11.a body | "You're signed in as dana@example.com, but this account isn't linked to a home." · "If you received an invitation, open the link in that email." · "Otherwise, contact your community manager." |
| 8.11.b title | "Your community isn't live yet" |
| 8.11.b body | "Juniper Row Community Management is still setting up your community. You'll see your home here once it's live." · "There's nothing you need to do now." |
| 8.11.b invitation line (D7) | "If you received an invitation, open the link in that email." |
| Button on both | "Sign out" |

**Home switcher (8.11.c)**

| Element | Text |
| --- | --- |
| Button | "711 Keystone Park Dr #29 ▾". Accessible name: "Switch home. Current: 711 Keystone Park Dr #29". |
| Menu heading | "Your homes" |
| Marker | "Current" |
| Archived home chip | "No longer managed" |

**Home in an Archived community** (conventions §6.1, `COMMUNITY_ARCHIVED`, resident)

| Where | Text |
| --- | --- |
| Banner on Statement; card on Dashboard, Info, Owner, Directory, Announcements, Calendar, Violations and Documents | "Juniper Row Community Management no longer manages Old Mill Commons. You can still view this home's ledger, statements, receipts and transactions." |
| Card on One-time and Auto-pay | "Payments and autopay aren't available for this home, because Juniper Row Community Management no longer manages Old Mill Commons." |
| Old-tab payment, after the card text (8.11.g) | "No payment was made." |
| Button on each refused page | "View billing history" |
| Billing history link | "View receipt" |

**Moved after a refusal** (conventions §6.1, `PROPERTY_ACCESS_DENIED`)

| Case | Text |
| --- | --- |
| Another home is left (8.11.h) | "You no longer have access to that home. Now showing 16 Old Mill Ln." |
| No home left (8.11.i) | "You no longer have access to that home." |
| Close button | "Dismiss". Accessible name: "Dismiss message". |

**Claim code (8.11.k)** (conventions §6.1, `COMMUNITY_NOT_ACTIVE`)

| Element | Text |
| --- | --- |
| Banner | "This home's community isn't live yet, so the claim code can't be used now. Your code has not been used up." |
| Page title, fields, button | Today's app copy, unchanged: "Create your login", "First name", "Last name", "Password", "Property claim code", "Create account →" |

**D9 option (not drawn):** "This home's community isn't live yet."

## 6. Interactions

**Landings (8.11.a, 8.11.b)**

- "Sign out" signs the person out and opens the sign-in page, as today.
- The page is the person's landing. Each sign-in and page load works it out again from their homes and roles (FR-036a). Nothing refreshes on its own.
- When Briar Hollow is marked Active, Ann's next request shows her home (US3 #3). If Ann accepts her Board Member invitation first, she lands in board mode (FR-036a step 1, FR-036b).
- An invitation link opened while signed in follows brief 10.

**Home switcher (8.11.c)**

- The button opens the menu. Arrow keys move, Enter chooses, Esc or a click outside closes it. Focus returns to the button.
- Choosing a home makes it the selected home and opens its page: Dashboard for a home in an Active community, Statement for a "No longer managed" home (D3). Focus moves to the page's main heading.
- Choosing "Current" closes the menu.
- The list is worked out on each request. A home whose community is archived appears as "No longer managed" on the next request; a home in an Onboarding community never appears.

**Home in an Archived community (8.11.d–8.11.g)**

- Statement and receipts open read-only, as today.
- Every other resident page shows its notice card in place of its content. "View billing history" opens Statement for the same home.
- No payment or autopay control is offered. If one is reached anyway, as from an old tab (8.11.g), the server refuses it and creates nothing. The form is replaced by the card, and focus moves to the card.

**Moved after a refusal (8.11.h, 8.11.i)**

- Any resident request after the link is gone is refused, even with an unexpired token (FR-024g).
- The app moves the person to the next home in their list (Active first, then Archived), opening that home's page per D3. With no home left, the app moves them to their FR-036a landing.
- The notice sits at the top of the new page and stays until "Dismiss" (conventions §6.1). Focus moves to the page's main heading; the notice is announced. After "Dismiss", focus moves to the main heading.
- In board mode, Jane's "Resident" segment follows brief 01's D2.

**Board landing (8.11.j)**

- Sign-in lands Jane in board mode on Sakura Heights' Community Home: one community, so its home (spec 025 FR-026). No message is shown.

**Claim code (8.11.k)**

- "Create account →" submits as today. On this refusal the banner appears, takes focus, and the fields keep their values. Any other failure keeps today's generic message.

## 7. Accessibility

Conventions §8 applies. In this brief:

- **Landings:** the title is the page's main heading and takes focus on load. "Sign out" is a real button. The pages read in order: title, lines, button.
- **Home switcher:** a menu button that says whether it is open, with the accessible name in §5. "Current" and "No longer managed" are text read with the address: "16 Old Mill Ln, Old Mill Commons, No longer managed".
- **Notices:** the archived banner and the refused-page cards are text in the page, not toasts. The access notice is announced on arrival and has a labelled "Dismiss" button. None depends on colour or an icon.
- **Refusal after an action** (8.11.g, 8.11.k): announced at once; focus moves to the message.
- **Billing history:** a real table with header cells. "View receipt" links name their month: "View receipt for September 2026 Dues".
- **Board landing:** banner unchanged, dark ink on violet (spec 025 FR-038). Locked entries are announced with "Not available to your role".
- **Keyboard:** every control works by keyboard with visible focus. Nothing depends on hover.

## 8. Out of scope for this design

- The landing-order flow and the company and board landings for Robin, Casey, Jordan and Pat (brief 01: 8.01.a, 8.01.c, 8.01.f, 8.01.j, 8.01.m, 8.01.r). The "Resident" segment for people with no home (brief 01, D2).
- The Remove resident dialogs (brief 06). The "no longer managed", "removed from a home" and "autopay ended" emails (brief 12).
- Invitation acceptance and sign-up pages, including signed-in acceptance (brief 10).
- Resident pages for homes in Active communities: unchanged from sections 2–6.
- The sign-in page itself (section 1, unchanged) and registration steps 1 and 2.
- Staff views of archived communities (brief 04) and staff `COMMUNITY_ARCHIVED` banners.
- Resident architectural applications (spec 029). Restricting resident rights (FR-024f).
- Phone and tablet layouts.

## 9. Design review checklist

- [ ] **US9 #3, FR-036a step 4.** 8.11.a has the title "No home linked to this account", the invitation line, "contact your community manager" and "Sign out". No claim-code field. No community named.
- [ ] **US9 #4, FR-024g.** 8.11.i shows Jane, who has no home left, moved to board mode with "You no longer have access to that home." Nothing from the resident page shows. 8.11.a notes that Dana's resident links land there (D8).
- [ ] **US9 #5, FR-036a step 3.** 8.11.b reads "Your community isn't live yet", with no error styling and no address, community name or status word (D7).
- [ ] **US9 #6, FR-012a order.** 8.11.c shows Sam on #29, with #29 first and 16 Old Mill Ln second.
- [ ] **US3 #1.** 9 Briar Hollow Ct is absent from 8.11.c. Ann has no home list. 8.11.k shows the claim-code banner word for word, with the fields kept.
- [ ] **US3 #8.** 16 Old Mill Ln carries "No longer managed" in the switcher (8.11.c) and the strip (8.11.d). 8.11.d shows billing history and receipts read-only. 8.11.e and 8.11.f show the notices from §5.
- [ ] **FR-012a, writes.** No Pay, "Make a payment", "Pay Balance", "Manage Auto-Pay" or autopay control appears on 8.11.d–8.11.h.
- [ ] **US3 #9.** 8.11.g replaces the form with the payments notice and "No payment was made.", and is labelled a snapshot.
- [ ] **US8 #6.** 8.11.i is Jane's very next request after removal, with an unexpired token. It is refused and she is moved.
- [ ] **FR-024g, another home.** 8.11.h shows "You no longer have access to that home. Now showing 16 Old Mill Ln." above the archived banner. #29's address appears nowhere, and the switcher has no "▾".
- [ ] **US8 #8, SC-013.** 8.11.j shows Jane in board mode on Sakura Heights' Community Home, Board Member nav, with no notice, toast or error.
- [ ] **US9 #1, #2.** The checklist for brief 01 (8.01.a, 8.01.f, 8.01.j, 8.01.m) has passed. They are not redrawn here.
- [ ] **Visual language.** Resident artboards use `WFShellSide` and no violet banner. 8.11.i and 8.11.j keep dark ink on violet.
- [ ] **No hidden data.** Residents never see "Onboarding", "Active" or "Archived". Ann's and Dana's pages name no community. No notice names the lost home.
- [ ] **Design choices.** D1–D9 are each on a post-it beside the named artboard, and each drawn choice is labelled "Design choice".
- [ ] **Accessibility.** The switcher's accessible name, "Dismiss message", focus on landing titles, and focus on refusal messages are noted.
- [ ] **Data and canvas.** Only conventions §4 people, homes and dates; labels and ids match §1 (s8-11a to s8-11k); sections 1–7 render as before.
