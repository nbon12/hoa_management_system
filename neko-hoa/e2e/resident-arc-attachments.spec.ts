import { test, expect, type Page } from '@playwright/test';
import { establishSession } from './helpers/auth';

/**
 * 029 T038 — US5 in a real browser against the seeded stack: a genuine PDF is accepted and listed; a text
 * file renamed .pdf is refused by content (FR-010). The draft this test creates is deleted at the end, so
 * the shared seed is left unchanged.
 */

const PDF = Buffer.from('%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n');
const FAKE_PDF = Buffer.from('This is plain text pretending to be a PDF.');

async function openNewRequest(page: Page): Promise<void> {
  for (let attempt = 1; attempt <= 2; attempt++) {
    await establishSession(page);
    await page.goto('/app/property/architectural/new');
    await page.waitForURL(/\/app\/|\/login/, { timeout: 15_000 });
    if (!/\/login/.test(page.url())) break;
  }
  await expect(page.getByRole('heading', { level: 1 })).toContainText('New architectural request');
}

test('a real PDF is attached; a renamed text file is refused by content', async ({ page }) => {
  await openNewRequest(page);
  await page.getByLabel('Project title').fill(`Playwright attachments ${Date.now()}`);

  const files = page.locator('input[type=file]');
  await files.setInputFiles({ name: 'site-plan.pdf', mimeType: 'application/pdf', buffer: PDF });
  await expect(page.getByRole('list', { name: 'Attached files' })).toContainText('site-plan.pdf');
  await expect(page).toHaveURL(/\/app\/property\/architectural\/drafts\//);

  await files.setInputFiles({ name: 'fake.pdf', mimeType: 'application/pdf', buffer: FAKE_PDF });
  await expect(page.getByRole('alert')).toContainText('fake.pdf: Only PDF, JPG, PNG and HEIC files can be attached.');
  await expect(page.getByRole('list', { name: 'Attached files' })).not.toContainText('fake.pdf');

  // Clean up: the draft and its uploaded object are deleted (FR-014).
  await page.getByRole('button', { name: 'Delete draft' }).click();
  await page.getByRole('button', { name: 'Yes, delete this draft' }).click();
  await expect(page).toHaveURL(/\/app\/property\/architectural$/);
});
