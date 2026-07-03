# Contract: Auth Session (cookie-based refresh) — Sub-specs A & D

Changes the token-transport contract so the refresh token lives in an `HttpOnly` cookie instead of a response body / client storage. Access token remains a bearer token held in memory by the SPA.

## POST /auth/login
- **Auth**: anonymous; rate-limited (`auth` policy, trusted-edge partition); per-account lockout applies (A FR-A4).
- **Request**: `{ email, password }`
- **Response 200**: body `{ token, expiresAt, user }` (access token + non-sensitive user summary) **and** header `Set-Cookie: neko_refresh=<token>; HttpOnly; Secure; SameSite=Strict; Path=/auth; Max-Age=<refresh-lifetime>`
- **Change from today**: `refreshToken` is **removed from the response body** and delivered only via the cookie.
- **Errors**: generic `INVALID_CREDENTIALS` (no enumeration); `423`/`INVALID_CREDENTIALS` while locked (no distinct "locked" leak beyond a generic retry-after if desired).

## POST /auth/refresh
- **Auth**: anonymous; refresh token read from the `neko_refresh` cookie (**not** the body).
- **Request**: empty body; cookie carries the token.
- **Response 200**: `{ token, expiresAt, user }` + rotated `Set-Cookie: neko_refresh=<new>; HttpOnly; Secure; SameSite=Strict; Path=/auth` (single-use rotation preserved).
- **Errors**: `401` with cleared cookie on invalid/expired/rotated-away token.

## POST /auth/logout
- **Auth**: authenticated.
- **Effect**: revoke the refresh token (existing behavior) **and** clear the cookie (`Set-Cookie: neko_refresh=; Max-Age=0`).

## Cookie / CORS notes
- `SameSite=Strict` (resolved): valid here because the app and API share the `nekohoa.com` registrable domain — `SameSite` is computed on the registrable domain, not the full origin — so app→API `/auth` requests are same-site and the cookie is sent. This requires CORS `Access-Control-Allow-Credentials: true` for the exact app origin and `withCredentials: true` on the frontend `/auth` calls. As CSRF defense-in-depth, `/auth/refresh` MUST also verify the request `Origin`/`Referer` matches the allowed app origin.
- `Path=/auth` limits cookie transmission to the auth endpoints.
- Frontend: access token in memory only; silent refresh on bootstrap (`APP_INITIALIZER`) calls `/auth/refresh` using the cookie.
- Contract doc must state auth requirements, error shape, and that these responses are **never** edge-cached.
