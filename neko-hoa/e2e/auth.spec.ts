import { test, expect } from '@playwright/test';
import { establishSession, loginAs } from './helpers/auth';

// Auth tests do NOT use the global storageState — they test the auth flow itself
const NO_AUTH = { cookies: [], origins: [] } as const;

// ─── Portal ──────────────────────────────────────────────────────────────────

test.describe('Portal', () => {
  test.use({ storageState: NO_AUTH });

  test('shows five portal cards', async ({ page }) => {
    await page.goto('/portal');
    await expect(page.locator('.card').filter({ hasText: 'Resident' }).first()).toBeVisible();
    await expect(page.locator('.card').filter({ hasText: 'Board' }).first()).toBeVisible();
    await expect(page.locator('.card').filter({ hasText: 'Vendor' }).first()).toBeVisible();
    await expect(page.locator('.card').filter({ hasText: 'Closing' }).first()).toBeVisible();
    await expect(page.locator('.card').filter({ hasText: 'Attorney' }).first()).toBeVisible();
  });

  test('root / redirects to /portal', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveURL(/\/portal/);
  });
});

// ─── Login page ──────────────────────────────────────────────────────────────

test.describe('Login page', () => {
  test.use({ storageState: NO_AUTH });

  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
  });

  test('renders email and password fields', async ({ page }) => {
    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="password"]')).toBeVisible();
  });

  test('renders Sign in button', async ({ page }) => {
    await expect(page.getByRole('button', { name: /Sign in/i })).toBeVisible();
  });

  test('shows error on invalid credentials', async ({ page }) => {
    // Use a non-existent email to avoid triggering rate limit on the seed user
    await page.locator('input[name="email"]').fill('nobody@invalid.example.com');
    await page.locator('input[name="password"]').fill('badpassword');
    await page.getByRole('button', { name: /Sign in/i }).click();
    await expect(page.locator('.alert--error')).toBeVisible({ timeout: 10_000 });
  });
});

// ─── Auth guard ───────────────────────────────────────────────────────────────

test.describe('Auth guard', () => {
  test.use({ storageState: NO_AUTH });

  test('unauthenticated visit to /app/dashboard redirects to /login', async ({ page }) => {
    await page.goto('/app/dashboard');
    await expect(page).toHaveURL(/\/login/, { timeout: 10_000 });
  });

  test('unauthenticated visit to /app/payments/statement redirects to /login', async ({ page }) => {
    await page.goto('/app/payments/statement');
    await expect(page).toHaveURL(/\/login/, { timeout: 10_000 });
  });
});

// ─── Login + Logout ───────────────────────────────────────────────────────────

test.describe('Login with valid credentials', () => {
  test.use({ storageState: NO_AUTH });

  test('lands on dashboard and shows user greeting', async ({ page }) => {
    await loginAs(page);
    await expect(page).toHaveURL(/\/app\/dashboard/);
    await expect(page.getByText(/Hi /i)).toBeVisible();
  });
});

test.describe('Logout', () => {
  test('redirects to /login; protected routes redirect again', async ({ page }) => {
    await establishSession(page);
    await page.goto('/app/dashboard');
    await expect(page.getByRole('button', { name: /Sign out/i })).toBeVisible({ timeout: 10_000 });
    await page.getByRole('button', { name: /Sign out/i }).click();
    await expect(page).toHaveURL(/\/login/, { timeout: 10_000 });

    // Visiting a protected route now should redirect back to login
    await page.goto('/app/dashboard');
    await expect(page).toHaveURL(/\/login/, { timeout: 10_000 });
  });
});

// ─── Register ────────────────────────────────────────────────────────────────

test.describe('Register', () => {
  test.use({ storageState: NO_AUTH });

  test('page renders the verified-registration stepper (no account-number path)', async ({ page }) => {
    await page.goto('/register');
    await expect(page.getByText('verify email')).toBeVisible();
    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="accountNum"]')).toHaveCount(0);
  });

  // 020-D FR-D9/FR-D11: the real verified flow. Codes are stored hashed server-side, so the
  // test obtains them via the e2e test-support seams, gated exactly like /e2e/cleanup
  // (DevTools flag + X-Scheduler-Secret; absent in Production/Staging).
  test('creates a new account via verification + claim code and lands on dashboard', async ({ page, request }) => {
    const apiBase = process.env.PLAYWRIGHT_API_URL || 'http://localhost:5212';
    const schedulerSecret =
      process.env.PLAYWRIGHT_SCHEDULER_SECRET || 'dev-scheduler-shared-secret-placeholder';
    const headers = { 'X-Scheduler-Secret': schedulerSecret };
    const unique = `e2e+${Date.now()}@test.dev`;

    // Obtain a fresh claim code for the seed property (supersedes any live one).
    const claimRes = await request.post(`${apiBase}/api/v1/e2e/claim-code`, { headers });
    expect(claimRes.ok()).toBeTruthy();
    const { claimCode } = await claimRes.json();

    // Step 1: request the verification code for a unique e2e address.
    await page.goto('/register');
    await page.locator('input[name="email"]').fill(unique);
    await page.getByRole('button', { name: /Send code/i }).click();

    // Step 2: fetch the delivered code from the gated vault and enter it.
    await expect(page.locator('input[name="code"]')).toBeVisible({ timeout: 10_000 });
    const codesRes = await request.get(
      `${apiBase}/api/v1/e2e/auth-codes?contact=${encodeURIComponent(unique)}`, { headers });
    expect(codesRes.ok()).toBeTruthy();
    const { verificationCode } = await codesRes.json();
    await page.locator('input[name="code"]').fill(verificationCode);
    await page.getByRole('button', { name: /Verify/i }).click();

    // Step 3: create the login with the claim code.
    await expect(page.locator('input[name="firstName"]')).toBeVisible({ timeout: 10_000 });
    await page.locator('input[name="firstName"]').fill('E2E');
    await page.locator('input[name="lastName"]').fill('Test');
    await page.locator('input[name="password"]').fill('Password1!');
    await page.locator('input[name="claimCode"]').fill(claimCode);
    await page.getByRole('button', { name: /Create account/i }).click();

    await expect(page).toHaveURL(/\/app\/dashboard/, { timeout: 15_000 });
  });
});
