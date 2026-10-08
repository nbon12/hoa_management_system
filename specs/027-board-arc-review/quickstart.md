# Quickstart: Board Architectural Review (ARC)

**Branch**: `027-board-arc-review`. Check each user story locally before relying on CI.

## Build and run

```bash
# Backend: migrations and seed apply automatically in Development/Dev
dotnet build
dotnet run --project HOAManagementCompany

# Frontend
cd neko-hoa && npm ci && npm start          # http://localhost:4200
```

The seed (`ArchitecturalSeeder`, research R11) gives the seeded community:
- ARC settings with defaults: 30-day review period, flag overdue only, majority of members, 7-day reminder.
- Four extra board members (`board2@nekohoa.dev` … `board5@nekohoa.dev`), so the board has five, and a community manager (`manager@nekohoa.dev`).
- The four applications from the wireframe, with attachments in MinIO.
- One application that was denied ("revisions requested") as v1 and resubmitted as an open v2.

## Check the user stories

Sign in as `board@nekohoa.dev` / `Password1!` and click **Enter board mode**.

1. **List (US1)**: open **Architectural Applications** in the sidebar.
   - The tabs read "Open · N" and "Closed · M".
   - The header pill reads "1 awaiting your vote".
   - Search "Pattyam" leaves only ARC-1042.
   - Calling `GET /api/v1/communities/{otherCommunityId}/architectural-applications` returns `403 FORBIDDEN`.
2. **Vote (US2)**: on ARC-1042 click **Approve**.
   - The cell reads "you voted approve", the tally goes up, and the pill disappears.
   - Voting again with curl returns `409 ALREADY_VOTED`.
3. **Detail and attachments (US3)**: select ARC-1042. The panel shows the owner, the received date and three files. Open `fence-plan.pdf` and it opens in a new tab. Load the same link again after 5 minutes and it fails.
4. **Request info (US4)**: on any open application, click **Info**, enter a message and send. The "info requested" marker appears, the due date is unchanged, and the "doesn't pause the review period" notice is shown.
5. **Needs your vote (US5)**: go to **Community Home**. The card lists unvoted applications, and **All architectural applications →** goes to the list.
6. **Decision and outcome (US6)**:
   - Sign in as the seeded board members `board2@nekohoa.dev` … `board5@nekohoa.dev` (same password) and vote **Revisions needed** until three of five are on the denial side. The row reads "decision reached: denied · revisions requested".
   - Sign in as `manager@nekohoa.dev` and record the outcome with a reason.
   - Check the email (below) for the reason, the formal statement and the Revise-and-resubmit link.
   - The v2 application shows its badge and a link back to v1.

## Settings (manager)

As `manager@nekohoa.dev`, open **ARC Settings**:
- Set the review period to 45 and the lapse rule to **Deemed approved**. Applications already received keep their old due date.
- A board member calling `PUT …/architectural-settings` gets `403 FORBIDDEN`.

## Reminder and lapse sweep (in deployed environments, the `nekohoa-arc-sweep-<env>` Cloud Scheduler job runs it hourly)

```bash
curl -X POST http://localhost:5212/api/v1/architectural/jobs/sweep \
  -H "X-Scheduler-Secret: dev-scheduler-shared-secret-placeholder"
```

To force a lapse locally, set an open application's `DueDate` in the past (dev database only), run the sweep, and check:
- the community's lapse rule was applied once;
- there is one `arc_board_lapsed` outbox row per board member;
- a second run changes nothing.

## Email locally

Links in emails use `ArchitecturalReview:AppBaseUrl` (deployed environments set it to `https://<frontend_domain>`; locally it is empty, so links are relative). Without SES credentials, outbox rows are marked `Failed` with "No configured provider" (existing behavior). Inspect them:

```sql
SELECT "Kind", "DedupKey", "Status", "PayloadJson" FROM "OutboxMessages"
WHERE "Kind" LIKE 'arc_%' ORDER BY "CreatedAt" DESC;
```

## Tests

```bash
dotnet test --filter "FullyQualifiedName~Architectural"
cd neko-hoa && npm run test:ci
```
