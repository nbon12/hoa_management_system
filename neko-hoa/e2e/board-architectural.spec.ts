import { test, expect, type Page } from '@playwright/test';
import { establishSession, BOARD_EMAIL, BOARD_PASSWORD } from './helpers/auth';

/**
 * 027 — Board architectural review in a real browser against the seeded stack.
 *
 * Read-only on purpose: the seed (ArchitecturalSeeder) is shared across runs, so these tests never cast
 * a vote — the vote journey runs stubbed in Cypress (cypress/e2e/board-architectural.cy.ts).
 *
 *  - US1-S8: "Given I hold only a Resident membership, When I navigate directly to the Architectural
 *    Applications route, Then I am refused and redirected to a permitted page."
 *  - US1 Independent Test / FR-002–FR-005: a board member opens the page and sees the tabs, search and table.
 *  - US4-S5: choosing Request info shows that questions don't pause the review period.
 *  - Accessibility (constitution, T074): tabs, search and row actions are keyboard-reachable and labeled.
 */

async function deepLinkExpectingRedirect(page: Page, path: string): Promise<void> {
  // Same cold-boot tolerance as board-role-gate.spec.ts (authGuard / silent-refresh race).
  for (let attempt = 1; attempt <= 2; attempt++) {
    await establishSession(page);
    await page.goto(path);
    await page.waitForURL(/\/app\/|\/login/, { timeout: 15_000 });
    if (!/\/login/.test(page.url())) return;
  }
  throw new Error(`Deep link to ${path} bounced to /login on both attempts.`);
}

async function openAsBoardMember(page: Page): Promise<void> {
  // Same single retry as deepLinkExpectingRedirect for the documented cold-boot bounce to /login.
  for (let attempt = 1; attempt <= 2; attempt++) {
    // No setMode: boardGuard checks memberships, not mode, and board-mode.spec.ts asserts the shared
    // user's persisted mode — this spec must not change it.
    await establishSession(page, BOARD_EMAIL, BOARD_PASSWORD);
    await page.goto('/app/board/architectural');
    await page.waitForURL(/\/app\/|\/login/, { timeout: 15_000 });
    if (!/\/login/.test(page.url())) break;
  }
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Architectural', { timeout: 15_000 });
}

test.describe('Board architectural review (027)', () => {
  test('a resident-only user is refused the route and redirected (US1-S8)', async ({ page }) => {
    await deepLinkExpectingRedirect(page, '/app/board/architectural');
    await expect(page).toHaveURL(/\/app\/dashboard/, { timeout: 10_000 });
    await expect(page.locator('.ap__table')).toHaveCount(0);
  });

  test('a board member sees tabs, search and the FR-002 table', async ({ page }) => {
    await openAsBoardMember(page);
    await expect(page.getByRole('tab', { name: /^Open · \d+$/ })).toHaveAttribute('aria-selected', 'true');
    await expect(page.getByRole('tab', { name: /^Closed · \d+$/ })).toBeVisible();
    await expect(page.getByRole('searchbox', { name: 'Search address or owner' })).toBeVisible();
    await expect(page.getByRole('columnheader')).toHaveText(
      ['ID', 'Property', 'Project', 'Attachments', 'Due', 'Board votes', 'Your vote']); // textContent; CSS uppercases
    await expect(page.getByRole('img', { name: /approve · \d+ revisions needed · \d+ deny · \d+ not voted/ }).first()).toBeVisible();
  });

  test('tabs and search are keyboard-operable', async ({ page }) => {
    await openAsBoardMember(page);
    const closed = page.getByRole('tab', { name: /^Closed · \d+$/ });
    await closed.focus();
    await page.keyboard.press('Enter');
    await expect(closed).toHaveAttribute('aria-selected', 'true');
    const search = page.getByRole('searchbox', { name: 'Search address or owner' });
    await search.focus();
    await expect(search).toBeFocused();
  });

  test('Request info explains that questions do not pause the review period (US4-S5)', async ({ page }) => {
    await openAsBoardMember(page);
    // The seed leaves ARC-1042 unvoted by board@nekohoa.dev, and nothing in this suite votes.
    const row = page.getByRole('row').filter({ hasText: 'ARC-1042' }).first();
    await row.getByRole('button', { name: 'Info', exact: true }).click();
    await expect(page.getByLabel('Comment to the board')).toBeFocused();
    await expect(page.getByRole('note')).toContainText("Questions don't pause the review period (due ");
    await expect(page.getByRole('note')).toContainText('vote Revisions needed');
  });
});
