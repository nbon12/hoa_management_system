import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { establishSession, loginAs, setMode, BOARD_HOME_LANDING } from './helpers/auth';

/**
 * 025 US1 (T026) — the enter/leave board-mode journey.
 *
 * Acceptance scenarios exercised (spec.md §US1):
 *   1. A board-eligible user sees an "Enter board mode" control in the top bar.
 *   3. Entering board mode shows a visually distinct banner AND a Resident/Board toggle;
 *      the shell navigation changes to the board sidebar.
 *   4. Signing out and back in returns the user to the mode they were last in.
 *
 * Seeding dependency: the journey needs a user with a home AND an active non-resident
 * membership, since only such a user has a resident mode to leave and come back to. Since 030
 * (FR-036a, T043) the seeded board@nekohoa.dev has no home and signs straight in to board mode,
 * so the suite provisions its own user once, through 030's own API: the company administrator
 * invites a fresh `e2e+…@test.dev` resident to a seeded home, the user signs up from the link
 * (read from the dev vault), and is appointed Board Member. The global setup's cleanup removes
 * the user on the next run. A resident-only user correctly sees NO toggle, which is asserted
 * separately in the mode-toggle unit spec and in the role-gate Playwright spec.
 */

const API = process.env.PLAYWRIGHT_API_URL || 'http://localhost:5212';
const SECRET = process.env.PLAYWRIGHT_SCHEDULER_SECRET || 'dev-scheduler-shared-secret-placeholder';
const COMPANY_EMAIL = process.env.PLAYWRIGHT_COMPANY_EMAIL || 'company@nekohoa.dev';
const COMPANY_PASSWORD = process.env.PLAYWRIGHT_COMPANY_PASSWORD || 'Password1!';
const COMMUNITY = process.env.PLAYWRIGHT_RESIDENT_COMMUNITY || 'Sakura Heights HOA';
// The 017 registration journey claims this home, so it must stay unclaimed.
const REGISTRATION_HOME = '3 Sakura Drive';
const PASSWORD = 'Password1!';

/** A resident of a seeded home who is also an active Board Member there. */
async function provisionResidentBoardMember(request: APIRequestContext): Promise<string> {
  const email = `e2e+boardmode-${Date.now()}${Math.floor(Math.random() * 1000)}@test.dev`;
  const login = await request.post(`${API}/api/v1/auth/login`, { data: { email: COMPANY_EMAIL, password: COMPANY_PASSWORD } });
  expect(login.ok(), `login as ${COMPANY_EMAIL}`).toBeTruthy();
  const headers = { Authorization: `Bearer ${(await login.json()).token}` };

  const communities = await request.get(
    `${API}/api/v1/company/communities?status=active&search=${encodeURIComponent(COMMUNITY)}`, { headers });
  const communityId = ((await communities.json()).items as { id: string }[])[0].id;
  const homes = await request.get(`${API}/api/v1/communities/${communityId}/residents?limit=100`, { headers });
  const home = ((await homes.json()).items as { propertyId: string; address: string }[])
    .find(h => h.address !== REGISTRATION_HOME)!;

  const invite = await request.post(`${API}/api/v1/communities/${communityId}/residents/invitations`,
    { headers, data: { propertyId: home.propertyId, email } });
  expect(invite.status(), 'invite the resident').toBe(201);

  let link: string | null = null;
  for (let attempt = 0; !link && attempt < 20; attempt++) {
    const res = await request.get(`${API}/api/v1/e2e/auth-codes?contact=${encodeURIComponent(email)}`,
      { headers: { 'X-Scheduler-Secret': SECRET } });
    link = res.ok() ? (await res.json()).invitationLink as string | null : null;
    if (!link) await new Promise(r => setTimeout(r, 500));
  }
  expect(link, 'invitation link in the dev vault').toBeTruthy();
  const token = new URL(link!, 'http://localhost').hash.replace(/^#t=/, '');
  const registered = await request.post(`${API}/api/v1/invitations/register`,
    { data: { token, firstName: 'Bea', lastName: 'Board', password: PASSWORD } });
  expect(registered.status(), 'sign up from the link').toBe(201);
  const userId = (await registered.json()).user.id as string;

  const appoint = await request.post(`${API}/api/v1/communities/${communityId}/memberships`,
    { headers, data: { role: 'BoardMember', userId } });
  expect(appoint.status(), 'appoint as Board Member').toBe(201);
  return email;
}

let boardEmail: string;
test.beforeAll(async ({ request }) => {
  boardEmail = await provisionResidentBoardMember(request);
});

/**
 * The shell is showing the RESIDENT sidebar: the Dashboard entry is present and no board entry
 * is. Both halves matter — asserting only "Dashboard is visible" would still be satisfiable if
 * the board nav had rendered alongside it, so the Community-Home absence check is what makes
 * this a real resident-mode assertion (the board sidebar's first entry is Community Home).
 */
async function expectResidentSidebar(page: Page): Promise<void> {
  const sidebar = page.locator('.shell__side');
  await expect(sidebar.getByRole('link', { name: 'Dashboard', exact: true })).toBeVisible();
  await expect(sidebar.getByRole('link', { name: 'Community Home', exact: true })).toHaveCount(0);
}

test.describe('Board mode: enter / leave journey (US1)', () => {
  test('board-eligible user can enter board mode, see the banner + board nav, and return to resident', async ({ page }) => {
    await establishSession(page, boardEmail, PASSWORD);
    // Deterministically start in resident mode — a sibling test persists Board for this
    // shared user, so set the server-side mode before the app boots (no client render race).
    await setMode(page, 'Resident', boardEmail, PASSWORD);
    await page.goto('/app/dashboard');
    await page.waitForFunction(
      () => document.querySelectorAll('.spinner').length === 0,
      { timeout: 15_000 },
    );
    // The silent-refresh boot populates auth state asynchronously; the avatar only
    // renders once auth.user() is hydrated, so wait for it before asserting nav/mode.
    await expect(page.locator('.avatar')).toBeVisible({ timeout: 15_000 });

    // Scenario 1: the "Enter board mode" control is present in the top bar.
    const enterBtn = page.getByRole('button', { name: /Enter board mode/i });
    await expect(enterBtn).toBeVisible();
    // Resident view intact before switching: the resident sidebar shows Dashboard and none of
    // the board entries. Matched on the link's accessible name (whitespace-normalised) rather
    // than an anchored regex over textContent — shell.component.ts renders `{{ item.label }}`
    // on its own template line, so the raw text is wrapped in newlines + indentation and
    // /^Dashboard$/ can never match it. `exact: true` keeps the match strict, and the
    // Community-Home absence check keeps this failing if the board sidebar rendered instead.
    await expectResidentSidebar(page);

    // Scenario 3: entering board mode shows the banner and the Resident/Board toggle,
    // and the shell navigation changes to the board sidebar.
    await enterBtn.click();
    // Switching mode is a server round-trip that then navigates a single-community board
    // user to /app/board/home; wait for that landing so the board nav has rendered before
    // asserting on it (FR-026).
    await page.waitForURL(/\/app\/board\/home/, { timeout: 15_000 });
    await expect(page.locator('.board-banner')).toBeVisible();
    await expect(page.locator('.board-banner')).toContainText(/Board mode/i);
    await expect(page.locator('.board-banner')).toContainText(/association-wide data/i);
    // The top-bar segmented Resident/Board toggle replaces the "Enter board mode" button.
    await expect(page.locator('.mode-seg')).toBeVisible();
    await expect(page.getByRole('button', { name: /Enter board mode/i })).toHaveCount(0);
    // Board nav replaced the resident nav: Community Home is the board landing entry, and the
    // resident Dashboard entry is gone (the symmetric half of expectResidentSidebar).
    const sidebar = page.locator('.shell__side');
    await expect(sidebar.getByRole('link', { name: 'Community Home', exact: true })).toBeVisible();
    await expect(sidebar.getByRole('link', { name: 'Dashboard', exact: true })).toHaveCount(0);

    // Toggle back to Resident and confirm the resident view is intact.
    await page.locator('.mode-seg__btn', { hasText: /^Resident$/ }).click();
    // Switching back navigates the user to the resident dashboard.
    await page.waitForURL(/\/app\/dashboard/, { timeout: 15_000 });
    await expect(page.locator('.board-banner')).toHaveCount(0);
    await expect(page.getByRole('button', { name: /Enter board mode/i })).toBeVisible();
    await expectResidentSidebar(page);
  });

  test('sign-out then sign-in returns to the last-used board mode (Scenario 4)', async ({ page }) => {
    // Deterministic starting mode: this board account is shared with the sibling test, which
    // can leave Board persisted. Force Resident server-side first, otherwise the sign-in below
    // lands on the board home (FR-026) and there is no "Enter board mode" button to click.
    await setMode(page, 'Resident', boardEmail, PASSWORD);

    // Sign in (resident landing) and enter board mode so Board becomes the persisted mode.
    await loginAs(page, boardEmail, PASSWORD);
    await page.getByRole('button', { name: /Enter board mode/i }).click();
    await page.waitForURL(/\/app\/board\/home/, { timeout: 15_000 });
    await expect(page.locator('.board-banner')).toBeVisible();

    // Sign out.
    await page.getByRole('button', { name: /Sign out/i }).click();
    await expect(page).toHaveURL(/\/login/, { timeout: 10_000 });

    // Sign back in — the server-persisted last-used mode is Board, so sign-in must resume board
    // mode. Assert the LANDING URL as well as the chrome: FR-026 says a single-community board
    // user lands on their community's home, and checking only .board-banner/.mode-seg would
    // still pass while sign-in dumped the user on the resident dashboard.
    await loginAs(page, boardEmail, PASSWORD, BOARD_HOME_LANDING);
    await page.waitForURL(/\/app\/board\/home/, { timeout: 15_000 });
    await expect(page.locator('.board-banner')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('.mode-seg')).toBeVisible();
  });
});
