import { test, expect } from '@playwright/test';
import { establishSession, loginAs, BOARD_EMAIL, BOARD_PASSWORD } from './helpers/auth';

/**
 * 025 US1 (T026) — the enter/leave board-mode journey.
 *
 * Acceptance scenarios exercised (spec.md §US1):
 *   1. A board-eligible user sees an "Enter board mode" control in the top bar.
 *   3. Entering board mode shows a visually distinct banner AND a Resident/Board toggle;
 *      the shell navigation changes to the board sidebar.
 *   4. Signing out and back in returns the user to the mode they were last in.
 *
 * Seeding dependency (see helpers/auth BOARD_EMAIL): these tests require a user holding an
 * active NON-resident membership. The base AuthSeeder only creates resident-only users, so the
 * E2E target must provision `board@nekohoa.dev` (or PLAYWRIGHT_BOARD_EMAIL) with an active
 * BoardMember/CommunityManager membership — the same state the manager membership-admin flow
 * (US5) produces. A resident-only user correctly sees NO toggle, which is asserted separately in
 * the mode-toggle unit spec and in the role-gate Playwright spec.
 */
test.describe('Board mode: enter / leave journey (US1)', () => {
  test('board-eligible user can enter board mode, see the banner + board nav, and return to resident', async ({ page }) => {
    await establishSession(page, BOARD_EMAIL, BOARD_PASSWORD);
    await page.goto('/app/dashboard');
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );

    // Scenario 1: the "Enter board mode" control is present in the top bar.
    const enterBtn = page.getByRole('button', { name: /Enter board mode/i });
    await expect(enterBtn).toBeVisible();
    // Resident view intact before switching: the resident sidebar shows Dashboard.
    await expect(page.locator('.shell__side-item').filter({ hasText: /^Dashboard$/ })).toBeVisible();

    // Scenario 3: entering board mode shows the banner and the Resident/Board toggle,
    // and the shell navigation changes to the board sidebar.
    await enterBtn.click();
    await expect(page.locator('.board-banner')).toBeVisible();
    await expect(page.locator('.board-banner')).toContainText(/Board mode/i);
    await expect(page.locator('.board-banner')).toContainText(/association-wide data/i);
    // The top-bar segmented Resident/Board toggle replaces the "Enter board mode" button.
    await expect(page.locator('.mode-seg')).toBeVisible();
    await expect(page.getByRole('button', { name: /Enter board mode/i })).toHaveCount(0);
    // Board nav appeared: Community Home is the board landing entry.
    await expect(page.locator('.shell__side-item').filter({ hasText: /Community Home/i })).toBeVisible();

    // Toggle back to Resident and confirm the resident view is intact.
    await page.locator('.mode-seg__btn', { hasText: /^Resident$/ }).click();
    await expect(page.locator('.board-banner')).toHaveCount(0);
    await expect(page.getByRole('button', { name: /Enter board mode/i })).toBeVisible();
    await expect(page.locator('.shell__side-item').filter({ hasText: /^Dashboard$/ })).toBeVisible();
  });

  test('sign-out then sign-in returns to the last-used board mode (Scenario 4)', async ({ page }) => {
    // Sign in and enter board mode so Board becomes the persisted last-used mode.
    await loginAs(page, BOARD_EMAIL, BOARD_PASSWORD);
    await page.getByRole('button', { name: /Enter board mode/i }).click();
    await expect(page.locator('.board-banner')).toBeVisible();

    // Sign out.
    await page.getByRole('button', { name: /Sign out/i }).click();
    await expect(page).toHaveURL(/\/login/, { timeout: 10_000 });

    // Sign back in — the server-persisted last-used mode is Board, so the banner returns.
    await loginAs(page, BOARD_EMAIL, BOARD_PASSWORD);
    await expect(page.locator('.board-banner')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('.mode-seg')).toBeVisible();
  });
});
