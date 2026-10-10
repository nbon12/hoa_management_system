# Feature Specification: Company Administration — Communities, Managers and Community Settings

**Feature Branch**: `030-company-administration`
**Created**: 2026-10-10
**Status**: Draft
**Input**: User description: "Company Administration: communities, managers, and community settings" — GitHub issue [nbon12/hoa_management_system#213](https://github.com/nbon12/hoa_management_system/issues/213) "(Community Manager) Initialize administration", including the owner's clarification comment on that issue.

## Context

Today a community can only be created by seed data. The Community Manager role (spec 025) works inside one community at a time. It manages memberships there and, since spec 027, that community's architectural review (ARC) rules. Nobody sits above that level. Nobody can add a new HOA the company has taken on, take it live, retire it when the company stops managing it, or keep settings consistent across the portfolio.

This spec adds the **Company Administrator**. This is a business role held by staff of the HOA management company, such as an owner, operations lead or portfolio manager. It is **not** an application or platform administrator. It never touches infrastructure, deployments, feature flags, secrets, other companies' data or the database. Everything it does is ordinary business setup through the product.

This spec also widens what a **Community Manager** can do (owner clarifications, 2026-10-08 and 2026-10-10):

- add a new community, becoming its first manager;
- appoint and remove co-managers;
- invite residents to a property by email, and remove them.

There is one management company per deployment for now. The data model must not prevent several companies later.

## Grounding: what exists today

Verified against `main` at commit `21e2491` (spec 027 merged):

- **Communities exist but have no lifecycle.** `Community` (spec 025) has legal name, community name (unique handle), county, formation date, management start date, description, an optional parent, and a status of only `Active` or `Inactive`. There is no create, archive or restore surface. Communities come from seed data and the 025 backfill.
- **The community-scope resolver denies everything in a non-Active community.** `CommunityScopeResolver` returns deny for any community whose status is not `Active`, whatever the caller's membership. Its capabilities are `ViewAssociationData`, `ManageMemberships`, `ViewArchitecturalApplications`, `VoteArchitecturalApplications` and `ManageArchitecturalReview`. There is no company-level capability.
- **Membership admin is manager-only.** The 025 membership create, update and list endpoints need `ManageMemberships`, which only a Community Manager has. A manager can already grant any non-Resident role in their community, including another Community Manager. Update refuses to leave a community with zero active managers (`LAST_MANAGER`). Creating a `Resident` membership is refused; residents reach their homes through `UserProperty` and the property claim codes from spec 016.
- **There is no invitation mechanism.** A membership can only be granted to a user who already has an account.
- **Residents link themselves to a home.** A resident reaches a property through a `UserProperty` link. Today the only way to create one is for the resident to redeem a single-use property claim code (spec 016), delivered to the owner's contact on file. A manager has no way to add or remove a resident.
- **ARC settings are per community** (spec 027, `CommunityArcSettings`): review period, lapse rule, decision rule, reminder days, time zone and formal disapproval statement. They are created from hard-coded defaults and edited at `board/arc-settings` by a Community Manager only.
- **Residents have recurring payments (autopay)** (spec 006, `RecurringPayment`) and vote in **community polls** (`Poll`, `PollVote`).
- **There is no company entity and no company-level role.** Nothing models the management company or anyone who works across communities.

## Clarifications

### Session 2026-10-08 (owner comment on issue #213)

- Q: How do the existing roles combine? → A: A Board Member can also be a Community Manager. A Community Manager does not need to be a Board Member or a resident. A Board Member does not need to be a Community Manager. (This matches the spec 025 rule that a user's capability in a community is the union of their active roles.)
- Q: What do Community Managers do? → A: Community Managers create resident accounts, add communities, add or remove residents, and can restrict resident rights such as voting. → This spec lets a Community Manager **add a community** (US1), which replaces the draft's "manager cannot create communities". For resident accounts and resident rights, see Session 2026-10-10.
- Q: Should a new community start as Onboarding or go straight to Active? → A: A new community starts as **Onboarding**, hidden from residents until a Company Administrator marks it Active.
- Q: Can a Community Manager appoint co-managers? → A: **Yes.** A Community Manager may appoint co-managers in their own community.
- Q: How long do invitations last? → A: **3 days.** (Who may resend was not answered. This spec lets anyone who could make the appointment resend or revoke it; see FR-022.)
- Q: Should archiving end everyone's memberships? → A: Owner: "if it means cancelling their payment subscriptions, yes — archiving a community should end the payment subscription." → **Archiving** means the company stops managing the community. It **cancels every resident's autopay** in that community. Access memberships are kept but confer nothing while archived, so a restore brings them back unchanged (FR-012, FR-013, FR-015).
- Q: Should changing a company default offer to push the change to existing communities? → A: **No.** A changed default applies only to communities created afterwards.

### Session 2026-10-10

- Q: May a Community Manager end or downgrade **another** manager in their community? → A: **Yes.** A Community Manager may remove or downgrade co-managers in their own community, but never the last active manager (FR-019, FR-020).
- Q: Are resident accounts, adding and removing residents, and restricting resident rights in this spec? → A: **Partly.** Managers **invite residents to a property by email** and **remove them** (US8). Restricting resident rights, such as suspending poll voting, is **not built**. The Residents page needs a Claude Design design, as do the other new screens; the briefs are in `design/` (see Design references).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add a community the company has taken on (Priority: P1)

As a Company Administrator or a Community Manager, I add a new community with its legal name, community name (a unique handle), county, formation date, management start date, an optional description and an optional parent (master) association. The new community starts as **Onboarding**. It gets the company's default settings (US4). If I am a Community Manager, I become its first manager.

**Why this priority**: Without it, every new HOA the company takes on needs an engineer to write seed data. Nothing else in this spec matters until a community can be created in the product.

**Independent Test**: As a Company Administrator, add "Keystone Park HOA, Inc." with community name "Keystone Park". Confirm it is saved as Onboarding, appears in the portfolio, and its ARC settings equal the company defaults. Repeat as a Community Manager and confirm the manager holds an active manager membership in the new community. Try a duplicate name and a non-manager user and confirm both are refused with nothing created.

**Acceptance Scenarios**:

1. **Given** I am a Company Administrator, **When** I add "Keystone Park HOA, Inc." with community name "Keystone Park", county "Hamilton", formation date 03/14/2004 and management start date 11/01/2026, **Then** it is saved with status Onboarding, it appears in my portfolio, it has no manager, and its ARC settings equal the company defaults at that moment.
2. **Given** a community named "Keystone Park" already exists, **When** I add another with the community name "keystone park " (different case, trailing space), **Then** it is refused with `COMMUNITY_NAME_TAKEN` and a message naming "Keystone Park", and nothing is created.
3. **Given** I hold an active Community Manager membership in at least one community, **When** I add "Keystone Ridge HOA, Inc." with community name "Keystone Ridge", **Then** it is saved as Onboarding, I hold an active Community Manager membership in it, and it appears in my community switcher on my next request.
4. **Given** I am neither a Company Administrator nor an active Community Manager anywhere (for example a Board Member, an Accountant or a Resident only), **When** I try to add a community, **Then** I am refused (403) and nothing is created.
5. **Given** I am a Company Administrator and "Keystone Crossing Master" exists, **When** I add a community with that parent, **Then** the new community is a sub-association of it. **And** a Board Member of the master gets no access to the new community (spec 025: scope is per community).
6. **Given** I am a Community Manager of community A only, **When** I add a community and choose community B as its parent, **Then** it is refused and nothing is created. The parent choices offered to me are only the communities I manage.
7. **Given** I leave legal name, community name, county, formation date or management start date empty, **When** I save, **Then** it is refused with a message naming each missing field.

---

### User Story 2 - Appoint and change a community's managers (Priority: P1)

As a Company Administrator, I appoint a Community Manager for any community, including its first, and I can end or replace a manager. As a Community Manager, I can appoint and remove co-managers in my own community. If the person has no account, I invite them by email.

**Why this priority**: A community created by a Company Administrator has no manager, and without a manager nobody can run it day to day. A community must never silently lose its last manager.

**Independent Test**: As a Company Administrator, appoint an existing user as manager of a new community and confirm they see it on their next request. Invite a new email, accept within 3 days and confirm the membership. Let a second invitation pass 3 days and confirm the link is refused. Try to end a community's only manager and confirm it is refused.

**Acceptance Scenarios**:

1. **Given** a community with no manager, **When** I (a Company Administrator) appoint an existing user as its Community Manager, **Then** they hold an active Community Manager membership and see the community in their switcher on their next request.
2. **Given** I am a Community Manager of community A, **When** I appoint an existing user as a co-manager of A, **Then** they hold an active Community Manager membership in A and A has two active managers.
3. **Given** I am a Community Manager of community A only, **When** I try to appoint a manager in community B, **Then** I am refused and the response does not reveal whether B exists.
4. **Given** the person has no account, **When** I appoint them by email, **Then** an invitation is emailed to that address. **When** they accept within 3 days and create their account with that same email, **Then** they hold the Community Manager membership.
5. **Given** an invitation was sent more than 3 days ago and not accepted, **When** its link is used, **Then** it is refused with `INVITATION_EXPIRED` and no membership is created.
6. **Given** a pending or expired invitation, **When** I (who could make that appointment) resend it, **Then** a new link valid for 3 days is emailed and the previous link is refused if used.
7. **Given** a community with exactly one active manager, **When** I end that manager without naming a replacement, **Then** it is refused with `LAST_MANAGER` and the manager stays active.
8. **Given** a community with exactly one active manager, **When** I replace them with another user in one action, **Then** the new manager is active, the old membership is ended, and the community never had zero active managers.
9. **Given** I am a Community Manager of A and another active manager exists in A, **When** I end my own manager membership, **Then** it succeeds.
10. **Given** I am a Community Manager of A and A has a co-manager, **When** I end the co-manager's manager membership or change it to Board Member, **Then** it succeeds, and the co-manager loses manager powers in A on their next request.
11. **Given** A has exactly two active managers, **When** each tries to end the other's manager membership at the same moment, **Then** exactly one succeeds, the other is refused with `LAST_MANAGER`, and A keeps one active manager.
12. **Given** I am a Board Member, an Accountant or a Resident (and not a Community Manager there or a Company Administrator), **When** I try to create or end a Community Manager membership, **Then** I am refused.
13. **Given** any appointment, change, end, invitation, resend, revoke or acceptance, **When** it happens, **Then** one sensitive event is logged with actor, community, target user or email, the change, and UTC time.

---

### User Story 3 - Take a community live, edit its profile, and archive or restore it (Priority: P1)

As a Company Administrator, I move a community through its statuses: **Onboarding** while it is being set up, **Active** once it is live, and **Archived** when the company stops managing it. I can restore an archived community. A Community Manager can edit their own community's profile but not its status or parent.

**Why this priority**: Onboarding keeps a half-configured HOA away from residents. Archiving is the only way to stop managing an HOA without deleting its history, and it must stop autopay charges.

**Independent Test**: Create a community (Onboarding), confirm a linked resident cannot see it, appoint a manager, mark it Active and confirm the resident can. Seed autopay for three owners, archive the community, and confirm autopay is cancelled, owners are emailed, writes are refused and the records remain readable to the Company Administrator. Restore it and confirm memberships and settings are unchanged.

**Acceptance Scenarios**:

1. **Given** an Onboarding community with a resident linked to one of its properties, **When** that resident signs in, **Then** the community's resident pages (community page, polls, announcements) are not shown, and resident actions in it (poll votes, payments, autopay enrollment, architectural applications) are refused with `COMMUNITY_NOT_ACTIVE`.
2. **Given** an Onboarding community, **When** its Community Manager or Board Member enters board mode, **Then** they can work in it (for example, set memberships and ARC settings).
3. **Given** an Onboarding community with at least one active manager, **When** I (a Company Administrator) mark it Active, **Then** its status is Active and its linked residents see it on their next request.
4. **Given** an Onboarding community with no active manager, **When** I mark it Active, **Then** it is refused with `NO_ACTIVE_MANAGER` and it stays Onboarding.
5. **Given** an Active community with no open work, **When** I archive it, **Then** it disappears from board and manager community switchers and from residents' views, every write in it is refused with `COMMUNITY_ARCHIVED` (for example a new architectural application, a vote, a poll vote, a payment), and its profile, settings and membership history stay readable to me. Nothing is deleted.
6. **Given** an Active community where 3 owners have active autopay, **When** I archive it, **Then** all 3 autopay schedules are cancelled, no further automatic charge is made for them, and each owner is emailed that autopay ended because the company no longer manages the community.
7. **Given** a community with architectural applications ARC-1041 (Open) and ARC-1042 (Decision Reached), **When** I try to archive it, **Then** it is refused with `OPEN_WORK` and the message lists ARC-1041 and ARC-1042 with their statuses.
8. **Given** a master community with a sub-association that is not archived, **When** I try to archive the master, **Then** it is refused with `OPEN_WORK` and the message lists that sub-association.
9. **Given** an archived community, **When** I restore it, **Then** it returns to Active with its memberships and settings unchanged, and no autopay is restarted. Owners must enroll again.
10. **Given** I am the Community Manager of an Active community, **When** I edit its legal name, community name, county, formation date, management start date or description, **Then** the change is saved and logged with old and new values.
11. **Given** I am a Community Manager, **When** I try to activate, archive, restore or change the parent of my community, **Then** I am refused and nothing changes.
12. **Given** I am a Community Manager of community A only, **When** I try to edit community B's profile, **Then** I am refused and the response does not reveal whether B exists.

---

### User Story 4 - Company-wide default settings (Priority: P2)

As a Company Administrator, I set the defaults a new community starts with, so every new HOA follows our standard setup until its own governing documents say otherwise.

Defaults cover, at minimum, architectural review (spec 027): review period in days, lapse rule (flag overdue only, deemed approved, or deemed denied), decision rule (majority of members, or majority of votes cast with a quorum), reminder days, time zone, and the formal disapproval statement. Notification defaults are added once the Notification Settings spec exists; until then that section is not shown.

**Why this priority**: Without defaults, each new community starts from hard-coded values and someone must remember to fix each one. It is P2 because a community can still be created and set up by hand.

**Independent Test**: Change the default lapse rule to "deemed approved", create a community and confirm it starts with "deemed approved". Confirm an existing community and its applications already received are unchanged. As a Community Manager, confirm the defaults can be read but not changed.

**Acceptance Scenarios**:

1. **Given** I am a Company Administrator, **When** I change the default lapse rule to "deemed approved" and a community is created afterwards, **Then** the new community starts with "deemed approved" and every existing community keeps its own lapse rule.
2. **Given** a community created before the change, and applications it already received, **When** I change any default, **Then** neither that community's settings nor the rules already copied onto those applications change (spec 027 FR-030).
3. **Given** I am a Community Manager, **When** I open the company defaults, **Then** I can read them, **and When** I try to change them, **Then** I am refused.
4. **Given** I am a Board Member, an Accountant or a Resident, **When** I try to read the company defaults, **Then** I am refused.
5. **Given** I enter a review period of 0 or 400 days, or reminder days of 31, **When** I save, **Then** it is refused with a message naming the allowed range (same ranges as spec 027 FR-029).
6. **Given** the Notification Settings spec has not landed, **When** I open the company defaults, **Then** no notification section is shown.

---

### User Story 5 - One settings page per community (Priority: P2)

As a Company Administrator or that community's Community Manager, I open one **Community settings** page with these sections:

- Profile
- Managers and memberships (managers, board and accountants, residents)
- Architectural review
- Notifications (shown only once the Notification Settings spec lands)

The Architectural review section is spec 027's ARC settings, moved under this page rather than rebuilt.

**Why this priority**: Settings are scattered today (ARC settings on their own page, memberships on another). One page makes setting up a new HOA a single task. It is P2 because each setting is already reachable on its own.

**Independent Test**: Open one community's settings as a Company Administrator and confirm every section is editable, including "Reset architectural review to company defaults". Open it as that community's manager and confirm status and parent are read-only. Open it as a Board Member and confirm you are refused.

**Acceptance Scenarios**:

1. **Given** I am a Company Administrator, **When** I open any non-archived community's settings, **Then** every section is editable, including status, parent and "Reset architectural review to company defaults".
2. **Given** I am the community's Community Manager, **When** I open its settings, **Then** Profile is editable except status and parent, Architectural review is editable as in spec 027, and Managers and memberships lets me grant and end Board Member and Accountant memberships, appoint and remove co-managers (never the last manager), and invite and remove residents (US8).
3. **Given** I am a Board Member, an Accountant or a Resident of the community (and not its manager or a Company Administrator), **When** I try to open its settings, **Then** I am refused and redirected to a permitted page (spec 025 FR-028).
4. **Given** I change any setting, **When** I save, **Then** one sensitive event is logged with the old and new values (spec 027 FR-030 pattern).
5. **Given** a community whose ARC review period is 45 days while the company default is 30, **When** I choose "Reset architectural review to company defaults" and confirm, **Then** every ARC setting equals the current company defaults, applications already received keep their copied rules, and the reset is logged with old and new values.
6. **Given** I go to the old ARC settings location from spec 027, **When** it loads, **Then** I land on the Architectural review section of this page.
7. **Given** an archived community, **When** I (a Company Administrator) open its settings, **Then** every section is shown read-only, with Restore as the only action.

---

### User Story 6 - Portfolio view (Priority: P3)

As a Company Administrator, I see every community the company manages in one list, with its status, its parent, its managers, whether it has any active manager, and whether its ARC settings differ from the company defaults. I can filter by status and search by name.

**Why this priority**: It is how the company spots a community nobody is running, but every action it leads to is available elsewhere.

**Independent Test**: Seed communities in each status, one with no manager and one whose ARC settings differ from the defaults. Confirm the flags, the status filter and the search. Confirm a Community Manager is never offered the view and is refused if they request it.

**Acceptance Scenarios**:

1. **Given** a community with no active manager, **When** I open the portfolio, **Then** its row is flagged "No manager".
2. **Given** a community whose only manager is a pending invitation, **When** I open the portfolio, **Then** its row is flagged "No manager" and shows "invitation pending".
3. **Given** a community whose ARC review period differs from the company default, **When** I open the portfolio, **Then** its row shows that its ARC settings differ from the defaults.
4. **Given** communities in Onboarding, Active and Archived, **When** I filter by Archived, **Then** only archived communities are listed.
5. **Given** I type "Keystone", **When** the list filters, **Then** only communities whose community name or legal name contains "Keystone" (any case) are listed.
6. **Given** I am a Community Manager, **When** I use the app, **Then** the portfolio is never offered to me, **and When** I request it directly, **Then** I am refused. My own communities stay in my existing switcher.

---

### User Story 7 - Grant and revoke the Company Administrator role (Priority: P2)

As a Company Administrator, I grant the role to another user and revoke it. The very first Company Administrator is created by a one-time setup step run by whoever deploys.

**Why this priority**: Without it, the role can only be given by an engineer. It is P2 because the one-time setup step gives a working first administrator.

**Independent Test**: Run the setup step on an empty deployment and confirm one Company Administrator exists; run it again and confirm nothing changes. Grant a second administrator, revoke the first, and confirm the last one cannot be revoked. Confirm holding the role gives no vote.

**Acceptance Scenarios**:

1. **Given** I am a Company Administrator, **When** I grant the role to an existing user, **Then** they can reach the portfolio on their next request.
2. **Given** there are two Company Administrators, **When** one revokes the other, **Then** the revoked user loses the role on their next request.
3. **Given** I am the only Company Administrator, **When** I try to revoke my own role, **Then** it is refused with `LAST_COMPANY_ADMIN`.
4. **Given** two Company Administrators each try to revoke the other at the same moment, **When** both requests are processed, **Then** exactly one succeeds and one Company Administrator remains.
5. **Given** I am a Community Manager and not a Company Administrator, **When** I try to grant or revoke the role, **Then** I am refused.
6. **Given** a deployment with no Company Administrator, **When** the deployer runs the one-time setup step for a user, **Then** that user becomes the first Company Administrator. **Given** one already exists, **When** the step is run again, **Then** nothing changes and the step says so.
7. **Given** I am a Company Administrator with no Board Member membership in community A, **When** I try to vote on an architectural application in A or record its outcome, **Then** I am refused.
8. **Given** any grant or revoke, **When** it happens, **Then** one sensitive event is logged with actor, target user, the change and UTC time.

---

### User Story 8 - Invite and remove residents (Priority: P1)

*(Numbered 8 so US1–US7 keep the issue's numbering; it is P1.)*

As a Community Manager, or a Company Administrator, I open the **Residents** area of a community's Managers and memberships section. I see every property in the community with its residents and any invitations. I invite a resident to a property by email. The person accepts and can then see that home in resident mode. I can remove a resident from a property, for example after a sale or a move-out.

**Why this priority**: A new HOA isn't useful until its residents are on it. Today residents can only join with a claim code mailed to the owner's contact on file, which needs owner records the company may not have yet. Inviting by email is how a manager brings a community's residents on board.

**Independent Test**: As a manager of an Active community, invite a new email to one property. Accept within 3 days and confirm the resident sees that home. Invite to an Onboarding community and confirm nothing is sent until it is marked Active. Remove the resident and confirm they lose that home on their next request, their autopay for it is cancelled, and their other homes and roles are untouched.

**Acceptance Scenarios**:

1. **Given** I am the Community Manager of Active community A, which has the property "711 Keystone Park Dr #29", **When** I invite "jane@example.com" as a resident of that property, **Then** an invitation is emailed to that address. **When** Jane accepts within 3 days with an account using that email, **Then** she is linked to that property as a resident and sees it in resident mode on her next request.
2. **Given** "sam@example.com" already has an account linked to another home, **When** I invite that email to a property in A, **Then** Sam is emailed to sign in and accept. Nothing changes until he accepts. After he accepts, he sees both homes.
3. **Given** community A is Onboarding, **When** I invite a resident, **Then** the invitation is saved as "scheduled" and no email is sent. **When** a Company Administrator marks A Active, **Then** every scheduled invitation is emailed, each valid for 3 days from that moment.
4. **Given** a resident invitation was sent more than 3 days ago and not accepted, **When** its link is used, **Then** it is refused with `INVITATION_EXPIRED` and no link to the property is made. **When** I resend it, **Then** a new link valid for 3 days is emailed and the old link is refused.
5. **Given** Jane is a resident of "711 Keystone Park Dr #29", **When** I remove her from that property, **Then** she loses access to that property on her next request. Her account and her other homes are unchanged. Any autopay she set up for that property is cancelled. She is emailed that she was removed. The removal is logged.
6. **Given** Jane is also a Board Member of A, **When** I remove her as a resident of her property, **Then** her Board Member membership is unchanged (spec 025 FR-011).
7. **Given** "jane@example.com" is already a resident of that property, **When** I invite that email to the same property, **Then** it is refused with `ALREADY_RESIDENT`.
8. **Given** I am a Community Manager of A only, **When** I try to invite or remove a resident of a property in community B, **Then** I am refused and the response does not reveal whether B or the property exists.
9. **Given** I am a Board Member, an Accountant or a Resident (and not a Community Manager there or a Company Administrator), **When** I try to invite or remove a resident, **Then** I am refused.
10. **Given** community A has 120 properties, **When** I open the Residents area, **Then** each property shows its address, its residents (name, email, resident since), and its pending, scheduled or expired invitations. I can search by address, name or email, and filter to "No resident", "Invitation pending" or "Invitation expired".
11. **Given** a resident invitation is pending, **When** I revoke it, **Then** its link is refused and no link to the property is made.
12. **Given** any resident invitation, resend, revoke, acceptance or removal, **When** it happens, **Then** one sensitive event is logged with actor, community, property, target user or email, the change and UTC time.

---

### Edge Cases

- **A user is both a Company Administrator and a Community Manager**: their capability is the union of both. Neither role is derived from the other.
- **A Company Administrator also sits on a board**: they vote only through their Board Member membership, like anyone else (FR-003).
- **Two people end a community's last two managers at the same moment**: exactly one succeeds. The community never reaches zero active managers.
- **A community is archived while invitations are pending**: the invitations are revoked. Their links are refused.
- **An invitation is accepted by an account with a different email**: refused. The invitation is bound to the invited email.
- **The invitee already has an account by the time they accept**: they sign in with that account and the membership is granted to it.
- **A community name is changed**: the old name becomes free for another community. Uniqueness ignores case and surrounding spaces.
- **A parent change would create a loop** (A under B under A): refused. A community cannot be its own ancestor. An archived community cannot be chosen as a parent.
- **A default changes while a community is being created**: the community gets the defaults in effect at the moment it is saved.
- **An owner's autopay is mid-charge when the community is archived**: a charge already submitted settles normally. No new charge starts after the archive.
- **A board member's term has already ended when the community is restored**: it stays ended. Restore does not change membership end dates.
- **A Community Manager who created a community later loses all their manager memberships**: the communities they created are unaffected.
- **An Onboarding community is abandoned before going live**: a Company Administrator can archive it directly (same open-work rule). Its scheduled resident invitations are revoked without ever being sent.
- **The same email is invited to two properties**: they are two separate invitations, each accepted on its own.
- **A resident is removed while a payment they made is still processing**: the payment settles normally against the property. Only future autopay charges are cancelled.
- **The last resident of a property is removed**: allowed. The property shows "No resident". The owner of record and the property's balance are unchanged.
- **A resident who joined with a claim code is removed**: same as any resident. The claim code (spec 016) is already used and can't be used again. Claim codes keep working as a second way to join.
- **A resident invitation is mistyped**: the manager revokes it and sends a new one. Only the invited email can accept.
- **A claim code is redeemed for a property in an Onboarding community**: refused with `COMMUNITY_NOT_ACTIVE`, like any other resident action there (FR-011).

## Requirements *(mandatory)*

> **Design references.** No wireframes exist for this feature yet; every UI requirement is `[no WFb]`. The briefs for Claude Design are in [`design/`](./design/README.md). There is one brief per screen or flow, written to add a section "8 · Company administration" to the existing "HOA Management CRM" Claude Design project, the same canvas as the spec 025 board wireframes (`WFb`). When the designs come back as a handoff bundle, they are saved under `design/` and the `[no WFb]` tags are replaced with design citations. Until then, the requirements below describe behavior and copy only. The board side keeps spec 025's visual language (025 FR-037).

### Permissions at a glance

| Action | Company Administrator | Community Manager of that community | Board Member, Accountant, Resident |
| --- | --- | --- | --- |
| Add a community | Yes, any parent | Yes; becomes its first manager; parent only from communities they manage | No |
| Edit profile (legal name, community name, county, dates, description) | Yes | Yes | No |
| Change parent | Yes | No | No |
| Activate, archive, restore | Yes | No | No |
| Appoint a Community Manager | Yes | Yes (co-manager) | No |
| End their own manager membership | Yes | Yes, if another active manager remains | — |
| End or downgrade another Community Manager | Yes | Yes (co-managers); never the last manager | No |
| Grant or end Board Member and Accountant memberships | Yes | Yes (spec 025 FR-042) | No |
| Invite a resident to a property, or remove one | Yes | Yes | No |
| Resend or revoke an invitation | Yes | Yes, for invitations they could make | No |
| Edit ARC settings, reset to company defaults | Yes | Yes | No |
| Read company defaults | Yes | Yes | No |
| Change company defaults | Yes | No | No |
| Portfolio view | Yes | No | No |
| Grant or revoke Company Administrator | Yes | No | No |

### Company and roles

- **FR-001**: System MUST model the **management company** as its own record that owns its communities and its Company Administrators. There is exactly one per deployment for now. Nothing in the model may assume there can only ever be one.
- **FR-002**: System MUST store a company-level **Company Administrator** role, separate from community memberships, and resolve it server-side on every request. It MUST NOT be derived from any community role, and no community role may be derived from it.
- **FR-003**: Holding the Company Administrator role MUST NOT by itself grant board powers in any community: no vote, no recording of ARC outcomes, and no association-wide homeowner or financial data beyond what this spec's pages show. A Company Administrator who needs those must hold the matching community membership.
- **FR-004**: Every endpoint in this spec MUST authorize through the spec 025 community-scope resolver, extended with company-level capabilities: `ManageCommunities`, `AppointCommunityManagers`, `ManageCompanyDefaults`, `ManageCompanyAdministrators` and `ViewPortfolio`. The resolver MUST still decide community scope per community and never through the parent link. Denials MUST fail closed and MUST NOT reveal whether a community exists (spec 025 FR-016).
- **FR-005**: Permissions MUST match the table above. Any action not listed for a role is refused.

### Communities

- **FR-006**: A Company Administrator, or a user with an active Community Manager membership in at least one community, MUST be able to add a community with: legal name (required, up to 200 characters), community name (required, 2–80 characters), county (required), formation date (required, not in the future), management start date (required), description (optional, up to 2,000 characters) and parent (optional). `[no WFb]`
- **FR-007**: Community name MUST be unique within the management company, compared without regard to case or leading and trailing spaces. A conflict MUST be refused with `COMMUNITY_NAME_TAKEN` and a message naming the existing community.
- **FR-008**: A new community MUST start as **Onboarding**. When a Community Manager adds it, that manager MUST receive an active Community Manager membership in it in the same action. When a Company Administrator adds it, it has no manager until one is appointed (US2).
- **FR-009**: At creation, the new community's settings MUST be copied from the company defaults in effect at that moment (FR-025). After that the copies are independent: later default changes never alter them.
- **FR-010**: A community's status MUST be one of **Onboarding**, **Active** or **Archived**. The allowed changes, all by a Company Administrator only, are:
  - Onboarding → Active, refused with `NO_ACTIVE_MANAGER` unless the community has at least one active Community Manager;
  - Onboarding or Active → Archived, subject to FR-014;
  - Archived → Active (restore).
  The existing `Inactive` status MUST be migrated to Archived.
- **FR-011**: While a community is **Onboarding**, it MUST be hidden from residents. Its resident pages are not shown, and resident actions in it (poll votes, payments, autopay enrollment, architectural applications, claim-code redemption) MUST be refused with `COMMUNITY_NOT_ACTIVE`. Resident invitations are scheduled, not sent (FR-024b). Its non-resident members (Community Managers, Board Members, Accountants) and Company Administrators MUST be able to work in it.
- **FR-012**: While a community is **Archived**:
  - it MUST NOT appear in any board or manager community switcher or in residents' views;
  - every write in it, across all specs (memberships, settings, votes, applications, poll votes, payments, autopay), MUST be refused with `COMMUNITY_ARCHIVED`;
  - its memberships MUST be kept unchanged but MUST confer no access;
  - its pending invitations MUST be revoked;
  - its profile, settings and membership history MUST stay readable, read-only, to Company Administrators;
  - owners MUST keep their existing read-only access to their own payment history and receipts;
  - none of its records may be deleted.
- **FR-013**: Archiving MUST cancel every active autopay schedule for properties in the community in the same action. No automatic charge may start afterwards. Each affected owner MUST be emailed through the existing transactional email that autopay ended because the company no longer manages the community. A charge already submitted before the archive settles normally. `[no WFb]`
- **FR-014**: Archiving MUST be refused with `OPEN_WORK` while the community has open work, and the refusal MUST list each item. In this spec, open work is:
  - architectural applications that are Open or Decision Reached (spec 027);
  - sub-associations that are not archived.
  Later specs MAY add their own kinds of open work.
- **FR-015**: Restoring MUST return the community to Active with its memberships (including their end dates) and settings unchanged. Autopay cancelled by the archive MUST NOT restart. Owners enroll again themselves.
- **FR-016**: A parent MAY be set when a community is added. A Company Administrator may choose any non-archived community; a Community Manager only one they manage. Changing the parent later is Company Administrator only. A community MUST NOT become its own ancestor. The parent link MUST NOT change anyone's access (spec 025).
- **FR-017**: A Community Manager MUST be able to edit their own community's legal name, community name, county, formation date, management start date and description, but not its status or parent. A Company Administrator may edit all of them.

### Managers and memberships

- **FR-018**: A Company Administrator MUST be able to appoint a Community Manager in any non-archived community. A Community Manager MUST be able to appoint co-managers in a community they manage.
- **FR-019**: A Company Administrator MUST be able to end or downgrade any Community Manager membership. A Community Manager MUST be able to end or downgrade their own manager membership, or a co-manager's, in a community they manage (Clarifications 2026-10-10). Both are subject to FR-020: the last active manager can never be removed.
- **FR-020**: A community that has an active Community Manager MUST never be left with zero, through any surface or concurrent requests. Ending the last one MUST be refused with `LAST_MANAGER`. A Company Administrator MUST be able to replace the last manager in one action, so the community is never left without one.
- **FR-021**: Board Member and Accountant memberships MUST remain manageable by the community's Community Manager (spec 025 FR-042) and MUST also be manageable by a Company Administrator.
- **FR-022**: Any membership this spec lets a user grant MAY be granted by email to someone without an account, as an **invitation**. The same rules apply to resident invitations (FR-024):
  - it is valid for **3 days** and usable once;
  - it is bound to the invited email: it can only be accepted by an account with that email, which the invitee creates or signs in with;
  - accepting it creates the membership, subject to the same rules as a direct appointment at that moment;
  - anyone who could make the appointment can **resend** it (a new 3-day link; the old link stops working) or **revoke** it;
  - an expired, revoked or used link MUST be refused (`INVITATION_EXPIRED` or `INVITATION_INVALID`) and create nothing;
  - a pending invitation does not count as an active manager (FR-010, FR-020).
- **FR-023**: Every appointment, change, end, invitation, resend, revoke and acceptance MUST be logged as a sensitive event with actor, community, target user or email, the change and UTC time.
### Residents

- **FR-024**: A Community Manager of the community, or a Company Administrator, MUST be able to invite a person by email to be a resident of a property in that community. The invitation follows FR-022 (3 days, single use, bound to the email, resend and revoke). Accepting it links the accepting account to that property as a resident, the same link a claim code creates (spec 016). An email already linked to that property MUST be refused with `ALREADY_RESIDENT`. A property outside the community MUST be refused without revealing whether it exists. `[no WFb]`
- **FR-024a**: If the invited email already has an account, the invitation asks them to sign in and accept. Nothing is linked until they accept.
- **FR-024b**: A resident invitation made while the community is Onboarding MUST be saved as **scheduled** and not emailed. When the community is marked Active, every scheduled invitation MUST be emailed, and its 3 days start then. If the community is archived instead, scheduled invitations are revoked without being sent.
- **FR-024c**: A Community Manager of the community, or a Company Administrator, MUST be able to remove a resident from a property. On removal:
  - the resident loses access to that property on their next request;
  - their account, other homes and community memberships are unchanged (spec 025 FR-011);
  - any autopay they set up for that property is cancelled, and a charge already submitted settles normally;
  - they are emailed that they were removed from that property;
  - the property's owner of record and balance are unchanged.
- **FR-024d**: The **Residents** area MUST list every property in the community with its address, its residents (name, email, resident since) and its scheduled, pending or expired invitations. It MUST search by address, resident name or email (case-insensitive partial match), filter to "No resident", "Invitation pending" and "Invitation expired", and paginate with a default of 25 and a maximum of 100 properties. `[no WFb]`
- **FR-024e**: Every resident invitation, resend, revoke, acceptance and removal MUST be logged as a sensitive event with actor, community, property, target user or email, the change and UTC time.
- **FR-024f**: Restricting resident rights, such as suspending a resident's poll voting, is NOT built (Clarifications 2026-10-10). Claim codes (spec 016) keep working as a second way for a resident to join.

### Company default settings

- **FR-025**: The company MUST have default settings, editable by a Company Administrator only, covering at minimum the spec 027 ARC settings: review period in days (1–365), lapse rule, decision rule, reminder days (0–30), time zone, and formal disapproval statement (required, up to 2,000 characters). Their initial values MUST be spec 027's defaults (30 days, flag overdue only, majority of members, 7 days, America/New_York, and 027's standard statement).
- **FR-026**: Changing a default MUST affect only communities created afterwards. It MUST NOT change any existing community's settings, nor the rules already copied onto applications already received (spec 027 FR-030). The product MUST NOT offer to push a changed default to existing communities.
- **FR-027**: Community Managers MUST be able to read the company defaults. Board Members, Accountants and Residents MUST NOT.
- **FR-028**: The company defaults page MUST NOT show a notification section until the Notification Settings spec lands. That spec adds it.

### Community settings page

- **FR-029**: Each community MUST have one **Community settings** page with the sections Profile, Managers and memberships, Architectural review and Notifications. Notifications is not shown until the Notification Settings spec lands. Company Administrators reach it from the portfolio; Community Managers reach it from board mode in the active community. `[no WFb]`
- **FR-030**: The spec 027 ARC settings MUST be shown as the page's Architectural review section, not rebuilt. Who may edit them widens from "that community's Community Manager" (spec 027 FR-030) to "a Company Administrator or that community's Community Manager". Recording ARC outcomes stays Community Manager only. The old ARC settings location MUST lead to this section.
- **FR-031**: A Company Administrator or the community's Community Manager MUST be able to reset the community's ARC settings to the current company defaults, after a confirmation. Applications already received keep their copied rules.
- **FR-032**: A Board Member, Accountant or Resident who is not also the community's manager or a Company Administrator MUST be refused the page and redirected to a permitted page (spec 025 FR-028).
- **FR-033**: Every settings change, including a reset, MUST be logged as a sensitive event with the old and new values.

### Portfolio

- **FR-034**: Company Administrators MUST have a portfolio listing every community of the company with: community name, legal name, status, parent, active managers and pending manager invitations, a "No manager" flag when there is no active manager, and a flag when its ARC settings differ from the company defaults. It MUST filter by status and search by community name or legal name (case-insensitive partial match). It is paginated with a default page size of 25 and a maximum of 100. `[no WFb]`
- **FR-035**: The portfolio MUST NOT be offered to anyone but a Company Administrator, and a direct request from anyone else MUST be refused.
- **FR-036**: A Company Administrator MUST be able to reach the portfolio from the top-bar account controls (spec 025 FR-019) even if they hold no community membership. `[no WFb]`

### Company Administrators

- **FR-037**: A Company Administrator MUST be able to grant the role to an existing user and revoke it from any Company Administrator. Revoking the last one, including oneself, MUST be refused with `LAST_COMPANY_ADMIN`, also under concurrent requests. Changes take effect on the user's next request.
- **FR-038**: The first Company Administrator MUST be created by a one-time setup step run by whoever deploys (a seed or command-line step). If a Company Administrator already exists, the step MUST change nothing and say so. This is the only part of this spec an application operator touches.
- **FR-039**: Every grant and revoke, and every run of the setup step, MUST be logged as a sensitive event with actor (or "setup"), target user, the change and UTC time.

### Cross-cutting

- **FR-040**: All changes in this spec MUST be logged as Serilog structured sensitive events (actor, scope, target, old and new values, UTC time). There is no new audit table, as in spec 025.
- **FR-041**: Every write endpoint in this spec MUST use the existing `board-writes` rate limit (spec 027), which also covers sending invitations (constitution §7).
- **FR-042**: Every user-visible refusal MUST say why in plain language (for example, "Keystone Park already uses that community name" or "This community can't be archived until ARC-1041 and ARC-1042 are closed"), except denials that must not reveal a community exists (FR-004).

### Key Entities

- **Management Company** *(new)*: The HOA management company running this deployment. Has a name. Owns its communities, its Company Administrators and its default settings. One per deployment for now.
- **Company Administrator grant** *(new)*: Links a user to the management company as a Company Administrator, with who granted it and when, and, once revoked, who revoked it and when.
- **Community** *(modified)*: Belongs to a management company. Status becomes Onboarding, Active or Archived (replacing Active and Inactive), with when and by whom it was activated, archived or restored.
- **Company Default Settings** *(new)*: One set per management company. Holds the ARC defaults (FR-025). Reserves a place for notification defaults.
- **Community ARC Settings** *(spec 027, unchanged shape)*: Now copied from Company Default Settings when a community is created, and resettable to them.
- **Invitation** *(new)*: An offer sent by email to someone who may not have an account yet. It offers either a community membership (role) or residency of one property in the community. Holds the invited email, community, the role or the property, who sent it, when it was sent and when it expires (3 days after sending), and its state (scheduled, pending, accepted, expired or revoked). Only a hash of its link is stored.
- **Resident link** *(existing `UserProperty`, modified behavior)*: Links a resident's account to a property. Can now also be created by accepting a resident invitation, and removed by a manager or Company Administrator.
- **Community Membership** *(spec 025, unchanged shape)*: Gains no fields. Confers nothing while its community is Archived.
- **Recurring Payment (autopay)** *(spec 006, modified behavior)*: Cancelled when its community is archived (reason "community archived") or when its resident is removed from the property (reason "resident removed").

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: Communities, invitations and community settings are scoped to one community, which belongs to one management company. Company Default Settings and Company Administrator grants are scoped to the management company. The portfolio is the one intentional cross-community surface: it is limited to Company Administrators and lists only that company's communities, with summary fields only. Cross-community access stays denied by default (025 FR-013). Community scope never follows the parent link.
- **Authorization**: Every action is checked server-side on every request, from the persisted Company Administrator grant and community memberships, through the 025 resolver extended with company-level capabilities (FR-004). The client's mode or route is never an input. Frontend checks only hide controls a user can't use. Constitution §3 ("an HOA MUST have at least one ... management administrator at all times") is enforced by FR-020 and by the activation rule in FR-010.
- **Ownership and moderation**: This spec adds no user-generated content beyond community profile text, which is entered by staff and rendered as text, never HTML.
- **API contract**: Uses the existing response and error shapes. Collections take `limit`/`offset` (default 25, max 100). All timestamps are UTC; formation and management start dates are plain dates. IDs are GUIDs; the community name is a display handle. Error codes added: `COMMUNITY_NAME_TAKEN`, `NO_ACTIVE_MANAGER`, `OPEN_WORK`, `COMMUNITY_ARCHIVED`, `COMMUNITY_NOT_ACTIVE`, `INVITATION_EXPIRED`, `INVITATION_INVALID`, `ALREADY_RESIDENT`, `LAST_COMPANY_ADMIN`; `LAST_MANAGER` is reused. The ARC settings endpoints keep their shape; only who may call them widens.
- **API implementation and docs**: New endpoints are FastEndpoints. Swagger stays available only in Development/Dev and is disabled in Production.
- **Database/runtime**: Forward-only migrations (spec 025 FR-005) add the management company, Company Administrator grants, company default settings and invitations, and move community status to Onboarding, Active and Archived (`Inactive` becomes Archived). They are applied idempotently at Cloud Run startup. The migration backfills one management company and links every existing community to it. Existing communities keep their status (Active stays Active). Short-lived DbContexts, within Neon's low connection limit. The last-manager and last-administrator rules must hold under concurrent requests.
- **File storage**: None. This spec stores no files.
- **Security and abuse controls**: All write endpoints use the `board-writes` rate limit, including sending manager and resident invitations (constitution §7). Invitation links are single-use, expire after 3 days, are bound to the invited email, and are stored only as hashes. The invitation acceptance page reveals only the community name and, for a resident invitation, the property address, and only to the holder of a valid link. Every membership, role, status, settings and administrator change is a sensitive event (constitution §7). Denials fail closed without revealing whether a community exists. Company Administrators get no board data by default (FR-003), following least privilege.
- **Observability**: Errors go to Sentry with environment and release tags, plus community ID and capability as tags where relevant. No emails, names or invitation tokens go to telemetry. Trace context flows from the frontend to the backend.
- **Accessibility**: The add-community form, Community settings page, portfolio, invitation acceptance and company defaults are fully keyboard operable, with labels, visible focus and validation messages tied to their fields. Status and the "No manager" and "differs from defaults" flags are conveyed by text, not color alone. Everything meets WCAG 2.1 AA.
- **Quality gates**: 95% coverage on new backend and frontend files. Sonar passes. xUnit integration tests on Testcontainers PostgreSQL, with isolated per-test communities and companies, so they are safe in parallel and after earlier runs. `[Theory]` data covers role (Company Administrator, Community Manager of this community, Community Manager of another community, Board Member, Accountant, Resident) × action, and status (Onboarding, Active, Archived) × write type. The required Serilog sensitive events are asserted. Repowise docs are refreshed for the PR. The PR stays a focused vertical slice per story.
- **Frontend testing**: Jasmine/Karma for permission-driven section rendering and portfolio flags. Angular Testing Library for the add-community form, Community settings sections, the Residents area (list, search, filters, invite, remove), portfolio filter and search, and invitation acceptance. Playwright for the journeys "add community → appoint manager → activate" and "invite resident → accept → see home", and for refusal of a Board Member on the settings route. Cypress E2E for sign-in as Company Administrator → portfolio → community settings. Storybook visual regression for the portfolio, settings page sections, Residents area and add-community form, once designs exist.
- **Executable & living spec**: Every acceptance scenario and Independent Test above maps to an automated test that runs on demand and passes before merge. This `spec.md` and `tasks.md` are updated before the implementation PR. The implementation PR MUST also reconcile the older specs this one changes:
  - spec 025's `Community` status (FR-001) and its "inactive/offboarded" edge case become Onboarding, Active and Archived;
  - spec 025 FR-042 gains co-manager appointment and removal by Community Managers, and management by Company Administrators;
  - spec 027 FR-030 widens to "Company Administrator or that community's Community Manager";
  - spec 016's claim code is no longer the only way a resident joins a property (resident invitations, FR-024).
- **Spec independence & parallelism**: Hard dependencies on spec 025 (communities, memberships, resolver, board shell) and spec 027 (ARC settings), both merged, so this spec is individually completable now. The Notification Settings spec is optional: its sections stay hidden until it lands. The in-progress Resident Architectural Application Submission spec (`029-resident-arc-requests`) does not block this one. Its writes are refused in Onboarding and Archived communities through the shared status rule (FR-011, FR-012), so the two can be built in parallel. US1–US3 and US8 are the MVP; US4–US7 can be built in parallel once US1 lands.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Company Administrator can add a community, appoint its first manager and take it live in under 5 minutes, with no engineering, database or seed-script involvement.
- **SC-002**: 100% of this spec's actions refuse every role the permissions table denies, verified by an automated test per role and action.
- **SC-003**: No community that has had an active manager is ever left with zero active managers, through any surface, including simultaneous requests.
- **SC-004**: 100% of new communities start with settings equal to the company defaults in effect when they are created, and 0 existing communities change when a default changes.
- **SC-005**: After a community is archived, 0 writes in it are accepted and 0 autopay charges start for its properties, across the automated suite.
- **SC-006**: 100% of invitation links stop working 3 days after sending, after a resend, after revocation or after use.
- **SC-007**: Every change made through this spec produces exactly one sensitive event with all required fields.
- **SC-008**: The portfolio shows its first page within 2 seconds for a company with 200 communities.
- **SC-009**: A Company Administrator can find every community without an active manager in one step (the "No manager" flag on the portfolio).
- **SC-010**: A manager can invite a resident to a property in under 1 minute, and the resident can go from the invitation email to seeing their home in under 5 minutes.
- **SC-011**: 100% of removed residents lose access to that property on their next request, and 0 autopay charges start for that property on their behalf afterwards.
- **SC-012**: 0 resident invitations are emailed while their community is Onboarding, and 100% of scheduled invitations are emailed when it is marked Active.

## Assumptions

- **One company**: There is one management company per deployment. The migration creates it and links every existing community to it. Multi-company tenancy is out of scope, but nothing in this spec prevents it.
- **Restore goes to Active**: A restored community returns to Active, not Onboarding, because it was live before.
- **Archived history**: "Readable for history and audit" means the archived community's profile, settings and membership history in this spec's pages. Other features' records (applications, payments, ledger) are kept unchanged but are not shown through new surfaces here. A reporting or audit spec can add that later.
- **Company Administrator grants go to existing accounts only**. The person creates their account first. Invitations (FR-022) are for community memberships.
- **Invitation resend**: Not answered by the owner. This spec lets anyone who could make the appointment resend or revoke it.
- **Accepting an invitation** uses the existing registration, sign-in and email verification (spec 016). No new sign-up flow is designed here.
- **Properties for a new community** come in through the existing import and seed path. Adding and editing properties is not part of this spec; the Residents area lists the properties that exist.
- **"Resident"** means any person linked to a property, as today. This spec does not tell owners and tenants apart.
- **Emails** (manager and resident invitations, autopay cancelled on archive, resident removed) use the existing transactional email. They are plain text until Claude Design delivers templates (brief in `design/`).
- **Desktop-first**, like the rest of the board side (spec 025). Mobile layout is out of scope.
- **Demo data**: The dev seed gets one Company Administrator (via the setup step) and one Onboarding community, so the flow can be tried end to end.

## Out of Scope

- Platform or application administration: environments, feature flags, secrets, deployments, database access.
- Billing the management company, or its contracts with communities.
- Resident account support tools: password resets and unlocking accounts. These may become a separate "support" spec.
- Notification Settings themselves (a separate spec). This spec only reserves the defaults section and the per-community section.
- Resident Architectural Application Submission (a separate spec).
- Several management companies sharing one deployment (multi-company tenancy).
- Other per-community settings, such as payment policy (`HoaPaymentConfig`), as company defaults. They can be added to the defaults later.
- Restricting resident rights, such as suspending poll voting (owner decision, 2026-10-10).
- Bulk-importing residents from a file. Invitations are one at a time in this spec; a bulk import can follow.
- Adding, editing or removing properties.
- Telling owners and tenants apart.

## Dependencies

- **Spec 025** (merged): communities, memberships, scope resolver, the membership create, update and list endpoints, and the board shell.
- **Spec 027** (merged): ARC settings, which this spec moves under Community settings and copies from company defaults.
- **Spec 006** (merged): recurring payments (autopay), cancelled when a community is archived.
- **Spec 016** (merged): registration and email verification, reused for invitation acceptance. Its `UserProperty` resident link and claim codes stay as they are, and resident invitations create the same link.
- **Notification Settings spec** (optional): its sections stay hidden until it lands.
