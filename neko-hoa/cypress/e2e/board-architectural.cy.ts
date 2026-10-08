// 027 T071 — Cypress E2E: sign in → board mode → Architectural Applications → vote.
//
// Stubbed with cy.intercept like board-mode.cy.ts so it runs against `ng serve` with no live API.
// Covers the US1 and US2 Independent Tests end to end in the browser: the list renders with its tabs
// and pill, and Approve on an unvoted row shows "you voted approve" and the updated tally.

export {};

const COMMUNITY_ID = '11111111-1111-1111-1111-111111111111';

function session(lastActiveMode: 'Resident' | 'Board') {
  return {
    token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
    expiresAt: '2099-01-01T00:00:00Z',
    user: {
      id: 'u-board', firstName: 'Bea', lastName: 'Board', email: 'board@nekohoa.dev', initials: 'BB',
      properties: [], lastActiveMode,
      memberships: [{ communityId: COMMUNITY_ID, communityName: 'Sakura Heights HOA', role: 'BoardMember' }],
    },
  };
}

const ROW = {
  id: 'a1042', displayId: 'ARC-1042', revision: 1, propertyAddress: '711 Keystone Park Dr #29', ownerName: 'Praneeth Pattyam',
  projectTitle: 'Fence replacement — 6ft cedar', attachmentCount: 3, receivedDate: '2026-05-28', dueDate: '2026-06-27',
  overdue: false, status: 'Open', decision: null, infoRequested: false,
  tally: { approve: 1, revisionsNeeded: 0, deny: 0, notVoted: 4, eligible: 5 }, myVote: { state: 'CanVote' },
};

describe('Board architectural review E2E (027)', () => {
  beforeEach(() => {
    cy.intercept('POST', '**/auth/login', { statusCode: 200, body: session('Resident') }).as('login');
    cy.intercept('POST', '**/auth/refresh', { statusCode: 200, body: session('Resident') });
    cy.intercept('POST', '**/auth/board-mode', { statusCode: 200, body: session('Board') }).as('boardMode');
    cy.intercept('GET', '**/api/**', { statusCode: 200, body: {} });
    cy.intercept('GET', '**/board/metrics**', { statusCode: 200, body: { items: [], total: 0, limit: 25, offset: 0 } });
    cy.intercept('GET', '**/architectural-applications?*', {
      statusCode: 200,
      body: { items: [ROW], total: 1, limit: 25, offset: 0, counts: { open: 1, closed: 0, awaitingMyVote: 1 } },
    }).as('list');
    cy.intercept('POST', '**/architectural-applications/a1042/votes', {
      statusCode: 201,
      body: { ...ROW, myVote: { state: 'Voted', choice: 'Approve' }, tally: { approve: 2, revisionsNeeded: 0, deny: 0, notVoted: 3, eligible: 5 } },
    }).as('vote');
    cy.intercept('POST', '**/telemetry', { statusCode: 200, body: {} });
  });

  it('signs in, enters board mode, opens Architectural Applications and approves', () => {
    cy.visit('/login');
    cy.get('input[name="email"]').type('board@nekohoa.dev');
    cy.get('input[name="password"]').type('Password1!');
    cy.get('button.btn--primary').click();
    cy.wait('@login');
    cy.contains('button', 'Enter board mode').should('be.visible').click();
    cy.wait('@boardMode');
    cy.url({ timeout: 15000 }).should('include', '/app/board/home');

    // Community Home shows the Needs-your-vote card (US5) linking to the full list.
    cy.contains('Needs your vote').should('be.visible');
    cy.contains('a', 'All architectural applications →').click();
    cy.url().should('include', '/app/board/architectural');
    cy.wait('@list');

    cy.contains('[role=tab]', 'Open · 1').should('have.attr', 'aria-selected', 'true');
    cy.contains('1 awaiting your vote').should('be.visible');
    cy.get('tr[data-app-id="a1042"]').within(() => {
      cy.contains('ARC-1042');
      cy.contains('button', 'Approve').click();
    });
    cy.wait('@vote').its('request.body').should('deep.equal', { choice: 'Approve', comment: null });
    cy.get('tr[data-app-id="a1042"]').within(() => {
      cy.contains('you voted approve').should('be.visible');
      cy.get('[role=img]').should('have.attr', 'aria-label', '2 approve · 0 revisions needed · 0 deny · 3 not voted');
    });
  });
});
