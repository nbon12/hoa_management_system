import { test, expect } from '@playwright/test';
import { establishSession } from './helpers/auth';

// ─── Property Info (GET /property) ───────────────────────────────────────────

test.describe('Property Info', () => {
  test.beforeEach(async ({ page }) => {
    await establishSession(page);
    await page.goto('/app/property/info');
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );
  });

  test('page title contains "Property info"', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /Property.*info/i })).toBeVisible();
  });

  test('account number is displayed', async ({ page }) => {
    // Account number shown in the shell strip and/or in the property details grid
    const acct = page.locator('.mono').first();
    await expect(acct).toBeVisible();
    const text = await acct.textContent();
    expect(text?.trim().length).toBeGreaterThan(0);
  });

  test('"Assessment rules" section is shown', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Assessment rules/i })).toBeVisible();
  });

  test('assessment rules table has Regular assessment, Late fee, and Finance charge rows', async ({ page }) => {
    await expect(page.getByText(/Regular assessment/i)).toBeVisible();
    await expect(page.locator('td').filter({ hasText: /^Late fee$/ })).toBeVisible();
    await expect(page.getByText(/Finance charge/i)).toBeVisible();
  });

  test('"Property details" section is shown with lot and year built', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Property details/i })).toBeVisible();
    await expect(page.getByText('Lot')).toBeVisible();
    await expect(page.getByText(/Year built/i)).toBeVisible();
  });

  test('"Late fee timeline" section is shown', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Late fee timeline/i })).toBeVisible();
  });

  test('"Request changes" button is visible', async ({ page }) => {
    await expect(page.getByRole('button', { name: /Request changes/i })).toBeVisible();
  });
});

// ─── Owner Profile (GET + PATCH /property/owner) ─────────────────────────────

test.describe.serial('Owner Profile', () => {
  test.beforeEach(async ({ page }) => {
    await establishSession(page);
    await page.goto('/app/property/owner');
    // Wait for ngOnInit to complete by checking address history table has data
    // (ngOnInit runs Promise.all([getProperty, getOwner, getAddressHistory]))
    await expect(page.locator('.data-table tbody tr').first()).toBeVisible({ timeout: 15_000 });
  });

  test('READ: page title is "Owner profile"', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Owner profile' })).toBeVisible();
  });

  test('READ: Account details section is shown', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Account details/i })).toBeVisible();
  });

  test('READ: "Edit profile" button is visible', async ({ page }) => {
    await expect(page.getByRole('button', { name: /Edit profile/i })).toBeVisible();
  });

  test('READ: address history table has at least one entry', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Address history/i })).toBeVisible();
    const rows = page.locator('.data-table tbody tr');
    await expect(rows.first()).toBeVisible({ timeout: 10_000 });
  });

  test('READ: mailing preferences section is shown', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Mailing preferences/i })).toBeVisible();
  });

  test('UPDATE: edit first name to "Janet", save, verify', async ({ page }) => {
    await page.getByRole('button', { name: /Edit profile/i }).click();
    // Wait for editing mode (Save changes button appears only when editing is true)
    await expect(page.getByRole('button', { name: /Save changes/i })).toBeVisible({ timeout: 5_000 });
    // First input.field is firstName (Angular [name] binding doesn't set HTML attribute)
    const firstNameInput = page.locator('input.field').first();
    await expect(firstNameInput).toBeVisible({ timeout: 5_000 });
    await firstNameInput.clear();
    await firstNameInput.fill('Janet');
    await page.getByRole('button', { name: /Save changes/i }).click();
    await expect(page.locator('.alert--success')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText(/Janet/i).first()).toBeVisible();
  });

  test('UPDATE teardown: restore first name to "Jane"', async ({ page }) => {
    await page.getByRole('button', { name: /Edit profile/i }).click();
    await expect(page.getByRole('button', { name: /Save changes/i })).toBeVisible({ timeout: 5_000 });
    const firstNameInput = page.locator('input.field').first();
    await expect(firstNameInput).toBeVisible({ timeout: 5_000 });
    await firstNameInput.clear();
    await firstNameInput.fill('Jane');
    await page.getByRole('button', { name: /Save changes/i }).click();
    await expect(page.locator('.alert--success')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Jane').first()).toBeVisible();
  });
});

// ─── Directory (GET + PATCH /property/directory-fields) ──────────────────────

test.describe.serial('Directory', () => {
  test.beforeEach(async ({ page }) => {
    await establishSession(page);
    await page.goto('/app/property/directory');
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );
  });

  test('READ: "Directory" page title is shown', async ({ page }) => {
    await expect(page.getByRole('heading', { name: /Directory/i })).toBeVisible();
  });

  test('READ: "What I share" table is shown', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /What I share/i })).toBeVisible();
  });

  test('READ: "Nothing is shared by default" banner is visible', async ({ page }) => {
    await expect(page.getByText(/Nothing is shared by default/i)).toBeVisible();
  });

  test('READ: "Privacy promise" section is visible', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Privacy promise/i })).toBeVisible();
  });

  test('READ: Neighbor directory section shows at least one seeded neighbor', async ({ page }) => {
    await expect(page.locator('.section-title').filter({ hasText: /Neighbor Directory/i })).toBeVisible();
    // Seeded neighbors are at addresses like "3 Sakura Drive"
    const neighborRows = page.locator('table tr').filter({ hasText: /Sakura Drive/i });
    await expect(neighborRows.first()).toBeVisible({ timeout: 10_000 });
  });

  test('UPDATE: toggle "Phone" field ON and verify active state', async ({ page }) => {
    const phoneRow = page.locator('tr').filter({ hasText: /^.*Phone.*$/ });
    const toggle = phoneRow.locator('.toggle').first();
    await expect(toggle).toBeVisible();
    const wasOn = await toggle.evaluate((el) => el.classList.contains('toggle--on'));
    if (!wasOn) {
      await toggle.click();
      await page.waitForTimeout(1_000);
      await expect(toggle).toHaveClass(/toggle--on/);
    } else {
      await expect(toggle).toHaveClass(/toggle--on/);
    }
  });

  test('UPDATE teardown: toggle "Phone" field OFF', async ({ page }) => {
    const phoneRow = page.locator('tr').filter({ hasText: /^.*Phone.*$/ });
    const toggle = phoneRow.locator('.toggle').first();
    await expect(toggle).toBeVisible();
    const isOn = await toggle.evaluate((el) => el.classList.contains('toggle--on'));
    if (isOn) {
      await toggle.click();
      await page.waitForTimeout(1_000);
      await expect(toggle).not.toHaveClass(/toggle--on/);
    }
  });
});
