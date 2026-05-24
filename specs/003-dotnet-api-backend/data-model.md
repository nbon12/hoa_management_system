# Data Model: Implement .NET Backend for NekoHOA API

**Branch**: `003-dotnet-api-backend` | **Date**: 2026-05-24

---

## Overview

All entities are EF Core classes mapped to PostgreSQL tables. Identity entities (`AspNetUsers`, `AspNetRoles`, etc.) are managed by ASP.NET Core Identity and extended via `ApplicationUser`. Community-scoped entities carry a `CommunityId` string (e.g., `"SAKURA"`) serving as the HOA tenant boundary.

---

## Entity Relationship Diagram (simplified)

```
ApplicationUser ──< UserProperty >── Property ──< Owner
                                         │
                     RefreshToken        ├──< AddressHistory
                                         ├──< DirectoryField
                                         ├──< LedgerEntry
                                         ├──< RecurringPayment ──< DraftEntry
                                         └──< Violation

Community (CommunityId string)
  ├──< Announcement
  ├── Poll ──< PollOption
  │         └──< PollVote (UserId FK)
  ├──< CalendarEvent ──< EventRsvp (UserId FK)
  ├──< HoaDocument
  └──< CommunityExpense
```

---

## Entities

### ApplicationUser
**Table**: `AspNetUsers` (extended by Identity)

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK | IdentityUser default |
| Email | `varchar(256)` | UNIQUE, NOT NULL | Identity default |
| NormalizedEmail | `varchar(256)` | UNIQUE | Identity default |
| PasswordHash | `text` | NOT NULL | Bcrypt via Identity |
| FirstName | `varchar(100)` | NOT NULL | Extended field |
| LastName | `varchar(100)` | NOT NULL | Extended field |
| *Standard Identity columns* | — | — | UserName, SecurityStamp, etc. |

**Derived**: `Initials` = `FirstName[0] + LastName[0]` (computed, never stored)

---

### UserProperty
**Table**: `UserProperties`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| UserId | `uuid` | FK → AspNetUsers.Id, NOT NULL | Cascade delete |
| PropertyId | `uuid` | FK → Properties.Id, NOT NULL | Cascade delete |
| LinkedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Unique constraint**: `(UserId, PropertyId)`  
**Index**: `UserId`, `PropertyId`

---

### RefreshToken
**Table**: `RefreshTokens`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| UserId | `uuid` | FK → AspNetUsers.Id, NOT NULL | Cascade delete |
| TokenHash | `varchar(64)` | NOT NULL | SHA-256 hex of plaintext token |
| ExpiresAt | `timestamptz` | NOT NULL |  |
| CreatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |
| RevokedAt | `timestamptz` | NULL | Set on logout; never deleted until TTL cleanup |

**Index**: `TokenHash` (for lookup on each refresh request), `UserId`

---

### Property
**Table**: `Properties`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| AccountNumber | `varchar(50)` | UNIQUE, NOT NULL | HOA account identifier, used at registration |
| CommunityId | `varchar(20)` | NOT NULL | HOA tenant boundary (e.g., `"SAKURA"`) |
| CommunityName | `varchar(100)` | NOT NULL |  |
| Address | `varchar(200)` | NOT NULL |  |
| City | `varchar(100)` | NOT NULL |  |
| State | `varchar(10)` | NOT NULL |  |
| Zip | `varchar(10)` | NOT NULL |  |
| Lot | `varchar(20)` | NOT NULL |  |
| Phase | `varchar(20)` | NULL |  |
| Section | `varchar(20)` | NOT NULL |  |
| Block | `varchar(20)` | NULL |  |
| FiscalYear | `int` | NOT NULL |  |
| YearBuilt | `int` | NOT NULL |  |
| Status | `varchar(20)` | NOT NULL | `active` \| `inactive` |
| MonthlyAssessment | `decimal(10,2)` | NOT NULL |  |
| AnnualAssessment | `decimal(10,2)` | NOT NULL |  |
| AssessmentDueDay | `int` | NOT NULL, CHECK (1–31) | Used to derive `balanceDueDate` in the dashboard response: next occurrence of this day — current month if the day has not yet passed, otherwise next month |
| LateFeeAmount | `decimal(10,2)` | NOT NULL |  |
| LateFeeGraceDays | `int` | NOT NULL |  |
| FinanceChargeRate | `decimal(5,4)` | NOT NULL | e.g., 0.0150 = 1.5% |
| CreatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Index**: `CommunityId`, `AccountNumber`

---

### Owner
**Table**: `Owners`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| PropertyId | `uuid` | FK → Properties.Id, UNIQUE, NOT NULL | 1:1 with Property |
| FirstName | `varchar(100)` | NOT NULL |  |
| LastName | `varchar(100)` | NOT NULL |  |
| OwnerName2 | `varchar(200)` | NULL | Second owner name |
| Email | `varchar(256)` | NOT NULL |  |
| Phone | `varchar(20)` | NULL |  |
| MailingToProperty | `bool` | NOT NULL, DEFAULT TRUE | Mailing address = property address |
| MailingAddress | `varchar(300)` | NULL | Only if `MailingToProperty = false` |
| PaperlessStatements | `bool` | NOT NULL, DEFAULT FALSE |  |
| SmsReminders | `bool` | NOT NULL, DEFAULT FALSE |  |
| VotingRights | `bool` | NOT NULL, DEFAULT TRUE |  |
| UpdatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

---

### AddressHistory
**Table**: `AddressHistories`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| PropertyId | `uuid` | FK → Properties.Id, NOT NULL |  |
| EventType | `varchar(20)` | NOT NULL | `created` \| `change` |
| Address | `varchar(300)` | NOT NULL | Full address string at event time |
| EffectiveDate | `date` | NOT NULL |  |
| CreatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Append-only**: no updates or deletes. New row inserted whenever Owner's mailing address changes.  
**Order**: returned `ORDER BY EffectiveDate DESC`.

---

### DirectoryField
**Table**: `DirectoryFields`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| PropertyId | `uuid` | FK → Properties.Id, NOT NULL |  |
| FieldKey | `varchar(50)` | NOT NULL | e.g., `phone`, `email`, `name` |
| Label | `varchar(100)` | NOT NULL | Human-readable label |
| Shared | `bool` | NOT NULL, DEFAULT FALSE |  |

**Unique constraint**: `(PropertyId, FieldKey)`  
**Known field keys** (seeded per property): `name`, `email`, `phone`, `address`

---

### LedgerEntry
**Table**: `LedgerEntries`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| PropertyId | `uuid` | FK → Properties.Id, NOT NULL |  |
| EntryDate | `date` | NOT NULL |  |
| DocumentNumber | `varchar(50)` | NULL |  |
| Description | `varchar(300)` | NOT NULL |  |
| ChargeAmount | `decimal(10,2)` | NOT NULL, DEFAULT 0 |  |
| PaymentAmount | `decimal(10,2)` | NOT NULL, DEFAULT 0 |  |
| RunningBalance | `decimal(10,2)` | NOT NULL |  |
| EntryType | `varchar(30)` | NOT NULL | `RegularAssessment` \| `Payment` \| `LateFee` \| `FinanceCharge` |
| CreatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Index**: `PropertyId`, `EntryDate DESC`  
**`currentBalance`**: `RunningBalance` of the most recent entry for the property.

---

### RecurringPayment
**Table**: `RecurringPayments`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| PropertyId | `uuid` | FK → Properties.Id, UNIQUE, NOT NULL | 1:1 with Property |
| AmountType | `varchar(20)` | NOT NULL | `assessment` \| `balance` \| `fixed` |
| FixedAmount | `decimal(10,2)` | NULL | Required when `AmountType = fixed` |
| Method | `varchar(10)` | NOT NULL | `ach` \| `card` |
| DraftDay | `int` | NOT NULL, CHECK (1–28) |  |
| Status | `varchar(10)` | NOT NULL | `active` \| `inactive` |
| ProcessingFee | `decimal(10,2)` | NOT NULL, DEFAULT 0 | $1.95 for card |
| — ACH fields — | | | |
| RoutingNumberMasked | `varchar(20)` | NULL | Last 4 digits only |
| AccountNumberMasked | `varchar(20)` | NULL | Last 4 digits only |
| AccountType | `varchar(10)` | NULL | `checking` \| `savings` |
| — Card fields — | | | |
| CardNumberMasked | `varchar(10)` | NULL | Last 4 digits only |
| CardExpiry | `varchar(7)` | NULL | `MM/YY` |
| CardholderName | `varchar(100)` | NULL |  |
| BillingZip | `varchar(10)` | NULL |  |
| UpdatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Sensitive data rule**: Full card numbers, CVV, and routing numbers are **never persisted**. Only masked representations are stored.

---

### DraftEntry
**Table**: `DraftEntries`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| PropertyId | `uuid` | FK → Properties.Id, NOT NULL |  |
| DraftDate | `date` | NOT NULL |  |
| SourceLabel | `varchar(100)` | NOT NULL | e.g., `"Monthly Assessment – ACH"` |
| Amount | `decimal(10,2)` | NOT NULL |  |
| Status | `varchar(10)` | NOT NULL | `paid` \| `scheduled` \| `failed` |

**Index**: `PropertyId`, `DraftDate DESC`

---

### Announcement
**Table**: `Announcements`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| CommunityId | `varchar(20)` | NOT NULL | HOA tenant boundary |
| Title | `varchar(300)` | NOT NULL |  |
| Body | `text` | NOT NULL |  |
| PublishedAt | `timestamptz` | NOT NULL |  |
| Category | `varchar(20)` | NOT NULL | `Board` \| `Maintenance` \| `Events` \| `Emergencies` |
| Pinned | `bool` | NOT NULL, DEFAULT FALSE |  |
| LikeCount | `int` | NOT NULL, DEFAULT 0 |  |
| CommentCount | `int` | NOT NULL, DEFAULT 0 |  |
| AuthorName | `varchar(200)` | NOT NULL |  |
| AuthorRole | `varchar(100)` | NULL |  |
| ImageUrl | `varchar(500)` | NULL |  |

**Index**: `CommunityId`, `PublishedAt DESC`

---

### Poll
**Table**: `Polls`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| CommunityId | `varchar(20)` | NOT NULL | HOA tenant boundary |
| Question | `varchar(500)` | NOT NULL |  |
| ClosingLabel | `varchar(100)` | NOT NULL | e.g., `"Closes June 30, 2026"` |
| TotalVotes | `int` | NOT NULL, DEFAULT 0 |  |
| IsActive | `bool` | NOT NULL, DEFAULT TRUE |  |
| CreatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Constraint**: at most one active poll per community (`WHERE IsActive = TRUE AND CommunityId = ?`)

### PollOption
**Table**: `PollOptions`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| PollId | `uuid` | FK → Polls.Id, NOT NULL | Cascade delete |
| OptionText | `varchar(300)` | NOT NULL |  |
| OptionIndex | `int` | NOT NULL | Zero-based, determines vote mapping |
| VoteCount | `int` | NOT NULL, DEFAULT 0 |  |
| Percentage | `decimal(5,2)` | NOT NULL, DEFAULT 0 | Recomputed on each vote |

### PollVote
**Table**: `PollVotes`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| PollId | `uuid` | FK → Polls.Id, NOT NULL |  |
| UserId | `uuid` | FK → AspNetUsers.Id, NOT NULL |  |
| OptionIndex | `int` | NOT NULL |  |
| VotedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Unique constraint**: `(PollId, UserId)` — one vote per resident per poll

---

### Violation
**Table**: `Violations`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| PropertyId | `uuid` | FK → Properties.Id, NOT NULL |  |
| CommunityId | `varchar(20)` | NOT NULL |  |
| Title | `varchar(300)` | NOT NULL |  |
| Description | `text` | NULL |  |
| Category | `varchar(20)` | NOT NULL | `Maintenance` \| `Landscape` \| `Architectural` \| `Parking` \| `Noise` \| `Other` |
| Status | `varchar(10)` | NOT NULL | `open` \| `closed` |
| IssuedDate | `date` | NOT NULL |  |
| ResolvedDate | `date` | NULL |  |
| DueDate | `date` | NULL |  |
| FineAmount | `decimal(10,2)` | NULL |  |
| ImageUrl | `varchar(500)` | NULL |  |
| CreatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Index**: `PropertyId`, `IssuedDate DESC`

---

### CalendarEvent
**Table**: `CalendarEvents`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| CommunityId | `varchar(20)` | NOT NULL | HOA tenant boundary |
| Title | `varchar(300)` | NOT NULL |  |
| Description | `text` | NULL |  |
| EventDate | `timestamptz` | NOT NULL |  |
| Location | `varchar(300)` | NULL |  |
| Category | `varchar(20)` | NOT NULL | `Board` \| `Amenity` \| `Social` \| `Maintenance` |
| RsvpEnabled | `bool` | NOT NULL, DEFAULT FALSE |  |
| RsvpCount | `int` | NOT NULL, DEFAULT 0 |  |
| CreatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Index**: `CommunityId`, `EventDate ASC`

### EventRsvp
**Table**: `EventRsvps`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| EventId | `uuid` | FK → CalendarEvents.Id, NOT NULL | Cascade delete |
| UserId | `uuid` | FK → AspNetUsers.Id, NOT NULL |  |
| Attending | `bool` | NOT NULL |  |
| RsvpdAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Unique constraint**: `(EventId, UserId)` — upsert on re-RSVP

---

### HoaDocument
**Table**: `HoaDocuments`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| CommunityId | `varchar(20)` | NOT NULL | HOA tenant boundary |
| Name | `varchar(300)` | NOT NULL |  |
| Category | `varchar(20)` | NOT NULL | `Forms` \| `Rules` \| `Governing` \| `Budgets` \| `Minutes` |
| EffectiveDate | `date` | NOT NULL |  |
| FileSizeLabel | `varchar(20)` | NOT NULL | Pre-formatted string, e.g., `"2.4 MB"` |
| Pinned | `bool` | NOT NULL, DEFAULT FALSE |  |
| StorageKey | `varchar(500)` | NOT NULL | Object key in MinIO/R2; never exposed directly |
| CreatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Index**: `CommunityId`, `Pinned DESC`, `EffectiveDate DESC`  
**Note**: `StorageKey` is internal. Download URLs are pre-signed on demand and never stored.

---

### CommunityExpense
**Table**: `CommunityExpenses`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| CommunityId | `varchar(20)` | NOT NULL | HOA tenant boundary |
| Label | `varchar(100)` | NOT NULL | e.g., `"Landscaping"` |
| Color | `varchar(20)` | NOT NULL | CSS hex or named color |
| Amount | `decimal(10,2)` | NOT NULL |  |
| FiscalYear | `int` | NOT NULL | Allows per-year expense snapshots |

**Index**: `CommunityId`, `FiscalYear`

---

## Migration Strategy

| Migration | Description |
|-----------|-------------|
| `20260524_InitialSchema` | Creates all tables listed above (except Identity tables, which are handled by `AddDefaultIdentity`) |
| `20260524_SeedCommunityConstants` | Optional: seeds community metadata row for `SAKURA` |

**Naming convention**: `YYYYMMDD_PascalCaseDescription`  
**Auto-apply**: `await db.Database.MigrateAsync()` on startup, guarded by environment checks for Production.  
**Rollback**: EF Core down-migrations generated for every migration; destructive changes require an explicit data-preservation plan documented in the migration file's comments.

---

## Validation Rules (from openapi.yaml + spec)

| Entity / Field | Rule |
|----------------|------|
| `RegisterRequest.password` | Min 8 chars, requires digit + uppercase + non-alphanumeric (Identity default) |
| `RegisterRequest.accountNumber` | Required; must match an existing unclaimed `Property.AccountNumber` |
| `LoginRequest.email` | Valid email format |
| `OneTimePaymentRequest.routingNumber` (ACH) | Exactly 9 digits |
| `OneTimePaymentRequest.cardExpiry` | `MM/YY` format |
| `OneTimePaymentRequest.amount` | > 0 |
| `RecurringPaymentRequest.draftDay` | 1–28 |
| `RecurringPaymentRequest.fixedAmount` | Required (non-null) when `amountType = fixed` |
| Pagination `page` | Min 1 |
| Pagination `pageSize` | Min 1, max 200, default 50 |
| `PATCH /property/directory-fields/{key}` | `key` must match a known `DirectoryField.FieldKey` for the property |
| `PATCH /property/owner` email | Valid email format if supplied |
