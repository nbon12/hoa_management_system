import { test, expect, type Page } from '@playwright/test';
import { establishSession } from './helpers/auth';

/**
 * 029 FR-027 / constitution §6 — every resident ARC page is usable at phone, tablet and desktop widths:
 * no horizontal page scroll, and the primary actions are visible and clickable. Read-only against the
 * seeded stack: it relies on ArchitecturalSeeder's resident demo ("Front-yard xeriscape", an open request
 * with an unanswered board question), opens the withdraw dialog but cancels it, and saves nothing.
 */

const VIEWPORTS = [
  { name: 'phone', width: 375, height: 812 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'desktop', width: 1280, height: 800 },
];

async function signedIn(page: Page, path: string): Promise<void> {
  for (let attempt = 1; attempt <= 2; attempt++) {
    await establishSession(page);
    await page.goto(path);
    await page.waitForURL(/\/app\/|\/login/, { timeout: 15_000 });
    if (!/\/login/.test(page.url())) return;
  }
}

async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  expect(overflow, 'page should not scroll horizontally').toBeLessThanOrEqual(0);
}

for (const vp of VIEWPORTS) {
  test.describe(`at ${vp.name} width (${vp.width}px)`, () => {
    test.use({ viewport: { width: vp.width, height: vp.height } });

    test('list, form, detail with a question, and the withdraw dialog fit and stay usable', async ({ page }) => {
      await signedIn(page, '/app/property/architectural');
      await expect(page.getByRole('heading', { name: 'My architectural requests' })).toBeVisible();
      await expectNoHorizontalScroll(page);

      await page.goto('/app/property/architectural/new');
      await expect(page.getByRole('button', { name: 'Save draft' })).toBeVisible();
      await expect(page.getByRole('button', { name: /Submit request/ })).toBeVisible();
      await expectNoHorizontalScroll(page);

      await page.goto('/app/property/architectural');
      await page.getByRole('link', { name: /Front-yard xeriscape/ }).first().click();
      await expect(page.getByLabel('Your reply')).toBeVisible();
      await expect(page.getByRole('button', { name: /Send reply/ })).toBeVisible();
      await expectNoHorizontalScroll(page);

      await page.getByRole('button', { name: 'Withdraw request' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog.getByRole('button', { name: 'Keep request' })).toBeVisible();
      await expect(dialog.getByRole('button', { name: /Withdraw request/ })).toBeVisible();
      await expectNoHorizontalScroll(page);
      await dialog.getByRole('button', { name: 'Keep request' }).click();
      await expect(page.getByRole('dialog')).toHaveCount(0);
    });
  });
}
