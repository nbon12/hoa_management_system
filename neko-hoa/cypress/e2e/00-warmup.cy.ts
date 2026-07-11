// Runs first (alphabetical) and absorbs the deployed preview's cold start — first-ever CDN
// asset fetches + scale-to-zero API wake — so real specs start against a warm stack instead of
// whichever suite is alphabetically first flaking on first-load latency (seen repeatedly on
// pr-env: a different spec's first page load timed out each run).
describe('warmup', () => {
  it('loads the shell and login chunk against a cold deployment', () => {
    cy.visit('/', { timeout: 90_000 });
    cy.get('body', { timeout: 60_000 }).should('be.visible');
    cy.visit('/login', { timeout: 60_000 });
    cy.get('input[name="email"]', { timeout: 60_000 }).should('exist');
    cy.visit('/register', { timeout: 60_000 });
    cy.get('input[name="email"]', { timeout: 60_000 }).should('exist');
  });
});
