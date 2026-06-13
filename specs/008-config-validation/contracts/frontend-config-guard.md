# Contract: Frontend Boot-Time Configuration Guard

No HTTP surface. The contract is the **bootstrap behavior** of the Angular app.

## Behavioral contract

| # | Given | When | Then |
|---|-------|------|------|
| F1 | `production === true` and `stripePublishableKey` empty | app starts | bootstrap is **halted**; a minimal static full-page error naming the missing value is rendered into the app root; the app does not load (FR-017, US4-1). |
| F2 | `production === true` and `apiBaseUrl` empty | app starts | bootstrap halted; full-page error names the missing value (FR-017, US4-2). |
| F3 | `production === true` and all required values present | app starts | app boots normally (US4-3). |
| F4 | `production === false` (dev defaults) | app starts | guard does not run / never blocks (FR-018, US4-4). |
| F5 | Multiple required values empty | app starts | error lists all missing values. |

## Surface

```typescript
// runtime-config.validator.ts — pure, unit-testable
export interface RuntimeConfig {
  production: boolean;
  apiBaseUrl: string;
  stripePublishableKey: string;
}
/** Returns the names of missing required values; only enforced in production. */
export function findMissingRequiredConfig(cfg: RuntimeConfig): string[];

// main.ts — guard runs BEFORE bootstrapApplication
const missing = findMissingRequiredConfig(environment);
if (missing.length > 0) {
  renderConfigError(missing);            // injects full-page DOM error; no bootstrap
} else {
  bootstrapApplication(AppComponent, appConfig).catch(err => console.error(err));
}
```

## Test contract (Jasmine/Karma)

- `findMissingRequiredConfig` returns `[]` for complete production config and for any
  non-production config; returns the missing key names when production values are empty.
- `renderConfigError` produces a perceivable DOM message containing the missing key name(s)
  (accessibility: rendered text, not console-only).
