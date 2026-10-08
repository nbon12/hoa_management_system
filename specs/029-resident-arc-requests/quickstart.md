# Quickstart: Resident Architecture Request

**Branch**: `029-resident-arc-requests`. Extends the `027-board-arc-review` ARC model (027 lands first). Check each user story locally before relying on CI.

## Build and run

```bash
# Backend: migrations and seed apply automatically in Development/Dev
dotnet build
dotnet run --project HOAManagementCompany

# Frontend
cd neko-hoa && npm ci && npm start          # http://localhost:4200
```

Sign in as a resident who owns a seeded property (the existing resident seed user / claim-code flow). All ARC actions below are scoped to the resident's **active property** (the `propertyId` claim); use **Switch property** to change it.

## Check the user stories

1. **File a request (US1)**: open **Architectural requests** → **New request**.
   - Pick a project type, title, description, planned start/completion dates, optional contractor; attach a PDF.
   - **Save draft**, leave, reopen, edit a field, save — it stays a Draft and is not on the board's Open list.
   - Try to **Submit** without the "work may not begin until approved" acknowledgement → refused.
   - Set completion earlier than start → refused.
   - Check the box and **Submit** → the request gets `ARC-<n>` (n ≥ 1001), a received date and a due date (received + 30 days by default), and shows as **Submitted / Under review**.
   - Confirm a `arc_owner_submitted` outbox row was written (see Email below).
2. **Track (US2)**: open **My architectural requests**.
   - The list shows each request with its status chip; open the detail page.
   - Open an attachment — it opens via a link that stops working after 5 minutes.
   - For a denied request, the detail shows the wording ("Revisions requested"/"Denied"), the manager's reason and the formal disapproval statement — and **no** votes, voters, or board comments.
   - `GET /api/v1/property/architectural-applications/{idOfAnotherProperty}` returns `403 FORBIDDEN`.
3. **Respond to more info (US3)**: have a board member (027, e.g. `board@nekohoa.dev`) use **Request info** on your submitted request.
   - Your **dashboard** shows a "more information requested" alert; the request detail shows the question.
   - Reply with a message and an attachment → the marker clears, the due date is unchanged, and the board/manager can see your reply.
4. **Withdraw (US4)**: on an undecided request, **Withdraw** → it becomes **Withdrawn** in your list and leaves the board's Open list.
   - It is absent from the board's default Closed tab; a board member enabling **Show withdrawn** (`?includeWithdrawn=true`) sees it.
   - Withdrawing an already-decided request returns `409 APPLICATION_DECIDED`.
5. **Revise and resubmit (US6)**: on a **Denied** request, click **Revise and resubmit**.
   - A new **Draft** revision (v2) opens, pre-filled, with the old attachments carried over; remove one and confirm v1 still has it.
   - Add a file, submit → the board sees `ARC-<n>` v2, Open, with a link back to v1.
   - Revising a non-denied request returns `409 NOT_DENIED`.

## Attachments: validation and limits

```bash
# Rename a non-PDF to .pdf and upload → rejected by content sniffing
cp some.txt fake.pdf
# (upload fake.pdf via the form) → 422 VALIDATION_ERROR

# Over the per-file limit (default 50 MB), or past 20 files / 250 MB total → 422 VALIDATION_ERROR
```

Limits are environment-level (`Architectural:Uploads` → `ArcUploadOptions`: `MaxFileBytes`, `MaxFilesPerApplication`, `MaxTotalBytes`). Override per deployment via configuration/env.

## Email locally

Without SES credentials, the `arc_owner_submitted` outbox row is marked `Failed` ("No configured provider") — expected locally. Inspect it:

```sql
SELECT "Kind", "DedupKey", "Status", "PayloadJson" FROM "OutboxMessages"
WHERE "Kind" = 'arc_owner_submitted' ORDER BY "CreatedAt" DESC;
```

## Tests

```bash
dotnet test --filter "FullyQualifiedName~Property.Architectural|FullyQualifiedName~ArcAttachmentValidator"
cd neko-hoa && npm run test:ci
```
