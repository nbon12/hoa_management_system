# Quickstart: Sub-Spec D — Frontend Session & Content Security

**Branch**: `020-security-hardening-subspec-d` · Verify each control locally before relying on CI.

## Build & run

```bash
# Backend (cookie endpoints; Development config uses SameSite=Lax)
dotnet build HOAManagementCompany/HOAManagementCompany.csproj
dotnet run --project HOAManagementCompany          # http://localhost:5212

# Frontend
cd neko-hoa && npm ci
npm start                                          # http://localhost:4200
```

## Verify the controls

1. **Cookie transport (FR-D1)** — log in at `localhost:4200`; in DevTools → Application:
   - Cookies: `neko_refresh` present with HttpOnly ✓, `Path=/api/v1/auth`.
   - localStorage: only `neko_has_session='1'` — no `neko_token`/`neko_refresh`/`neko_user`.
   - Console: `document.cookie` does **not** show `neko_refresh`.
2. **Re-hydration (hint-gated)** — reload: still signed in; Network shows one `POST /api/v1/auth/refresh` (withCredentials). Log out, reload: **no** refresh call fires.
3. **Cross-tab single-flight (FR-D5)** — open two tabs, let the access token expire (or force 401): exactly one `/auth/refresh` across both tabs (second tab adopts via BroadcastChannel).
4. **CSP (FR-D2)** —
   ```bash
   npm run build && node scripts/stamp-headers.mjs http://localhost:5212
   grep -q "__API_ORIGIN__" dist/neko-hoa/browser/_headers && echo "FAIL: unstamped" || echo "stamped OK"
   ```
   Serve `dist` behind the headers (Pages preview or a local static server honoring `_headers`) and complete a test-mode payment — Stripe frames/XHR must work under the policy.
5. **Interceptor scoping (FR-D4)** — `npm run test:ci`; `auth.interceptor.spec.ts` asserts bearer only on `environment.apiBaseUrl` requests.
6. **Backend contract** — `dotnet test --filter "FullyQualifiedName~AuthCookieTests"` (Set-Cookie attrs per config matrix, rotation, Origin check, body omits refreshToken, logout clears).
7. **E2E** — `npm run e2e:ci` (Cypress session-security spec: login → reload → session survives; storage credential-free).

## Ops step — invalidate the committed token (FR-D3 / SC-D3)

The tracked `e2e/.auth/state.json` held a real Dev refresh token. After the removal commit, revoke the seed user's refresh tokens in the **Dev** database (one-time):

```sql
UPDATE "RefreshTokens" SET "RevokedAt" = now()
WHERE "UserId" = (SELECT "Id" FROM "AspNetUsers" WHERE "Email" = 'resident@nekohoa.dev')
  AND "RevokedAt" IS NULL;
```

CI is unaffected: `e2e/global-setup.ts` regenerates auth state at runtime.

## CI wiring

- `test.yml` (deploy-dev) and `pr-env.yml` run `stamp-headers.mjs` with that deployment's API origin before publishing to Pages (alongside the existing environment sed).
- Confirm the served CSP on a deployed URL:
  ```bash
  curl -sI https://pr-<N>.nekohoa-dev.pages.dev | grep -i content-security-policy
  ```
