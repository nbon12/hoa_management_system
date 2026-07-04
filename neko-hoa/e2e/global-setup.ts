import { chromium } from '@playwright/test';
import { SEED_EMAIL, SEED_PASSWORD } from './helpers/auth';

async function globalSetup() {
  const apiBase = process.env.PLAYWRIGHT_API_URL || 'http://localhost:5212';
  const frontendBase = process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:4200';

  // Clean up any test users registered by previous E2E runs so the registration
  // test can run against an unclaimed SAKURA-003 property each time.
  // 017-A FR-A6: the endpoint requires the environment's scheduler shared secret. CI passes
  // PLAYWRIGHT_SCHEDULER_SECRET (fetched from Secret Manager); the fallback matches the
  // committed local-dev placeholder in appsettings.Development.json.
  const schedulerSecret =
    process.env.PLAYWRIGHT_SCHEDULER_SECRET || 'dev-scheduler-shared-secret-placeholder';
  try {
    const res = await fetch(`${apiBase}/api/v1/e2e/cleanup`, {
      method: 'DELETE',
      headers: { 'X-Scheduler-Secret': schedulerSecret },
    });
    if (!res.ok) {
      // Loud but non-fatal: a 401 here means the secret is wrong/missing and seed state will
      // accumulate, eventually breaking the registration test — surface it in the CI log.
      console.warn(`[global-setup] e2e cleanup returned ${res.status} — seed state NOT reset`);
    }
  } catch {
    // API unreachable or no test users exist — non-fatal
  }

  const browser = await chromium.launch();
  const context = await browser.newContext({ baseURL: frontendBase });
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
