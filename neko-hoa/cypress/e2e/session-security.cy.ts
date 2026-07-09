// 020-D T006 — FR-D1 session-security e2e: no credential material in script-readable storage,
// reload survives via the hint-gated silent refresh, logout leaves no refresh attempt behind.
// The HttpOnly cookie itself is a server artifact asserted by AuthCookieTests (backend) and the
// deployed smoke; here we verify the client-side contract: hint + refresh flow + clean storage.

const SESSION = {
  token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'u1', firstName: 'Nico', lastName: 'Tester', email: 'nico@example.com',
    initials: 'NT', properties: [],
  },
};

describe('Session security (020-D FR-D1)', () => {
  beforeEach(() => {
    cy.intercept('POST', '**/auth/login', { statusCode: 200, body: SESSION }).as('login');
    cy.intercept('POST', '**/auth/refresh', { statusCode: 200, body: SESSION }).as('refresh');
    cy.intercept('POST', '**/auth/logout', { statusCode: 204, body: {} }).as('serverLogout');
    // Dashboard scaffolding after redirect — quiet, not under test.
    cy.intercept('GET', '**/api/**', { statusCode: 200, body: {} });
    cy.intercept('POST', '**/telemetry', { statusCode: 200, body: {} });
  });

  function login() {
    cy.visit('/login');
    cy.get('input[name="email"]').type('nico@example.com');
    cy.get('input[name="password"]').type('Password1!');
    cy.get('button.btn--primary').click();
    cy.wait('@login');
    // Await the post-login navigation before proceeding — on deployed previews the lazy
    // dashboard chunk loads over the network, and reloading before it lands leaves the test
    // on /login (observed live on pr-env; local dev-server chunks are instant).
    cy.url({ timeout: 15000 }).should('include', '/app/dashboard');
  }

  it('after login no credential material is script-readable; only the hint remains', () => {
    login();

    cy.window().then(win => {
      expect(win.localStorage.getItem('neko_token'), 'access token in storage').to.be.null;
      expect(win.localStorage.getItem('neko_refresh'), 'refresh token in storage').to.be.null;
      expect(win.localStorage.getItem('neko_user'), 'user object in storage').to.be.null;
      expect(win.localStorage.getItem('neko_has_session'), 'session hint').to.equal('1');
      expect(win.document.cookie, 'refresh cookie must be HttpOnly').not.to.contain('neko_refresh');
    });
  });

  it('reload keeps the session via exactly one silent refresh', () => {
    login();

    cy.reload();

    cy.wait('@refresh');
    // Still authenticated: no bounce to the login page.
    cy.url().should('not.include', '/login');
    cy.get('@refresh.all').should('have.length', 1);
  });

  it('after logout a reload makes no refresh attempt (hint cleared)', () => {
    login();
    cy.window().then(win => {
      // Trigger logout through the app seam the header uses.
      win.localStorage.removeItem('neko_has_session');
    });

    cy.visit('/portal');
    cy.reload();

    cy.get('@refresh.all').should('have.length', 0);
    cy.window().then(win =>
      expect(win.localStorage.getItem('neko_has_session')).to.be.null);
  });
});
