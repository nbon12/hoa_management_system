# Feature Specification: Implement .NET Backend for NekoHOA API

**Feature Branch**: `003-dotnet-api-backend`  
**Created**: 2026-05-24  
**Status**: Draft  
**Input**: User description: "implement a .NET backend that fulfils this openapi.yaml contract. Also add test data seeder that seeds the local dev environment with test users, with test usernames and passwords, test violations, test payment history, etc."

## Clarifications

### Session 2026-05-24

- Q: How should the new API be delivered relative to the existing project structure? → A: Replace the existing `HOAManagementCompany` Blazor project entirely with a fresh `dotnet new webapi` scaffold under the same project name and solution, discarding the Blazor/Razor layer.
- Q: What JWT logout invalidation strategy should be used? → A: Short-lived access tokens (15-minute expiry) paired with a refresh token stored server-side in PostgreSQL; logout invalidates the refresh token so it cannot be exchanged for a new access token. The access token expires naturally within 15 minutes.
- Q: A user can own multiple properties — how does the API resolve which property to scope a request to? → A: `propertyId` is embedded as a claim in the JWT at login time (defaulting to the user's first property). A `POST /auth/switch-property` endpoint accepts a target `propertyId` and re-issues a new access token + refresh token scoped to that property. All scoped queries derive property context exclusively from the JWT claim — no per-request parameter or header is needed.
- Q: How should the test data seeder be invoked? → A: As a CLI flag on the API host project — `dotnet run -- --seed` runs the seeder then exits without starting the API server. Normal startup (`dotnet run`) does not seed.
- Q: Should MinIO be a required service in docker-compose or use stub URLs for local dev? → A: MinIO is a required service in `docker-compose.yaml`. The document download endpoint generates real pre-signed URLs against the local MinIO instance. No stub URL code path is needed.
- Q: Should the seeder upload actual files to MinIO or only create PostgreSQL document metadata? → A: The seeder uploads small placeholder files (e.g., lightweight PDFs or text files) to MinIO for each seeded document record, so pre-signed download URLs resolve end-to-end without any manual file setup.
- Q: How does a self-registered user get linked to a property? → A: `POST /auth/register` accepts an `accountNumber` field alongside email and password. The server looks up the matching property, creates the `UserProperty` link, and returns HTTP 422 with `ACCOUNT_NOT_FOUND` if the account number is unknown, or `ACCOUNT_ALREADY_CLAIMED` if it is already linked to another user.
- Q: Does `POST /auth/register` return a full token pair or only a user profile? → A: Registration returns the same token pair as login — access token, refresh token, expiry timestamp, and user profile — so the client is immediately authenticated without a separate login call.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Resident Authentication (Priority: P1)

A resident visits the NekoHOA portal for the first time and creates an account using their email address and a password. On subsequent visits, they sign in with those credentials and receive a secure session token that grants them access to all protected areas. When done, they can sign out to invalidate the token. Existing authenticated clients can also retrieve their own profile at any time.

**Why this priority**: Authentication is the foundation of every other feature. No other API functionality can be accessed without a valid token. Without this, nothing else works.

**Independent Test**: Can be fully tested by submitting a registration request, then a login request, then calling `/auth/me`, then logging out — verifying a JWT is issued, the correct user profile is returned, and the token is invalidated on logout.

**Acceptance Scenarios**:

1. **Given** valid email, password (8+ chars), and a known unclaimed `accountNumber` are provided, **When** `POST /auth/register` is called, **Then** the system returns HTTP 201 with an access token, refresh token, expiry timestamp, and the new user's profile (id, firstName, lastName, email, initials).
2. **Given** the same email is registered again, **When** `POST /auth/register` is called, **Then** the system returns HTTP 409 with error code `EMAIL_TAKEN`.
2a. **Given** an unknown `accountNumber`, **When** `POST /auth/register` is called, **Then** the system returns HTTP 422 with error code `ACCOUNT_NOT_FOUND`.
2b. **Given** an `accountNumber` already linked to another user, **When** `POST /auth/register` is called, **Then** the system returns HTTP 422 with error code `ACCOUNT_ALREADY_CLAIMED`.
3. **Given** a registered user's correct credentials, **When** `POST /auth/login` is called, **Then** the system returns HTTP 200 with a fresh access token, refresh token, expiry timestamp, and user profile.
4. **Given** an incorrect password, **When** `POST /auth/login` is called, **Then** the system returns HTTP 401 with error code `INVALID_CREDENTIALS`.
5. **Given** a valid JWT in the Authorization header, **When** `GET /auth/me` is called, **Then** the system returns the authenticated user's profile.
6. **Given** a valid JWT, **When** `POST /auth/logout` is called, **Then** the system returns HTTP 204 and subsequent use of that token is rejected.
7. **Given** a request with a missing or malformed JWT, **When** any protected endpoint is called, **Then** the system returns HTTP 401 with error code `UNAUTHORIZED`.

---

### User Story 2 - Resident Dashboard (Priority: P2)

A signed-in resident opens the home screen of the portal and sees a consolidated summary of their account and community: their current balance and due date, any open violations, the count and newest documents, a pinned announcement, upcoming calendar events for the week, recent ledger activity, and a breakdown of community expenses by category.

**Why this priority**: The dashboard is the resident's primary landing page and aggregates data from multiple domains. It unblocks front-end development across all major sections simultaneously.

**Independent Test**: Can be fully tested by seeding representative data for a resident and calling `GET /dashboard`, verifying all required fields are present and accurate.

**Acceptance Scenarios**:

1. **Given** an authenticated resident with a seeded account, **When** `GET /dashboard` is called, **Then** the response contains `currentBalance`, `balanceDueDate`, `openViolations`, `documentCount`, `newDocumentsThisMonth`, `thisWeekEvents`, `recentActivity` (up to 5 entries), and `communityExpenses`.
2. **Given** a pinned announcement exists in the community, **When** `GET /dashboard` is called, **Then** `pinnedAnnouncement` is populated with the announcement details.
3. **Given** no upcoming event exists this week, **When** `GET /dashboard` is called, **Then** `nextEvent` is `null` and `thisWeekEvents` is an empty array.
4. **Given** an unauthenticated request, **When** `GET /dashboard` is called, **Then** the system returns HTTP 401.

---

### User Story 3 - Payment Management (Priority: P2)

A resident views their account ledger to review charges and payments, optionally filtering by date range, entry type, or a search term. They can submit a one-time payment using either ACH bank transfer or a credit/debit card. They can also set up, modify, or cancel a recurring monthly draft so their assessment is paid automatically each month.

**Why this priority**: Payments represent the most financially sensitive and frequently used resident workflow. Residents need accurate ledger visibility and the ability to pay without contacting management.

**Independent Test**: Can be fully tested independently for each sub-flow: ledger retrieval with filters, one-time ACH payment submission, one-time card payment submission, recurring payment setup, and recurring payment cancellation.

**Acceptance Scenarios**:

1. **Given** an authenticated resident, **When** `GET /payments/ledger` is called without filters, **Then** the response returns paginated ledger entries with `data`, `total`, `page`, `pageSize`, and `currentBalance`.
2. **Given** a `startDate` and `endDate` query parameter, **When** `GET /payments/ledger` is called, **Then** only entries within that date range are returned.
3. **Given** a `type` query parameter of `Payment`, **When** `GET /payments/ledger` is called, **Then** only payment-type entries are returned.
4. **Given** a valid ACH payment body (`method: ach`, `routingNumber`, `accountNumber`, `accountType`), **When** `POST /payments/one-time` is called, **Then** the system returns HTTP 200 with a `confirmationNumber`, `amount`, and `date`.
5. **Given** a valid card payment body (`method: card`, `cardNumber`, `cardExpiry`, `cardCvv`, `cardholderName`, `billingZip`), **When** `POST /payments/one-time` is called, **Then** the system returns HTTP 200 and a processing fee of $1.95 is added server-side.
6. **Given** an invalid payment body (missing required field), **When** `POST /payments/one-time` is called, **Then** the system returns HTTP 422 with field-level validation errors.
7. **Given** a valid upsert body, **When** `PUT /payments/recurring` is called, **Then** the system returns HTTP 200 with the updated recurring payment configuration.
8. **Given** an active recurring payment, **When** `DELETE /payments/recurring` is called, **Then** the system returns HTTP 204 and the recurring payment status becomes `inactive` (record is retained).
9. **Given** an authenticated resident, **When** `GET /payments/drafts` is called, **Then** up to 12 months of draft history (paid, scheduled, failed) is returned.

---

### User Story 4 - Property and Owner Information (Priority: P3)

A resident views the details of their property (address, lot, assessment amounts, status) and their own contact information. They can update mutable contact fields such as name, email, phone, and notification preferences (paperless statements, SMS reminders, mailing address flag). They can also view their mailing address history and control which personal fields are visible to other community members in the directory.

**Why this priority**: Property and owner information is reference data that changes infrequently. It is important for completeness but is lower priority than authentication and payments.

**Independent Test**: Can be fully tested by retrieving property info, retrieving owner info, patching owner fields, checking address history, and toggling a directory field visibility flag.

**Acceptance Scenarios**:

1. **Given** an authenticated resident, **When** `GET /property` is called, **Then** the response contains all required property fields including `accountNumber`, `communityId`, `address`, `monthlyAssessment`, `annualAssessment`, `assessmentDueDay`, `lateFeeAmount`, and `financeChargeRate`.
2. **Given** an authenticated resident, **When** `GET /property/owner` is called, **Then** the owner record is returned with contact and notification preference fields.
3. **Given** a valid partial update body, **When** `PATCH /property/owner` is called, **Then** only the supplied fields are updated and the full updated owner record is returned.
4. **Given** an invalid email format in the patch body, **When** `PATCH /property/owner` is called, **Then** the system returns HTTP 422 with a field-level error on `email`.
5. **Given** an authenticated resident, **When** `GET /property/address-history` is called, **Then** a list of address change events in reverse chronological order is returned.
6. **Given** an authenticated resident, **When** `GET /property/directory-fields` is called, **Then** all directory fields with their `shared` visibility status are returned.
7. **Given** a valid `{ "shared": true }` body and a known field key (e.g., `phone`), **When** `PATCH /property/directory-fields/{key}` is called, **Then** the updated `DirectoryField` is returned.
8. **Given** an unknown field key, **When** `PATCH /property/directory-fields/{key}` is called, **Then** the system returns HTTP 404.

---

### User Story 5 - Community Features (Priority: P3)

A resident browses community content: they read announcements filtered by category, view a single announcement in detail, vote on the active community poll, review violations logged against their property, browse the event calendar with optional date and category filters, RSVP to an event, list HOA documents by category or search term, and retrieve a short-lived download URL for a specific document.

**Why this priority**: Community content enriches the resident experience but is largely read-only and does not gate any financial or administrative flow.

**Independent Test**: Each sub-domain (announcements, polls, violations, events, documents) can be tested independently with its own seed data and endpoint calls.

**Acceptance Scenarios**:

1. **Given** an authenticated resident, **When** `GET /community/announcements` is called, **Then** announcements are returned in reverse chronological order with pagination metadata.
2. **Given** a `category=Board` query parameter, **When** `GET /community/announcements` is called, **Then** only Board-category announcements are returned.
3. **Given** a `pinned=true` query parameter, **When** `GET /community/announcements` is called, **Then** only pinned announcements are returned.
4. **Given** a valid announcement id, **When** `GET /community/announcements/{id}` is called, **Then** the full announcement object is returned.
5. **Given** an invalid announcement id, **When** `GET /community/announcements/{id}` is called, **Then** HTTP 404 is returned.
6. **Given** an active poll exists, **When** `GET /community/poll` is called, **Then** the poll with options, vote percentages, and total votes is returned.
7. **Given** no active poll exists, **When** `GET /community/poll` is called, **Then** HTTP 204 is returned.
8. **Given** a valid `optionIndex`, **When** `POST /community/poll/{id}/vote` is called, **Then** the updated poll with recalculated percentages is returned.
9. **Given** a resident who has already voted, **When** `POST /community/poll/{id}/vote` is called again, **Then** HTTP 409 is returned.
10. **Given** an authenticated resident, **When** `GET /community/violations` is called, **Then** violations for that property are returned, newest first.
11. **Given** `status=open` and `category=Landscape` query parameters, **When** `GET /community/violations` is called, **Then** only matching violations are returned.
12. **Given** an authenticated resident, **When** `GET /community/events` is called, **Then** events sorted by date ascending are returned.
13. **Given** `startDate` and `endDate` query parameters, **When** `GET /community/events` is called, **Then** only events within that range are returned.
14. **Given** a valid event id for an RSVP-enabled event, **When** `POST /community/events/{id}/rsvp` is called with `{ "attending": true }`, **Then** HTTP 204 is returned.
15. **Given** an authenticated resident, **When** `GET /community/documents` is called, **Then** documents are returned pinned-first then by effective date descending.
16. **Given** a valid document id, **When** `GET /community/documents/{id}/download` is called, **Then** a pre-signed URL valid for 5 minutes and an `expiresAt` timestamp are returned.
17. **Given** an invalid document id, **When** `GET /community/documents/{id}/download` is called, **Then** HTTP 404 is returned.

---

### User Story 6 - Local Development Seed Data (Priority: P1)

A developer clones the repository, starts the local environment, and runs a single seed command, which brings up a Docker container that inserts Test Data into the database. The Seeder code should be based in .NET code so it's able to be aware of the data models.  The database is immediately populated with a realistic set of test residents (with known email addresses and passwords), a fully populated property and owner record, a multi-year ledger history, active and cancelled violations, a recurring payment setup, draft history, announcements across all categories, an active poll, upcoming and past calendar events, and a library of HOA documents. Every endpoint in the API can be exercised against this data without any manual database setup.

**Why this priority**: Without seed data, every developer working on this backend or the Angular frontend must manually construct test data before they can test anything. Seed data is essential for a usable local dev environment and is required before the frontend can be wired to the real API.

**Independent Test**: Can be fully tested by running the seed command against a fresh database, then calling a representative endpoint from each domain group (auth, dashboard, payments, property, community) and verifying that meaningful, non-empty responses are returned for each known test account.

**Acceptance Scenarios**:

1. **Given** a fresh (empty) local database, **When** the seed command is run, **Then** it completes without error and all test accounts are created with known credentials.
2. **Given** seeded test account `resident@nekohoa.dev` / `Password1!`, **When** `POST /auth/login` is called with those credentials, **Then** a valid JWT is returned.
3. **Given** the seeded primary resident is authenticated, **When** `GET /dashboard` is called, **Then** the response contains a non-zero balance, at least one open violation, at least one upcoming event, a pinned announcement, and at least one community expense entry.
4. **Given** the seeded primary resident is authenticated, **When** `GET /payments/ledger` is called, **Then** at least 12 ledger entries covering the past 12 months are returned, including both assessment charges and payments.
5. **Given** the seeded primary resident is authenticated, **When** `GET /payments/drafts` is called, **Then** entries spanning paid, scheduled, and failed statuses are present.
6. **Given** the seeded community data, **When** `GET /community/violations` is called for the primary resident, **Then** at least one open violation and one closed violation are returned across multiple categories.
7. **Given** the seeded community data, **When** `GET /community/announcements` is called, **Then** announcements from at least three different categories (Board, Maintenance, Events) are returned, including at least one pinned announcement.
8. **Given** the seeded community data, **When** `GET /community/poll` is called, **Then** an active poll with at least two options and non-zero vote totals is returned.
9. **Given** the seeded community data, **When** `GET /community/documents` is called, **Then** at least five documents across multiple categories are returned, with at least one pinned.
10. **Given** the seed command is run a second time on an already-seeded database, **Then** it completes without error and does not create duplicate records (idempotent behavior).
11. **Given** a production or staging environment configuration, **When** the seed command is invoked, **Then** the seeder refuses to run and returns an error indicating it is restricted to the development environment.

---

### Edge Cases

- What happens when a JWT is structurally valid but expired? The system must return HTTP 401 with `UNAUTHORIZED`.
- What happens when `amount` is 0 or negative in a one-time payment? The system must return HTTP 422.
- What happens when `method: ach` is submitted but `routingNumber` is missing? The system must return HTTP 422 with a field-level validation error.
- What happens when `method: card` is submitted but `cardNumber` is missing? The system must return HTTP 422 with a field-level validation error.
- What happens when `fixedAmount` is null but `amountType: fixed` is set in a recurring payment upsert? The system must return HTTP 422.
- What happens when `draftDay` exceeds 28 in a recurring payment upsert? The system must return HTTP 422.
- What happens when `page` is 0 or negative in a paginated endpoint? The system must return HTTP 422.
- What happens when `pageSize` exceeds 200? The system must return HTTP 422.
- What happens when the ledger `search` parameter matches no entries? An empty `data` array with `total: 0` is returned.
- What happens when a resident attempts to access another resident's property data? The system must deny access and return HTTP 403.
- What happens when `POST /auth/register` is called with an unknown `accountNumber`? The system must return HTTP 422 with `ACCOUNT_NOT_FOUND`.
- What happens when `POST /auth/register` is called with an `accountNumber` already linked to another user? The system must return HTTP 422 with `ACCOUNT_ALREADY_CLAIMED`.

## Requirements *(mandatory)*

### Functional Requirements

**Authentication**
- **FR-001**: The system MUST accept `email`, `password`, and `accountNumber` to register a new resident account. It MUST validate that the `accountNumber` matches an existing, unclaimed property, create a `UserProperty` link, and return a signed access token, refresh token, expiry timestamp, and the new user's profile. It MUST return HTTP 422 with `ACCOUNT_NOT_FOUND` if the account number is unknown, and `ACCOUNT_ALREADY_CLAIMED` if already linked to another user.
- **FR-002**: The system MUST reject registration if the email is already in use, returning error code `EMAIL_TAKEN`.
- **FR-003**: The system MUST accept email and password to authenticate an existing resident and return a signed access token, refresh token, expiry timestamp, `propertyId` claim (defaulting to the user's first property), and user profile.
- **FR-004**: The system MUST reject login with incorrect credentials using error code `INVALID_CREDENTIALS`.
- **FR-005**: The system MUST invalidate the calling resident's refresh token on logout (delete the server-side record). The short-lived access token (15-minute expiry) expires naturally. A `POST /auth/refresh` endpoint MUST accept a valid refresh token and return a new access token and a rotated refresh token.
- **FR-006**: The system MUST return the authenticated resident's profile (id, firstName, lastName, email, initials) and the list of their linked properties (id, address) via `GET /auth/me`.
- **FR-006a**: The system MUST re-issue a new access token + refresh token scoped to a requested `propertyId` via `POST /auth/switch-property`. The endpoint MUST return HTTP 403 if the authenticated user is not linked to the requested property.
- **FR-007**: The system MUST reject all protected requests that lack a valid, non-expired JWT with HTTP 401.

**Dashboard**
- **FR-008**: The system MUST return a single aggregated dashboard payload containing: current balance, balance due date, open violation count, next calendar event, document count, new documents this month, pinned announcement (nullable), this-week events array, recent ledger activity (up to 5 entries), and community expense breakdown.

**Payments — Ledger**
- **FR-009**: The system MUST return paginated ledger entries (charges and payments) sorted by date descending.
- **FR-010**: The system MUST support optional filtering of ledger entries by `startDate`, `endDate`, `type`, and full-text `search` across description and document number.
- **FR-011**: The ledger response MUST include `currentBalance` reflecting the running balance as of the last entry.
- **FR-012**: The ledger response MUST support pagination via `page` (min 1) and `pageSize` (min 1, max 200, default 50).

**Payments — One-Time**
- **FR-013**: The system MUST accept a one-time payment via ACH, requiring `routingNumber` (9 digits), `accountNumber`, and `accountType` (`checking` or `savings`).
- **FR-014**: The system MUST accept a one-time payment via card, requiring `cardNumber`, `cardExpiry` (MM/YY format), `cardCvv`, `cardholderName`, and `billingZip`.
- **FR-015**: The system MUST add a $1.95 processing fee server-side for card payments (not modifiable by the client).
- **FR-016**: Successful one-time payments MUST return a `confirmationNumber`, `amount`, and `date`.

**Payments — Recurring**
- **FR-017**: The system MUST return the current recurring payment configuration via `GET /payments/recurring`.
- **FR-018**: The system MUST create or fully replace the recurring payment configuration via `PUT /payments/recurring`, accepting `amountType` (`assessment`, `balance`, or `fixed`), `method` (`ach` or `card`), `draftDay` (1–28), and relevant bank or card fields.
- **FR-019**: When `amountType` is `fixed`, the system MUST require a non-null `fixedAmount`.
- **FR-020**: The system MUST cancel the recurring payment (set status to `inactive`) via `DELETE /payments/recurring` while retaining the record for audit history.
- **FR-021**: The system MUST return up to 12 months of draft history (paid, scheduled, failed) via `GET /payments/drafts`.

**Property**
- **FR-022**: The system MUST return the full property record associated with the authenticated resident's account via `GET /property`.
- **FR-023**: The system MUST return the owner's contact information and notification preferences via `GET /property/owner`.
- **FR-024**: The system MUST support partial updates to the owner record (firstName, lastName, ownerName2, email, phone, mailingToProperty, paperlessStatements, smsReminders) via `PATCH /property/owner`.
- **FR-025**: The system MUST return mailing address change history in reverse chronological order via `GET /property/address-history`.
- **FR-026**: The system MUST return all directory visibility fields for the resident's account via `GET /property/directory-fields`.
- **FR-027**: The system MUST allow toggling the `shared` flag on individual directory fields by key via `PATCH /property/directory-fields/{key}`.
- **FR-028**: The system MUST return HTTP 404 when an unknown directory field key is patched.

**Community — Announcements**
- **FR-029**: The system MUST return announcements in reverse chronological order with pagination, supporting optional filters for `category` and `pinned`.
- **FR-030**: The system MUST return a single announcement by id, returning HTTP 404 for unknown ids.

**Community — Polls**
- **FR-031**: The system MUST return the active community poll with options, percentages, total votes, and a closing label, or HTTP 204 if no active poll exists.
- **FR-032**: The system MUST record a resident's vote by option index (zero-based), update and return recalculated percentages, and return HTTP 409 if the resident has already voted.

**Community — Violations**
- **FR-033**: The system MUST return violations for the authenticated resident's property, newest first, with optional filters for `status` and `category`, and pagination support.

**Community — Events**
- **FR-034**: The system MUST return community calendar events sorted by date ascending, with optional filters for `category` (repeatable), `startDate`, and `endDate`.
- **FR-035**: The system MUST record an RSVP (attending true/false) for an event via `POST /community/events/{id}/rsvp`, returning HTTP 204 on success and HTTP 404 for unknown event ids.

**Community — Documents**
- **FR-036**: The system MUST return HOA documents pinned-first then by effective date descending, with optional filters for `category`, `pinned`, and full-text `search` across document name, and pagination support.
- **FR-037**: The system MUST return a pre-signed download URL (valid for 5 minutes) and `expiresAt` timestamp for a document by id, returning HTTP 404 for unknown ids.

**Test Data Seeder**
- **FR-038**: The system MUST provide a seed command that can be run in the local development environment to populate the database with representative test data covering every domain (auth, payments, property, community).
- **FR-039**: The seeder MUST create at least two test resident accounts with documented, human-readable email addresses and passwords so developers can log in immediately without looking up credentials.
- **FR-040**: The seeder MUST create a complete property record and owner record for the primary test resident, including all required fields from the API contract.
- **FR-041**: The seeder MUST create at least 12 months of ledger history for the primary test resident, including Regular Assessment charges, Payment entries, at least one Late Fee, and at least one Finance Charge.
- **FR-042**: The seeder MUST create a recurring payment configuration with ACH details for the primary test resident.
- **FR-043**: The seeder MUST create draft history entries covering all three statuses: paid, scheduled, and failed.
- **FR-044**: The seeder MUST create at least three violations for the primary test resident's property, spanning multiple categories (e.g., Landscape, Parking, Maintenance) and including both open and closed statuses.
- **FR-045**: The seeder MUST create at least five announcements for the community, covering all four categories (Board, Maintenance, Events, Emergencies), with at least one pinned announcement.
- **FR-046**: The seeder MUST create one active community poll with at least two options and a non-zero total vote count.
- **FR-047**: The seeder MUST create at least six calendar events spanning past, current week, and future dates, covering multiple event categories (Board, Amenity, Social, Maintenance), with at least one RSVP-enabled event.
- **FR-048**: The seeder MUST create at least five HOA documents across multiple categories (Forms, Rules, Governing, Budgets, Minutes), with at least one pinned document. For each document record the seeder MUST upload a corresponding small placeholder file (e.g., a minimal PDF or `.txt` file) to the MinIO bucket so that pre-signed download URLs resolve successfully end-to-end.
- **FR-049**: The seeder MUST create community expense breakdown entries covering at least four expense categories for the dashboard chart.
- **FR-050**: The seeder MUST be idempotent — running it multiple times against the same database must not create duplicate records.
- **FR-051**: The seeder MUST be restricted to the development environment and MUST refuse to run in staging or production, returning a clear error message if invoked outside of development.
- **FR-052**: The seeder MUST be executable via `dotnet run -- --seed` from the API project directory. When this flag is present the process seeds the database and exits without starting the HTTP server. Normal `dotnet run` starts the API without seeding.

### Key Entities

- **User / Resident**: Represents an authenticated portal user. Identified by UUID (ASP.NET Core Identity `ApplicationUser` extending `IdentityUser` with `firstName`, `lastName`). A user may be linked to **one or more** properties via a `UserProperty` join table. The currently active property is encoded as a `propertyId` claim in the JWT. `initials` is derived server-side.
- **UserProperty**: Join table linking a `User` to a `Property`. Fields: `userId` (FK → ApplicationUser), `propertyId` (FK → Property), `linkedAt` (timestamp). Enables multi-property ownership.
- **RefreshToken**: Server-side record of an issued refresh token. Fields: `id`, `userId` (FK → ApplicationUser), `token` (opaque string, hashed at rest), `expiresAt`, `createdAt`, `revokedAt` (nullable). Deleted on logout; rotated on each refresh exchange.
- **Property**: Represents a single HOA-managed parcel. Contains account number, community association, physical address, lot/section details, assessment schedule, and financial rate configuration.
- **Owner**: Contact and preference record linked to a property. Includes name(s), email, phone, mailing preference, notification preference flags, and voting rights.
- **AddressHistory**: Immutable log of mailing address changes for a property. Each entry records the event type (created, change), address string, and date.
- **DirectoryField**: A named field (e.g., phone, email) with a resident-controlled visibility flag (`shared`) determining whether it appears in the community directory.
- **LedgerEntry**: A financial transaction record (assessment charge, payment, late fee, finance charge) with a date, document number, description, charge/payment amounts, and running balance.
- **RecurringPayment**: Configuration for a monthly auto-draft. Tracks amount type, method, draft day, bank or card details (masked), processing fee, and active/inactive status.
- **DraftEntry**: A historical or scheduled recurring draft event with date, source label, amount, and status (paid, scheduled, failed).
- **Announcement**: A community communication with a title, body, date, category, pinned flag, like/comment counts, author info, and optional image.
- **Poll**: A time-limited community question with options, per-option vote percentages, total vote count, and a closing label. Tracks which residents have voted.
- **Violation**: A compliance issue logged against a property, categorized (Maintenance, Landscape, Architectural, Parking, Noise, Other) with a status (open, closed).
- **CalendarEvent**: A community event with title, date, location, category, and RSVP-enabled flag. Tracks resident RSVPs.
- **HoaDocument** (formerly `HOADocument`): A community document record with name, category, effective date, file size label, pinned flag, and a reference to file storage. Download URLs are short-lived. The C# entity class is named `HoaDocument` per PascalCase convention.
- **CommunityExpense**: A budget breakdown item with a label, display color, and dollar amount, used for the dashboard chart.

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: All resident-facing data (property, ledger, violations, recurring payment, draft history, owner info, directory fields) is scoped to the authenticated resident's linked account and community. A resident may never read or write data belonging to a different account or community. The `communityId` field on the Property entity serves as the HOA tenant boundary for community-scoped content (announcements, events, documents, polls).
- **Authorization**: All endpoints except `POST /auth/login` and `POST /auth/register` require a valid Bearer JWT. Authorization is enforced server-side on every request. Frontend session checks are for UX only and not a security boundary.
- **Ownership and moderation**: Announcement content is authored by the HOA board or management; residents cannot create or edit announcements. Residents own their poll votes (one vote per poll per resident). RSVP records are owned by the resident. Owner contact updates are audited (address history is append-only).
- **API contract**: All responses follow the schema defined in `openapi.yaml`. Error responses use `{ code, message }`. Validation errors use `{ code, message, errors: [{ field, message }] }`. Pagination uses `page`/`pageSize` (not `limit`/`offset`). All date fields use ISO 8601 date (`YYYY-MM-DD`) or date-time (`YYYY-MM-DDTHH:mm:ssZ`) formats. IDs are strings (format varies per entity per spec). No breaking changes to response shapes without a version increment.
- **API implementation and docs**: Endpoints are implemented as described in `openapi.yaml` with base path `/api/v1`. Swagger/OpenAPI UI is available only in the development environment and is disabled in staging and production. All endpoint operations must be documented with operationId matching the spec.
- **Database/runtime**: Database schema migrations are versioned and idempotent. Connection pooling is used to respect PostgreSQL connection limits. DbContext instances are short-lived (per-request). Startup migrations run automatically in the correct environment.
- **File storage**: HOA document files are stored in an object store (Cloudflare R2 in production, MinIO locally). Document metadata (name, category, effective date, pinned flag, file size label) is persisted in PostgreSQL. Pre-signed download URLs are generated on demand with a 5-minute expiry and are never stored.
- **Security and abuse controls**: Payment endpoints require rate limiting to prevent abuse. Sensitive payment input (card numbers, routing numbers, CVV) must never be logged in plain text. All authentication events (login, registration, logout, failed attempts) are recorded via **Serilog structured log output** (not a separate database audit table). Input validation is enforced on all request bodies.
- **Observability**: Errors are tracked via Sentry. Request traces include environment and release tags. Personally identifiable information and payment credentials are excluded from error payloads and trace data. Structured logging captures request outcomes and timing.
- **Accessibility**: This feature is a backend API; accessibility requirements apply to the consuming Angular frontend (WCAG 2.1 AA). The API must return clear, human-readable validation messages suitable for display to residents.
- **Quality gates**: New endpoint handlers require unit tests covering happy path, validation failure, and authorization failure. Integration tests using Testcontainers must verify database interactions. Test data must be isolated per test to allow safe parallel execution. Serilog is used for structured logging. PRs must be focused on a single vertical slice or have a documented rationale for cross-cutting scope.
- **Frontend testing**: Not in scope for this backend feature. The Angular frontend test suite (Jasmine/Karma, Angular Testing Library, Playwright) will be updated separately when the frontend is wired to these live endpoints.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 30 endpoints (28 from `openapi.yaml` + `POST /auth/refresh` and `POST /auth/switch-property` added per `contracts/auth-contract-additions.md`) respond correctly for happy-path scenarios as verified by an integration test suite — no manual testing required to confirm contract compliance.
- **SC-002**: Authentication round-trip (register → login → /me → logout) completes in under 500ms under normal load conditions.
- **SC-003**: The dashboard endpoint returns a complete, correctly structured response in under 300ms for a resident with up to 24 months of ledger history.
- **SC-004**: One-time payment submissions return a confirmation within 2 seconds under normal conditions.
- **SC-005**: All protected endpoints consistently return HTTP 401 for missing, expired, or tampered tokens — verified across all endpoint groups (auth, dashboard, payments, property, community).
- **SC-006**: All validation rules (required fields, enum values, numeric ranges, format patterns) are enforced server-side and return HTTP 422 with field-level error detail — no required-field bypass is possible.
- **SC-007**: Paginated endpoints correctly apply `page`, `pageSize`, and filter parameters — no data leaks across pages or filter boundaries.
- **SC-008**: The backend can be run locally by any team member in under 5 minutes using a single command (e.g., `docker compose up` or equivalent), with seed data available for all endpoints.
- **SC-009**: A new developer can run the seed command on a fresh database and immediately authenticate as a test resident, view a populated dashboard, browse a ledger with at least 12 entries, and explore community content — all without any manual database manipulation.
- **SC-010**: The seed command completes in under 30 seconds on a standard developer machine and produces no errors on first run or on subsequent re-runs against an already-seeded database.

## Assumptions

- The Angular frontend (`neko-hoa`) currently uses `MockDataService` to simulate all API responses. This backend will replace those mocks with real data, and the frontend's service layer will be updated in a separate task to point to `http://localhost:5000/api/v1`.
- PostgreSQL is the primary data store, consistent with the existing project setup using Entity Framework Core 9 and Npgsql.
- A user may be linked to multiple properties (via a `UserProperty` join table). The JWT access token carries the active `propertyId` claim. The login response defaults to the user's first property. Switching properties requires calling `POST /auth/switch-property` to obtain a new token pair.
- A single HOA community (Sakura Heights) is the initial target tenant. Multi-HOA isolation is designed into the data model but not required to be UI-accessible in this phase.
- Payment processing (ACH and card) is simulated server-side for the initial implementation — no real payment gateway integration is required. The $1.95 card processing fee is enforced by the server regardless of gateway status.
- Document file storage (pre-signed URLs) uses MinIO locally and Cloudflare R2 in production. MinIO is a required service in `docker-compose.yaml`; the download endpoint always generates real pre-signed URLs against the configured object store. No stub URL fallback exists.
- ASP.NET Core Identity is used for user account management and password hashing, consistent with existing tooling decisions.
- JWT access tokens have a default expiry of 15 minutes. A refresh token (opaque, stored in PostgreSQL with an expiry of 30 days) is issued alongside each access token. Clients exchange a valid refresh token for a new access token via `POST /auth/refresh`. Logout deletes the refresh token server-side; the short-lived access token expires naturally.
- The `initials` field on `CurrentUser` is derived server-side from the first character of `firstName` and `lastName` (e.g., "NB" for Nicholas Bonilla).
- The poll system supports one active poll per community at a time.
- Address history is append-only; the system creates a new entry whenever the owner's mailing address changes.
- The `fileSizeLabel` on HOADocument is a pre-formatted human-readable string (e.g., "2.4 MB") stored alongside the document metadata, not computed on every request.
- The test data seeder is a development-only utility and does not ship to staging or production builds. Its presence in the codebase is gated by an environment check, not by build configuration alone.
- Seeded test accounts use well-known, documented credentials (e.g., `resident@nekohoa.dev` / `Password1!`) that are committed to the repository's README or developer setup guide, since the seeder is strictly development-only.
- The seeder targets the "Sakura Heights" community (communityId: `SAKURA`) and creates data consistent with the examples shown in `openapi.yaml`.
- A second test account (e.g., `resident2@nekohoa.dev`) may be seeded to enable testing of tenant isolation (i.e., that resident 1 cannot access resident 2's data).
