import { defineConfig } from 'cypress';

// One-time payment happy-path E2E. The app is served (dev config) on :4200; the backend and
// Stripe.js are both stubbed in-spec, so this exercises the Angular wizard end-to-end without a
// live API or real card iframe (in-iframe entry is deferred to the Playwright suite — Split 5/T093).
export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:4200',
    specPattern: 'cypress/e2e/**/*.cy.ts',
    supportFile: false,
    fixturesFolder: false,
    video: false,
    screenshotOnRunFailure: false,
  },
});
