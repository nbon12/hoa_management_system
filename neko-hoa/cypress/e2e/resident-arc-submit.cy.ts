// 029 T034/T045 — Cypress E2E: sign in → Architectural requests → New → save draft → reload → submit
// → the request is "Submitted / Under review" and listed.
//
// Stubbed with cy.intercept like board-architectural.cy.ts, so it runs against `ng serve` with no live API.
// Covers the US1 Independent Test in the browser (create, save, reopen, acknowledge, submit) and the US2
// list/detail landing. The server rules (numbering, limits, scope) are covered by the xUnit suite.

export {};

const DRAFT_ID = 'd-1';
const APP_ID = 'a-1042';

const session = {
  token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'u-res', firstName: 'Rae', lastName: 'Resident', email: 'resident@nekohoa.dev', initials: 'RR',
    properties: [], lastActiveMode: 'Resident', memberships: [],
  },
};

const draft = (body: Record<string, unknown> = {}) => ({
  id: DRAFT_ID, projectType: 'Fence', projectTitle: '', description: '', plannedStartDate: null, plannedCompletionDate: null,
  contractorName: null, contractorContact: null, acknowledged: false, previousRevisionId: null, previousDisplayId: null,
  attachments: [], carriedAttachments: [], removedCarriedAttachmentIds: [], updatedAt: '2026-10-08T12:00:00Z', ...body,
});

const detail = {
  kind: 'Application', id: APP_ID, displayId: 'ARC-1042', revision: 1, projectType: 'Fence',
  projectTitle: 'Fence replacement — 6ft cedar', status: 'Submitted', attachmentCount: 0,
  receivedDate: '2026-10-08', dueDate: '2026-11-07', updatedAt: '2026-10-08T12:00:00Z',
  description: 'Replace the rear fence.', plannedStartDate: '2026-11-02', plannedCompletionDate: '2026-11-20',
  contractorName: null, contractorContact: null, acknowledgedAt: '2026-10-08T12:00:00Z', attachments: [],
  infoRequests: [], timeline: [{ event: 'Submitted', at: '2026-10-08T12:00:00Z' }], decision: null,
  ownerReason: null, formalDisapprovalStatement: null, conditionsOfApproval: null, closedAt: null,
  canWithdraw: true, canRevise: false, revisions: [{ id: APP_ID, revision: 1, status: 'Submitted', receivedDate: '2026-10-08' }],
};

describe('Resident architectural request E2E (029)', () => {
  let saved = draft();

  beforeEach(() => {
    saved = draft();
    cy.intercept('POST', '**/auth/login', { statusCode: 200, body: session }).as('login');
    cy.intercept('POST', '**/auth/refresh', { statusCode: 200, body: session });
    cy.intercept('GET', '**/api/**', { statusCode: 200, body: {} });
    cy.intercept('POST', '**/telemetry', { statusCode: 200, body: {} });
    cy.intercept('POST', '**/property/architectural-applications/drafts', req => {
      saved = draft(req.body);
      req.reply({ statusCode: 201, body: saved });
    }).as('createDraft');
    cy.intercept('PUT', `**/property/architectural-applications/drafts/${DRAFT_ID}`, req => {
      saved = draft(req.body);
      req.reply({ statusCode: 200, body: saved });
    }).as('updateDraft');
    cy.intercept('GET', `**/property/architectural-applications/drafts/${DRAFT_ID}`, req => req.reply({ statusCode: 200, body: saved })).as('getDraft');
    cy.intercept('POST', `**/property/architectural-applications/drafts/${DRAFT_ID}/submit`, { statusCode: 201, body: detail }).as('submit');
    cy.intercept('GET', `**/property/architectural-applications/${APP_ID}`, { statusCode: 200, body: detail }).as('detail');
    cy.intercept('GET', '**/property/architectural-applications?*', {
      statusCode: 200,
      body: { items: [{ ...detail, kind: 'Application' }], total: 1, limit: 25, offset: 0 },
    }).as('list');
  });

  it('creates a draft, reopens it, acknowledges and submits, then sees it in the list', () => {
    cy.visit('/login');
    cy.get('input[name="email"]').type('resident@nekohoa.dev');
    cy.get('input[name="password"]').type('Password1!');
    cy.get('button.btn--primary').click();
    cy.wait('@login');

    cy.contains('a', 'Architectural requests').click();
    cy.contains('a', 'New request').click();
    cy.url().should('include', '/app/property/architectural/new');

    cy.get('#arc-type').select('Fence');
    cy.get('#arc-title').type('Fence replacement — 6ft cedar');
    cy.get('#arc-description').type('Replace the rear fence.');
    cy.get('#arc-start').type('2026-11-02');
    cy.get('#arc-finish').type('2026-11-20');
    cy.contains('button', 'Save draft').click();
    cy.wait('@createDraft').its('request.body').should('include', { projectTitle: 'Fence replacement — 6ft cedar', acknowledged: false });
    cy.url().should('include', `/app/property/architectural/drafts/${DRAFT_ID}`);
    cy.contains('Draft saved').should('be.visible');

    // Reopen the draft: the saved values come back.
    cy.reload();
    cy.wait('@getDraft');
    cy.get('#arc-title').should('have.value', 'Fence replacement — 6ft cedar');

    // Submit stays disabled until the acknowledgement (US1 AS5).
    cy.contains('button', 'Submit request').should('be.disabled');
    cy.get('#arc-ack').check();
    cy.contains('button', 'Submit request').should('not.be.disabled').click();
    cy.wait('@updateDraft').its('request.body').should('include', { acknowledged: true });
    cy.wait('@submit');

    cy.url().should('include', `/app/property/architectural/${APP_ID}`);
    cy.contains('h1', 'ARC-1042').should('be.visible');
    cy.contains('Submitted / Under review').should('be.visible');

    cy.contains('a', 'Back to my requests').click();
    cy.wait('@list');
    cy.contains('[aria-label="Architectural requests"] a', 'ARC-1042').should('contain.text', 'Submitted / Under review');
  });
});
