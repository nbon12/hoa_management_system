# Data Model: Resident Architecture Request

**Feature**: `029-resident-arc-requests` | **Date**: 2026-10-08 (revised after 027 merged) | **Research**: [research.md](./research.md)

029 adds one forward-only EF Core migration, `<timestamp>_AddResidentArcSubmission`, applied idempotently at Cloud Run startup. It adds **two draft tables** and extends the tables `027-board-arc-review` created (migration `20261007011110_AddArchitecturalReview`, merged in #209). It does **not** change the nullability, unique index `(CommunityId, ApplicationNumber, Revision)`, or any existing column of the 027 tables, so every 027 board query, the sweep and the seeder keep working unchanged. Enums are stored as strings, so new values need no DB type change.

> **Why drafts get their own table** (research R3): in the merged code `ArchitecturalApplication.ApplicationNumber`, `ReceivedDate` and `DueDate` are non-nullable and `ArcApplicationFactory` only creates submitted (`Open`) rows. A draft maps 1:1 onto the factory's `ArcNewApplication` input, so submitting a draft is just a call to `CreateFromSettingsAsync` (new request) or `CreateRevisionAsync` (revise and resubmit).

## New tables

### `ArchitecturalApplicationDrafts`

| Field | Type | Rules |
|---|---|---|
| `Id` | `uuid` PK | Used in draft API paths |
| `CommunityId` | `uuid` FK → `Communities` | Must equal `Property.CommunityId` |
| `PropertyId` | `uuid` FK → `Properties` (cascade) | Scope: the caller's active property |
| `CreatedByUserId` | `text` FK → `AspNetUsers` (set null) | |
| `PreviousRevisionId` | `uuid?` FK → `ArchitecturalApplications` (restrict) | Set for a revise-and-resubmit draft; **unique (filtered, non-null)** — one open revision draft per denied application |
| `ProjectType` | `varchar(32)` (`ArcProjectType` string) | Required |
| `ProjectTitle` | `varchar(200)` | May be blank while drafting; required at submit |
| `Description` | `varchar(4000)` | |
| `PlannedStartDate` | `date?` | Required at submit |
| `PlannedCompletionDate` | `date?` | Required at submit; ≥ start |
| `ContractorName` | `varchar(200)?` | Optional |
| `ContractorContact` | `varchar(200)?` | Optional |
| `Acknowledged` | `bool` | Default `false`; must be `true` to submit |
| `RemovedCarriedAttachmentIds` | `uuid[]` | Revision drafts only: previous-revision attachment IDs the resident removed (passed to `CreateRevisionAsync`) |
| `CreatedAt` / `UpdatedAt` | `timestamptz` | |

Index: `(PropertyId)`. Check: `"PlannedCompletionDate" IS NULL OR "PlannedStartDate" IS NULL OR "PlannedCompletionDate" >= "PlannedStartDate"`.

### `ArchitecturalDraftAttachments`

| Field | Type | Rules |
|---|---|---|
| `Id` | `uuid` PK | |
| `DraftId` | `uuid` FK → `ArchitecturalApplicationDrafts` (cascade) | |
| `FileName` | `varchar(255)` | Display only, rendered as text |
| `SizeBytes` | `bigint` | > 0 |
| `ContentType` | `varchar(100)` | The **sniffed** type |
| `StorageKey` | `varchar(500)` | `arc/{communityId}/drafts/{draftId}/{guid}` — kept as-is on submit (the application's attachment row points at the same key) |
| `UploadedByUserId` | `text?` FK → `AspNetUsers` (set null) | |
| `CreatedAt` | `timestamptz` | |

Index: `(DraftId)`.

## Extensions to 027 tables

### `ArchitecturalApplications` (add nullable columns only)

| Field | Type | Rules |
|---|---|---|
| `PlannedStartDate` | `date?` | Copied from the draft at submit; null on 027-seeded rows |
| `PlannedCompletionDate` | `date?` | Same |
| `ContractorName` | `varchar(200)?` | |
| `ContractorContact` | `varchar(200)?` | |
| `AcknowledgedAt` | `timestamptz?` | Submission time when the acknowledgement was accepted |
| `WithdrawnAt` | `timestamptz?` | Set on withdraw |
| `WithdrawnByUserId` | `text?` FK → `AspNetUsers` (set null) | |

`ArcNewApplication` (factory input record) gains optional trailing parameters `PlannedStartDate`, `PlannedCompletionDate`, `ContractorName`, `ContractorContact`, `AcknowledgedAt` (all defaulting to `null`) so 027's existing callers and seeder compile unchanged; `NewRow` copies them.

### `ArchitecturalInfoRequests`

| Field | Type | Rules |
|---|---|---|
| `ResponseMessage` | `varchar(2000)?` | Resident reply; required + non-blank when replying |
| `RespondedByUserId` | `text?` FK → `AspNetUsers` (set null) | |

027's `RespondedAt` is set together with these. `RespondedAt IS NULL` is the "info requested" marker read by the board list (027 `infoRequested`) and the resident dashboard.

### `ArchitecturalAttachments`

| Field | Type | Rules |
|---|---|---|
| `InfoRequestId` | `uuid?` FK → `ArchitecturalInfoRequests` (set null) | Set for attachments added with an info-request reply |
| `UploadedByUserId` | `text?` FK → `AspNetUsers` (set null) | |

### `OutboxMessages`

New `Kind` value `arc_owner_submitted` (`ArcEmailKinds.OwnerSubmitted`, 19 chars, within the 30-char max), addressed by `RecipientUserId` (the submitting resident), DedupKey `arc:{applicationId}:submitted`.

### Enum additions

| Enum (027-owned) | Added value | Meaning |
|---|---|---|
| `ArcOutcome` | `Withdrawn` | Resident-initiated close of an undecided application — not a board decision |

`ArcApplicationStatus` is **unchanged** (Draft lives in its own table).

## Lifecycle

```text
 create draft ──► ArchitecturalApplicationDraft ──submit──► ArcApplicationFactory.CreateFromSettingsAsync ──► Open
   ▲  edit / attach / delete (resident)                     (number from NextApplicationNumber, dates, snapshots)
   │                                                                     │
   │                                                    withdraw (Open, no decision)
   │                                                                     ▼
   │                                                    Closed + Outcome=Withdrawn  (hidden from board default views)
   │
   └── revise (Closed + Denied, no newer revision, no existing revision draft)
          → draft with PreviousRevisionId ──submit──► ArcApplicationFactory.CreateRevisionAsync(prev, input, removedIds) ──► Open (Revision+1)
```

**Resident-facing status projection**

| Resident status | Source |
|---|---|
| Draft | a row in `ArchitecturalApplicationDrafts` |
| Submitted / Under review | `Status = Open` or `DecisionReached`, no outstanding info request |
| More info requested | `Status = Open` with an `ArchitecturalInfoRequest` where `RespondedAt IS NULL` |
| Approved | `Status = Closed`, `DecisionOutcome = Approved` |
| Denied | `Status = Closed`, `DecisionOutcome = Denied` (wording `RevisionsRequested`/`Denied`) |
| Withdrawn | `Status = Closed`, `DecisionOutcome = Withdrawn` |

`DecisionReached` (decided but outcome not yet recorded by the manager) is shown as **Submitted / Under review** — the decision isn't official until the manager records it (027 FR-025).

**Transition rules (029-owned)**
- Submit: all required fields, completion ≥ start, `Acknowledged = true`; on success the draft row and its draft-attachment rows are deleted in the same transaction (objects kept — the new application's attachment rows reference the same keys) and the confirmation outbox row is enqueued.
- Withdraw: only `Status = Open` (409 `APPLICATION_DECIDED` for `DecisionReached`, 409 `APPLICATION_CLOSED` for any `Closed`, including already withdrawn). Runs under 027's `ArcLocks.InLockedTransactionAsync` so it cannot race a deciding vote.
- Revise: only `Closed` + `Denied` with no newer revision and no existing revision draft (409 `REVISION_NOT_ALLOWED`, the factory's existing code).
- Info reply: only while `RespondedAt IS NULL` and the application is `Open` (409 `INFO_ALREADY_ANSWERED`; 409 `APPLICATION_CLOSED`/`APPLICATION_DECIDED` otherwise). Does not change `Status` or `DueDate`.
- Delete draft: deletes draft-attachment objects via `IDocumentStorage.DeleteAsync` (new), then the rows. Carried-over keys belonging to earlier revisions are never deleted.

## Validation summary

| Input | Rule | Error |
|---|---|---|
| Project type | One of the 8 `ArcProjectType` values | 422 `VALIDATION_ERROR` |
| Project title | Required at submit, ≤ 200 | 422 `VALIDATION_ERROR` |
| Description | Required at submit, ≤ 4000 | 422 `VALIDATION_ERROR` |
| Contractor fields | ≤ 200 each | 422 `VALIDATION_ERROR` |
| Planned dates | Both required at submit; completion ≥ start | 422 `VALIDATION_ERROR` |
| Acknowledgement | `true` required at submit | 422 `ACKNOWLEDGEMENT_REQUIRED` |
| Attachment content | Sniffed ∈ {PDF, JPEG, PNG, HEIC} | 422 `UNSUPPORTED_FILE_TYPE` |
| Attachment size | ≤ `MaxFileBytes` (default 50 MB) | 422 `FILE_TOO_LARGE` |
| Attachment count / total | ≤ `MaxFilesPerApplication` (20) and `MaxTotalBytes` (250 MB), counting carried-over files on a revision draft | 422 `ATTACHMENT_LIMIT_REACHED` |
| Reply message | Required, non-blank, ≤ 2000 | 422 `VALIDATION_ERROR` |
| Object storage failure on upload | Upload happens before the metadata row is written | 503 `STORAGE_UNAVAILABLE` (no row; retryable) |
| Anything outside the caller's active property | Same body as 025/027 | 403 `FORBIDDEN` |
