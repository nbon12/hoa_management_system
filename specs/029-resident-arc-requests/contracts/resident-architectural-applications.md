# Contract: Resident Architectural Applications

All routes carry the global `api/v1` prefix and live under `/property/architectural-applications` (alongside the existing resident `/property/*` endpoints). Every response sets `Cache-Control: no-store` (authenticated, property-specific). Errors use the existing shape `{ code, message }`.

**Authorization** (all endpoints): authenticated resident. The active-property GUID comes from the `propertyId` JWT claim (`User.RequirePropertyId()`); every query/command is pinned to `application.PropertyId == propertyId`. A request for an application whose property is not the caller's active property returns the 025 non-disclosing `403 { code: "FORBIDDEN", message: "You do not have access to this property." }` — the same whether the application exists or not. No board capability is consulted; a board/manager role does not widen access (025 FR-015).

**Rate limiting**: all write endpoints (create, update, delete, submit, upload, delete-attachment, reply, withdraw, revise) carry `RequireRateLimiting("resident-writes")` (R9). Reads are not write-limited.

Residents never receive board votes, voter identities, or board vote comments in any response (FR-016).

---

## POST /property/architectural-applications

Create a draft (US1).

- **Request**: `{ "projectType": "Fence", "projectTitle": "…", "description": "…", "plannedStartDate": "2026-07-01"?, "plannedCompletionDate": "2026-07-20"?, "contractorName": "…"?, "contractorContact": "…"? }` — all but `projectType` optional at draft time (full validation applies at submit).
- **Response 201**: the draft detail (same shape as GET detail), `status: "Draft"`, no `displayId`/`receivedDate`/`dueDate`.
- **Errors**: 422 `VALIDATION_ERROR` (bad project type, over-length title/description).

## PUT /property/architectural-applications/{id}

Edit a draft (US1). Draft only.

- **Request**: the same fields as create; provided fields overwrite.
- **Response 200**: the updated draft detail.
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`; 409 `NOT_DRAFT` (already submitted).

## DELETE /property/architectural-applications/{id}

Delete a draft (US1). Draft only. Removes the row and deletes its attachment objects that no other row references.

- **Response 204**.
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`; 409 `NOT_DRAFT`.

## POST /property/architectural-applications/{id}/submit

Submit a draft (US1). Allocates `ARC-<number>`, received/due dates and rule snapshots via `ArcApplicationFactory`, flips to `Open`, sets `SubmittedByUserId` + `OwnerName`, enqueues the confirmation email.

- **Request**: empty (the draft holds the data).
- **Behavior**: validates all required fields, `plannedCompletionDate ≥ plannedStartDate`, and `acknowledged = true`; allocates the number atomically from `CommunityArcSettings.NextApplicationNumber`; writes the `arc_owner_submitted` outbox row (DedupKey `arc:{id}:submitted`, `RecipientUserId` = caller) in the same transaction; dispatches after commit.
- **Response 200**: the submitted detail with `displayId: "ARC-1042"`, `receivedDate`, `dueDate`, `status: "Submitted"`.
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`; 409 `NOT_DRAFT`; 422 `VALIDATION_ERROR` (missing required field, bad dates, or acknowledgement not accepted — `code: "ACKNOWLEDGEMENT_REQUIRED"` in the message detail).

> The acknowledgement is carried on the draft (set via create/update as `acknowledged: true`) or passed on submit; either way submit refuses without it (FR-003).

## POST /property/architectural-applications/{id}/attachments

Upload one attachment (US1/US5) to a draft, or to an info-request reply in progress when `infoRequestId` is supplied.

- **Request**: `multipart/form-data` with `file`, optional `infoRequestId`.
- **Behavior**: sniffs content (PDF/JPEG/PNG/HEIC), checks the declared type agrees, enforces the env-level per-file / count / total limits against existing attachments, uploads via `IDocumentStorage.UploadAsync`, writes the `ArchitecturalAttachment` row (with `InfoRequestId` when a reply).
- **Response 201**: `{ "id": "guid", "fileName": "fence-plan.pdf", "sizeBytes": 1258291, "contentType": "application/pdf" }`.
- **Errors**:

  | Status | Code | When |
  |---|---|---|
  | 403 | `FORBIDDEN` | Not the caller's property |
  | 409 | `NOT_DRAFT` | Application attachment on a non-Draft (reply attachments use an open info request instead) |
  | 422 | `VALIDATION_ERROR` | Content type not allowed / declared type mismatch, over per-file size, or over count/total |

## DELETE /property/architectural-applications/{id}/attachments/{attachmentId}

Remove an attachment from a Draft (or a carried-over attachment on a revision draft). Deletes the object only when no other row references its `StorageKey`.

- **Response 204**.
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`; 409 `NOT_DRAFT`.

## GET /property/architectural-applications

"My architectural requests" (US2). Lists the caller's applications for their active property.

- **Query**: `status` (`all` default, or one of `draft|open|moreInfo|approved|denied|withdrawn`), `limit` (default 25, clamp 100), `offset` (default 0).
- **Response 200**:

```json
{
  "items": [{
    "id": "guid",
    "displayId": "ARC-1042",
    "revision": 1,
    "projectType": "Fence",
    "projectTitle": "Fence replacement — 6ft cedar",
    "status": "MoreInfoRequested",
    "attachmentCount": 3,
    "receivedDate": "2026-05-28",
    "dueDate": "2026-06-27",
    "decision": { "outcome": "Denied", "wording": "RevisionsRequested" },
    "infoRequested": true,
    "submittedAt": "2026-05-28T15:04:00Z"
  }],
  "total": 6, "limit": 25, "offset": 0
}
```

- `status` is the resident projection (`Draft|Submitted|MoreInfoRequested|Approved|Denied|Withdrawn`). `displayId`/`receivedDate`/`dueDate` are null for drafts. `decision` is null unless Closed with a decision.
- No votes, voters, comments, or attachment URLs appear here.
- **Errors**: 422 `VALIDATION_ERROR` (bad `status`).

## GET /property/architectural-applications/{id}

Request detail (US2). Resident-safe projection.

- **Response 200**: everything in a list item, plus:

```json
{
  "description": "…",
  "plannedStartDate": "2026-07-01",
  "plannedCompletionDate": "2026-07-20",
  "contractorName": "…",
  "contractorContact": "…",
  "acknowledged": true,
  "attachments": [{ "id": "guid", "fileName": "fence-plan.pdf", "sizeBytes": 1258291, "contentType": "application/pdf", "infoRequestId": null }],
  "infoRequests": [{ "id": "guid", "message": "Please attach a plat survey", "requestedAt": "…", "responseMessage": null, "respondedAt": null }],
  "timeline": [{ "event": "Created", "at": "…" }, { "event": "Submitted", "at": "…" }, { "event": "InfoRequested", "at": "…" }],
  "ownerReason": "Lower the fence to 5ft per Guideline 4.2",
  "denialWording": "RevisionsRequested",
  "formalDisapprovalStatement": "This is a formal disapproval …",
  "conditionsOfApproval": null,
  "closedAt": null,
  "revisions": [{ "id": "guid", "revision": 1, "status": "Denied", "receivedDate": "…" }]
}
```

- `infoRequests[]` carries the board's `message` and the resident's `responseMessage` (both resident-visible), but **not** the requesting board member's identity beyond what the resident needs; it never carries votes or vote comments.
- `ownerReason`/`denialWording`/`formalDisapprovalStatement` appear only when Denied; `conditionsOfApproval` only when Approved with conditions.
- `revisions[]` lists every revision of the same `ApplicationNumber` the caller owns, oldest first.
- Attachment URLs are not embedded; fetch them per item below.
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`.

## GET /property/architectural-applications/{id}/attachments/{attachmentId}/url

Issue one short-lived link (US2/US5).

- **Behavior**: authorizes the property scope, emits the 025 FR-017 sensitive event (`resource attachment:{id}`), returns a pre-signed URL from `IDocumentStorage.GetPreSignedUrlAsync`.
- **Response 200**: `{ "url": "https://…", "expiresAt": "…" }` — expires in 5 minutes (within the 15-minute cap).
- **Errors**: 403 `FORBIDDEN` (also when the attachment is not on this application); 404 `ATTACHMENT_UNAVAILABLE` (object missing from storage).

## POST /property/architectural-applications/{id}/info-requests/{infoRequestId}/reply

Reply to an outstanding "Request info" (US3).

- **Request**: `{ "responseMessage": "required, non-blank, ≤ 2000" }` (attachments are uploaded first via the attachments endpoint with `infoRequestId`).
- **Behavior**: in one transaction, store `ResponseMessage` + `RespondedByUserId`, set `RespondedAt` (clears the marker). Does not change `Status` or `DueDate`.
- **Response 200**: the updated detail.
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`; 409 `INFO_ALREADY_ANSWERED`; 422 `VALIDATION_ERROR` (blank or too long).

## POST /property/architectural-applications/{id}/withdraw

Withdraw an undecided request (US4).

- **Behavior**: allowed only when `Status = Open` and no decision reached. Sets `Status = Closed`, `DecisionOutcome = Withdrawn`, `WithdrawnAt`, `WithdrawnByUserId`, `ClosedAt`. Enqueues no email. The application leaves the board's Open list and the board's default Closed view (surfaced only via `includeWithdrawn`).
- **Response 200**: the updated detail (`status: "Withdrawn"`).
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`; 409 `APPLICATION_DECIDED` (a decision was reached) or `ALREADY_WITHDRAWN`.

## POST /property/architectural-applications/{id}/revise

Revise and resubmit a denied request (US6).

- **Behavior**: allowed only when `Status = Closed` and `DecisionOutcome = Denied`. Calls 027's revision factory to create a new `Draft` revision (`Revision + 1`, `PreviousRevisionId = {id}`), pre-filled from the previous revision with its attachments carried over (new `ArchitecturalAttachment` rows pointing at the same `StorageKey`). Returns the new draft so the resident can edit/add/remove attachments and then `submit` it via the normal draft flow.
- **Response 201**: the new draft detail (`status: "Draft"`, `revision: 2`, `displayId` still null until submit).
- **Errors**: 403 `FORBIDDEN`; 404 `NOT_FOUND`; 409 `NOT_DENIED` (not a closed, denied application).

---

## Board list change (027-owned, additive)

`GET /communities/{communityId}/architectural-applications` (027) gains an optional query param:

| Param | Values | Default | Effect |
|---|---|---|---|
| `includeWithdrawn` | `true` / `false` | `false` | When `false`, the `closed` tab excludes `DecisionOutcome = Withdrawn`. When `true`, withdrawn applications are included (the board "show withdrawn" filter). |

Backward-compatible: existing callers omitting the param see today's behavior minus withdrawn rows (which did not exist before 029).

## Confirmation email (outbox payload)

| Kind | To | Subject | Body fields |
|---|---|---|---|
| `arc_owner_submitted` | `RecipientUserId` = submitting resident (`AspNetUsers.Email`) | "We received your architectural request ARC-1042" | owner first name, display ID, project title, received date, due date, request link `/app/property/architectural/{id}` |

Plain text for now (an `ArcSubmissionEmail` renderer), consistent with 027's plain-text-until-templates approach.
