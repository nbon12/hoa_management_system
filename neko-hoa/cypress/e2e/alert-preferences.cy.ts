/// <reference types="cypress" />

// End-to-end for the payment-alerts opt-in section (Split 5 / T073). The section lives on the
// auto-pay page (/app/payments/recurring), so we stub that page's calls too and fake Stripe at the
// window.Stripe seam (the page injects it). Alerts default OFF — this drives opt-in then opt-out.

const PREFS_OFF = { smsOptIn: false, emailOptIn: false, alertPhone: null };
const PREFS_SMS = { smsOptIn: true, emailOptIn: true, alertPhone: '+19195551234' };

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
    registerAppInfo: () => undefined,
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

describe('Payment alert preferences (opt-in / opt-out)', () => {
  beforeEach(() => {
    cy.intercept('POST', '**/auth/refresh', { statusCode: 200, body: REFRESH_SESSION }).as('refresh');
    // Auto-pay page scaffolding (not under test here) — keep it quiet.
    cy.intercept('GET', '**/api/*/payments/recurring', { statusCode: 204, body: '' }).as('getRecurring');
    cy.intercept('GET', '**/api/*/payments/drafts*',
      { statusCode: 200, body: { items: [], totalCount: 0, limit: 50, offset: 0 } }).as('drafts');
    cy.intercept('POST', '**/telemetry', { statusCode: 200, body: {} });
  });

  it('opts in to SMS + email with a phone number', () => {
    cy.intercept('GET', '**/api/*/payments/alert-preferences', { statusCode: 200, body: PREFS_OFF }).as('getPrefs');
    cy.intercept('PUT', '**/api/*/payments/alert-preferences', { statusCode: 200, body: PREFS_SMS }).as('savePrefs');

    cy.visit('/app/payments/recurring', { onBeforeLoad: seedAuthAndStripe });
    cy.wait('@getPrefs');

    cy.get('[data-testid="alerts-section"]').should('exist');
    cy.get('[data-testid="sms-toggle"]').check();
    cy.get('[data-testid="alert-phone"]').type('+19195551234');
    cy.get('[data-testid="email-toggle"]').check();
    cy.get('[data-testid="alerts-save"]').click();

    cy.wait('@savePrefs').its('request.body').should('deep.equal', {
      smsOptIn: true, emailOptIn: true, alertPhone: '+19195551234',
    });
    cy.get('[data-testid="alerts-saved"]').should('be.visible');
  });

  it('opts out by clearing both channels', () => {
    cy.intercept('GET', '**/api/*/payments/alert-preferences', { statusCode: 200, body: PREFS_SMS }).as('getPrefs');
    cy.intercept('PUT', '**/api/*/payments/alert-preferences', { statusCode: 200, body: PREFS_OFF }).as('savePrefs');

    cy.visit('/app/payments/recurring', { onBeforeLoad: seedAuthAndStripe });
    cy.wait('@getPrefs');

    // Starts enrolled; uncheck both and save.
    cy.get('[data-testid="sms-toggle"]').should('be.checked').uncheck();
    cy.get('[data-testid="email-toggle"]').should('be.checked').uncheck();
    cy.get('[data-testid="alerts-save"]').click();

    cy.wait('@savePrefs').its('request.body').should('deep.equal', {
      smsOptIn: false, emailOptIn: false, alertPhone: null,
    });
    cy.get('[data-testid="alerts-saved"]').should('be.visible');
  });

  it('refuses SMS opt-in without a phone (client guard, no request)', () => {
    cy.intercept('GET', '**/api/*/payments/alert-preferences', { statusCode: 200, body: PREFS_OFF }).as('getPrefs');
    cy.intercept('PUT', '**/api/*/payments/alert-preferences', cy.spy().as('putSpy'));

    cy.visit('/app/payments/recurring', { onBeforeLoad: seedAuthAndStripe });
    cy.wait('@getPrefs');

    cy.get('[data-testid="sms-toggle"]').check();
    cy.get('[data-testid="alerts-save"]').click();

    cy.get('[data-testid="alerts-error"]').should('contain', 'mobile number is required');
    cy.get('@putSpy').should('not.have.been.called');
  });
});
