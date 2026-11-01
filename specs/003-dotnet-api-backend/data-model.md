# Data Model: Implement .NET Backend for NekoHOA API

**Branch**: `003-dotnet-api-backend` | **Date**: 2026-05-24

---

## Overview

All entities are EF Core classes mapped to PostgreSQL tables. Identity entities (`AspNetUsers`, `AspNetRoles`, etc.) are managed by ASP.NET Core Identity and extended via `ApplicationUser`. Community-scoped entities carry a `CommunityId` (`uuid`, FK → `Communities.Id`) serving as the HOA tenant boundary. *(Superseded by specs/025-board-overall-design FR-004/FR-006, PR #174: before 2026-08-28 these were `varchar(20)` strings such as `"SAKURA"`.)*

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

Community (Communities table; CommunityId is a uuid FK since 025)
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
| Id | `text` | PK | IdentityUser default (a GUID string). Every `UserId` FK below is `text` too |
| Email | `varchar(256)` | UNIQUE, NOT NULL | Identity default |
| NormalizedEmail | `varchar(256)` | UNIQUE | Identity default |
| PasswordHash | `text` | NULL | ASP.NET Core Identity default `PasswordHasher` (PBKDF2) |
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
| UserId | `text` | FK → AspNetUsers.Id, NOT NULL | Cascade delete |
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
| UserId | `text` | FK → AspNetUsers.Id, NOT NULL | Cascade delete |
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
| CommunityId | `uuid` | FK → Communities.Id, NOT NULL | HOA tenant boundary (025 FR-004). The community name is read from `Communities.CommunityName`; the stored `CommunityName` column was removed |
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
| EntryType | `varchar(30)` | NOT NULL | `RegularAssessment` \| `Payment` \| `LateFee` \| `FinanceCharge`; 006 adds `Refund` \| `Reversal` \| `Chargeback` \| `ReturnedPaymentFee` \| `Credit` \| `Adjustment` |
| Sequence | `bigint` | NOT NULL | Monotonic per-property order key; the authoritative order for `RunningBalance` (006 FR-007d) |
| TransactionId | `uuid` | NULL | FK → PaymentTransactions.Id (006 FR-007e) |
| FundCode | `varchar(50)` | NULL | Optional GL hook (006 FR-038) |
| CreatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Index**: `PropertyId`, `EntryDate DESC`  
**`currentBalance`**: `RunningBalance` of the entry with the highest `Sequence` for the property (006 FR-007d). This is not necessarily the entry with the latest `EntryDate`, because entries can arrive out of date order.

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
| ProcessingFee | `decimal(10,2)` | NOT NULL, DEFAULT 0 | Derived per method from `HoaPaymentConfig` at draft time (006 FR-010d); no longer a fixed $1.95 |
| VaultedPaymentMethodId | `varchar(255)` | NULL | Stripe `pm_…` reference (006 FR-009) |
| MethodBrand | `varchar(50)` | NULL | Card brand or bank name, display only |
| MethodLast4 | `varchar(4)` | NULL | Last 4, display only |
| MethodFunding | `text` | NULL | Card funding type (006 FR-004b) |
| CurrentAuthorizationId | `uuid` | NULL | FK → PaymentAuthorizations.Id (006 FR-011b) |
| UpdatedAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Sensitive data rule**: No card or bank numbers are stored, raw or masked. Only the Stripe vaulted-method reference and the display brand and last 4 are kept. *(Superseded by specs/006-stripe-payments/data-model.md "RecurringPayment (modified)", PR #19: the masked ACH and card columns were dropped by migration `RecurringVaultedMethod`.)*

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
| CommunityId | `uuid` | FK → Communities.Id, NOT NULL | HOA tenant boundary (025 FR-006) |
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
| CommunityId | `uuid` | FK → Communities.Id, NOT NULL | HOA tenant boundary (025 FR-006) |
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
| UserId | `text` | FK → AspNetUsers.Id, NOT NULL |  |
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
| CommunityId | `uuid` | FK → Communities.Id, NOT NULL | Set from the property's community (025 FR-006) |
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
| CommunityId | `uuid` | FK → Communities.Id, NOT NULL | HOA tenant boundary (025 FR-006) |
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
| UserId | `text` | FK → AspNetUsers.Id, NOT NULL |  |
| Attending | `bool` | NOT NULL |  |
| RsvpdAt | `timestamptz` | NOT NULL, DEFAULT NOW() |  |

**Unique constraint**: `(EventId, UserId)` — upsert on re-RSVP

---

### HoaDocument
**Table**: `HoaDocuments`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| Id | `uuid` | PK |  |
| CommunityId | `uuid` | FK → Communities.Id, NOT NULL | HOA tenant boundary (025 FR-006) |
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
| CommunityId | `uuid` | FK → Communities.Id, NOT NULL | HOA tenant boundary (025 FR-006) |
| Label | `varchar(100)` | NOT NULL | e.g., `"Landscaping"` |
| Color | `varchar(20)` | NOT NULL | CSS hex or named color |
| Amount | `decimal(10,2)` | NOT NULL |  |
| FiscalYear | `int` | NOT NULL | Allows per-year expense snapshots |

**Index**: `CommunityId`, `FiscalYear`

---

## Migration Strategy

| Migration | Description |
|-----------|-------------|
| `20260524200408_InitialSchema` | Creates all tables listed above, including the ASP.NET Core Identity tables (`AddIdentityCore` + `AddEntityFrameworkStores`). Seed data comes from the seeder (`--seed`), not a migration |

**Naming convention**: EF Core's default `YYYYMMDDHHMMSS_PascalCaseDescription` (e.g. `20260524200408_InitialSchema`). Later migrations are in `HOAManagementCompany/Infrastructure/Persistence/Migrations/`.  
**Auto-apply**: `MigrateAsync()` runs on startup when `Startup:ApplyMigrations` or `Startup:SeedData` is on. By default that is only in `Development` and `Dev` (009; `StartupOptions`, `StartupTasks.ApplyStartupDatabaseAsync`).  
**Rollback**: EF Core down-migrations generated for every migration; destructive changes require an explicit data-preservation plan documented in the migration file's comments.

---

## Validation Rules (from openapi.yaml + spec)

| Entity / Field | Rule |
|----------------|------|
| `RegisterRequest.password` | Min 8 chars, requires digit + uppercase + non-alphanumeric (Identity default) |
| `RegisterRequest.verificationToken` / `claimCode` | Both required; a valid, unconsumed email-verification proof and an active single-use claim code for an unclaimed property (017 FR-A1/FR-A3; replaces `accountNumber`) |
| `LoginRequest.email` | Valid email format |
| `CreateIntentRequest.amount` (`POST /payments/intent`) | > 0 |
| `CreateIntentRequest.method` | `card` \| `ach` |
| `RecurringPaymentRequest.setupIntentId` | Required |
| `RecurringPaymentRequest.mandateAccepted` | Must be `true` |
| `RecurringPaymentRequest.draftDay` | 1–28 |
| `RecurringPaymentRequest.fixedAmount` | Required (non-null) when `amountType = fixed` |
| Pagination `page` | Min 1 |
| Pagination `pageSize` | Min 1, max 200, default 50 |
| `PATCH /property/directory-fields/{key}` | `key` must match a known `DirectoryField.FieldKey` for the property |
| `PATCH /property/owner` email | Valid email format if supplied |
