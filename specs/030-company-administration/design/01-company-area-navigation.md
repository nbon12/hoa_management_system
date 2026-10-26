# 01 — Company area and navigation

This brief asks Claude Design for the ways people reach spec 030's new pages: the Company Administrator's company entry (two options), the board-mode nav entries for Community Managers, switchers that show Onboarding communities, and the sign-in landing order. §1 is the prompt the owner pastes into Claude Design. §2–§9 are the details it points to.

## 1. Prompt for Claude Design

```text
Project "HOA Management CRM", canvas "NekoHOA Wireframes.html". Attached: spec.md (spec 030), 00-conventions.md and 01-company-area-navigation.md (this brief). spec.md wins over both. Follow the conventions exactly: naming, the §4 dataset, vocabulary, refusal copy, fidelity.

Add these artboards to section "8 · Company administration". Create it after section 7 (conventions §3) if it isn't there yet. Ids are s8-01a to s8-01r. Lofi, same visual language as section 7. Put shared section-8 data and frames in wf-company.jsx (extend it if it exists) and these artboards in wf-company-nav.jsx. Reuse WFShellBoard, boardNav(), the account cluster (.wf-user), the mode control (.wfb-mode), BoardModeSwitch and BoardNavMulti. Add props and data with defaults so sections 1–7 render as before.

§4 gives each artboard's person, data, states and design choices; §5 gives the exact copy.

Company sign-in and company entry (owner decision: a bookmarkable company sign-in page, plus a "Company" menu in the account controls):
8.01.a Company sign-in · bookmarkable page · Company Administrator (Robin Hale)
8.01.b Company sign-in · not a Company Administrator · Community Manager (Jordan Blake)
8.01.c Company sign-in · sign-in failed · Company Administrator (Robin)
8.01.d Board mode · Company menu open · Company Administrator and Board Member (Casey Novak)
8.01.e Company area · company shell · Company Administrator and Board Member (Casey)
8.01.f Company area · company shell · Company Administrator (Robin)
8.01.g Company area · Community settings open · Company Administrator (Robin)
8.01.h Company area · board page refused · Company Administrator (Robin)

Board-mode nav:
8.01.i Board mode · Community settings and Add community · Community Manager (Jordan Blake)
8.01.j Board mode · Onboarding community · Community Manager (Pat Rivera)
8.01.k Board mode · locked entries · Board Member (Lee Morales)
8.01.l Board mode · after co-manager appointment · Board Member and Community Manager (Lee)

Switchers:
8.01.m My Communities · Onboarding chip · Community Manager (Jordan)
8.01.n Community switcher · open · Community Manager (Jordan, Design choice)
8.01.o My Communities · archived left out · Accountant (Avery Quinn)
8.01.p My Communities · load error · Community Manager (Jordan)
8.01.q My Communities · portfolio refused · Community Manager (Jordan)

8.01.r Sign-in landing order · flow

Rules. Robin never sees a "Board" option or "Enter board mode". Casey sees both the company entry and the board-mode control. Nobody else sees a company entry or a Portfolio item, not even locked. In board mode, entries the role doesn't confer show locked (🔒). Onboarding communities carry an "Onboarding" text chip. Archived communities never appear in a switcher or My Communities. No violet banner outside board mode.

Don't draw: page bodies other briefs own (use a labelled placeholder), phone layouts, Notifications, or status shown only by ✓ or colour.

Put each "Design choice" from §4 on a DCPostIt beside its artboard.

When done, open the canvas and check that each artboard renders in its stated state, sections 1–7 are unchanged and every id is unique. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

- **Company Administrators** (Robin Hale, Casey Novak) run the company's portfolio. They often hold no community membership and live in no community. They need one way in, from the top-bar account controls, that works with no membership and no home (FR-036, FR-036a). The role is not a board role. Holding it alone must not show the board-mode control or open any board page (FR-003, FR-036).
- **A Company Administrator who also sits on a board** (Casey Novak) needs both ways in: the company entry and the spec 025 board-mode control (FR-036).
- **Community Managers** (Jordan Blake, Pat Rivera) work in board mode. They now reach their community's Community settings page and Add community from the board nav (FR-029, FR-036). They never see the portfolio (FR-035). Their switchers now include Onboarding communities, because Onboarding memberships count for board mode (FR-036b).
- **Board Members and Accountants** (Lee Morales, Avery Quinn) see the new board-nav entries locked, as spec 025 FR-040 requires for entries their role doesn't confer. A Board Member appointed co-manager gets the entries unlocked (US2 #3).
- **Everyone** who signs in lands somewhere useful, even with no linked home (FR-036a). The flow artboard shows the order. Brief 11 draws the landing pages themselves.

**Owner decision (2026-10-14):** Company Administrators get their own sign-in page at a stable address they can bookmark (FR-036c). Inside the app they move around with a "Company" menu in the top-bar account controls, which opens the company shell. (This was Option B; the earlier Option A, a third "Company" mode in the mode control, is dropped.) Briefs 02–09 draw their pages in this company shell.

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| FR-036 (entry) | A Company Administrator opens the company area from a "Company" menu in the top-bar account controls, with no community membership. The menu opens the company shell. | 8.01.d, 8.01.e, 8.01.f |
| FR-036c | A company sign-in page at a stable address that Company Administrators can bookmark. Same sign-in rules as the usual page. A Company Administrator lands in the company area. Anyone else is signed in to their usual landing with a short notice; the page grants nothing. | 8.01.a, 8.01.b, 8.01.c |
| FR-036 (contents) | The company area nav holds Portfolio, Add community, Company defaults and Company administrators. Each community's Community settings page opens inside the company area. | 8.01.e, 8.01.f, 8.01.g |
| FR-036 (grant alone) | Robin, who holds only the grant, sees no "Board" option, no "Enter board mode" and no board nav. A link to a board page is refused and Robin is returned to the portfolio. | 8.01.f, 8.01.h |
| FR-036 (both roles) | Casey, a Company Administrator and a Board Member, sees the Company menu and the board-mode control. | 8.01.d, 8.01.e |
| FR-036 (last sentence) | "A Community Manager reaches Add community from board mode." An "Add community" board-nav entry, unlocked for Community Managers. | 8.01.i, 8.01.j, 8.01.l |
| FR-029 | "Community Managers reach it from board mode in the active community": a "Community settings" board-nav entry opens the active community's page. "Company Administrators reach it from the portfolio": a breadcrumb "Portfolio › Keystone Park". The page opens for a community that is in no switcher. | 8.01.i, 8.01.g |
| FR-035 | The portfolio is never offered to anyone but a Company Administrator: no company entry and no Portfolio nav item, not even locked. A direct request is refused with 403 `FORBIDDEN`. | 8.01.i, 8.01.k, 8.01.o, 8.01.q |
| FR-036a | The landing order for a user with no usable home: 1 board mode, 2 company area, 3 "Your community isn't live yet", 4 "No home linked to this account". | 8.01.r; the landings themselves in 8.01.f, 8.01.d, 8.01.j, 8.01.m |
| FR-036b | Memberships in Onboarding communities count for the board-mode control, "My Communities", landing and the route guard. Only Archived communities are excluded. | 8.01.j, 8.01.m, 8.01.n |
| FR-012 (switchers) | An Archived community "MUST NOT appear in any board or manager community switcher or in 'My Communities'". | 8.01.o, and its absence in 8.01.m and 8.01.n |
| US6 #6 | "Given I am a Community Manager, When I use the app, Then the portfolio is never offered to me. When I request it directly, Then I am refused with 403 `FORBIDDEN`. My own communities stay in my existing switcher." | 8.01.i (never offered), 8.01.q (refused, landing with toast), 8.01.m (own communities stay) |
| US1 #4 (switcher) | Jordan added Keystone Ridge, so "it appears in my community switcher on my next request". Keystone Ridge is listed with an "Onboarding" chip. | 8.01.m, 8.01.n |
| US2 #1 (switcher) | Pat's pending invitation puts Keystone Park in no switcher ("A pending invitation does not count as a manager", FR-022). After Pat accepts, Pat holds an open-ended Community Manager membership and Keystone Park is Pat's board-mode community. | 8.01.j |
| US2 #2 | "Pat signs in to board mode for that community, although Pat has no linked home." | 8.01.j, 8.01.r |
| US2 #3 | Lee, a Board Member, is appointed co-manager directly from the people list and "holds an active Community Manager membership in A at once, with no invitation". Lee's board nav changes from locked to unlocked entries. | 8.01.k, 8.01.l |
| US9 #1 | "Given I am a Company Administrator with no linked home and no community membership, When I sign in, Then I land on the portfolio." Both from the usual sign-in page and from the company sign-in page. | 8.01.a, 8.01.f, 8.01.r |
| US9 #2 | "Given I hold an effective Community Manager membership in an Onboarding or Active community and no linked home, When I sign in, Then I land in board mode for that community." One community: Pat lands on Keystone Park. Several: Jordan lands on My Communities (spec 025 FR-025). | 8.01.j, 8.01.m, 8.01.r |

## 4. Artboards

All people, communities and dates come from conventions §4. "Now" is Monday, Oct 12, 2026, 10:00 AM EDT. Staff artboards are 1360 wide and as tall as the content. The flow is 1100 wide. Artboards are drawn at desktop width, plus the tablet and phone artboards of the main page that conventions §2 asks for.

### 4.1 Frames used in this brief

**Board-mode frame.** `WFShellBoard`, unchanged except for the data passed in:

- **Top bar cluster**, left to right: (Company Administrators only) the "Company" menu button; the mode control; alerts "🔔 3"; the avatar with the person's initials (RH, CN, JB, PR, LM, AQ). Everything sits to the left of alerts and avatar, per spec 025 FR-019.
- **Violet banner**, unchanged: dark ink on violet, no control.
- **Strip:** the active community's name in bold, then " · " and the person's role or roles in that community, in title case ("Community Manager", "Board Member · Community Manager"). If the community is Onboarding, an "Onboarding" chip follows the name (design choice D8). Leave out section 7's right-hand mono handle; in spec 030 the community name is the handle (FR-007). On "My Communities", where no community is active yet, the strip reads "No community chosen yet".
- **Left nav** from `boardNav()`, with two new data entries. Add them behind a new option that defaults off, so section 7's nav renders as before. Let `boardNav()` take a set of roles as well as one role, because Lee holds two (spec 025 FR-027, the union of active roles).
  - **"Add community"**, in the top ungrouped block, after "My Communities" when that item is shown (design choice D1). Unlocked when the person holds an effective Community Manager membership in any Onboarding or Active community (FR-004 (b), FR-006). This does not depend on the active community. Locked (🔒, "Not available to your role") otherwise.
  - **"Community settings"**, the last item of the "Community management" group. Unlocked when the person is a Community Manager of the active community (FR-029). Locked otherwise (spec 025 FR-040).
  - No "Portfolio" item, locked or not, for anyone (FR-035).
  - Everything else is section 7's nav. Finance items stay locked for Board Members, as in section 7.
- **Body:** a `wf-ph` placeholder naming the page and who draws it, for example "Community Home — section 7, unchanged" or "Community settings · Sakura Heights — briefs 04–07".

**Company-area frame.** Conventions §7.1, plus the company top bar below:

- No violet banner. The violet banner means board mode.
- **Strip:** "Juniper Row Community Management · Company area".
- **Left nav,** no group headings, in this order: Portfolio · Add community · Company defaults · Company administrators. No locks: everyone who sees this frame can use every entry.
- **Body:** the page title (`wf-h1`) and a dashed placeholder naming the brief that draws the page: "Portfolio — brief 02", "Add community — brief 03", "Company defaults — brief 08", "Company administrators — brief 09", "Community settings — briefs 04–07".

**Company top bar.** Spec 025's board-mode control, unchanged and only for board-eligible people: the "🗝️ Enter board mode" chip outside board mode (as in `BoardModeSwitch`), and "Resident · Board" in board mode. For Company Administrators only, a "Company" menu button sits to its left. Its menu lists: Portfolio · Add community · Company defaults · Company administrators. In the company shell, the "Company" button shows as current.

### 4.2 Design choices (each goes on a `DCPostIt`)

The spec doesn't decide these. Draw the first option. Put the question and the other options on a post-it next to the artboard named.

| # | Question | Drawn | Other options | Post-it beside |
| --- | --- | --- | --- | --- |
| D1 | Where does "Add community" sit in the board nav? | Top block, after "My Communities". | The last item of the nav, after "Reports". | 8.01.i |
| D2 | What does "Resident" do for a person with no usable home (Robin, Casey, Jordan, Pat)? The spec says the mode switch must work for them (FR-036a), but not what they then see. Their resident pages are refused, and the app sends them back to their FR-036a landing (FR-024g). | "Resident" shown as today, where spec 025 shows it. | Leave "Resident" out for people with no usable home. | 8.01.f |
| D3 | In board mode, does a Company Administrator who is not a Community Manager there (Casey) get "Community settings" and "Add community" as working links? The server would allow both, because Casey is a Company Administrator. | Locked, like any Board Member. The board nav follows community roles (spec 025 FR-027, FR-040). Casey uses the company area for both (FR-029, FR-036). | Unlocked links that open those pages in the company area. | 8.01.d |
| D4 | How different does the company sign-in page look from the usual sign-in page? | The usual sign-in layout (wf-auth), titled "Company sign-in" with the company name, and a line saying it is for Juniper Row staff. | Identical to the usual page except for the title. | 8.01.a |
| D5 | Is a quicker community switcher wanted? US6 #6 says "my existing switcher". Today, and in section 7, the only way to switch is "My Communities". | 8.01.n draws an optional menu on the strip's community name. "My Communities" stays either way. | No new control. "My Communities" is the only switcher. | 8.01.n |
| D6 | In the company area, how does the left nav show that a community's Community settings page is open? | "Portfolio" stays current. The breadcrumb names the community. | A child item under "Portfolio" with the community's name. | 8.01.g |
| D7 | Order of rows in "My Communities" and the switcher. | Alphabetical by community name. | Onboarding communities first, then alphabetical. | 8.01.m |
| D8 | Does the board-mode strip show the "Onboarding" chip? The conventions list switchers and "My Communities"; the strip shows the active community. | Yes, after the community name. | No chip in the strip. | 8.01.j |

### 4.3 Company entry (FR-036)

**8.01.a Company sign-in · bookmarkable page · Company Administrator**

- **Person:** Robin Hale, Company Administrator. No community membership. No home.
- **Shows:** the sign-in look of section 1 (`wf-auth.jsx`), not the app shell. Title "Company sign-in". Line under it: "For Juniper Row Community Management staff. Bookmark this page." Fields "Email" and "Password", button "Sign in", link "Forgot password?" (today's flow, unchanged). Below the card, small text: "Not company staff? Use the usual sign-in page." with a link. A canvas note shows the stable address, for example "/company/sign-in" (the exact path is a build decision).
- **After sign-in:** Robin lands in the company area, on the portfolio (8.01.f; FR-036c, FR-036a step 2).
- **States:** default.
- **Notes on canvas:** "Same accounts, passwords, lockout and rate limits as the usual sign-in page. The page grants nothing by itself (FR-036c)." Post-it D4.

**8.01.b Company sign-in · not a Company Administrator · Community Manager**

- **Person:** Jordan Blake, Community Manager, not a Company Administrator.
- **Shows:** where Jordan lands after signing in on the company sign-in page: Jordan's usual landing, board mode "My Communities" (board-mode frame), with a notice at the top of the body: "This account isn't a Company Administrator, so you've been signed in to your usual page."
- **Why here:** the company sign-in page only changes where a Company Administrator lands. Anyone else signs in normally (FR-036c, FR-036a).
- **States:** notice.

**8.01.c Company sign-in · sign-in failed · Company Administrator**

- **Person:** Robin Hale, wrong password.
- **Shows:** 8.01.a with today's generic sign-in failure message above the fields (spec 017; it never says whether the email has an account). A second note shows the rate-limit message from conventions §6.2: "Too many requests. Wait a few minutes, then try again."
- **States:** refusal.

**8.01.d Board mode · Company menu open · Company Administrator and Board Member**

- **Person:** Casey Novak.
- **Shows:** the board-mode frame with the company top bar. Strip "Keystone Crossing Master · Board Member". Nav as a Board Member with one community: no "My Communities"; "Add community" and "Community settings" locked (D3); Finance items locked as in section 7. Top bar: "Company" menu button (open), then "Resident · Board" with "Board" on, then alerts and avatar. The open menu has a small heading "Company area" and four items: Portfolio · Add community · Company defaults · Company administrators.
- **Why here:** Casey lands in board mode after signing in on the usual page (FR-036a step 1). The Company menu is how Casey gets to the company area.
- **States:** menu open.
- **Notes on canvas:** post-it D3.

**8.01.e Company area · company shell · Company Administrator and Board Member**

- **Person:** Casey Novak.
- **Shows:** the company-area frame with the company top bar: "Company" shown as current, then the "🗝️ Enter board mode" chip (spec 025, as drawn in `BoardModeSwitch`), then alerts and avatar. Nav: "Portfolio" current. Body: "Portfolio" title and placeholder.
- **States:** default.
- **Notes on canvas:** "'Enter board mode' takes Casey back to board mode (spec 025, unchanged)".

**8.01.f Company area · company shell · Company Administrator**

- **Person:** Robin Hale.
- **Shows:** as 8.01.e, but the cluster is "Company" (current), alerts and avatar only. No board-mode control of any kind (FR-036).
- **Why here:** Robin's landing after signing in on either sign-in page (FR-036a step 2, FR-036c, US9 #1).
- **Notes on canvas:** post-it D2.
- **States:** default.

**8.01.g Company area · Community settings open · Company Administrator**

- **Person:** Robin Hale. Community: Keystone Park (Onboarding, no manager, pat@example.com invitation pending).
- **Shows:** the company-area frame with the company top bar. Breadcrumb "Portfolio › Keystone Park". Nav: "Portfolio" stays current (D6). Body: title "Keystone Park" with its "Onboarding" chip, then the placeholder "Community settings — briefs 04–07".
- **Why here:** Company Administrators reach Community settings from the portfolio (FR-029). Keystone Park is in nobody's switcher, because nobody holds a membership in it yet, and the page still opens (FR-029: "addressed by community ID").
- **States:** default.
- **Notes on canvas:** post-it D6.

**8.01.h Company area · board page refused · Company Administrator**

- **Person:** Robin Hale.
- **Shows:** 8.01.f, with the toast "You don't have access to that page." near the top of the body.
- **Why here:** Robin opened a link to a board-mode page. The grant alone unlocks no board page (FR-003, FR-036). The request is refused with 403 `FORBIDDEN`, and the app takes Robin to the landing page (spec 025 FR-028), which for Robin is the portfolio (FR-036a).
- **States:** refusal.

### 4.4 Board-mode nav (FR-029, FR-036)

**8.01.i Board mode · Community settings and Add community · Community Manager**

- **Person:** Jordan Blake, open-ended Community Manager of Keystone Crossing Master, Keystone Crossing SF, Keystone Crossing TH, Sakura Heights and Keystone Ridge. No home. Avatar "JB". Active community: Sakura Heights.
- **Shows:** the board-mode frame. Mode control "Resident · Board", with "Board" on. No company entry in either option. Strip "Sakura Heights · Community Manager". Nav: "My Communities" (five communities); "Add community"; "Community Home"; the "Community management" group ending with "Community settings", which is current; Finance unlocked. No "Portfolio" item. Body: "Community settings · Sakura Heights — briefs 04–07".
- **States:** default.
- **Notes on canvas:** "No Company segment (A), no Company menu (B), no Portfolio item: Jordan isn't a Company Administrator (FR-035, US6 #6)". "Community settings opens the active community's page (FR-029)". Post-it D1.

**8.01.j Board mode · Onboarding community · Community Manager**

- **Person:** Pat Rivera, after accepting Robin Hale's invitation and creating an account. Open-ended Community Manager of Keystone Park only. No home. Avatar "PR".
- **Shows:** the board-mode frame. Mode control "Resident · Board", with "Board" on. Strip "Keystone Park", the "Onboarding" chip, then "· Community Manager". Nav: no "My Communities" (one community); "Add community" unlocked; "Community Home" current; "Community settings" unlocked; Finance unlocked. Body: "Community Home — section 7, unchanged".
- **Why here:** Pat signs in to board mode for Keystone Park with no home (US2 #2, US9 #2, FR-036a step 1). Keystone Park is Onboarding, and Onboarding memberships count for board mode (FR-036b). Before Pat accepted, the pending invitation put Keystone Park in no switcher (FR-022).
- **States:** default.
- **Notes on canvas:** post-it D8.

**8.01.k Board mode · locked entries · Board Member**

- **Person:** Lee Morales, Board Member of Sakura Heights (term ends Dec 31, 2027). Avatar "LM".
- **Shows:** the board-mode frame. Strip "Sakura Heights · Board Member". Nav: no "My Communities"; "Add community" locked; "Community Home" current; "Community settings" locked; Finance "AP Ledger" and "Vendor Aging" locked. No "Portfolio" item. Body: "Community Home — section 7, unchanged".
- **Why here:** board-nav entries the role doesn't confer are shown disabled with a lock (spec 025 FR-040). A Board Member who opens Community settings anyway is refused (FR-032); brief 04 covers that.
- **States:** default (locked entries).

**8.01.l Board mode · after co-manager appointment · Board Member and Community Manager**

- **Person:** Lee Morales, after Jordan Blake appointed Lee co-manager of Sakura Heights directly from its people list.
- **Shows:** 8.01.k with strip "Sakura Heights · Board Member · Community Manager". "Add community", "Community settings" and the Finance items are unlocked. The nav follows the union of Lee's roles (spec 025 FR-027).
- **Why here:** US2 #3: the appointment takes effect at once, with no invitation. The nav reflects it on Lee's next request.
- **States:** default.
- **Notes on canvas:** "Snapshot after US2 #3. Other briefs show Sakura Heights before it, with Jordan Blake the only open-ended manager."

### 4.5 Switchers (FR-012, FR-036b)

**8.01.m My Communities · Onboarding chip · Community Manager**

- **Person:** Jordan Blake.
- **Shows:** `BoardNavMulti`'s page layout in the board-mode frame. Strip "No community chosen yet". Nav: "My Communities" current. Title "My communities". Helper "Choose a community to work in." A table in a card. Columns: "Community" and "Your role". Do not reuse section 7's ✓ "Status" column, its manager and accountant columns, or "Over 30d". Rows, alphabetical (D7):

  | Community | Your role |
  | --- | --- |
  | Keystone Crossing Master | Community Manager |
  | Keystone Crossing SF | Community Manager |
  | Keystone Crossing TH | Community Manager |
  | Keystone Ridge `Onboarding` | Community Manager |
  | Sakura Heights | Community Manager |

  Each community name is a link. The "Onboarding" chip follows Keystone Ridge's name. Active rows have no chip (conventions §5.1).
- **Why here:** this is Jordan's landing after signing in: board mode, with two or more communities, lands on "My Communities" (FR-036a step 1, spec 025 FR-025, US9 #2). Keystone Ridge is listed because Jordan added it (US1 #4) and Onboarding counts (FR-036b).
- **States:** default.
- **Notes on canvas:** post-it D7.

**8.01.n Community switcher · open · Community Manager**

- **Person:** Jordan Blake, active community Sakura Heights.
- **Label the artboard "Design choice" (D5).**
- **Shows:** the board-mode frame as in 8.01.i, with "Community Home" current. The strip's community name is a menu button, "Sakura Heights ▾", shown open. Menu heading "Switch community". Items, alphabetical: Keystone Crossing Master · Keystone Crossing SF · Keystone Crossing TH · Keystone Ridge with the "Onboarding" chip · Sakura Heights marked "Current" in text. Footer link "All my communities", which opens "My Communities".
- **States:** menu open.
- **Notes on canvas:** post-it D5.

**8.01.o My Communities · archived left out · Accountant**

- **Person:** Avery Quinn, Accountant of Keystone Crossing Master (Active), Sakura Heights (Active) and Old Mill Commons (Archived since Sep 30, 2026). Avatar "AQ".
- **Shows:** as 8.01.m. Nav: "My Communities" current; "Add community" and "Community settings" locked; Finance unlocked. Two rows: Keystone Crossing Master · Accountant, and Sakura Heights · Accountant.
- **Why here:** Old Mill Commons is Archived, so it is not listed and does not count toward "more than one" (FR-012, FR-036b). Avery's membership there is kept but confers nothing.
- **States:** default.
- **Notes on canvas:** "Old Mill Commons is archived: not listed, not counted (FR-012)".

**8.01.p My Communities · load error · Community Manager**

- **Person:** Jordan Blake.
- **Shows:** 8.01.m's frame and title. In place of the table, inside the card: "Couldn't load your communities." and a "Try again" button. The nav, banner and top bar still work (conventions §7.7).
- **States:** error.

**8.01.q My Communities · portfolio refused · Community Manager**

- **Person:** Jordan Blake.
- **Shows:** 8.01.m, with the toast "You don't have access to that page." near the top of the body.
- **Why here:** Jordan opened a link to the portfolio. It is refused with 403 `FORBIDDEN` (FR-035, US6 #6). The app takes him to his landing page (spec 025 FR-028), which is "My Communities". His own communities are still there.
- **States:** refusal.

### 4.6 Sign-in landing order (FR-036a)

**8.01.r Sign-in landing order · flow**

- **Size:** 1100 wide, as tall as needed.
- **Shows:** a top-to-bottom flow of numbered boxes and arrows, in section 8's sketch style. Each outcome box names the landing and the dataset people who land there. Brief 11 draws the landing pages; here they are named only.

  1. **Start:** "Signs in".
  2. **Question 0:** "Has a home in an Active or Archived community?"
     - **Yes:** "As today (spec 025): the last-used mode decides. A resident lands on a home in an Active community first, then an Archived one, never an Onboarding one (FR-012a)." Example: Sam Ortiz → 711 Keystone Park Dr #29.
     - **No:** go to question 1.
  3. **Question 1:** "Holds an effective Board Member, Accountant or Community Manager membership in an Onboarding or Active community?"
     - **Yes:** "Board mode. One community: its Community Home. Two or more: My Communities." Examples: Casey Novak → Keystone Crossing Master; Jordan Blake → My Communities; Pat Rivera → Keystone Park; Jane Alvarez, after removal from #29 → Sakura Heights.
  4. **Question 2:** "Company Administrator?"
     - **Yes:** "Company area · Portfolio." Example: Robin Hale. (The company sign-in page leads here too, FR-036c.)
  5. **Question 3:** "Are all their homes in Onboarding communities?"
     - **Yes:** "“Your community isn't live yet” (brief 11)." Example: Ann Brooks.
  6. **Otherwise:** "“No home linked to this account” (brief 11)." Example: Dana Fox.

- **Footnotes on the artboard:**
  - "Effective means active and not past its end date."
  - "Pending invitations and memberships in Archived communities don't count." (Ann's pending Board Member invitation doesn't put her in board mode.)
  - "Token refresh, mode switch and the current-user check also work with no home (FR-036a)."
  - "A refused page request (403) returns the person to this landing (spec 025 FR-028)."
  - "Question 0 reads 'usable home' from FR-012a: sign-in picks an Active home first, then an Archived one; Onboarding homes are never picked."
- **States:** default.

## 5. Copy

Quote these exactly. Placeholders such as "Portfolio — brief 02" are canvas labels, not product copy.

**Company sign-in page (8.01.a–c)**

| Element | Text |
| --- | --- |
| Title | "Company sign-in" |
| Line under the title | "For Juniper Row Community Management staff. Bookmark this page." |
| Fields and button | "Email", "Password", "Sign in", "Forgot password?" (as today) |
| Link below the card | "Not company staff? Use the usual sign-in page." |
| Notice for someone who isn't a Company Administrator | "This account isn't a Company Administrator, so you've been signed in to your usual page." |
| Sign-in failure | Today's generic sign-in failure message, unchanged (spec 017) |

**Top bar**

| Element | Text |
| --- | --- |
| Company button | "Company". Accessible name: "Company area menu". |
| Company menu heading | "Company area" |
| Company menu items | "Portfolio", "Add community", "Company defaults", "Company administrators" |
| Board-mode chip outside board mode (spec 025, unchanged) | "🗝️ Enter board mode" |
| Mode switch failure (today's app copy, unchanged) | "Could not switch modes. Please try again." |

**Board mode**

| Element | Text |
| --- | --- |
| Banner (section 7, unchanged) | "Board mode · you are seeing association-wide data, not just your home" |
| Strip | "Sakura Heights · Community Manager" · "Keystone Park" + chip "Onboarding" + "· Community Manager" · "Keystone Crossing Master · Board Member" · "Sakura Heights · Board Member · Community Manager" |
| Strip on "My Communities" | "No community chosen yet" |
| New nav entries | "Add community" · "Community settings" |
| Locked entry reason (section 7, unchanged) | "Not available to your role" |

**Company area**

| Element | Text |
| --- | --- |
| Strip | "Juniper Row Community Management · Company area" |
| Left nav | "Portfolio" · "Add community" · "Company defaults" · "Company administrators" |
| Breadcrumb | "Portfolio › Keystone Park" |
| Page titles | "Portfolio" · "Add community" · "Company defaults" · "Company administrators" · the community's name on its Community settings page |

**Switchers**

| Element | Text |
| --- | --- |
| Page title (section 7) | "My communities". The nav item stays "My Communities". |
| Helper | "Choose a community to work in." |
| Table columns | "Community" · "Your role" |
| Role values | "Community Manager" · "Board Member" · "Accountant" |
| Status chip | "Onboarding" |
| Switcher button (design choice D5) | "Sakura Heights ▾". Accessible name: "Switch community. Current: Sakura Heights". |
| Switcher menu | Heading "Switch community" · marker "Current" · footer link "All my communities" |
| Load error | "Couldn't load your communities." · button "Try again" |

**Refusals** (conventions §6.1)

| Case | Text | Where |
| --- | --- | --- |
| `FORBIDDEN`, page: a non-administrator opens the portfolio or any company-area page; a Company Administrator with no membership opens a board page | "You don't have access to that page." | Toast on the landing page the app moves them to. Never names what was requested. |

**Flow (8.01.r):** the box and footnote texts in §4.6, word for word.

## 6. Interactions

**Company sign-in page**

- Signing in runs the usual sign-in (same rules, lockout and rate limits). Then a Company Administrator lands on the portfolio; anyone else lands on their usual FR-036a landing with the notice in §5. Focus moves to the new page's main heading.
- The page's address never changes, so it can be bookmarked. Opening it while already signed in as a Company Administrator goes straight to the portfolio.

**Company menu**

- "Company" opens a menu. Choosing an item opens the company shell on that page.
- Esc or clicking outside closes the menu. Focus returns to "Company".
- In the company shell, "Company" shows as current and still opens the menu.
- For Casey, "🗝️ Enter board mode" leaves the company shell for the board landing.

**Company area**

- The left nav entries are links. The current page is marked.
- A portfolio row (brief 02) opens that community's Community settings page in the company area, with the breadcrumb "Portfolio › <community name>". "Portfolio" in the breadcrumb goes back to the portfolio.
- The page opens for Onboarding and Archived communities that are in no switcher (FR-029).

**Board-mode nav**

- "Community settings" opens the Community settings page of the active community, inside board mode (FR-029; conventions §7.1).
- "Add community" opens the add-community form inside board mode (brief 03).
- Locked entries are not links and do nothing. They carry the reason "Not available to your role" (spec 025 FR-040).
- The nav is worked out from the person's current roles on each request. Lee's nav unlocks on his next request after the appointment (US2 #3). A person whose last manager membership ends sees the entries locked on their next request.

**My Communities and the switcher**

- Choosing a community makes it the active community and opens its Community Home, as today (spec 025). Onboarding communities open the same way (FR-011, FR-036b).
- In the switcher (D5), the current community is marked "Current". Choosing it closes the menu. "All my communities" opens "My Communities".
- "Try again" reloads the list in place.
- Lists and the board-mode control are worked out on each request. When a community is archived, it is gone from every switcher and "My Communities" on the person's next request (FR-012). If someone is still working in it, for example in an old tab, their next change there is refused with the staff `COMMUNITY_ARCHIVED` message in conventions §6.1. No artboard in this brief.

**Refusals and changes of role**

- A request for a page the person can't use is refused with 403 `FORBIDDEN`. The app takes them to their landing page and shows the toast (spec 025 FR-028). Focus goes to the landing page's main heading.
- When a Company Administrator's grant is revoked (US7 #2), the company entry disappears on their next request, and a company-area request gets the same refusal.
- When Pat creates an account from the invitation, Pat is signed in to board mode for Keystone Park (US2 #2), and the board-mode control shows for Pat from then on (FR-036b). Later sign-ins land there too (FR-036a step 1).

## 7. Accessibility

Conventions §8 applies. In this brief:

- **Company sign-in page:** a real form with labelled fields; the failure message is announced and tied to the form; the notice after a non-administrator signs in is announced and doesn't take focus.
- **Company menu and switcher (D5):** menu buttons that say whether they are open. Arrow keys move through the items. Enter chooses. Esc closes and returns focus to the button. The current item is marked "Current" in text.
- **Navigation:** the left nav is a navigation landmark with a name ("Board navigation", "Company area navigation"). The current page is marked as current. Locked entries are announced as unavailable with their reason, for example "Community settings, not available to your role". The lock icon only adds to that.
- **Breadcrumb:** a navigation landmark named "Breadcrumb". The last item is the current page.
- **Chips:** "Onboarding" is text. In a table it is read with the name: "Keystone Ridge, Onboarding".
- **Tables:** "My communities" has real header cells, and the page title names the table.
- **Refusal toast:** announced politely after the redirect. Focus is on the landing page's main heading. The toast never holds focus and needs no action.
- **Board banner:** unchanged, dark ink on violet (spec 025 FR-038).
- **Flow artboard:** numbered boxes read top to bottom, so the order is clear without the arrows.

## 8. Out of scope for this design

- The page bodies other briefs own: the portfolio (02), Add community (03), Community settings and its sections and status dialogs (04–07), Company defaults (08), Company administrators (09), invitation pages (10), the landing pages "Your community isn't live yet" and "No home linked to this account", and the resident home switcher with "No longer managed" (11), and emails (12).
- The Community Manager's Onboarding notice on Profile (FR-029). Brief 04 draws it.
- A Board Member refused on the Community settings page (FR-032, US5 #3). Brief 04 draws it.
- Resident-mode screens. Nobody in this brief is drawn in resident mode.
- Phone and tablet artboards beyond the main page's two (conventions §2).
- Notifications, in any nav or section (FR-028, FR-029).
- Changes to the board banner, section 7's nav entries, or section 7's artboards.
- Today's app has separate "Memberships" and "ARC Settings" nav entries, which section 7 never drew. US5 folds both pages into Community settings, and FR-030 sends the old ARC settings location to its Architectural review section. That redirect is behaviour, not a screen. Draw only "Community settings".
- How the mode is stored, tokens, and route guards. These are build details, not screens.

## 9. Design review checklist

- [ ] The company sign-in page has a stable, bookmarkable address, uses the usual sign-in rules, lands a Company Administrator on the portfolio and anyone else on their usual page with the notice (8.01.a–c; FR-036c).
- [ ] The "Company" menu sits in the top-bar account cluster, left of alerts and avatar, and opens the company shell (8.01.d–f; FR-036; spec 025 FR-019).
- [ ] Robin Hale sees no "Board" option, no "Enter board mode" and no board nav in any artboard (FR-036, FR-003).
- [ ] A board-page link opened by Robin is refused, and Robin is back on the portfolio with "You don't have access to that page." (8.01.h; FR-036, spec 025 FR-028).
- [ ] Casey Novak sees both the Company menu and the board-mode control (8.01.d, 8.01.e; FR-036).
- [ ] The company-area nav is exactly Portfolio, Add community, Company defaults and Company administrators, with no locks and no violet banner (FR-036; conventions §7.1).
- [ ] Community settings opens in the company area from the portfolio, with "Portfolio › Keystone Park", for a community in no switcher (8.01.g; FR-029).
- [ ] Jordan Blake's top bar has no company entry, and no board nav anywhere has a "Portfolio" item, locked or not (8.01.i, 8.01.k, 8.01.o; FR-035, US6 #6).
- [ ] Jordan's direct portfolio request lands him on "My Communities" with "You don't have access to that page." (8.01.q; US6 #6, FR-035).
- [ ] "Community settings" in board nav is unlocked for a Community Manager of the active community and opens that community's page; it is locked for Board Members and Accountants (8.01.i–8.01.l, 8.01.o; FR-029, spec 025 FR-040).
- [ ] "Add community" in board nav is unlocked for Community Managers and locked for others (8.01.i–8.01.l; FR-036).
- [ ] Pat Rivera, with no home, is in board mode for Keystone Park, with no "My Communities" item and an "Onboarding" chip in the strip (8.01.j; US2 #1, US2 #2, US9 #2, FR-036b).
- [ ] Lee Morales's entries are locked as a Board Member and unlocked after the co-manager appointment, with both roles in the strip (8.01.k, 8.01.l; US2 #3).
- [ ] Jordan's "My Communities" lists five communities, with Keystone Ridge carrying an "Onboarding" text chip (8.01.m; US1 #4, FR-036b).
- [ ] The switcher artboard is labelled "Design choice" and lists the same five communities, with "Current" in text (8.01.n; D5).
- [ ] Avery Quinn's "My Communities" lists Keystone Crossing Master and Sakura Heights only; Old Mill Commons is absent (8.01.o; FR-012).
- [ ] No status is shown by a ✓ or by colour alone; section 7's "Status" column is not reused (conventions §5.1).
- [ ] The load-error state keeps the frame and offers "Try again" (8.01.p; conventions §7.7).
- [ ] The flow shows FR-036a's order (board mode, company area, "Your community isn't live yet", "No home linked to this account") after the usable-home question, with the dataset examples (8.01.r; FR-036a, US9 #1, US9 #2).
- [ ] D1–D8 are each on a post-it beside the named artboard (conventions §2).
- [ ] Only conventions §4 people and communities appear. No pending invitee is shown by name (FR-018).
- [ ] Sections 1–7 render as before, and ids s8-01a to s8-01r are unique (conventions §2, §3).
