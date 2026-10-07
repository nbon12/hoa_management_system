# Data Model: Board Architectural Review (ARC)

**Feature**: `027-board-arc-review` | **Date**: 2026-10-07 | **Research**: [research.md](./research.md)

All new tables are added in one forward-only EF Core migration (`<timestamp>_AddArchitecturalReview`), which applies idempotently at Cloud Run startup. Timestamps are `timestamptz` (UTC). Enums are stored as strings, matching existing conventions (for example `OutboxMessage.Status`).

## New enums (`HOAManagementCompany/Domain/Enums/`)

| Enum | Values | Notes |
|---|---|---|
| `ArcApplicationStatus` | `Open`, `DecisionReached`, `Closed` | Lifecycle below |
| `ArcVoteChoice` | `Approve`, `RevisionsNeeded`, `Deny` | RevisionsNeeded + Deny = denial side (spec FR-023) |
| `ArcOutcome` | `Approved`, `Denied` | The legal record (FR-025) |
| `ArcDenialWording` | `RevisionsRequested`, `Denied` | Owner-facing only; null when approved |
| `ArcDecisionSource` | `Votes`, `Lapse` | Lapse = "by default (review period lapsed)" |
| `ArcDecisionRule` | `MajorityOfMembers`, `MajorityOfVotesCastWithQuorum` | FR-029 |
| `ArcLapseRule` | `FlagOverdueOnly`, `DeemedApproved`, `DeemedDenied` | FR-027 |
| `ArcProjectType` | `Fence`, `Solar`, `ExteriorPaint`, `Outbuilding`, `Landscaping`, `WindowsDoors`, `Addition`, `Other` | Owned by the submission spec; defined here so seeding works |

## Entities

### `CommunityArcSettings` (one per community)

| Field | Type | Rules |
|---|---|---|
| `CommunityId` | Guid, PK + FK → `Communities` | One row per community, created on first read with defaults |
| `ReviewPeriodDays` | int | 1–365, default 30 |
| `LapseRule` | `ArcLapseRule` | Default `FlagOverdueOnly` |
| `DecisionRule` | `ArcDecisionRule` | Default `MajorityOfMembers` |
| `ReminderDays` | int | 0–30, default 7, 0 = off |
| `TimeZoneId` | string(64) | IANA, default `America/New_York`, must resolve (R7) |
| `FormalDisapprovalStatement` | string(1000) | Required; default text from spec FR-029 |
| `NextApplicationNumber` | int | Starts at 1001; incremented atomically (R3) |
| `UpdatedAt` | timestamptz | |
| `UpdatedByUserId` | string? | FK → `AspNetUsers`, set null on delete |

### `ArchitecturalApplication` (one row per revision)

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid PK | Used in all API paths |
| `CommunityId` | Guid FK → `Communities` | Denormalized from the property for scoping and indexing; must equal `Property.CommunityId` |
| `PropertyId` | Guid FK → `Properties` | |
| `ApplicationNumber` | int | Shown as `ARC-<n>`; shared by all revisions |
| `Revision` | int | ≥ 1; badge "v{n}" shown when ≥ 2 |
| `PreviousRevisionId` | Guid? FK → self | Required when `Revision` > 1 |
| `SubmittedByUserId` | string? FK → `AspNetUsers` | Set by the submission spec; null for seeded rows |
| `OwnerName` | string(200) | Snapshot at submission (owners change) |
| `ProjectType` | `ArcProjectType` | |
| `ProjectTitle` | string(200) | Required |
| `Description` | string(4000) | |
| `ReceivedDate` | date | Received date |
| `DueDate` | date | `ReceivedDate + ReviewPeriodDays`, fixed at submission (FR-029/030) |
| `DecisionRule` | `ArcDecisionRule` | Snapshot at submission (FR-030) |
| `LapseRule` | `ArcLapseRule` | Snapshot at submission (FR-030) |
| `TimeZoneId` | string(64) | Snapshot at submission |
| `Status` | `ArcApplicationStatus` | |
| `DecisionOutcome` | `ArcOutcome?` | Set when `DecisionReached` |
| `DecisionWording` | `ArcDenialWording?` | Set when the outcome is `Denied` |
| `DecisionSource` | `ArcDecisionSource?` | |
| `DecisionReachedAt` | timestamptz? | |
| `ConditionsOfApproval` | string(2000)? | Only when `Approved` (FR-025) |
| `OwnerReason` | string(2000)? | Required when `Denied` (FR-025) |
| `ClosedAt` | timestamptz? | Set when the manager records the outcome |
| `ClosedByUserId` | string? FK → `AspNetUsers` | |
| `ReminderSentAt` | timestamptz? | Sweep idempotency (R6) |
| `LapseProcessedAt` | timestamptz? | Sweep idempotency (R6) |
| `OwnerEmailStatus` | string(20)? | `Pending`, `Sent`, `Failed`; mirrors the latest outcome outbox row for the manager's resend UI |
| `CreatedAt` | timestamptz | |

**Indexes**
- Unique `(CommunityId, ApplicationNumber, Revision)`
- `(CommunityId, Status, DueDate)` for list tabs and the sweep
- `(PropertyId)` for recusal and owner lookups
- `(PreviousRevisionId)`

**Checks**
- `"Revision" >= 1`
- `("Revision" = 1) = ("PreviousRevisionId" IS NULL)`
- `"ConditionsOfApproval" IS NULL OR "DecisionOutcome" = 'Approved'`

### `ArchitecturalAttachment`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid PK | |
| `ApplicationId` | Guid FK → `ArchitecturalApplications` (cascade) | Per revision |
| `FileName` | string(255) | Shown in the UI; text only |
| `SizeBytes` | long | > 0 |
| `ContentType` | string(100) | |
| `StorageKey` | string(500) | `arc/{communityId}/{applicationNumber}/{guid}`; may be shared across revisions (R8) |
| `CreatedAt` | timestamptz | |

Index: `(ApplicationId)`.

### `ArchitecturalVote`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid PK | |
| `ApplicationId` | Guid FK (cascade) | |
| `VoterUserId` | string FK → `AspNetUsers` (restrict) | |
| `Choice` | `ArcVoteChoice` | |
| `Comment` | string(2000)? | Board and manager only (FR-019) |
| `CastAt` | timestamptz | |

Unique `(ApplicationId, VoterUserId)` enforces one vote per member (FR-016). Votes are immutable: there is no update or delete endpoint.

### `ArchitecturalInfoRequest`

| Field | Type | Rules |
|---|---|---|
| `Id` | Guid PK | |
| `ApplicationId` | Guid FK (cascade) | |
| `RequestedByUserId` | string FK → `AspNetUsers` (restrict) | |
| `Message` | string(2000) | Required, non-blank |
| `RequestedAt` | timestamptz | |
| `RespondedAt` | timestamptz? | Set by the submission spec when the owner replies; clears the "info requested" marker |

### `OutboxMessage` (modified — R5)

| Change | Detail |
|---|---|
| `OwnerId` | `Guid` → `Guid?` (existing rows unaffected) |
| + `RecipientUserId` | `string?` FK → `AspNetUsers`, cascade delete |
| + Check constraint | `("OwnerId" IS NULL) <> ("RecipientUserId" IS NULL)` |
| `Kind` | New values `arc_owner_approved`, `arc_owner_revisions_requested`, `arc_owner_denied`, `arc_board_reminder`, `arc_board_lapsed`. Current max length is 30; the longest new kind is 29 characters, so it fits. |

### `CommunityCapability` (modified — R1)

Add `ViewArchitecturalApplications` (BoardMember, CommunityManager), `VoteArchitecturalApplications` (BoardMember) and `ManageArchitecturalReview` (CommunityManager).

## Lifecycle

```text
          vote meets decision rule ─┐
 Open ──────────────────────────────┼──► DecisionReached ──manager records outcome──► Closed
   │   lapse: DeemedApproved/Denied ┘        (no more votes)          (owner emailed)
   │
   └── lapse: FlagOverdueOnly → stays Open (shown overdue; voting continues; board emailed once)

 Closed + Denied ──owner "Revise and resubmit" (submission spec)──► new row: Revision+1, Open,
                                                                   PreviousRevisionId → old row
```

**Transition rules**
- `Open → DecisionReached`: only through `ArcDecisionRules.Evaluate` (on a vote or in the sweep), under a row lock (R4). The decision is set exactly once.
- `DecisionReached → Closed`: Community Manager only, with a reason when denied and optional conditions when approved. Writes the outcome outbox row in the same transaction.
- Votes and info requests are refused unless the status is `Open`. Votes are also refused from recused or ineligible users.
- `Closed` is terminal. Revisions are new rows, and earlier rows never change.
- An application is shown as overdue when `Status = Open` and now is past the end of `DueDate` in `TimeZoneId`. This is computed at read time, not stored.

## Derived values (computed per request, not stored)

| Value | Definition |
|---|---|
| Eligible voters | Active BoardMember memberships in the community, minus users linked to the property through `UserProperty` |
| Tally | Counts per `ArcVoteChoice` + not voted (eligible minus votes from current eligible members) |
| Awaiting my vote | Open, caller eligible, caller not recused, no vote by caller |
| Info requested | Any `ArchitecturalInfoRequest` with `RespondedAt` null |
| Rule text | From the snapshotted `DecisionRule` and the eligible count (FR-013), e.g. "Three of five votes decide." |

## Validation summary

| Input | Rule | Error |
|---|---|---|
| Vote `choice` | One of the three | 422 `VALIDATION_ERROR` |
| Vote / info `comment`, `message` | ≤ 2,000 chars; info message required, non-blank | 422 `VALIDATION_ERROR` |
| Vote on non-Open | | 409 `APPLICATION_DECIDED` or `APPLICATION_CLOSED` |
| Duplicate vote | | 409 `ALREADY_VOTED` |
| Recused voter | | 403 `RECUSED` |
| Outcome on non-`DecisionReached` | | 409 `NO_DECISION` |
| Outcome reason missing when denied | | 422 `VALIDATION_ERROR` |
| Conditions on denial | | 422 `VALIDATION_ERROR` |
| Wording supplied for a vote-decided denial | Wording comes from votes | 422 `VALIDATION_ERROR` |
| Settings out of range / bad time zone / blank statement | | 422 `VALIDATION_ERROR` |
| Any request outside the caller's scope | Same body as 025 | 403 `FORBIDDEN` (FR-016) |
