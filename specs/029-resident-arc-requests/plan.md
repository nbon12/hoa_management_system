# Implementation Plan: Resident Architecture Request

**Branch**: `029-resident-arc-requests` | **Date**: 2026-10-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/029-resident-arc-requests/spec.md`

## Summary

Let homeowners file and track architectural (ARC) requests, extending the shared data model introduced by `027-board-arc-review` (PR #209, lands first). Residents create and edit drafts, submit them (number/received/due dates assigned, confirmation email sent), track status on a "My architectural requests" list and detail page, reply to board "Request info" questions, withdraw undecided requests, and revise-and-resubmit after a denial. All endpoints are **resident property-scoped** through the existing active-property `propertyId` JWT claim — no board resolver, no board capabilities. Attachments are validated by content, size- and count-limited at the environment level, stored privately via `IDocumentStorage`, and served only through ≤15-minute pre-signed links.

The feature reuses 027's `ArchitecturalApplication` / `ArchitecturalAttachment` / `ArchitecturalInfoRequest` tables, its `ArcApplicationFactory` (number allocation, due-date and rule snapshots, revision factory), and the `OutboxMessage` transactional-email pipeline. It extends them **additively, never loosening 027's columns or indexes**: a separate drafts table that feeds 027's factory on submit, a `Withdrawn` outcome, resident-authored fields (planned dates, contractor, acknowledgement), the info-request reply (body + attachments), the `arc_owner_submitted` email kind, and a board "show withdrawn" list filter.

## Technical Context

**Language/Version**: C# / .NET 9.0 (backend `HOAManagementCompany`, tests `HOAManagementCompany.Tests`); TypeScript / Angular 17.3 (frontend `neko-hoa`)

**Primary Dependencies**: All existing — FastEndpoints, EF Core 9 (Npgsql), ASP.NET Core Identity/JWT, `IDocumentStorage` (R2/MinIO), `OutboxMessage` + `OutboxDispatcher` + `SesEmailProvider`, `Microsoft.AspNetCore.RateLimiting`, Serilog, FluentValidation via `AddValidatedOptions`; Angular standalone components + signals, existing `AuthService` active-property claim. From 027 (shared, lands first): `ArchitecturalApplication` et al., `ArcApplicationFactory`, `Arc*` enums. **No new packages.**

**Storage**: PostgreSQL (Neon prod; Testcontainers CI/local). One additive forward-only migration `<ts>_AddResidentArcSubmission`:
- New tables: `ArchitecturalApplicationDrafts`, `ArchitecturalDraftAttachments` (drafts never touch 027's tables — research R3).
- `ArchitecturalApplications` (027): + nullable `PlannedStartDate`, `PlannedCompletionDate`, `ContractorName`, `ContractorContact`, `AcknowledgedAt`, `WithdrawnAt`, `WithdrawnByUserId`. No nullability or index changes.
- `ArchitecturalInfoRequests` (027): + `ResponseMessage`, `RespondedByUserId` (027 already has `RespondedAt`).
- `ArchitecturalAttachments` (027): + `InfoRequestId?`, `UploadedByUserId?`.
- Enum string value `ArcOutcome.Withdrawn`; outbox kind `arc_owner_submitted`.
- Objects: drafts under `arc/{communityId}/drafts/{draftId}/{guid}` (kept after submit), reply attachments under 027's `arc/{communityId}/{applicationNumber}/{guid}`.

**Testing**: Backend xUnit + Testcontainers (PostgreSQL + MinIO), one integration class per user story, `[Theory]` over attachment-type/size/count boundaries, content-sniffing unit tests, Serilog `LogSink` for sensitive events, outbox assertions on the `arc_owner_submitted` kind/recipient. Frontend Karma/Jasmine, Angular Testing Library, Playwright (real upload + non-owner refusal), Cypress (submit→track journey), Storybook.

**Target Platform**: Cloud Run (backend, scale-to-zero), Cloudflare Pages (frontend). No scheduler/job work in this spec (the sweep belongs to 027).

**Project Type**: Web application (existing Angular + .NET layout).

**Performance Goals** (from spec): SC-001 submit a complete request in <5 min; SC-007 request appears in the tracking list within 5 s of submission. The "my requests" query is index-backed on `(PropertyId)` (027's existing index) filtered to the caller's active property.

**Constraints**:
- All authorization is resident property-scope: `application.PropertyId == User.RequirePropertyId()` (025 FR-015). A board/manager role never widens this.
- Attachments validated by content (magic bytes), limits enforced before any bytes persist; env-level `ArcUploadOptions` (50 MB/file, 20 files, 250 MB total).
- No durable public object URLs; links expire ≤15 min (`IDocumentStorage` gives 5 min).
- `ARC-<number>` allocated atomically from 027's `CommunityArcSettings.NextApplicationNumber` at submission.
- Submission-confirmation and all state changes enqueue outbox rows in the same transaction (exactly-once via DedupKey), dispatched after commit.
- Withdrawn requests excluded from the board's default Closed view; surfaced only via an opt-in `includeWithdrawn` filter.
- Resident pages render correctly at phone (375 px), tablet (768 px) and desktop (1280 px) widths (constitution §6, spec FR-027).

**Scale/Scope**: Hundreds of applications per community per year; a resident typically has a handful. 15 resident endpoints (8 draft, 7 application); 2 new tables + 1 additive migration; 1 new backend subfolder (`Features/Property/Architectural/`); 1 new frontend feature folder (`features/property/architectural/`), 1 service, 3 routes, 1 nav entry; a dashboard-alert addition; small additive edits to four 027-owned files (list query, `IDocumentStorage`, `OutboxMessage` kind, email renderer).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design below.*

- **Technology fit**: ✅ Angular, FastEndpoints, PostgreSQL/Neon, Identity + JWT, Cloudflare, Cloud Run, Sentry, Swashbuckle (dev only), GitHub Actions — all reused. No new packages, no new infra.
- **HOA tenancy**: ✅ Every request is community-scoped through its property (`ArchitecturalApplication.CommunityId` denormalized = `Property.CommunityId`, per 027). Resident endpoints additionally pin to the caller's **active property** (`propertyId` claim); cross-property / cross-community access is denied by default with the 025 non-disclosing 403.
- **API contracts**: ✅ The contract documents auth, property-scope, `limit`/`offset` (25/100), error codes (`FORBIDDEN`, `NOT_FOUND`, `VALIDATION_ERROR`, `ACKNOWLEDGEMENT_REQUIRED`, `UNSUPPORTED_FILE_TYPE`, `FILE_TOO_LARGE`, `ATTACHMENT_LIMIT_REACHED`, `APPLICATION_DECIDED`, `APPLICATION_CLOSED`, `REVISION_NOT_ALLOWED`, `INFO_ALREADY_ANSWERED`, `ATTACHMENT_UNAVAILABLE`), `no-store`, and the confirmation-email payload. No existing endpoint changes shape; the board list gains an **optional** `includeWithdrawn` param (backward-compatible).
- **Security and operations**: ✅ No new secrets. Authorization is server-side from the property claim. Uploads validate content type by bytes and enforce env-level size/count limits before persisting. A `resident-writes` rate limit covers create/submit/upload/reply/withdraw/revise. Non-owner access and rejected uploads are logged as 025 FR-017 sensitive events (IDs only, never file bytes or owner PII). Production error shape unchanged (`DomainException`).
- **File storage**: ✅ Objects in R2 (hosted) / MinIO (local/CI); PostgreSQL holds metadata + keys only; access only via `IDocumentStorage` pre-signed URLs. This spec is the first to **delete** objects (draft discard), so `IDocumentStorage` gains an additive `DeleteAsync`.
- **Caching/edge**: ✅ Every endpoint is `no-store` (authenticated, user-specific).
- **Testing discipline**: ✅ Test-first per user story; Testcontainers PostgreSQL + MinIO with isolated per-test communities/properties (parallel-safe); `[Theory]` over attachment types and limit boundaries; content-sniffing unit tests; sensitive-event assertions via the existing `LogSink`.
- **CI/CD and documentation**: ✅ Sonar + Codecov (95% on new files); Repowise markers below; no IaC change (no Trivy delta).
- **Executable & living specs**: ✅ Spec clarified 2026-10-08 (three answers). Every US1–US6 acceptance scenario — including the denial scenarios — maps to a planned test; task generation must cite each. This spec extends 027's shared entities without contradicting 025/027.
- **Spec independence & parallelism**: ⚠️ Documented soft ordering. Hard dependency on **025** (merged). Shares the ARC data model with **027**, which lands first and introduces the base tables/factory; 029 extends them additively. 029's own tests exercise create/submit/track/withdraw against its own writes (it does not need 027's board UI). See Complexity Tracking for the four 027-owned files 029 touches and why. No gate failure — the shared-model split is exactly the sanctioned pattern (027 plan §Spec independence).

No gate failures.

## Project Structure

### Documentation (this feature)

```text
specs/029-resident-arc-requests/
├── spec.md
├── plan.md                 # this file
├── research.md             # Phase 0
├── data-model.md           # Phase 1
├── quickstart.md           # Phase 1
├── contracts/
│   └── resident-architectural-applications.md
├── checklists/requirements.md
└── tasks.md                # /speckit.tasks (not created here)
```

### Source Code (repository root)

```text
HOAManagementCompany/
├── Domain/
│   ├── Entities/
│   │   ├── ArchitecturalApplication.cs        # 027-owned; + nullable resident fields, withdrawal stamps
│   │   ├── ArchitecturalApplicationDraft.cs   # new
│   │   ├── ArchitecturalDraftAttachment.cs    # new
│   │   ├── ArchitecturalAttachment.cs         # 027-owned; + InfoRequestId, UploadedByUserId
│   │   ├── ArchitecturalInfoRequest.cs        # 027-owned; + ResponseMessage, RespondedByUserId
│   │   └── Enums/ArcOutcome.cs                # 027-owned; + Withdrawn
│   └── Entities/OutboxMessage.cs              # 027-owned; + arc_owner_submitted kind
├── Features/Property/Architectural/           # new (resident ARC slice)
│   ├── ResidentArcDraftService.cs             # draft create/get/update/delete (+ revision drafts)
│   ├── ResidentArcSubmitService.cs            # draft → ArcApplicationFactory, outbox confirmation
│   ├── ResidentArcQueries.cs                  # my-list + resident-safe detail (no votes/comments)
│   ├── ResidentArcScope.cs                    # active-property scoping + non-disclosing 403
│   ├── ResidentArcLog.cs                      # sensitive events (IDs only)
│   ├── ArcAttachmentValidator.cs              # content sniffing + env-level limits
│   ├── ResidentArcActionsService.cs           # withdraw, info reply (+ reply files), attachment keys
│   ├── ResidentArcHttp.cs                     # no-store, multipart file reading, short-lived links
│   ├── DraftEndpoints.cs                      # Create/Get/Update/Delete/Submit draft endpoints
│   ├── DraftAttachmentEndpoints.cs            # Upload/Delete draft file, draft file link
│   ├── ApplicationEndpoints.cs                # List, Detail, file link, reply file, Reply, Withdraw, Revise
│   └── ResidentArcModels.cs                   # DTOs + ResidentArcErrorCodes (no vote fields)
├── Features/Board/Architectural/
│   ├── ApplicationsListEndpoint.cs            # 027-owned; + includeWithdrawn (default excludes Withdrawn)
│   ├── ArcQueries.cs / ArcModels.cs           # 027-owned; Withdrawn decision mapping, info-reply fields in board detail
│   ├── ArcEmailRenderer.cs                    # 027-owned; + OwnerSubmitted / arc_owner_submitted
│   └── ArcApplicationFactory.cs               # 027-owned; reused (number, due date, snapshots, revision)
├── Features/Dashboard/DashboardService.cs     # + architecturalInfoRequested alert count
├── Infrastructure/Storage/IDocumentStorage.cs # 027-owned; + DeleteAsync
├── Infrastructure/Storage/S3DocumentStorage.cs# + DeleteAsync impl
├── Infrastructure/Configuration/ArcUploadOptions.cs  # new (env-level limits) + validator
├── Infrastructure/Persistence/
│   ├── ApplicationDbContext.cs                # updated entity configuration (new columns/nullability)
│   └── Migrations/<ts>_AddResidentArcSubmission.cs   # new additive migration
└── Program.cs                                 # resident-writes rate policy, ArcUploadOptions, DI

HOAManagementCompany.Tests/
├── Unit/Architectural/                                      # ArcAttachmentValidatorTests, ArcUploadOptionsValidatorTests,
│                                                            # ArcEmailRendererSubmittedTests
├── Integration/Dashboard/DashboardArchitecturalAlertTests.cs
├── Integration/RateLimiting/ResidentWritesRateLimitTests.cs
└── Integration/Property/Architectural/                      # ResidentArcTestBase + one class per user story:
    ├── FileRequestTests.cs  TrackRequestsTests.cs  AttachmentTests.cs  AttachmentStorageFailureTests.cs
    ├── InfoReplyTests.cs    WithdrawTests.cs       ReviseTests.cs
    ├── ResidentArcAuthorizationTests.cs                     # every route's non-disclosing 403, co-owner, board role
    └── ResidentArcMigrationTests.cs  ResidentArcTelemetryHygieneTests.cs  ResidentArcScopeStaticAnalysisTests.cs

neko-hoa/src/app/
├── app.routes.ts                              # + property/architectural (list), /:id (detail), /:id/revise
├── core/services/resident-architectural.service.ts         # new
└── features/property/architectural/           # new
    ├── my-requests-page.component.ts (+ .stories)
    ├── request-form.component.ts              # create/edit draft + attachments + acknowledgement
    ├── request-detail.component.ts (+ .stories)  # status, timeline, attachments, decision; no votes
    ├── info-reply.component.ts
    └── withdraw-dialog.component.ts
```

**Structure Decision**: Web application, following the 025/027 layout. The resident ARC slice lives under `Features/Property/Architectural/` (alongside the existing resident `Features/Property/` endpoints that already scope by the `propertyId` claim) and `features/property/architectural/` on the frontend. No new project or top-level directory. The four 027-owned files 029 edits are additive extensions (see Complexity Tracking).

## Repowise Documentation

**Status**: In progress — marker regions added in the files below; regenerated by the `repowise-gate` CI job.

### Configuration

- Marker instructions: [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md)
- PR health thresholds: [`repowise/health-gates.yaml`](../../repowise/health-gates.yaml)

### Marker regions (this feature)

| File | Region ID | Purpose |
|------|-----------|---------|
| `Features/Property/Architectural/ResidentArcDraftService.cs`, `ResidentArcSubmitService.cs` | `domain=resident-arc` | Draft lifecycle, revision drafts, submit via 027's factory |
| `Features/Property/Architectural/ArcAttachmentValidator.cs` | `domain=resident-arc-uploads` | Content sniffing and env-level limit enforcement |
| `Features/Property/Architectural/ResidentArcQueries.cs` | `domain=resident-arc` | Resident-safe projection (status map, no votes/comments) |
| `Infrastructure/Configuration/ArcUploadOptions.cs` | `domain=configuration` | Environment-level attachment limits |

### Marker syntax

```csharp
// <!-- REPOWISE:START domain=resident-arc -->
// ... generated content ...
// <!-- REPOWISE:END -->
```

### CI (pull requests to `main`)

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | `repowise init/update --index-only`, `status`, `health`, `risk`, marker validation |

## Complexity Tracking

No constitution violations. Four additive touches to 027-owned code and one new storage capability are recorded for reviewers:

| Choice | Why needed | Simpler alternative rejected because |
|--------|------------|--------------------------------------|
| Extend 027's `ArchitecturalApplication`/`InfoRequest`/`Attachment` + enums rather than new tables | The spec mandates one shared model with 027 (issue + 027 plan §independence) | Parallel resident tables would fork the entity the board reads and break the single `ARC-<n>`/revision lineage |
| Separate `ArchitecturalApplicationDrafts` table instead of a `Draft` status | Merged 027 code treats number/dates as non-null everywhere and its factory only creates `Open` rows | A `Draft` status would loosen 027's columns and unique index and force every board query, the sweep and the seeder to exclude drafts |
| Represent withdraw as `Closed` + `ArcOutcome.Withdrawn` and filter it out of the board's default Closed list (`includeWithdrawn` opt-in) | Clarification 2026-10-08: keep the record but hide it from the board by default | A standalone `Withdrawn` status would touch more 027 read paths; hard-deleting withdrawn rows loses the resident's history |
| Add `IDocumentStorage.DeleteAsync` | Deleting a draft must not orphan its uploaded objects (FR-014); only draft-owned keys are ever deleted | 027 deletes nothing; leaving objects behind violates FR-014 |
| Add `arc_owner_submitted` outbox kind + `resident-writes` rate policy | Confirmation email reuses the exactly-once outbox; resident writes need a limit like `board-writes` | A direct SES call could send on a rolled-back submission; reusing `payments` conflates budgets |
