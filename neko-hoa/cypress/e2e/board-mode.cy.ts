// 025 US3 (T041) — Cypress E2E: sign-in → enter board mode → land on Community Home.
//
// Matches the repo's Cypress pattern (registration/session-security specs): the backend is
// stubbed with cy.intercept so the flow runs against a local `ng serve` with no live API. The
// login response carries an active NON-resident membership, which is exactly what makes the user
// board-eligible client-side (mode-toggle visibility derives from memberships), and the
// board-mode switch response flips the persisted mode to Board.

// A board-eligible session: one active BoardMember membership → mode control renders; a single
// community → entering board mode lands directly on that community's home (no My Communities).
const COMMUNITY_ID = '11111111-1111-1111-1111-111111111111';

function session(lastActiveMode: 'Resident' | 'Board') {
  return {
    token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
    expiresAt: '2099-01-01T00:00:00Z',
    user: {
      id: 'u-board', firstName: 'Bea', lastName: 'Board', email: 'board@nekohoa.dev',
      initials: 'BB', properties: [],
      lastActiveMode,
      memberships: [
        { communityId: COMMUNITY_ID, communityName: 'Sakura Heights HOA', role: 'BoardMember' },
      ],
    },
  };
}

describe('Board mode E2E (025 US3)', () => {
  beforeEach(() => {
    cy.intercept('POST', '**/auth/login', { statusCode: 200, body: session('Resident') }).as('login');
    cy.intercept('POST', '**/auth/refresh', { statusCode: 200, body: session('Resident') }).as('refresh');
    // Switching mode is a server round-trip that returns a fresh session in Board mode.
    cy.intercept('POST', '**/auth/board-mode', { statusCode: 200, body: session('Board') }).as('boardMode');
    // Quiet scaffolding for the pages that render after navigation.
    cy.intercept('GET', '**/api/**', { statusCode: 200, body: {} });
    // Registry-driven metric surfaces are empty until spec 2 registers descriptors.
    cy.intercept('GET', '**/board/metrics**',
      { statusCode: 200, body: { items: [], total: 0, limit: 25, offset: 0 } }).as('metrics');
    cy.intercept('POST', '**/telemetry', { statusCode: 200, body: {} });
  });

  it('signs in, enters board mode, and lands on Community Home', () => {
    cy.visit('/login');
    cy.get('input[name="email"]').type('board@nekohoa.dev');
    cy.get('input[name="password"]').type('Password1!');
    cy.get('button.btn--primary').click();
    cy.wait('@login');
    cy.url({ timeout: 15000 }).should('include', '/app/dashboard');

    // The board-eligible user sees the "Enter board mode" control.
    cy.contains('button', 'Enter board mode').should('be.visible').click();
    cy.wait('@boardMode');

    // Lands on the single community's home, with the distinct board banner and Resident/Board toggle.
    cy.url({ timeout: 15000 }).should('include', '/app/board/home');
    cy.get('.board-banner').should('be.visible').and('contain.text', 'Board mode');
    cy.get('.mode-seg').should('be.visible');
    // Community Home content rendered (its "at a glance" header), not a blank screen.
    cy.contains('at a glance').should('be.visible');
  });
});
