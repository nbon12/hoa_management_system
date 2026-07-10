import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 45_000,
  // Cold per-PR environments (scale-to-zero Cloud Run + fresh Neon branch) render data-heavy
  // pages slower than local; give assertions headroom over the 5s default.
  expect: { timeout: 15_000 },
  retries: 1,
  reporter: [['list'], ['html', { open: 'never' }]],
  globalSetup: require.resolve('./e2e/global-setup'),
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:4200',
    // 020-D: no shared storageState — strict one-time-use refresh rotation means a shared
    // cookie snapshot dies after the first context refreshes. Authenticated specs establish
    // their own session per test via helpers/auth.establishSession.
    video: 'retain-on-failure',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
