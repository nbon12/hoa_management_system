import type { Page } from '@playwright/test';

export const SEED_EMAIL = 'resident@nekohoa.dev';
export const SEED_PASSWORD = 'Password1!';

export async function loginAs(
  page: Page,
  email = SEED_EMAIL,
  password = SEED_PASSWORD,
): Promise<void> {
  await page.goto('/login');
  await page.locator('input[name="email"]').fill(email);
  await page.locator('input[name="password"]').fill(password);
  await page.getByRole('button', { name: /Sign in/i }).click();
  await page.waitForURL('**/app/dashboard', { timeout: 15_000 });
}
