# Data Model: Security Hardening Program (016)

**Date**: 2026-07-02 · Only slices A, B, C introduce schema changes. D/E/F have no application-schema impact. All changes are **strict EF Core migrations tested against PostgreSQL** (Testcontainers). All timestamps UTC.

## Sub-spec A — Identity & Access

### New: `EmailVerification`
Proves control of an email address before any registration/claim state is revealed (FR-A3) and reused for the verified email-change flow (Sub-spec C, FR-C6).

| Field | Type | Notes |
|-------|------|-------|
| `Id` | uuid (PK) | |
| `Email` | text | normalized/lowercased |
| `Purpose` | text/enum | `registration` \| `email_change` |
| `UserId` | uuid (FK, nullable) | null for pre-registration; set for email-change |
| `CodeHash` | text | SHA-256 of the one-time code (never store raw) |
| `ExpiresAt` | timestamptz | short-lived (e.g., 30 min) for verification codes |
| `ConsumedAt` | timestamptz (nullable) | single-use |
| `AttemptCount` | int | attempt-limited |
| `CreatedAt` | timestamptz | |

Indexes: `(Email, Purpose)`; `ExpiresAt` for cleanup.

### New: `PropertyClaimCode`
Authorizes binding a user to a property (FR-A1). Single-use, 90-day validity, delivered only to the owner's contact on file.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | uuid (PK) | |
| `PropertyId` | uuid (FK → Properties) | **tenant boundary** |
| `CodeHash` | text | SHA-256 of the claim code |
| `DeliveredToContact` | text | masked record of the destination (audit) |
| `ExpiresAt` | timestamptz | issuance + 90 days |
| `RedeemedAt` | timestamptz (nullable) | single-use |
| `RedeemedByUserId` | uuid (nullable) | |
| `AttemptCount` | int | attempt-limited before regeneration |
| `CreatedAt` | timestamptz | |

Indexes: `PropertyId`; unique partial on `(PropertyId)` where `RedeemedAt IS NULL` to prevent multiple live codes per property (validate against seeding).

> **Tenancy (Constitution §3)**: `PropertyId` is the tenant-boundary equivalent — a Property belongs to a community/HOA, matching the existing `communityId`/`propertyId` scoping — and applies equally to `SettlementReviewQueue.PropertyId`. `EmailVerification` is **identity-scoped** (tied to an email/user, used pre-registration and for email-change), so it carries no HOA boundary by design.

### Changed: Identity lockout (no new table)
Uses existing `AspNetUsers` lockout columns (`LockoutEnabled`, `LockoutEnd`, `AccessFailedCount`). Config: `MaxFailedAccessAttempts=10`, `DefaultLockoutTimeSpan=30 min` (FluentValidation-validated options).

## Sub-spec B — Payments Integrity

### Changed: `LedgerEntries` — uniqueness backstop
Add **unique index** on `(TransactionId, EntryType)` (FR-B2). Permits distinct compensating entries (refund/reversal/chargeback have different `EntryType`) while preventing a second settlement `Payment` credit per transaction. Existing `(PropertyId, Sequence)` unique index retained.

### Changed: `PaymentTransactions` — per-tenant idempotency
Replace the global unique index on `IdempotencyKey` with composite unique `(PropertyId, IdempotencyKey)` (FR-B4). Unique-violation is caught and collapsed to a replay response.

### New: `SettlementReviewQueue`
Holds settlements blocked by an amount mismatch (FR-B5) for human resolution.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | uuid (PK) | |
| `PropertyId` | uuid (FK) | **tenant boundary** |
| `TransactionId` | uuid (FK → PaymentTransactions) | |
| `ExpectedAmount` | numeric | server-computed total |
| `ProviderAmount` | numeric | provider-reported received amount |
| `Currency` | text | |
| `Status` | text/enum | `open` \| `resolved` \| `dismissed` |
| `CreatedAt` | timestamptz | |
| `ResolvedAt` | timestamptz (nullable) | |
| `ResolvedByUserId` | uuid (nullable) | |
| `ResolutionNote` | text (nullable) | |

Indexes: `Status` (open-queue queries); `TransactionId`.

**No schema change**: the atomic-settlement fix (FR-B1) is a transaction-boundary change in `WebhookProcessor`/`LedgerService`, not a schema change. Forward-only (FR-B0): no data backfill.

## Sub-spec C — Platform & Data Protection

- **No new tables.** The verified email-change flow reuses `EmailVerification` (Purpose=`email_change`).
- Profile field constraints (MaximumLength, E.164 phone) are validation-layer, not schema (existing columns are `text`).
- Pagination clamping, header middleware, Serilog enricher registration, telemetry limiter, and error-shape change are code/config, no schema.

## Sub-specs D, E, F
No application-schema changes.
- **D**: refresh token already persisted (hashed) in the existing token store; the HttpOnly cookie only transports it — no new table. Access token becomes in-memory (client state).
- **E**: infrastructure (IAM/WIF, per-PR Neon roles) — no app schema.
- **F**: agent configuration files — no app schema.

## Migration ordering & safety
1. Additive-only migrations (new tables, new indexes) — safe on Cloud Run startup, no downtime.
2. The `PaymentTransactions` idempotency index change is a **drop-and-recreate** of a unique index: verify no existing cross-tenant key collisions before applying (query first); include a rollback note (destructive-migration rule, Constitution §3).
3. The `LedgerEntries (TransactionId, EntryType)` unique index: verify no existing duplicates would violate it (ties to B being forward-only — if historical duplicates exist, the index creation would fail, surfacing them; decide per-case, do not auto-delete).
