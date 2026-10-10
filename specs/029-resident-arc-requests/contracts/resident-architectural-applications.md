# Contract: Resident Architectural Applications

All routes carry the global `api/v1` prefix and live under `/property/architectural-applications` next to the existing resident `/property/*` endpoints. Every response sets `Cache-Control: no-store`. Errors use the existing `{ code, message }` shape.

**Authorization (all endpoints)**: an authenticated resident. The active property comes from the `propertyId` JWT claim (`User.RequirePropertyId()`), and every query and command is pinned to `PropertyId == propertyId`. A draft or application on any other property gets `403 { code: "FORBIDDEN", message: "You do not have access to this request." }`, the same body whether it exists or not. No board capability is consulted, and a board or manager membership never widens access (025 FR-015).

**Rate limiting**: every write (draft create/update/delete, upload, delete-attachment, submit, reply, withdraw, revise) carries `RequireRateLimiting("resident-writes")`. Reads don't.

**Residents never receive board votes, voter identities or board vote comments** (FR-016). Resident DTOs have no such fields at all.

Submit, withdraw and revise are bodiless `POST`s (no request body or content type needed).

Two resources: **drafts** (`ArchitecturalApplicationDrafts`, editable, invisible to the board) and **applications** (027's `ArchitecturalApplications`, created by submitting a draft).

---

## Drafts (US1, US5, US6)

### POST /property/architectural-applications/drafts

- **Request**: `{ "projectType": "Fence", "projectTitle": "…", "description": "…", "plannedStartDate": "2026-07-01"?, "plannedCompletionDate": "2026-07-20"?, "contractorName": "…"?, "contractorContact": "…"?, "acknowledged": false }`. Only `projectType` is required while drafting.
- **Response 201**: the draft `{ id, projectType, projectTitle, description, plannedStartDate, plannedCompletionDate, contractorName, contractorContact, acknowledged, previousRevisionId: null, attachments: [], carriedAttachments: [], updatedAt }`.
- **Errors**: 422 `VALIDATION_ERROR` (unknown project type, over-length text, completion before start).

### GET /property/architectural-applications/drafts/{draftId}

- **Response 200**: the draft. `attachments[]` lists its own uploads. On a revision draft, `carriedAttachments[]` lists the previous revision's attachments minus `removedCarriedAttachmentIds`.
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`.

### PUT /property/architectural-applications/drafts/{draftId}

- **Request**: the create body, plus `removedCarriedAttachmentIds: [guid]` for revision drafts. Replaces the editable fields.
- **Response 200**: the updated draft.
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`; 422 `VALIDATION_ERROR`.

### DELETE /property/architectural-applications/drafts/{draftId}

Deletes the draft, its draft-attachment rows and their objects (`IDocumentStorage.DeleteAsync`). Objects carried over from earlier revisions are never deleted.

- **Response 204**. **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`.

### POST /property/architectural-applications/drafts/{draftId}/attachments

Upload one file (`multipart/form-data`, field `file`).

- **Behavior**:
  1. Sniff the content (PDF, JPEG, PNG or HEIC).
  2. Enforce the `ArcUploadOptions` limits. On a revision draft, the count and total include carried-over files.
  3. Upload to `arc/{communityId}/drafts/{draftId}/{guid}` and write an `ArchitecturalDraftAttachment` row with the sniffed content type.
- **Response 201**: `{ id, fileName, sizeBytes, contentType }`.
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`; 422 `UNSUPPORTED_FILE_TYPE`, `FILE_TOO_LARGE`, `ATTACHMENT_LIMIT_REACHED`; 503 `STORAGE_UNAVAILABLE` when object storage fails (no row is written, so the upload can simply be retried).

### DELETE /property/architectural-applications/drafts/{draftId}/attachments/{attachmentId}

Removes one uploaded draft attachment and its object. To drop a carried-over attachment, PUT `removedCarriedAttachmentIds` instead.

- **Response 204**. **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`.

### GET /property/architectural-applications/drafts/{draftId}/attachments/{attachmentId}/url

Short-lived link to a draft attachment or a carried-over attachment. Same response, errors and audit event as the application attachment link below.

### POST /property/architectural-applications/drafts/{draftId}/submit

- **Behavior**: validates the required fields, `plannedCompletionDate ≥ plannedStartDate` and `acknowledged = true`, then does the following in one transaction:
  1. Calls `ArcApplicationFactory.CreateFromSettingsAsync`, or `CreateRevisionAsync(previousRevisionId, input, removedCarriedAttachmentIds)` for a revision draft. The input carries:
     - the draft's fields;
     - `OwnerName`, a snapshot of the property owner's name;
     - `SubmittedByUserId` = the caller;
     - `ReceivedDate` = today in the community's ARC time zone;
     - `AcknowledgedAt` = now;
     - the draft attachments, as `ArcNewAttachment`s on the same storage keys.
  2. Deletes the draft rows. The objects are kept.
  3. Enqueues the `arc_owner_submitted` outbox row.

  It dispatches the email after commit.
- **Response 201**: the new application's detail, with `displayId` (for example `"ARC-1042"`), `revision`, `receivedDate`, `dueDate` and `status: "Submitted"`.
- **Errors**:

  | Status | Code | When |
  |---|---|---|
  | 403 | `FORBIDDEN` | Not the caller's property |
  | 404 | `NOT_FOUND` | No such draft |
  | 409 | `REVISION_NOT_ALLOWED` | The previous revision can no longer be revised (factory) |
  | 422 | `VALIDATION_ERROR` | A required field is missing, or the dates are invalid |
  | 422 | `ACKNOWLEDGEMENT_REQUIRED` | The acknowledgement isn't accepted |

---

## Applications (US2, US3, US4, US6)

### GET /property/architectural-applications

"My architectural requests": the active property's drafts and applications.

- **Query**: `limit` (default 25, clamped to 100), `offset` (default 0).
- **Response 200**:

```json
{
  "items": [{
    "kind": "Application",
    "id": "guid",
    "displayId": "ARC-1042",
    "revision": 1,
    "projectType": "Fence",
    "projectTitle": "Fence replacement — 6ft cedar",
    "status": "MoreInfoRequested",
    "attachmentCount": 3,
    "receivedDate": "2026-05-28",
    "dueDate": "2026-06-27",
    "updatedAt": "2026-06-02T14:03:00Z"
  }],
  "total": 6, "limit": 25, "offset": 0
}
```

- `kind` is `Draft` or `Application`. Drafts have a null `displayId`, `receivedDate` and `dueDate`, and `status: "Draft"`.
- `status` is the resident projection: `Draft`, `Submitted`, `MoreInfoRequested`, `Approved`, `Denied` or `Withdrawn`.
- Drafts are listed first, then applications by received date, newest first. When a revision exists, only the latest revision of each `ApplicationNumber` is listed.

### GET /property/architectural-applications/{id}

Resident-safe detail.

- **Response 200**: the list item, plus:

```json
{
  "description": "…",
  "plannedStartDate": "2026-07-01",
  "plannedCompletionDate": "2026-07-20",
  "contractorName": "…",
  "contractorContact": "…",
  "acknowledgedAt": "…",
  "attachments": [{ "id": "guid", "fileName": "fence-plan.pdf", "sizeBytes": 1258291, "contentType": "application/pdf", "infoRequestId": null }],
  "infoRequests": [{ "id": "guid", "message": "Please attach a plat survey", "requestedAt": "…", "responseMessage": null, "respondedAt": null }],
  "timeline": [{ "event": "Submitted", "at": "…" }, { "event": "InfoRequested", "at": "…" }],
  "decision": { "outcome": "Denied", "wording": "RevisionsRequested" },
  "ownerReason": "Lower the fence to 5ft per Guideline 4.2",
  "formalDisapprovalStatement": "This is a formal disapproval …",
  "conditionsOfApproval": null,
  "closedAt": "…",
  "canWithdraw": false,
  "canRevise": true,
  "revisions": [{ "id": "guid", "revision": 1, "status": "Denied", "receivedDate": "…" }]
}
```

- `infoRequests[]` carries the board's question and the resident's reply. It never names the board member and never includes votes or vote comments.
- `timeline` events, in time order, are `Submitted`, `InfoRequested`, `InfoReplied`, `Approved`, `Denied` and `Withdrawn`. They are derived from `ReceivedDate`/`CreatedAt`, the info-request timestamps, `ClosedAt` and `WithdrawnAt`.
- `decision` is set only when `Status = Closed` with `Approved` or `Denied`. A `DecisionReached` application shows no decision until the manager records it.
- `ownerReason` and `formalDisapprovalStatement` appear only when the request is Denied. `conditionsOfApproval` appears only when it is Approved.
- `canWithdraw` and `canRevise` mirror the server rules so the UI can hide the actions.
- `revisions[]` lists every revision of the same `ApplicationNumber`, oldest first.
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`.

### GET /property/architectural-applications/{id}/attachments/{attachmentId}/url

- **Behavior**: checks the property scope, emits the sensitive event `ArcResidentAttachmentAccess` (actor, property, resource `attachment:{id}`, UTC), and returns `IDocumentStorage.GetPreSignedUrlAsync`.
- **Response 200**: `{ "url": "https://…", "expiresAt": "…" }`. The link expires after 5 minutes, within the 15-minute cap.
- **Errors**:
  - 403 `FORBIDDEN`, including when the attachment is on a different application.
  - 404 `ATTACHMENT_UNAVAILABLE` when the object is missing.

### POST /property/architectural-applications/{id}/info-requests/{infoRequestId}/attachments

Upload a reply attachment (`multipart/form-data`, field `file`) while the info request is unanswered. It goes through the same sniffing and limits as a draft upload, counted against the application's existing attachments. The file is stored under `arc/{communityId}/{applicationNumber}/{guid}` as an `ArchitecturalAttachment` with `InfoRequestId` set.

- **Response 201**: `{ id, fileName, sizeBytes, contentType }`.
- **Errors**:
  - 403 `FORBIDDEN`; 404 `NOT_FOUND`.
  - 409 `INFO_ALREADY_ANSWERED` or `APPLICATION_CLOSED`.
  - 422 `UNSUPPORTED_FILE_TYPE`, `FILE_TOO_LARGE` or `ATTACHMENT_LIMIT_REACHED`.
  - 503 `STORAGE_UNAVAILABLE` when object storage fails; nothing is written.

### POST /property/architectural-applications/{id}/info-requests/{infoRequestId}/reply

- **Request**: `{ "responseMessage": "required, non-blank, ≤ 2000" }`.
- **Behavior**: under `ArcLocks.InLockedTransactionAsync`, sets `ResponseMessage`, `RespondedByUserId` and `RespondedAt`, which clears the marker. It doesn't change `Status` or `DueDate`. Logs `ArcInfoReplied`.
- **Response 200**: the updated detail.
- **Errors**:
  - 403 `FORBIDDEN`; 404 `NOT_FOUND`.
  - 409 `INFO_ALREADY_ANSWERED`, `APPLICATION_DECIDED` or `APPLICATION_CLOSED`.
  - 422 `VALIDATION_ERROR`.

### POST /property/architectural-applications/{id}/withdraw

- **Behavior**: under `ArcLocks.InLockedTransactionAsync`, allowed only when `Status = Open`. Sets:
  - `Status = Closed` and `DecisionOutcome = Withdrawn`;
  - `WithdrawnAt` and `ClosedAt` to now;
  - `WithdrawnByUserId` and `ClosedByUserId` to the caller.

  It sends no email and logs `ArcWithdrawn`.
- **Response 200**: the updated detail, with `status: "Withdrawn"`.
- **Errors**:
  - 403 `FORBIDDEN`; 404 `NOT_FOUND`.
  - 409 `APPLICATION_DECIDED` (`DecisionReached`).
  - 409 `APPLICATION_CLOSED` (already Approved, Denied or Withdrawn).

### POST /property/architectural-applications/{id}/revise

- **Behavior**: allowed only on the **latest** revision when it is `Closed` + `Denied` and has no revision draft yet. Creates an `ArchitecturalApplicationDraft` with `PreviousRevisionId = {id}`, pre-filled from that revision. Its attachments appear as `carriedAttachments`. The resident edits it, then submits it through the draft submit endpoint, which calls `CreateRevisionAsync`.
- **Response 201**: the new draft.
- **Errors**:
  - 403 `FORBIDDEN`; 404 `NOT_FOUND`.
  - 409 `REVISION_NOT_ALLOWED`: not Closed + Denied, a newer revision exists, or a revision draft already exists.

---

## Board list change (027-owned, additive)

`GET /communities/{communityId}/architectural-applications` gains an optional query parameter:

| Param | Values | Default | Effect |
|---|---|---|---|
| `includeWithdrawn` | `true` or `false` | `false` | When false, the `closed` tab and `counts.closed` exclude `DecisionOutcome = Withdrawn`. When true, they include it. |

The change is backward-compatible. A withdrawn application's board list item and detail show `decision: { outcome: "Withdrawn", wording: null, source: null }`, and the board UI labels it "Withdrawn". The board Applications page gets a **Show withdrawn** toggle on the Closed tab.

## Dashboard change (additive)

`GET /dashboard` gains `architecturalInfoRequested: { count, applicationId }`. `count` is the number of the active property's `Open` applications with an unanswered info request. `applicationId` is the oldest such application, used for the alert link. The resident dashboard shows an alert when `count > 0`.

## Confirmation email (outbox payload)

| Kind | To | Subject | Body fields |
|---|---|---|---|
| `arc_owner_submitted` | `RecipientUserId` = the submitting resident (`AspNetUsers.Email`) | "We received your architectural request ARC-1042" | community name, display ID (and `v{n}` for revisions), project title, property address, received date, due date, a "work may not begin until approved" reminder, the request link `/app/property/architectural/{id}` |

The body is plain text, rendered by `ArcEmailRenderer.OwnerSubmitted`, and never includes board data. The DedupKey is `arc:{applicationId}:submitted`.
