import { environment } from '../../../environments/environment.pr-preview';
import { findMissingRequiredConfig } from './runtime-config.validator';

// 013-ephemeral-pr-envs (T024). The per-PR environment's API base URL is injected at build time by the
// pr-env.yml workflow (compile-time file replacement). These tests assert the file's shape so the
// boot-time config guard (008) behaves correctly for PR previews: a valid origin passes, and the three
// API-derived fields all track the single `apiOrigin` sentinel that CI rewrites.
//
// Lives under src/app/core/config (NOT src/environments) because tsconfig.app.json force-includes
// src/environments/**/*.ts into the production build — a spec there would break `ng build`.

describe('environment.pr-preview', () => {
  it('is a production build so the 008 boot-time config guard is active', () => {
    expect(environment.production).toBe(true);
  });

  it('passes the boot-time required-config guard (non-empty apiBaseUrl + Stripe key)', () => {
    expect(findMissingRequiredConfig(environment)).toEqual([]);
  });

  it('derives apiBaseUrl, telemetryUrl, and trace-propagation origin from one origin', () => {
    // All three must share the same origin so CI only has to rewrite one sentinel.
    const origin = new URL(environment.apiBaseUrl).origin;
    expect(environment.apiBaseUrl.endsWith('/api/v1')).toBe(true);
    expect(environment.telemetryUrl.startsWith(origin)).toBe(true);
    expect(environment.propagateTraceHeaderCorsUrls).toContain(origin);
  });

  it('ships a Stripe TEST publishable key (never a live key)', () => {
    expect(environment.stripePublishableKey.startsWith('pk_test_')).toBe(true);
  });
});
