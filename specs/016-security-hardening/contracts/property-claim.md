# Contract: Property Claim & Email Verification — Sub-spec A

Replaces "register binds to a property by account number" with an email-verification gate followed by a single-use claim code. No unauthenticated caller can distinguish account states before verifying email (FR-A3).

## POST /auth/verify-email/request
- **Auth**: anonymous; rate-limited.
- **Request**: `{ email }`
- **Response 202 (always the same)**: `{ status: "if_eligible_a_code_was_sent" }` — **uniform** regardless of whether the email exists/eligible (enumeration defense).
- **Effect**: if eligible, create an `EmailVerification` (purpose=`registration`), send a short-lived code to that email.

## POST /auth/verify-email/confirm
- **Auth**: anonymous; rate-limited; attempt-limited per `EmailVerification`.
- **Request**: `{ email, code }`
- **Response 200**: `{ verificationToken }` (short-lived proof of email control) on match; generic `400`/`INVALID_OR_EXPIRED` otherwise.

## POST /auth/register
- **Auth**: anonymous; requires a valid `verificationToken` from the step above; rate-limited.
- **Request**: `{ verificationToken, password, firstName, lastName, claimCode }`
- **Behaviour**: the request names no property; the claim code alone identifies it. The server looks the code up by its SHA-256 hash and binds the new user to that code's property **only** if the code is live (not redeemed, not expired). Account-number matching alone is no longer sufficient. Guessing is bounded by the `auth` rate limit and the code's entropy (10 characters from a 30-symbol alphabet).
- **Response 201**: session (see `auth-session.md`). Generic failures do not reveal whether the property exists/claimable.
- **Community status** *(added by specs/030-company-administration FR-012a, 2026-10-18)*: once the proof and the code have both been checked, a code for a property in an Onboarding community returns `409 COMMUNITY_NOT_ACTIVE`, and one in an Archived community returns `409 COMMUNITY_ARCHIVED`. The code is not used up and no account is created. Once a property has a resident by any route (claim code or invitation), its outstanding code is refused with the generic `422 REGISTRATION_FAILED` (030 FR-024f).
- **Errors**: uniform generic `422 REGISTRATION_FAILED` for not-found / already-claimed / bad-code. A wrong code matches no row, so nothing is counted against any property. `AttemptCount` is incremented only when a known code is presented after it has expired, been redeemed or reached its limit (5).

## Claim-code issuance (out-of-band)
- Codes are generated per property and delivered to the owner's contact channel on file (email/SMS/mail); 90-day, single-use. Delivery is an internal/admin/seeding operation, not a public endpoint. A missing deliverable contact is an operational precondition (owner data quality), not an in-app admin-approval bypass.
  - *Superseded by specs/030-company-administration (2026-10-18; FR-022a, FR-024, FR-024f):* a resident with no deliverable contact on file no longer depends on a claim code. A Community Manager or Company Administrator can invite them by email, and `POST /invitations/register` creates the account from the link with no claim code (`specs/030-company-administration/contracts/invitations-and-auth.md`). Claim codes still work, but only while registering a new account and only for a property with no linked resident.

## Security-event logging
- Claim attempts (success/failure), email-verification requests/confirms, and lockout events are logged as security-sensitive events (Constitution §7) **with scrubbed PII** (email `[REDACTED]` per Sub-spec C) and no raw codes.
