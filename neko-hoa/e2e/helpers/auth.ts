import type { Page } from '@playwright/test';

export const SEED_EMAIL = 'resident@nekohoa.dev';
export const SEED_PASSWORD = 'Password1!';

// 020-D FR-D1: sessions are cookie-based with strict one-time-use refresh rotation, so a shared
// storageState snapshot cannot work — the first context to silently refresh rotates the shared
// cookie and every other context 401s (observed live on pr-103). Instead each test context
// performs its own API login: its own cookie, its own rotation chain. The has-session hint makes
// the app re-hydrate via silent refresh on first load, exercising the real boot path.
export async function establishSession(
  page: Page,
  email = SEED_EMAIL,
  password = SEED_PASSWORD,
): Promise<void> {
  const apiBase = process.env.PLAYWRIGHT_API_URL || 'http://localhost:5212';
  // Parallel workers can burst past the per-IP auth rate limit (fixed 1-minute window);
  // back off briefly on 429 instead of flaking the test.
  let res = await page.request.post(`${apiBase}/api/v1/auth/login`, { data: { email, password } });
  for (let attempt = 0; res.status() === 429 && attempt < 3; attempt++) {
    await new Promise(r => setTimeout(r, 15_000 * (attempt + 1)));
    res = await page.request.post(`${apiBase}/api/v1/auth/login`, { data: { email, password } });
  }
  if (!res.ok()) {
    throw new Error(`establishSession: login failed with ${res.status()}`);
  }
  // page.request shares the context cookie jar, so neko_refresh is now set for the API origin.
  await page.addInitScript(() => localStorage.setItem('neko_has_session', '1'));
}

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
