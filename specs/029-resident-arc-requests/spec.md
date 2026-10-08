# Feature Specification: Resident Architecture Request

**Feature Branch**: `029-resident-arc-requests`
**Created**: 2026-10-08
**Status**: Draft
**Input**: GitHub issue #210 — "(Resident) Architecture Request". Resident Architectural Application Submission: homeowners file and track architectural (ARC) requests in the app. Sibling of `027-board-arc-review` (board review & voting, which reads the applications this spec creates; split from 027's clarifications on 2026-10-07). Builds on `025-board-overall-design` (Community entity, CommunityMembership/roles, community-scope resolver, `IDocumentStorage` pre-signed URLs).

## Alignment with 027 (shared data model)

`027-board-arc-review` (PR #209) is a complete spec + plan + implementation that **lands first** and **introduces the shared entities** (`ArchitecturalApplication`, `ArchitecturalAttachment`, `ArchitecturalVote`, `ArchitecturalInfoRequest`, `CommunityArcSettings`, the `Arc*` enums, and `OutboxMessage` changes). 027 is built and tested against *seeded* applications and explicitly defers the resident intake, status pages, info-request reply, and the owner-side "Revise and resubmit" action to this spec. 027 merged on `main` (#209). **029 extends the 027 model additively and never loosens it**: drafts live in a separate draft store (not in `ArchitecturalApplication`, whose number and dates are always set), and submitting a draft goes through 027's application factory. Concretely, 029 adds: the draft store, the `Withdrawn` outcome, the resident-authored fields (planned start/completion dates, contractor, acknowledgement) as optional fields on the application, the info-request **reply** (body + attachments, setting `RespondedAt`), the owner-side revise-and-resubmit action over 027's revision factory, and the submission-confirmation email `Kind`. Canonical names, numbering, due-date snapshotting, 15-minute link expiry, and pagination all follow 027.

The resident-facing status is a projection over 027's `ArcApplicationStatus` plus the two new states:

| Resident-facing status | Underlying 027 state |
|---|---|
| Draft | **New (029)** — a draft record, not yet an application; invisible to the board |
| Submitted / Under review | `Open` with no outstanding info request, or `DecisionReached` (the board has decided but the manager hasn't recorded the outcome yet) |
| More info requested | `Open` with an `ArchitecturalInfoRequest` whose `RespondedAt` is null |
| Approved | `Closed`, `DecisionOutcome = Approved` |
| Denied | `Closed`, `DecisionOutcome = Denied` (owner wording "Revisions requested" or "Denied" per 027 FR-025) |
| Withdrawn | **New (029)** — `Closed`, resident-initiated, outcome "withdrawn"; hidden from the board's default views, shown only via a board "show withdrawn" filter |

## Clarifications

### Session 2026-10-08

- Q: Attachment size & count limits? → A: 50 MB per file, 20 files per application, 250 MB total per application — **configured at the environment level** (application/infra configuration, not per-community), so each deployment can tune them.
- Q: Does the owner-side "Revise and resubmit" action (027 FR-034/035) belong in this spec? → A: Yes — it is in scope for this spec/PR (User Story 6, FR-026), built over 027's revision factory.
- Q: How does a withdrawn request appear to the board? → A: Hidden from the board's default views (Open and the default Closed list), but a board **"show withdrawn" filter/toggle** can reveal them — so the record is kept but out of the way. (Requires a small 027-side filter extension.)

> UI requirements below are marked **[no WFb]** because the Claude Design mockups for the resident submission flow are not yet present under `wireframes/`. When those mockups land, replace `[no WFb]` with the specific wireframe citation (`WFb ...`). The issue attaches two standalone HTML mockups: the **ARC Decision Emails** mockup belongs to the approved/denied outcome emails owned by 027 (out of scope here), and the **NekoHOA Wireframes** mockup is the general app wireframe, not the resident-ARC-specific flow.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - File an architectural request (Priority: P1)

A homeowner wants to put up a fence (or install solar, repaint, add a shed, etc.). They open "Architectural requests" for a property they own, start a new request, pick the project type, describe the work, give planned start and completion dates, optionally name a contractor, attach supporting documents (plans, elevations, plat surveys, photos), and acknowledge that work may not begin until the request is approved. They can save the request as a draft and come back to it, edit it, or delete it. When ready, they submit; the request is assigned a community-unique ID (`ARC-<number>`), a received date, and a due date, and they receive a confirmation email.

**Why this priority**: This is the core of the feature — without the ability to create and submit a request, nothing else (tracking, board review in 027) can happen. It is the MVP.

**Independent Test**: A resident who owns a property can create a draft, edit it, submit it, observe the assigned `ARC-<number>` ID / received date / due date, and receive a submission-confirmation email — all without any board-side (027) voting functionality present.

**Acceptance Scenarios**:

1. **Given** a resident who owns a property, **When** they create a request with project type Fence, a title, a description, planned start and completion dates, the required acknowledgement checked, and submit it, **Then** the request is stored as Submitted (`Open`), is assigned an ID of the form `ARC-<number>` that is unique within the property's community (the number drawn atomically from the community's ARC counter, starting at 1001), records the received date as the submission date, and records the due date as received date + the community review period in effect at submission (027 FR-029; default 30 days).
2. **Given** a resident filling out a request, **When** they save it without submitting, **Then** the request is stored with status Draft and does not appear in the board's Open list.
3. **Given** a resident with a draft request, **When** they edit the draft's fields and save, **Then** the updated values are persisted and status remains Draft.
4. **Given** a resident with a draft request, **When** they delete the draft, **Then** the request (and any attachments it held) is removed and no longer appears in their list.
5. **Given** a resident filling out a request, **When** they attempt to submit without checking the "work may not begin until approved" acknowledgement, **Then** submission is refused and the request is not assigned an ID.
6. **Given** a resident filling out a request, **When** they enter a planned completion date earlier than the planned start date, **Then** submission is refused with a validation error.
7. **Given** a request was just submitted, **When** the submission completes, **Then** the submitting resident is sent a plain-template submission-confirmation email identifying the request by its `ARC-<number>` ID, queued through the existing transactional email (OutboxMessage) pipeline.
8. **Given** a request was just submitted, **When** the board's Open list (027) is queried, **Then** the submitted request is present in it (this spec writes the data 027 reads).

---

### User Story 2 - Track my architectural requests (Priority: P2)

A homeowner wants to know where each of their requests stands. They open "My architectural requests" and see a list of their requests with current status. They open one and see its full detail: the fields they submitted, a timeline of what has happened, the attachments (opened through short-lived links), and — once decided — the outcome and, if denied, the owner-facing wording ("Revisions requested" or "Denied"), the manager's reason, the community's formal disapproval statement, and (if approved with conditions) the conditions. They never see how individual board members voted, who the voters were, or internal board comments.

**Why this priority**: Visibility into status is the second-most valuable capability and is what makes the feature usable day-to-day, but it depends on P1 having created requests.

**Independent Test**: A resident with at least one submitted request can open the list, see its status, open the detail page, open an attachment via a time-limited link, and confirm no board vote/voter/comment data is present — verifiable without the 027 voting UI.

**Acceptance Scenarios**:

1. **Given** a resident with requests in various states, **When** they open "My architectural requests", **Then** they see each request with a status of Draft, Submitted/Under review, More info requested, Approved, Denied, or Withdrawn (the projection above).
2. **Given** a resident viewing a request's detail page, **When** the page loads, **Then** it shows the submitted fields, a chronological timeline of status changes, and the attachments.
3. **Given** a request has attachments, **When** the resident opens an attachment, **Then** it is served through a pre-signed link that expires within 15 minutes and never through a durable public object URL.
4. **Given** a request that was denied, **When** the resident views its detail page, **Then** they see the Denied outcome with its owner-facing wording, the manager's reason, and the community's formal disapproval statement (and, for an approval with conditions, the conditions) — and a "Revise and resubmit" action.
5. **Given** a request that has board votes, voter identities, and board comments recorded (via 027), **When** the resident views the request, **Then** none of the votes, voter identities, or board comments are shown or returned to the resident.

---

### User Story 3 - Respond to a request for more information (Priority: P3)

While a request is under review, a board member asks a question via "Request info" (027). The homeowner sees the question on the request and as an alert on their dashboard. They reply, optionally attaching more documents. Their reply is visible to the board and the manager, and sending it clears the "more info requested" marker so review can continue.

**Why this priority**: Important for keeping a request moving, but only relevant once requests are submitted and a board member has asked something; a request can complete without it.

**Independent Test**: Given a request flagged "more info requested" (an `ArchitecturalInfoRequest` with `RespondedAt` null), the owning resident can see the question (on the request and as a dashboard alert), post a reply with an attachment, and observe that `RespondedAt` is set (the info-requested marker is cleared) and the reply is recorded for the board/manager to read.

**Acceptance Scenarios**:

1. **Given** a board member has used "Request info" on a resident's request, **When** the resident opens the app, **Then** they see the question on the request and a dashboard alert indicating more information is requested.
2. **Given** a request flagged "more info requested", **When** the resident submits a reply (with or without additional attachments), **Then** the reply is stored, is visible to the board and manager, and the "more info requested" marker is cleared (`RespondedAt` set).
3. **Given** a request is in the "more info requested" state, **When** the resident has not yet replied, **Then** the review due date continues to count down unchanged (the review clock is not paused by an info request, per 027 FR-021).

---

### User Story 4 - Withdraw a request (Priority: P3)

A homeowner changes their mind before a decision is made and withdraws the request. It moves to Closed with outcome "withdrawn" and disappears from the board's open list.

**Why this priority**: A useful control for residents, but affects a minority of requests and is not needed for the core submit/track loop.

**Independent Test**: A resident with an undecided submitted request can withdraw it, see it become Closed/withdrawn in their list, and confirm it is no longer in the board's open list; attempting to withdraw a decided request is refused.

**Acceptance Scenarios**:

1. **Given** a resident with a submitted, undecided (`Open`, no decision reached) request, **When** they withdraw it, **Then** the request moves to `Closed` with outcome "withdrawn", is removed from the board's Open list, and is excluded from the board's default Closed view (visible to the board only when they enable a "show withdrawn" filter).
2. **Given** a request that has already reached a decision or been closed (Approved, Denied, deemed-decided, or already Withdrawn), **When** the resident attempts to withdraw it, **Then** the action is refused and the request's state is unchanged.

---

### User Story 5 - Attachments are validated and private (Priority: P1)

Whenever a homeowner attaches files to a request (at creation or when replying to an info request), only genuine PDF/JPG/PNG/HEIC files within the size and count limits are accepted, each file is checked by its actual content rather than just its filename, and every file is stored privately and only ever reachable through short-lived links.

**Why this priority**: Attachments are integral to the submission (plans, elevations, surveys) and are a security-sensitive surface; getting validation and privacy right is part of the MVP, not an add-on.

**Independent Test**: Uploading a valid PDF succeeds; uploading a file whose content is not an allowed type (e.g. an executable renamed `.pdf`) is rejected; uploading a file over the per-file limit, or exceeding the per-application total-size or count limit, is rejected; a stored attachment is only retrievable via a time-limited link.

**Acceptance Scenarios**:

1. **Given** a resident adding an attachment, **When** the file is a valid PDF, JPG, PNG, or HEIC within limits, **Then** it is accepted, stored in private object storage under the `arc/{communityId}/{applicationNumber}/{guid}` key convention, and its metadata is recorded.
2. **Given** a resident adding an attachment, **When** the file's actual content is not one of the allowed types (even if its extension says it is), **Then** the upload is refused and the file is not stored.
3. **Given** a resident adding an attachment, **When** the file exceeds the per-file size limit (default 50 MB, environment-configurable), **Then** the upload is refused.
4. **Given** a request already at the per-application attachment count (default 20) or total-size (default 250 MB) limit, **When** the resident adds another attachment, **Then** the upload is refused.

---

### User Story 6 - Revise and resubmit after a denial (Priority: P3)

A homeowner whose request was denied (wording "Revisions requested" or "Denied") clicks "Revise and resubmit". A new revision is pre-filled from the previous one with every field editable; attachments carry over and can be removed or added to. On submit, the revision becomes a new submission (`ARC-<same number>` with the next revision badge, its own received and due dates, no votes), linked to the earlier revision, and appears in the board's Open list.

**Why this priority**: Closes the resubmission loop that 027 (FR-034/FR-035) explicitly hands to this spec, but it only applies after a denial and depends on the create/submit flow (P1).

**Independent Test**: Given a resident with a closed, denied request, invoke "Revise and resubmit", confirm a new revision is created pre-filled with carried-over attachments, edit a field and submit, and confirm the revision is `Open` with an incremented revision number linked to the prior revision and visible to the board.

**Acceptance Scenarios**:

1. **Given** a resident whose request `ARC-1042` is Closed with outcome Denied, **When** they choose "Revise and resubmit", **Then** a new revision is created from 027's revision factory, pre-filled from the previous revision with all fields editable and its attachments carried over.
2. **Given** a revision draft pre-filled from a denial, **When** the resident removes a carried-over attachment, **Then** it is removed from the revision only and the earlier revision's attachments are unchanged.
3. **Given** a revision draft, **When** the resident submits it, **Then** it becomes `Open` as `ARC-1042` revision 2 (badge "v2"), with its own received date and due date and no votes, linked to revision 1.
4. **Given** a request that is not Closed/Denied (e.g. Open, Approved, or Withdrawn), **When** the resident attempts to revise and resubmit it, **Then** the action is refused (only a closed, denied revision can be revised, per 027 edge case).

### Edge Cases

- A resident attempts to create, view, withdraw, reply to, or revise a request for a property they do **not** own → refused (403); see FR-022.
- A co-owner (second `UserProperty` on the same property) opens a request another owner created → they can see and act on it (withdraw, reply, revise).
- A user who is also a board member in the same community opens the resident view → still restricted to their own properties (no widening from board membership; 025 FR-015). As a board member they are recused from voting on their own property (027 FR-017).
- Two residents in the same community submit at nearly the same moment → each gets a distinct, sequential `ARC-<number>` from the atomic community counter; no collision.
- A resident deletes a draft that had attachments → the attachment objects are removed from storage, not orphaned.
- A resident tries to edit the content of an already-submitted request → not permitted; the only post-submission resident actions are reply-to-info-request, withdraw, and (after a denial) revise-and-resubmit.
- Object storage is temporarily unavailable during upload → the request is not left referencing a missing object; the upload fails cleanly and the resident can retry.
- A resident opens an attachment link after it has expired (>15 min) → the link no longer works and a fresh short-lived link must be issued.

## Requirements *(mandatory)*

### Functional Requirements

**Creating and drafting**

- **FR-001**: The system MUST allow a resident who owns a property (via `UserProperty`) to create an architectural application for that property with: project type, project title, description, planned start date, planned completion date, optional contractor information, zero or more attachments, and a required acknowledgement that work may not begin until the request is approved. (Planned dates, contractor, acknowledgement, and the Draft state are additive extensions to the 027 `ArchitecturalApplication`.)
- **FR-002**: Project type MUST be one of the canonical `ArcProjectType` values (027): `Fence`, `Solar`, `ExteriorPaint` (Exterior paint), `Outbuilding` (Shed/outbuilding), `Landscaping`, `WindowsDoors` (Windows/doors), `Addition`, `Other`.
- **FR-003**: The system MUST refuse to submit a request unless the "work may not begin until approved" acknowledgement is recorded as accepted.
- **FR-004**: The system MUST reject a request whose planned completion date is earlier than its planned start date.
- **FR-005**: Residents MUST be able to save a request as a Draft without submitting, edit a Draft's fields, and delete a Draft. A Draft MUST NOT appear in the board's Open list.
- **FR-006**: On submission, the system MUST assign a human-readable identifier `ARC-<number>` where `<number>` is drawn atomically from the community's ARC counter (`CommunityArcSettings.NextApplicationNumber`, starting at 1001) and is unique within the community; all revisions of one request share the same number.
- **FR-007**: On submission, the system MUST set the received date to the submission date and the due date to received date + the community's review period, and MUST snapshot the community's decision rule, lapse rule, and time zone onto the application at that moment (027 FR-029/FR-030; review period default 30 days).
- **FR-008**: After a request is submitted, the system MUST NOT allow the resident to edit the originally submitted content; the only resident actions on a submitted request are replying to an info request (FR-018), withdrawing (FR-020), and — after a denial — revising and resubmitting (FR-026).
- **FR-009**: On submission, the system MUST make the request available to the board's Open list consumed by 027 (this spec is the writer of the application data; 027 is the reader), setting `SubmittedByUserId` and the `OwnerName` snapshot.

**Attachments**

- **FR-010**: The system MUST accept attachments only of types PDF, JPG, PNG, and HEIC, and MUST validate the file by its actual content, not solely by its filename or declared extension.
- **FR-011**: The system MUST enforce a per-file size limit, a per-application attachment-count limit, and a per-application total-size limit, refusing uploads that would exceed any of them. These limits are configured at the environment level (application/infra configuration, not per-community), defaulting to 50 MB per file, 20 attachments per application, and 250 MB total per application.
- **FR-012**: Attachment bytes MUST be stored in private object storage (Cloudflare R2 in production, MinIO locally and in tests) under the `arc/{communityId}/{applicationNumber}/{guid}` key convention, with attachment metadata (filename, content type, size, owning application) persisted in PostgreSQL (027 `ArchitecturalAttachment`).
- **FR-013**: Attachments MUST never be exposed through durable public object URLs; they MUST be served only through pre-signed links (025 FR-039) that expire within 15 minutes, issued per request after an authorization check (matching 027 FR-012).
- **FR-014**: When a Draft is deleted, its attachment objects MUST be removed from storage so no orphaned objects remain.

**Tracking and visibility**

- **FR-015**: Residents MUST be able to view a "My architectural requests" list of their own requests (paginated with `limit`/`offset`, default page size 25, max 100) with a current status per the projection table, and a detail page showing the submitted fields, a chronological timeline, the attachments, and — once decided — the outcome, the owner-facing denial wording and the manager's reason (when denied), the community's formal disapproval statement (when denied), and the conditions of approval (when approved with conditions).
- **FR-016**: Residents MUST NOT be shown (and the system MUST NOT return to a resident) any board votes, voter identities, or board vote comments for any request (027 FR-019).

**More information requested**

- **FR-017**: When a board member uses "Request info" (027 FR-021) on a request, the owning resident(s) MUST see the question on the request and a corresponding alert on their dashboard.
- **FR-018**: Residents MUST be able to reply to an outstanding "more info requested" question (an `ArchitecturalInfoRequest` with `RespondedAt` null), optionally adding attachments; the reply body and attachments MUST be visible to the board and manager, and submitting the reply MUST set `RespondedAt`, clearing the "more info requested" marker. (The reply body and reply attachments are additive extensions to 027's `ArchitecturalInfoRequest`.)
- **FR-019**: An info request MUST NOT pause the review clock — the due date continues to count down while the request is in the "more info requested" state (027 FR-021).

**Withdraw**

- **FR-020**: Residents MUST be able to withdraw an undecided (`Open`, no decision reached) request; doing so MUST move it to `Closed` with outcome "withdrawn" and remove it from the board's Open list. Withdrawn requests MUST be excluded from the board's default Closed view and surfaced to the board only through an opt-in "show withdrawn" filter (a small additive extension to 027's board list; the historical record is retained, not deleted).
- **FR-021**: The system MUST refuse a withdraw of a request that is not an undecided `Open` request (already Approved, Denied, deemed-decided, or Withdrawn), leaving its state unchanged.

**Revise and resubmit**

- **FR-026**: Residents MUST be able to revise and resubmit a Closed, Denied request (either denial wording). The system MUST use 027's revision factory to create a new revision pre-filled from the previous one, with all fields editable and prior attachments carried over (removable without affecting earlier revisions; more may be added). On submit the revision becomes `Open` with the next revision number, its own received and due dates, no votes, linked to the previous revision (027 FR-034/FR-035). Revise-and-resubmit MUST be refused on any request that is not Closed/Denied.

**Authorization and tenancy**

- **FR-022**: Only owners of the property (users linked to it via `UserProperty`) MUST be able to create, view, reply to, withdraw, or revise that property's architectural requests; all such actions by a non-owner MUST be refused (403, failing closed without revealing existence per 025 FR-016). Co-owners linked to the same property MUST all be able to see and act on the property's requests.
- **FR-023**: Resident-scoped access MUST NOT widen because the user also holds a board or manager membership (025 FR-015); board/manager access to these applications is governed entirely by 027.
- **FR-024**: Every request MUST be scoped to the community of its property (`CommunityId` denormalized to equal `Property.CommunityId`, per 027); cross-community access to a request MUST be denied by default.

**Notifications**

- **FR-025**: On submission, the system MUST send the submitting resident a plain-template submission-confirmation email identifying the request by its `ARC-<number>` ID, queued through the existing OutboxMessage pipeline under a new `Kind` (e.g. `arc_owner_submitted`, within the existing 30-character limit) addressed by `RecipientUserId`. (Approved/denied/revisions-requested outcome emails are owned by 027 and are out of scope here.)

**Responsive UI**

- **FR-027**: Every resident ARC page (my-requests list, request form including attachments, request detail, info reply, withdraw confirmation, revise) MUST be fully usable and render without horizontal scrolling or clipped controls at phone (≈375 px), tablet (≈768 px) and desktop (≥1280 px) widths. **[no WFb]**

### Key Entities *(include if feature involves data)*

> **Shared with 027 — 029 extends.** The entities below are introduced by `027-board-arc-review` (which lands first). This spec adds fields and states additively; it does not redefine them. See the "Alignment with 027" section.

- **ArchitecturalApplicationDraft** (029, new): a resident's unsubmitted request for one property — the same fields as an application (project type, title, description, planned dates, optional contractor, acknowledgement) plus its own uploaded attachments and, for a revise-and-resubmit draft, the previous revision it revises and which carried-over attachments were removed. Editable and deletable; never visible to the board; holds no `ARC-` number or dates. Submitting it creates the application through 027's factory and removes the draft.
- **ArchitecturalApplication** (027): belongs to one Property (and thus one Community) and, for resident-submitted rows, to the submitting resident. 027 fields include `ApplicationNumber`/`Revision`/`PreviousRevisionId`, `ProjectType`, `ProjectTitle`, `Description`, `OwnerName`, `ReceivedDate`, `DueDate`, snapshotted rules, `Status` (`Open`/`DecisionReached`/`Closed`), and decision outcome/wording/reason/conditions. **029 adds** optional resident-authored fields (planned start/completion dates, contractor, acknowledgement time), withdrawal stamps, and a `Withdrawn` outcome. It never changes 027's existing fields.
- **ArchitecturalAttachment** (027): a file attached to an application revision — filename, size, content type, private storage key, created-at. **029 owns** the upload path: content-based type validation, the size/count limits, and the reply-attachment association.
- **ArchitecturalInfoRequest** (027): a board member's question (message, requester, requested-at) and `RespondedAt`. **029 adds** the resident reply body and reply attachments and sets `RespondedAt` on reply.
- **ApplicationTimelineEvent** (029, resident-safe): a chronological record of resident-visible status changes and actions (created, submitted, info requested, info replied, withdrawn, approved, denied, revised) shown on the detail page, derived where possible from 027 state; excludes vote-level and board-comment detail.
- **CommunityArcSettings** (027): source of the review period and the atomic `NextApplicationNumber` counter that 029 reads at submission.

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: `ArchitecturalApplication` and its attachments are community-scoped through their Property's `CommunityId` (025 Community entity; denormalized per 027). Cross-community access is denied by default and fails closed (025 FR-016). No intentional cross-community queries exist in this spec.
- **Authorization**: Create / view / reply / withdraw / revise require the acting user to own the request's property via `UserProperty` (resident property-scope, 025 FR-015). Server-side enforcement is mandatory; any frontend gating is UX-only. Board/manager authorization is out of scope (governed by 027). Holding a board/manager role MUST NOT widen resident access.
- **Ownership and moderation**: The request is owned by the property's owner(s); co-owners share access. Residents may edit/delete only Drafts; after submission content is immutable except for info-request replies, withdraw, and revise-and-resubmit (which creates a new revision, leaving earlier revisions unchanged). A resident-safe timeline provides the audit trail.
- **API contract**: Collection endpoints (e.g. "my requests") use `limit`/`offset` pagination, default 25, max 100. Timestamps are UTC; entity IDs are GUIDs with the `ARC-<number>` human identifier exposed alongside. Errors use the project's standard error shape with stable error codes for the denial cases: non-owner `403 FORBIDDEN`; disallowed content `422 UNSUPPORTED_FILE_TYPE`; oversize file `422 FILE_TOO_LARGE`; over the count/total limit `422 ATTACHMENT_LIMIT_REACHED`; missing acknowledgement `422 ACKNOWLEDGEMENT_REQUIRED`; withdraw of a decided or closed request `409 APPLICATION_DECIDED` / `409 APPLICATION_CLOSED`; revise of a non-denied request `409 REVISION_NOT_ALLOWED`; second reply `409 INFO_ALREADY_ANSWERED` (see `contracts/`). Resident responses MUST omit vote/voter/comment fields entirely (not merely null them).
- **API implementation and docs**: Endpoints implemented with FastEndpoints; documented via Swashbuckle/OpenAPI. `/swagger` remains development-only and disabled in production.
- **Database/runtime**: New columns/states are added via strict, additive, forward-only EF Core migrations that apply idempotently at Cloud Run startup and are compatible with Neon's low max-connection pooling and short-lived DbContext. The additions extend the 027 schema without loosening it: two new draft tables, new nullable columns on 027's tables (resident fields, withdrawal stamps, info-request reply fields), and the `Withdrawn` outcome value. No existing 027 column, constraint or index changes.
- **File storage**: Attachment objects go to Cloudflare R2 (prod) with metadata in PostgreSQL; MinIO covers local Docker Compose and tests. Access only via the shared short-lived (≤15 min) pre-signed URL primitive (025 FR-039); no durable public URLs. Content-based type validation and environment-configured size/count limits are enforced before any bytes are persisted.
- **Security and abuse controls**: Upload endpoints validate content type by bytes and enforce size/count limits before persisting. Every resident ARC write (draft changes, upload, submit, reply, withdraw, revise) is rate-limited by a per-user `resident-writes` policy (default 30 requests/minute, configurable), and a test proves the limit returns 429. Non-owner access attempts and rejected uploads are logged as sensitive events (Serilog, 025 FR-017) with the acting user and property/community, without logging file contents.
- **Observability**: Sentry error tracking and trace context across frontend/backend; environment/release tags applied. Attachment bytes, owner names, storage keys, and PII are excluded from traces/logs.
- **Responsiveness**: All resident ARC pages (list, form, detail, reply, withdraw, revise) function and render correctly at phone, tablet and desktop widths (constitution §6), verified by a browser test at 375, 768 and 1280 px. **[no WFb]**
- **Accessibility**: The create, track, reply, withdraw, and revise flows meet WCAG 2.1 AA — keyboard operable, labeled form controls, programmatically associated validation messages (type/size/acknowledgement/date errors), and accessible status/timeline presentation. **[no WFb]**
- **Quality gates**: Backend files touched meet the 95% coverage bar; Sonar static analysis passes; xUnit with Testcontainers.PostgreSQL + MinIO exercises the submission, upload, reply, withdraw, and revise paths with isolated per-test communities (parallel-safe); `[Theory]` variations cover each allowed/disallowed attachment type and each limit boundary; Serilog sensitive-events asserted for denial paths; Repowise docs refreshed for PR delivery; PR scoped to this vertical slice.
- **Frontend testing**: Karma unit tests and Angular Testing Library component tests for the create/track/reply/withdraw/revise components; Cypress E2E for the primary submit-and-track journey; Playwright where a real browser upload is needed; Storybook entries for new components. **[no WFb]**
- **Executable & living spec**: Every acceptance scenario above (including the denial scenarios: non-owner create/view/withdraw/revise refused, disallowed-type and oversize upload refused, withdraw/revise on an ineligible state refused, resident cannot see votes/voters/comments) maps to an automated test that runs on demand and passes before merge. This `spec.md` and `tasks.md` are updated before the PR. This spec extends 027's shared entities additively and reconciles any contradiction so the 025/027/029 corpus stays internally consistent.
- **Spec independence & parallelism**: This spec has a hard dependency on **025 only** (Community entity, `CommunityMembership`/roles, community-scope resolver, `IDocumentStorage` pre-signed URL primitive). It shares the `ArchitecturalApplication`/attachment/info-request entities with **027**: 027 lands first and introduces them (built/tested against seeded data); 029 extends them additively. 029 never requires 027 to land first for its own tests (it can exercise create/submit/track/withdraw against its own writes), and 027 never requires 029 (it seeds applications) — so the two proceed in parallel. 027 explicitly delegates resident intake, status pages, the info-request reply, and the owner-side revise-and-resubmit to this spec.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A resident can create and submit a complete architectural request (including at least one attachment) in under 5 minutes. (A usability target: the automated end-to-end journey proves the flow completes in a single pass; the time itself is checked in manual review.)
- **SC-002**: 100% of submitted requests receive a community-unique `ARC-<number>` with no duplicates and no reused numbers, even under concurrent submissions in the same community.
- **SC-003**: 100% of attachment uploads whose actual content is not an allowed type, or that exceed the per-file / per-application size or count limits, are rejected before any bytes are persisted.
- **SC-004**: 100% of attachment access occurs through links that stop working within 15 minutes; zero attachments are reachable through a durable public URL.
- **SC-005**: Zero board votes, voter identities, or board vote comments appear in any resident-facing list, detail view, or API response.
- **SC-006**: 100% of create/view/withdraw/reply/revise attempts by a non-owner of the property are refused.
- **SC-007**: A submission-confirmation email is queued for the submitting resident for 100% of successful submissions, and the request appears in the resident's tracking list within 5 seconds of submission (asserted by an automated test immediately after submit).
- **SC-008**: 100% of attempts to withdraw an ineligible request, or to revise a non-denied request, are refused with no state change.

## Assumptions

- **Attachment limits** *(resolved — Clarifications 2026-10-08)*: 50 MB per file, 20 files per application, 250 MB total per application, configured at the environment level so each deployment can tune them.
- **Review clock during info requests**: Per 027 FR-021, an info request does not pause the review clock (FR-019).
- **Review period default & snapshotting**: Owned by 027 (`CommunityArcSettings`, FR-029); defaults to 30 days; the review period, decision rule, lapse rule, and time zone are snapshotted onto the application at submission.
- **`ARC-<number>` numbering**: Per 027, the number is drawn atomically from `CommunityArcSettings.NextApplicationNumber` starting at 1001, shared across a request's revisions; revision ≥2 shows a "v{n}" badge.
- **Co-owner parity**: All owners linked to a property via `UserProperty` have equal access to that property's requests (view, reply, withdraw, revise); there is no "primary owner" distinction in this spec.
- **Required to submit**: project type, title, description, planned start and completion dates, and the acknowledgement; contractor and files are optional.
- **Submitted content is immutable to residents**: After submission the resident cannot edit the request body; corrections flow through the info-request reply, a withdraw, or (after a denial) revise-and-resubmit.
- **Email mechanism**: The submission-confirmation email is enqueued through the existing OutboxMessage transactional-email pipeline (shared with 027) rather than a direct provider call; opt-outs are owned by the Notification Settings spec and are out of scope.
- **Out of scope** (restating the issue): board review, voting, decision and lapse rules (027); approved/denied/revisions-requested outcome emails (027); notification opt-outs (Notification Settings spec); a manager entering an application on an owner's behalf.
