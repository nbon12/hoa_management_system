// Per-PR ephemeral environment (013-ephemeral-pr-envs, D5). The Angular API base URL is COMPILE-TIME
// (file replacement), so the pr-env.yml workflow rewrites `apiOrigin` below to the PR's own Cloud Run
// URL before `ng build --configuration pr-preview`. The sentinel default keeps the repo building and the
// unit tests green; CI always overwrites it with the real per-PR origin.
//
// production: true so the 008 boot-time config guard is ACTIVE (a missing/blank apiBaseUrl or Stripe key
// fails loudly at startup) — same posture as Dev.
const apiOrigin = 'https://pr-env-api-origin.invalid'; // <-- replaced at build time by pr-env.yml (lowercase: real Cloud Run hosts are lowercase, and URL.origin normalizes case)

export const environment = {
  production: true,
  apiBaseUrl: `${apiOrigin}/api/v1`,
  telemetryUrl: `${apiOrigin}/api/v1/telemetry`,
  propagateTraceHeaderCorsUrls: [apiOrigin],
  // Stripe TEST publishable key — shared with Dev (the per-PR backend runs Stripe in test mode).
  stripePublishableKey: 'pk_test_51TfQKLAYiK3ygWP9zjAAjTHV50P2tM2R2AmUKEKucawE6kb0Zap9hTkmGI8ld4hiqnR763SLNrsha6V9gqj2jdop00udeBIkP9',
};
