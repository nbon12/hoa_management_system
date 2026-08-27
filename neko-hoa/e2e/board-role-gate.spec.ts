import { test, expect } from '@playwright/test';
import { establishSession } from './helpers/auth';

/**
 * 025 US3 (T040) — role-gated route refusal + redirect.
 *
 * Acceptance scenario (spec.md §US3, scenario 5): "Given I navigate directly to a route my role
 * cannot use, When the route resolves, Then I am refused and redirected to a permitted page
 * rather than shown an empty screen." (FR-028)
 *
 * The default seed user (resident@nekohoa.dev, helpers/auth SEED_EMAIL) holds no non-resident
 * membership, so its role set cannot use ANY board route — the strongest, seed-independent form
 * of the refusal case. The board guard redirects such a user to the resident dashboard, and the
 * dashboard renders (never a blank screen).
 */
test.describe('Board route role-gating (US3)', () => {
  test('a role that cannot use a board route is redirected to a permitted page, not shown a blank screen', async ({ page }) => {
    await establishSession(page);
    // Warm the session in-app before deep-linking. authGuard is synchronous, so a deep link
    // arriving before startup re-hydration has settled bounces to /login and boardGuard never
    // gets to decide — which makes this spec assert the boot race instead of the ROLE gate.
    await page.goto('/app/dashboard');
    await expect(page.locator('.avatar')).toBeVisible({ timeout: 15_000 });

    // Direct navigation to a board route the resident role cannot use.
    await page.goto('/app/board/home');

    // Refused and redirected to a permitted page (the resident dashboard), never left on the
    // board route with an empty screen.
    await expect(page).toHaveURL(/\/app\/dashboard/, { timeout: 10_000 });
    await expect(page).not.toHaveURL(/\/app\/board\//);
    // The permitted page actually rendered — not a blank screen.
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );
    await expect(page.getByText(/Hi /i)).toBeVisible();
  });

  test('a manager-only board route is likewise refused for a role that cannot use it', async ({ page }) => {
    await establishSession(page);
    // Same hydration warm-up as above — this deep link races the boot refresh identically.
    await page.goto('/app/dashboard');
    await expect(page.locator('.avatar')).toBeVisible({ timeout: 15_000 });

    await page.goto('/app/board/memberships');
    await expect(page).toHaveURL(/\/app\/dashboard/, { timeout: 10_000 });
    await expect(page).not.toHaveURL(/\/app\/board\//);
    await expect(page.getByText(/Hi /i)).toBeVisible();
  });
});
