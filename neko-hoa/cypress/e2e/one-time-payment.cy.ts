/// <reference types="cypress" />

// End-to-end happy path for the one-time payment wizard (Split 3 / T037).
//
// Two seams keep this hermetic:
//  1. cy.intercept stubs every backend call the screen makes (options → intent → confirm), so no
//     live API is needed and the fee/total shown are exactly what the server "returned".
//  2. window.Stripe is replaced before the app boots, so ngx-stripe reuses our fake instead of
//     loading js.stripe.com. The fake never renders a real card iframe — in-iframe field entry is
//     covered by the Playwright suite (Split 5/T093); here we assert the wizard orchestration.

const OPTIONS = {
  currentBalance: 300, creditBalance: 0, nextAssessment: 250, nextAssessmentDueDate: '2026-07-01',
  cardFeeType: 'Flat', cardFeeValue: 1.95, cardScope: 'All', surchargingEnabled: true, achFeeValue: 0,
};

const INTENT = {
  paymentIntentId: 'pi_test_123', clientSecret: 'pi_test_123_secret', amount: 300, fee: 1.95, total: 301.95,
};

const CONFIRMED = {
  transactionId: 't1', status: 'Succeeded', grossAmount: 300, feeAmount: 1.95, total: 301.95,
  maskedMethod: 'Visa •••• 4242', confirmationNumber: 'NEKO-ABC123', receiptId: 'r1',
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
    // The Element confirmation succeeds in-browser; the backend confirm is stubbed separately.
    confirmPayment: () => Promise.resolve({ paymentIntent: { status: 'succeeded' } }),
  };
}

function seedAuthAndStripe(win: Window) {
  // A stored user + refresh token satisfies AuthService's localStorage session restore, so authGuard
  // lets /app/payments/one-time render without hitting the login flow.
  win.localStorage.setItem('neko_user', JSON.stringify({
    id: 'u1', firstName: 'Nico', lastName: 'Tester', email: 'nico@example.com', initials: 'NT',
  }));
  win.localStorage.setItem('neko_refresh', 'fake-refresh-token');
  // ngx-stripe's loader reuses window.Stripe when present, so it never fetches js.stripe.com.
  (win as unknown as { Stripe: () => unknown }).Stripe = () => fakeStripe();
}

describe('One-time payment (Stripe Payment Element)', () => {
  beforeEach(() => {
    cy.intercept('GET', '**/payments/options', { statusCode: 200, body: OPTIONS }).as('options');
    cy.intercept('POST', '**/payments/intent', { statusCode: 200, body: INTENT }).as('intent');
    cy.intercept('POST', '**/payments/one-time/confirm', { statusCode: 200, body: CONFIRMED }).as('confirm');
    // Shell + telemetry chatter the page emits — answered so they don't error in the console.
    cy.intercept('GET', '**/property', { statusCode: 404, body: {} });
    cy.intercept('POST', '**/telemetry', { statusCode: 200, body: {} });
  });

  it('completes a card payment via preset → Payment Element → receipt', () => {
    cy.visit('/app/payments/one-time', { onBeforeLoad: seedAuthAndStripe });

    // Step 1 — presets are built from the backend options.
    cy.wait('@options');
    cy.contains('Step 1 — How much?');
    cy.get('[data-preset="current"]').click();
    cy.contains('button', 'Continue').click();

    // Step 2 — entering the step creates the server-authoritative PaymentIntent and mounts the Element.
    cy.wait('@intent').its('request.body').should('deep.equal', { amount: 300, method: 'card' });
    cy.get('ngx-stripe-payment').should('exist');
    cy.contains('button', 'Continue').should('not.be.disabled').click();

    // Step 3 — fee/total come straight from the intent response, not recomputed client-side.
    cy.contains('Step 3 — Review & submit');
    cy.get('[data-testid="summary-fee"]').should('contain', '1.95');
    cy.get('[data-testid="summary-total"]').should('contain', '301.95');
    cy.contains('button', 'Submit payment').click();

    // Step 4 — backend confirm recorded the payment; the receipt shows the confirmation number.
    cy.wait('@confirm').its('request.body').should('deep.equal', { paymentIntentId: 'pi_test_123' });
    cy.get('[data-testid="receipt"]').should('be.visible');
    cy.get('[data-testid="confirmation-number"]').should('contain', 'NEKO-ABC123');
  });

  it('never sends raw card or bank fields to the backend (SC-001)', () => {
    cy.visit('/app/payments/one-time', { onBeforeLoad: seedAuthAndStripe });
    cy.wait('@options');

    // The legacy mock posted PAN/CVC/routing/account in plain inputs; those inputs must be gone.
    cy.contains('Card number').should('not.exist');
    cy.get('input[placeholder*="4242"]').should('not.exist');

    cy.get('[data-preset="current"]').click();
    cy.contains('button', 'Continue').click();
    cy.wait('@intent').then(({ request }) => {
      // The only fields ever sent are the amount and method — no instrument data.
      expect(Object.keys(request.body).sort()).to.deep.equal(['amount', 'method']);
    });
  });
});
