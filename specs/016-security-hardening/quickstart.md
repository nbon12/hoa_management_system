# Quickstart: Validating the Security Hardening Program (016)

How to verify each slice is correctly implemented. Each slice ships as its own PR; validate independently. All backend tests use xUnit + Testcontainers.PostgreSQL with per-test transaction rollback.

## Prerequisites
- Backend: `dotnet test` (from repo root)
- Frontend: `cd neko-hoa && npm ci && npm run test:ci` (Karma) and `npm run e2e:ci` (Cypress)
- Infra: `tofu validate` / `tofu plan` in the relevant module; CI YAML linted
- Agent config: JSON/shell inspection + hook dry-run

## A — Identity & Access
1. **Claim takeover blocked**: integration test — register with a valid account number but no verified email / no valid claim code → binding refused. Enumerate account numbers → uniform responses (Theory over exists/unclaimed/claimed/absent).
2. **Lockout**: Theory — 10 failed logins → account locked 30 min; correct password during lock still refused; independent of source IP.
3. **JWT pinning**: token asserting a non-HS256 alg → rejected; expired-by >30s token → rejected.
4. **E2E cleanup**: call without `X-Scheduler-Secret` → refused; call in production-like env → unavailable.
5. **Enumeration**: `/auth/verify-email/request` returns identical response for known vs unknown emails.

## B — Payments Integrity
1. **Exactly-once settlement**: simulate crash between ledger append and status flip, then reprocess via redelivery and via reconciliation → exactly one ledger credit. Run webhook + reconciliation concurrently for one pending payment → one credit.
2. **Uniqueness backstop**: attempt to insert a second settlement credit for the same `(TransactionId, EntryType)` → rejected; a refund (different EntryType) for the same transaction → allowed.
3. **Idempotency isolation**: same key from two properties → both succeed; same-property replay → returns original, no 500.
4. **Amount mismatch**: settlement with provider≠expected → no credit, `SettlementReviewQueue` row created (status `open`).

## C — Platform & Data Protection
1. **PII scrubbing**: trigger login/registration; assert emitted log events contain `[REDACTED]`, not the email. A test asserts the enricher is registered (fails if not).
2. **Pagination**: `PageSize=2147483647` and overflow `Page` → clamped, no server error, no full-table scan (Theory over boundary values).
3. **Telemetry limiter**: behind simulated edge, requests attribute to trusted-edge identity; a global default limiter bounds an un-policied endpoint.
4. **Error shape**: force a 500 in a deployed-non-local profile → generic message + correlationId, no stack trace; local Development still shows detail.
5. **Email change/validation**: oversized/invalid profile fields rejected; email change requires new-address verification before taking effect; privileged fields non-editable.
6. **Headers**: response carries `nosniff`/frame/HSTS baseline (asserted).

## D — Frontend Session & Content Security
1. **Cookie session**: after login, the refresh token is not readable by page script; `document.cookie` does not expose it; access token is in memory only. Reload → silent refresh restores session.
2. **CSP**: served `_headers` includes an enforcing CSP limiting script/frame to self + `js.stripe.com` and connect to self + API; payments and API calls still work (Cypress).
3. **No committed creds**: `git ls-files | grep e2e/.auth/state.json` → absent; `.gitignore` covers `e2e/.auth/`; e2e still runs (global-setup regenerates state).
4. **Interceptor**: bearer attached only to API-origin requests; concurrent 401s → one refresh call.

## E — CI/CD & Infra Least Privilege
1. **SA split/ref-scope**: assume apply identity from a non-`main` ref → denied; deployer roles contain no `roles/owner`.
2. **Secret scoping**: `infra-plan` PR job has no write-capable operator secrets; per-PR install/e2e steps have no infra secrets in env.
3. **Pinning**: every `uses:` in the privileged workflows is a full commit SHA.
4. **Branch protection**: merge to `main` with failing/pending checks → blocked (status-checks-only; human review not required — accepted risk).
5. **Containers**: `docker run ... id -u` → non-root; base images digest-pinned; compose services bind loopback.
6. **Per-PR creds**: two PR envs have distinct DB credentials.

## F — AI Supply Chain
1. **No passthrough bypass**: `.claude/settings.local.json` has no `Bash(rtk proxy *)`; deny list blocks passthrough + `.claude/**` writes (deny > allow).
2. **Installer pinned**: `rtk-install.sh` fetches a pinned version + verifies checksum; tampered checksum → refuses (fail closed).
3. **Trust posture**: `.claude/CLAUDE.md` says "verify before acting"; no "trust and act"; `.claude/**` covered by CODEOWNERS.
4. **Model channel**: `:8787` ownership confirmed, access-restricted, documented.
5. **Merge routine (out-of-repo)**: routine decides from structured metadata only, restricted to patch/minor, notifications on; a dependency PR with an injected changelog does not cause any action on the embedded text (controlled test).

## Program-level exit criteria
- All Critical/High findings `fixed` with a passing test/assertion (SC-001).
- Every finding in a documented resolution state (SC-002).
- No live secrets in the repo (SC-003).
- No agent merges/deploys to `main` bypassing required checks (SC-004).
- Independent re-review reproduces zero prior Critical/High findings (SC-005).
- All existing + new tests pass (SC-006).
