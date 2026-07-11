import { defineConfig } from 'cypress';

// One-time payment happy-path E2E. The app is served (dev config) on :4200; the backend and
// Stripe.js are both stubbed in-spec, so this exercises the Angular wizard end-to-end without a
// live API or real card iframe (in-iframe entry is deferred to the Playwright suite — Split 5/T093).
export default defineConfig({
  e2e: {
    // baseUrl is overridable via the CYPRESS_BASE_URL env var so the same suite can run against a
    // local `ng serve` (CI happy-path) or the deployed Dev preview URL as the promotion gate
    // (009-dev-auto-deploy / `npm run e2e:dev`). Cypress also honors CYPRESS_BASE_URL natively.
    baseUrl: process.env['CYPRESS_BASE_URL'] || 'http://localhost:4200',
    specPattern: 'cypress/e2e/**/*.cy.ts',
    // Deployed previews serve lazy chunks over the network with cold caches; the 4s default
    // intermittently times out on first element lookups after a visit (seen on pr-env).
    defaultCommandTimeout: 10_000,
    // cy.wait('@alias') uses requestTimeout, not defaultCommandTimeout — the first page load of
    // a spec pays cold chunk + silent-refresh before data requests fire (seen on pr-env).
    requestTimeout: 15_000,
    supportFile: false,
    fixturesFolder: false,
    video: false,
    screenshotOnRunFailure: false,
  },
});
