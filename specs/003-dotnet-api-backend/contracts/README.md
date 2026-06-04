# API Contracts

**Branch**: `003-dotnet-api-backend` | **Date**: 2026-05-24

## Primary Contract

The canonical API contract is defined in [`neko-hoa/api/openapi.yaml`](../../../neko-hoa/api/openapi.yaml).  
**Base path**: `/api/v1`  
**Auth scheme**: Bearer JWT (access token, 15-min expiry)

All 28 endpoints defined in `openapi.yaml` are implemented without breaking changes to request/response shapes.

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
| `RegisterRequest` | `accountNumber` | **Added** — required string; links user to a property at registration |
| `AuthResponse` | `refreshToken` | **Added** — opaque string; returned by register, login, and refresh endpoints |
| All paginated endpoints | Pagination params | **No change** — `page`/`pageSize` as defined; this deviates from the constitution's `limit`/`offset` standard (justified — contract is fixed) |

### 3. New Error Codes

| Code | HTTP Status | Trigger |
|------|-------------|---------|
| `ACCOUNT_NOT_FOUND` | 422 | `POST /auth/register` — `accountNumber` not found |
| `ACCOUNT_ALREADY_CLAIMED` | 422 | `POST /auth/register` — `accountNumber` linked to another user |
| `PROPERTY_ACCESS_DENIED` | 403 | `POST /auth/switch-property` — user not linked to requested property |
| `INVALID_REFRESH_TOKEN` | 401 | `POST /auth/refresh` — token not found, expired, or revoked |

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
| 9 | POST | `/payments/one-time` | Bearer | Submit one-time payment |
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

*`/auth/refresh` carries the refresh token in the request body; it does not require a Bearer access token (since the access token may be expired).

**Total endpoints implemented**: 28 from `openapi.yaml` + 2 additions = **30**
