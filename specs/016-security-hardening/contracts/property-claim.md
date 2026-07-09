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
- **Behaviour**: bind the new user to a property **only** if `claimCode` matches a live, single-use `PropertyClaimCode` for that property (hashed, constant-time compare, not expired, attempt-limited). Account-number matching alone is no longer sufficient.
- **Response 201**: session (see `auth-session.md`). Generic failures do not reveal whether the property exists/claimable.
- **Errors**: uniform generic errors for not-found / already-claimed / bad-code; each attempt increments `AttemptCount`.

## Claim-code issuance (out-of-band)
- Codes are generated per property and delivered to the owner's contact channel on file (email/SMS/mail); 90-day, single-use. Delivery is an internal/admin/seeding operation, not a public endpoint. A missing deliverable contact is an operational precondition (owner data quality), not an in-app admin-approval bypass.

## Security-event logging
- Claim attempts (success/failure), email-verification requests/confirms, and lockout events are logged as security-sensitive events (Constitution §7) **with scrubbed PII** (email `[REDACTED]` per Sub-spec C) and no raw codes.
