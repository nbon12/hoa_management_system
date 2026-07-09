# Contract: Owner Profile & Platform Behaviors — Sub-spec C

## PATCH /owners/{id} (verified email change + input caps)
- **Auth**: authenticated; property-scoped (existing tenancy check preserved).
- **Request** (partial): `firstName?`, `lastName?`, `mailingAddress?`, `phone?`, `email?`
- **Validation** (FluentValidation): all string fields `MaximumLength` bounded; `phone` E.164; `email` well-formed. Privileged fields (e.g., voting rights) remain **non-editable** (no over-posting) — preserved and tested.
- **Email change**: if `email` changes, the new value does **not** take effect immediately. The endpoint initiates an `EmailVerification` (purpose=`email_change`) to the new address; the change applies only after `POST /auth/verify-email/confirm` succeeds, at which point the login/identity store is synced. Response indicates "verification pending" for the email field.
- **Errors**: consistent documented error shape.

## Pagination (all collection endpoints — C FR-C2)
- Collections accept `limit`/`offset` (constitution §4/§5) with a documented default and max: `1 ≤ limit ≤ 100`, `offset ≥ 0`. `Page`/`PageSize` are retained as **deprecated aliases** (mapped to limit/offset) for the existing frontend and are clamped identically.
- Out-of-range values are clamped or rejected with a validation error — never a server error, never a full-table scan.

## Error shape (deployed non-local — C FR-C5)
- Deployed non-local environments (incl. Dev) return `{ error: <generic message>, correlationId }` with no type/stack/internals. Full detail only in local `Development` and server-side logs keyed by `correlationId`. Production unchanged.

## Security headers (C FR-C7)
- API responses carry the hardened baseline via repo-controlled app middleware: `X-Content-Type-Options: nosniff`, frame options, and `Strict-Transport-Security` where applicable. Asserted by test. (Frontend CSP is delivered separately via `neko-hoa/src/assets/_headers`, enforcing — Sub-spec D.)

## Rate limiting (C FR-C3/FR-C4)
- Telemetry proxy limiter partitions by trusted-edge client identity (not the edge address). A global default limiter applies to endpoints without a specific policy, including anonymous registration.

## Observability (C FR-C1)
- All log sinks pass through the registered `TelemetryScrubbingEnricher`; configured sensitive fields (email, name, token, card/account numbers) are redacted in every emitted event. Asserted by an integration test that fails if the enricher is unregistered.
