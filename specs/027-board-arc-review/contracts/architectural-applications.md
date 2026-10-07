# Contract: Architectural Applications (Board)

All routes carry the global `api/v1` prefix. Every response sets `Cache-Control: no-store` (`BoardHttp.NoStore`), because each is authenticated and community-specific (constitution §8). Errors use the existing shape `{ code, message }`. A request outside the caller's scope returns the 025 body `403 { code: "FORBIDDEN", message: "You do not have access to this community." }`, the same whether the community or application exists or not (025 FR-016). An `applicationId` that belongs to a different community than the path's `communityId` gets the same 403.

Capabilities are from research R1. Write endpoints carry `RequireRateLimiting("board-writes")` (R9).

---

## GET /communities/{communityId}/architectural-applications

The list behind the Open/Closed tabs (US1) and the Needs-your-vote card (US5).

- **Auth**: `ViewArchitecturalApplications`.
- **Query**:

  | Param | Values | Default |
  |---|---|---|
  | `status` | `open` (Open + DecisionReached) or `closed` | `open` |
  | `search` | case-insensitive partial match on property address or owner name, ≤ 100 chars | none |
  | `awaitingMyVote` | `true` restricts to applications the caller can still vote on (US5) | `false` |
  | `limit` | max 100 | 25 |
  | `offset` | | 0 |

- **Response 200**:

```json
{
  "items": [{
    "id": "guid",
    "displayId": "ARC-1042",
    "revision": 1,
    "propertyAddress": "711 Keystone Park Dr #29",
    "ownerName": "Praneeth Pattyam",
    "projectTitle": "Fence replacement — 6ft cedar",
    "attachmentCount": 3,
    "receivedDate": "2026-05-28",
    "dueDate": "2026-06-27",
    "overdue": false,
    "status": "Open",
    "decision": null,
    "infoRequested": false,
    "tally": { "approve": 2, "revisionsNeeded": 0, "deny": 0, "notVoted": 3, "eligible": 5 },
    "myVote": { "state": "CanVote" }
  }],
  "total": 4, "limit": 25, "offset": 0,
  "counts": { "open": 4, "closed": 27, "awaitingMyVote": 1 }
}
```

  - `myVote.state` is one of `CanVote`, `Voted` (with `choice`), `Recused` or `NotEligible` (managers).
  - `decision` is `null` or `{ outcome: "Approved" | "Denied", wording: "RevisionsRequested" | "Denied" | null, source: "Votes" | "Lapse" }`.
  - `counts` ignores `search` and paging, so the tab labels and header pill stay stable (FR-003, FR-005).
  - No attachment URLs appear here (R8).

- **Errors**: 403 `FORBIDDEN`; 422 `VALIDATION_ERROR` for a bad `status`, a `limit` over 100, or a `search` over 100 characters.

## GET /communities/{communityId}/architectural-applications/{applicationId}

The detail panel (US3), including the revision history (FR-035).

- **Auth**: `ViewArchitecturalApplications`. Emits the `ArcSensitiveAccess` sensitive event (actor, community, resource `application:{id}`, UTC time), per 025 FR-017.
- **Response 200**: everything in a list item, plus:

```json
{
  "description": "…",
  "projectType": "Fence",
  "attachments": [{ "id": "guid", "fileName": "fence-plan.pdf", "sizeBytes": 1258291, "contentType": "application/pdf" }],
  "votes": [{ "voterName": "Nicholas Board", "choice": "RevisionsNeeded", "comment": "…", "castAt": "2026-06-02T14:03:00Z" }],
  "infoRequests": [{ "requestedBy": "…", "message": "…", "requestedAt": "…", "respondedAt": null }],
  "ruleText": "Three of five votes decide. The manager records the outcome and notifies the owner.",
  "conditionsOfApproval": null,
  "ownerReason": null,
  "closedAt": null,
  "ownerEmailStatus": null,
  "revisions": [{ "id": "guid", "revision": 1, "receivedDate": "…", "decision": { "outcome": "Denied", "wording": "RevisionsRequested", "source": "Votes" } }]
}
```

  - `votes` and `infoRequests` go only to board members and managers. Every caller of this endpoint is one of those, by the capability check (FR-019).
  - `revisions` lists every revision of the same `ApplicationNumber`, oldest first, the current one included.

- **Errors**: 403 `FORBIDDEN`.

## GET /communities/{communityId}/architectural-applications/{applicationId}/attachments/{attachmentId}/url

Issues one short-lived link (FR-011, FR-012).

- **Auth**: `ViewArchitecturalApplications`. Emits `ArcSensitiveAccess` with resource `attachment:{id}`.
- **Response 200**: `{ "url": "https://…", "expiresAt": "…" }`. The URL comes from `IDocumentStorage.GetPreSignedUrlAsync` and expires in 5 minutes, within the spec's 15-minute cap.
- **Errors**:
  - 403 `FORBIDDEN`, also when the attachment isn't on this application.
  - 404 `ATTACHMENT_UNAVAILABLE` when the object is missing from storage. The UI shows "attachment unavailable" (Edge Cases).

## POST /communities/{communityId}/architectural-applications/{applicationId}/votes

Cast a vote (US2).

- **Auth**: `VoteArchitecturalApplications`, plus the recusal check. Rate-limited.
- **Request**: `{ "choice": "Approve" | "RevisionsNeeded" | "Deny", "comment": "optional, ≤ 2000" }`
- **Behavior**: In one transaction under the row lock: insert the vote, re-count, run `ArcDecisionRules.Evaluate`, and set the decision if one is reached (R4). Logs `ArcVoteCast` as a sensitive event.
- **Response 201**: the updated list item (same shape as in the list), so the row can re-render in place.
- **Errors**:

  | Status | Code | When |
  |---|---|---|
  | 403 | `FORBIDDEN` | Not a board member of this community |
  | 403 | `RECUSED` | Caller owns the property |
  | 409 | `ALREADY_VOTED` | Caller has already voted |
  | 409 | `APPLICATION_DECIDED` | A decision has been reached |
  | 409 | `APPLICATION_CLOSED` | The application is closed |
  | 422 | `VALIDATION_ERROR` | Bad choice or comment too long |

## POST /communities/{communityId}/architectural-applications/{applicationId}/info-requests

Request info (US4). Doesn't count as a vote and doesn't move the due date (FR-021).

- **Auth**: `VoteArchitecturalApplications`. Rate-limited.
- **Request**: `{ "message": "required, non-blank, ≤ 2000" }`
- **Response 201**: `{ id, requestedBy, message, requestedAt, dueDate }`. `dueDate` is echoed unchanged so the UI can show the "doesn't pause the review period" notice.
- **Errors**: 403 `FORBIDDEN`; 409 `APPLICATION_DECIDED` / `APPLICATION_CLOSED`; 422 `VALIDATION_ERROR` (blank or too long).

## POST /communities/{communityId}/architectural-applications/{applicationId}/outcome

The manager records the outcome (FR-025) and the owner is emailed (FR-026).

- **Auth**: `ManageArchitecturalReview`. Rate-limited.
- **Request**: `{ "ownerReason": "required if denied", "conditionsOfApproval": "approved only, optional", "wording": "RevisionsRequested | Denied (deemed denials only, default RevisionsRequested)" }`
- **Behavior**: Under the row lock: requires `DecisionReached`, sets `Closed`/`ClosedAt`/`ClosedByUserId`, and enqueues the owner email outbox row (`arc_owner_*`, DedupKey `arc:{id}:outcome`) in the same transaction, then dispatches after commit (R5). Logs `ArcOutcomeRecorded`.
- **Response 200**: the detail response, with `ownerEmailStatus` set to `Sent`, `Pending`, `Failed`, or `NoOwnerEmail` when the property has no owner email (the outcome is still recorded and no email is queued). The status is always read from the outbox, so it stays correct when the sweep delivers the email later.
- **Errors**:

  | Status | Code | When |
  |---|---|---|
  | 403 | `FORBIDDEN` | Not a manager of this community |
  | 409 | `NO_DECISION` | No decision has been reached |
  | 409 | `APPLICATION_CLOSED` | Already closed |
  | 422 | `VALIDATION_ERROR` | Reason missing on a denial, conditions on a denial, or wording on a vote-decided denial |

## POST /communities/{communityId}/architectural-applications/{applicationId}/outcome/resend-email

Resend a failed owner email (FR-026).

- **Auth**: `ManageArchitecturalReview`. Rate-limited.
- **Behavior**: Allowed only when `Closed` and the latest outcome email failed. Enqueues a new row (DedupKey `arc:{id}:outcome:resend:{n}`) and dispatches it.
- **Response 202**: `{ ownerEmailStatus }`
- **Errors**: 403 `FORBIDDEN`; 409 `EMAIL_NOT_FAILED`.

## GET /communities/{communityId}/architectural-settings

- **Auth**: `ViewArchitecturalApplications`, so board members can see the rules. Returns the defaults when no row exists yet, without writing one (the row is created on the first PUT or the first application).
- **Response 200**: `{ reviewPeriodDays, lapseRule, decisionRule, reminderDays, timeZoneId, formalDisapprovalStatement, updatedAt }`

## PUT /communities/{communityId}/architectural-settings

- **Auth**: `ManageArchitecturalReview`. Rate-limited.
- **Request**: the same fields as GET, all required.
- **Behavior**: Applies to applications received afterwards; existing rows keep their snapshots (FR-030). Logs `ArcSettingsChanged` as a sensitive event with old and new values.
- **Response 200**: the saved settings.
- **Errors**:
  - 403 `FORBIDDEN`: board members and residents (US6, Scenario 17).
  - 422 `VALIDATION_ERROR`: a range violation, an unknown time zone, or a blank or over-1,000-character statement.

## POST /architectural/jobs/sweep

The hourly reminder, lapse and dispatch sweep (R6). Not community-scoped. It is on the static-analysis allow-list with a reason.

- **Auth**: header `X-Scheduler-Secret`, compared in constant time with `Jobs:SchedulerSharedSecret`. No user session.
- **Response 200**: `{ "remindersQueued": n, "lapsesProcessed": n, "decisionsAtDueDate": n, "emailsDispatched": n }`
- **Errors**: 401 for a missing or wrong secret.

---

## Email contract (outbox payloads)

| Kind | To | Subject | Body fields |
|---|---|---|---|
| `arc_owner_approved` | `Owner.Email` of the property | "Your architectural request ARC-1042 was approved" | owner first name, display ID, project, address, received date, decision date, conditions (if any), request link |
| `arc_owner_revisions_requested` | owner | "Changes needed: architectural request ARC-1042" | same as approved, plus reason ("What needs to change"), formal disapproval statement, Revise-and-resubmit link |
| `arc_owner_denied` | owner | "Your architectural request ARC-1042 was not approved" | same as approved, plus reason, formal disapproval statement, Revise-and-resubmit link |
| `arc_board_reminder` | each active board member (`AspNetUsers.Email`) | "Decision due {date}: ARC-1042" | display ID, project, address, due date, tally, link |
| `arc_board_lapsed` | each active board member | "Review period ended: ARC-1042 ({overdue / approved by default / denied by default})" | display ID, project, outcome, link |

Owner emails never include vote counts, voter names or board comments (FR-026).

Link targets:

| Link | Target |
|---|---|
| Request link (owner) | `/app/property/architectural/{applicationId}` |
| Revise and resubmit (owner) | `/app/property/architectural/{applicationId}/revise` |
| Board emails | `/app/board/architectural?open={applicationId}` |

The owner routes are built by the Resident Architectural Application Submission spec. Until it lands, those links fall through to the router's existing `**` fallback.
