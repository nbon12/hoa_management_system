export const environment = {
  production: false,
  apiBaseUrl: '/api/v1',
  // Same-origin via nginx — no cross-origin propagation needed.
  telemetryUrl: '/api/v1/telemetry',
  propagateTraceHeaderCorsUrls: [],
};
