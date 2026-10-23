# Claude Design briefs — spec 030 Company Administration

This folder holds one brief per screen or flow that spec 030 needs. Together they cover about 200 lofi artboards and emails. They are written for **Claude Design** (claude.ai/design). They add a new canvas section, **"8 · Company administration"**, to the existing **"HOA Management CRM"** project, the same canvas as the board wireframes from spec 025 (sections 1–7 of "NekoHOA Wireframes").

The spec ([`../spec.md`](../spec.md)) is the authority. When a brief and the spec disagree, the spec wins. Next comes [`00-conventions.md`](./00-conventions.md), which holds the shared rules, dataset, vocabulary and refusal copy. The individual brief comes last.

## How to run a brief

1. Open the **HOA Management CRM** project in Claude Design.
2. The first time only, paste the kickoff prompt below.
3. Attach three files: `spec.md`, `00-conventions.md` and the brief, for example `06-residents.md`.
4. Paste the fenced prompt from the brief's **§1** as is. The rest of the brief is detail that the prompt points to: artboards, copy, interactions and accessibility.
5. Iterate in the chat until the designs look right. Where a brief asks for **Option A / Option B**, keep both until you choose (see "Design choices for you" below).
6. Accept the result against the brief's **§9 Design review checklist**.
7. When every brief you need is done, export the handoff bundle from Claude Design. Save it under `design/handoff/`, as spec 006 did with `specs/006-stripe-payments/design/`. Then replace the spec's `[no WFb]` tags with citations to the new artboards, and run `/speckit.plan`.

## Kickoff prompt (paste once, at the start)

```text
We're adding company administration to the HOA Management CRM. A Company Administrator (staff of the management company, not a platform admin) adds communities, takes them live, archives and restores them, and appoints managers. Community Managers can also add communities, appoint co-managers, and invite or remove residents by email. Invitees can create an account from the invitation link, and people with no home can still sign in.

All of this goes in a new canvas section after section 7: "8 · Company administration". Create the empty section now; don't change sections 1–7.

I'll attach spec.md (spec 030, the authority) and 00-conventions.md (rules, sample data, vocabulary, refusal copy, artboard naming). Read both. Every later brief uses them. The designs are lofi and in section 7's visual language: layout, copy, states and who sees what are binding; colour and spacing are placeholders. Staff pages are desktop-first but must also work at tablet and phone width: each staff brief adds a tablet and a phone artboard of its main page (conventions §2). I'll then give you one brief at a time, numbered 01–12.
```

## Recommended order

1. **01 Company area and navigation**: how everyone gets to the new pages. It settles the company-area frame that 02, 03, 08 and 09 reuse.
2. **04 Community settings, Profile and status**: the page frame that 05, 06 and 07 reuse.
3. **06 Residents**: the page you asked for first. It can run right after 04.
4. **05 Managers and memberships**, then **02 Portfolio** and **03 Add community**.
5. **07 Architectural review section**, **08 Company defaults** and **09 Company administrators**.
6. **10 Invitation acceptance and sign-up**, **11 Sign-in and resident states** and **12 Emails**. These are independent of the staff pages and can run any time after 00.

## Index

The Artboards column counts the briefs' listed artboards. Each staff brief (01–09) adds two more: a tablet and a phone artboard of its main page (conventions §2).

| Brief | What it designs | Spec stories | Artboards |
| --- | --- | --- | --- |
| [00 Conventions](./00-conventions.md) | Rules, dataset (Juniper Row Community Management; its people and communities), vocabulary, refusal copy, shared patterns | all | — |
| [01 Company area and navigation](./01-company-area-navigation.md) | Company Administrator entry point (two options), company-area nav, board-mode "Community settings" and "Add community", Onboarding chips in switchers, sign-in landing order | US1, US2, US6, US9 | 18 |
| [02 Portfolio](./02-portfolio.md) | Every community with status, parent, managers, "No manager" and "Differs from defaults" flags; filter, search, paging | US6 | 11 |
| [03 Add community](./03-add-community.md) | The add-community form as Company Administrator and as Community Manager; validation; name conflict with and without the other name; success states | US1 | 15 |
| [04 Community settings, Profile and status](./04-community-settings-profile-status.md) | Page frame and section nav; Profile editing; Mark Active, Archive (consequences and open work), archived read-only, End membership, Restore (Active or Onboarding), Change parent; refusals | US3, US5 | 26 |
| [05 Managers and memberships](./05-managers-memberships.md) | Managers (open-ended rule), board and accountants, role invitations with states; appoint directly or by email; end, downgrade, replace; refusals | US2, US5 | 26 |
| [06 Residents](./06-residents.md) | Property list with residents and invitations; search, filters, paging; invite (sent or scheduled); resend, revoke; remove with autopay outcome; empty and archived states | US8, US5 | 26 |
| [07 Architectural review section](./07-architectural-review-section.md) | Spec 027 ARC settings as a section; per-field "differs from defaults"; reset to company defaults | US5, US4 | 7 |
| [08 Company defaults](./08-company-defaults.md) | ARC defaults: editable for Company Administrators, read-only for Community Managers; "applies only to new communities" | US4 | 8 |
| [09 Company administrators](./09-company-administrators.md) | List, invite by email, revoke, last-administrator refusals | US7 | 13 |
| [10 Invitation acceptance and sign-up](./10-invitation-acceptance.md) | Public invitation pages at phone and desktop width: landing, create account, sign in to accept, success landings, expired, invalid, wrong account, rate limit | US2, US7, US8 | 25 |
| [11 Sign-in and resident states](./11-sign-in-and-resident-states.md) | "No home linked", "isn't live yet", "No longer managed" homes, archived-home resident views, removed-resident redirect, Board Member with no home | US9, US3, US8 | 11 |
| [12 Emails](./12-emails.md) | Role, resident and Company Administrator invitations; community added; no longer managed; removed from home; autopay ended; recovery-mode notice | US1, US3, US7, US8 | 19 |

## Coverage

Coverage was checked mechanically against the spec on 2026-10-13:

- **User stories.** Of the spec's 105 acceptance scenarios (US1–US9), 104 are cited by at least one brief's traceability table (§3). The one left out is US2 #19, which is about logging and has nothing to draw.
- **FRs.** Every FR tagged `[no WFb]` is cited by at least one brief. Only FR-001 (the company record) and FR-005 (permissions match the table) are not cited; neither has anything to draw.
- **Copy.** Refusal messages are quoted the same way in every brief, from conventions §6. One sample dataset is used throughout.
- **Review.** Each brief was written against the spec and conventions. The planned per-brief review agents did not run, because of usage limits. These checks were done by hand instead: dataset names, refusal copy, 72-hour expiry, artboard ids and scenario coverage. Review each brief's §9 checklist when the designs come back.

## Design choices

Decided on 2026-10-14 and 2026-10-17. The briefs draw only these; nothing is left as an A/B choice.

| Where | Decision |
| --- | --- |
| 01 Company area entry | A bookmarkable **company sign-in page** (8.01.a–c), plus a **Company** menu in the account controls that opens the company shell (8.01.d–f) |
| 02 Portfolio sort | **A–Z by community name**; Community name, Status and Parent columns sortable; a **No manager** filter (8.02.a, b) |
| 04 Settings section nav | **Side list with a settings search box** (8.04.a, b) |
| 04 Profile saving | **Per field**, saved when the field is finished, with **"✓ Saved"** beside it (8.04.c–e). Architectural review keeps one Save, as in spec 027 |
| 04 Mark Active without a manager | **Disabled button**; the reason shows on hover and on keyboard focus (8.04.l); a request made anyway is refused (8.04.m) |
| 06 Residents detail | **Property drawer** (2026-10-17, recommended default; easy to switch back to rows): short rows with no buttons; clicking a property opens a panel on the right with its residents, invitations and every action (8.06.y) |

Each brief also lists its smaller design choices and open questions on post-its, for example whether an expired invitation link may show any invitation details (the briefs take the safe default: no).
