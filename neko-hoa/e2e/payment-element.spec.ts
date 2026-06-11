import { test, expect, FrameLocator, Page } from '@playwright/test';

/**
 * T093 — Stripe Payment Element iframe interaction (constitution §9).
 *
 * This is the ONE browser test that actually types card details into the Stripe-hosted iframe, which
 * the Cypress happy-path deliberately stubs (it fakes `window.Stripe` so no real iframe mounts). Card
 * data lives only inside Stripe's cross-origin frame — it never touches Angular state or our API
 * (SC-001), so the only way to exercise real entry is a full browser driving the live test-mode
 * Element.
 *
 * **Run locally, not in CI.** It needs the backend up (localhost:5212), the SPA on :4200, and a real
 * Stripe **test-mode** publishable key in `environment.development.ts`. Because that requires live
 * Stripe connectivity, it is intentionally excluded from the `e2e:ci` (Cypress) gate. Run with:
 *
 *     cd neko-hoa && npm run e2e -- payment-element
 *
 * Stripe test card: 4242 4242 4242 4242 · any future expiry · any CVC · any ZIP.
 */

const TEST_CARD = '4242424242424242';
const TEST_EXP = '12 / 34';
const TEST_CVC = '123';
const TEST_ZIP = '28210';

/** The Payment Element mounts its inputs inside a Stripe iframe; resolve it once the form is ready. */
async function paymentElementFrame(page: Page): Promise<FrameLocator> {
  const frame = page.frameLocator('iframe[title="Secure payment input frame"]');
  await expect(frame.locator('[name="number"], [placeholder*="1234"]').first())
    .toBeVisible({ timeout: 20_000 });
  return frame;
}

async function fillCard(frame: FrameLocator) {
  // Field names/placeholders follow Stripe's Payment Element DOM; the number field is always present,
  // expiry/cvc/zip are filled best-effort because their presence depends on the account's field config.
  await frame.locator('[name="number"], [placeholder*="1234"]').first().fill(TEST_CARD);
  const exp = frame.locator('[name="expiry"], [placeholder*="MM"]').first();
  if (await exp.count()) await exp.fill(TEST_EXP);
  const cvc = frame.locator('[name="cvc"], [placeholder*="CVC"]').first();
  if (await cvc.count()) await cvc.fill(TEST_CVC);
  const zip = frame.locator('[name="postalCode"], [placeholder*="ZIP"], [placeholder*="Postal"]').first();
  if (await zip.count().then(c => c > 0).catch(() => false)) await zip.fill(TEST_ZIP).catch(() => {});
}

test.describe('Stripe Payment Element — one-time card (local-only)', () => {
  test('enters a card in the Stripe iframe and reaches the receipt', async ({ page }) => {
    await page.goto('/app/payments/one-time');
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );

    // Step 1: a non-zero preset so the intent has an amount.
    await page.getByText('Next due').click();
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByText(/Step 2/i)).toBeVisible({ timeout: 10_000 });

    // Step 2: card method → the Payment Element mounts; type into the Stripe iframe.
    await page.getByText(/Credit card/i).click();
    const frame = await paymentElementFrame(page);
    await fillCard(frame);

    // Server-authoritative summary must be visible on review — never recomputed client-side.
    await page.getByRole('button', { name: /Continue/i }).click();
    await expect(page.getByText(/Step 3/i)).toBeVisible({ timeout: 10_000 });
    await expect(page.getByTestId('summary-total')).toBeVisible();

    // Confirm via Stripe.js (redirect: 'if_required') then the backend records the transaction.
    await page.getByRole('button', { name: /Submit payment/i }).click();
    await expect(page.getByText(/Payment submitted/i)).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId('confirmation-number')).not.toBeEmpty();

    // SC-001 guard: the masked method only ever shows the last 4 — no full PAN anywhere on the page.
    const receipt = await page.getByTestId('receipt').textContent();
    expect(receipt ?? '').not.toContain(TEST_CARD);
  });
});

test.describe('Stripe Payment Element — recurring vaulting (local-only)', () => {
  test('enters a card in the SetupIntent Element and saves auto-pay', async ({ page }) => {
    await page.goto('/app/payments/recurring');
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );

    // Open the setup form → a SetupIntent mounts the Payment Element.
    await page.getByTestId('setup-toggle').click();
    const frame = await paymentElementFrame(page);
    await fillCard(frame);

    // Accept the mandate (required before any method is vaulted / charged off-session).
    await page.getByTestId('mandate-checkbox').check();
    await page.getByTestId('save').click();

    await expect(page.locator('.alert--success')).toBeVisible({ timeout: 30_000 });
  });
});
