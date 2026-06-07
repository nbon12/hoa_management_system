export const environment = {
  production: true,
  apiBaseUrl: 'https://api.nekohoa.com/api/v1',
  telemetryUrl: 'https://api.nekohoa.com/api/v1/telemetry',
  propagateTraceHeaderCorsUrls: ['https://api.nekohoa.com'],
  // Set at deploy time (pk_live_... in production).
  stripePublishableKey: '',
};
