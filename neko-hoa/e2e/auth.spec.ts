import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

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
  // Uses the global storageState (already authenticated)
  test('redirects to /login; protected routes redirect again', async ({ page }) => {
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

  test('page renders stepper and account number field', async ({ page }) => {
    await page.goto('/register');
    await expect(page.getByText('find property')).toBeVisible();
    await expect(page.locator('input[name="accountNum"]')).toBeVisible();
  });

  test('creates a new account and lands on dashboard', async ({ page }) => {
    const unique = `e2e+${Date.now()}@test.dev`;
    await page.goto('/register');

    // Step 1: look up by account number (lookup is mocked — any input triggers "found")
    await page.locator('input[name="accountNum"]').fill('SAKURA-003');
    await page.getByRole('button', { name: /Find my property/i }).click();

    // Step 2: confirm property card appears (mocked 700ms delay)
    await expect(page.getByText(/Yes, that.*me/i)).toBeVisible({ timeout: 5_000 });
    await page.getByText(/Yes, that.*me/i).click();

    // Step 3: create login
    await page.locator('input[name="firstName"]').fill('E2E');
    await page.locator('input[name="lastName"]').fill('Test');
    await page.locator('input[name="email"]').fill(unique);
    await page.locator('input[name="password"]').fill('Password1!');
    await page.getByRole('button', { name: /Create account/i }).click();

    await expect(page).toHaveURL(/\/app\/dashboard/, { timeout: 15_000 });
  });
});
