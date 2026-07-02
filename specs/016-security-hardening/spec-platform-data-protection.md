# Sub-Spec C: Backend Platform & Data Protection

**Feature Branch**: `016-security-hardening`
**Parent**: [`spec.md`](./spec.md)
**Created**: 2026-07-01
**Status**: Draft

## Overview

The backend platform is largely well-configured (clean CORS with explicit allow-lists, no SQL injection, correct document-storage authorization, startup config validation). The findings here are: a **High-severity privacy regression** where the PII-scrubbing log enricher was written and tested but never wired into the pipeline — so user emails ship un-redacted to an external log sink; plus resource-exhaustion gaps (unclamped pagination, incomplete rate-limit coverage, a telemetry rate limiter that is ineffective behind the edge proxy), error-detail exposure in a deployed environment, missing input length caps, and absent transport/security headers.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Stop leaking PII into logs (Priority: P1)

Every log event that reaches any sink (console or external telemetry vendor) has sensitive fields — email, name, tokens, card/account numbers — redacted, matching the redaction policy the codebase already defines and tests.

**Why this priority**: The scrubbing enricher exists and is unit-tested but is **not registered** in the logging pipeline, while authentication logs emit the user's email on every registration, login, and failed login. Those records are exported to an external observability vendor, leaking PII the platform explicitly promises to redact and turning the log store into an email-enumeration oracle. Trace spans are scrubbed; logs are not.

**Independent Test**: Trigger registration/login/failed-login and inspect the emitted log events; the email is `[REDACTED]` (or equivalent) in every sink, and an integration test asserts this so the enricher cannot be silently dropped again.

**Acceptance Scenarios**:

1. **Given** an authentication event, **When** a log event is emitted, **Then** no configured sensitive field appears in cleartext in any sink.
2. **Given** the logging pipeline configuration, **When** it is loaded, **Then** the scrubbing enricher is active, verified by an automated test that fails if it is not registered.

---

### User Story 2 - Resist resource exhaustion (Priority: P2)

List endpoints reject or clamp abusive page sizes, all state-changing and anonymous endpoints have effective rate limiting, and the telemetry proxy's limiter attributes requests to the real client even behind the edge proxy.

**Why this priority**: List endpoints accept unbounded page sizes (full-table materialization; integer overflow on page math yields server errors). The telemetry limiter partitions by an address that is the edge proxy's, not the client's — so all real users share one bucket while an attacker spread across edge addresses can burn the external telemetry vendor's quota with credentialed forwarding. Several endpoints (including anonymous registration) have no limiter and there is no global default.

**Independent Test**: Request a list endpoint with an extreme page size; the response is clamped/rejected without a server error. Confirm the telemetry limiter attributes to the trusted-edge client identity. Confirm a global default limiter applies to otherwise-unlimited endpoints.

**Acceptance Scenarios**:

1. **Given** a list request with an out-of-range page or page size, **When** it is processed, **Then** the values are clamped to safe bounds (or rejected) with no server error and no full-table scan.
2. **Given** requests behind the edge proxy, **When** the telemetry limiter partitions them, **Then** it uses the verified client identity, not the shared edge address.
3. **Given** an endpoint without a specific limiter, **When** it is called, **Then** a global default limiter applies.

---

### User Story 3 - Do not expose internals; validate input; carry security headers (Priority: P3)

Deployed non-local environments do not return raw exception detail to callers, user-editable fields have length and format bounds, and responses carry a hardened set of security headers.

**Why this priority**: The deployed Dev environment (internet-reachable, real credentials) currently returns full exception detail on errors, which can leak connection-string fragments and internals; production is correctly protected. Editable owner fields accept unbounded strings and an unverified email change. Transport/security headers (HSTS, nosniff, frame options) are absent unless the edge injects them.

**Independent Test**: Trigger an error on a deployed non-local environment; the response contains a sanitized message, not a full stack trace. Submit oversized/invalid profile fields; they are rejected. Inspect responses for the required security headers.

**Acceptance Scenarios**:

1. **Given** a deployed non-local environment, **When** an unhandled error occurs, **Then** the response exposes only a sanitized type/message, not full internals (full detail remains available only in local development).
2. **Given** an owner profile update, **When** oversized or malformed fields are submitted, **Then** they are rejected by validation with clear messages.
3. **Given** any API response, **When** headers are inspected, **Then** the hardened security-header baseline is present (via the app or a verified edge configuration).

---

### Edge Cases

- The scrubbing enricher must not degrade logging throughput or drop structured fields other than the sensitive ones.
- Pagination clamping must preserve legitimate large-but-bounded queries and must not silently truncate results without signaling the applied bound.
- An owner email change requires a defined verification/identity-sync behavior (coordinated with Sub-Spec A) rather than silently diverging profile and login identity.
- Security headers added by the app must not conflict with headers set at the edge (avoid duplicate/contradictory policies).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-C1**: The PII-scrubbing enricher MUST be registered in the production logging pipeline so that all configured sensitive fields are redacted in every log sink, and an automated test MUST assert its presence and effect.
- **FR-C2**: All collection/list endpoints MUST validate and clamp pagination parameters to safe bounds (minimum page, maximum page size) and MUST NOT allow page arithmetic to overflow or trigger a server error.
- **FR-C3**: The telemetry proxy rate limiter MUST partition by the trusted-edge-resolved client identity, consistent with the authentication limiter, so it is effective behind the edge proxy.
- **FR-C4**: A global default rate limit MUST apply to endpoints lacking a specific policy, including anonymous registration.
- **FR-C5**: Deployed non-local environments MUST NOT return full exception detail to callers; only a sanitized type/message is exposed, with full detail restricted to local development. Production behavior (already sanitized) MUST be preserved.
- **FR-C6**: User-editable profile fields MUST enforce maximum length and appropriate format validation (e.g., phone number format), and an owner email change MUST follow a defined verification/identity-sync path rather than an unverified direct update.
- **FR-C7**: API responses MUST carry a hardened security-header baseline (at minimum content-type-options and, where applicable, transport-security and frame options), provided by **application-level middleware (repo-controlled)** and asserted by an automated test. *(Clarified 2026-07-02: headers are set in repo-controlled app config and tested, not relied upon from the edge/dashboard; the edge may add headers additionally but is not the source of truth.)*
- **FR-C8**: Privileged fields MUST remain non-editable via profile update endpoints (no over-posting); this existing protection MUST be preserved and covered by a test.

### Key Entities

- **Log event**: Now passes through the registered scrubbing enricher before reaching any sink.
- **Pagination parameters**: Bounded page and page-size inputs on list endpoints.
- **Client identity (telemetry)**: The trusted-edge-resolved identity used for rate-limit partitioning.
- **Owner profile**: Editable resident-contact fields with enforced bounds and a verified email-change path.

### Security & Abuse Controls *(constitution subset)*

- **Observability**: Sensitive-data exclusion is enforced in logs as well as traces; environment/release tagging preserved; no PII in exported telemetry.
- **Security and abuse controls**: Pagination clamping and comprehensive rate limiting bound resource use; error detail is not exposed outside local development; untrusted profile input is length- and format-validated.
- **API contract**: Collection responses honor pagination defaults/max limits; error shape is sanitized outside local development.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-C1**: 0 occurrences of a configured sensitive field in cleartext across emitted log events in testing; an automated test fails if the scrubbing enricher is unregistered.
- **SC-C2**: 0 server errors and 0 full-table materializations for out-of-range pagination inputs across list endpoints in testing.
- **SC-C3**: The telemetry limiter and a global default limiter demonstrably attribute and bound requests correctly behind the edge proxy, verified by test.
- **SC-C4**: Deployed non-local environments return no raw stack traces on error, verified by test; production remains sanitized.
- **SC-C5**: Oversized/malformed profile inputs are rejected in 100% of tested cases; privileged fields remain non-editable.
- **SC-C6**: The security-header baseline is present on responses, set by repo-controlled application middleware and verified by an automated test.

## Assumptions

- The redaction field set is the one already defined and tested in the codebase; this sub-spec wires it in rather than redefining it.
- "Deployed non-local environment" includes the internet-reachable Dev environment; local developer machines may still see full exception detail.
- Per the 2026-07-02 clarification, the security-header baseline is set by repo-controlled application middleware and asserted in tests; any edge-added headers are supplementary, not the source of truth.
- The owner email-change verification path is coordinated with Sub-Spec A's identity handling.
