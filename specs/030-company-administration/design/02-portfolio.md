# 02 — Portfolio

This brief is for the Company Administrator's portfolio: the list of every community the company has. §1 is the prompt to paste into Claude Design. The other sections hold the details that the prompt points to. Attach [`spec.md`](../spec.md), [`00-conventions.md`](./00-conventions.md) and this file.

## 1. Prompt for Claude Design

```text
Project: "HOA Management CRM". Canvas: NekoHOA Wireframes.html. Put every new artboard in section "8 · Company administration" (DCSection id "company-admin"). If the section doesn't exist yet, add it after section 7, as 00-conventions.md §3 describes.

Attached: spec.md (feature spec 030, the authority), 00-conventions.md (shared rules, dataset, vocabulary, refusal copy) and 02-portfolio.md (this brief). Details are in the brief's §4 (artboards and rows), §5 (exact copy) and §6 (interactions). If the files disagree, spec.md wins, then the conventions.

Draw the Portfolio: the Company Administrator's list of every community. Use the neutral company-area frame from conventions §7.1, with avatar "RH" (Robin Hale), no violet banner and no board-mode control. If section 8 already has that frame, reuse it.

Artboards, 1360 wide, as tall as the content, ids s8-02a to s8-02k, labels exactly:
8.02.a Portfolio · first page · Option A · Company Administrator (A–Z by community name)
8.02.b Portfolio · first page · Option B · Company Administrator ("No manager" rows first, then A–Z)
8.02.c Portfolio · Archived filter · Company Administrator
8.02.d Portfolio · Onboarding filter · Company Administrator
8.02.e Portfolio · search “Keystone” · Company Administrator
8.02.f Portfolio · no search results · Company Administrator
8.02.g Portfolio · loading · Company Administrator
8.02.h Portfolio · load error · Company Administrator
8.02.i Portfolio · no communities yet · Company Administrator
8.02.j Board mode · no portfolio entry · Community Manager
8.02.k Portfolio requested directly · refused · Community Manager

Options A and B are alternatives for the default sort order. Draw both. 8.02.c–8.02.i use Option A.

Reuse: BoardArchApps (filter buttons with counts, search field, table in a card), BoardVendors (card header bar), wf-pill for status chips and flags, wf-ph for loading rows, and WFShellBoard + boardNav() + BoardNavMulti for 8.02.j–k with Jordan Blake's data passed in. Sections 1–7 must render exactly as before.

Add two DCPostIts next to 8.02.a: the MVP note (brief §4.3) and the open questions (brief §4.4).

Fidelity: lofi, the same visual language as section 7. Layout, columns, copy, states and who sees what are binding. Colour and spacing are placeholders. Status and flags are always words, never a ✓ or a coloured dot alone.

Do not draw: row actions other than the "Community settings" link (no Mark Active, Archive or Restore in rows), column sorting, count tiles, a "No manager" filter, a page-size picker, a locked "Portfolio" entry in board mode, a name for a pending invitee, end-dated managers, expired invitations, Board Member invitations in the Managers column, a phone layout.

When you finish, open each artboard and check that it renders with no console errors, that its rows and counts match brief §4, and that its copy matches brief §5 word for word. Tell me anything you couldn't draw.
```

## 2. Who uses it and why

- **Company Administrators** (Robin Hale; Casey Novak). The portfolio is their home in the company area. Robin lands on it after sign-in, because Robin has no home and no community membership (US9 #1, FR-036a). They use it to:
  - spot every community with no open-ended manager, in one step (SC-009);
  - see each community's status, parent and managers;
  - spot communities whose architectural review settings differ from the company defaults;
  - open a community's **Community settings** page;
  - start **Add community**.
- **Community Managers** (Jordan Blake) never see it. They are not offered it, and a direct request is refused (FR-035, US6 #6). They keep using "My Communities" and their switcher.
- **Board Members, Accountants and Residents** never see it either. A direct request gets the same refusal (FR-035).

The page only lists and links. It changes no data. Status actions live in the Community settings header (brief 04).

## 3. Spec traceability

| Spec item | What the design must show | Artboards |
| --- | --- | --- |
| US6 #1 | A community with no open-ended Community Manager is flagged "No manager" in its row: Briar Hollow. | 8.02.a, b, d |
| US6 #2 | A community whose only manager is a pending invitation is flagged "No manager" and shows "invitation pending": Keystone Park, "pat@example.com · invitation pending". Email only, never "Pat Rivera". | 8.02.a, b, d, e |
| US6 #3 | A community whose architectural review settings differ from the company defaults shows "Differs from defaults": Keystone Crossing SF. | 8.02.a, b, e |
| US6 #4 | The Archived filter lists only archived communities: Old Mill Commons and Old Mill Commons Villas, "Showing 1–2 of 2". | 8.02.c |
| US6 #5 | Search "Keystone" lists exactly the five communities whose community name or legal name contains it. The field's label and placeholder say it searches community or legal name. | 8.02.e; 8.02.f for no results |
| US6 #6 | A Community Manager is never offered the portfolio: no entry in board-mode nav (not even a locked one), none in the top bar. Their own communities stay in "My Communities". A direct request takes them to their landing page with "You don't have access to that page." | 8.02.j, k |
| US6 Independent Test | Communities in each status; one with no manager; one that differs from defaults; the status filter; search; a Community Manager not offered the view and refused when requesting it. | 8.02.a–e, j, k |
| FR-034 | Columns: community name, legal name, status, parent, open-ended managers and pending manager invitations, the "No manager" flag, the "Differs from defaults" flag. Status filter, search by community or legal name, pagination of 25. End-dated managers are not listed: Sakura Heights shows Jordan Blake only, not Taylor Grant. | 8.02.a–f |
| FR-035 | Not offered to anyone but a Company Administrator. A direct request is refused with 403 `FORBIDDEN`, shown as the §6.1 page refusal. | 8.02.j, k |
| FR-036 (portfolio part) | The company area holds the portfolio and Add community. "Add community" is the page's primary action. | 8.02.a–i |
| US1 #1 (appears in portfolio) | Keystone Park, added by Robin Hale on Oct 11, 2026, is in the list: status "Onboarding", parent "—", "No manager" (zero Community Manager memberships), and no "Differs from defaults" flag (its settings were copied from the company defaults). | 8.02.a, b, d |
| US3 #14 (No manager after restore) | Briar Hollow was archived and then restored on Oct 5, 2026. Reese Walker's term ended while it was archived, so it returned to Onboarding. Its row reads "Onboarding" and "No manager". The `NO_ACTIVE_MANAGER` refusal on Mark Active is drawn in brief 04, not here. | 8.02.a, b, d |
| SC-008 | The first page of 25 is all that loads. The frame, title, filters and search appear at once; placeholder rows stand in for the table. Nothing on the page needs every community at once: no "Show all" and no page size above 100. | 8.02.a, g |
| SC-009 | "No manager" is a word in the row, seen without opening anything. Both no-manager rows are on the first page. Option B guarantees this for any data, not just the sample. | 8.02.a, b, d |
| Spec independence, MVP foundation (d) | The minimal list: community name, status chip, link to Community settings. | DCPostIt by 8.02.a |
| Constitution, Accessibility | The portfolio is fully keyboard operable. Status, "No manager" and "Differs from defaults" are text, not colour. | All, see §7 |

## 4. Artboards

### 4.1 The page (8.02.a–8.02.i)

**Frame.** The neutral company-area frame from conventions §7.1:

- the top bar with the account cluster and avatar "RH". There is no board-mode control, because Robin holds no community membership (FR-036). Where the company entry sits is brief 01's choice; this brief doesn't change it.
- a `wf-strip` reading "Juniper Row Community Management · Company area";
- a left nav: Portfolio (current), Add community, Company defaults, Company administrators;
- no violet banner.

**Body, top to bottom.**

1. **Header row.** Title "Portfolio". Under it the subtitle "Every community of Juniper Row Community Management, including archived ones." On the right, the primary button "Add community".
2. **Controls row.** On the left, the status filter: a group of four buttons labelled "Filter by status": "All · 31", "Onboarding · 3", "Active · 26", "Archived · 2". The pressed one looks different by more than colour (for example filled and bold). On the right, the search field with the visible label "Search communities" and the placeholder "Search by community or legal name".
3. **Table card** (`wf-card` with `wf-table`). A header bar reading "Communities", as in `BoardVendors`. The table has these columns, in this order:

   | Column | Shows |
   | --- | --- |
   | Community | The community name, in bold. |
   | Legal name | The legal name, or "—" when it is empty (spec 025 backfill). |
   | Status | A text chip: "Onboarding", "Active" or "Archived". |
   | Parent | The parent's community name, or "—". Plain text. |
   | Managers | Each open-ended Community Manager by name, one per line. Then each pending Community Manager invitation as "pat@example.com · invitation pending". When there is no open-ended manager, a "No manager" chip comes first. |
   | Architectural review | A "Differs from defaults" chip, or "—" when the settings match the company defaults. |
   | Settings | A "Community settings" link. |

4. **Footer** inside the card: "Showing 1–25 of 31" on the left; "Previous", "1", "2", "Next" on the right. "Previous" is disabled on page 1.

**Column rules.**

- **Managers** lists only open-ended managers and pending **Community Manager** invitations (FR-034). It never lists:
  - a manager with an end date (Taylor Grant at Sakura Heights);
  - an expired or revoked invitation;
  - a Board Member or Accountant invitation (Ann Brooks's Board Member invitation to Briar Hollow is not shown);
  - a name for an invitee. Invitees are emails until they accept (FR-018).
- **"No manager"** means no open-ended Community Manager membership. A pending invitation doesn't count (FR-020, FR-034). So Keystone Park shows both the flag and the invitation.
- **"Differs from defaults"** compares the community's architectural review settings with the current company defaults. Keystone Crossing TH has never saved its settings, so it reads spec 027's built-in values. These equal today's defaults, so it has no flag.
- **Archived rows** look like the others: no greying, so text keeps its contrast. The "Archived" chip says it. Their "Community settings" link opens the read-only view (brief 04).
- No cell is ever empty. "—" stands for none, with screen-reader text (§7).

**Design choice: where the flags sit.** Drawn: "No manager" in the Managers column, and "Differs from defaults" in the Architectural review column, so each flag sits next to what it describes. The other option is one "Flags" column holding both chips. Not drawn.

**Design choice: one link per row.** Drawn: a "Community settings" link in its own column, and the community name as plain text. The other option is to make the community name the link and drop the Settings column. Either way there is one link per row.

**Rows: the nine named communities** (conventions §4.3). All are on page 1.

| Community | Legal name | Status | Parent | Managers | Architectural review | Situation |
| --- | --- | --- | --- | --- | --- | --- |
| Briar Hollow | Briar Hollow Homeowners Association, Inc. | Onboarding | — | "No manager" | — | No manager after restore (US3 #14, US6 #1). |
| Keystone Crossing Master | Keystone Crossing Owners Association, Inc. | Active | — | Jordan Blake | — | Normal. |
| Keystone Crossing SF | Keystone Crossing SF Association, Inc. | Active | Keystone Crossing Master | Jordan Blake | "Differs from defaults" | Differs from defaults (US6 #3). |
| Keystone Crossing TH | — | Active | Keystone Crossing Master | Jordan Blake | — | Empty legal name (backfill). |
| Keystone Park | Keystone Park HOA, Inc. | Onboarding | — | "No manager", then "pat@example.com · invitation pending" | — | Only a pending invitation (US6 #2); just added (US1 #1). |
| Keystone Ridge | Keystone Ridge HOA, Inc. | Onboarding | — | Jordan Blake | — | Added by a Community Manager, waiting to be marked Active. |
| Old Mill Commons | Old Mill Commons Association, Inc. | Archived | — | Morgan Ellis | — | Archived. |
| Old Mill Commons Villas | Old Mill Commons Villas Association, Inc. | Archived | Old Mill Commons | Morgan Ellis | — | Archived sub-association. |
| Sakura Heights | Sakura Heights Homeowners Association, Inc. | Active | — | Jordan Blake | — | Taylor Grant ends Dec 31, 2026, so is not listed. |

**Filler rows.** The other 22 communities are Active, each with one open-ended manager (a made-up name) and no flag. Their names must not contain "Keystone". At least 6 must sort after "Sakura Heights" (for example names starting with T to Z), so that page 1 under Option A holds all nine named communities. Page 1 then has 9 named rows and 16 filler rows; page 2 has 6 filler rows.

Keystone Ridge's row shows nothing about who added it. The spec doesn't list that in the portfolio. Every Company Administrator was emailed when Jordan added it (FR-008; brief 12).

### 4.2 Artboard list

**8.02.a Portfolio · first page · Option A · Company Administrator**

- Viewpoint: Robin Hale.
- Filter "All · 31" pressed. Search empty.
- Rows sorted A–Z by community name. Page 1 of 2: "Showing 1–25 of 31".
- Every situation in §4.1 is visible on this page.
- Design choice: Option A, the default sort order.

**8.02.b Portfolio · first page · Option B · Company Administrator**

- Same as 8.02.a, except the order: rows flagged "No manager" first (Briar Hollow, then Keystone Park), then every other row A–Z.
- Design choice: Option B, the default sort order. With Option A, a no-manager community can fall on page 2 when the portfolio is large. Option B always puts them first (SC-009).

**8.02.c Portfolio · Archived filter · Company Administrator**

- "Archived · 2" pressed.
- Rows: Old Mill Commons, Old Mill Commons Villas.
- Footer: "Showing 1–2 of 2". Design choice: when everything fits on one page, the footer shows only the "Showing" line. The other option keeps a disabled Previous and Next.

**8.02.d Portfolio · Onboarding filter · Company Administrator**

- "Onboarding · 3" pressed.
- Rows: Briar Hollow ("No manager"), Keystone Park ("No manager", "pat@example.com · invitation pending"), Keystone Ridge (Jordan Blake).
- Footer: "Showing 1–3 of 3".
- This artboard puts the three Onboarding situations side by side: no manager after a restore, only a pending invitation, and a community with a manager that is waiting to be marked Active.

**8.02.e Portfolio · search “Keystone” · Company Administrator**

- The search field holds "Keystone". It has a "Clear search" button.
- Filter "All" pressed. Filter counts follow the search: "All · 5", "Onboarding · 2", "Active · 3", "Archived · 0" (design choice, see §4.4).
- Rows: Keystone Crossing Master, Keystone Crossing SF, Keystone Crossing TH, Keystone Park, Keystone Ridge.
- Footer: "Showing 1–5 of 5".

**8.02.f Portfolio · no search results · Company Administrator**

- The search field holds "Keystone Pk".
- Filter counts: "All · 0", "Onboarding · 0", "Active · 0", "Archived · 0".
- In place of the table, inside the card: "No communities match “Keystone Pk”." and a "Clear search" button. No table headers, no footer.

**8.02.g Portfolio · loading · Company Administrator**

- The frame, title, "Add community", filter buttons and search field are drawn at once. The filter counts are `wf-ph` placeholders.
- The table card has its header bar, the column headers and about 6 `wf-ph` placeholder rows.
- No footer text yet.

**8.02.h Portfolio · load error · Company Administrator**

- The frame, title, "Add community", filters and search stay usable.
- Inside the card, in place of the table: "Couldn't load communities." and a "Try again" button.

**8.02.i Portfolio · no communities yet · Company Administrator**

- The company has no communities. This happens on a new deployment before anyone adds one.
- No filter buttons and no search, because there is nothing to filter. Design choice; the other option shows them with zero counts.
- Inside the card: "Juniper Row Community Management has no communities yet." and an "Add community" button. The header's "Add community" stays too.

**8.02.j Board mode · no portfolio entry · Community Manager**

- Viewpoint: Jordan Blake, in board mode.
- Frame: `WFShellBoard`, with the violet banner unchanged. Pass Jordan's data in:
  - strip: "Keystone Crossing Master · Community Manager";
  - avatar "JB";
  - nav from `boardNav()` with role manager and 5 communities, so "My Communities" is shown and current. Brief 01 adds "Community settings" and "Add community" as nav data.
- What must be absent:
  - "Portfolio", "Company defaults" and "Company administrators" appear nowhere: not in the nav, not locked, not disabled;
  - the top bar has no company entry.
- Body: the "My Communities" list, as brief 01 draws it. If brief 01 isn't drawn yet, use `BoardNavMulti`'s layout with two columns, Community and Role. Rows, each with role "Community Manager": Keystone Crossing Master, Keystone Crossing SF, Keystone Crossing TH, Keystone Ridge (with an "Onboarding" chip), Sakura Heights. Active rows have no chip (conventions §5.1). Don't use the ✓ status column.

**8.02.k Portfolio requested directly · refused · Community Manager**

- Jordan typed the portfolio's address. The server refused it with 403 `FORBIDDEN` (FR-035).
- Draw the same page as 8.02.j, Jordan's landing in board mode, with the toast "You don't have access to that page." If brief 01 draws a different landing page for Jordan, use that page.
- No portfolio content appears, not even briefly.
- Board Members, Accountants and Residents get the same refusal on their own landing page. Not drawn.

### 4.3 MVP note (DCPostIt by 8.02.a)

> **MVP portfolio.** The spec's MVP foundation, item (d), ships a minimal list first: community name, status chip and the "Community settings" link. "Add community" is part of the MVP too (US1). Keep the 25-row footer, because the API contract pages every collection at 25. Legal name, parent, managers, both flags, the status filter and search arrive with US6. Nothing extra is drawn for the MVP.

### 4.4 Open questions (DCPostIt by 8.02.a)

> 1. **Default sort order.** Option A, A–Z by community name (8.02.a), or Option B, "No manager" first (8.02.b)? The spec sets no order.
> 2. **Filter counts while searching.** Drawn: the counts follow the search. Or should they always count the whole portfolio?
> 3. **A "No manager" filter.** It would make SC-009 one click away on a portfolio of any size. FR-034 lists only a status filter, so it isn't drawn.
> 4. **Onboarding rows that already have a manager** (Keystone Ridge). Drawn: no extra text. Or add a second line such as "Has a manager · can be marked Active"? The spec doesn't ask for it.
> 5. **Managers of Archived communities.** Morgan Ellis is listed for Old Mill Commons, but the membership confers nothing while it is archived (FR-012). Drawn: listed plainly. Or add "No access while archived"? The spec doesn't say.

## 5. Copy

Exact strings. Quotes mark visible text. "SR" means text for screen readers only.

**Page**

| Element | Text |
| --- | --- |
| Page title | "Portfolio" |
| Subtitle | "Every community of Juniper Row Community Management, including archived ones." |
| Primary button | "Add community" |
| Strip | "Juniper Row Community Management · Company area" |
| Left nav | "Portfolio" · "Add community" · "Company defaults" · "Company administrators" |

**Filter and search**

| Element | Text |
| --- | --- |
| Filter group label (SR) | "Filter by status" |
| Filter buttons | "All · 31" · "Onboarding · 3" · "Active · 26" · "Archived · 2" |
| Filter button names (SR) | "All, 31 communities" · "Onboarding, 3 communities" · "Active, 26 communities" · "Archived, 2 communities" |
| Search label | "Search communities" |
| Search placeholder | "Search by community or legal name" |
| Clear button | "Clear search" |

**Table**

| Element | Text |
| --- | --- |
| Card header (and the table's name) | "Communities" |
| Column headers | "Community" · "Legal name" · "Status" · "Parent" · "Managers" · "Architectural review" · "Settings" |
| Status chips | "Onboarding" · "Active" · "Archived" |
| No-manager flag | "No manager" |
| Pending manager invitation | "pat@example.com · invitation pending" (SR: "pat@example.com, invitation pending") |
| Differs flag | "Differs from defaults" |
| Empty legal name | "—" (SR: "No legal name") |
| No parent | "—" (SR: "No parent") |
| Matches defaults | "—" (SR: "Same as company defaults") |
| Row link | "Community settings" (SR: "Community settings for Keystone Park") |

**Pagination**

| Element | Text |
| --- | --- |
| Footer | "Showing 1–25 of 31" · "Showing 1–2 of 2" · "Showing 1–3 of 3" · "Showing 1–5 of 5" |
| Buttons | "Previous" · "1" · "2" · "Next" (SR: "Previous page", "Page 1", "Page 2", "Next page") |
| Pagination landmark (SR) | "Pages" |

**Empty, loading and error**

| State | Text | Action |
| --- | --- | --- |
| Search, no results | "No communities match “Keystone Pk”." | "Clear search" |
| Search and filter, no results (not drawn) | "No Archived communities match “Keystone”." | "Clear search" |
| Filter, no rows (not drawn) | "No Archived communities." Same pattern for Onboarding and Active. | None |
| No communities at all | "Juniper Row Community Management has no communities yet." | "Add community" |
| Loading (SR) | "Loading communities…" | None |
| Load error | "Couldn't load communities." | "Try again" |

**Announcements (SR, polite)**

- After a filter, search or page change: "Showing 1–5 of 5 communities." (using the new numbers).
- After a search with no results: "No communities match “Keystone Pk”."

**Refusal** (conventions §6.1, `FORBIDDEN`)

| Case | Text | Where |
| --- | --- | --- |
| Anyone but a Company Administrator requests the portfolio | "You don't have access to that page." | Toast on their landing page (spec 025 FR-028). |

Conventions §6.1 names a toast for this case, while §7.8 says a toast never carries a refusal. This brief follows §6.1 because it names this exact case, and the person has nothing to act on. The message never names a community or anything else from the request.

## 6. Interactions

**Add community.** Opens the Add community page (brief 03). Brief 03 owns the form and its success state. When the person comes back to the portfolio, the new community is in the list as "Onboarding", in its sort position.

**Status filter.**

- Pressing a button shows only communities in that status. "All" shows every status, Archived included.
- Exactly one button is pressed at a time. "All" is pressed when the page opens.
- The list returns to page 1. Focus stays on the pressed button. The new count is announced (§5).
- The filter and the search work together. Example: Onboarding plus "Keystone" gives Keystone Park and Keystone Ridge. The spec lists both controls but doesn't say how they combine. Combining them is a design choice.

**Search.**

- Matches community name or legal name, ignoring case, and matches part of a word (FR-034, US6 #5).
- The list updates as the person types, after a short pause. The spec's scenario reads "I type … When the list filters". The other option is to search on Enter.
- Each change returns to page 1 and announces the count.
- "Clear search" empties the field, shows the unsearched list, and puts focus back in the search field.
- Leading and trailing spaces are ignored.

**Pagination.**

- 25 rows per page (FR-034). The server allows at most 100. A page-size picker (25, 50, 100) is optional and not drawn.
- "Previous", "Next" and the page numbers change the page. The current page number is marked as current.
- After a page change, focus moves to the table's heading "Communities", so keyboard users start at the top of the new page. The new "Showing" line is announced.

**Community settings link.**

- Opens that community's Community settings page at its Profile section (brief 04), with the breadcrumb "Portfolio › Keystone Park".
- An Archived community opens read-only (conventions §7.3).
- Going back returns to the portfolio with the same filter, search and page. Design choice; the other option is to reset to page 1 of "All".

**Flags** are not links. "No manager" and "Differs from defaults" are labels only. Design choice; one option for the owner is to link "Differs from defaults" to that community's Architectural review section (brief 07).

**No row actions.** Mark Active, Archive and Restore are in the Community settings header (brief 04), not in the portfolio. The portfolio has no confirmation dialogs, because it changes nothing.

**Try again** reloads the list only. The filter and search the person chose are kept.

**Refusal (8.02.k).**

- Anyone who isn't a Company Administrator and requests the portfolio is refused with 403 `FORBIDDEN` (FR-035).
- The app takes them to their landing page (spec 025 FR-028; FR-036a) and shows the toast "You don't have access to that page."
- The same happens to a Company Administrator whose role was revoked while the page was open, on their next filter, search or page change (FR-037: changes take effect on the next request). Not drawn.

**Not offered (8.02.j).** The client hides every route to the portfolio from people who aren't Company Administrators. The server still decides; 8.02.k is what happens when the address is reached anyway (conventions §7.10).

## 7. Accessibility

The conventions §8 baseline applies. For this page:

- **Headings.** The page has one `h1`, "Portfolio". The table is named by its heading "Communities".
- **Keyboard order:** left nav → "Add community" → filter buttons → search field → "Clear search" (when shown) → each row's "Community settings" link → pagination. Each row has one tab stop. Focus is always visible.
- **Filter buttons** are a group named "Filter by status". Each button reports whether it is pressed. The pressed state is shown by more than colour (for example a fill and bold text). Each button's name includes its count, for example "Onboarding, 3 communities".
- **Search** has a visible label, "Search communities". The placeholder doesn't replace it.
- **Table.** Column headers are real header cells. The Community cell is the row header, so a screen reader reads "Keystone Park, Status, Onboarding".
- **Words, not colour.** Status chips, "No manager" and "Differs from defaults" are text. Colour and icons only support them. Never a ✓ or a dot alone.
- **No silent cells.** "—" has screen-reader text: "No legal name", "No parent", "Same as company defaults".
- **Link names make sense out of context:** "Community settings for Keystone Park".
- **Announcements.** Filter, search and page changes announce the new "Showing" line, politely. "No communities match …" is announced. The loading state announces "Loading communities…". The load error is announced at once.
- **Focus moves:** to the "Communities" heading after a page change; to the search field after "Clear search"; it stays on the pressed filter button after a filter change.
- **Toast (8.02.k)** is announced politely. It doesn't take focus. It stays on screen long enough to read, and the message isn't needed to continue.
- **Contrast:** 4.5:1 for chip text and "—" cells; 3:1 for control borders and the pressed-filter indicator. Archived rows aren't dimmed. In board mode (8.02.j–k), the banner stays dark ink on violet (spec 025 FR-038).
- **Zoom.** At 200% zoom the controls row wraps. The table may scroll sideways inside its card, but the page itself doesn't.

## 8. Out of scope for this design

- The company entry in the top bar and the sign-in landing flow (brief 01).
- The Add community form and its success state (brief 03).
- Community settings, the status actions and their dialogs, including the `NO_ACTIVE_MANAGER` refusal for Briar Hollow and Keystone Park (brief 04).
- Row actions other than "Community settings": no Mark Active, Archive, Restore, Change parent or Appoint in the portfolio.
- Count tiles (`HeroStat`), a "No manager" filter, column sorting, a page-size picker, export, and bulk actions. None is in FR-034.
- Extra columns: property counts, archived or activated dates, "added by", end-dated managers, expired or revoked invitations, Board Member and Accountant invitations.
- Any view of the portfolio for Community Managers, Board Members, Accountants or Residents.
- Phone layout (staff pages are desktop-first, spec Assumptions).
- Notifications: nothing about it appears here.

## 9. Design review checklist

- [ ] **US6 #1.** Briar Hollow's row shows the word "No manager" (8.02.a, b, d).
- [ ] **US6 #2.** Keystone Park's row shows "No manager" and "pat@example.com · invitation pending". No name appears for the invitee (8.02.a, b, d, e).
- [ ] **US6 #3.** Keystone Crossing SF shows "Differs from defaults". No other named row does (8.02.a, b, e).
- [ ] **US6 #4.** With "Archived · 2" pressed, exactly Old Mill Commons and Old Mill Commons Villas are listed (8.02.c).
- [ ] **US6 #5.** Searching "Keystone" lists exactly the five Keystone communities. The label and placeholder say it searches community or legal name (8.02.e).
- [ ] **US6 #5, no results.** "No communities match “Keystone Pk”." with "Clear search", and no empty table (8.02.f).
- [ ] **US6 #6, not offered.** Jordan's board mode has no Portfolio, Company defaults or Company administrators entry, not even locked, and no company entry in the top bar. "My Communities" lists Jordan's five communities, with "Onboarding" on Keystone Ridge (8.02.j).
- [ ] **US6 #6 and FR-035, refused.** A direct request lands Jordan on the board-mode landing page with "You don't have access to that page." No portfolio content shows (8.02.k).
- [ ] **FR-034, columns.** Community, Legal name, Status, Parent, Managers, Architectural review, Settings, in that order.
- [ ] **FR-034, managers.** Only open-ended managers and pending Community Manager invitations are listed. Sakura Heights shows Jordan Blake only, not Taylor Grant. Briar Hollow doesn't show Ann's Board Member invitation.
- [ ] **FR-034, filter.** "All · 31", "Onboarding · 3", "Active · 26", "Archived · 2"; one pressed at a time; the pressed state isn't shown by colour alone.
- [ ] **FR-034, pagination.** Page 1 reads "Showing 1–25 of 31" with Previous (disabled), 1, 2, Next.
- [ ] **FR-036.** "Add community" is the primary action on every list artboard, and in the left nav.
- [ ] **US1 #1.** Keystone Park reads Onboarding, parent "—", "No manager", and has no "Differs from defaults" flag.
- [ ] **US3 #14.** Briar Hollow reads "Onboarding" and "No manager".
- [ ] **SC-008.** The loading state shows the frame, filters and search at once, with placeholder rows. Nothing asks to load every community (8.02.g).
- [ ] **SC-009.** Both no-manager rows are on the first page in 8.02.a and 8.02.b. 8.02.b puts them first.
- [ ] **Sort options.** 8.02.a (A–Z) and 8.02.b ("No manager" first) are both drawn and labelled "Design choice".
- [ ] **States.** Loading (8.02.g), load error with "Try again" (8.02.h) and no communities yet (8.02.i) are drawn with the §5 copy.
- [ ] **MVP note.** A DCPostIt by 8.02.a says the MVP shows only community name, status and the Community settings link, plus Add community.
- [ ] **Open questions.** A DCPostIt by 8.02.a lists the five questions in §4.4.
- [ ] **Words, not colour.** Every status and flag is a word. There is no ✓ status column and no coloured dot on its own.
- [ ] **Accessibility.** One tab stop per row; link names include the community name; "—" cells have screen-reader text; announcements are noted on the artboards or in notes.
- [ ] **Frame.** The company-area pages have no violet banner and no board-mode control for Robin. The board-mode pages keep dark ink on violet.
- [ ] **Data.** Only the conventions §4 people and communities appear. Filler names don't contain "Keystone". No names from sections 1–7 are reused.
- [ ] **Canvas.** Labels and ids match §1 exactly (s8-02a to s8-02k). Sections 1–7 render as before.
