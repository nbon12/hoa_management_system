async function globalSetup() {
  const apiBase = process.env.PLAYWRIGHT_API_URL || 'http://localhost:5212';

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

  // 020-D: no storageState capture — strict one-time-use refresh rotation means a shared
  // cookie snapshot dies after the first context refreshes it. Each authenticated test
  // establishes its own session via helpers/auth.establishSession instead.
}

export default globalSetup;
