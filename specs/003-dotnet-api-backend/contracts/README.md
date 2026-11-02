# API Contracts

**Branch**: `003-dotnet-api-backend` | **Date**: 2026-05-24

## Primary Contract

The canonical API contract is defined in [`neko-hoa/api/openapi.yaml`](../../../neko-hoa/api/openapi.yaml). *(Amended 2026-10-31: that file was last changed in PR #10, before the 006, 017, 020 and 030 changes noted below. The running API's Swagger UI at `/swagger`, which is on by default in `Development` and `Dev` and never in Production, shows the current routes.)*  
**Base path**: `/api/v1`  
**Auth scheme**: Bearer JWT (access token, 15-min expiry)

All 28 endpoints defined in `openapi.yaml` were implemented in PR #10 without breaking changes to request/response shapes. Later specs changed some of them: #9 below (006), and registration and the auth response (017, 020).

## Additions and Deviations

Two categories of changes were introduced during clarification:

### 1. New Endpoints (not in current `openapi.yaml`)

The following endpoints must be **added to `openapi.yaml`** before implementation begins:

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/auth/refresh` | Exchange a refresh token for a new access + refresh token pair |
| `POST` | `/auth/switch-property` | Re-issue a token pair scoped to a different property |

See [`auth-contract-additions.md`](./auth-contract-additions.md) for full schema definitions.

### 2. Schema Changes (deviations from current `openapi.yaml`)

| Schema | Field | Change |
|--------|-------|--------|
| `RegisterRequest` | `accountNumber` | ~~Added~~ — *superseded by 017 FR-A1/FR-A3 (PR #100)*: the request is now `verificationToken`, `password`, `firstName`, `lastName`, `claimCode` |
| `AuthResponse` | `refreshToken` | ~~Added~~ — *removed by 020 FR-D1 (PR #103)*: the refresh token travels only in the HttpOnly `neko_refresh` cookie; the body is `{ token, expiresAt, user }` |
| All paginated endpoints | Pagination params | **No change** — `page`/`pageSize` as defined; this deviates from the constitution's `limit`/`offset` standard (justified — contract is fixed) |

### 3. New Error Codes

| Code | HTTP Status | Trigger |
|------|-------------|---------|
| `REGISTRATION_FAILED` | 422 | `POST /auth/register` — any failed verification proof or claim code, a taken email, or a claimed property (017 FR-A3/FR-A5; replaces `ACCOUNT_NOT_FOUND` and `ACCOUNT_ALREADY_CLAIMED`) |
| `PROPERTY_ACCESS_DENIED` | 403 | `POST /auth/switch-property` — user not linked to requested property |
| `INVALID_REFRESH_TOKEN` | 401 | `POST /auth/refresh` — cookie missing; token not found, expired, or revoked; or request `Origin` not allowed. The cookie is cleared (020 FR-D1) |

## Endpoint Inventory

| # | Method | Path | Auth | Description |
|---|--------|------|------|-------------|
| 1 | POST | `/auth/register` | None | Register new resident + link to property |
| 2 | POST | `/auth/login` | None | Authenticate; returns token pair |
| 3 | POST | `/auth/logout` | Bearer | Revoke refresh token |
| 4 | GET | `/auth/me` | Bearer | Return authenticated user profile + properties |
| 5 | POST | `/auth/refresh` | None* | Exchange refresh token for new token pair |
| 6 | POST | `/auth/switch-property` | Bearer | Re-scope token to a different property |
| 7 | GET | `/dashboard` | Bearer | Aggregated resident dashboard |
| 8 | GET | `/payments/ledger` | Bearer | Paginated ledger entries |
| 9 | POST | ~~`/payments/one-time`~~ | Bearer | *Replaced by 006 (PR #16):* `POST /payments/intent` creates a Stripe PaymentIntent, then `POST /payments/one-time/confirm` records the confirmed payment (specs/006-stripe-payments/contracts/payments-api.md) |
| 10 | GET | `/payments/recurring` | Bearer | Get recurring payment config |
| 11 | PUT | `/payments/recurring` | Bearer | Create/replace recurring payment config |
| 12 | DELETE | `/payments/recurring` | Bearer | Cancel recurring payment |
| 13 | GET | `/payments/drafts` | Bearer | Up to 12 months of draft history |
| 14 | GET | `/property` | Bearer | Full property record |
| 15 | GET | `/property/owner` | Bearer | Owner contact + preferences |
| 16 | PATCH | `/property/owner` | Bearer | Partial update owner record |
| 17 | GET | `/property/address-history` | Bearer | Mailing address change log |
| 18 | GET | `/property/directory-fields` | Bearer | Directory visibility fields |
| 19 | PATCH | `/property/directory-fields/{key}` | Bearer | Toggle field visibility |
| 20 | GET | `/community/announcements` | Bearer | Paginated announcements |
| 21 | GET | `/community/announcements/{id}` | Bearer | Single announcement |
| 22 | GET | `/community/poll` | Bearer | Active community poll |
| 23 | POST | `/community/poll/{id}/vote` | Bearer | Cast a vote |
| 24 | GET | `/community/violations` | Bearer | Resident violations |
| 25 | GET | `/community/events` | Bearer | Community calendar events |
| 26 | POST | `/community/events/{id}/rsvp` | Bearer | RSVP to an event |
| 27 | GET | `/community/documents` | Bearer | HOA documents |
| 28 | GET | `/community/documents/{id}/download` | Bearer | Pre-signed download URL |

*`/auth/refresh` reads the refresh token from the HttpOnly `neko_refresh` cookie and takes no body (020 FR-D1); it does not require a Bearer access token (since the access token may be expired).

**Total endpoints implemented by this spec**: **28** (PR #10). The 2 additions above are already included: `openapi.yaml` as committed contains `/auth/refresh` and `/auth/switch-property`. Later specs replaced #9 and added more endpoints; see their `contracts/`.
