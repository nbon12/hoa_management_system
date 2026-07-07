# Data Model: Sub-Spec D — Frontend Session & Content Security

**Date**: 2026-07-07 · **No database schema changes.** Refresh tokens are already persisted hashed with rotation in PostgreSQL (constitution §2/§7); this feature changes only their *transport* and the client-side session state.

## Client-side session state (frontend)

| Item | Where | Lifetime | Contains credential? |
|------|-------|----------|----------------------|
| Access token | `TokenService` in-memory signal | Until tab close / expiry (15 min) / refresh | Yes — never persisted |
| User summary (name, initials, email) | In-memory signal (from login/refresh response) | Same as access token | PII only, no credential |
| `neko_has_session` hint | `localStorage`, value `'1'` | Set on login/refresh success; cleared on logout or failed startup refresh | **No** — boolean marker only |
| Refresh token | **Browser cookie jar only** (`neko_refresh`, HttpOnly) | 30 days (Max-Age), rotated on every use | Yes — script-inaccessible |

Removed from localStorage: `neko_token`, `neko_refresh`, `neko_user` (all three keys deleted on upgrade; a one-time cleanup wipe runs at startup).

## Refresh cookie (backend-set)

```text
Set-Cookie: neko_refresh=<opaque token>;
  HttpOnly; Secure;
  SameSite=<Auth:RefreshCookie:SameSite>;   # Strict (Prod, Dev) | None (PR envs) | Lax (local Development)
  Path=/api/v1/auth;
  Max-Age=2592000                            # 30 days = Jwt:RefreshTokenExpiryDays
```

- Cleared on logout and on failed refresh: same attributes with `Max-Age=0`.
- `Auth:RefreshCookie` options are FluentValidation-validated at startup: `SameSite ∈ {Strict, Lax, None}`; `None` additionally requires Secure (always true) and a non-empty CORS allowlist.

## State transitions

```text
Anonymous ──login──▶ Authenticated(access in memory, cookie set, hint set)
Authenticated ──access expiry/401──▶ Refreshing(single-flight, cross-tab lock)
Refreshing ──200──▶ Authenticated(new access, rotated cookie, hint refreshed)
Refreshing ──401──▶ Anonymous(cookie cleared by server, hint cleared, redirect to login)
Authenticated ──logout──▶ Anonymous(token revoked server-side, cookie cleared, hint cleared)
App start + hint ──refresh──▶ Authenticated | Anonymous (as above)
App start, no hint ──▶ Anonymous (no network call)
```

## Cross-tab coordination artifacts (frontend, ephemeral)

- Web Lock name: `neko-refresh` (exclusive) — holder performs the single rotation.
- `BroadcastChannel('neko-auth')` messages: `{type:'token', token, expiresAt}` on refresh success; `{type:'logout'}` on logout/refresh failure (all tabs drop to Anonymous).
