# Research: Resident Architecture Request

**Feature**: `029-resident-arc-requests` | **Date**: 2026-10-08 | **Spec**: [spec.md](./spec.md)

Each item records a decision, its rationale, and alternatives considered. Checked against `main` and the `027-board-arc-review` branch (PR #209), which lands first and owns the shared ARC data model.

---

## R1. Authorization: resident active-property scope, not the board resolver

**Decision**: Resident ARC endpoints authorize exactly like the existing `Features/Property/*` endpoints: `var propertyId = User.RequirePropertyId()` reads the active-property GUID from the JWT `propertyId` claim (`Features/Common/ClaimsPrincipalExtensions.cs`), and every query/command is pinned to `application.PropertyId == propertyId`. No `ICommunityScopeResolver` call and no `CommunityCapability` — those are board-side only.

- **Co-owner parity** falls out for free: co-owners each hold a `UserProperty` row for the same property (`AuthService`), so when a co-owner's active property is that property, their claim matches and they can view/act. There is no "primary owner".
- **A board/manager role never widens access** (025 FR-015): the resident endpoints never read membership; they only read the property claim.
- A request for an application whose `PropertyId` ≠ the caller's claim returns the 025 non-disclosing `403 FORBIDDEN` (not 404-with-details), matching 027's cross-scope behavior.

**Rationale**: The resident app is already single-active-property via the JWT claim, with `AuthService.SwitchPropertyAsync` to change it. Reusing that is the smallest, best-tested surface and keeps 029 free of board concepts.

**Alternatives considered**: A new resident-scope resolver (rejected: the claim already encodes the scope and the `Features/Property` endpoints prove the pattern). Scoping "my requests" across *all* of a user's properties regardless of the active one (rejected: inconsistent with the rest of the resident app, which shows one active property; switching property re-scopes naturally).

## R2. Shared model: extend 027's tables additively

**Decision**: 027 creates `ArchitecturalApplication`, `ArchitecturalAttachment`, `ArchitecturalInfoRequest`, `ArchitecturalVote`, `CommunityArcSettings` and the `Arc*` enums. 029 adds one forward-only migration `<ts>_AddResidentArcSubmission` that extends them (full column list in [data-model.md](./data-model.md)):

- `ArchitecturalApplication`: resident-authored fields (`PlannedStartDate`, `PlannedCompletionDate`, `ContractorName`, `ContractorContact`, `AcknowledgedNoWorkUntilApproved`), withdrawal stamps (`WithdrawnAt`, `WithdrawnByUserId`), and `ReceivedDate`/`DueDate` made **nullable** (null while Draft).
- `ArchitecturalInfoRequest`: `ResponseMessage`, `RespondedByUserId` (027 already has `RespondedAt`, which 029 sets on reply).
- `ArchitecturalAttachment`: `InfoRequestId?` (tags a reply attachment) and `UploadedByUserId?`.
- Enum string values: `ArcApplicationStatus.Draft`, `ArcOutcome.Withdrawn` (stored as strings — no DB enum type to alter).

**Rationale**: The issue and 027's plan both commit to one shared model, with whichever spec lands first introducing the tables. 027 is fully implemented, so 029 extends. Additive, nullable columns are safe on existing (seeded) rows and keep 027's board reads working unchanged.

**Alternatives considered**: Parallel resident-only tables (rejected: forks the entity the board reads, breaks the single `ARC-<n>`/revision lineage, and duplicates the attachment model). A `ResidentArcDraft` side table merged at submission (rejected: duplicates the whole application shape for no gain; nullable dates are simpler).

## R3. Drafts and number/date allocation

**Decision**: A draft is an `ArchitecturalApplication` row with `Status = Draft`, `Revision = 1`, no `ApplicationNumber` yet (allocated at submission), and null `ReceivedDate`/`DueDate`. Drafts are editable and deletable, are filtered out of every board query (027 lists `Open`/`DecisionReached`/`Closed`, never `Draft`), and are the resident's own.

On **submit**, 029 calls 027's `ArcApplicationFactory`: it allocates `ApplicationNumber` atomically from `CommunityArcSettings.NextApplicationNumber` (`UPDATE … +1 … RETURNING`, starting at 1001), sets `ReceivedDate = today`, `DueDate = ReceivedDate + ReviewPeriodDays`, and snapshots `DecisionRule`/`LapseRule`/`TimeZoneId`, then flips `Status → Open` and sets `SubmittedByUserId` + the `OwnerName` snapshot. If the factory's submission path does not already exist (027 built it for the *revision* case and seeding), 029 adds a `SubmitFromDraft` method on the factory so the allocation logic stays in one place.

**Rationale**: Keeping number/date/snapshot allocation inside the single factory (027 R3) preserves the race-free guarantee (SC-002) and the one-source-of-truth for the `ARC-<n>`/revision invariants.

**Alternatives considered**: Allocating the number at draft creation (rejected: drafts may never be submitted, leaving gaps and leaking a community-visible counter). `MAX()+1` at submit (rejected: races, as 027 R3 found).

## R4. Attachments: content validation, env-level limits, private storage

**Decision**:
- **Allowed types**: PDF, JPG, PNG, HEIC. Validation is by **content**, not extension: an `ArcAttachmentValidator` sniffs magic bytes — `%PDF` (PDF), `FF D8 FF` (JPEG), `89 50 4E 47` (PNG), and the ISO-BMFF `ftyp` box with a `heic`/`heix`/`mif1`/`heif` brand (HEIC). A declared content-type/extension that disagrees with the sniffed type is rejected (`VALIDATION_ERROR`).
- **Limits** are environment-level via a validated `ArcUploadOptions` (section `Architectural:Uploads`), defaulting to 50 MB/file, 20 files/application, 250 MB total/application, registered with the existing `AddValidatedOptions` + FluentValidation `ValidateOnStart` pattern (`Infrastructure/Configuration`). Per-file size is checked from the stream length; count and total are checked against existing attachments on the application in the same transaction.
- **Storage**: bytes go through `IDocumentStorage.UploadAsync` to R2/MinIO. Submitted-application objects use 027's `arc/{communityId}/{applicationNumber}/{guid}` key; draft objects (no number yet) use `arc/{communityId}/draft/{applicationId}/{guid}` and are re-keyed or left in place at submission (left in place — the stored key is authoritative; no object copy needed, the metadata row keeps whatever key was written). Metadata rows are `ArchitecturalAttachment`.
- **Serving**: only via `GET …/attachments/{id}/url` → `IDocumentStorage.GetPreSignedUrlAsync` (5-min expiry, within the 15-min cap). No durable URLs in any list/detail response.
- **Deletion**: deleting a draft (or removing a carried-over attachment on a revision draft) deletes the object via a new `IDocumentStorage.DeleteAsync`, but **only** when no other `ArchitecturalAttachment` row references the same `StorageKey` (revisions share keys per 027 R8).

**Rationale**: Content sniffing is the spec's explicit requirement (FR-010) and the only defense against a renamed executable. Env-level limits (the user's clarification) keep upload cost a deployment concern, not a per-community governing-document concern. Reusing `IDocumentStorage` keeps the private-link guarantee intact.

**Alternatives considered**: Extension/declared-MIME trust (rejected: FR-010 forbids it). Per-community limits in `CommunityArcSettings` (rejected: the clarification put limits at the environment level). A full image/PDF parse (rejected: magic-byte sniffing is sufficient and cheap; deep parsing risks DoS on crafted files).

## R5. Withdraw representation and the board "show withdrawn" filter

**Decision**: Withdrawing an undecided `Open` application sets `Status = Closed`, `DecisionOutcome = Withdrawn`, `WithdrawnAt`, `WithdrawnByUserId`, and `ClosedAt` — a resident-initiated close that bypasses `DecisionReached` and the manager outcome path, and enqueues **no** email. The board's list query (`ArcQueries`/`ApplicationsListEndpoint`) gets an additive, backward-compatible `includeWithdrawn` (default `false`): the default `closed` tab filters `DecisionOutcome != 'Withdrawn'`, and `includeWithdrawn=true` includes them. The resident projection maps this state to "Withdrawn".

**Rationale**: Matches the spec's "moves to Closed with outcome withdrawn" wording and the 2026-10-08 clarification (hidden from the board by default, opt-in filter, record retained). Representing it as an outcome rather than a new status touches the fewest 027 read paths — only the one list filter — and withdraw never triggers 027's outcome-email code because that path requires `DecisionReached` and is manager-only.

**Alternatives considered**: A distinct `ArcApplicationStatus.Withdrawn` (rejected: diverges from the approved spec wording and spreads status-handling across more 027 read sites). Hard-deleting withdrawn rows (rejected: loses the resident's history and the `ARC-<n>` record). Leaving withdrawn in the default Closed tab (rejected by the clarification).

## R6. Info-request reply

**Decision**: 027's `ArchitecturalInfoRequest` carries the board's `Message` and an unset `RespondedAt`. 029 adds `ResponseMessage` (≤2000 chars, required, non-blank) and `RespondedByUserId`, and a `POST …/info-requests/{id}/reply` endpoint that, in one transaction, stores the reply, links any uploaded attachments (`ArchitecturalAttachment.InfoRequestId`), and sets `RespondedAt`, clearing the "info requested" marker (027's derived `infoRequested` flag flips to false). The reply body and its attachments are visible to board/manager through 027's existing detail endpoint (which already returns `infoRequests[]`; 029's additive fields appear there). The review clock is untouched (FR-019 / 027 FR-021).

**Rationale**: 027 explicitly left `RespondedAt` "set by the submission spec". Reusing the same row and the shared attachment table keeps board and resident views consistent with one source of truth.

**Alternatives considered**: A separate reply table (rejected: a 1:1 reply fits on the request row; attachments already have a home). Multiple back-and-forth rounds in this spec (deferred: the board can open a *new* info request after a reply — 027 allows requests while `Open` — so threading is naturally supported without extra modeling here).

## R7. Submission-confirmation email via the outbox

**Decision**: On submit, enqueue one `OutboxMessage` with `Kind = arc_owner_submitted`, `RecipientUserId = SubmittedByUserId`, `DedupKey = arc:{applicationId}:submitted`, and a plain-text payload (display ID, project title, received/due dates, a link to `/app/property/architectural/{id}`) rendered by a small `ArcSubmissionEmail` helper, written in the same transaction as the submit and dispatched after commit — the exact pattern 027 uses for owner/board emails (R5). Addressing is by `RecipientUserId` (the nullable recipient 027 added), not `OwnerId`.

**Rationale**: The outbox gives atomic enqueue, dedup and terminal-failure handling, and `RecipientUserId` already exists. A confirmation lost on a rolled-back submit (or sent for one that failed) is exactly what the outbox prevents.

**Alternatives considered**: A direct `SesEmailProvider` call in the request (rejected: send/rollback races, per 027 R5). A new email table (rejected: duplicates working machinery).

## R8. Dashboard alert for "more info requested"

**Decision**: Extend `DashboardService.GetDashboardAsync` (already scoped by the active `propertyId` + `communityId`) with a count of the caller's applications that have an outstanding info request (`ArchitecturalInfoRequest.RespondedAt == null`), surfaced in `DashboardResponse` as `architecturalInfoRequested`. The resident dashboard renders it as an alert linking to the request. This is the "dashboard alert" of FR-017; the per-request marker on the detail page is separate.

**Rationale**: The dashboard is the resident's landing surface and already aggregates per-property counts (open violations, new documents), so this is a one-field addition, not a new surface.

**Alternatives considered**: A dedicated notifications center (rejected: out of scope; the dashboard alert is what the spec asks for). A push/email alert on info request (rejected: owned by 027's board flow / the Notification Settings spec).

## R9. Rate limiting

**Decision**: Add a `resident-writes` fixed-window policy in `Program.cs`, partitioned by user ID, default 30/min via a new `RateLimitingOptions.ResidentWritesPermitsPerMinute`, applied to create/update/delete-draft, submit, upload, reply, withdraw and revise. Reads (`my list`, detail, attachment URL) are not write-limited.

**Rationale**: Constitution §7 requires limits on content-creation endpoints; uploads especially. Mirrors 027's `board-writes` so the two ARC slices behave alike.

**Alternatives considered**: Reusing `payments` (rejected: different budget/semantics). No limit (rejected: uploads are abuse-prone).

## R10. Frontend structure

**Decision**: New `features/property/architectural/` standalone components: `my-requests-page` (list + status chips), `request-form` (create/edit draft, attachments, acknowledgement, date validation), `request-detail` (status, timeline, attachments via on-demand links, decision/denial wording/reason, Revise action; **no** votes/comments), `info-reply`, `withdraw-dialog`. One `resident-architectural.service.ts` in `core/services/`. Routes (matching 027's owner link targets): `property/architectural` (list), `property/architectural/:id` (detail), `property/architectural/:id/revise` (revise), plus a create route (`property/architectural/new`). A resident nav entry "Architectural requests".

**Rationale**: Mirrors the 025/027 standalone-component layout and lands exactly on the routes 027's owner emails already link to (`/app/property/architectural/{id}` and `…/revise`), which fall through to the router's `**` fallback until 029 ships.

**Alternatives considered**: An Angular feature module (rejected: the app is standalone-component throughout).

## R11. Testing approach

**Decision**:
- **Backend**: xUnit integration tests in `HOAManagementCompany.Tests/Integration/Property/Architectural/`, Testcontainers PostgreSQL + MinIO, isolated per-test community/property (parallel-safe). One class per user story (create/submit, track, attachments, info-reply, withdraw, revise) plus an authorization class for the non-owner / cross-property 403s. A `[Theory]`-driven `ArcAttachmentValidatorTests` unit suite over each allowed type (valid + spoofed), each disallowed type, and each limit boundary (per-file, count, total). Outbox assertions on `Kind = arc_owner_submitted`, recipient and DedupKey. Sensitive-event assertions via the existing `LogSink`.
- **Frontend**: Karma/Jasmine for the status-projection mapping, the date validator and the service; Angular Testing Library for the list, form, detail and info-reply; Playwright for a real-file upload and the non-owner route refusal; Cypress for sign-in → create → submit → see it in the list; Storybook for the list and detail components.

**Rationale**: Mirrors 025/027 and satisfies the CLAUDE.md rule that every acceptance scenario (including denials) maps to a faithful automated test.

**Alternatives considered**: Mock storage instead of MinIO (rejected: the constitution requires Testcontainers MinIO for the file path, and content sniffing must run against real bytes).
