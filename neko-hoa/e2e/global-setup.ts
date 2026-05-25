import { chromium } from '@playwright/test';
import { SEED_EMAIL, SEED_PASSWORD } from './helpers/auth';

async function globalSetup() {
  // Clean up any test users registered by previous E2E runs so the registration
  // test can run against an unclaimed SAKURA-003 property each time.
  try {
    await fetch('http://localhost:5212/api/v1/e2e/cleanup', { method: 'DELETE' });
  } catch {
    // API unreachable or no test users exist — non-fatal
  }

  const browser = await chromium.launch();
  const context = await browser.newContext({ baseURL: 'http://localhost:4200' });
  const page = await context.newPage();

  await page.goto('/login');
  await page.locator('input[name="email"]').fill(SEED_EMAIL);
  await page.locator('input[name="password"]').fill(SEED_PASSWORD);
  await page.getByRole('button', { name: /Sign in/i }).click();
  await page.waitForURL('**/app/dashboard', { timeout: 30_000 });

  // Save localStorage (JWT tokens) and cookies
  await context.storageState({ path: 'e2e/.auth/state.json' });
  await browser.close();
}

export default globalSetup;
