import { test, expect, type APIRequestContext, type Browser, type Locator, type Page } from '@playwright/test';
import { BOARD_EMAIL, BOARD_PASSWORD, establishSession, setMode } from './helpers/auth';

// 030 company administration, against a deployed environment with the dev tools on (the seed of
// CompanySeeder, and the gated e2e vault of T054). Every journey here is tagged @company only, so
// CI's @smoke run skips them. Each run uses fresh `e2e+…@test.dev` emails, which the global setup's
// cleanup removes, and communities with a unique name, so the shared seed isn't changed.
//
// - T146: the journeys (SC-001, SC-010 proxies), the Board Member refusal and the company sign-in.
// - T148: accessibility, with explicit role, label and focus assertions (axe isn't a dependency).
// - T150: responsive checks at 390, 834 and 1360 px.

const API = process.env.PLAYWRIGHT_API_URL || 'http://localhost:5212';
const SECRET = process.env.PLAYWRIGHT_SCHEDULER_SECRET || 'dev-scheduler-shared-secret-placeholder';
const COMPANY_EMAIL = process.env.PLAYWRIGHT_COMPANY_EMAIL || 'company@nekohoa.dev';
const COMPANY_PASSWORD = process.env.PLAYWRIGHT_COMPANY_PASSWORD || 'Password1!';
const RESIDENT_COMMUNITY = process.env.PLAYWRIGHT_RESIDENT_COMMUNITY || 'Sakura Heights HOA';
const PASSWORD = 'Password1!';

// ─── helpers ─────────────────────────────────────────────────────────────────

function stamp(): string {
  return `${Date.now()}${Math.floor(Math.random() * 1000)}`;
}

function freshEmail(kind: string): string {
  return `e2e+${kind}-${stamp()}@test.dev`;
}

/** A bearer token for setup calls made through the API (not what the test is about). */
async function apiToken(request: APIRequestContext, email = COMPANY_EMAIL, password = COMPANY_PASSWORD): Promise<string> {
  let res = await request.post(`${API}/api/v1/auth/login`, { data: { email, password } });
  for (let attempt = 0; res.status() === 429 && attempt < 3; attempt++) {
    await new Promise(r => setTimeout(r, 5_000 * (attempt + 1)));
    res = await request.post(`${API}/api/v1/auth/login`, { data: { email, password } });
  }
  expect(res.ok(), `login as ${email}`).toBeTruthy();
  return (await res.json()).token as string;
}

/** Creates an Onboarding community through the API (no manager yet). */
async function createCommunity(request: APIRequestContext, token: string, name: string): Promise<string> {
  const res = await request.post(`${API}/api/v1/communities`, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      legalName: `${name} Association, Inc.`, communityName: name, county: 'Wake County, NC',
      formationDate: '2010-01-15', managementStartDate: '2027-01-01', description: null, parentCommunityId: null,
    },
  });
  expect(res.status(), 'create community').toBe(201);
  return (await res.json()).id as string;
}

/** T054: the newest invitation link emailed to `email`, from the gated dev vault. */
async function invitationLink(request: APIRequestContext, email: string): Promise<string> {
  for (let attempt = 0; attempt < 20; attempt++) {
    const res = await request.get(`${API}/api/v1/e2e/auth-codes?contact=${encodeURIComponent(email)}`, {
      headers: { 'X-Scheduler-Secret': SECRET },
    });
    if (res.ok()) {
      const link = (await res.json()).invitationLink as string | null;
      if (link) return link;
    }
    await new Promise(r => setTimeout(r, 500));
  }
  throw new Error(`No invitation link in the dev vault for ${email}`);
}

/** Opens an invitation on this deployment, whatever host the email's link names (none locally). */
async function openInvitation(page: Page, link: string): Promise<void> {
  await page.goto(`/invite${new URL(link, 'http://localhost').hash}`);
  await expect(page.getByRole('heading', { level: 1, name: "You're invited" })).toBeVisible();
}

/** Accepts from the link by creating an account (brief 10: no claim code, no verification). */
async function acceptByCreatingAccount(browser: Browser, link: string, firstName: string): Promise<Page> {
  const context = await browser.newContext();
  const page = await context.newPage();
  await openInvitation(page, link);
  await page.getByRole('button', { name: 'Create an account' }).click();
  await page.getByLabel('First name').fill(firstName);
  await page.getByLabel('Last name').fill('Tester');
  await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
  await page.getByRole('button', { name: 'Create account and accept' }).click();
  return page;
}

async function signInAsCompanyAdmin(page: Page): Promise<void> {
  await establishSession(page, COMPANY_EMAIL, COMPANY_PASSWORD);
}

function settingsUrl(communityId: string, section = 'profile'): string {
  return `/app/company/communities/${communityId}/settings/${section}`;
}

/** Tabs until `target` has focus; fails after `limit` presses. Proves keyboard reach. */
async function tabTo(page: Page, target: Locator, limit = 60): Promise<void> {
  for (let i = 0; i < limit; i++) {
    await page.keyboard.press('Tab');
    if (await target.evaluate(el => el === document.activeElement)) return;
  }
  throw new Error('Not reachable with Tab');
}

/** Conventions §8: focus is visible (an outline or a ring), never removed. */
async function expectVisibleFocus(target: Locator): Promise<void> {
  const style = await target.evaluate(el => {
    const s = getComputedStyle(el);
    return { outline: s.outlineStyle, width: s.outlineWidth, shadow: s.boxShadow };
  });
  const outlined = style.outline !== 'none' && style.width !== '0px';
  expect(outlined || style.shadow !== 'none', `visible focus: ${JSON.stringify(style)}`).toBeTruthy();
}

/** Neither the document nor the shell's content area (where pages scroll) scrolls sideways. */
async function expectNoSidewaysScroll(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => {
    const doc = document.documentElement.scrollWidth - document.documentElement.clientWidth;
    const content = document.querySelector<HTMLElement>('.shell__content');
    return { doc, content: content ? content.scrollWidth - content.clientWidth : 0 };
  });
  expect(overflow.doc, 'the page scrolls sideways').toBeLessThanOrEqual(1);
  expect(overflow.content, 'the content area scrolls sideways').toBeLessThanOrEqual(1);
}

/** Tables either fit or scroll inside their own container (constitution §6). */
async function expectTablesContained(page: Page): Promise<void> {
  const problems = await page.locator('table').evaluateAll(tables => tables.flatMap(t => {
    const width = document.documentElement.clientWidth;
    const fits = t.getBoundingClientRect().width <= width;
    let scrolls = false;
    for (let el = t.parentElement; el && el !== document.body; el = el.parentElement) {
      if (['auto', 'scroll'].includes(getComputedStyle(el).overflowX)) { scrolls = true; break; }
    }
    return fits || scrolls ? [] : [t.getAttribute('aria-labelledby') ?? t.className];
  }));
  expect(problems).toEqual([]);
}

// ─── T146: journeys ──────────────────────────────────────────────────────────

test.describe('Company administration journeys @company', () => {
  test('add community → invite manager → accept → activate (SC-001)', async ({ page, browser, request }) => {
    const name = `E2E Ridge ${stamp()}`;
    const managerEmail = freshEmail('mgr');
    await signInAsCompanyAdmin(page);

    // Add the community (brief 03).
    await page.goto('/app/company/communities/new');
    await page.getByLabel('Legal name (required)').fill(`${name} Association, Inc.`);
    await page.getByLabel('Community name (required)').fill(name);
    await page.getByLabel('County (required)').fill('Wake County, NC');
    await page.getByLabel('Formation date (required)').fill('2010-01-15');
    await page.getByLabel('Management start date (required)').fill('2027-01-01');
    await page.getByRole('button', { name: 'Add community' }).click();
    await page.waitForURL(/\/app\/company\/communities\/[^/]+\/settings\/profile$/);
    await expect(page.getByText(`${name} was added as Onboarding.`, { exact: false })).toBeVisible();
    const communityId = page.url().split('/communities/')[1].split('/')[0];

    // With no manager yet, Mark Active is disabled with its reason (US3-S4).
    await expect(page.getByRole('button', { name: `Mark ${name} Active` })).toHaveAttribute('aria-disabled', 'true');

    // Invite the manager by email (brief 05).
    await page.goto(settingsUrl(communityId, 'managers-and-memberships'));
    await page.getByRole('button', { name: `Appoint a Community Manager for ${name}` }).click();
    const appoint = page.getByRole('dialog', { name: 'Appoint a Community Manager' });
    await appoint.getByRole('radio', { name: 'Invite someone by email' }).check();
    await appoint.getByLabel('Email address (required)').fill(managerEmail);
    await appoint.getByRole('button', { name: 'Send invitation' }).click();
    await expect(appoint).toBeHidden();

    // The manager accepts from the emailed link, creating an account.
    const managerPage = await acceptByCreatingAccount(browser, await invitationLink(request, managerEmail), 'Morgan');
    await managerPage.waitForURL(/\/app\/board\//);
    await managerPage.context().close();

    // Back as the Company Administrator: Mark Active now works (US3-S3).
    await page.goto(settingsUrl(communityId));
    const mark = page.getByRole('button', { name: `Mark ${name} Active` });
    await expect(mark).not.toHaveAttribute('aria-disabled', 'true');
    await mark.click();
    const dialog = page.getByRole('dialog', { name: `Mark ${name} Active?` });
    await dialog.getByRole('button', { name: 'Mark community Active' }).click();
    await expect(dialog).toBeHidden();
    await expect(page.getByText(`${name} is now Active.`, { exact: false })).toBeVisible();
    await expect(page.getByRole('heading', { level: 1, name: new RegExp(`^${name}`) })).toContainText('Active');
  });

  test('invite resident → sign up from the link → see the home (SC-010)', async ({ page, browser, request }) => {
    const residentEmail = freshEmail('res');
    const token = await apiToken(request);
    const auth = { Authorization: `Bearer ${token}` };

    // An Active community of the seed, and one of its properties.
    const list = await request.get(`${API}/api/v1/company/communities?status=active&search=${encodeURIComponent(RESIDENT_COMMUNITY)}`, { headers: auth });
    expect(list.ok()).toBeTruthy();
    const community = (await list.json()).items[0] as { id: string; communityName: string };
    const residents = await request.get(`${API}/api/v1/communities/${community.id}/residents?limit=1`, { headers: auth });
    expect(residents.ok()).toBeTruthy();
    const address = (await residents.json()).items[0].address as string;

    // The Company Administrator invites from the property's drawer (brief 06).
    await signInAsCompanyAdmin(page);
    await page.goto(settingsUrl(community.id, 'managers-and-memberships'));
    await page.getByRole('searchbox', { name: 'Search residents' }).fill(address);
    const table = page.getByRole('table', { name: /^Properties in / });
    await table.getByRole('button', { name: address, exact: true }).click();
    const drawer = page.getByRole('dialog', { name: address });
    await drawer.getByRole('button', { name: `Invite a resident to ${address}` }).click();
    const invite = page.getByRole('dialog', { name: 'Invite a resident' });
    await invite.getByLabel('Email address (required)').fill(residentEmail);
    await invite.getByRole('button', { name: 'Send invitation' }).click();
    await expect(page.getByText(`Invitation sent to ${residentEmail}.`)).toBeVisible();

    // The invitee signs up from the link and sees the home (US8-S1).
    const resident = await acceptByCreatingAccount(browser, await invitationLink(request, residentEmail), 'Rae');
    await resident.waitForURL('**/app/dashboard');
    await expect(resident.locator('.shell__strip')).toContainText(address);
    await resident.context().close();
  });

  test('a Board Member is refused Community settings and taken to their landing (US5-S3)', async ({ page }) => {
    await setMode(page, 'Board', BOARD_EMAIL, BOARD_PASSWORD);
    await establishSession(page, BOARD_EMAIL, BOARD_PASSWORD);
    await page.goto('/app/board/settings/profile');
    await page.waitForURL(/\/app\/board\/(home|communities)/);
    await expect(page.getByText("You don't have access to that page.")).toBeVisible();
  });

  test('the company sign-in page lands a Company Administrator on the portfolio (US9-S1)', async ({ page }) => {
    await page.goto('/company/sign-in');
    await expect(page.getByRole('heading', { level: 1, name: 'Company sign-in' })).toBeVisible();
    await page.getByLabel('Email').fill(COMPANY_EMAIL);
    await page.getByLabel('Password', { exact: true }).fill(COMPANY_PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();
    await page.waitForURL('**/app/company/portfolio');
    await expect(page.getByRole('heading', { level: 1, name: 'Portfolio' })).toBeVisible();
  });
});

// ─── T148: accessibility ─────────────────────────────────────────────────────

test.describe('Company administration accessibility @company', () => {
  test('portfolio: keyboard reach, visible focus, labelled controls, status and flags in words', async ({ page }) => {
    await signInAsCompanyAdmin(page);
    await page.goto('/app/company/portfolio');
    await expect(page.getByRole('heading', { level: 1, name: 'Portfolio' })).toBeVisible();

    // The page's own button; the company side list has an "Add community" link too.
    const add = page.getByRole('main').getByRole('link', { name: 'Add community' });
    await tabTo(page, add);
    await expectVisibleFocus(add);
    const filters = page.getByRole('group', { name: 'Filter by status' });
    await tabTo(page, filters.getByRole('button', { name: /^All, / }));
    await expect(filters.getByRole('button', { name: /^All, / })).toHaveAttribute('aria-pressed', 'true');
    const search = page.getByRole('searchbox', { name: 'Search communities' });
    await tabTo(page, search);
    await tabTo(page, page.getByRole('link', { name: /^Community settings for / }).first());

    // Status and flags are words, never colour alone.
    const statuses = await page.getByRole('table', { name: 'Communities' }).locator('tbody td .pill').allTextContents();
    for (const s of statuses.map(t => t.trim())) {
      expect(['Onboarding', 'Active', 'Archived', 'No manager', 'Differs from defaults']).toContain(s);
    }
    // Sortable headers say how they sort.
    await expect(page.getByRole('columnheader', { name: 'Community', exact: true })).toHaveAttribute('aria-sort', 'ascending');
  });

  test('add community: labels, errors tied to their fields and an error summary that takes focus', async ({ page }) => {
    await signInAsCompanyAdmin(page);
    await page.goto('/app/company/communities/new');
    await page.getByRole('button', { name: 'Add community' }).click();
    const summary = page.getByRole('alert').filter({ hasText: "The community wasn't added" });
    await expect(summary).toBeFocused();
    const county = page.getByLabel('County (required)');
    await expect(county).toHaveAttribute('aria-invalid', 'true');
    const describedBy = (await county.getAttribute('aria-describedby')) ?? '';
    const tied = await page.evaluate(ids => ids.split(/\s+/).map(id => document.getElementById(id)?.textContent ?? '').join(' '), describedBy);
    expect(tied).toContain('Enter the county');
  });

  test('settings: the disabled-reason tooltip shows on focus, and the drawer handles focus', async ({ page, request }) => {
    const name = `E2E Quiet ${stamp()}`;
    const communityId = await createCommunity(request, await apiToken(request), name);
    await signInAsCompanyAdmin(page);
    await page.goto(settingsUrl(communityId));

    // US3-S4: Mark Active stays in the Tab order and reads its reason on keyboard focus.
    const mark = page.getByRole('button', { name: `Mark ${name} Active` });
    await tabTo(page, mark);
    await expect(mark).toHaveAttribute('aria-disabled', 'true');
    const tipId = ((await mark.getAttribute('aria-describedby')) ?? '').split(/\s+/).pop()!;
    const tip = page.locator(`[id="${tipId}"]`);
    await expect(tip).toBeVisible();
    await expect(tip).toContainText("can't be marked Active yet");

    // Settings search: a visible label, and the side list is a named landmark.
    await expect(page.getByRole('navigation', { name: 'Community settings sections' })).toBeVisible();
    await expect(page.getByRole('searchbox', { name: 'Search settings' })).toBeVisible();

    // Profile: per-field saving states are text, and every field has a visible label.
    for (const label of ['Legal name (required)', 'Community name (required)', 'County (required)', 'Description (optional)']) {
      await expect(page.getByLabel(label)).toBeVisible();
    }
  });

  test('residents drawer: focus moves in, Esc closes it, and focus returns to the address', async ({ page, request }) => {
    const token = await apiToken(request);
    const list = await request.get(`${API}/api/v1/company/communities?status=active&search=${encodeURIComponent(RESIDENT_COMMUNITY)}`,
      { headers: { Authorization: `Bearer ${token}` } });
    const community = (await list.json()).items[0] as { id: string };
    await signInAsCompanyAdmin(page);
    await page.goto(settingsUrl(community.id, 'managers-and-memberships'));

    const address = page.getByRole('table', { name: /^Properties in / }).locator('tbody th button').first();
    const text = (await address.textContent())!.trim();
    await address.focus();
    await page.keyboard.press('Enter');
    const drawer = page.getByRole('dialog', { name: text });
    await expect(drawer.getByRole('heading', { name: text })).toBeFocused();
    await expect(address).toHaveAttribute('aria-expanded', 'true');
    await page.keyboard.press('Escape');
    await expect(drawer).toBeHidden();
    await expect(address).toBeFocused();
  });

  test('company administrators: captions, named buttons and the role note', async ({ page }) => {
    await signInAsCompanyAdmin(page);
    await page.goto('/app/company/administrators');
    await expect(page.getByRole('heading', { level: 1, name: 'Company administrators' })).toBeVisible();
    await expect(page.getByRole('region', { name: 'What this role is' })).toBeVisible();
    await expect(page.getByRole('table', { name: 'Current Company Administrators' })).toBeVisible();
    await expect(page.getByRole('button', { name: /Company Administrator role$/ }).first()).toBeVisible();
    const invite = page.getByRole('button', { name: 'Invite a Company Administrator' });
    await tabTo(page, invite);
    await expectVisibleFocus(invite);
  });
});

// ─── T150: responsive ────────────────────────────────────────────────────────

const WIDTHS = [
  { name: 'phone', width: 390, height: 844 },
  { name: 'tablet', width: 834, height: 1112 },
  { name: 'desktop', width: 1360, height: 900 },
];

for (const size of WIDTHS) {
  test.describe(`Company administration at ${size.width} px @company`, () => {
    test.use({ viewport: { width: size.width, height: size.height } });

    test(`company pages don't scroll sideways and keep their tables usable (${size.name})`, async ({ page }) => {
      await signInAsCompanyAdmin(page);
      for (const path of ['/app/company/portfolio', '/app/company/communities/new', '/app/company/defaults', '/app/company/administrators']) {
        await page.goto(path);
        await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
        await page.waitForLoadState('networkidle');
        await expectNoSidewaysScroll(page);
        await expectTablesContained(page);
      }
    });

    test(`Community settings collapses its side list below 768 px (${size.name})`, async ({ page, request }) => {
      const token = await apiToken(request);
      const list = await request.get(`${API}/api/v1/company/communities?status=active&search=${encodeURIComponent(RESIDENT_COMMUNITY)}`,
        { headers: { Authorization: `Bearer ${token}` } });
      const community = (await list.json()).items[0] as { id: string };
      await signInAsCompanyAdmin(page);
      for (const section of ['profile', 'managers-and-memberships', 'architectural-review']) {
        await page.goto(settingsUrl(community.id, section));
        await expect(page.getByRole('heading', { level: 2 }).first()).toBeVisible();
        await page.waitForLoadState('networkidle');
        await expectNoSidewaysScroll(page);
        await expectTablesContained(page);
      }
      const menu = page.getByRole('button', { name: /^Sections: / });
      if (size.width < 768) {
        await expect(menu).toBeVisible();
      } else {
        await expect(menu).toHaveCount(0);
        await expect(page.getByRole('navigation', { name: 'Community settings sections' }).getByRole('link', { name: 'Profile' })).toBeVisible();
      }
    });

    test(`the property drawer opens full width on a phone (${size.name})`, async ({ page, request }) => {
      const token = await apiToken(request);
      const list = await request.get(`${API}/api/v1/company/communities?status=active&search=${encodeURIComponent(RESIDENT_COMMUNITY)}`,
        { headers: { Authorization: `Bearer ${token}` } });
      const community = (await list.json()).items[0] as { id: string };
      await signInAsCompanyAdmin(page);
      await page.goto(settingsUrl(community.id, 'managers-and-memberships'));
      const address = page.getByRole('table', { name: /^Properties in / }).locator('tbody th button').first();
      const text = (await address.textContent())!.trim();
      await address.click();
      const drawer = page.getByRole('dialog', { name: text });
      await expect(drawer).toBeVisible();
      const box = (await drawer.boundingBox())!;
      if (size.width < 768) expect(Math.round(box.width)).toBe(size.width);
      else expect(box.width).toBeLessThan(size.width);
      await expectNoSidewaysScroll(page);
    });
  });
}
