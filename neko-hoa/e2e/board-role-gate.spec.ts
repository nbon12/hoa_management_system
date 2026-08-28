import { test, expect, type Page } from '@playwright/test';
import { establishSession } from './helpers/auth';

/**
 * Deep-link to a protected route, tolerating a KNOWN product race that is out of scope here.
 *
 * `authGuard` (core/guards/auth.guard.ts) is synchronous — it redirects to /login whenever
 * `auth.isLoggedIn()` is false at the instant the route resolves. Session re-hydration happens in
 * an APP_INITIALIZER (core/services/session-refresh.ts) that awaits the silent refresh, but when
 * that refresh does not resolve into a session (transient failure), a cold-boot deep link lands on
 * /login and `boardGuard` never gets to decide. That behaviour lives in authGuard, which spec 025
 * does not touch; it is recorded as its own decision rather than fixed here.
 *
 * Each attempt is ONE cold boot — deliberately not a warm-up navigation, which would add a second
 * boot and double the exposure to the same race. On a bounce we re-establish and retry once, then
 * give up loudly. The role-gating assertions in the tests below are unchanged and still strict.
 */
async function deepLinkExpectingRedirect(page: Page, path: string): Promise<void> {
  for (let attempt = 1; attempt <= 2; attempt++) {
    await establishSession(page);
    await page.goto(path);
    // Settles either on an /app page (guard decided) or back on /login (the boot race).
    await page.waitForURL(/\/app\/|\/login/, { timeout: 15_000 });
    if (!/\/login/.test(page.url())) return;
  }
  throw new Error(
    `Deep link to ${path} bounced to /login on both attempts — the session never hydrated, ` +
    'so the role gate was never exercised (see the authGuard/silent-refresh race above).',
  );
}

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
    // Direct navigation to a board route the resident role cannot use.
    await deepLinkExpectingRedirect(page, '/app/board/home');

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
    await deepLinkExpectingRedirect(page, '/app/board/memberships');
    await expect(page).toHaveURL(/\/app\/dashboard/, { timeout: 10_000 });
    await expect(page).not.toHaveURL(/\/app\/board\//);
    await expect(page.getByText(/Hi /i)).toBeVisible();
  });
});
