// 020-D T028 — FR-D9/FR-D10: verified 3-step registration. The account-number path is gone;
// every backend failure renders one generic message (no enumeration through the UI).

const SESSION = {
  token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'u1', firstName: 'Jane', lastName: 'Doe', email: 'jane@example.com',
    initials: 'JD', properties: [],
  },
};

const GENERIC = 'Registration could not be completed';

describe('Registration (verified email + claim code)', () => {
  beforeEach(() => {
    cy.intercept('GET', '**/api/**', { statusCode: 200, body: {} });
    cy.intercept('POST', '**/telemetry', { statusCode: 200, body: {} });
  });

  function completeVerification() {
    cy.intercept('POST', '**/auth/verify-email/request', { statusCode: 202, body: { status: 'sent' } }).as('request');
    cy.intercept('POST', '**/auth/verify-email/confirm', { statusCode: 200, body: { verificationToken: 'proof-1' } }).as('confirm');

    cy.visit('/register');
    cy.get('input[name="email"]').type('jane@example.com');
    cy.contains('button', 'Send code').click();
    cy.wait('@request');

    cy.get('input[name="code"]').type('123456');
    cy.contains('button', 'Verify').click();
    cy.wait('@confirm');
  }

  it('offers no account-number claiming path (FR-D9)', () => {
    cy.visit('/register');
    cy.get('input[name="accountNum"]').should('not.exist');
    cy.contains(/find my property/i).should('not.exist');
  });

  it('completes the 3-step flow and lands on the dashboard', () => {
    cy.intercept('POST', '**/auth/register', req => {
      expect(req.body).to.deep.equal({
        verificationToken: 'proof-1', password: 'Password1!',
        firstName: 'Jane', lastName: 'Doe', claimCode: 'HOA-CLAIM-1234',
      });
      req.reply({ statusCode: 201, body: SESSION });
    }).as('register');

    completeVerification();

    cy.get('input[name="firstName"]').type('Jane');
    cy.get('input[name="lastName"]').type('Doe');
    cy.get('input[name="password"]').type('Password1!');
    cy.get('input[name="claimCode"]').type('HOA-CLAIM-1234');
    cy.contains('button', 'Create account').click();

    cy.wait('@register');
    cy.url().should('include', '/app/dashboard');
  });

  it('renders the same generic message for a wrong verification code (FR-D10)', () => {
    cy.intercept('POST', '**/auth/verify-email/request', { statusCode: 202, body: { status: 'sent' } }).as('request');
    cy.intercept('POST', '**/auth/verify-email/confirm',
      { statusCode: 400, body: { code: 'VERIFICATION_FAILED' } }).as('confirm');

    cy.visit('/register');
    cy.get('input[name="email"]').type('jane@example.com');
    cy.contains('button', 'Send code').click();
    cy.wait('@request');
    cy.get('input[name="code"]').type('000000');
    cy.contains('button', 'Verify').click();
    cy.wait('@confirm');

    cy.contains(GENERIC);
  });

  it('renders the same generic message when registration is refused (FR-D10)', () => {
    cy.intercept('POST', '**/auth/register',
      { statusCode: 400, body: { code: 'REGISTRATION_FAILED' } }).as('register');

    completeVerification();

    cy.get('input[name="firstName"]').type('Jane');
    cy.get('input[name="lastName"]').type('Doe');
    cy.get('input[name="password"]').type('Password1!');
    cy.get('input[name="claimCode"]').type('WRONG-CODE');
    cy.contains('button', 'Create account').click();
    cy.wait('@register');

    cy.contains(GENERIC);
    cy.url().should('include', '/register');
  });
});
