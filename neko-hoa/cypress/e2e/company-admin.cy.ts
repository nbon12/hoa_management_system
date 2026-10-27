// 030 T147 — Cypress E2E: a Company Administrator with no home signs in, lands on the portfolio,
// and opens Keystone Ridge's Community settings.
//
// Same pattern as the other Cypress specs: `ng serve` with NO backend. Every API call the journey
// makes is stubbed with cy.intercept (a catch-all first, then the specific routes, because the
// most recently defined intercept wins).

// A module, so its names don't collide with the other specs' globals when type-checked together.
export {};

const RIDGE_ID = '22222222-2222-2222-2222-222222222222';
const SAKURA_ID = '33333333-3333-3333-3333-333333333333';
const NONE = { allowed: false, reasonCode: null };

// company@nekohoa.dev: no home, no membership, the Company Administrator grant (FR-036a).
const SESSION = {
  token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'u-company', firstName: 'Casey', lastName: 'Company', email: 'company@nekohoa.dev', initials: 'CC',
    properties: [], memberships: [], lastActiveMode: 'Resident', isCompanyAdministrator: true, landing: 'Company',
  },
};

function row(id: string, communityName: string, status: string, over: Record<string, unknown> = {}) {
  return {
    id, communityName, legalName: `${communityName} HOA, Inc.`, status, parent: null,
    managers: [{ userId: 'u-morgan', name: 'Morgan Manager' }], pendingManagerInvitations: [],
    noManager: false, differsFromDefaults: false, ...over,
  };
}

const PORTFOLIO = {
  items: [row(RIDGE_ID, 'Keystone Ridge', 'Onboarding'), row(SAKURA_ID, 'Sakura Heights', 'Active', { differsFromDefaults: true })],
  total: 2, limit: 25, offset: 0,
  counts: { all: 2, onboarding: 1, active: 1, archived: 0, noManager: 0 },
};

const SETTINGS = {
  id: RIDGE_ID, communityName: 'Keystone Ridge', legalName: 'Keystone Ridge HOA, Inc.', status: 'Onboarding',
  parent: null, noManager: false, missingRequiredFields: [],
  sections: ['profile', 'managers-and-memberships', 'architectural-review'],
  can: {
    editProfile: true, changeParent: true, activate: { allowed: true, reasonCode: null }, archive: { allowed: true, reasonCode: null },
    restore: NONE, manageManagers: true, replaceLastManager: true, manageMemberships: true, manageResidents: true,
    editArcSettings: true, endMembershipsWhileArchived: false,
  },
};

const PROFILE = {
  legalName: 'Keystone Ridge HOA, Inc.', communityName: 'Keystone Ridge', county: 'Durham County, NC',
  formationDate: '2011-06-02', managementStartDate: '2026-12-01', description: null, missingRequiredFields: [],
};

describe('Company Administrator E2E (030 T147)', () => {
  beforeEach(() => {
    // Quiet scaffolding first; the specific routes below take precedence.
    cy.intercept('GET', '**/api/**', { statusCode: 200, body: {} });
    cy.intercept('POST', '**/telemetry', { statusCode: 200, body: {} });
    cy.intercept('GET', '**/property', { statusCode: 404, body: { code: 'NOT_FOUND', message: 'No home.' } });
    cy.intercept('POST', '**/auth/login', { statusCode: 200, body: SESSION }).as('login');
    cy.intercept('POST', '**/auth/refresh', { statusCode: 200, body: SESSION }).as('refresh');
    cy.intercept('GET', '**/company/communities*', { statusCode: 200, body: PORTFOLIO }).as('portfolio');
    cy.intercept('GET', `**/communities/${RIDGE_ID}/settings`, { statusCode: 200, body: SETTINGS }).as('settings');
    cy.intercept('GET', `**/communities/${RIDGE_ID}/profile`, { statusCode: 200, body: PROFILE }).as('profile');
  });

  it('signs in with no home, lands on the portfolio, and opens Keystone Ridge\'s Community settings', () => {
    cy.visit('/login');
    cy.get('input[name="email"]').type('company@nekohoa.dev');
    cy.get('input[name="password"]').type('Password1!');
    cy.get('button.btn--primary').click();
    cy.wait('@login');

    // FR-036a: a Company Administrator with no home lands on the portfolio, in the company area.
    cy.url({ timeout: 15000 }).should('include', '/app/company/portfolio');
    cy.wait('@portfolio');
    cy.contains('h1', 'Portfolio').should('be.visible');
    cy.get('nav[aria-label="Company area navigation"]').should('contain.text', 'Company defaults');
    cy.get('table').contains('tr', 'Keystone Ridge').within(() => {
      cy.contains('Onboarding').should('be.visible');
      cy.contains('a', 'Community settings').click();
    });

    cy.url({ timeout: 15000 }).should('include', `/app/company/communities/${RIDGE_ID}/settings/profile`);
    cy.wait('@settings');
    cy.get('nav[aria-label="Breadcrumb"]').should('contain.text', 'Portfolio');
    cy.contains('h1', 'Keystone Ridge').should('be.visible');
    cy.get('button[aria-label="Mark Keystone Ridge Active"]').should('be.visible');
    cy.wait('@profile');
    cy.contains('h2', 'Profile').should('be.visible');
    cy.get('#pf-county').should('have.value', 'Durham County, NC');
    cy.get('nav[aria-label="Community settings sections"]').within(() => {
      cy.contains('a', 'Managers and memberships').should('be.visible');
      cy.contains('a', 'Architectural review').should('be.visible');
    });
  });
});
