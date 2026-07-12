# Contract: Auth Session (cookie-based refresh) — Sub-Spec D

Supersedes `specs/016-security-hardening/contracts/auth-session.md` (SameSite and Path corrected against verified environment origins — see research.md D-R1). Access token remains a bearer held in memory by the SPA. **Breaking change** vs. today: `refreshToken` leaves the response bodies; refresh input moves from body to cookie. Frontend and backend ship atomically in one PR. These responses are **never** edge-cached (`Cache-Control: no-store`).

## POST /api/v1/auth/login
- **Auth**: anonymous; rate-limited (`auth` policy); per-account lockout applies (017-A FR-A4).
- **Request**: `{ email, password }`
- **Response 200**: `{ token, expiresAt, user }` **+** `Set-Cookie: neko_refresh=<token>; HttpOnly; Secure; SameSite=<config>; Path=/api/v1/auth; Max-Age=2592000`
- **Errors**: generic `INVALID_CREDENTIALS` (no enumeration; lockout not distinguishable).

## POST /api/v1/auth/refresh
- **Auth**: anonymous; refresh token read **only** from the `neko_refresh` cookie; request body empty. Requires `withCredentials` on the client.
- **CSRF**: request `Origin` (fallback `Referer`) MUST match the configured CORS allowlist/suffixes; mismatch → `401` (generic).
- **Response 200**: `{ token, expiresAt, user }` + rotated `Set-Cookie` (strict one-time-use rotation preserved).
- **Errors**: `401 INVALID_REFRESH_TOKEN` **with cookie cleared** (`Max-Age=0`) on invalid/expired/rotated-away token or Origin mismatch.

## POST /api/v1/auth/logout
- **Auth**: authenticated (bearer).
- **Effect**: revoke the refresh token server-side (existing behavior) **and** clear the cookie.
- **Response 204**.

## Cookie / environment matrix
| Environment | Frontend origin | API origin | SameSite |
|---|---|---|---|
| Production | `nekohoa.com` | `api.nekohoa.com` | `Strict` (same registrable domain) |
| Dev | `dev.nekohoa.com` **and** `*.nekohoa-dev.pages.dev` preview origins (the pre-promotion smoke logs in from previews) | `api-dev.nekohoa.com` | `None` (preview origins are cross-site to the API) |
| Per-PR env | `pr-N.nekohoa-dev.pages.dev` | `nekohoa-api-pr-N-*.run.app` | `None` (cross-site) |
| Local Development | `localhost:4200` | `localhost:5212` | `Lax` |

`SameSite` is sourced from validated configuration (`Auth:RefreshCookie:SameSite`) per environment; `None` requires the Origin check above (always on) as CSRF defense.

## Client obligations
- Send `withCredentials: true` on `/api/v1/auth/*` calls only.
- Attach the bearer token only to requests targeting `environment.apiBaseUrl` (FR-D4).
- Maintain the non-credential `neko_has_session` hint; only call refresh at startup when present (research D-R2).
