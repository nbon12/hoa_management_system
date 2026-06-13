// <!-- REPOWISE:START domain=configuration -->
// Boot-time guard for required frontend runtime configuration (008-config-validation,
// FR-017/FR-018). In production builds a missing API base URL or Stripe publishable key must
// fail loudly at startup rather than surfacing later in the browser (e.g. mid-checkout).
// <!-- REPOWISE:END -->

/** The subset of environment values the app requires to function at startup. */
export interface RuntimeConfig {
  production: boolean;
  apiBaseUrl: string;
  stripePublishableKey: string;
}

/**
 * Returns the names of required configuration values that are missing or blank. Enforced only
 * for production builds (FR-018) — development relies on local defaults and is never blocked.
 */
export function findMissingRequiredConfig(config: RuntimeConfig): string[] {
  if (!config.production) {
    return [];
  }

  const required: Array<keyof RuntimeConfig> = ['apiBaseUrl', 'stripePublishableKey'];
  return required.filter((key) => {
    const value = config[key];
    return typeof value !== 'string' || value.trim().length === 0;
  });
}
