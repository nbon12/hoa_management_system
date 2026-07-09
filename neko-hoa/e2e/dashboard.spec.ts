import { test, expect } from '@playwright/test';
import { establishSession } from './helpers/auth';

test.describe('Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await establishSession(page);
    await page.goto('/app/dashboard');
    // Wait for spinner to clear
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );
  });

  test('shows user first-name greeting', async ({ page }) => {
    await expect(page.getByText(/Hi /i)).toBeVisible();
  });

  test('renders the Balance stat card', async ({ page }) => {
    await expect(page.locator('.field-label').filter({ hasText: /^Balance$/ })).toBeVisible();
  });

  test('renders the Violations stat card', async ({ page }) => {
    await expect(page.locator('.field-label').filter({ hasText: /^Violations$/ })).toBeVisible();
  });

  test('renders the Documents stat card', async ({ page }) => {
    await expect(page.locator('.field-label').filter({ hasText: /^Documents$/ })).toBeVisible();
  });

  test('renders the Next event stat card', async ({ page }) => {
    await expect(page.locator('.field-label').filter({ hasText: /^Next event$/ })).toBeVisible();
  });

  test('shows "Recent activity" section with ledger rows', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Recent activity/ })).toBeVisible();
    const rows = page.locator('.data-table tbody tr');
    await expect(rows.first()).toBeVisible({ timeout: 10_000 });
  });

  test('shows "Pinned announcement" section', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Pinned announcement/ })).toBeVisible();
  });

  test('shows "This week" section', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /This week/ })).toBeVisible();
  });

  test('shows "My community" expenses section', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /My community/ })).toBeVisible();
  });

  test('shows "Quick links" section', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Quick links/ })).toBeVisible();
  });

  test('shows "Payment due" CTA or "paid up" indicator for balance', async ({ page }) => {
    // Balance may be zero if prior test runs made payments; accept either state
    const paymentDue = page.getByText(/Payment due/i);
    const paidUp = page.locator('.pill--ok').filter({ hasText: /paid up/i });
    const indicator = paymentDue.or(paidUp.first());
    await expect(indicator.first()).toBeVisible({ timeout: 10_000 });
  });

  test('"View ledger" link navigates to statement page', async ({ page }) => {
    await page.getByRole('link', { name: /View ledger/i }).click();
    await expect(page).toHaveURL(/\/app\/payments\/statement/);
  });

  test('"Pay now" link or one-time payment page is accessible', async ({ page }) => {
    const payNow = page.getByRole('link', { name: /Pay now/i });
    if (await payNow.isVisible()) {
      await payNow.click();
      await expect(page).toHaveURL(/\/app\/payments\/one-time/);
    } else {
      // Balance is paid off; verify the one-time payment page is still accessible
      await page.goto('/app/payments/one-time');
      await expect(page).toHaveURL(/\/app\/payments\/one-time/);
    }
  });
});
