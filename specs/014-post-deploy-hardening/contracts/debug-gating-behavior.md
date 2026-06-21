# Contract: Config-gated debug behavior (US3)

Verified by `HOAManagementCompany.Tests/Unit/Configuration/DebugGatingTests.cs` — exercises the resolution logic directly (`DevToolsOptions.ApplyEnvironmentDefaults` and `ObservabilityOptions.FromConfiguration`) across environments and config overrides; deterministic, no host/DB needed.

## Exception detail (`GlobalExceptionHandler`)

Response shape is unchanged: `{ code, message, detail }`. Only `detail` population is gated.

| # | Environment | `DevTools:ExposeExceptionDetail` | Expected `detail` | Maps to |
|---|-------------|----------------------------------|-------------------|---------|
| DG-1 | `Dev` (deployed) | unset (default) | populated (`exception.ToString()`) | US3 #1; SC-006 |
| DG-2 | `Development` (local) | unset (default) | populated | US3 #1 |
| DG-3 | `Production` | unset | `null` | US3 #2; SC-007 |
| DG-4 | `Production` | `true` (attempted override) | `null` (hard invariant wins) | US3 #2; FR-009/SC-007 |
| DG-5 | `Dev` | `false` (explicit) | `null` | FR-008 (config wins, safe direction) |

## SQL-text capture (`ObservabilityOptions.CaptureSqlText`)

| # | Environment | `Observability:CaptureSqlText` | Expected | Maps to |
|---|-------------|-------------------------------|----------|---------|
| DG-6 | `Dev` (deployed) | unset (default) | `true` (was `false` before fix) | US3 #1; SC-006 |
| DG-7 | `Production` | unset | `false` | US3 #2; SC-007 |
| DG-8 | any | explicit value | honored (explicit wins) | FR-008 |

## Invariants

- No `detail` or SQL text in `Production` by default (FR-009, SC-007).
- Enabling in `Dev` never bypasses existing sensitive-data exclusion (`ScrubbedKeys`); secrets/PII stay redacted.
- Audit: `grep -rn "IsDevelopment()" HOAManagementCompany --include=*.cs` yields no remaining gate that *should* apply to deployed `Dev` (SC-006). Remaining hits, if any, are documented as genuinely Development-only.
