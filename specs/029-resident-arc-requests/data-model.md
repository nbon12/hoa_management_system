# Data Model: Resident Architecture Request

**Feature**: `029-resident-arc-requests` | **Date**: 2026-10-08 | **Research**: [research.md](./research.md)

029 introduces **no new tables**. It adds one forward-only EF Core migration, `<timestamp>_AddResidentArcSubmission`, that extends the tables `027-board-arc-review` creates (see 027 `data-model.md`). The migration applies idempotently at Cloud Run startup. All additions are nullable or defaulted so existing (seeded) rows are unaffected, and 027's board reads keep working. Enums are stored as strings, so new values need no DB type change.

## Enum additions (`HOAManagementCompany/Domain/Enums/`)

| Enum (027-owned) | Added value | Meaning |
|---|---|---|
| `ArcApplicationStatus` | `Draft` | Created but not submitted; resident-owned, invisible to the board |
| `ArcOutcome` | `Withdrawn` | Resident-initiated close of an undecided application (not a board decision) |

## Column additions

### `ArchitecturalApplications` (extend)

| Field | Type | Rules |
|---|---|---|
| `PlannedStartDate` | `date?` | Required at submission; may be null while Draft |
| `PlannedCompletionDate` | `date?` | Required at submission; must be ≥ `PlannedStartDate` |
| `ContractorName` | `varchar(200)?` | Optional |
| `ContractorContact` | `varchar(200)?` | Optional (phone/email, free text) |
| `AcknowledgedNoWorkUntilApproved` | `bool` | Default `false`; must be `true` to submit |
| `WithdrawnAt` | `timestamptz?` | Set on withdraw |
| `WithdrawnByUserId` | `text?` | FK → `AspNetUsers`, set null on delete |
| `ReceivedDate` | `date?` | **Changed to nullable** (027 had non-null); set at submission |
| `DueDate` | `date?` | **Changed to nullable** (027 had non-null); set at submission |

**New / changed checks**
- `("Status" <> 'Draft') OR ("ApplicationNumber" IS NULL AND "ReceivedDate" IS NULL AND "DueDate" IS NULL)` — Draft rows carry no number/dates.
- `("Status" = 'Draft') OR ("ReceivedDate" IS NOT NULL AND "DueDate" IS NOT NULL)` — submitted rows always have dates.
- `("PlannedCompletionDate" IS NULL) OR ("PlannedStartDate" IS NULL) OR ("PlannedCompletionDate" >= "PlannedStartDate")`.
- 027's unique index `(CommunityId, ApplicationNumber, Revision)` is unaffected (Draft rows have a null `ApplicationNumber`, which Postgres treats as distinct — multiple drafts coexist).

**Indexes**: reuse 027's `(PropertyId)` for the "my requests" list (filtered by the caller's active property) and `(CommunityId, Status, DueDate)`. Add a partial index `(PropertyId) WHERE "Status" = 'Draft'` is optional (drafts per property are few; skip unless profiling shows a need).

### `ArchitecturalInfoRequests` (extend)

| Field | Type | Rules |
|---|---|---|
| `ResponseMessage` | `varchar(2000)?` | Resident's reply; required + non-blank when replying |
| `RespondedByUserId` | `text?` | FK → `AspNetUsers`, set null on delete |

027 already defines `RespondedAt timestamptz?`; 029 sets it (with `ResponseMessage` and `RespondedByUserId`) atomically on reply. `RespondedAt IS NULL` is the "info requested" marker both the board (027 derived `infoRequested`) and the resident dashboard read.

### `ArchitecturalAttachments` (extend)

| Field | Type | Rules |
|---|---|---|
| `InfoRequestId` | `uuid?` | FK → `ArchitecturalInfoRequests` (cascade with the application). Null for application attachments; set for reply attachments |
| `UploadedByUserId` | `text?` | FK → `AspNetUsers`, set null on delete |

027's `StorageKey`, `FileName`, `SizeBytes`, `ContentType`, `CreatedAt`, `ApplicationId` are reused. Draft attachments use key prefix `arc/{communityId}/draft/{applicationId}/{guid}`; submitted ones use 027's `arc/{communityId}/{applicationNumber}/{guid}`.

### `OutboxMessages` (extend — already modified by 027)

| Change | Detail |
|---|---|
| `Kind` | New value `arc_owner_submitted` (18 chars; within the existing 30-char max). Routes to the `email` channel. Addressed by `RecipientUserId` (027's nullable recipient). DedupKey `arc:{applicationId}:submitted`. |

## Lifecycle (resident view over 027's `Status`)

```text
          create                 submit (ArcApplicationFactory: number, dates, snapshots)
  (none) ───────► Draft ──────────────────────────────────────────────► Open ──► (027 board review) ──► DecisionReached ──► Closed
                   │  edit/delete (resident)                              │                                                  │ (Approved / Denied)
                   └──────────────┘                                      │ withdraw                                         │
                                                                         ▼                                                  ▼
                                                                 Closed + Outcome=Withdrawn            Closed + Denied ──► revise & resubmit (029)
                                                                 (hidden from board default;            creates a new Draft revision
                                                                  includeWithdrawn filter)              (Revision+1, PreviousRevisionId → old)
```

**Resident-facing status projection** (unchanged from spec):

| Resident status | Underlying state |
|---|---|
| Draft | `Status = Draft` |
| Submitted / Under review | `Status = Open` |
| More info requested | `Status = Open` + an `ArchitecturalInfoRequest` with `RespondedAt = null` |
| Approved | `Status = Closed`, `DecisionOutcome = Approved` |
| Denied | `Status = Closed`, `DecisionOutcome = Denied` (wording `RevisionsRequested`/`Denied`) |
| Withdrawn | `Status = Closed`, `DecisionOutcome = Withdrawn` |

**Transition rules (029-owned)**
- `(none) → Draft`: resident create for a property their active `propertyId` claim matches.
- `Draft → Draft`: edit (any field) or add/remove attachments; delete removes the row and its objects (no other row references the key).
- `Draft → Open`: submit — requires all required fields, `PlannedCompletionDate ≥ PlannedStartDate`, and `AcknowledgedNoWorkUntilApproved = true`; allocates number/dates/snapshots via `ArcApplicationFactory`; sets `SubmittedByUserId` + `OwnerName`; enqueues the confirmation outbox row.
- `Open → Closed (Withdrawn)`: resident withdraw; allowed only while `Open` with no decision reached; refused otherwise (`APPLICATION_DECIDED` / `ALREADY_WITHDRAWN`).
- `Closed (Denied) → new Draft (Revision+1)`: revise-and-resubmit via 027's revision factory; refused on any non-Closed/Denied state (`NOT_DENIED`). The new revision follows the `Draft → Open` path.
- Info reply: allowed only while an `ArchitecturalInfoRequest` has `RespondedAt = null` (`INFO_ALREADY_ANSWERED` otherwise); does not change `Status` or `DueDate`.

## Validation summary

| Input | Rule | Error |
|---|---|---|
| Project type | One of the 8 `ArcProjectType` values | 422 `VALIDATION_ERROR` |
| Project title | Required, ≤ 200 | 422 `VALIDATION_ERROR` |
| Description | ≤ 4000 | 422 `VALIDATION_ERROR` |
| Planned dates | Both required at submit; completion ≥ start | 422 `VALIDATION_ERROR` |
| Acknowledgement | `true` required at submit | 422 `VALIDATION_ERROR` (submit refused) |
| Attachment type | Content sniff ∈ {PDF, JPEG, PNG, HEIC}; declared type must agree | 422 `VALIDATION_ERROR` |
| Attachment size | ≤ per-file limit (default 50 MB) | 422 `VALIDATION_ERROR` |
| Attachment count / total | ≤ per-application count (20) and total (250 MB) | 422 `VALIDATION_ERROR` |
| Edit / delete a non-Draft | | 409 `NOT_DRAFT` |
| Withdraw a non-Open / decided | | 409 `APPLICATION_DECIDED` or `ALREADY_WITHDRAWN` |
| Revise a non-Denied | | 409 `NOT_DENIED` |
| Reply to an answered / absent info request | | 409 `INFO_ALREADY_ANSWERED` / 404 |
| Any request outside the caller's active property | Same body as 025/027 | 403 `FORBIDDEN` |
