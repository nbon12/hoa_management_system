# Contract: Uniform Business-Error Envelope

**Applies to**: every FastEndpoints endpoint in `HOAManagementCompany` (FR-006, FR-008).

## Shape

All business errors (thrown as `DomainException`) are mapped centrally by `GlobalExceptionHandler` to:

```json
{
  "code": "STABLE_ERROR_CODE",
  "message": "Human-readable explanation."
}
```

- **HTTP status**: the `DomainException.StatusCode` (400/401/403/404/409/422 as thrown at the raise site).
- **Content-Type**: `application/json`.
- **Cacheability**: never cacheable.
- **Missing/invalid identity claim** (via the shared accessor): `403` with `code = "MISSING_CLAIM"` — replaces today's 500/NRE.
- **Unhandled (non-business) exceptions**: unchanged — generic 500 with no system details in production; details logged server-side and reported to Sentry.

## Stability rules

- `code` values are part of the public contract: existing codes (`EMAIL_TAKEN`, `PROPERTY_ACCESS_DENIED`, `INVALID_CREDENTIALS`, …) are preserved verbatim.
- Endpoints MUST NOT hand-write this envelope; raising `DomainException` is the only sanctioned mechanism (enforced by review + the disappearance of per-endpoint `catch (DomainException)` blocks).
- Validation failures keep the existing FastEndpoints 422 response shape (unchanged by this feature).

## Migration note (non-breaking tightening)

Endpoints that previously surfaced `DomainException` as an unstructured 500 (no catch block) now return the envelope with the intended status. Clients coded to the documented envelope are unaffected; this closes a gap rather than changing a documented shape.

## Verification

- Integration test suite: one Theory over every documented `code`, asserting envelope shape + status via the public API (spec US2 scenarios 1–3).
- A "no boilerplate" test: an endpoint without any error handling raises `DomainException` and still yields the envelope.
