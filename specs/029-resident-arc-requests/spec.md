# Feature Specification: Resident Architecture Request

**Feature Branch**: `029-resident-arc-requests`
**Created**: 2026-10-08
**Status**: Draft
**Input**: GitHub issue #210 — "(Resident) Architecture Request". Resident Architectural Application Submission: homeowners file and track architectural (ARC) requests in the app. Sibling of `027-board-arc-review` (board review & voting, which reads the applications this spec creates; split from 027's clarifications on 2026-10-07). Builds on `025-board-overall-design` (Community entity, CommunityMembership/roles, community-scope resolver, `IDocumentStorage` pre-signed URLs).

## User Scenarios & Testing *(mandatory)*

> UI requirements below are marked **[no WFb]** because the Claude Design mockups for the resident submission flow are not yet present under `wireframes/`. When those mockups land, replace `[no WFb]` with the specific wireframe citation (`WFb ...`). The issue attaches two standalone HTML mockups: the **ARC Decision Emails** mockup belongs to the approved/denied outcome emails owned by 027 (out of scope here), and the **NekoHOA Wireframes** mockup is the general app wireframe, not the resident-ARC-specific flow.

### User Story 1 - File an architectural request (Priority: P1)

A homeowner wants to put up a fence (or install solar, repaint, add a shed, etc.). They open "Architectural requests" for a property they own, start a new request, pick the project type, describe the work, give planned start and completion dates, optionally name a contractor, attach supporting documents (plans, elevations, plat surveys, photos), and acknowledge that work may not begin until the request is approved. They can save the request as a draft and come back to it, edit it, or delete it. When ready, they submit; the request is assigned a community-unique ID (`ARC-<number>`), a received date, and a due date, and they receive a confirmation email.

**Why this priority**: This is the core of the feature — without the ability to create and submit a request, nothing else (tracking, board review in 027) can happen. It is the MVP.

**Independent Test**: A resident who owns a property can create a draft, edit it, submit it, observe the assigned `ARC-<number>` ID / received date / due date, and receive a submission-confirmation email — all without any board-side (027) functionality present.

**Acceptance Scenarios**:

1. **Given** a resident who owns a property, **When** they create a request with project type Fence, a title, a description, planned start and completion dates, the required acknowledgement checked, and submit it, **Then** the request is stored as Submitted, is assigned an ID of the form `ARC-<number>` that is unique within the property's community, records the received date as the submission date, and records the due date as received date + the community review period (027 FR-029, default 30 days).
2. **Given** a resident filling out a request, **When** they save it without submitting, **Then** the request is stored with status Draft and does not appear in the board's Open list.
3. **Given** a resident with a draft request, **When** they edit the draft's fields and save, **Then** the updated values are persisted and status remains Draft.
4. **Given** a resident with a draft request, **When** they delete the draft, **Then** the request (and any attachments it held) is removed and no longer appears in their list.
5. **Given** a resident filling out a request, **When** they attempt to submit without checking the "work may not begin until approved" acknowledgement, **Then** submission is refused and the request is not assigned an ID.
6. **Given** a resident filling out a request, **When** they enter a planned completion date earlier than the planned start date, **Then** submission is refused with a validation error.
7. **Given** a request was just submitted, **When** the submission completes, **Then** the submitting resident is sent a plain-template submission-confirmation email identifying the request by its `ARC-<number>` ID.
8. **Given** a request was just submitted, **When** the board's Open list (027) is queried, **Then** the submitted request is present in it (this spec writes the data 027 reads).

---

### User Story 2 - Track my architectural requests (Priority: P2)

A homeowner wants to know where each of their requests stands. They open "My architectural requests" and see a list of their requests with current status. They open one and see its full detail: the fields they submitted, a timeline of what has happened, the attachments (opened through short-lived links), and — once decided — the outcome and, if denied, the manager's denial reason. They never see how individual board members voted, who the voters were, or internal board comments.

**Why this priority**: Visibility into status is the second-most valuable capability and is what makes the feature usable day-to-day, but it depends on P1 having created requests.

**Independent Test**: A resident with at least one submitted request can open the list, see its status, open the detail page, open an attachment via a time-limited link, and confirm no board vote/voter/comment data is present — verifiable without the 027 voting UI.

**Acceptance Scenarios**:

1. **Given** a resident with requests in various states, **When** they open "My architectural requests", **Then** they see each request with a status of Draft, Submitted/Under review, More info requested, Approved, Denied, or Withdrawn.
2. **Given** a resident viewing a request's detail page, **When** the page loads, **Then** it shows the submitted fields, a chronological timeline of status changes, and the attachments.
3. **Given** a request has attachments, **When** the resident opens an attachment, **Then** it is served through a short-lived pre-signed link and never through a durable public object URL.
4. **Given** a request that was denied, **When** the resident views its detail page, **Then** they see the Denied outcome and the manager's denial reason.
5. **Given** a request that has board votes, voter identities, and board comments recorded (via 027), **When** the resident views the request, **Then** none of the votes, voter identities, or board comments are shown or returned to the resident.

---

### User Story 3 - Respond to a request for more information (Priority: P3)

While a request is under review, a board member asks a question via "Request info" (027). The homeowner sees the question on the request and as an alert on their dashboard. They reply, optionally attaching more documents. Their reply is visible to the board and the manager, and sending it clears the "more info requested" marker so review can continue.

**Why this priority**: Important for keeping a request moving, but only relevant once requests are submitted and a board member has asked something; a request can complete without it.

**Independent Test**: Given a request flagged "more info requested", the owning resident can see the question (on the request and as a dashboard alert), post a reply with an attachment, and observe that the info-requested marker is cleared and the reply is recorded for the board/manager to read.

**Acceptance Scenarios**:

1. **Given** a board member has used "Request info" on a resident's request, **When** the resident opens the app, **Then** they see the question on the request and a dashboard alert indicating more information is requested.
2. **Given** a request flagged "more info requested", **When** the resident submits a reply (with or without additional attachments), **Then** the reply is stored, is visible to the board and manager, and the "more info requested" marker is cleared.
3. **Given** a request is in the "more info requested" state, **When** the resident has not yet replied, **Then** the review due date continues to count down unchanged (the review clock is not paused by an info request).

---

### User Story 4 - Withdraw a request (Priority: P3)

A homeowner changes their mind before a decision is made and withdraws the request. It moves to Closed with outcome "withdrawn" and disappears from the board's open list.

**Why this priority**: A useful control for residents, but affects a minority of requests and is not needed for the core submit/track loop.

**Independent Test**: A resident with an undecided submitted request can withdraw it, see it become Closed/withdrawn in their list, and confirm it is no longer in the board's open list; attempting to withdraw a decided request is refused.

**Acceptance Scenarios**:

1. **Given** a resident with a submitted, undecided request, **When** they withdraw it, **Then** the request moves to Closed with outcome "withdrawn" and is removed from the board's open list.
2. **Given** a request that has already been Approved, Denied, or Withdrawn, **When** the resident attempts to withdraw it, **Then** the action is refused and the request's state is unchanged.

---

### User Story 5 - Attachments are validated and private (Priority: P1)

Whenever a homeowner attaches files to a request (at creation or when replying to an info request), only genuine PDF/JPG/PNG/HEIC files within the size and count limits are accepted, each file is checked by its actual content rather than just its filename, and every file is stored privately and only ever reachable through short-lived links.

**Why this priority**: Attachments are integral to the submission (plans, elevations, surveys) and are a security-sensitive surface; getting validation and privacy right is part of the MVP, not an add-on.

**Independent Test**: Uploading a valid PDF succeeds; uploading a file whose content is not an allowed type (e.g. an executable renamed `.pdf`) is rejected; uploading a file over the per-file limit, or exceeding the per-application total-size or count limit, is rejected; a stored attachment is only retrievable via a time-limited link.

**Acceptance Scenarios**:

1. **Given** a resident adding an attachment, **When** the file is a valid PDF, JPG, PNG, or HEIC within limits, **Then** it is accepted, stored in private object storage, and its metadata is recorded.
2. **Given** a resident adding an attachment, **When** the file's actual content is not one of the allowed types (even if its extension says it is), **Then** the upload is refused and the file is not stored.
3. **Given** a resident adding an attachment, **When** the file exceeds the per-file size limit, **Then** the upload is refused.
4. **Given** a request already at the per-application attachment count or total-size limit, **When** the resident adds another attachment, **Then** the upload is refused.

### Edge Cases

- A resident attempts to create, view, withdraw, or reply to a request for a property they do **not** own → refused (403); see FR-022.
- A co-owner (second `UserProperty` on the same property) opens a request another owner created → they can see and act on it.
- A user who is also a board member in the same community opens the resident view → still restricted to their own properties (no widening from board membership; 025 FR-015).
- Two residents in the same community submit at nearly the same moment → each gets a distinct, sequential `ARC-<number>`; no collision or gap that reuses a number.
- A resident deletes a draft that had attachments → the attachment objects are removed from storage, not orphaned.
- A resident tries to edit the content of an already-submitted request → not permitted; only replying to an info request or withdrawing is allowed after submission.
- Object storage is temporarily unavailable during upload → the request is not left referencing a missing object; the upload fails cleanly and the resident can retry.
- A resident opens an attachment link after it has expired → the link no longer works and a fresh short-lived link must be issued.

## Requirements *(mandatory)*

### Functional Requirements

**Creating and drafting**

- **FR-001**: The system MUST allow a resident who owns a property (via `UserProperty`) to create an architectural application for that property with: project type, project title, description, planned start date, planned completion date, optional contractor information, zero or more attachments, and a required acknowledgement that work may not begin until the request is approved.
- **FR-002**: Project type MUST be one of a fixed set: Fence, Solar, Exterior paint, Shed/outbuilding, Landscaping, Windows/doors, Addition, Other.
- **FR-003**: The system MUST refuse to submit a request unless the "work may not begin until approved" acknowledgement is recorded as accepted.
- **FR-004**: The system MUST reject a request whose planned completion date is earlier than its planned start date.
- **FR-005**: Residents MUST be able to save a request as a Draft without submitting, edit a Draft's fields, and delete a Draft. A Draft MUST NOT appear in the board's Open list.
- **FR-006**: On submission, the system MUST assign a human-readable identifier of the form `ARC-<number>` that is unique within the property's community, where `<number>` is a monotonically increasing sequence scoped to the community (no reuse, no board-visible gaps introduced by this spec).
- **FR-007**: On submission, the system MUST set the received date to the submission date and the due date to received date + the community's review period (027 FR-029; default 30 days when no community-specific period is configured).
- **FR-008**: After a request is submitted, the system MUST NOT allow the resident to edit the originally submitted content; the only resident actions on a submitted request are replying to an info request (FR-016) and withdrawing (FR-018).
- **FR-009**: On submission, the system MUST make the request available to the board's Open list consumed by 027 (this spec is the writer of the application data; 027 is the reader).

**Attachments**

- **FR-010**: The system MUST accept attachments only of types PDF, JPG, PNG, and HEIC, and MUST validate the file by its actual content, not solely by its filename or declared extension.
- **FR-011**: The system MUST enforce a per-file size limit and a per-application total-size and attachment-count limit, refusing uploads that would exceed any of them.
- **FR-012**: Attachment bytes MUST be stored in private object storage (Cloudflare R2 in production, MinIO locally and in tests), with attachment metadata (filename, content type, size, owning request, upload time) persisted in PostgreSQL.
- **FR-013**: Attachments MUST never be exposed through durable public object URLs; they MUST be served only through short-lived pre-signed links using the shared primitive from 025 FR-039.
- **FR-014**: When a Draft or request is deleted (where deletion is permitted), its attachment objects MUST be removed from storage so no orphaned objects remain.

**Tracking and visibility**

- **FR-015**: Residents MUST be able to view a "My architectural requests" list of their own requests with a current status of Draft, Submitted/Under review, More info requested, Approved, Denied, or Withdrawn, and a detail page showing the submitted fields, a chronological timeline, the attachments, and — once decided — the outcome and (if denied) the manager's denial reason.
- **FR-016**: Residents MUST NOT be shown (and the system MUST NOT return to a resident) any board votes, voter identities, or board comments for any request.

**More information requested**

- **FR-017**: When a board member uses "Request info" (027 FR-021) on a request, the owning resident(s) MUST see the question on the request and a corresponding alert on their dashboard.
- **FR-018**: Residents MUST be able to reply to a "more info requested" question, optionally adding attachments; the reply MUST be visible to the board and manager and MUST clear the "more info requested" marker.
- **FR-019**: An info request MUST NOT pause the review clock — the due date continues to count down while the request is in the "more info requested" state.

**Withdraw**

- **FR-020**: Residents MUST be able to withdraw an undecided (not yet Approved, Denied, or Withdrawn) request; doing so MUST move it to Closed with outcome "withdrawn" and remove it from the board's open list.
- **FR-021**: The system MUST refuse a withdraw of a request that is already Approved, Denied, or Withdrawn, leaving its state unchanged.

**Authorization and tenancy**

- **FR-022**: Only owners of the property (users linked to it via `UserProperty`) MUST be able to create, view, reply to, or withdraw that property's architectural requests; all such actions by a non-owner MUST be refused (403). Co-owners linked to the same property MUST all be able to see and act on the property's requests.
- **FR-023**: Resident-scoped access MUST NOT widen because the user also holds a board or manager membership (025 FR-015); board/manager access to these applications is governed entirely by 027.
- **FR-024**: Every request MUST be scoped to the community of its property; cross-community access to a request MUST be denied by default.

**Notifications**

- **FR-025**: On submission, the system MUST send the submitting resident a plain-template submission-confirmation email identifying the request by its `ARC-<number>` ID. (Approved/denied outcome emails are owned by 027 and are out of scope here.)

### Key Entities *(include if feature involves data)*

> **Shared with 027.** The `ArchitecturalApplication` and its attachment entity are shared between this spec and `027-board-arc-review`. Whichever spec lands first introduces these entities; the other extends them. This spec owns the resident-authored fields and lifecycle (draft → submitted → withdrawn, info-request replies); 027 owns the board review, voting, decision, and lapse fields. Both can proceed in parallel against this shared model.

- **ArchitecturalApplication**: A homeowner's request to make an architectural change to a property. Belongs to one Property (and thus one Community) and is authored by a resident owner. Key attributes: human-readable community-unique ID (`ARC-<number>`), project type (enum), title, description, planned start date, planned completion date, optional contractor, acknowledgement flag, status (Draft, Submitted/Under review, More info requested, Approved, Denied, Withdrawn), received date, due date, outcome (for closed requests, incl. "withdrawn"), and the manager's denial reason (resident-visible). Board votes, voter identities, and board comments (owned by 027) are associated but never exposed to residents.
- **ArchitecturalApplicationAttachment**: A file attached to an application (at creation or on an info-request reply). Key attributes: owning application, original filename, validated content type, size, private storage key, upload time, and who uploaded it. Bytes live in private object storage; only metadata lives in PostgreSQL.
- **InfoRequest / Reply (thread)**: A board member's "Request info" question (originating in 027) and the resident's reply(ies) with optional attachments. The question and resident replies are visible to the resident; the reply is additionally visible to board/manager. The marker that a request is awaiting resident info is resident-visible and is cleared on reply.
- **ApplicationTimelineEvent**: A chronological, resident-safe record of status changes and actions (created, submitted, info requested, info replied, withdrawn, approved, denied) shown on the detail page. Excludes vote-level and board-comment detail.

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: `ArchitecturalApplication` and its attachments are community-scoped through their Property's `CommunityId` (025 Community entity). Cross-community access is denied by default. No intentional cross-community queries exist in this spec.
- **Authorization**: Create / view / reply / withdraw require the acting user to own the request's property via `UserProperty` (resident property-scope, 025 FR-015). Server-side enforcement is mandatory; any frontend gating is UX-only. Board/manager authorization is out of scope (governed by 027). Holding a board/manager role MUST NOT widen resident access.
- **Ownership and moderation**: The request is owned by the property's owner(s); co-owners share access. Residents may edit/delete only Drafts; after submission content is immutable except for info-request replies and withdraw. A resident-safe timeline provides the audit trail of status changes.
- **API contract**: Collection endpoints (e.g. "my requests") use `limit`/`offset` pagination with documented defaults and max. Timestamps are UTC; IDs are GUIDs internally with the `ARC-<number>` human identifier exposed alongside. Errors use the project's standard error shape with stable error codes for the denial cases (non-owner, disallowed/oversize upload, withdraw-after-decision). Resident responses MUST omit vote/voter/comment fields entirely (not merely null them).
- **API implementation and docs**: Endpoints implemented with FastEndpoints; documented via Swashbuckle/OpenAPI. `/swagger` remains development-only and disabled in production.
- **Database/runtime**: New tables/columns introduced via strict, additive EF Core migrations applied idempotently at startup (Cloud Run) and compatible with Neon's low max-connection pooling and short-lived DbContext. The shared schema with 027 must be forward-compatible (whichever lands first creates it; the other extends additively).
- **File storage**: Attachment objects go to Cloudflare R2 (prod) with metadata in PostgreSQL; MinIO covers local Docker Compose and tests. Access only via the shared short-lived pre-signed URL primitive (025 FR-039); no durable public URLs. Content-based type validation required.
- **Security and abuse controls**: Upload endpoints validate content type by bytes and enforce size/count limits before persisting. Submission, withdraw, info-reply, and upload are rate-limited consistent with existing resident endpoints. Non-owner access attempts and rejected uploads are logged as sensitive events (Serilog) with the acting user and property/community, without logging file contents.
- **Observability**: Sentry error tracking and trace context across frontend/backend; environment/release tags applied. Attachment bytes and PII are excluded from traces/logs.
- **Accessibility**: The create, track, reply, and withdraw flows meet WCAG 2.1 AA — keyboard operable, labeled form controls, programmatically associated validation messages (type/size/acknowledgement errors), and accessible status/timeline presentation. **[no WFb]**
- **Quality gates**: Backend files touched meet the 95% coverage bar; Sonar static analysis passes; xUnit with Testcontainers.PostgreSQL + MinIO exercises the webhook-free persistence and storage paths; Theory variations cover each allowed/disallowed attachment type and each limit boundary; tests are safe under parallel execution and prior-run artifacts; Serilog logging asserted for denial paths; Repowise docs refreshed for PR delivery; PR scoped to this vertical slice.
- **Frontend testing**: Karma unit tests and Angular Testing Library component tests for the create/track/reply/withdraw components; Cypress E2E for the primary submit-and-track journey; Playwright where a real browser upload is needed; Storybook entries for new components. **[no WFb]**
- **Executable & living spec**: Every acceptance scenario above (including the denial scenarios: non-owner create/view/withdraw refused, disallowed-type and oversize upload refused, withdraw-after-decision refused, resident cannot see votes/voters/comments) maps to an automated test that runs on demand and passes before merge. This `spec.md` and `tasks.md` are updated before the PR. Any contradiction with 025/027 is reconciled so the spec corpus stays internally consistent.
- **Spec independence & parallelism**: This spec has a hard dependency on **025 only** (Community entity, `CommunityMembership`/roles, community-scope resolver, `IDocumentStorage` pre-signed URL primitive) — these are prerequisite platform capabilities that cannot be re-created here. It shares the `ArchitecturalApplication`/attachment entities with **027**, but the split is designed so both can proceed in parallel: whichever lands first introduces the shared entities and the other extends them additively; this spec never requires 027 to land first (it writes data 027 reads, and treats board-side fields as present-but-hidden).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A resident can create and submit a complete architectural request (including at least one attachment) in under 5 minutes.
- **SC-002**: 100% of submitted requests receive a community-unique `ARC-<number>` ID with no duplicates and no reused numbers, even under concurrent submissions in the same community.
- **SC-003**: 100% of attachment uploads whose actual content is not an allowed type, or that exceed the per-file / per-application size or count limits, are rejected before any bytes are persisted.
- **SC-004**: 100% of attachment access occurs through short-lived links; zero attachments are reachable through a durable public URL.
- **SC-005**: Zero board votes, voter identities, or board comments appear in any resident-facing list, detail view, or API response.
- **SC-006**: 100% of create/view/withdraw/reply attempts by a non-owner of the property are refused.
- **SC-007**: A submission-confirmation email is delivered to the submitting resident for 100% of successful submissions, and the request appears in the resident's tracking list within 5 seconds of submission.
- **SC-008**: 100% of attempts to withdraw an already-decided (Approved/Denied) or already-withdrawn request are refused with no state change.

## Assumptions

- **Attachment limits**: Pending explicit limits in the issue, the default limits are **25 MB per file**, **15 attachments per application**, and **100 MB total per application**. These are documented here so `/speckit.plan` or `/speckit.clarify` can adjust; they do not change the shape of the feature.
- **Review clock during info requests**: Per the issue's stated default, an info request does **not** pause the review clock (FR-019). Revisit only if clarified otherwise.
- **Review period default**: The community review period is owned by 027 (FR-029) and defaults to **30 days** when no community-specific value is configured; this spec consumes that value to compute the due date.
- **`ARC-<number>` format**: `<number>` is a plain sequential integer scoped per community (e.g. `ARC-1`, `ARC-2`, …). Zero-padding/prefix styling is a presentation detail left to planning.
- **Co-owner parity**: All owners linked to a property via `UserProperty` have equal access to that property's requests (view, reply, withdraw); there is no "primary owner" distinction in this spec.
- **Submitted content is immutable to residents**: After submission the resident cannot edit the request body; corrections flow through the info-request reply mechanism or a withdraw-and-resubmit.
- **Email provider**: The submission-confirmation email uses the project's existing email sending path (026 SES provider) with a plain template; opt-outs are owned by the Notification Settings spec and are out of scope.
- **Out of scope** (restating the issue): board review, voting, decision and lapse rules (027); approved/denied outcome emails (027); notification opt-outs (Notification Settings spec); a manager entering an application on an owner's behalf.
