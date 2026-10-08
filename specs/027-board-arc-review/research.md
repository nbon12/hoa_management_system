# Research: Board Architectural Review (ARC)

**Feature**: `027-board-arc-review` | **Date**: 2026-10-07 | **Spec**: [spec.md](./spec.md)

Each item records a decision, why it was chosen, and what else was considered. Everything was checked against `main` at `fb6be6b`.

---

## R1. Authorization: new capabilities on the existing resolver

**Decision**: Add three values to `CommunityCapability` (`Features/Board/ICommunityScopeResolver.cs`) and map them in `CommunityScopeResolver`:

| Capability | Granted to | Used for |
|---|---|---|
| `ViewArchitecturalApplications` | BoardMember, CommunityManager | List, detail, attachment links, Needs-your-vote |
| `VoteArchitecturalApplications` | BoardMember | Votes, info requests |
| `ManageArchitecturalReview` | CommunityManager | Record outcome, resend email, ARC settings |

Recusal (spec FR-017) is checked in the vote handler after the capability check: a voter is recused when a `UserProperty` row links them to the application's property.

**Rationale**: Spec 025 FR-012 requires every board endpoint to authorize through the single resolver, and its static-analysis test enforces this. The existing `ViewAssociationData` also grants Accountant, but the spec gives Accountants no ARC access, so a new view capability is needed rather than reusing that one.

**Alternatives considered**: Reusing `ViewAssociationData` (rejected: leaks ARC to Accountants). Role checks inside endpoints (rejected: violates 025 FR-012 and fails `BoardScopeEnforcementStaticAnalysisTests`).

## R2. Where the code lives, and keeping the static scope test honest

**Decision**: Backend endpoints go in `HOAManagementCompany/Features/Board/Architectural/`. `BoardScopeEnforcementStaticAnalysisTests` currently scans `Features/Board` with `SearchOption.TopDirectoryOnly`, so it is changed to `AllDirectories`. The scheduler-only job endpoint (R6) joins the documented allow-list with its reason ("secret-authenticated job, not a user-facing community resource").

**Rationale**: A subfolder keeps the ARC slice together, as `Features/Payments/*` does. Without widening the scan, the new endpoints would silently escape the guard that 025 built.

**Alternatives considered**: Putting files flat in `Features/Board/` (rejected: that folder becomes a dumping ground as specs 2–6 land).

## R3. Application identity, display number and revisions

**Decision**: `ArchitecturalApplication` has a GUID `Id` (used in every API path), an integer `ApplicationNumber` that is unique per community (shown as `ARC-<n>`), and an integer `Revision` (1 for the first submission, shown as a "v2" badge from revision 2 on). A unique index covers `(CommunityId, ApplicationNumber, Revision)`. `PreviousRevisionId` links each revision to the one before it.

Numbers are allocated from `CommunityArcSettings.NextApplicationNumber` with a single `UPDATE … SET "NextApplicationNumber" = "NextApplicationNumber" + 1 … RETURNING`, which is atomic under concurrency. Numbering starts at 1001 for new communities. A revision reuses its application's number.

**Rationale**: The spec requires GUID entity IDs with `ARC-` as a display handle only (Constitution Requirements → API contract). A per-community counter row is the simplest gap-tolerant, race-free allocator on Postgres, and needs no sequence per community.

**Alternatives considered**: One Postgres sequence per community (rejected: has to be created dynamically and isn't EF-friendly). `MAX()+1` (rejected: races under concurrent submission).

## R4. Vote counting, decision rules and concurrency

**Decision**: There is one pure, static decision function (`ArcDecisionRules.Evaluate`). It takes the decision rule, the eligible voter count, the vote counts, and whether the due date has passed, and returns `None`, `Approved`, `DeniedRevisionsRequested` or `Denied`.

- **Sides**: approve, and denial (RevisionsNeeded + Deny).
- **Majority of members**: a side wins once it holds more than half of eligible members.
- **Majority of votes cast with a quorum**: quorum is more than half of eligible members having voted. A side wins early once quorum is met and the remaining eligible votes can't overtake it. At the due date, the leading side wins if quorum is met. No quorum or a tie hands over to the lapse rule.
- **Wording**: denial is RevisionsRequested when RevisionsNeeded votes are at least as many as Deny votes, otherwise Denied.
- **Eligible members**: active BoardMember memberships minus recused members, counted when each vote is evaluated. Votes already cast by members who have since left still count.

Writes take `SELECT … FOR UPDATE` on the application row inside one transaction: insert the vote, re-count, evaluate, set the decision. A unique index on `(ApplicationId, VoterUserId)` blocks double votes. A vote arriving after a decision is refused with 409 `APPLICATION_DECIDED`.

**Rationale**: A pure function makes the `[Theory]` matrix in the Constitution Requirements cheap and exhaustive. A row lock is the simplest way to guarantee one decision (SC-005) on Neon without serializable isolation.

**Alternatives considered**: Optimistic concurrency with `xmin` (rejected: retry loops on the hot path and harder to test). Serializable isolation (rejected: retry handling, and it's unusual elsewhere in this codebase).

## R5. Emails: reuse the transactional outbox

**Decision**: Reuse `OutboxMessage` and `OutboxDispatcher` (spec 006/026), which already send through `SesEmailProvider` behind `IAlertProvider`, with a `PayloadJson` holding target, subject and body. Two changes:

1. `OutboxMessage.OwnerId` becomes nullable, and a nullable `RecipientUserId` (FK to `AspNetUsers`, cascade delete) is added. Board emails go to users who may not be owners. A check constraint requires exactly one of `OwnerId` or `RecipientUserId`.
2. New `Kind` values: `arc_owner_approved`, `arc_owner_revisions_requested`, `arc_owner_denied`, `arc_board_reminder`, `arc_board_lapsed`. All route to the `email` channel (the dispatcher's default branch).

`DedupKey` makes each email happen exactly once:

| Email | DedupKey |
|---|---|
| Owner outcome | `arc:{applicationId}:outcome` |
| Pre-deadline reminder | `arc:{applicationId}:reminder:{userId}` |
| Lapse notice | `arc:{applicationId}:lapse:{userId}` |

A manager's resend (FR-026) adds a fresh row with `arc:{applicationId}:outcome:resend:{n}`. Outbox rows are written in the same transaction as the state change, then dispatched in-process right after commit (same as the webhook path), with the sweep job as backstop (R6).

Bodies are rendered by an `ArcEmailRenderer`: plain text for now, with HTML templates swapped in when the Claude Design templates arrive (spec Assumptions). Before the Notification Settings spec lands, board emails go to every active board member. That spec will add a preference check at enqueue time, through an `IArcNotificationPreferences` seam that allows everyone for now.

**Rationale**: The outbox already provides atomic enqueue, dedup and terminal-failure semantics, and SES is wired in. Making `OwnerId` nullable is additive and doesn't change existing payment rows.

**Alternatives considered**: A separate ARC outbox table and dispatcher (rejected: duplicates working, tested machinery). Sending SES directly in the request (rejected: an email could be lost if the transaction rolls back, or sent when it shouldn't be).

**Risk**: Payment code that reads `OwnerId` must handle the nullable type. Every current producer sets it, so this is a compile-time sweep, and the payment outbox tests re-run unchanged.

## R6. Time-based work: reminder and lapse sweep

**Decision**: Add `POST /api/v1/architectural/jobs/sweep`, authenticated by the existing `X-Scheduler-Secret` header with a constant-time compare (the same pattern as `RunDraftsEndpoint`). It is idempotent and does three things:

1. **Reminders**: open applications with no decision, where today in the community's time zone is at or after `DueDate − ReminderDays` (ReminderDays > 0), and no reminder sent yet. Enqueue a reminder per active board member and stamp `ReminderSentAt`.
2. **Lapses**: open applications with no decision and past the end of `DueDate` in the community's time zone, with `LapseProcessedAt` null. Under majority-of-votes-cast, evaluate the due-date branch of R4 first; if that produces a decision, no lapse. Otherwise apply the lapse rule snapshotted on the application, stamp `LapseProcessedAt`, and enqueue the lapse emails.
3. **Dispatch**: drain pending outbox rows.

A Cloud Scheduler job (`google_cloud_scheduler_job`, hourly) is added to `infra/modules/environment` and calls the endpoint with the existing `scheduler-secret`. PR environments don't get the job; the quickstart triggers the sweep by hand with curl, as the payment jobs do.

The board UI doesn't depend on the sweep for display: "overdue" is computed from `DueDate` at read time. Only state changes (deemed outcomes) and emails wait for the sweep, so FR-027/FR-028 take effect within one hour (spec Assumptions).

**Rationale**: Cloud Run scales to zero, so an in-process timer wouldn't fire while idle. The secret-authenticated job pattern already exists and is tested. Stamping the application (`LapseProcessedAt`, `ReminderSentAt`) plus the outbox dedup keys keeps re-runs harmless.

**Alternatives considered**: An `IHostedService` timer (rejected: doesn't run under scale-to-zero, and multiple instances would double-process). Lazily applying the lapse on read (rejected: a GET with side effects, and emails tied to page views).

**Note**: The existing payment job endpoints have no scheduler resource in OpenTofu. This spec adds one for ARC only. Scheduling the payment jobs stays out of scope.

## R7. Community time zone

**Decision**: `CommunityArcSettings.TimeZoneId` is an IANA ID, default `America/New_York`, validated with `TimeZoneInfo.FindSystemTimeZoneById` (Linux containers ship tzdata). Due dates are `DateOnly`, and "past due" means after 23:59:59 of `DueDate` in that zone.

**Rationale**: The governing documents count calendar days. Without a zone, an application could lapse up to 5 hours early or late around UTC midnight. `Community` has no zone column, and the ARC settings are the only consumer so far.

**Alternatives considered**: Adding `Community.TimeZoneId` (deferred: wider than this spec; it can move later). UTC due dates (rejected: off-by-one-day risk against "45 days").

## R8. Attachments and the private-link primitive

**Decision**: `ArchitecturalAttachment` stores `FileName`, `SizeBytes`, `ContentType` and `StorageKey` per revision. Objects live under `arc/{communityId}/{applicationNumber}/{guid}` in the existing bucket. Links come from `IDocumentStorage.GetPreSignedUrlAsync`, which already expires in 5 minutes (within FR-012's 15-minute cap), through `GET …/attachments/{attachmentId}/url`. That endpoint authorizes, emits the 025 FR-017 sensitive event, and returns `{ url, expiresAt }`. The list and detail responses never contain URLs, only attachment IDs.

When a revision carries attachments over, it copies the metadata rows pointing at the same `StorageKey`. Objects are never deleted while any row references them, and this spec deletes nothing.

**Rationale**: This reuses 025's primitive unchanged. Issuing links on demand means a page or log never holds a link for long, and every access is audited.

**Alternatives considered**: Embedding URLs in the detail response (rejected: links expire while the page is still open, and it logs access the user didn't make). Copying objects per revision (rejected: storage cost for no benefit).

## R9. Rate limiting

**Decision**: Add a `board-writes` policy in `Program.cs`: a fixed window partitioned by user ID, 30 requests per minute, configurable as `RateLimiting:BoardWritesPermitsPerMinute` on the validated `RateLimitingOptions`. Apply it to the vote, info-request, outcome, resend and settings endpoints.

**Rationale**: Constitution §7 requires rate limits on content-creation endpoints. Votes and info requests are user-generated content.

**Alternatives considered**: Reusing `payments` (rejected: different budget and semantics).

## R10. Frontend structure

**Decision**:
- Add `features/board/architectural/` with standalone components: `applications-page` (tabs, search, table), `tally`, `application-detail-panel`, `cast-vote-card`, `needs-your-vote-card` (placed on Community Home), and `arc-settings` (manager only).
- Add a new `architectural.service.ts` in `core/services/`.
- Route: `board/architectural` with `boardGuard` and `requiredRoles: ['BoardMember', 'CommunityManager']`.
- In `BoardNavigationService`, the "Architectural Applications" entry changes from a stub to `/app/board/architectural`, and a manager-only "ARC Settings" item is added.
- The Community Home placeholder gets the `needs-your-vote-card` above the metrics panels as its own section (spec independence note: spec 2 owns the rest of that page).

**Rationale**: Matches the 025 layout and its "nav as data" contract (025 FR-024). The stub was already in place for this.

**Alternatives considered**: A separate Angular feature module (rejected: the app is standalone-component throughout).

## R11. Seed and demo data

**Decision**: Add an `ArchitecturalSeeder`, called from `DatabaseSeeder` and idempotent like `EnsureBoardUserAsync`. It creates `CommunityArcSettings` for the seeded community and four applications mirroring the wireframe (ARC-1036, 1039, 1041, 1042), with small placeholder PDFs and images uploaded through the `StorageSeeder` pattern. It also adds four extra board members (`board2@nekohoa.dev` … `board5@nekohoa.dev`) so the 5-member tally is real, and a community manager, `manager@nekohoa.dev`. No manager is seeded today, and one is needed to record outcomes and edit settings. All use the existing seed password and are Dev/Development only, like the existing seed users.

`board@nekohoa.dev` is left without a vote on ARC-1042, so "Needs your vote" is populated. One closed, denied v1 plus an open v2 demonstrates revisions.

**Rationale**: The spec's Assumptions promise demo data for the board user. Applications can't be created in-app until the submission spec lands.

## R12. Testing approach

**Decision**:
- **Backend**: xUnit integration tests in `HOAManagementCompany.Tests/Integration/Board/Architectural/`, built on `BoardTestBase` (Testcontainers PostgreSQL and MinIO, isolated communities per test). One test class per user story, plus:
  - A `[Theory]` over `ArcDecisionRules` covering board sizes 3–6, both rules, recusal, quorum met and unmet at the due date, and the revisions-vs-deny wording split.
  - A concurrency test firing the deciding votes in parallel and asserting one decision and one outcome email row.
  - Sweep tests with a controllable `TimeProvider`. Production code registers `TimeProvider.System`; tests replace it with a fake clock. This is the first use of `TimeProvider` in the app.
  - Outbox assertions on `Kind`, `DedupKey` and recipients.
  - Sensitive-event assertions through the `LogSink` Serilog capture the 025 tests already use (`MembershipAdminEndpointsTests`).
- **Frontend**:
  - Karma/Jasmine for the tally label, the rule text and the service.
  - Angular Testing Library for the page, panel, card and settings.
  - Playwright for the vote journey and refusal of a Resident on the route.
  - Cypress for sign-in → board mode → Architectural Applications → vote.
  - Storybook stories for the table, tally, detail panel and card.

**Rationale**: Mirrors 025's structure. `TimeProvider` avoids sleeping or clock hacks for the deadline logic.
