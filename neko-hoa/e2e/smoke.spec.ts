import { test, expect } from '@playwright/test';

/**
 * Post-deploy smoke gate (014 US2). A small, fast, deterministic, READ-ONLY set of
 * deployment-health checks tagged @smoke — "is this deployment healthy?", not the full
 * local regression suite. The post-deploy gate runs `playwright test --grep @smoke`.
 *
 * Curation rules (contracts/smoke-gate.md): every check here is read-only and leaves NO
 * persistent mutation — no registration (claims a property), no auto-pay toggle (durably
 * disables seed enrollment), no poll-vote / RSVP / payment submission. Those remain in the
 * full suite only. The set still fails loudly on genuine breakage: portal/login not
 * rendering, auth wiring broken, or key authenticated pages not loading.
 */

const NO_AUTH = { cookies: [], origins: [] } as const;

// ─── Anonymous deployment-health (no auth) ──────────────────────────────────

test.describe('Smoke: public pages render', { tag: '@smoke' }, () => {
  test.use({ storageState: NO_AUTH });

  // 020-D FR-D2 / SC-D2: the deployed frontend must serve the enforcing CSP with the stamped
  // API origin — an unstamped or missing policy is a deployment defect.
  test('deployed response carries the enforcing CSP with a stamped API origin', async ({ page }) => {
    const response = await page.goto('/portal');
    const csp = response?.headers()['content-security-policy'] ?? '';

    expect(csp).toContain("default-src 'self'");
    expect(csp).toContain('https://js.stripe.com');
    expect(csp).not.toContain('__API_ORIGIN__');
    // connect-src must pin a concrete https API origin (Dev or the per-PR service).
    expect(csp).toMatch(/connect-src [^;]*https:\/\/[a-z0-9.-]+/);
  });

  test('portal renders its cards', async ({ page }) => {
    await page.goto('/portal');
    await expect(page.locator('.card').filter({ hasText: 'Resident' }).first()).toBeVisible();
  });

  test('login page renders email, password, and Sign in', async ({ page }) => {
    await page.goto('/login');
    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="password"]')).toBeVisible();
    await expect(page.getByRole('button', { name: /Sign in/i })).toBeVisible();
  });

  test('auth guard redirects unauthenticated users to /login', async ({ page }) => {
    await page.goto('/app/dashboard');
    await expect(page).toHaveURL(/\/login/);
  });
});

// ─── Authenticated deployment-health (seed-user storage state) ──────────────
// Uses the storageState captured by global-setup (a seed-user login — token issuance only,
// no business-data mutation). These are read-only page loads.

test.describe('Smoke: authenticated shell renders', { tag: '@smoke' }, () => {
  test('dashboard renders greeting and the Balance stat card', async ({ page }) => {
    await page.goto('/app/dashboard');
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );
    await expect(page.getByText(/Hi /i)).toBeVisible();
    await expect(page.locator('.field-label').filter({ hasText: /^Balance$/ })).toBeVisible();
  });

  test('statement page loads', async ({ page }) => {
    await page.goto('/app/payments/statement');
    await expect(page).toHaveURL(/\/app\/payments\/statement/);
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );
  });

  test('one-time payment page is reachable', async ({ page }) => {
    await page.goto('/app/payments/one-time');
    await expect(page).toHaveURL(/\/app\/payments\/one-time/);
  });
});
