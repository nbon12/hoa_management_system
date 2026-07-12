/// <reference types="cypress" />

// End-to-end happy path for the auto-pay (recurring) setup wizard (Split 4 / T058).
//
// Same two seams as the one-time suite keep this hermetic:
//  1. cy.intercept stubs every backend call (recurring → drafts → setup-intent → save), so no live
//     API is needed and the masked method / next draft shown are exactly what the server "returned".
//  2. window.Stripe is replaced before the app boots, so ngx-stripe reuses our fake instead of
//     loading js.stripe.com. The fake never renders a real card iframe — vaulting card/bank details
//     in-iframe is covered by the Playwright suite (Split 5); here we assert the wizard orchestration
//     and that no raw instrument data ever leaves the browser (SC-001).

const NOT_ENROLLED = { statusCode: 204, body: '' };

const ENROLLED = {
  id: 'rec1', amountType: 'fixed', fixedAmount: 150, method: 'card', draftDay: 5,
  status: 'active', processingFee: 1.95, maskedMethod: 'Visa •••• 4242',
  nextDraftDate: '2026-07-05', nextDraftAmount: 151.95, mandateAcceptedAt: '2026-06-07T00:00:00Z',
};

const SETUP = { setupIntentId: 'seti_test_1', clientSecret: 'seti_test_1_secret', publishableKey: 'pk_test_x' };

const DRAFTS = {
  items: [
    { id: 'd1', draftDate: '2026-06-05', sourceLabel: 'Visa •••• 4242', amount: 151.95, status: 'Paid', transactionStatus: 'Succeeded' },
  ],
  totalCount: 1, limit: 50, offset: 0,
};

// A no-op Stripe Element: any method access returns a function that does nothing.
function fakeElement() {
  return new Proxy({}, { get: () => () => undefined });
}

function fakeStripe() {
  const elements = {
    create: () => fakeElement(),
    getElement: () => fakeElement(),
    update: () => undefined,
    fetchUpdates: () => Promise.resolve(),
    submit: () => Promise.resolve({}),
  };
  return {
    elements: () => elements,
    // ngx-stripe's StripeInstance constructor calls registerAppInfo on the native Stripe object;
    // without it the loader subscription throws and the elements stream never emits.
    registerAppInfo: () => undefined,
    // The method is vaulted in-browser; the backend save is stubbed separately.
    confirmSetup: () => Promise.resolve({ setupIntent: { status: 'succeeded' } }),
  };
}

const REFRESH_SESSION = {
  token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'u1', firstName: 'Nico', lastName: 'Tester', email: 'nico@example.com',
    initials: 'NT', properties: [],
  },
};

function seedAuthAndStripe(win: Window) {
  // 020-D FR-D1: sessions re-hydrate via the hint-gated silent refresh — the hint below
  // makes APP_INITIALIZER call /auth/refresh, which the intercept in beforeEach answers.
  win.localStorage.setItem('neko_has_session', '1');
  (win as unknown as { Stripe: () => unknown }).Stripe = () => fakeStripe();
}

describe('Auto-pay setup (Stripe SetupIntent + Payment Element)', () => {
  beforeEach(() => {
    cy.intercept('POST', '**/auth/refresh', { statusCode: 200, body: REFRESH_SESSION }).as('refresh');
    // Scope to the API base path (…/api/v1/…) so these globs don't hijack the SPA navigation
    // to /app/payments/recurring — the document load must reach the dev server, not an intercept.
    cy.intercept('POST', '**/api/*/payments/recurring/setup-intent', { statusCode: 200, body: SETUP }).as('setupIntent');
    cy.intercept('GET', '**/api/*/payments/recurring', NOT_ENROLLED).as('getRecurring');
    cy.intercept('PUT', '**/api/*/payments/recurring', { statusCode: 200, body: ENROLLED }).as('save');
    cy.intercept('GET', '**/api/*/payments/drafts*', { statusCode: 200, body: DRAFTS }).as('drafts');
    // The recurring page now embeds the payment-alerts section, which loads its own prefs.
    cy.intercept('GET', '**/api/*/payments/alert-preferences',
      { statusCode: 200, body: { smsOptIn: false, emailOptIn: false, alertPhone: null } }).as('alertPrefs');
    cy.intercept('POST', '**/telemetry', { statusCode: 200, body: {} });
  });

  it('enrolls via preset → SetupIntent Element → mandate → active status card', () => {
    cy.visit('/app/payments/recurring', { onBeforeLoad: seedAuthAndStripe });

    // Initial load: not enrolled (204) → the set-up CTA is shown, no enrolled pill.
    cy.wait('@getRecurring');
    cy.get('[data-testid="enrolled-pill"]').should('not.exist');
    cy.get('[data-testid="setup-toggle"]').click();

    // Entering setup creates the SetupIntent and mounts the Payment Element.
    cy.wait('@setupIntent');
    cy.get('ngx-stripe-payment').should('exist');

    // Pick a fixed amount + draft day.
    cy.get('[data-amount-type="fixed"]').click();
    cy.get('[data-testid="fixed-amount"]').clear().type('150');
    cy.get('[data-testid="draft-day"]').select('5');

    // Mandate must be accepted before save is enabled (FR-009).
    cy.get('[data-testid="save"]').should('be.disabled');
    cy.get('[data-testid="mandate-checkbox"]').check();
    cy.get('[data-testid="save"]').should('not.be.disabled').click();

    // The backend save carries the vaulted setupIntentId + explicit mandate — never raw card data.
    cy.wait('@save').its('request.body').should('deep.include', {
      amountType: 'fixed', fixedAmount: 150, draftDay: 5, setupIntentId: 'seti_test_1', mandateAccepted: true,
    });

    // Status card flips to active with the masked method + next draft from the server.
    cy.get('[data-testid="enrolled-pill"]').should('exist');
    cy.get('[data-testid="status-state"]').should('contain', 'Active');
    cy.get('[data-testid="masked-method"]').should('contain', 'Visa •••• 4242');
    cy.get('[data-testid="saved"]').should('be.visible');
  });

  it('never sends raw card or bank fields to the backend (SC-001)', () => {
    cy.visit('/app/payments/recurring', { onBeforeLoad: seedAuthAndStripe });
    cy.wait('@getRecurring');

    cy.get('[data-testid="setup-toggle"]').click();
    cy.wait('@setupIntent');

    // No legacy raw-instrument inputs anywhere in the setup form.
    cy.contains('Card number').should('not.exist');
    cy.contains('Routing number').should('not.exist');
    cy.get('input[placeholder*="4242"]').should('not.exist');

    cy.get('[data-amount-type="fixed"]').click();
    cy.get('[data-testid="fixed-amount"]').clear().type('150');
    cy.get('[data-testid="mandate-checkbox"]').check();
    cy.get('[data-testid="save"]').click();

    cy.wait('@save').then(({ request }) => {
      // The only fields ever sent are the enrollment settings + vault reference + mandate — no PAN/account.
      expect(Object.keys(request.body).sort()).to.deep.equal(
        ['amountType', 'draftDay', 'fixedAmount', 'mandateAccepted', 'mandateText', 'mandateVersion', 'setupIntentId']);
    });
  });
});
