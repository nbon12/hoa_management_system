export const environment = {
  production: true,
  apiBaseUrl: 'https://api-dev.nekohoa.com/api/v1',
  telemetryUrl: 'https://api-dev.nekohoa.com/api/v1/telemetry',
  propagateTraceHeaderCorsUrls: ['https://api-dev.nekohoa.com'],
  // Stripe TEST publishable key (pk_test_…) — the Dev backend runs Stripe in test mode.
  // Publishable keys are safe to ship to the browser; the matching secret key lives only in
  // the Dev backend's managed secrets (009-dev-auto-deploy).
  stripePublishableKey: 'pk_test_51TfQKLAYiK3ygWP9zjAAjTHV50P2tM2R2AmUKEKucawE6kb0Zap9hTkmGI8ld4hiqnR763SLNrsha6V9gqj2jdop00udeBIkP9',
};
