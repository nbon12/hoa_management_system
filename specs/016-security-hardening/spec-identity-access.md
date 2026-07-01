# Sub-Spec A: Backend Identity & Access Hardening

**Feature Branch**: `016-security-hardening`
**Parent**: [`spec.md`](./spec.md)
**Created**: 2026-07-01
**Status**: Draft

## Overview

The authentication and account-provisioning flows contain a High-severity business-logic flaw that lets an attacker claim a residence they do not own, plus several Medium/Low weaknesses that enable online password guessing, account enumeration, and an unauthenticated destructive maintenance endpoint. Data-access authorization itself was reviewed and found sound (access is scoped by server-trusted token claims, not client-supplied IDs) — this sub-spec hardens **how identities are established, protected, and de-authorized**, not how data is scoped.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Prevent property-claim takeover (Priority: P1)

A prospective resident registers and is linked to their unit only after proving they are entitled to it. An attacker who guesses or enumerates unit/account identifiers cannot bind their own login to a unit they do not own.

**Why this priority**: Today registration links a new user to a property by matching a **sequential, guessable account number**, guarded only by a "not already claimed" check, on an endpoint with **no rate limiting**. An attacker can enumerate account numbers and claim any still-unclaimed unit before the real owner registers, gaining full resident access (ledger, statements, payment history, owner PII, directory). This is the single highest-impact application finding.

**Independent Test**: Attempt to register against a valid-but-unowned account identifier without possessing the out-of-band proof; registration is refused. Attempt rapid enumeration of identifiers; requests are throttled and responses do not distinguish "exists/unclaimed" from "does not exist".

**Acceptance Scenarios**:

1. **Given** a valid account number the caller does not own, **When** the caller attempts to register/claim it without the required out-of-band proof, **Then** the claim is denied.
2. **Given** an attacker submitting many account numbers, **When** they probe the claim endpoint, **Then** they are rate-limited and receive a uniform response that does not reveal whether a given unit exists or is claimable.
3. **Given** a legitimate resident with valid proof of entitlement, **When** they register, **Then** they are linked to exactly their own property.

---

### User Story 2 - Resist online password guessing (Priority: P2)

Repeated failed login attempts against an account trigger escalating lockout, so an attacker cannot brute-force or credential-stuff a known email regardless of how the requests are distributed across source addresses.

**Why this priority**: Login currently verifies the password directly, bypassing the lockout-aware sign-in path, so there is **no per-account failure counter**. The only throttle is IP/edge-based, which a botnet trivially spreads around. Login itself does not leak whether an email exists (good), but the missing lockout leaves the door open to distributed guessing.

**Independent Test**: Submit repeated failed logins for one account from varied sources; after the configured threshold the account is locked for the configured window, and correct credentials during lockout are still refused.

**Acceptance Scenarios**:

1. **Given** an account, **When** the failed-attempt threshold is exceeded, **Then** further authentication attempts are refused until the lockout window elapses, independent of source IP.
2. **Given** a locked account, **When** the correct password is supplied during the lockout window, **Then** authentication is still refused.

---

### User Story 3 - Remove enumeration and unauthenticated destructive surface (Priority: P2)

Registration does not reveal whether an email is already registered, and the end-to-end test cleanup capability cannot be invoked by an unauthenticated caller even if a configuration flag is mistakenly enabled.

**Why this priority**: Registration returns a distinct "email taken" response, forming a fast enumeration oracle on an unthrottled endpoint. Separately, a destructive bulk-delete maintenance endpoint is anonymous and gated only by a single boolean; one misconfiguration exposes unauthenticated mass deletion.

**Independent Test**: Probe registration with known and unknown emails; responses are indistinguishable. Attempt to invoke the cleanup endpoint without the required secret; it is refused even when the enable-flag is on.

**Acceptance Scenarios**:

1. **Given** an email already registered, **When** a caller attempts registration, **Then** the response is indistinguishable from the unregistered-email case.
2. **Given** the cleanup capability is enabled, **When** it is invoked without the required shared secret, **Then** it is refused.
3. **Given** the cleanup capability, **When** it is invoked in a production-like environment, **Then** it is unavailable regardless of configuration.

---

### User Story 4 - Harden tokens and claim handling (Priority: P3)

Access tokens are validated against a pinned signing algorithm with tight clock tolerance; logout/property-switch de-authorizes prior access promptly enough for the platform's risk tolerance; and requests bearing a structurally valid token that is missing required claims fail cleanly rather than crashing.

**Why this priority**: Defense-in-depth. Algorithm is not currently pinned (limited impact given symmetric keys), access tokens remain valid for their full lifetime after logout/property-switch, and null-forgiving claim reads produce server errors instead of clean authorization failures.

**Independent Test**: Present a token signed with a non-approved algorithm — it is rejected. Present a valid-signature token lacking a required claim — the response is a clean authorization failure, not a server error.

**Acceptance Scenarios**:

1. **Given** a token asserting an unapproved signing algorithm, **When** it is presented, **Then** it is rejected.
2. **Given** an authenticated principal whose token lacks a required claim, **When** it calls a claim-scoped endpoint, **Then** it receives a clean authorization error, not a server error.

---

### Edge Cases

- A legitimate resident who genuinely cannot receive the out-of-band proof (e.g., no mailing address on file) needs a defined fallback (e.g., manual administrator approval) so the anti-takeover control does not lock out real owners.
- Lockout must not become a denial-of-service against a victim: define whether lockout is per-account, and how a locked-out legitimate user recovers.
- Property-switch must not leave a previously issued token usable against the prior property beyond the platform's accepted revocation window.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-A1**: The system MUST require out-of-band proof of entitlement (e.g., a mailed/emailed one-time claim code, or administrator approval) before binding a user identity to a property. Possession of an account number MUST NOT by itself be sufficient to claim a property.
- **FR-A2**: The registration/claim endpoint MUST be rate-limited per client, with the client identity resolved by the same trusted-edge mechanism used by the login limiter (so limiting is effective behind the edge proxy).
- **FR-A3**: The registration/claim endpoint MUST return responses that do not distinguish "account exists and is claimable", "account already claimed", and "account does not exist" to an unauthenticated caller.
- **FR-A4**: Login MUST enforce per-account lockout after a configurable number of failed attempts, independent of source IP, using the lockout-aware authentication path.
- **FR-A5**: Registration MUST NOT reveal whether a submitted email is already registered.
- **FR-A6**: The end-to-end cleanup capability MUST require a shared secret (constant-time compared) in addition to any enable flag, and MUST be unavailable in production-like environments regardless of configuration.
- **FR-A7**: Token validation MUST pin the accepted signing algorithm set and apply a tight clock-skew tolerance.
- **FR-A8**: Endpoints MUST read required identity claims defensively and return a clean authorization error (not a server error) when a required claim is absent.
- **FR-A9**: Committed dev/test signing secrets MUST be removed from source control in favor of developer-local secret provisioning; production MUST continue to source its signing secret from the managed secret store and fail startup if absent.
- **FR-A10**: Logout and property-switch behavior for already-issued access tokens MUST be documented and, if the platform requires stronger revocation, access-token lifetime or a revocation mechanism MUST be adjusted to meet the stated window.

### Key Entities

- **Property claim**: The binding between a user identity and a property, now requiring proof of entitlement.
- **Claim proof**: An out-of-band, single-use credential (or administrator approval) that authorizes a claim.
- **Failed-login counter**: Per-account state driving lockout.
- **Signing key / algorithm policy**: The pinned algorithm set and key source used to validate tokens.

### Security & Abuse Controls *(constitution subset)*

- **Authorization**: Property binding and all authenticated actions enforce server-side checks; the property-claim proof is server-verified and single-use.
- **Security and abuse controls**: Rate limiting on registration/claim and login; per-account lockout; uniform responses to defeat enumeration; anonymous destructive endpoints require a secret and are environment-gated.
- **Auditability**: Property-claim attempts (success and failure), lockout events, and cleanup invocations are recorded as sensitive security events (without logging the secrets themselves).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-A1**: An attacker with only guessed account numbers and no out-of-band proof can claim 0 properties in testing.
- **SC-A2**: Enumeration of registration or claim endpoints yields no response signal that distinguishes existing from non-existing accounts (verified by comparing responses/latencies in tests).
- **SC-A3**: After the configured number of failed logins, 100% of further attempts on that account are refused during the lockout window, regardless of source IP.
- **SC-A4**: The cleanup capability is unreachable without the shared secret and unreachable in production-like environments, verified by automated tests.
- **SC-A5**: Tokens using an unapproved algorithm and requests missing required claims are rejected/handled cleanly with no server errors, verified by automated tests.

## Assumptions

- An out-of-band channel exists to deliver a claim proof (the platform already sends email/SMS notifications), or an administrator-approval fallback is acceptable.
- The existing trusted-edge client-identity resolution is reused for the new rate limits rather than inventing a new mechanism.
- The 15-minute access-token lifetime is the current baseline; whether it must shrink depends on the revocation window chosen in FR-A10.
- Data-access authorization (tenant/property scoping by token claims) is already correct and is out of scope for this sub-spec except for the defensive claim-read hardening (FR-A8).
