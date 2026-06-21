# Contract: Rate-limiting behavior (US1)

Behavioral contract for the `auth` and `payments` rate-limit policies. Verified by `HOAManagementCompany.Tests/Integration/RateLimitingTests.cs` using `WebApplicationFactory` (no external infrastructure).

## Applies to

- `auth` policy: `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh` (`RequireRateLimiting("auth")`).
- `payments` policy: `POST /api/v1/payments/intent`, `POST .../confirm`, `POST .../setup-intent` (`RequireRateLimiting("payments")`).

## Partitioning

| Policy | Partition key | Source |
|--------|---------------|--------|
| `auth` | resolved client IP | `CF-Connecting-IP`, trusted only when the configured edge secret header is present and matches; else `"unknown"`. |
| `payments` | authenticated user identity | `HttpContext.User` subject/owner id; else `"unknown"`. |

## Rejection

- Over-limit requests receive **HTTP 429** (`RejectionStatusCode = 429`, unchanged).
- `QueueLimit = 0` (no queueing, unchanged). Fixed window = 1 minute.

## Required behaviors (map to acceptance scenarios)

| # | Given | When | Then | Scenario |
|---|-------|------|------|----------|
| RL-1 | Two clients A and B, each with a valid distinct trusted client IP | A exhausts its `auth` quota | A gets 429; B's requests still succeed | US1 #1, #2; SC-001/SC-002 |
| RL-2 | App configured with edge secret header | Request omits/mismatches the secret but supplies a forged `CF-Connecting-IP` | The forged value is ignored; request is attributed to `"unknown"`, not the forged IP | US1 #4; SC-003 |
| RL-3 | App configured with edge secret header | Request presents valid secret + `CF-Connecting-IP` X | Request is attributed to partition X (the true client), not the connection/proxy address | US1 #3 |
| RL-4 | Many un-attributable requests | They flood the `"unknown"` partition | Only `"unknown"` is throttled; attributable clients are unaffected | Edge case (fail-safe) |
| RL-5 | Two authenticated users behind one IP (NAT) | Each calls a payment endpoint at normal volume | Neither is throttled by the other (payments keyed by user, not IP) | NAT edge case; FR-003 |
| RL-6 | `RateLimiting:*PermitsPerMinute` overridden in config | App starts | Effective limits reflect config (no code change) | FR-004 |

## Non-goals / unchanged

- Response body of 429 is the framework default (unchanged).
- The `telemetry` policy is already partitioned and out of scope (its `RemoteIpAddress` key is acceptable for browser telemetry and unchanged by this feature).
