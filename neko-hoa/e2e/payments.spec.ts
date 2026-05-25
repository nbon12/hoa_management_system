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
    await page.goto('/app/payments/one-time');
  });

  test('step 1 shows "How much?" heading', async ({ page }) => {
    await expect(page.getByText(/How much/i)).toBeVisible();
  });

  test('step 1 shows amount preset cards', async ({ page }) => {
    await expect(page.getByText('Current')).toBeVisible();
    await expect(page.getByText('Next due')).toBeVisible();
    await expect(page.getByText('Both')).toBeVisible();
  });

  test('selecting a preset and clicking Continue advances to step 2', async ({ page }) => {
    await page.getByText('Current').click();
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByText(/Step 2/i)).toBeVisible({ timeout: 5_000 });
  });

  test('step 2 shows Credit card and eCheck method options', async ({ page }) => {
    await page.getByText('Current').click();
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByText(/Credit card/i)).toBeVisible();
    await expect(page.getByText(/eCheck/i)).toBeVisible();
  });

  test('full ACH flow results in "Payment submitted!" confirmation', async ({ page }) => {
    // Wait for balance to load so presets have amounts
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 10_000 },
    );

    // Step 1: pick "Next due" preset ($250 assessment - always > 0 regardless of ledger state)
    await page.getByText('Next due').click();
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByText(/Step 2/i)).toBeVisible({ timeout: 5_000 });

    // Step 2: select eCheck and fill ACH fields
    await page.getByText(/eCheck/i).click();

    // Fill routing number and account number (input.mono inputs visible after eCheck selected)
    const monoInputs = page.locator('input.mono');
    await expect(monoInputs.first()).toBeVisible({ timeout: 5_000 });
    await monoInputs.first().fill('021000021');  // routing number
    await monoInputs.nth(1).fill('987654321');   // account number

    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByText(/Step 3/i)).toBeVisible({ timeout: 5_000 });

    // Step 3: review — submit
    await page.getByRole('button', { name: /Submit payment/i }).click();

    // Step 4: confirmation
    await expect(page.getByText(/Payment submitted/i)).toBeVisible({ timeout: 15_000 });
  });

  test('confirmation step shows a confirmation number', async ({ page }) => {
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 10_000 },
    );
    // Use "Next due" ($250) to ensure amount is always > 0 regardless of current balance
    await page.getByText('Next due').click();
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByText(/Step 2/i)).toBeVisible({ timeout: 5_000 });
    await page.getByText(/eCheck/i).click();
    const monoInputs = page.locator('input.mono');
    await expect(monoInputs.first()).toBeVisible({ timeout: 5_000 });
    await monoInputs.first().fill('021000021');
    await monoInputs.nth(1).fill('987654321');
    await page.getByRole('button', { name: /Continue/i }).click();
    // Wait for step 3 to render before submitting
    await expect(page.getByText(/Step 3/i)).toBeVisible({ timeout: 5_000 });
    await page.getByRole('button', { name: /Submit payment/i }).click();
    await expect(page.getByText(/Payment submitted/i)).toBeVisible({ timeout: 15_000 });
    const body = await page.locator('app-one-time').textContent();
    expect(body).toMatch(/[A-Z0-9]{6,}/);
  });
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
    await expect(page.getByText(/Auto-pay/i)).toBeVisible();
  });

  test('READ: draft history table has entries', async ({ page }) => {
    await expect(page.getByText(/Drafts/i)).toBeVisible();
    const rows = page.locator('.data-table tbody tr');
    await expect(rows.first()).toBeVisible({ timeout: 10_000 });
  });

  test('READ: enrolled toggle or pill is shown', async ({ page }) => {
    const enrolledPill = page.locator('.pill').filter({ hasText: /Enrolled/i });
    const toggleOn = page.locator('.toggle--on');
    await expect(enrolledPill.or(toggleOn).first()).toBeVisible({ timeout: 10_000 });
  });

  test('UPDATE: change draft day to 15th and save', async ({ page }) => {
    const draftDaySelect = page.locator('select.field');
    await draftDaySelect.selectOption('15');
    await page.getByRole('button', { name: /Save changes/i }).click();
    await expect(page.locator('.alert--success')).toBeVisible({ timeout: 10_000 });
  });

  test('UPDATE: restore draft day to 1st', async ({ page }) => {
    const draftDaySelect = page.locator('select.field');
    await draftDaySelect.selectOption('1');
    await page.getByRole('button', { name: /Save changes/i }).click();
    await expect(page.locator('.alert--success')).toBeVisible({ timeout: 10_000 });
  });

  test('DELETE: cancel recurring payment via Turn off button', async ({ page }) => {
    page.on('dialog', async (dialog) => dialog.accept());
    const turnOffBtn = page.getByRole('button', { name: /Turn off/i });
    if (await turnOffBtn.isVisible()) {
      await turnOffBtn.click();
      await page.waitForTimeout(2_000);
      // After cancellation toggle is off or re-enrollment UI is shown
      const toggleOff = page.locator('.toggle:not(.toggle--on)');
      const saveBtn = page.getByRole('button', { name: /Save changes/i });
      await expect(toggleOff.or(saveBtn).first()).toBeVisible({ timeout: 10_000 });
    } else {
      test.skip();
    }
  });

  test('CREATE: re-enroll with ACH and save', async ({ page }) => {
    // Ensure toggle is on
    const toggleOff = page.locator('.toggle:not(.toggle--on)').first();
    if (await toggleOff.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await toggleOff.click();
    }
    // Pick "Just the assessment"
    const assessmentOption = page.locator('.radio').filter({ hasText: /Just the assessment/i });
    if (await assessmentOption.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await assessmentOption.click();
    }
    // Bank ACH method
    const achBtn = page.getByRole('button', { name: /Bank/i });
    if (await achBtn.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await achBtn.click();
    }
    // Fill routing and account
    const monoInputs = page.locator('input.mono');
    const inputCount = await monoInputs.count();
    if (inputCount >= 1) await monoInputs.first().fill('021000021');
    if (inputCount >= 2) await monoInputs.nth(1).fill('987654321');
    // Draft day 1st
    const draftSelect = page.locator('select.field');
    if (await draftSelect.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await draftSelect.selectOption('1');
    }
    await page.getByRole('button', { name: /Save changes/i }).click();
    await expect(page.locator('.alert--success')).toBeVisible({ timeout: 15_000 });
  });
});
