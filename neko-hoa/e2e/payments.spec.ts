import { test, expect } from '@playwright/test';

// ─── Statement (GET /payments/ledger) ────────────────────────────────────────

test.describe('Statement', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/app/payments/statement');
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );
  });

  test('page title contains "Account statement"', async ({ page }) => {
    await expect(page.getByText('Account statement')).toBeVisible();
  });

  test('ledger table has at least 10 rows (18 months seed data)', async ({ page }) => {
    const rows = page.locator('.data-table tbody tr');
    await expect(rows.nth(9)).toBeVisible({ timeout: 10_000 });
  });

  test('"Balance" footer label is shown', async ({ page }) => {
    // Footer row has three .muted spans: Total charges, Total payments, Balance
    await expect(page.locator('.muted').filter({ hasText: /^Balance$/ })).toBeVisible();
  });

  test('"Make payment" link navigates to one-time payment', async ({ page }) => {
    await page.getByRole('link', { name: 'Make payment' }).click();
    await expect(page).toHaveURL(/\/app\/payments\/one-time/);
  });

  test('search box filters rows to matching text', async ({ page }) => {
    const search = page.locator('input[placeholder*="Search ledger"]');
    await search.fill('Assessment');
    await page.waitForTimeout(400);
    const rows = page.locator('.data-table tbody tr');
    const count = await rows.count();
    expect(count).toBeGreaterThan(0);
    for (let i = 0; i < Math.min(count, 3); i++) {
      await expect(rows.nth(i).getByText(/Assessment/i)).toBeVisible();
    }
  });

  test('type filter "Payment" shows only payment rows', async ({ page }) => {
    await page.locator('select.field').selectOption('Payment');
    await page.waitForTimeout(400);
    const rows = page.locator('.data-table tbody tr');
    const count = await rows.count();
    expect(count).toBeGreaterThan(0);
    // Type column renders as a .pill with text "payment" (lowercase) for Payment entries
    for (let i = 0; i < Math.min(count, 3); i++) {
      await expect(rows.nth(i).locator('td:first-child .pill')).toContainText('payment');
    }
  });

  test('clearing search restores full ledger', async ({ page }) => {
    const search = page.locator('input[placeholder*="Search ledger"]');
    await search.fill('Assessment');
    await page.waitForTimeout(400);
    const filteredCount = await page.locator('.data-table tbody tr').count();

    await search.clear();
    await page.waitForTimeout(400);
    const allCount = await page.locator('.data-table tbody tr').count();
    expect(allCount).toBeGreaterThanOrEqual(filteredCount);
  });
});

// ─── One-time payment (POST /payments/one-time) ───────────────────────────────

test.describe('One-time payment wizard', () => {
  test.beforeEach(async ({ page }) => {
    // Preset amounts are populated from /payments/options; until it resolves they default to 0,
    // and Continue refuses to advance (amount must be > 0). Wait for the response so the wizard
    // is interactive before tests select a preset.
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/payments/options'), { timeout: 15_000 }).catch(() => null),
      page.goto('/app/payments/one-time'),
    ]);
  });

  test('step 1 shows "How much?" heading', async ({ page }) => {
    await expect(page.getByText(/How much/i)).toBeVisible();
  });

  test('step 1 shows amount preset cards', async ({ page }) => {
    // Scope to the preset cards' data-preset attributes — the labels ("Current", "Both")
    // also appear elsewhere on the page, so getByText matches multiple elements.
    await expect(page.locator('[data-preset="current"]')).toBeVisible();
    await expect(page.locator('[data-preset="next"]')).toBeVisible();
    await expect(page.locator('[data-preset="both"]')).toBeVisible();
  });

  test('selecting a preset and clicking Continue advances to step 2', async ({ page }) => {
    await page.locator('[data-preset="current"]').click();
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByText(/Step 2/i)).toBeVisible({ timeout: 5_000 });
  });

  test('step 2 shows Credit card and eCheck method options', async ({ page }) => {
    await page.locator('[data-preset="current"]').click();
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByText(/Credit card/i)).toBeVisible();
    await expect(page.getByText(/eCheck/i)).toBeVisible();
  });

  // Full card entry + confirmation drives the Stripe-hosted iframe and so lives in
  // `payment-element.spec.ts` (T093, local-only). Raw routing/account inputs were removed in Split 3
  // (SC-001) — no card/bank number is ever typed into an Angular-owned field here.
});

// ─── Recurring payment CRUD ───────────────────────────────────────────────────

test.describe.serial('Recurring payment CRUD', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/app/payments/recurring');
    // Wait for ngOnInit to complete: loadRecurring() then getDrafts()
    // Draft history rows appear only after both API calls finish
    await expect(page.locator('.data-table tbody tr').first()).toBeVisible({ timeout: 15_000 });
  });

  test('READ: page shows "Auto-pay" title', async ({ page }) => {
    // "Auto-pay" also appears in the saved-alert and the history caption; match the page heading.
    await expect(page.getByRole('heading', { name: /Auto-pay/i })).toBeVisible();
  });

  test('READ: draft history table has entries', async ({ page }) => {
    await expect(page.getByText(/Drafts/i)).toBeVisible();
    const rows = page.locator('.data-table tbody tr');
    await expect(rows.first()).toBeVisible({ timeout: 10_000 });
  });

  test('SETUP: opening setup reveals amount type, draft day, mandate and a Stripe-mounted method', async ({ page }) => {
    await page.getByTestId('setup-toggle').click();

    // Amount type + draft day are Angular-owned (non-sensitive) and editable.
    await expect(page.getByTestId('draft-day')).toBeVisible({ timeout: 10_000 });
    await page.getByTestId('draft-day').selectOption('15');

    // The mandate gate and the Save button exist; Save stays disabled until the method + mandate are ready.
    await expect(page.getByTestId('mandate-checkbox')).toBeVisible();
    await expect(page.getByTestId('save')).toBeDisabled();
  });

  test('SETUP: card/bank details are collected only inside the Stripe iframe (SC-001)', async ({ page }) => {
    await page.getByTestId('setup-toggle').click();
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );
    // The payment method is a cross-origin Stripe iframe — there is no Angular-owned routing/account input.
    await expect(page.locator('app-recurring').locator('input.mono')).toHaveCount(0);
    await expect(page.locator('iframe[title="Secure payment input frame"]')).toBeVisible({ timeout: 20_000 });
  });

  test('DELETE: Turn off cancels auto-pay when enrolled', async ({ page }) => {
    page.on('dialog', async (dialog) => dialog.accept());
    const turnOffBtn = page.getByTestId('turn-off');
    if (await turnOffBtn.isVisible().catch(() => false)) {
      await turnOffBtn.click();
      // Once disabled, the page offers to set up auto-pay again.
      await expect(page.getByTestId('setup-toggle')).toBeVisible({ timeout: 10_000 });
    } else {
      test.skip();
    }
  });

  // Vaulting a method end-to-end (SetupIntent confirm + mandate) drives the Stripe iframe and lives in
  // `payment-element.spec.ts` (T093, local-only).
});
