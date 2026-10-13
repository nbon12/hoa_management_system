# 10 — Invitation acceptance and sign-up

This brief is for the public pages an invitee reaches from an invitation email: the landing for a valid link, creating an account, signing in to accept, and every refusal. §1 is the prompt to paste into Claude Design. The other sections hold the details the prompt points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file. Sending, resending and revoking invitations are briefs 05, 06 and 09. The emails are brief 12.

## 1. Prompt for Claude Design

```text
Project "HOA Management CRM", canvas "NekoHOA Wireframes.html". Attached: spec.md (spec 030, the authority), 00-conventions.md (rules, dataset, vocabulary, refusal copy) and this brief, 10-invitation-acceptance.md. spec.md wins, then the conventions. Brief §4 has each artboard's data and states, §5 the exact copy, §6 the interactions.

Add these to section "8 · Company administration" (after section 7; conventions §3). Ids s8-10a to s8-10y. Public pages: desktop 1100 × 780; phone 390 × full height. App landings: board and company pages 1360 wide, resident pages 1280 wide. Shared frames and data go in wf-company.jsx; these artboards go in wf-invitations.jsx. Labels exactly:
8.10.a Invitation landing · Community Manager
8.10.b Invitation landing · resident
8.10.c Invitation landing · resident · phone
8.10.d Invitation landing · Company Administrator
8.10.e Invitation landing · Board Member
8.10.f Create account · Community Manager
8.10.g Create account · errors · resident · phone
8.10.h Create account · failed · Board Member
8.10.i Account created · board mode · Community Manager
8.10.j Account created · resident home · resident
8.10.k Account created · company area · Company Administrator
8.10.l Sign in to accept · Board Member
8.10.m Sign in to accept · Board Member · phone
8.10.n Accept invitation · confirm · Board Member
8.10.o Accepted · board mode · Board Member
8.10.p Accept invitation · confirm · resident · phone
8.10.q Accepted · resident home · resident
8.10.r Accepted · role already held · Board Member
8.10.s Invitation refused · expired
8.10.t Invitation refused · expired · phone
8.10.u Invitation refused · link can't be used
8.10.v Invitation refused · link can't be used · phone
8.10.w Invitation refused · different email
8.10.x Invitation link · too many requests · phone
8.10.y Create account · too many requests · Community Manager

Public pages reuse wf-auth.jsx: Registration's header bar (right label "Invitation"), step pills and lav "Property found" card; LoginB's two columns for sign-in, one column at phone width. No app nav. App landings reuse WFShellBoard + boardNav() (violet banner unchanged, dark ink on violet), the company-area frame (conventions §7.1) and WFShell, with §4 data passed in.

A valid link shows community, offer, address (resident only), sender, invited email and expiry, with two paths: create an account, or sign in to accept. Refusal pages show nothing about the invitation; 8.10.w never shows the invited email.

Put §4.2's design choices and open question on DCPostIts beside the named artboards.

Fidelity: lofi, section 7's visual language, section 1's auth look. Layout, copy, states and what is shown or hidden are binding; colour and spacing are placeholders.

Don't draw: a claim-code field; Google sign-in, Quick pay or portal selection; a "community isn't live yet" state; invitation details on a refusal page; a status chip; other residents' names; a confirm-password field; error codes; tablet layouts.

When done, open every artboard and check it renders without console errors, shows its stated state and width (no sideways scroll at 390), and matches brief §4 and §5 word for word. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

An invitation offers a community role, residency of one property, or the Company Administrator role (FR-022). The invitee opens the link from email, often on a phone, so these pages work at phone width (spec Assumptions).

- **New people** (Pat Rivera, Kim Tran, ops@example.com) have no account. They create one from the link with a name and a password. No claim code is needed. The link proves they control the email, so the account is created with the email confirmed (FR-022a).
- **People with an account** (Ann Brooks, Sam Ortiz) sign in and confirm (FR-022, FR-024a).
- **Someone signed in as another account** (Bob Brooks, on the laptop he shares with Ann) is refused without seeing the invited email (US2 #7).
- **Anyone holding a dead link** (expired, revoked, used, replaced) sees one plain refusal that reveals nothing about the invitation (FR-022a).

The page is the same whoever sent the invitation. Company Administrators and Community Managers have no different powers here.

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| US2 #2 | Pat has no account. Pat creates one from the link with a name and password, no claim code. The email is fixed and confirmed. Pat is signed in and lands in board mode for Keystone Park, with no home. | 8.10.a, f, i |
| US2 #5 | alex@example.com's link, sent more than 72 hours ago: "This invitation has expired". Nothing is created. | 8.10.s |
| US2 #6 | alex@example.com's old link, after Jordan resent it (8.05.l): "This invitation link can't be used". | 8.10.u |
| US2 #7 | Bob is signed in and opens Ann's link: "This invitation is for a different email address", "You're signed in as bob@example.com", and "Sign out". ann@example.com appears nowhere. Ann's invitation stays pending. | 8.10.w |
| US7 #1 | ops@example.com's landing offers the Company Administrator role, expiry in the company default time zone. After creating an account, the person is in the company area. | 8.10.d, k |
| US8 #1 | Kim opens the link within 72 hours, creates an account (no claim code), and lands on 711 Keystone Park Dr #29 in resident mode. | 8.10.b, c, g, j |
| US8 #3 | Sam already has an account. He signs in and confirms. Nothing changes until he presses "Accept invitation". Afterwards his home list shows #29 and 16 Old Mill Ln. | 8.10.p, q |
| US8 #5 | max@example.com's link: expired page. After the resend (8.06.p), the old link: "can't be used" page. | 8.10.t, v |
| FR-022 validity | Every valid landing shows "Expires …" with date, time and time-zone abbreviation, in the community's time zone; ops@'s uses the company default. | 8.10.a–e |
| FR-022 bound to email | Sign in with the matching account, then confirm. A different account is refused. | 8.10.l–o, w |
| FR-022 check order | Revoked, used or replaced first, whatever the age; then expired; then different email. books@example.com's link (revoked, also older than 72 hours) shows "can't be used", not "expired". | 8.10.s, u, w |
| FR-022 duplicates | The account already holds the role at acceptance: marked accepted, nothing duplicated, and the person is told so. | 8.10.r |
| FR-022 sender authority, races | A link whose sender lost authority, or that was revoked or used a moment before submit, shows "can't be used" and creates nothing. | 8.10.u, n |
| FR-022a | No claim code. Email fixed and confirmed. A membership or Company Administrator invitation links no property; a resident invitation links only the invited property, even when it has residents. Details only for a valid link. Account-creation failure is generic and says to sign in and accept. | 8.10.a–k, h, s–x |
| FR-041 (b) | The per-client limit on lookup and acceptance: "Too many requests. Wait a few minutes, then try again." | 8.10.x, y |
| Edge: mismatch | Never shows the invited email; the invitation stays pending; the attempt is logged (canvas note). | 8.10.w |
| Edge: revoke and accept race | Exactly one wins. If the revoke wins, the invitee sees the "can't be used" page. | 8.10.u, n |
| Edge: already holds the role | Marked accepted; nothing duplicated. | 8.10.r |

## 4. Artboards

### 4.1 Frames, data and timing

- **"Now"** is Monday, Oct 12, 2026, 10:00 AM EDT. Artboards after an acceptance (8.10.i, j, k, o, r) are snapshots; every other brief shows those invitations still pending. 8.10.p and q are snapshots as on Oct 9, 2026, when Sam accepted. The dataset gives no sender or times for Sam's invitation, so those values are `wf-ph`.
- **Public pages** (8.10.a–h, l–n, p, s–y): Registration's header bar, "NekoHOA" on the left, "Invitation" on the right. No nav, no account cluster.
  - **Desktop:** two columns, as in Registration and LoginB. The left column has the heading, the sub line and the actions; the right column has the invitation card.
  - **Phone:** one column. Heading, card, then actions stacked at full width.
- **Invitation card** (`wf-card lav`, from Registration's "Property found"): a `wf-pill ok` reading "Valid invitation"; the main line (community name; for a resident, the address with a 🏠 `wf-ph` and pills for the community and "Resident"); then "You're invited as", "Sent by", "For" and "Expires" rows. No status chip, no lot number, no other residents.
- **Step pills** (D8): the create-account path shows "1 invitation ✓" and "2 create account". The sign-in path shows "1 invitation ✓", "2 sign in" and "3 accept". The landing and the refusal pages show no pills.
- **Refusal pages** (8.10.s–x): header bar, then one centred `wf-card` with a heading and body. No pills, no invitation card. Actions per §5.4.
- **App landings:** Pat (8.10.i) and Ann (8.10.o, r) in `WFShellBoard` with `boardNav()`, violet banner, "Community Home" current, an "Onboarding" chip next to the community name in the strip, body "Community Home — section 7, unchanged". ops@ (8.10.k) in the company-area frame, "Portfolio" current, body "Portfolio — brief 02". Kim and Sam (8.10.j, q) in `WFShell`, body "Resident home — section 2, unchanged".

### 4.2 Design choices and open question (each on a `DCPostIt`)

| # | Question | Drawn | Other option | Post-it beside |
| --- | --- | --- | --- | --- |
| D1 | Does the landing depend on whether the email has an account? | No. Everyone sees both paths, "Create an account" first. | Lead with "Sign in to accept" when the email has an account, as the invitation email does (FR-024a). | 8.10.a |
| D2 | Name fields. FR-022a says "a name". | "First name" and "Last name", both required, as today's create-login step shows them (today it checks only the first name). | One "Full name" field. | 8.10.f |
| D3 | Can the password be shown? | Yes: a "Show" button in the field. | No button. | 8.10.f |
| D4 | Is the sign-in email filled in? | Yes, with the invited email, and it can be edited. | Empty. | 8.10.l |
| D5 | Where does the person go after accepting by sign-in? | To what they accepted: board mode for that community, the new home, or the company area. | Their normal sign-in landing (FR-036a), with the same toast. | 8.10.o |
| D6 | What does someone who already holds the role see? | The landing for what they accepted, with a toast saying nothing was added. | The ordinary success toast. | 8.10.r |
| D7 | Where does "Sign out" go on 8.10.w? | The sign-in page, with the toast "You're signed out." The person opens the link again from the email, as the message says. | Reload the same invitation link, signed out. | 8.10.w |
| D8 | Step pills. | Per path, as in §4.1. | No pills. | 8.10.f |
| D9 | An action on refusal pages. | A "Go to sign in" link. | No action. | 8.10.s |

**Open question Q1** (`DCPostIt` beside 8.10.n): the spec says acceptance needs "a matching, verified account". Accounts made by claim code or invitation are verified. If an account with a matching but unverified email can exist, what does it see? Not drawn.

### 4.3 Valid link

**8.10.a Invitation landing · Community Manager.** Pat's link (pat@example.com). Card: "Keystone Park" · "You're invited as: Community Manager" · "Sent by Robin Hale" · "For pat@example.com" · "Expires Oct 14, 2026, 9:15 AM EDT". Buttons "Create an account" (primary) and "Sign in to accept", with the helper line. State: default (FR-022a). Post-it D1.

**8.10.b Invitation landing · resident.** Kim's link (kim@example.com). Card: 🏠 "711 Keystone Park Dr #29", pills "Sakura Heights" and "Resident" · "You're invited as: Resident of this home" · "Sent by Jordan Blake" · "For kim@example.com" · "Expires Oct 13, 2026, 4:40 PM EDT". Same buttons. Note: "#29 already has residents. The page doesn't name them." State: default.

**8.10.c Invitation landing · resident · phone.** 8.10.b at 390 wide, one column. Buttons full width, about 44 px tall. State: default, phone.

**8.10.d Invitation landing · Company Administrator.** ops@example.com's link. Card main line "Juniper Row Community Management" (the company stands where a community would; design choice) · "You're invited as: Company Administrator" · "Sent by Robin Hale" · "For ops@example.com" · "Expires Oct 15, 2026, 8:30 AM EDT". Note: "Company default time zone (FR-022)." State: default (US7 #1).

**8.10.e Invitation landing · Board Member.** Ann's link, opened after Bob signed out (8.10.w). Card: "Briar Hollow" · "You're invited as: Board Member" · "Sent by Robin Hale" · "For ann@example.com" · "Expires Oct 14, 2026, 3:30 PM EDT". Note: "With a term, the role row reads “Board Member, until Dec 31, 2027” (new@example.com, Sakura Heights)." State: default.

### 4.4 Create an account

**8.10.f Create account · Community Manager.** Pat, after "Create an account". Pills "1 invitation ✓", "2 create account". A one-line summary of the offer above the form. Email shown as text "pat@example.com" with its helper, not an input. First name "Pat", Last name "Rivera", Password (dots) with "Show" and its helper. "All fields are required." Button "Create account and accept". Links "Back to invitation" and "Already have an account? Sign in to accept". State: filled, before submit. Post-its D2, D3, D8.

**8.10.g Create account · errors · resident · phone.** Kim at 390 wide pressed "Create account and accept" with First name empty and password "kim123". Error summary at the top with focus: heading "Check these fields", links "Enter your first name." and "Password must be at least 8 characters, with an uppercase letter, a number and a symbol." The same messages under each field. Nothing is created. State: validation error.

**8.10.h Create account · failed · Board Member.** Ann chose "Create an account" by mistake; she already has an account. After submit, a banner at the top of the form: "We couldn't create an account with these details. If you already have an account, sign in to accept the invitation." with the button "Sign in to accept". Names kept; password emptied. Note: "Generic on purpose (spec 017 FR-A5). Never says the email is taken." State: refusal.

**8.10.i Account created · board mode · Community Manager.** Snapshot. Pat Rivera, avatar "PR", board mode for Keystone Park, strip "Keystone Park · Community Manager" with "Onboarding". Toast "Your account is ready. You're now a Community Manager of Keystone Park." Notes: "Open-ended Community Manager from Oct 12, 2026. No home is linked (FR-022a). Same landing as 8.01.j." State: success (US2 #2).

**8.10.j Account created · resident home · resident.** Snapshot. Kim Tran, avatar "KT", strip "Sakura Heights · 711 Keystone Park Dr #29". Toast "Your account is ready. You're now a resident of 711 Keystone Park Dr #29." Note: "Kim is linked to #29 next to the residents already there. The claim code 'already claimed' rule doesn't apply (FR-022a)." State: success (US8 #1).

**8.10.k Account created · company area · Company Administrator.** Snapshot. The person who accepted ops@example.com's invitation. Avatar `wf-ph` (the dataset has no name). No board-mode control. Toast "Your account is ready. You're now a Company Administrator." State: success (US7 #1).

### 4.5 Sign in to accept

**8.10.l Sign in to accept · Board Member.** Ann, desktop, LoginB layout. Left (lav): heading "Sign in to accept" and the invitation card from 8.10.e. Right: label "Sign in", pills "1 invitation ✓", "2 sign in", "3 accept", Email "ann@example.com" with its helper, Password, button "Sign in", links "Forgot password" and "New here? Create an account". Note: "Wrong password: today's “Invalid email or password.” Signing in as another account: 8.10.w." State: default. Post-it D4.

**8.10.m Sign in to accept · Board Member · phone.** 8.10.l at 390 wide: the card on top, the form below. State: default, phone.

**8.10.n Accept invitation · confirm · Board Member.** Ann, signed in. Pill "3 accept" current. Heading "Accept this invitation?", "You're signed in as ann@example.com.", the card, then "Your Board Member role in Briar Hollow starts today, Oct 12, 2026, with no end date." Buttons "Accept invitation" (primary) and "Not now". Notes: "Ann opened the link while already signed in: she comes straight here." · "If the invitation was revoked, used or resent a moment before she presses Accept, she sees 8.10.u and nothing is created." State: confirmation. Post-it Q1.

**8.10.o Accepted · board mode · Board Member.** Snapshot. Ann Brooks, avatar "AB", board mode for Briar Hollow, strip "Briar Hollow · Board Member" with "Onboarding". "Community settings" and "Add community" locked as in 8.01.k. Toast "You're now a Board Member of Briar Hollow." Note: "Before this, Ann landed on “Your community isn't live yet” (brief 11)." State: success. Post-it D5.

**8.10.p Accept invitation · confirm · resident · phone.** Snapshot, Oct 9, 2026. Sam, signed in, 390 wide. "You're signed in as sam@example.com." Card: 🏠 "711 Keystone Park Dr #29", pills "Sakura Heights", "Resident"; "Sent by" and "Expires" as `wf-ph`. Text "711 Keystone Park Dr #29 is added to your homes. Your other homes and roles don't change." Buttons "Accept invitation", "Not now". Note: "Nothing changes until Sam presses Accept invitation (US8 #3)." State: confirmation, phone.

**8.10.q Accepted · resident home · resident.** Snapshot, Oct 9. Sam Ortiz, avatar "SO", on #29. Toast "You're now a resident of 711 Keystone Park Dr #29." Home switcher open, as brief 11 draws it: "711 Keystone Park Dr #29 · Sakura Heights" (current) and "16 Old Mill Ln · No longer managed". Note: "9 Briar Hollow Ct is left out while Briar Hollow is Onboarding (FR-012a)." State: success (US8 #3).

**8.10.r Accepted · role already held · Board Member.** Variant of 8.10.o, not in the dataset's timeline: after sending the invitation, Robin appointed Ann directly from Briar Hollow's people list. Ann then pressed "Accept invitation". Same frame as 8.10.o. Toast "You're already a Board Member of Briar Hollow. Nothing was added." Note: "The invitation is marked accepted. No second membership is made (FR-022)." State: success, duplicate. Post-it D6.

### 4.6 Refusals

Every refusal page shows no community, role, address, sender or invited email. "Nothing was created" is true on each; it is a canvas note, not page copy.

**8.10.s Invitation refused · expired.** alex@example.com's link (Sakura Heights, Community Manager, expired Oct 8, 2026, 11:00 AM EDT). Title and body per §5.4, link "Go to sign in". Note: "No membership is created (US2 #5)." State: refusal. Post-it D9.

**8.10.t Invitation refused · expired · phone.** max@example.com's link (#31, expired Oct 9, 2026, 1:20 PM EDT), 390 wide. Same page. Note: "No link to the property is made (US8 #5)." State: refusal, phone.

**8.10.u Invitation refused · link can't be used.** alex@example.com's old link after Jordan resent it (8.05.l). Title and body per §5.4, link "Go to sign in". Notes, one line each: the same page for books@example.com's revoked link, although it is also older than 72 hours (check order); drew@ and rae@example.com's links, revoked by the Old Mill Commons archive; Pat's link opened again after Pat accepted; an invitation whose sender lost authority (8.05.q); a term end date that has passed; and a revoke, resend or acceptance that won a race while this page was open. State: refusal (US2 #6, FR-022).

**8.10.v Invitation refused · link can't be used · phone.** max@example.com's old link after Robin resent it (8.06.p), 390 wide. Same page. State: refusal, phone (US8 #5).

**8.10.w Invitation refused · different email.** Bob Brooks is signed in on the shared laptop and opens Ann's link. Title and body per §5.4, naming only bob@example.com. Button "Sign out". Notes: "Same page if Bob signs in from 8.10.l." · "Ann's invitation stays pending. The attempt is logged." · "The check order still applies: an expired link shows 8.10.s." State: refusal. Post-it D7.

**8.10.x Invitation link · too many requests · phone.** Looking up a link hit the per-client limit (FR-041 b). Title "Too many requests", body "Wait a few minutes, then try again.", button "Try again". No invitation details, because none were loaded. State: refusal, phone.

**8.10.y Create account · too many requests · Community Manager.** 8.10.f after submit hit the same limit. A banner at the top of the form: "Too many requests. Wait a few minutes, then try again." Every value kept. Nothing is created. State: refusal.

**Not drawn (copy in §5):** loading; a wrong password on sign-in; too many requests on the confirm step; "Couldn't load your invitation." with "Try again".

## 5. Copy

Quote these exactly. "SR" means text for screen readers only. Values in brackets change per invitation.

### 5.1 Landing

| Element | Text |
| --- | --- |
| Header right label | "Invitation" |
| Loading (SR and visible) | "Checking your invitation link…" |
| Load error | "Couldn't load your invitation." with "Try again" |
| Heading | "You're invited" |
| Sub line | "Accept to get access. Invitation links work for 72 hours." |
| Card pill | "Valid invitation" |
| Card rows | "You're invited as: [Community Manager / Board Member / Accountant / Company Administrator / Resident of this home]" · "Sent by [Robin Hale]" · "For [pat@example.com]" · "Expires [Oct 14, 2026, 9:15 AM EDT]" |
| Role with a term | "Board Member, until Dec 31, 2027" |
| Buttons | "Create an account" (primary) · "Sign in to accept" |
| Helper | "New here? Create an account. Already have an account for [pat@example.com]? Sign in to accept." |

### 5.2 Create account

| Element | Text |
| --- | --- |
| Heading | "Create your account" |
| Sub line | "Creating your account accepts the invitation. You don't need a claim code." |
| Offer summary | "[Community Manager] · [Keystone Park] · Sent by [Robin Hale] · Expires [Oct 14, 2026, 9:15 AM EDT]". Resident: "Resident of [711 Keystone Park Dr #29] · [Sakura Heights] · …" |
| Email | Label "Email". Value "[pat@example.com]". Helper "Your invitation link confirms this email address. It can't be changed." |
| Fields | "First name" · "Last name" · "Password" with "Show" / "Hide" (SR: "Show password") |
| Password helper | "At least 8 characters, with an uppercase letter, a number and a symbol." (today's password rules) |
| Required line | "All fields are required." |
| Button | "Create account and accept" (busy: "Creating account…") |
| Links | "Back to invitation" · "Already have an account? Sign in to accept" |
| Error summary heading | "Check these fields" |
| Validation | "Enter your first name." · "Enter your last name." · "Enter a password." · "Password must be at least 8 characters, with an uppercase letter, a number and a symbol." If spec 017's registration messages say this differently, use theirs. |
| Account creation failed | "We couldn't create an account with these details. If you already have an account, sign in to accept the invitation." Button "Sign in to accept". |

### 5.3 Sign in and confirm

| Element | Text |
| --- | --- |
| Heading (left panel) | "Sign in to accept" |
| Form label | "Sign in" |
| Email helper | "Use the account for [ann@example.com], the address this invitation was sent to." |
| Button and links | "Sign in" (busy: "Signing in…") · "Forgot password" · "New here? Create an account" |
| Wrong password | "Invalid email or password." (today's copy) |
| Confirm heading | "Accept this invitation?" |
| Signed-in line | "You're signed in as [ann@example.com]." |
| What happens: role | "Your [Board Member] role in [Briar Hollow] starts today, [Oct 12, 2026], with no end date." With a term: "…starts today, Oct 12, 2026, and ends Dec 31, 2027." |
| What happens: resident | "[711 Keystone Park Dr #29] is added to your homes. Your other homes and roles don't change." |
| What happens: Company Administrator | "You'll be able to open the company area for Juniper Row Community Management." |
| Buttons | "Accept invitation" (busy: "Accepting…") · "Not now" |

### 5.4 Results and refusals

| Case | Text | Where |
| --- | --- | --- |
| Created, role | "Your account is ready. You're now a [Community Manager] of [Keystone Park]." | Toast on the landing |
| Created, resident | "Your account is ready. You're now a resident of [711 Keystone Park Dr #29]." | Toast |
| Created, Company Administrator | "Your account is ready. You're now a Company Administrator." | Toast |
| Accepted by sign-in | "You're now a [Board Member] of [Briar Hollow]." · "You're now a resident of [711 Keystone Park Dr #29]." · "You're now a Company Administrator." | Toast |
| Already held | "You're already a [Board Member] of [Briar Hollow]. Nothing was added." · Resident: "[711 Keystone Park Dr #29] is already one of your homes. Nothing was added." | Toast |
| `INVITATION_EXPIRED` | Title "This invitation has expired". Body "Invitation links work for 72 hours. Ask the person who invited you to send a new link." Link "Go to sign in". | Page |
| `INVITATION_INVALID` | Title "This invitation link can't be used". Body "It may have been cancelled, already used, or replaced by a newer link. If you still need access, ask the person who invited you." Link "Go to sign in". | Page |
| `INVITATION_EMAIL_MISMATCH` | Title "This invitation is for a different email address". Body "You're signed in as bob@example.com. Sign out, then open the link again and sign in or create an account with the email address the invitation was sent to." Button "Sign out". | Page |
| After "Sign out" (D7) | "You're signed out." | Toast on the sign-in page |
| Rate limited, lookup | Title "Too many requests". Body "Wait a few minutes, then try again." Button "Try again". | Page |
| Rate limited, form or confirm | "Too many requests. Wait a few minutes, then try again." | Banner on the form or confirm card |

## 6. Interactions

- **Opening a link.** The page shows "Checking your invitation link…" and no details. The checks run in the FR-022 order: revoked, used or replaced → 8.10.u; else older than 72 hours → 8.10.s; else, if someone is signed in with a different email → 8.10.w. Checking the email on open, not only on accept, is a design choice, so a different account never sees the invitation (conventions §6.1). Otherwise: signed out → the landing; signed in with the matching email → the confirm step (8.10.n). Focus goes to the page heading.
- **"Create an account"** opens the form. Focus moves to First name. "Back to invitation" returns to the landing with nothing lost.
- **"Create account and accept".** Client checks run first. If any fail, the error summary appears and takes focus; each link moves focus to its field. Otherwise the request is sent and the button reads "Creating account…". Then:
  - **Success:** the account is created for the invited email, confirmed, and the invitation is accepted in the same action. The person is signed in and lands on what they accepted: board mode for that community (8.10.i), the new home (8.10.j), or the company area (8.10.k). The toast is announced.
  - **Generic failure:** the banner in 8.10.h, which takes focus. Its button goes to the sign-in step with the email filled in.
  - **Invitation refused** (the link expired, or was revoked, used or resent after the page opened): the form is replaced by 8.10.s or 8.10.u. No account is created.
  - **Rate limited:** the banner in 8.10.y; values are kept.
- **"Sign in to accept"** opens 8.10.l with focus on Password (the email is filled in, D4). "Sign in" with the matching account leads to 8.10.n. With another account: 8.10.w; that account stays signed in. A wrong password shows today's message under the form.
- **"Accept invitation"** accepts. Success → D5's landing with the toast. If the account already holds the role or home → 8.10.r. Invitation refused → 8.10.s or 8.10.u, and nothing is created. Rate limited → the banner on the confirm card.
- **"Not now"** leaves the invitation pending and takes the person to their usual sign-in landing (FR-036a). Ann goes to "Your community isn't live yet". The link still works until it expires.
- **"Sign out"** (8.10.w) ends Bob's session and goes to the sign-in page with "You're signed out." (D7). Ann's invitation is unchanged.
- **"Try again"** (8.10.x) repeats the lookup. **"Go to sign in"** opens the normal sign-in page.
- **Races.** When an accept, revoke, resend or archive race, exactly one wins (FR-022). If the invitee loses, they see 8.10.u. If a sender's Resend or Revoke loses, the sender sees "This invitation was already accepted or revoked. The list is now up to date." (briefs 05, 06).
- **Nothing on screen** shows the link's token. Each acceptance, and each failed attempt, is logged (FR-023, FR-024e); no screen shows it.

## 7. Accessibility

Conventions §8 applies. For these pages:

- **Headings.** Each page has one `h1`: "You're invited", "Create your account", "Sign in to accept", "Accept this invitation?" or the refusal title. It takes focus when the page changes, so a screen reader hears where it is.
- **Invitation card.** A labelled region. Its rows are a description list ("You're invited as", "Sent by", "For", "Expires").
- **Expiry** is a full date, time and zone, never only a countdown.
- **Forms.** Visible labels. The email on 8.10.f is text, not a disabled input, and its helper says why it can't change. Fields identify their purpose to the browser (name, email, new password, current password), so password managers and autofill work. The email field brings up the email keyboard. Helpers and errors are tied to their fields.
- **Errors.** The summary takes focus and links to each field. The account-creation and rate-limit banners are announced at once and take focus.
- **Password "Show"** is a real button whose name says what it does, and it says whether the password is shown.
- **Phone.** Reflows at 320 CSS px without sideways scrolling, works at 200% zoom, touch targets about 44 × 44 px, buttons stacked full width.
- **Words, not colour.** "Valid invitation" and every refusal are words. The ok pill colour only adds to them.
- **Toasts** after acceptance are announced politely. Refusal pages are announced through their focused heading.
- **Board landing.** The banner stays dark ink on violet (spec 025 FR-038).

## 8. Out of scope for this design

- Sending, resending and revoking invitations, and the staff-side lists (briefs 05, 06, 09).
- The invitation emails themselves (brief 12).
- A "community isn't live yet" state on these pages. Scheduled resident invitations are never emailed while a community is Onboarding (FR-024b), and an archive revokes every invitation (FR-012), so no such link exists.
- The landing pages "Your community isn't live yet" and "No home linked to this account", the home switcher, and the archived home view (brief 11). 8.10.q borrows the switcher only to show the result.
- Claim-code registration, which is unchanged (FR-024f).
- Google sign-in, Quick pay and portal selection.
- Password reset, account unlock and other account support (spec Out of Scope).
- Notifying the owner of record when a resident joins (spec Out of Scope).
- Tablet layouts.

## 9. Design review checklist

- [ ] **FR-022a.** 8.10.a–e show the community (or company), the offer, the address (resident only), the sender, the invited email and the expiry, with both "Create an account" and "Sign in to accept".
- [ ] **FR-022 validity.** Every expiry reads like "Expires Oct 14, 2026, 9:15 AM EDT"; ops@'s uses the company default time zone (8.10.d).
- [ ] **US2 #2.** 8.10.f has no claim-code field, the email as fixed text with "Your invitation link confirms this email address…", and "Create account and accept"; 8.10.i lands Pat in board mode for Keystone Park with no home.
- [ ] **US8 #1.** Kim's landing, form and errors work at 390 wide (8.10.c, g); 8.10.j lands Kim on #29.
- [ ] **US7 #1.** ops@'s landing (8.10.d) and the company-area landing (8.10.k).
- [ ] **US8 #3.** Sam confirms with "Accept invitation" (8.10.p); afterwards #29 and 16 Old Mill Ln are in his home list (8.10.q).
- [ ] **Sign in and confirm.** 8.10.l, m, n and o show sign-in, then "Accept this invitation?" with "You're signed in as ann@example.com." and the start date.
- [ ] **US2 #5, US8 #5.** "This invitation has expired" with the conventions body, and no invitation details (8.10.s, t).
- [ ] **US2 #6, US8 #5.** "This invitation link can't be used" for old links after a resend (8.10.u, v); the note lists every other reason, including the check-order case (books@).
- [ ] **US2 #7.** 8.10.w names only bob@example.com, offers "Sign out", and shows no invitation detail. ann@example.com appears nowhere on it.
- [ ] **FR-022 duplicates.** 8.10.r says "Nothing was added."
- [ ] **FR-022a generic failure.** 8.10.h shows the conventions message and "Sign in to accept", and never says the email is taken.
- [ ] **FR-041 (b).** 8.10.x (page, no details) and 8.10.y (banner, values kept) show "Too many requests. Wait a few minutes, then try again."
- [ ] **Races.** Notes on 8.10.n and 8.10.u say a lost race shows 8.10.u and creates nothing.
- [ ] **Hidden data.** No refusal page shows a community, role, address, sender or invited email. No page names other residents. No status chip appears on a card.
- [ ] **Not drawn.** No claim code, Google, Quick pay, portal selection, confirm-password field, "isn't live yet" state or error code.
- [ ] **Phone.** 8.10.c, g, m, p, t, v and x are 390 wide, one column, with no sideways scroll.
- [ ] **Frames.** Public pages use the Registration header with no nav. Board landings keep the violet banner, dark ink on violet.
- [ ] **Design choices.** D1–D9 and Q1 are on post-its beside the named artboards.
- [ ] **Data.** Only conventions §4 people, emails, communities and times. Sam's unknown values are `wf-ph`.
- [ ] **Canvas.** Labels and ids match §1 exactly (s8-10a to s8-10y). Sections 1–7 render as before.
