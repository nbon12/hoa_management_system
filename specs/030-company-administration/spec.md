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

Because invitations reach people who have no account yet, and managers and Company Administrators often don't live in a community they run, this spec also lets a person **create an account from an invitation** and **sign in without a linked home**.

There is one management company per deployment for now. The data model must not prevent several companies later.

## Grounding: what exists today

Verified against `main` at commit `21e2491` (spec 027 merged):

- **Communities exist but have no lifecycle.** `Community` (spec 025) has these fields: legal name, community name (unique handle), county, formation date, management start date, description, an optional parent, and a status of only `Active` or `Inactive`. There is no create, archive or restore surface. Communities come from seed data and the 025 backfill. Backfilled communities have an empty legal name and no county, formation date or management start date. The community-name uniqueness rule is global and case-sensitive.
- **The community-scope resolver denies everything in a non-Active community.** `CommunityScopeResolver` denies any community whose status is not `Active`, whatever the caller's membership. Its capabilities are `ViewAssociationData`, `ManageMemberships`, `ViewArchitecturalApplications`, `VoteArchitecturalApplications` and `ManageArchitecturalReview`. There is no company-level capability. Board mode, "My Communities" and the board route guard also count only memberships in Active communities.
- **Membership admin is manager-only.** The 025 membership create, update and list endpoints need `ManageMemberships`, which only a Community Manager has.
  - A manager can already grant any non-Resident role in their community, including another Community Manager. Creating a `Resident` membership is refused.
  - Create takes a user ID, not an email, so only someone who already has an account can be granted a role.
  - A user holds at most one membership row per role in a community, and the row is kept after it ends.
  - Update refuses to leave a community with zero managers who are active *today* (`LAST_MANAGER`). It does not look at future end dates.
  - The create and update endpoints have no rate limit.
- **There is no invitation mechanism.**
- **Registration needs a claim code, and sign-in needs a home.**
  - A new account can only be registered with a valid property claim code (specs 016/017). The account is then always linked to that code's property.
  - Registration is refused (generic `REGISTRATION_FAILED`) when that property already has a linked user.
  - Sign-in, token refresh and the Resident/Board mode switch all need at least one linked property; otherwise they fail with `NO_PROPERTY`. Access tokens always carry a property and a community claim.
  - The dev seed works around this by giving its board user a co-residence property.
- **Residents link themselves to a home.** A resident reaches a property through a `UserProperty` link. The only way to create one is for the resident to redeem a single-use claim code, which is mailed to the owner's contact on file. Spec 017 sub-spec A deliberately ruled out any staff-initiated way to link a user to a property. A manager has no way to add or remove a resident.
- **Resident endpoints trust token claims.** These endpoints read the property (or community) from the 15-minute access token: ledger, statements, receipts, transactions, unpaid assessments, one-time and recurring payments, setup intent, drafts, alert preferences, community directory, violations and poll vote. They do not re-check the `UserProperty` link or the community's status on each request.
- **ARC settings are per community** (spec 027, `CommunityArcSettings`): review period, lapse rule, decision rule, reminder days, time zone and formal disapproval statement (at most 1,000 characters).
  - A community's settings row is created lazily from hard-coded defaults; a community without a row reads those defaults.
  - The same row holds the community's ARC application-number counter.
  - The settings are edited at `board/arc-settings` by a Community Manager only.
  - The settings endpoints use the same capabilities as recording outcomes (`ManageArchitecturalReview`) and reading applications (`ViewArchitecturalApplications`).
- **Autopay is one schedule per property** (spec 006, `RecurringPayment`). The schedule is billed to the owner of record's payment account and does not record which user enrolled it. The scheduled draft job ignores community status. Residents also vote in **community polls**.
- **No property import exists.** Properties come only from seed code.
- **The `board-writes` rate limit** (spec 027) is keyed per signed-in user. Callers who are not signed in share one bucket.
- **There is no company entity and no company-level role.**

## Clarifications

### Session 2026-10-08 (owner comment on issue #213)

- Q: How do the existing roles combine? → A: A Board Member can also be a Community Manager. A Community Manager does not need to be a Board Member or a resident. A Board Member does not need to be a Community Manager. (This matches the spec 025 rule that a user's capability in a community is the union of their active roles.)
- Q: What do Community Managers do? → A: Community Managers create resident accounts, add communities, add or remove residents, and can restrict resident rights such as voting. → This spec lets a Community Manager **add a community** (US1), which replaces the draft's "manager cannot create communities". For resident accounts and resident rights, see Session 2026-10-10.
- Q: Should a new community start as Onboarding or go straight to Active? → A: A new community starts as **Onboarding**, hidden from residents until a Company Administrator marks it Active.
- Q: Can a Community Manager appoint co-managers? → A: **Yes.** A Community Manager may appoint co-managers in their own community.
- Q: How long do invitations last? → A: **3 days.** (Who may resend was not answered. This spec lets anyone who could send the invitation resend or revoke it; see FR-022.)
- Q: Should archiving end everyone's memberships? → A: Owner: "if it means cancelling their payment subscriptions, yes — archiving a community should end the payment subscription." → **Archiving** means the company stops managing the community. It **cancels every autopay schedule** in that community. Access memberships are kept but confer nothing while archived (FR-012, FR-013, FR-015).
- Q: Should changing a company default offer to push the change to existing communities? → A: **No.** A changed default applies only to communities created afterwards.

### Session 2026-10-10

- Q: May a Community Manager end or downgrade **another** manager in their community? → A: **Yes.** A Community Manager may remove or downgrade co-managers in their own community, but never the last manager (FR-019, FR-020).
- Q: Are resident accounts, adding and removing residents, and restricting resident rights in this spec? → A: **Partly.** Managers **invite residents to a property by email** and **remove them** (US8). Restricting resident rights, such as suspending poll voting, is **not built**. The Residents page needs a Claude Design design, as do the other new screens; the briefs are in `design/` (see Design references).

### Decisions made in spec review (2026-10-11)

An adversarial review of this spec against the code and the merged specs forced the decisions below. They are **not** owner decisions. Each is the safest default that makes the owner's decisions work. The owner may override any of them.

1. **Sign-up from an invitation.** An invitee with no account creates one from the invitation link, without a claim code (FR-022a).
2. **Sign-in without a home.** A user with no linked home can still sign in. They land in board mode, the company area, or a "No home linked" page (FR-036a).
3. **Appointing by email always sends an invitation**, even when the email already has an account. Otherwise a manager could learn who has an account. Direct appointment is only for people already in that community (FR-018).
4. **Nobody grants a role or a home to themselves** (FR-003a).
5. **A community "has a manager" only while at least one manager membership has no end date.** Otherwise a manager's term could run out and leave a live community with no manager (FR-020).
6. **Restore returns a community to Active only if it was live before and still has a manager.** Otherwise it returns to Onboarding (FR-010).
7. **Invitations act on their sender's authority.** They are revoked if the sender loses it before they are accepted or sent (FR-022).
8. **Autopay records who enrolled it.** Removing a resident cancels only the autopay that resident enrolled (FR-024c).
9. **An archived community's homes stay visible to their residents**, marked "No longer managed", with payment history readable. Every linked resident is emailed when it is archived (FR-012a, FR-013).
10. **When a manager adds a community, every Company Administrator is emailed**, because only they can take it live (FR-008).
11. **Each resident endpoint re-checks the resident's link to the home on every request.** A removed resident loses the home at once, not when their token expires (FR-024g).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add a community the company has taken on (Priority: P1)

As a Company Administrator or a Community Manager, I add a new community with its legal name, community name (a unique handle), county, formation date, management start date, an optional description and an optional parent (master) association. The new community starts as **Onboarding**. It gets the company's default settings (FR-025). If I am a Community Manager, I become its first manager, and the Company Administrators are told it is waiting to go live.

**Why this priority**: Without it, every new HOA the company takes on needs an engineer to write seed data. Nothing else in this spec matters until a community can be created in the product.

**Independent Test**: As a Company Administrator, add "Keystone Park HOA, Inc." with community name "Keystone Park". Confirm it is saved as Onboarding, appears in the portfolio, and its ARC settings equal the company defaults. Repeat as a Community Manager. Confirm the manager holds an open-ended manager membership in the new community and every Company Administrator is emailed. Try a duplicate name and a non-manager user and confirm both are refused with nothing created.

**Acceptance Scenarios**:

1. **Given** I am a Company Administrator, **When** I add "Keystone Park HOA, Inc." with community name "Keystone Park", county "Hamilton", formation date 03/14/2004 and management start date 11/01/2026, **Then** it is saved with status Onboarding and no parent, it has zero Community Manager memberships, it appears in my portfolio, and its ARC settings equal the company defaults at that moment.
2. **Given** a community named "Keystone Park" already exists and I am a Company Administrator, **When** I add another with the community name "keystone park " (different case, trailing space), **Then** it is refused with `COMMUNITY_NAME_TAKEN` and a message naming "Keystone Park", and nothing is created.
3. **Given** a community named "Keystone Park" exists and I am a Community Manager who does not manage it, **When** I add a community with that name, **Then** it is refused with `COMMUNITY_NAME_TAKEN` and the message "That community name is unavailable", without naming or describing the existing community.
4. **Given** I hold an effective Community Manager membership in at least one Onboarding or Active community, **When** I add "Keystone Ridge HOA, Inc." with community name "Keystone Ridge", **Then**:
   - it is saved as Onboarding;
   - I hold an active Community Manager membership in it with no end date, and it appears in my community switcher on my next request;
   - every Company Administrator is emailed that "Keystone Ridge" was added by me and is waiting to be marked Active, with a link to its Community settings page.
5. **Given** I am neither a Company Administrator nor an effective Community Manager anywhere (for example a Board Member, an Accountant or a Resident only), **When** I try to add a community, **Then** I am refused with 403 `FORBIDDEN` and nothing is created.
6. **Given** my only Community Manager membership is in an Archived community, **When** I try to add a community, **Then** I am refused with 403 `FORBIDDEN` and nothing is created.
7. **Given** I am a Company Administrator and "Keystone Crossing Master" exists, **When** I add a community with that parent, **Then** the new community is a sub-association of it. **And** a Board Member of the master gets no access to the new community (spec 025: scope is per community).
8. **Given** I am a Community Manager of community A only, **When** I add a community and choose community B as its parent, **Then** it is refused, nothing is created, and the response is the same whether B exists or not (FR-004). The parent choices offered to me are only the communities I manage.
9. **Given** I leave legal name, community name, county, formation date or management start date empty, or enter a formation date in the future, **When** I save, **Then** it is refused with a message naming each field at fault.

---

### User Story 2 - Appoint and change a community's managers (Priority: P1)

As a Company Administrator, I appoint a Community Manager for any community, including its first, and I can end or replace a manager. As a Community Manager, I can appoint and remove co-managers in my own community. I appoint someone already in the community directly; anyone else, I invite by email.

**Why this priority**: A community created by a Company Administrator has no manager, and without a manager nobody can run it day to day. A community must never silently lose its last manager.

**Independent Test**: As a Company Administrator, invite a new email as manager of a new community. Accept within 3 days by creating an account from the link, and confirm the new manager lands in board mode for that community. Let a second invitation pass 3 days and confirm the link is refused. Try to end, or end-date, a community's only manager and confirm it is refused.

**Acceptance Scenarios**:

1. **Given** a new community with no manager, **When** I (a Company Administrator) appoint "pat@example.com" as its Community Manager, **Then** an invitation is emailed to that address and the community shows the invitation as pending. **When** Pat accepts within 3 days, **Then** Pat holds an active Community Manager membership with no end date, starting on the acceptance date.
2. **Given** Pat has no account, **When** Pat opens the invitation link and creates an account with a name and password (no claim code), **Then** the account is created for pat@example.com with the email confirmed, the membership is created, and Pat signs in to board mode for that community, although Pat has no linked home.
3. **Given** I am a Community Manager of A and Lee is a Board Member of A, **When** I appoint Lee as a co-manager, choosing Lee from A's people list, **Then** Lee holds an active Community Manager membership in A at once, with no invitation, and A has two active managers.
4. **Given** "has-account@example.com" belongs to an existing account and "no-account@example.com" does not, **When** I appoint each by email as a Community Manager of A, **Then** both requests return the same response (invitation pending, no name shown) and both are emailed an invitation.
5. **Given** an invitation was sent more than 72 hours ago and not accepted, **When** its link is used, **Then** it is refused with `INVITATION_EXPIRED` and no membership is created.
6. **Given** a pending or expired invitation, **When** I (who could send it) resend it, **Then** a new link valid for 72 hours is emailed, and the previous link is refused with `INVITATION_INVALID`.
7. **Given** a pending invitation to ann@example.com, **When** bob@example.com signs in and tries to accept it, **Then** it is refused with `INVITATION_EMAIL_MISMATCH` without showing the invited email, no membership is created, and the invitation stays pending.
8. **Given** I am a Community Manager of community A only, **When** I try to appoint a manager in community B, **Then** I am refused with 403 `FORBIDDEN`, and the response is the same as for a community that does not exist.
9. **Given** a community whose only manager has no end date, **When** I end that manager without naming a replacement, **Then** it is refused with `LAST_MANAGER` and the manager stays active.
10. **Given** a community whose only manager has no end date, **When** I set that manager's end date to 12/31/2026, **Then** it is refused with `LAST_MANAGER` and the end date stays empty.
11. **Given** I am a Company Administrator, Pat is A's only manager, and Lee is a Board Member of A, **When** I replace Pat with Lee in one action, **Then** Lee holds an active, open-ended Community Manager membership and Pat's manager membership is ended, both in one transaction. **When** the replacement is someone not in A, **Then** the replace is refused and Pat stays active.
12. **Given** I am a Community Manager of A and another open-ended manager exists in A, **When** I end my own manager membership, **Then** it ends with today's date and my next manager request in A is refused with 403 `FORBIDDEN`.
13. **Given** I am a Community Manager of A and A has a co-manager who also holds a Board Member membership in A, **When** I downgrade the co-manager to Board Member, **Then** their manager membership ends, their Board Member membership stays active, and they lose manager powers in A on their next request.
14. **Given** A has exactly two open-ended managers, **When** each tries to end the other's manager membership at the same moment, **Then** exactly one succeeds, the other is refused with `LAST_MANAGER`, and A keeps one open-ended manager.
15. **Given** co-manager X sent a Community Manager invitation and X is then removed as manager, **When** the invitation link is used, **Then** it is refused with `INVITATION_INVALID` and no membership is created.
16. **Given** I am a Community Manager of A, **When** I invite new@example.com as a Board Member with a term ending 12/31/2027 and they accept within 72 hours, **Then** they hold an active Board Member membership in A from the acceptance date to 12/31/2027.
17. **Given** I am a Community Manager of A, **When** I try to appoint myself a Board Member of A, or send an invitation to my own email, **Then** it is refused with `SELF_GRANT_NOT_ALLOWED` and nothing changes.
18. **Given** I am a Board Member, an Accountant or a Resident (and not a Community Manager there or a Company Administrator), **When** I try to create or end a Community Manager membership, **Then** I am refused with 403 `FORBIDDEN`.
19. **Given** any appointment, change, end, invitation, resend, revoke, or acceptance (successful or not), **When** it happens, **Then** one sensitive event is logged per change, with actor, community, the target user ID (or invitation ID when there is no account yet), the change and UTC time, and the event contains no email address, name or token.

---

### User Story 3 - Take a community live, edit its profile, and archive or restore it (Priority: P1)

As a Company Administrator, I move a community through its statuses: **Onboarding** while it is being set up, **Active** once it is live, and **Archived** when the company stops managing it. I can restore an archived community. A Community Manager can edit their own community's profile but not its status or parent.

**Why this priority**: Onboarding keeps a half-configured HOA away from residents. Archiving is the only way to stop managing an HOA without deleting its history, and it must stop autopay charges.

**Independent Test**: Create a community (Onboarding). Confirm a resident linked to one of its properties can't use it, appoint a manager, mark it Active, and confirm the resident can. Seed autopay on three properties and archive the community. Confirm:
- autopay is cancelled;
- every linked resident is emailed;
- writes are refused;
- residents can still read their payment history;
- the records remain readable to the Company Administrator.

Restore it and confirm memberships and settings are unchanged.

**Acceptance Scenarios**:

1. **Given** an Onboarding community and Ann, a resident linked to one of its properties, **When** Ann signs in, **Then** that home is not in her home list. Every resident request for it is refused with `COMMUNITY_NOT_ACTIVE`, including claim-code redemption and the reads and writes listed in FR-012a.
2. **Given** an Onboarding community, **When** its Community Manager creates a Board Member membership and saves ARC settings, **Then** both succeed. **When** its Board Member reads board metrics, **Then** that succeeds. **When** the Board Member tries to save ARC settings, **Then** it is refused with 403 `FORBIDDEN`, exactly as in an Active community.
3. **Given** an Onboarding community with at least one open-ended Community Manager and two scheduled resident invitations, **When** I (a Company Administrator) mark it Active, **Then** its status is Active, its linked residents see it on their next request, and both invitations are emailed.
4. **Given** an Onboarding community with no open-ended Community Manager, **When** I mark it Active, **Then** it is refused with `NO_ACTIVE_MANAGER` and it stays Onboarding.
5. **Given** an Active community with no open work, a pending manager invitation and a pending resident invitation, **When** I archive it, **Then**:
   - it is removed from board and manager community switchers;
   - both invitations are revoked and their links are refused with `INVITATION_INVALID`;
   - its profile, settings and Managers and memberships stay readable to me;
   - nothing is deleted.
6. **Given** an archived community, **When** one of its Board Members tries to vote on an architectural application, **Then** it is refused with `COMMUNITY_ARCHIVED`. **When** a user with no membership or home in it makes the same request, **Then** it is refused with 403 `FORBIDDEN`, the same as for a community that does not exist.
7. **Given** an Active community where three properties have active autopay, **When** I archive it, **Then**:
   - all three schedules are cancelled with the reason "community archived", and no further automatic charge starts for them;
   - every resident linked to the community's properties is emailed once that the company no longer manages the community, and the email says autopay has ended where that applies;
   - each cancellation is logged.
8. **Given** Jane's home is in a community that was just archived, **When** she signs in, **Then** the home is in her home list labelled "No longer managed". Its ledger, statements, receipts and transactions are readable. Every other resident request for it is refused with `COMMUNITY_ARCHIVED`.
9. **Given** Jane holds an access token issued before her community was archived, **When** she uses it to enroll autopay or make a payment, **Then** it is refused with `COMMUNITY_ARCHIVED` and nothing is created.
10. **Given** a community with architectural applications ARC-1041 (Open) and ARC-1042 (Decision Reached), **When** I try to archive it, **Then** it is refused with `OPEN_WORK` and the message lists ARC-1041 and ARC-1042 with their statuses.
11. **Given** a master community with a sub-association that is not archived, **When** I try to archive the master, **Then** it is refused with `OPEN_WORK` and the message lists that sub-association.
12. **Given** an archived community that was Active before it was archived and still has an open-ended manager, **When** I restore it, **Then** it returns to Active with its memberships and settings unchanged. No autopay is restarted (residents enroll again), and the invitations revoked by the archive stay revoked.
13. **Given** a community that was archived while still Onboarding, **When** I restore it, **Then** it returns to Onboarding and stays hidden from residents.
14. **Given** an archived community, previously Active, whose only manager's end date passed while it was archived, **When** I restore it, **Then** it returns to Onboarding and the portfolio flags it "No manager". **When** I then try to mark it Active before appointing a manager, **Then** it is refused with `NO_ACTIVE_MANAGER`.
15. **Given** an archived community whose manager M has left the company, **When** I end M's manager membership while it is archived, **Then** it ends (the last-manager rule does not apply while archived). **When** I then restore the community, **Then** it returns to Onboarding and M has no access.
16. **Given** an archived sub-association whose parent is also archived, **When** I try to restore the sub-association, **Then** it is refused with `PARENT_ARCHIVED`, naming the parent.
17. **Given** I am the Community Manager of an Active community, **When** I edit its legal name, community name, county, formation date, management start date or description, **Then** the change is saved and logged with old and new values.
18. **Given** "Keystone Park" exists and I manage "Keystone Ridge", **When** I rename Keystone Ridge to " KEYSTONE PARK", **Then** it is refused with `COMMUNITY_NAME_TAKEN` and the name is unchanged.
19. **Given** a community from the spec 025 backfill with an empty legal name and no county, **When** I change only its description, **Then** the change is saved, and the Profile section flags legal name and county as missing.
20. **Given** I am a Community Manager, **When** I try to activate, archive, restore or change the parent of my community, **Then** I am refused with 403 `FORBIDDEN` and nothing changes.
21. **Given** I am a Community Manager of community A only, **When** I try to edit community B's profile, **Then** I am refused with 403 `FORBIDDEN`, and the response is the same as for a community that does not exist.

---

### User Story 4 - Company-wide default settings (Priority: P2)

As a Company Administrator, I set the defaults a new community starts with, so every new HOA follows our standard setup until its own governing documents say otherwise.

Defaults cover, at minimum, architectural review (spec 027): review period in days, lapse rule (flag overdue only, deemed approved, or deemed denied), decision rule (majority of members, or majority of votes cast with a quorum), reminder days, time zone, and the formal disapproval statement. Notification defaults are added once the Notification Settings spec exists; until then that section is not shown.

**Why this priority**: Without defaults, each new community starts from hard-coded values and someone must remember to fix each one. It is P2 because, until it lands, new communities start from spec 027's built-in values (see Spec independence).

**Independent Test**: Change the default lapse rule to "deemed approved", create a community and confirm it starts with "deemed approved". Confirm an existing community and its applications already received are unchanged. As a Community Manager, confirm the defaults can be read but not changed.

**Acceptance Scenarios**:

1. **Given** I am a Company Administrator, **When** I change the default lapse rule to "deemed approved" and a community is created afterwards, **Then** the new community starts with "deemed approved" and every existing community keeps its own lapse rule.
2. **Given** a community created before the change (including one whose ARC settings were never saved), and applications it already received, **When** I change any default, **Then** neither that community's settings nor the rules already copied onto those applications change (spec 027 FR-030).
3. **Given** I am a Community Manager, **When** I open "Company defaults" from my community's Architectural review section, **Then** I can read them. **When** I try to change them, **Then** I am refused with 403 `FORBIDDEN`.
4. **Given** I am a Board Member, an Accountant or a Resident, **When** I try to read the company defaults, **Then** I am refused with 403 `FORBIDDEN`.
5. **Given** I enter a review period of 0 or 400 days, reminder days of 31, or a statement over 1,000 characters, **When** I save, **Then** it is refused with a message naming the allowed range (the same ranges as spec 027 FR-028a and FR-029).
6. **Given** I open the company defaults, **When** the page renders, **Then** exactly one section, Architectural review, is shown. (The Notification Settings spec updates this scenario when it adds its section.)

---

### User Story 5 - One settings page per community (Priority: P2)

As a Company Administrator or that community's Community Manager, I open one **Community settings** page with these sections:

- Profile, including status actions;
- Managers and memberships: managers, board and accountants, residents, and pending invitations;
- Architectural review;
- Notifications, shown only once the Notification Settings spec lands.

The Architectural review section is spec 027's ARC settings, moved under this page rather than rebuilt.

**Why this priority**: Settings are scattered today: ARC settings on their own page, memberships on another. One page makes setting up a new HOA a single task. The page and its Profile and Managers and memberships sections are part of the MVP foundation (see Spec independence). This story adds the Architectural review section, the reset, and the old-location redirect.

**Independent Test**: As a Company Administrator with no membership in the community, open its settings and confirm every section is editable, including "Reset architectural review to company defaults". Open it as that community's manager and confirm status and parent are read-only. Open it as a Board Member and confirm you are refused.

**Acceptance Scenarios**:

1. **Given** I am a Company Administrator with no membership in community A, **When** I open A's settings from the portfolio, **Then** exactly Profile, Managers and memberships, and Architectural review are shown, every section is editable, and the status actions for A's status and "Reset architectural review to company defaults" are offered.
2. **Given** I am A's Community Manager, **When** I open A's settings, **Then**:
   - Profile is editable except status and parent;
   - Architectural review is editable as in spec 027;
   - Managers and memberships lets me grant and end Board Member and Accountant memberships, appoint and remove co-managers (never the last manager), and invite and remove residents (US8).
3. **Given** I am a Board Member, an Accountant or a Resident of A (and not its manager or a Company Administrator), **When** I open A's settings, **Then** the request is refused with 403 `FORBIDDEN` and the app takes me to my landing page (spec 025 FR-028).
4. **Given** I change any setting, **When** I save, **Then** one sensitive event is logged with the old and new values (spec 027 FR-030 pattern).
5. **Given** a community whose ARC review period is 45 days while the company default is 30, **When** I choose "Reset architectural review to company defaults" and confirm, **Then** every ARC rule setting equals the current company defaults, applications already received keep their copied rules, the next ARC application number is unchanged, and the reset is logged with old and new values.
6. **Given** I go to the old ARC settings location from spec 027, **When** it loads, **Then** I land on the Architectural review section of this page.
7. **Given** an archived community, **When** I (a Company Administrator) open its settings, **Then** every section is shown read-only, including Residents. Invitations revoked by the archive are labelled "Revoked — community archived". The only actions offered are Restore and ending memberships (FR-012).
8. **Given** A has a pending Board Member invitation and an expired Community Manager invitation, **When** I (A's manager) open Managers and memberships, **Then** both are listed with email, role, sent by, sent at and expires at, each with Resend and Revoke.

---

### User Story 6 - Portfolio view (Priority: P3)

As a Company Administrator, I see every community the company manages in one list, with its status, its parent, its managers, whether it has any manager, and whether its ARC settings differ from the company defaults. I can filter by status and search by name.

**Why this priority**: It is how the company spots a community nobody is running. A minimal list (name, status, link to settings) is part of the MVP foundation as the Company Administrator's entry point; this story adds the flags, filter and search.

**Independent Test**: Seed communities in each status, one with no manager and one whose ARC settings differ from the defaults. Confirm the flags, the status filter and the search. Confirm a Community Manager is never offered the view and is refused if they request it.

**Acceptance Scenarios**:

1. **Given** a community with no open-ended Community Manager, **When** I open the portfolio, **Then** its row is flagged "No manager".
2. **Given** a community whose only manager is a pending invitation, **When** I open the portfolio, **Then** its row is flagged "No manager" and shows "invitation pending".
3. **Given** a community whose ARC review period differs from the company default, **When** I open the portfolio, **Then** its row shows that its ARC settings differ from the defaults.
4. **Given** communities in Onboarding, Active and Archived, **When** I filter by Archived, **Then** only archived communities are listed.
5. **Given** I type "Keystone", **When** the list filters, **Then** only communities whose community name or legal name contains "Keystone" (any case) are listed.
6. **Given** I am a Community Manager, **When** I use the app, **Then** the portfolio is never offered to me. **When** I request it directly, **Then** I am refused with 403 `FORBIDDEN`. My own communities stay in my existing switcher.

---

### User Story 7 - Grant and revoke the Company Administrator role (Priority: P2)

As a Company Administrator, I grant the role to another person by email and revoke it. The very first Company Administrator is created by a one-time setup step run by whoever deploys.

**Why this priority**: Without it, the role can only be given by an engineer. The setup step itself is part of the MVP foundation; this story adds granting and revoking.

**Independent Test**: Run the setup step on an empty deployment and confirm one Company Administrator exists; run it again and confirm nothing changes. Grant a second administrator, revoke the first, and confirm the last one cannot be revoked. Confirm holding the role gives no vote.

**Acceptance Scenarios**:

1. **Given** I am a Company Administrator, **When** I grant the role to "ops@example.com", **Then** a Company Administrator invitation is emailed (72 hours, single use, bound to that email). **When** it is accepted, **Then** that person can reach the company area on their next request.
2. **Given** there are two Company Administrators, **When** one revokes the other, **Then** the revoked user loses the role on their next request, and the pending invitations they sent that they can no longer send are revoked.
3. **Given** I am the only Company Administrator, **When** I try to revoke my own role, **Then** it is refused with `LAST_COMPANY_ADMIN`.
4. **Given** two Company Administrators each try to revoke the other at the same moment, **When** both requests are processed, **Then** exactly one succeeds, the other is refused with `LAST_COMPANY_ADMIN`, and one Company Administrator remains.
5. **Given** I am a Community Manager and not a Company Administrator, **When** I try to grant or revoke the role, **Then** I am refused with 403 `FORBIDDEN`.
6. **Given** a deployment with no Company Administrator, **When** the deployer runs the setup step from the command line for "owner@example.com", **Then**:
   - if that email has an account, it becomes the first Company Administrator;
   - otherwise a Company Administrator invitation is emailed to it;
   - the step prints what it did.
7. **Given** a Company Administrator already exists, **When** the setup step is run again, **Then** it exits successfully, prints "A Company Administrator already exists; nothing changed", and writes nothing.
8. **Given** a Company Administrator already exists, **When** the deployer runs the setup step in recovery mode for "owner@example.com", **Then** that person is granted the role (or invited), a sensitive event is logged, and every current Company Administrator is emailed about it.
9. **Given** I am a Company Administrator with no Board Member membership in community A, **When** I try to vote on an architectural application in A or record its outcome, **Then** I am refused with 403 `FORBIDDEN`.
10. **Given** I am a Company Administrator, **When** I try to grant myself a Board Member membership in community A, or invite my own email as a resident of a property, **Then** it is refused with `SELF_GRANT_NOT_ALLOWED`.
11. **Given** any grant, revoke or setup-step run, **When** it happens, **Then** one sensitive event is logged with actor (or "setup"), target user ID or invitation ID, the change and UTC time.

---

### User Story 8 - Invite and remove residents (Priority: P1)

*(Numbered 8 so US1–US7 keep the issue's numbering; it is P1.)*

As a Community Manager, or a Company Administrator, I open the **Residents** area of a community's Managers and memberships section. I see every property in the community with its residents and any invitations. I invite a resident to a property by email. The person accepts and can then see that home in resident mode. I can remove a resident from a property, for example after a sale or a move-out.

**Why this priority**: A new HOA isn't useful until its residents are on it. Today residents can only join with a claim code mailed to the owner's contact on file. That needs owner records the company may not have yet, and it stops working once a home has any resident. Inviting by email is how a manager brings a community's residents on board.

**Independent Test**: As a manager of an Active community with properties, invite a new email to one property. Accept within 72 hours by creating an account from the link, and confirm the resident sees that home. Invite to an Onboarding community and confirm nothing is sent until it is marked Active. Remove the resident and confirm:
- they lose that home on their very next request;
- the autopay they enrolled is cancelled;
- their other homes and roles are untouched.

**Acceptance Scenarios**:

1. **Given** I am the Community Manager of Active community A, which has the property "711 Keystone Park Dr #29", **When** I invite "jane@example.com" as a resident of that property, **Then** an invitation is emailed to that address. **When** Jane opens the link within 72 hours and creates an account (no claim code), **Then** she is linked to that property as a resident and sees it in resident mode.
2. **Given** #29 already has a resident, **When** I invite a second person to #29 and they accept, **Then** both are linked to #29.
3. **Given** "sam@example.com" already has an account linked to another home, **When** I invite that email to a property in A, **Then** Sam is emailed to sign in and accept. Nothing changes until he accepts. After he accepts, he sees both homes.
4. **Given** community A is Onboarding, **When** I invite a resident, **Then** the invitation is saved as "scheduled" and no email is sent. **When** I try to resend it, **Then** it is refused with `COMMUNITY_NOT_ACTIVE` and nothing is sent. **When** a Company Administrator marks A Active, **Then** every scheduled invitation whose sender still has authority is emailed, each valid for 72 hours from that moment.
5. **Given** a resident invitation was sent more than 72 hours ago and not accepted, **When** its link is used, **Then** it is refused with `INVITATION_EXPIRED` and no link to the property is made. **When** I resend it, **Then** a new link valid for 72 hours is emailed and the old link is refused with `INVITATION_INVALID`.
6. **Given** Jane is a resident of #29 and enrolled #29's active autopay, **When** I remove her from #29, **Then**:
   - her very next request for #29, even with an unexpired access token, is refused with `PROPERTY_ACCESS_DENIED`;
   - #29's autopay is cancelled with the reason "resident removed", and no further automatic charge starts;
   - Jane is emailed that she was removed from #29 and that its autopay ended, and any other resident of #29 is emailed that autopay ended;
   - her account and her other homes are unchanged;
   - the removal and the cancellation are each logged.
7. **Given** Sam and Jane are residents of #29 and Sam enrolled its autopay, **When** I remove Jane, **Then** #29's autopay stays active.
8. **Given** Jane is also a Board Member of A and #29 was her only home, **When** I remove her as a resident of #29, **Then** her Board Member membership is unchanged, and she can still sign in, landing in board mode (FR-036a).
9. **Given** "jane@example.com" is already a resident of #29, **When** I invite that email to #29, **Then** it is refused with `ALREADY_RESIDENT`.
10. **Given** "kim@example.com" has a scheduled or pending invitation to #29, **When** I invite that email to #29 again, **Then** it is refused with `INVITATION_PENDING`, and Resend is offered instead.
11. **Given** I am a Community Manager of A only, **When** I try to invite or remove a resident of a property in community B, **Then** I am refused with 403 `FORBIDDEN`, and the response is the same as for a property or community that does not exist.
12. **Given** I am a Board Member, an Accountant or a Resident (and not a Community Manager there or a Company Administrator), **When** I try to invite or remove a resident, **Then** I am refused with 403 `FORBIDDEN`.
13. **Given** community A has 120 properties, 7 of which have no resident, **When** I open the Residents area:
    - **Then** the first page shows 25 of 120 properties, each with its address, its residents (name, email, resident since), and its scheduled, pending or expired invitations with who sent them.
    - **When** I search "keystone park dr #29", **Then** exactly that property is listed.
    - **When** I filter "No resident", **Then** exactly the 7 properties with no resident are listed.
14. **Given** a community with no properties loaded, **When** I open the Residents area, **Then** it says that properties have not been loaded for this community yet and offers no invite action.
15. **Given** a resident invitation is pending, **When** I revoke it, **Then** its link is refused with `INVITATION_INVALID` and no link to the property is made.
16. **Given** I am a Company Administrator with no membership in Active community A, **When** I open A's Residents area from the portfolio, invite "jane@example.com" to #29, and later remove her, **Then** the invitation is emailed and, after removal, she loses #29 on her next request.
17. **Given** I am A's Community Manager, **When** I invite my own email as a resident of a property in A, **Then** it is refused with `SELF_GRANT_NOT_ALLOWED`.
18. **Given** I open or search the Residents area, **When** the list loads, **Then** the spec 025 FR-017 association-data access event is logged (actor, community, resource, UTC time).
19. **Given** any resident invitation, resend, revoke, acceptance (successful or not) or removal, **When** it happens, **Then** one sensitive event is logged per change with actor, community, property ID, target user ID (or invitation ID), the change and UTC time, and the event contains no email address, name, street address or token.

---

### User Story 9 - Sign in without a linked home (Priority: P1)

As a Company Administrator, Community Manager, Board Member or Accountant who doesn't live in a community I work in, or whose only home was removed, I can still sign in. I land where my work is.

**Why this priority**: Without it, US1–US3 and US8 fail in practice. Managers and Company Administrators who are not residents could never sign in, and removing someone's only home would lock them out of their board role.

**Independent Test**: Sign in as each kind of user with no linked home and confirm the landing place. Remove a Board Member's only home and confirm they still reach board mode.

**Acceptance Scenarios**:

1. **Given** I am a Company Administrator with no linked home and no community membership, **When** I sign in, **Then** I land on the portfolio.
2. **Given** I hold an effective Community Manager membership in an Onboarding or Active community and no linked home, **When** I sign in, **Then** I land in board mode for that community.
3. **Given** I have no linked home, no effective non-resident membership in a non-archived community, and no Company Administrator role, **When** I sign in, **Then** I land on a "No home linked to this account" page that tells me to use an invitation link or contact my community manager.
4. **Given** I have no linked home, **When** I call any resident endpoint, **Then** it is refused with `PROPERTY_ACCESS_DENIED` and nothing is shown or changed.
5. **Given** my only linked homes are in Onboarding communities and I hold no other role, **When** I sign in, **Then** I land on a "Your community isn't live yet" page, not an error.
6. **Given** I have homes in both an Active and an Archived community, **When** I sign in, **Then** I land on the home in the Active community.

---

### Edge Cases

- **A user is both a Company Administrator and a Community Manager**: their capability is the union of both. Neither role is derived from the other. FR-003a still stops them from granting anything to themselves.
- **A Company Administrator also sits on a board**: they vote only through a Board Member membership granted by someone else (FR-003, FR-003a).
- **Two people end a community's last two managers at the same moment**: exactly one succeeds. The community always keeps one open-ended manager (FR-020).
- **A manager's term would end with no open-ended manager left**: refused (FR-020). End-dated co-managers are allowed while at least one manager has no end date.
- **A community is archived while invitations are scheduled or pending**: they are revoked; their links are refused with `INVITATION_INVALID`.
- **An invitation is accepted by an account with a different email**: refused with `INVITATION_EMAIL_MISMATCH`; the invitation stays pending for the invited email; the refusal never shows the invited email; the attempt is logged.
- **A revoke and an acceptance of the same invitation arrive at the same moment**: exactly one wins (FR-022). The other is told the final state.
- **The invitee already holds the role, or is already linked to the property, when they accept**: the invitation is marked accepted and nothing is duplicated (FR-022).
- **The invitee's sender lost their authority before acceptance or before a scheduled send**: the invitation is revoked and not acted on (FR-022).
- **A former manager or board member is appointed again**: they get an active membership again for that role (FR-019).
- **A community name is changed**: the old name becomes free for another community. Archived communities keep their names reserved, so a restore never collides (FR-007).
- **A parent change would create a loop** (A under B under A): refused with `PARENT_CYCLE`. An archived community cannot be chosen as a parent (`PARENT_ARCHIVED`).
- **A default changes while a community is being created**: the community gets the defaults in effect at the moment it is saved.
- **Autopay is mid-charge when the community is archived or the enrolling resident is removed**: a charge already submitted settles normally, and processor-driven updates (refunds, ACH returns, disputes, reconciliation) keep applying afterwards (FR-012). No new charge starts.
- **A board member's term has already ended when the community is restored**: it stays ended. Restore does not change membership end dates.
- **A Community Manager who created a community later loses all their manager memberships**: the communities they created are unaffected, but their unsent and unaccepted invitations are revoked (FR-022).
- **An Onboarding community is abandoned before going live**: a Company Administrator can archive it directly (same open-work rule). Its scheduled resident invitations are revoked without ever being sent.
- **The same email is invited to two properties**: they are two separate invitations, each accepted on its own.
- **The last resident of a property is removed**: allowed. The property shows "No resident". The owner of record and the property's balance are unchanged.
- **A resident who joined with a claim code is removed**: same as any resident. See FR-024f for when claim codes still work.
- **A resident invitation is mistyped**: the manager revokes it (scheduled or pending) and sends a new one. Only the invited email can accept.
- **A claim code is redeemed for a property in an Onboarding community**: refused with `COMMUNITY_NOT_ACTIVE` after the code and the email-verification proof are checked; the code is not used up and no account or link is created.
- **A newly linked resident sees the property's earlier payment history**: this is existing spec 006/016 behavior for the property and is unchanged here.

## Requirements *(mandatory)*

> **Design references.** No wireframes exist for this feature yet; every UI requirement is `[no WFb]`. The briefs for Claude Design are in [`design/`](./design/README.md). There is one brief per screen or flow, written to add a section "8 · Company administration" to the existing "HOA Management CRM" Claude Design project, the same canvas as the spec 025 board wireframes (`WFb`). When the designs come back as a handoff bundle, they are saved under `design/` and the `[no WFb]` tags are replaced with design citations. Until then, the requirements below describe behavior and copy only. The board side keeps spec 025's visual language (025 FR-037).

### Permissions at a glance

| Action | Company Administrator | Community Manager of that community | Board Member, Accountant, Resident |
| --- | --- | --- | --- |
| Add a community | Yes, any non-archived parent | Yes, while an effective manager somewhere; becomes its first manager; parent only from communities they manage | No |
| Edit profile (legal name, community name, county, dates, description) | Yes | Yes | No |
| Change parent | Yes | No | No |
| Activate, archive, restore | Yes | No | No |
| Appoint a Community Manager | Yes | Yes (co-manager) | No |
| End their own manager membership | Yes; never the last manager (FR-020) | Yes; never the last manager | — |
| End or downgrade another Community Manager | Yes; never the last manager (FR-020) | Yes (co-managers); never the last manager | No |
| Replace the last manager in one action | Yes | No | No |
| Grant or end Board Member and Accountant memberships | Yes | Yes (spec 025 FR-042) | No |
| End memberships in an Archived community | Yes (end only) | No | No |
| Invite a resident to a property, or remove one; view the Residents area | Yes | Yes | No |
| Resend or revoke an invitation | Yes | Yes, for invitations they could send | No |
| Edit ARC settings, reset to company defaults | Yes | Yes | No |
| Read company defaults | Yes | Yes | No |
| Change company defaults | Yes | No | No |
| Portfolio view | Yes | No | No |
| Grant or revoke Company Administrator | Yes | No | No |
| Grant anything to themselves | No (FR-003a) | No (FR-003a) | No |
| Accept an invitation | Any person, signed in or signing up, with a valid link and the invited email | Same | Same |

### Company and roles

- **FR-001**: System MUST model the **management company** as its own record that owns its communities, its Company Administrators and its default settings. There is exactly one per deployment for now. Nothing in the model may assume there can only ever be one.
- **FR-002**: System MUST store a company-level **Company Administrator** role, separate from community memberships, and resolve it server-side on every request. It MUST NOT be derived from any community role, and no community role may be derived from it.
- **FR-003**: Holding the Company Administrator role MUST NOT by itself grant board powers in any community: no vote, no recording or resending of ARC outcomes, no reading of ARC applications or attachments, and no association-wide homeowner or financial data beyond what this spec's pages show. A Company Administrator who needs those must hold the matching community membership.
- **FR-003a**: Nobody may grant a membership, appointment, invitation, resident link or Company Administrator role to their own account or to their own email. Such a request MUST be refused with `SELF_GRANT_NOT_ALLOWED`. A Company Administrator who needs a community role must get it from another Company Administrator or from that community's Community Manager. (A Community Manager becoming the first manager of a community they add, FR-008, is not a grant.)
- **FR-004**: Every authorization check in this spec, except invitation acceptance, MUST be decided server-side through the spec 025 community-scope resolver. The resolver is extended so that its capabilities encode the permissions table exactly (FR-005). Capability names are set in the plan; the owner's examples are ManageCommunities, AppointCommunityManagers, ManageCompanyDefaults and ManageCompanyAdministrators. There are three kinds of check:
  - **(a) Community-scoped.** Passed by that community's effective Community Manager and by a Company Administrator, decided per community and never through the parent link. They cover:
    - editing the profile (except status and parent);
    - appointing, ending and downgrading Community Managers;
    - granting, ending and listing Board Member and Accountant memberships;
    - inviting, resending, revoking, removing and listing residents and invitations;
    - reading, editing and resetting ARC settings.
  - **(b) Company-level, shared.** Passed by a Company Administrator, or by a user with an effective Community Manager membership in at least one Onboarding or Active community. They cover adding a community and reading the company defaults. When a Community Manager chooses a parent, the parent MUST also pass check (a) for that user.
  - **(c) Company Administrator only.** They cover:
    - changing status and parent;
    - ending memberships in an Archived community;
    - changing company defaults;
    - granting and revoking Company Administrators;
    - the portfolio.

  A Company Administrator grant MUST NOT satisfy `ViewAssociationData`, `ViewArchitecturalApplications`, `VoteArchitecturalApplications` or `ManageArchitecturalReview`. Board Members keep their spec 027 read access to ARC settings.

  Invitation acceptance is not decided by the resolver. It needs a valid link and the invited email (FR-022).
- **FR-004a**: For any request naming a community, checks MUST run in this order:
  1. Authorize as if the community were Active, using the caller's effective membership, resident link, Company Administrator grant, or valid invitation or claim code. A caller who fails this check gets the spec 025 fail-closed 403 `FORBIDDEN`, identical to a nonexistent community, whatever the community's status.
  2. For an authorized caller, apply the community's status:
     - Archived: refuse with `COMMUNITY_ARCHIVED`, except Restore and ending memberships by a Company Administrator, and the reads allowed by FR-012 and FR-012a;
     - Onboarding: refuse resident actions with `COMMUNITY_NOT_ACTIVE` (FR-012a).
  3. Only then apply entity rules, for example `LAST_MANAGER` or spec 027's application rules.
- **FR-005**: Permissions MUST match the table above. Any action not listed for a role is refused.

### Communities

- **FR-006**: A Company Administrator, or a user with an effective Community Manager membership (active and not past its end date) in at least one Onboarding or Active community, MUST be able to add a community with these fields:
  - legal name (required, up to 200 characters);
  - community name (required, 2–80 characters);
  - county (required);
  - formation date (required, not in the future);
  - management start date (required);
  - description (optional, up to 2,000 characters);
  - parent (optional).

  `[no WFb]`
- **FR-007**: Community name MUST be unique within the management company, compared without regard to case or leading and trailing spaces. This includes Archived communities, which keep their names reserved. A conflict on create or rename MUST be refused with `COMMUNITY_NAME_TAKEN`. The message names the existing community only for a Company Administrator; anyone else is told "That community name is unavailable".
- **FR-008**: A new community MUST start as **Onboarding**.
  - **Added by a Community Manager**: in the same action, that manager MUST receive an active Community Manager membership in it with no end date, and every Company Administrator MUST be emailed that the community was added and is waiting to be marked Active, with a link to its Community settings page. `[no WFb]`
  - **Added by a Company Administrator**: it has no manager until one is appointed (US2).

  While a community has no manager, the company's Company Administrators are its management administrators for constitution §3. FR-037 guarantees at least one always exists.
- **FR-009**: At creation, the new community's ARC settings MUST be copied from the company defaults in effect at that moment (FR-025). After that the copies are independent: later default changes never alter them.
- **FR-010**: A community's status MUST be one of **Onboarding**, **Active** or **Archived**. The allowed changes, all by a Company Administrator only, are:
  - **Onboarding → Active.** Refused with `NO_ACTIVE_MANAGER` unless the community has at least one open-ended Community Manager membership (active, no end date). Activation emails the scheduled resident invitations (FR-024b).
  - **Onboarding or Active → Archived.** Subject to FR-014.
  - **Restore.** The community returns to Active only if it had been Active before it was archived and it has an open-ended Community Manager at the moment of restore. Otherwise it returns to Onboarding, and the activation rule above applies from there. Restore is never refused for lack of a manager. Restoring a sub-association whose parent is Archived is refused with `PARENT_ARCHIVED`, naming the parent. Restoring a master does not restore its sub-associations.

  The existing `Inactive` status MUST be migrated to Archived.
- **FR-011**: While a community is **Onboarding**, it MUST be hidden from residents (FR-012a). Resident invitations are scheduled, not sent (FR-024b). Its non-resident members (Community Managers, Board Members, Accountants) MUST be able to work in it with their normal capabilities. Company Administrators use this spec's pages for it; the role gives them no board-mode data (FR-003).
- **FR-012**: While a community is **Archived**:
  - it MUST NOT appear in any board or manager community switcher or in "My Communities";
  - every user-initiated write in it, across all specs, MUST be refused with `COMMUNITY_ARCHIVED` in the FR-004a order. This covers memberships, settings, votes, applications, poll votes, payments and autopay. The exceptions are Restore and a Company Administrator ending memberships, which is allowed even for the last manager;
  - writes driven by the payment processor MUST continue to apply to its properties: webhook status updates, refunds, ACH returns, disputes and reconciliation (spec 006 FR-014–FR-014e, FR-032, FR-033);
  - its memberships MUST be kept, but MUST confer no access;
  - its scheduled and pending invitations MUST be revoked, each logged as a revoke;
  - all of its Community settings sections MUST stay readable, read-only, to Company Administrators. This includes memberships with their dates, residents, and invitations (revoked ones labelled "Revoked — community archived");
  - residents keep the access in FR-012a;
  - none of its records may be deleted.
- **FR-012a**: What residents see depends on the status of the community their home is in. The status is checked on every request, from the home's current community, never from the token. A resident's home list and sign-in choose a home in an Active community first, then an Archived one; Onboarding homes are never chosen.

  | Status | The home | Resident reads | Resident writes |
  | --- | --- | --- | --- |
  | **Onboarding** | Left out of the resident's home list | All refused with `COMMUNITY_NOT_ACTIVE` | All refused with `COMMUNITY_NOT_ACTIVE` |
  | **Archived** | Stays in the home list, labelled "No longer managed" | Ledger, statements, receipts and transactions allowed (read-only) for every resident linked to the home; all others refused with `COMMUNITY_ARCHIVED` | All refused with `COMMUNITY_ARCHIVED` |

  The reads are dashboard, property, owner, ledger, statements, receipts, transactions, unpaid assessments, announcements, polls, events, documents, directory and violations.

  The writes are: owner profile and directory-field edits, alert preferences, one-time payment intent and confirm, setup intent, autopay enroll, change and cancel, poll vote, event RSVP, drafts, and claim-code redemption.

  Resident architectural applications are governed by the Resident Architectural Application Submission spec, which applies the same rule.
- **FR-013**: Archiving MUST cancel every active autopay schedule for properties in the community in the same action:
  - each schedule's status becomes cancelled with the reason "community archived", and each cancellation is logged;
  - no automatic charge may start afterwards; a charge already submitted settles normally;
  - every resident linked to the community's properties MUST be emailed once that the company no longer manages the community, saying that autopay has ended where that applies, and that payment history stays readable. `[no WFb]`

  This applies equally to communities migrated from `Inactive` (see Database/runtime).
- **FR-014**: Archiving MUST be refused with `OPEN_WORK` while the community has open work, and the refusal MUST list each item. In this spec, open work is:
  - architectural applications that are Open or Decision Reached (spec 027);
  - sub-associations that are not archived.

  Later specs MAY add their own kinds of open work.
- **FR-015**: Restoring MUST return the community to the status FR-010 gives, with its memberships (including their end dates) and settings unchanged.
  - Autopay cancelled by the archive MUST NOT restart. Residents enroll again themselves.
  - Invitations revoked by the archive are not reinstated.
  - Before restoring, the Company Administrator MUST be shown which memberships will confer access again, and can end any of them first. `[no WFb]`
- **FR-016**: A parent MAY be set when a community is added. A Company Administrator may choose any non-archived community; a Community Manager only one they manage. Changing the parent later is Company Administrator only.
  - A community MUST NOT become its own ancestor (`PARENT_CYCLE`).
  - An archived community cannot be chosen as a parent (`PARENT_ARCHIVED`).
  - The parent link MUST NOT change anyone's access (spec 025).
- **FR-017**: A Community Manager MUST be able to edit their own community's legal name, community name, county, formation date, management start date and description, but not its status or parent. A Company Administrator may edit all of them. The FR-006 rules apply on edit, with one difference: a required field that has a value cannot be cleared, but a field left empty by the spec 025 backfill may stay empty until filled. The Profile section flags any missing required fields.

### Managers and memberships

- **FR-018**: A Company Administrator MUST be able to appoint a Community Manager in any non-archived community. A Community Manager MUST be able to appoint co-managers in a community they manage. Appointment works in one of two ways:
  - **Direct.** For a person who already holds an effective membership or resident link in that same community, chosen from that community's people list. It takes effect at once.
  - **By email.** For anyone else. It ALWAYS creates an invitation (FR-022), whether or not the email has an account. The response and the sender's view MUST be identical in both cases (pending, no name shown) until the invitation is accepted.

  The same applies to Board Member and Accountant memberships (FR-021).
- **FR-019**: A Company Administrator MUST be able to end or downgrade any Community Manager membership. A Community Manager MUST be able to end or downgrade their own manager membership, or a co-manager's, in a community they manage (Clarifications 2026-10-10).
  - Both are subject to FR-020.
  - Downgrading a manager ends their manager membership and gives them an active Board Member membership. This reuses their Board Member membership if they had one.
  - Appointing someone who held that role before, directly or by invitation, gives them an active membership for that role again.
- **FR-020**: A community "has a manager" only while at least one Community Manager membership is active and has no end date (open-ended). A change that would leave a community that has a manager with no open-ended manager, now or on a later date, MUST be refused with `LAST_MANAGER`, also under concurrent requests. This covers:
  - ending, downgrading or deactivating a manager;
  - giving a manager an end date, or moving it earlier.

  Other rules:
  - A community's first manager MUST be open-ended. A manager membership MAY carry an end date only while another open-ended manager remains.
  - A Company Administrator MUST be able to replace the last manager in one action. The replacement must be someone who can be appointed directly (FR-018). The new open-ended membership and the end of the old one happen in one transaction, and the replace is logged as one appointment event and one end event. An invitation cannot replace the last manager until it is accepted.
  - The rule does not apply in an Archived community (FR-012), which is not live.
- **FR-021**: Board Member and Accountant memberships MUST remain manageable by the community's Community Manager (spec 025 FR-042) and MUST also be manageable by a Company Administrator, directly or by invitation (FR-018). A membership invitation MAY carry a term end date.
- **FR-022**: An **invitation** offers one of three things:
  - a community membership (Community Manager, Board Member or Accountant);
  - residency of one property (FR-024);
  - the Company Administrator role (FR-037).

  These rules apply to all three:
  - **Validity.** An invitation is valid for **72 hours** after it is emailed, measured in UTC, and is usable once. The email and the acceptance page show the expiry in the community's time zone (its ARC time zone setting; the company default for Company Administrator invitations).
  - **Bound to the email.** It can only be accepted by an account whose email matches the invited email, compared trimmed and case-insensitive. Acceptance happens when the invitee creates an account from the link (FR-022a), or when they sign in with a matching, verified account and confirm. It must happen before the invitation expires.
  - **Lifecycle.**
    - Membership and Company Administrator invitations start **pending** and are emailed at once, whatever the community's status. Resident invitations start **scheduled** in an Onboarding community (FR-024b) and **pending** otherwise.
    - **Scheduled** becomes pending at activation, or revoked on revoke or archive. A resend is refused with `COMMUNITY_NOT_ACTIVE` and sends nothing.
    - **Pending** becomes accepted, expired after 72 hours, or revoked on revoke or archive. A resend keeps it pending with a new link and a new 72 hours; the old link is refused.
    - **Expired** becomes pending on resend, or revoked on revoke.
    - **Accepted** and **revoked** are final. Resending or revoking them is refused with `INVITATION_INVALID`.
    - Every change of state is atomic. When an accept, revoke, resend or archive race, exactly one wins.
  - **Who may resend or revoke.** Anyone who could send the invitation can resend or revoke it. A resend makes the resender its sender.
  - **Refusals.** A link that cannot be accepted MUST be refused and create nothing. Checks run in this order:
    1. The invitation was revoked, already accepted, or replaced by a resend → `INVITATION_INVALID`, whatever its age.
    2. Otherwise, more than 72 hours have passed since this link was emailed → `INVITATION_EXPIRED`.
    3. Otherwise, the accepting account's email differs → `INVITATION_EMAIL_MISMATCH`. The invitation stays pending, and the refusal does not show the invited email.
  - **Sender authority.** An invitation acts on its sender's authority. Before an invitation is accepted, and before a scheduled invitation is emailed at activation, the system MUST check that its sender could still send it, as a Company Administrator or as an effective Community Manager of that community. If not, the invitation is revoked and logged, and its link is refused with `INVITATION_INVALID`. When a user's manager membership ends or is downgraded, or their Company Administrator grant is revoked, every scheduled or pending invitation they can no longer send MUST be revoked in the same action, each logged.
  - **Duplicates.** Inviting an email that already has a scheduled or pending invitation for the same role in the community, or for the same property, MUST be refused with `INVITATION_PENDING`, and Resend is offered instead. Inviting an email that already holds that role there MUST be refused with `ALREADY_MEMBER`. If, at acceptance, the account already holds the role, or is already linked to the property, the invitation is marked accepted and nothing is duplicated.
  - **Terms.** A membership invitation's membership starts on the acceptance date (UTC) and carries the invitation's term end date, if any. If that end date has passed by acceptance, the link is refused with `INVITATION_INVALID`. FR-020 is applied at acceptance too.
  - **Not a manager yet.** A pending invitation does not count as a manager (FR-010, FR-020).
- **FR-022a**: An invitee without an account MUST be able to accept by opening the invitation link and entering a name and a password. No claim code is needed.
  - Holding the unexpired, single-use link that was emailed to that address is the proof that the invitee controls the email. The account is created for the invited email with the email confirmed.
  - A membership or Company Administrator invitation links no property.
  - A resident invitation links only the invited property, even when that property already has a resident. The claim-code "property already claimed" refusal does not apply.
  - Invitation-state failures use FR-022's codes. Account-creation failures stay generic (spec 017 FR-A5) and tell the person to sign in and accept instead.
  - The acceptance page shows the community name and, for a resident invitation, the property address, only to the holder of a valid link. It shows nothing for an invalid link. `[no WFb]`
- **FR-022b**: The Managers and memberships section MUST list, next to active and ended memberships, every scheduled, pending, expired and revoked invitation for the Community Manager, Board Member and Accountant roles. Each shows email, role, sent by, sent at and expires at, with Resend and Revoke for anyone who could send it. `[no WFb]`
- **FR-023**: Every appointment, change, end, invitation, resend, revoke and acceptance MUST be logged as a sensitive event per change. The event carries actor, community, the target user ID (or the invitation ID when the invitee has no account) and UTC time. Failed acceptance attempts MUST also be logged, with the refusal reason, the community, the signed-in user if any, and UTC time, matching spec 017's auditing of failed claim attempts.

### Residents

- **FR-024**: A Community Manager of the community, or a Company Administrator, MUST be able to invite a person by email to be a resident of a property in that community. The invitation follows FR-022 and FR-022a. Accepting it links the accepting account to that property as a resident, the same link a claim code creates.
  - An email already linked to that property MUST be refused with `ALREADY_RESIDENT`.
  - A property outside the community MUST be refused with 403 `FORBIDDEN`, without revealing whether it exists.

  `[no WFb]`
- **FR-024a**: If the invited email already has an account, the invitation asks them to sign in and accept. Nothing is linked until they accept.
- **FR-024b**: A resident invitation made while the community is Onboarding MUST be saved as **scheduled** and not emailed. When the community is marked Active, every scheduled invitation whose sender still has authority (FR-022) MUST be emailed, and its 72 hours start then. If the community is archived instead, scheduled invitations are revoked without being sent.
- **FR-024c**: A Community Manager of the community, or a Company Administrator, MUST be able to remove a resident from a property. On removal:
  - the resident loses access to that property on their very next request (FR-024g);
  - their account, other homes and community memberships are unchanged (spec 025 FR-011); if it was their only home, they can still sign in (FR-036a);
  - if the property's active autopay was enrolled by the removed resident, or has no recorded enroller (enrolled before this spec), it is cancelled in the same action with the reason "resident removed", and logged. Autopay enrolled by another resident is unchanged. A charge already submitted settles normally;
  - the removed resident is emailed that they were removed from the property, and, where autopay was cancelled, that it ended. Every other resident of the property is emailed that its autopay ended;
  - the property's owner of record and balance are unchanged.
- **FR-024d**: The **Residents** area MUST list every property in the community with these details:
  - its address;
  - its residents (name, email, resident since);
  - its scheduled, pending, expired and revoked invitations, with who sent them.

  It MUST support:
  - search by address, resident name or email (case-insensitive partial match);
  - filters "No resident", "Invitation pending" (including scheduled ones, labelled "Scheduled") and "Invitation expired";
  - pagination: 25 properties by default, at most 100.

  A community with no properties MUST show that properties have not been loaded yet and offer no invite action. Each load and search MUST log the spec 025 FR-017 association-data access event. `[no WFb]`
- **FR-024e**: Every resident invitation, resend, revoke, acceptance (successful or not) and removal MUST be logged as a sensitive event per change. The event carries actor, community, property ID, the target user ID (or invitation ID), the change and UTC time.
- **FR-024f**: Restricting resident rights, such as suspending a resident's poll voting, is NOT built (Clarifications 2026-10-10). Claim codes (spec 016/017) keep working unchanged:
  - A code is redeemed only while registering a new account, and only for a property with no linked resident.
  - Once a property has a resident, by code or by invitation, an outstanding claim code for it is refused.
  - Further residents of that property, and anyone who already has an account, join by invitation.
- **FR-024g**: Every resident endpoint, in any spec, MUST check on each request, from stored data, that the caller still has a resident link to the selected property, and apply FR-012a. Otherwise it MUST refuse with `PROPERTY_ACCESS_DENIED` (the code switch-property already uses) and change nothing. The token's property claim only says which home is selected; it never grants access. On that refusal the app moves the user to another linked home, or to their FR-036a landing.

### Company default settings

- **FR-025**: The company MUST have default settings, editable by a Company Administrator only, covering at minimum the spec 027 ARC rule settings:
  - review period in days (1–365);
  - lapse rule;
  - decision rule;
  - reminder days (0–30);
  - time zone;
  - formal disapproval statement (required, up to 1,000 characters, as in spec 027).

  Their initial values MUST be spec 027's defaults: 30 days, flag overdue only, majority of members, 7 days, America/New_York, and 027's standard statement. `[no WFb]`
- **FR-026**: Changing a default MUST affect only communities created afterwards. It MUST NOT change any existing community's settings, nor the rules already copied onto applications already received (spec 027 FR-030). A community whose ARC settings were never saved counts as having spec 027's built-in defaults, and those do not change when a company default changes. The product MUST NOT offer to push a changed default to existing communities.
- **FR-027**: Community Managers MUST be able to read the company defaults, read-only, through a "Company defaults" link in their community's Architectural review section. Board Members, Accountants and Residents MUST NOT.
- **FR-028**: The company defaults page MUST show exactly one section, Architectural review, until the Notification Settings spec adds its own.

### Community settings page

- **FR-029**: Each community MUST have one **Community settings** page with the sections Profile, Managers and memberships, Architectural review and Notifications. Notifications is not shown until the Notification Settings spec lands.
  - The page is addressed by community ID, not by the board-mode active community, so it works for Onboarding and Archived communities that appear in no switcher.
  - Company Administrators reach it from the portfolio. Community Managers reach it from board mode in the active community.
  - A Community Manager of an Onboarding community sees on the Profile section that a Company Administrator must mark it Active, and that resident invitations are sent then.

  `[no WFb]`
- **FR-030**: The spec 027 ARC settings MUST be shown as the page's Architectural review section, not rebuilt.
  - Who may edit them widens from "that community's Community Manager" (spec 027 FR-030) to "a Company Administrator or that community's Community Manager". Board Members keep read access.
  - A Company Administrator reaches these settings through a check from FR-004 that never unlocks recording or resending outcomes, or reading applications and attachments (FR-003).
  - Recording ARC outcomes stays Community Manager only.
  - The old ARC settings location MUST lead to this section.
- **FR-031**: A Company Administrator or the community's Community Manager MUST be able to reset the community's ARC rule settings to the current company defaults, after a confirmation. Applications already received keep their copied rules, and the community's ARC application numbering is never changed.
- **FR-032**: A Board Member, Accountant or Resident who is not also the community's manager or a Company Administrator MUST be refused the page with 403 `FORBIDDEN`. The app then takes them to their landing page (spec 025 FR-028).
- **FR-033**: Every settings change, including a reset, MUST be logged as a sensitive event with the old and new values.

### Portfolio and company area

- **FR-034**: Company Administrators MUST have a portfolio listing every community of the company with:
  - community name, legal name, status and parent;
  - open-ended managers and pending manager invitations;
  - a "No manager" flag when there is no open-ended manager;
  - a flag when its ARC settings differ from the company defaults.

  It MUST filter by status, search by community name or legal name (case-insensitive partial match), and paginate with a default of 25 and a maximum of 100. `[no WFb]`
- **FR-035**: The portfolio MUST NOT be offered to anyone but a Company Administrator, and a direct request from anyone else MUST be refused with 403 `FORBIDDEN`.
- **FR-036**: A Company Administrator MUST be able to open the **company area** from the top-bar account controls (spec 025 FR-019) without holding any community membership. The company area holds the portfolio, Add community, Company defaults, Company administrators, and each community's Community settings page. Holding the grant alone does not show the spec 025 board-mode control and unlocks no other board page (FR-003). A Community Manager reaches Add community from board mode. `[no WFb]`
- **FR-036a**: Sign-in, token refresh, mode switch and the current-user endpoint MUST succeed for a user with no linked home. Their tokens carry no property, and every resident endpoint refuses them (FR-024g). After sign-in, a user with no usable home lands, in this order:
  1. in board mode, if they hold an effective non-resident membership in an Onboarding or Active community;
  2. otherwise in the company area, if they are a Company Administrator;
  3. otherwise on a "Your community isn't live yet" page, if their only homes are in Onboarding communities;
  4. otherwise on a "No home linked to this account" page.

  Removing a user's last home MUST NOT stop them signing in. `[no WFb]`
- **FR-036b**: Spec 025's board eligibility (the board-mode control, "My Communities", landing and route guard) MUST count memberships in Onboarding communities as well as Active ones. Only Archived communities are excluded.

### Company Administrators

- **FR-037**: A Company Administrator MUST be able to grant the role by email, always as an invitation (FR-022), and revoke it from any Company Administrator. Revoking the last one, including oneself, MUST be refused with `LAST_COMPANY_ADMIN`, also under concurrent requests. Changes take effect on the user's next request. `[no WFb]`
- **FR-038**: The first Company Administrator MUST be created by a one-time setup step that the deployer runs from the command line. It never runs through an HTTP endpoint, and never implicitly from seed data outside Development, Dev and PR environments.
  - The step targets an email. If that email has an account, it is granted the role; otherwise a Company Administrator invitation is emailed.
  - If a Company Administrator already exists, the step exits successfully, prints "A Company Administrator already exists; nothing changed", and writes nothing.
  - A separate **recovery mode** of the same command MAY grant the role (or invite) when administrators already exist. It is for when every Company Administrator is unavailable or compromised. It logs a sensitive event and emails every current Company Administrator.

  This is the only part of this spec an application operator touches.
- **FR-039**: Every grant, revoke and run of the setup step MUST be logged as a sensitive event with actor (or "setup"), target user ID or invitation ID, the change and UTC time.

### Cross-cutting

- **FR-040**: All changes in this spec MUST be logged as Serilog structured sensitive events. There is no new audit table, as in spec 025.
  - **Content.** Events identify people, properties and invitations by ID only. They MUST NOT contain email addresses, people's names, street addresses or invitation tokens. Invitation records, with their invited email, are kept, so an event's invitation ID can always be resolved. Community profile and settings values are not personal data and are logged as old and new values.
  - **One event per change.** Every user action logs an event for the action itself, and every change it causes logs its own event naming the same actor. Caused changes include each invitation revoked by an archive or by a loss of authority, each scheduled invitation emailed at activation, each autopay cancelled, and the end and the appointment that make up a replace. No change is logged twice.
- **FR-041**: Rate limits:
  - **(a) Writes.** Every write endpoint this spec adds or widens uses the per-user `board-writes` limit (spec 027). This includes the spec 025 membership create and update endpoints, community create, edit, status and parent changes, and invitation send, resend, revoke and resident removal.
  - **(b) Invitation links.** The invitation lookup and acceptance endpoints, which can be reached before sign-in, use the per-client `auth` limit, as register and verify-email do (spec 017 FR-A2).
  - **(c) Invitation emails.** First sends and resends are also capped per recipient email: at most a configured number in a rolling 24 hours, plus a short resend cooldown per invitation. Both values are configured and checked at startup. The activation send of scheduled invitations (FR-024b) is exempt.

  Refusals use the existing 429 response.
- **FR-042**: Every user-visible refusal MUST say why in plain language, for example "This community can't be archived until ARC-1041 and ARC-1042 are closed". Denials that must not reveal that a community, property or account exists (FR-004, FR-004a, FR-007, FR-018) are the exception.

### Key Entities

- **Management Company** *(new)*: The HOA management company running this deployment. Has a name. Owns its communities, its Company Administrators and its default settings. One per deployment for now.
- **Company Administrator grant** *(new)*: Links a user to the management company as a Company Administrator, with who granted it and when, and, once revoked, who revoked it and when.
- **Community** *(modified)*: Belongs to a management company. Status becomes Onboarding, Active or Archived, replacing Active and Inactive. It records when and by whom it was activated, archived or restored. Whether it was ever activated decides where a restore returns it (FR-010).
- **Company Default Settings** *(new)*: One set per management company. Holds the ARC rule defaults (FR-025). Reserves a place for notification defaults.
- **Community ARC Settings** *(spec 027, unchanged shape)*: Now copied from Company Default Settings when a community is created, and resettable to them. The reset never touches the application-number counter.
- **Invitation** *(new)*: An offer sent by email to someone who may not have an account yet. It offers a community membership (role and optional term end date), residency of one property, or the Company Administrator role. It holds:
  - the invited email, the community (and the property, if any) and the role;
  - who sent it (updated on resend);
  - when it was emailed and when it expires (72 hours later);
  - its state: scheduled, pending, accepted, expired or revoked, with the revoke reason;
  - a hash of its link, the only form in which the link is stored.

  Records are kept, never deleted.
- **Resident link** *(existing `UserProperty`, modified behavior)*: Links a resident's account to a property. Can now also be created by accepting a resident invitation, and removed by a manager or Company Administrator. A property may have several.
- **Community Membership** *(spec 025, unchanged shape)*: Gains no fields. Confers nothing while its community is Archived. A former role can be given again (FR-019).
- **Recurring Payment (autopay)** *(spec 006, modified shape)*: Gains the user who enrolled it, set on every enrollment or re-enrollment, and a cancellation reason ("community archived" or "resident removed").

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: Communities, invitations, residents and community settings are scoped to one community, which belongs to one management company. Company Default Settings and Company Administrator grants are scoped to the management company. Intentional cross-community surfaces:
  - the portfolio (Company Administrators only; that company's communities; summary fields);
  - a Community Manager's parent picker (only communities they manage);
  - spec 025's "My Communities".

  Cross-community access stays denied by default (025 FR-013). Community scope never follows the parent link.
- **Authorization**: Every action is checked server-side on every request, through the 025 resolver extended per FR-004 and in the FR-004a order. The checks use the persisted Company Administrator grant, community memberships and resident links. Resident endpoints in every spec re-check the resident link and community status on each request (FR-024g). The client's mode, route or token claims are never an authorization input. Frontend checks only hide controls a user can't use. Constitution §3 ("an HOA MUST have at least one ... management administrator at all times") is met in three ways:
  - FR-020's open-ended-manager rule;
  - FR-010's activation and restore rules;
  - FR-008: Company Administrators act for a community with no manager, and FR-037 keeps at least one of them.
- **Ownership and moderation**: This spec adds no user-generated content beyond community profile text, which is entered by staff and rendered as text, never HTML.
- **API contract**: Uses the existing response and error shapes. Collections take `limit`/`offset` (default 25, max 100; larger values are clamped, as today). All timestamps are UTC; formation and management start dates are plain dates. IDs are GUIDs; the community name is a display handle.
  - **New error codes**: `COMMUNITY_NAME_TAKEN`, `NO_ACTIVE_MANAGER`, `OPEN_WORK`, `COMMUNITY_ARCHIVED`, `COMMUNITY_NOT_ACTIVE`, `PARENT_CYCLE`, `PARENT_ARCHIVED`, `INVITATION_EXPIRED`, `INVITATION_INVALID`, `INVITATION_EMAIL_MISMATCH`, `INVITATION_PENDING`, `ALREADY_MEMBER`, `ALREADY_RESIDENT`, `SELF_GRANT_NOT_ALLOWED`, `LAST_COMPANY_ADMIN`.
  - **Reused codes**: `LAST_MANAGER` (422), `PROPERTY_ACCESS_DENIED` and spec 025's 403 `FORBIDDEN`.
  - Each new code's HTTP status is set in this feature's `contracts/` during planning. Out-of-scope denials reuse spec 025's 403 `FORBIDDEN` body.
  - The ARC settings request and response shapes are unchanged; only who may call them widens.
- **API implementation and docs**: New endpoints are FastEndpoints. Swagger stays available only in Development/Dev and is disabled in Production.
- **Database/runtime**: Forward-only migrations (spec 025 FR-005), applied idempotently at Cloud Run startup. They:
  - add the management company, Company Administrator grants, company default settings and invitations;
  - add the enroller and cancellation reason to recurring payments;
  - move community status to Onboarding, Active and Archived, with `Inactive` becoming Archived;
  - for each community migrated to Archived, cancel any active autopay without sending email;
  - backfill one management company and link every existing community to it; existing Active communities stay Active and count as having been activated;
  - replace the global, case-sensitive community-name uniqueness with per-company uniqueness that ignores case and surrounding spaces. Before switching, the migration MUST detect existing names that would collide under the new rule and fail with a clear message.

  Short-lived DbContexts, within Neon's low connection limit. The last-manager, last-administrator and invitation-state rules must hold under concurrent requests.
- **File storage**: None. This spec stores no files.
- **Security and abuse controls**:
  - **Rate limits.** Per FR-041: per-user write limits, per-client limits on the invitation endpoints that can be reached before sign-in, and per-recipient invitation-email caps.
  - **Invitation links.** They are single-use, expire after 72 hours and are bound to the invited email, which is compared trimmed and case-insensitive. Their tokens are unguessable (at least 128 bits from a cryptographic random source) and stored only as hashes.
  - **Keeping the token out of logs.** The token is never placed in a URL path or query string that ends up in logs or telemetry. The acceptance page sends no referrer and is excluded from client error and breadcrumb URL capture. A wrong-account refusal never shows the invited email.
  - **No enumeration.** Appointing by email never reveals whether the email has an account (FR-018), and community-name conflicts never reveal another community to a non-administrator (FR-007).
  - **Least privilege.** Nobody grants anything to themselves (FR-003a), and invitations die with their sender's authority (FR-022). Company Administrators get no board data by default (FR-003).
  - **Logging and denials.** Every membership, role, residency, status, settings and administrator change is a sensitive event, as are failed invitation acceptances (constitution §7). Denials fail closed without revealing whether a community, property or account exists.
  - **Owner of record.** The owner of record is not told when a resident is linked by invitation; that can be added later if wanted.
- **Observability**: Errors go to Sentry with environment and release tags, plus community ID and capability as tags where relevant. No emails, names, street addresses or invitation tokens go to telemetry (FR-040). Trace context flows from the frontend to the backend.
- **Accessibility**: These surfaces are fully keyboard operable, with labels, visible focus and validation messages tied to their fields:
  - the add-community form;
  - every Community settings section, including the Residents area and the invitation lists;
  - the portfolio;
  - the company defaults page;
  - the Company administrators page;
  - the invitation acceptance and sign-up pages;
  - the "No home linked" and "isn't live yet" pages.

  Status and the "No manager", "differs from defaults", "Scheduled" and "No longer managed" labels are conveyed by text, not color alone. Everything meets WCAG 2.1 AA.
- **Quality gates**: 95% coverage on new backend and frontend files. Sonar passes. xUnit integration tests run on Testcontainers PostgreSQL, with isolated per-test communities and companies, so they are safe in parallel and after earlier runs. `[Theory]` data covers:
  - role × action: Company Administrator, Community Manager of this community, Community Manager of another community, Board Member, Accountant, Resident, and a user with no role;
  - community status (Onboarding, Active, Archived) × every resident read and write in FR-012a;
  - invitation state × action (accept, resend, revoke).

  The required Serilog sensitive events are asserted, including that they contain no email or name. Repowise docs are refreshed for the PR. The PR stays a focused vertical slice per story.
- **Frontend testing**:
  - **Jasmine/Karma**: permission-driven section rendering, portfolio flags, and the sign-in landing order.
  - **Angular Testing Library**: the add-community form, each Community settings section (including the Residents area, the invitation lists and the restore dialog), portfolio filter and search, the company defaults form (editable and read-only), granting and revoking Company Administrators, invitation acceptance and sign-up, and the "No home linked" page.
  - **Playwright**: the journeys "add community → invite manager → accept → activate" and "invite resident → sign up from link → see home", and refusal of a Board Member on the settings route.
  - **Cypress E2E**: sign in as a Company Administrator with no home → portfolio → community settings.
  - **Storybook visual regression**: every new page and section, once designs exist.
- **Executable & living spec**: Every acceptance scenario, Independent Test, functional requirement and edge case above maps to an automated test that runs on demand and passes before merge (constitution §11). This `spec.md` and `tasks.md` are updated before the implementation PR. The implementation PR MUST also reconcile the older specs this one changes:
  - **Spec 025.**
    - `Community` status (FR-001) and its "inactive/offboarded" edge case become Onboarding, Active and Archived.
    - Board eligibility (FR-020, FR-023, FR-025, FR-026) counts Onboarding communities (FR-036b).
    - FR-042 gains co-manager removal by Community Managers, management by Company Administrators, invitations, and the open-ended-manager rule.
    - Its Assumption that authentication is "reused unchanged" and its tenant-boundary note.
  - **Spec 027 FR-030** widens to "Company Administrator or that community's Community Manager".
  - **Spec 016 and spec 017 sub-spec A** (update both `specs/016-security-hardening/spec-identity-access.md` and `specs/017-security-hardening-subspec-a/spec.md`). These are superseded by resident invitations, invitation sign-up and sign-in without a home (FR-022a, FR-024, FR-036a):
    - the umbrella clarification "one-time claim code only ... no administrator-approval path";
    - FR-A1's clarification;
    - the Edge Case rejecting an administrator path;
    - the Assumption that claim-code delivery is the only claim mechanism.

    FR-A2, FR-A3 and FR-A5 still apply and are reused for acceptance. FR-A10's accepted 15-minute token window no longer covers property access after a resident is removed or a community changes status (FR-024g).
  - **Spec 006.** Recurring payments gain the enroller and cancellation reason, and are cancelled on archive and on removal of the enrolling resident.
- **Spec independence & parallelism**: Hard dependencies on spec 025 (communities, memberships, resolver, board shell), spec 027 (ARC settings) and spec 006 (autopay), all merged, so this spec is individually completable now. The Notification Settings spec is optional: its sections stay hidden until it lands. The in-progress Resident Architectural Application Submission spec (`029-resident-arc-requests`) does not block this one; its writes follow the shared status rule (FR-012a), so the two can be built in parallel.

  The **MVP** is US1–US3, US8 and US9, plus a foundation slice they all need:
  - (a) the management company record and its backfill (FR-001);
  - (b) the Company Administrator role, the resolver checks (FR-002–FR-004a) and the setup step (FR-038, FR-039, US7 #6–#7);
  - (c) Company Default Settings created with spec 027's values and copied at community creation (FR-009, FR-025 initial values), with no page yet;
  - (d) the company area with a portfolio that shows each community's name and status, with a link to its settings (FR-034 list only, FR-035, FR-036);
  - (e) the Community settings page with its Profile and Managers and memberships sections (FR-029, FR-032).

  Once the MVP lands, these can be built in parallel: US4 (the defaults page), US5's Architectural review section, reset and redirect (FR-030, FR-031), US6's flags, filter and search, and US7's grant and revoke (FR-037).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Company Administrator can add a community, invite its first manager and, once the manager has accepted, take it live in under 5 minutes of their own time, with no engineering, database or seed-script involvement.
- **SC-002**: 100% of this spec's actions refuse every role the permissions table denies, verified by an automated test per role and action.
- **SC-003**: No community that has had a manager is ever left without an open-ended manager, now or on any later date, through any surface, including simultaneous requests.
- **SC-004**: 100% of new communities start with settings equal to the company defaults in effect when they are created, and 0 existing communities change when a default changes.
- **SC-005**: After a community is archived, 0 writes other than Restore and ending memberships are accepted in it, and 0 autopay charges start for its properties, across the automated suite.
- **SC-006**: 100% of invitation links stop working 72 hours after sending, after a resend, after revocation, after use, and when their sender loses authority.
- **SC-007**: Every user action in this spec logs one sensitive event for itself and one for each change it causes, each with all required fields and no personal data; no change is logged twice.
- **SC-008**: The portfolio shows its first page within 2 seconds for a company with 200 communities.
- **SC-009**: A Company Administrator can find every community without an open-ended manager in one step (the "No manager" flag on the portfolio).
- **SC-010**: Once a community's properties are loaded, a manager can invite a resident to a property in under 1 minute, and the resident can go from the invitation email to seeing their home in under 5 minutes.
- **SC-011**: 100% of removed residents are refused that property on their very next request, and 0 autopay charges start afterwards on any schedule they enrolled.
- **SC-012**: 0 resident invitations are emailed while their community is Onboarding, and 100% of scheduled invitations whose sender still has authority are emailed when it is marked Active.
- **SC-013**: 100% of users with no linked home who hold a membership or the Company Administrator role can sign in and land where their work is.

## Assumptions

- **One company**: There is one management company per deployment. The migration creates it and links every existing community to it. Multi-company tenancy is out of scope, but nothing in this spec prevents it.
- **Archived history**: "Readable for history and audit" means the archived community's Community settings sections, read-only, plus residents' own payment history (FR-012a). Other features' records (applications, payments, ledger) are kept unchanged but are not shown through new staff surfaces here. A reporting or audit spec can add that later.
- **Invitation resend**: Not answered by the owner. This spec lets anyone who could send the invitation resend or revoke it.
- **Invitations survive only while their sender has authority** (FR-022). The alternative, "invitations belong to the community and outlive their sender", was rejected as less safe. The owner may choose it instead.
- **Properties for a new community**: No property import exists today. A community added through this spec has no properties until an engineer-run seed loads them, or until a later Properties spec adds an import. Its Residents area says so (FR-024d). This is the one remaining engineer step in onboarding an HOA.
- **"Resident"** means any person linked to a property, as today. This spec does not tell owners and tenants apart.
- **Emails**: these use the existing transactional email and are plain text until Claude Design delivers templates (briefs in `design/`):
  - manager, board, accountant, resident and Company Administrator invitations;
  - "community added, waiting to go live" to Company Administrators;
  - "no longer managed" to residents on archive;
  - "removed from a home" and "autopay ended" on resident removal;
  - recovery-mode notice to Company Administrators.
- **Desktop-first**, like the rest of the board side (spec 025). Mobile layout is out of scope for staff pages. The invitation acceptance and sign-up pages must work on a phone, because invitees open them from email.
- **Demo data**: In Development, Dev and PR environments only, the dev seed gets one Company Administrator (via the setup step) and one Onboarding community, so the flow can be tried end to end. Seeded invitations are sent only to the email simulator or allow-listed addresses.

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
- Adding, editing, importing or removing properties (a later Properties spec).
- Telling owners and tenants apart.
- Notifying the owner of record when a resident is linked by invitation.

## Dependencies

- **Spec 025** (merged): communities, memberships, scope resolver, the membership create, update and list endpoints, and the board shell. Its board-eligibility rules are extended to Onboarding communities (FR-036b).
- **Spec 027** (merged): ARC settings, which this spec moves under Community settings and copies from company defaults.
- **Spec 006** (merged): recurring payments (autopay). They gain an enroller and a cancellation reason, and are cancelled on archive and on removal of the enrolling resident.
- **Specs 016 and 017** (merged): registration, sign-in, email verification and claim codes.
  - Claim codes keep working as they do today (FR-024f).
  - Invitation sign-up (FR-022a) and sign-in without a home (FR-036a) are added beside the claim-code path.
  - The "no staff path to a property" rule is superseded (see the reconcile list).
- **Notification Settings spec** (optional): its sections stay hidden until it lands.
