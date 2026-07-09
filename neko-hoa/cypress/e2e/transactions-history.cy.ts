/// <reference types="cypress" />

// End-to-end for the read-only Stripe transaction history on the statement page (Split 6 / T082).
// The history lives behind the "Payments" tab at /app/payments/statement and is lazy-loaded the first
// time the tab is opened. TransactionDto carries masked methods only — there is no raw card/bank data
// anywhere in the payload or the DOM (SC-001).

const LEDGER_PAGE = {
  items: [
    { id: '1', date: '2026-01-01', description: 'Regular Assessment – January 2026', type: 'Regular Assessment', charge: 250, payment: 0, balance: 250, docNumber: 'RA202601' },
  ],
  totalCount: 1, page: 1, pageSize: 100,
};

const TRANSACTIONS = {
  items: [
    { id: 't1', createdAt: '2026-01-05', grossAmount: 250, feeAmount: 5,    total: 255,    cumulativeRefundedAmount: 0,  status: 'Succeeded', paymentMethod: 'card', maskedMethod: 'Visa •••• 4242', isRecurring: false },
    { id: 't2', createdAt: '2026-02-01', grossAmount: 250, feeAmount: 1.95, total: 251.95, cumulativeRefundedAmount: 50, status: 'Refunded',  paymentMethod: 'card', maskedMethod: 'Visa •••• 4242', isRecurring: true },
  ],
  totalCount: 2, limit: 20, offset: 0,
};

const REFRESH_SESSION = {
  token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'u1', firstName: 'Nico', lastName: 'Tester', email: 'nico@example.com',
    initials: 'NT', properties: [],
  },
};

function seedAuth(win: Window) {
  // 020-D FR-D1: sessions re-hydrate via the hint-gated silent refresh — the hint below
  // makes APP_INITIALIZER call /auth/refresh, which the intercept in beforeEach answers.
  win.localStorage.setItem('neko_has_session', '1');
}

describe('Payment history (statement Payments tab)', () => {
  beforeEach(() => {
    cy.intercept('POST', '**/auth/refresh', { statusCode: 200, body: REFRESH_SESSION }).as('refresh');
    // Scope intercepts to the API host so they never hijack the SPA navigation URL.
    cy.intercept('GET', '**/api/*/payments/ledger*', { statusCode: 200, body: LEDGER_PAGE }).as('getLedger');
    cy.intercept('POST', '**/telemetry', { statusCode: 200, body: {} });
  });

  it('lazy-loads and renders the transaction history when the Payments tab is opened', () => {
    cy.intercept('GET', '**/api/*/payments/transactions*', { statusCode: 200, body: TRANSACTIONS }).as('getTxns');

    cy.visit('/app/payments/statement', { onBeforeLoad: seedAuth });
    cy.wait('@getLedger');

    // History is not fetched until the tab is opened.
    cy.get('[data-testid="tab-payments"]').click();
    cy.wait('@getTxns');

    cy.get('[data-testid="transactions-table"]').should('exist');
    cy.get('[data-testid="txn-row"]').should('have.length', 2);
    cy.get('[data-testid="transactions-table"]').within(() => {
      cy.contains('Visa •••• 4242');
      cy.contains('auto-pay');
      cy.contains('one-time');
    });
  });

  it('shows an empty state when there are no payments', () => {
    cy.intercept('GET', '**/api/*/payments/transactions*',
      { statusCode: 200, body: { items: [], totalCount: 0, limit: 20, offset: 0 } }).as('getTxns');

    cy.visit('/app/payments/statement', { onBeforeLoad: seedAuth });
    cy.wait('@getLedger');

    cy.get('[data-testid="tab-payments"]').click();
    cy.wait('@getTxns');

    cy.get('[data-testid="txn-empty"]').should('be.visible');
    cy.get('[data-testid="txn-row"]').should('not.exist');
  });

  it('does not refetch when switching away and back to the Payments tab', () => {
    cy.intercept('GET', '**/api/*/payments/transactions*', { statusCode: 200, body: TRANSACTIONS }).as('getTxns');

    cy.visit('/app/payments/statement', { onBeforeLoad: seedAuth });
    cy.wait('@getLedger');

    cy.get('[data-testid="tab-payments"]').click();
    cy.wait('@getTxns');
    cy.get('[data-testid="tab-statement"]').click();
    cy.get('[data-testid="tab-payments"]').click();

    // Only the first open triggered a request.
    cy.get('@getTxns.all').should('have.length', 1);
  });
});
