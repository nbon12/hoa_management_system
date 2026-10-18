import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { PortfolioComponent } from './portfolio.component';
import { CompanyService, PortfolioItem, PortfolioPage, PortfolioQuery } from '../../core/services/company.service';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { CurrentUser } from '../../core/models';

// 030 T073 / US1 / brief 02: the portfolio lists every community with its status in words and a
// link to its Community settings.

function portfolioRow(over: Partial<PortfolioItem> & { id: string; communityName: string }): PortfolioItem {
  return {
    legalName: `${over.communityName} HOA, Inc.`, status: 'Active', parent: null, managers: [],
    pendingManagerInvitations: [], noManager: false, differsFromDefaults: false, ...over,
  };
}

function portfolioPage(items: PortfolioItem[], over: Partial<PortfolioPage> = {}): PortfolioPage {
  return {
    items, total: items.length, limit: 25, offset: 0,
    counts: { all: items.length, onboarding: 0, active: items.length, archived: 0, noManager: 0 }, ...over,
  };
}

const ROBIN: CurrentUser = {
  id: 'u-robin', firstName: 'Robin', lastName: 'Hale', email: 'robin@example.com', initials: 'RH', lastActiveMode: 'Resident',
  memberships: [], properties: [], isCompanyAdministrator: true, landing: 'Company',
};

@Component({ standalone: true, template: '' })
class StubComponent {}

describe('PortfolioComponent (030 T073, minimal)', () => {
  let company: jasmine.SpyObj<CompanyService>;

  async function setup(result: PortfolioPage | HttpErrorResponse, user: CurrentUser = ROBIN) {
    company = jasmine.createSpyObj<CompanyService>('CompanyService', ['getCommunities']);
    if (result instanceof HttpErrorResponse) company.getCommunities.and.rejectWith(result);
    else company.getCommunities.and.resolveTo(result);
    const r = await render(PortfolioComponent, {
      providers: [
        provideRouter([]),
        { provide: CompanyService, useValue: company },
        { provide: AuthService, useValue: { user: signal(user).asReadonly() } },
      ],
    });
    await r.fixture.whenStable();
    r.fixture.detectChanges();
    return r;
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  const KEYSTONE_PARK = portfolioRow({ id: 'c-kp', communityName: 'Keystone Park', status: 'Onboarding', noManager: true });
  const OLD_MILL = portfolioRow({ id: 'c-omc', communityName: 'Old Mill Commons', status: 'Archived', legalName: '' });
  const SF = portfolioRow({ id: 'c-sf', communityName: 'Keystone Crossing SF', parent: { id: 'c-kcm', communityName: 'Keystone Crossing Master' } });

  it('lists each community with its status as text and a settings link (brief 02)', async () => {
    await setup(portfolioPage([KEYSTONE_PARK, OLD_MILL, SF]));
    expect(screen.getByRole('heading', { level: 1 }).textContent!.trim()).toBe('Portfolio');
    const table = screen.getByRole('table', { name: 'Communities' });
    const rows = within(table).getAllByRole('row').slice(1);
    expect(rows.length).toBe(3);
    const first = rows[0];
    expect(within(first).getByRole('rowheader').textContent!.trim()).toBe('Keystone Park');
    expect(within(first).getByText('Onboarding')).toBeTruthy();
    const link = within(first).getByRole('link', { name: 'Community settings for Keystone Park' });
    expect(link.textContent!.trim()).toBe('Community settings');
    expect(link.getAttribute('href')).toBe('/app/company/communities/c-kp/settings/profile');
    expect(within(rows[1]).getByText('Archived')).toBeTruthy();
    // Empty values read as "—", with words for screen readers.
    expect(within(rows[1]).getByText('No legal name')).toBeTruthy();
    expect(within(rows[2]).getByText('Keystone Crossing Master')).toBeTruthy();
    expect(company.getCommunities).toHaveBeenCalledWith(jasmine.objectContaining({ limit: 25, offset: 0 }));
  });

  it('has real column headers', async () => {
    await setup(portfolioPage([KEYSTONE_PARK]));
    const headers = screen.getAllByRole('columnheader').map(h => h.textContent!.trim());
    expect(headers[0]).toContain('Community');
    expect(headers).toContain('Status');
    expect(headers[headers.length - 1]).toBe('Settings');
  });

  it('offers Add community', async () => {
    await setup(portfolioPage([KEYSTONE_PARK]));
    expect(screen.getByRole('link', { name: 'Add community' }).getAttribute('href')).toBe('/app/company/communities/new');
  });

  it('pages 25 at a time and moves focus to the table heading', async () => {
    const r = await setup(portfolioPage([KEYSTONE_PARK], { total: 31 }));
    expect(screen.getByText(/Showing 1–25 of 31/)).toBeTruthy();
    company.getCommunities.and.resolveTo(portfolioPage([OLD_MILL], { total: 31, offset: 25 }));
    fireEvent.click(screen.getByRole('button', { name: 'Next page' }));
    await until(() => company.getCommunities.calls.count() === 2);
    expect((company.getCommunities.calls.mostRecent().args[0] as PortfolioQuery).offset).toBe(25);
    await r.fixture.whenStable();
    r.fixture.detectChanges();
    expect(screen.getByText(/Showing 26–31 of 31/)).toBeTruthy();
    await until(() => document.activeElement === screen.getByRole('heading', { name: 'Communities' }));
  });

  it('says when the company has no communities yet, with Add community', async () => {
    await setup(portfolioPage([], { counts: { all: 0, onboarding: 0, active: 0, archived: 0, noManager: 0 } }));
    expect(screen.getByText('The company has no communities yet.')).toBeTruthy();
    expect(screen.queryByRole('table')).toBeNull();
  });

  it('says when the list could not load, and tries again', async () => {
    const r = await setup(new HttpErrorResponse({ status: 0 }));
    expect(screen.getByText("Couldn't load communities.")).toBeTruthy();
    company.getCommunities.and.resolveTo(portfolioPage([KEYSTONE_PARK]));
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await until(() => company.getCommunities.calls.count() === 2);
    await r.fixture.whenStable();
    r.fixture.detectChanges();
    expect(screen.getByRole('table', { name: 'Communities' })).toBeTruthy();
  });

  it('a 403 sends the person to their landing with the page refusal (FR-035)', async () => {
    company = jasmine.createSpyObj<CompanyService>('CompanyService', ['getCommunities']);
    company.getCommunities.and.rejectWith(new HttpErrorResponse({ status: 403, error: { code: 'FORBIDDEN', message: 'x' } }));
    await render(PortfolioComponent, {
      providers: [
        provideRouter([{ path: '**', component: StubComponent }]),
        { provide: CompanyService, useValue: company },
        { provide: AuthService, useValue: { user: signal({ ...ROBIN, isCompanyAdministrator: false, landing: 'Resident' }).asReadonly() } },
      ],
    });
    await until(() => TestBed.inject(Router).url === '/app/dashboard');
    expect(TestBed.inject(ToastService).toasts().map(t => t.message)).toEqual(["You don't have access to that page."]);
    expect(screen.queryByRole('table')).toBeNull();
  });
});

// 030 T143 / US6 / brief 02: the portfolio's flags, status filter with counts, "No manager" filter,
// search and sortable columns.
describe('PortfolioComponent (030 T143, US6)', () => {
  let company: jasmine.SpyObj<CompanyService>;
  const COUNTS = { all: 31, onboarding: 3, active: 26, archived: 2, noManager: 2 };

  const BRIAR = portfolioRow({ id: 'c-bh', communityName: 'Briar Hollow', status: 'Onboarding', noManager: true });
  const KCM = portfolioRow({ id: 'c-kcm', communityName: 'Keystone Crossing Master', managers: [{ userId: 'u-jordan', name: 'Jordan Blake' }] });
  const SF = portfolioRow({
    id: 'c-sf', communityName: 'Keystone Crossing SF', parent: { id: 'c-kcm', communityName: 'Keystone Crossing Master' },
    managers: [{ userId: 'u-jordan', name: 'Jordan Blake' }], differsFromDefaults: true,
  });
  const KP = portfolioRow({
    id: 'c-kp', communityName: 'Keystone Park', status: 'Onboarding', noManager: true,
    pendingManagerInvitations: [{ email: 'pat@example.com', expiresAt: '2099-10-15T12:00:00Z' }],
  });

  async function setup(result: PortfolioPage = portfolioPage([BRIAR, KCM, SF, KP], { total: 31, counts: COUNTS })) {
    company = jasmine.createSpyObj<CompanyService>('CompanyService', ['getCommunities']);
    company.getCommunities.and.resolveTo(result);
    const r = await render(PortfolioComponent, {
      providers: [
        provideRouter([]),
        { provide: CompanyService, useValue: company },
        { provide: AuthService, useValue: { user: signal(ROBIN).asReadonly() } },
      ],
    });
    await settle(r);
    return r;
  }

  async function settle(r: Awaited<ReturnType<typeof setup>>) {
    await r.fixture.whenStable();
    r.fixture.detectChanges();
    await r.fixture.whenStable();
    r.fixture.detectChanges();
  }

  function lastQuery(): PortfolioQuery {
    return company.getCommunities.calls.mostRecent().args[0] as PortfolioQuery;
  }

  function rowOf(name: string): HTMLElement {
    return within(screen.getByRole('table', { name: 'Communities' })).getAllByRole('row').find(r => r.querySelector('th[scope="row"]')?.textContent!.trim() === name)!;
  }

  function cell(row: HTMLElement, column: string): HTMLElement {
    const headers = within(screen.getByRole('table', { name: 'Communities' })).getAllByRole('columnheader').map(h => h.textContent!.trim());
    return Array.from(row.children)[headers.indexOf(column)] as HTMLElement;
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  it('has the columns of brief 02, in order', async () => {
    await setup();
    expect(screen.getAllByRole('columnheader').map(h => h.textContent!.trim()))
      .toEqual(['Community', 'Legal name', 'Status', 'Parent', 'Managers', 'Architectural review', 'Settings']);
  });

  it('shows "No manager" and "Differs from defaults" as text labels, with managers and pending manager invitations (US6-S1, US6-S2, US6-S3)', async () => {
    await setup();
    expect(within(cell(rowOf('Briar Hollow'), 'Managers')).getByText('No manager')).toBeTruthy();
    expect(within(cell(rowOf('Keystone Crossing Master'), 'Managers')).getByText('Jordan Blake')).toBeTruthy();
    expect(within(cell(rowOf('Keystone Crossing Master'), 'Managers')).queryByText('No manager')).toBeNull();
    const kp = cell(rowOf('Keystone Park'), 'Managers');
    expect(within(kp).getByText('No manager')).toBeTruthy();
    expect(kp.textContent!.replace(/\s+/g, ' ')).toContain('pat@example.com · invitation pending');
    expect(within(kp).getByText('pat@example.com, invitation pending').classList).toContain('sr-only');
    expect(within(cell(rowOf('Keystone Crossing SF'), 'Architectural review')).getByText('Differs from defaults')).toBeTruthy();
    expect(within(cell(rowOf('Keystone Crossing Master'), 'Architectural review')).getByText('Same as company defaults')).toBeTruthy();
  });

  it('filters by status, with counts, one at a time, back on page 1 (US6-S4, US6-S7)', async () => {
    const r = await setup();
    const group = screen.getByRole('group', { name: 'Filter by status' });
    expect(within(group).getAllByRole('button').map(b => b.textContent!.replace(/\s+/g, ' ').trim()))
      .toEqual(['All · 31', 'Onboarding · 3', 'Active · 26', 'Archived · 2']);
    const all = within(group).getByRole('button', { name: 'All, 31 communities' });
    expect(all.getAttribute('aria-pressed')).toBe('true');

    company.getCommunities.and.resolveTo(portfolioPage([portfolioRow({ id: 'c-om', communityName: 'Old Mill Commons', status: 'Archived' })], { total: 1, counts: COUNTS }));
    const archived = within(group).getByRole('button', { name: 'Archived, 2 communities' });
    archived.focus();
    fireEvent.click(archived);
    await settle(r);
    expect(lastQuery()).toEqual(jasmine.objectContaining({ status: 'archived', offset: 0 }));
    expect(archived.getAttribute('aria-pressed')).toBe('true');
    expect(all.getAttribute('aria-pressed')).toBe('false');
    expect(document.activeElement).toBe(archived);
    expect(screen.getByText(/Showing 1–1 of 1/)).toBeTruthy();
  });

  it('narrows to communities with no manager, together with the status (US6-S7)', async () => {
    const r = await setup();
    const toggle = screen.getByRole('button', { name: 'No manager, 2 communities' });
    expect(toggle.textContent!.replace(/\s+/g, ' ').trim()).toBe('No manager · 2');
    expect(toggle.getAttribute('aria-pressed')).toBe('false');
    company.getCommunities.and.resolveTo(portfolioPage([BRIAR, KP], { total: 2, counts: COUNTS }));
    fireEvent.click(toggle);
    await settle(r);
    expect(lastQuery()).toEqual(jasmine.objectContaining({ noManager: true, offset: 0 }));
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
    fireEvent.click(screen.getByRole('button', { name: 'Onboarding, 3 communities' }));
    await settle(r);
    expect(lastQuery()).toEqual(jasmine.objectContaining({ noManager: true, status: 'onboarding' }));
    fireEvent.click(toggle);
    await settle(r);
    expect(lastQuery().noManager).toBeUndefined();
  });

  it('searches community and legal names as the person types (US6-S5)', async () => {
    const r = await setup();
    const search = screen.getByRole('searchbox', { name: 'Search communities' }) as HTMLInputElement;
    expect(search.placeholder).toBe('Search by community or legal name');
    company.getCommunities.and.resolveTo(portfolioPage([KCM, SF, KP], { total: 3, counts: COUNTS }));
    fireEvent.input(search, { target: { value: '  keystone ' } });
    await until(() => company.getCommunities.calls.count() === 2);
    await settle(r);
    expect(lastQuery()).toEqual(jasmine.objectContaining({ search: 'keystone', offset: 0 }));
    expect(screen.getByText(/Showing 1–3 of 3/)).toBeTruthy();
  });

  it('says when nothing matches, and "Clear search" restores the list and focuses the field', async () => {
    const r = await setup();
    const search = screen.getByRole('searchbox', { name: 'Search communities' }) as HTMLInputElement;
    company.getCommunities.and.resolveTo(portfolioPage([], { total: 0, counts: COUNTS }));
    fireEvent.input(search, { target: { value: 'Keystone Pk' } });
    await until(() => company.getCommunities.calls.count() === 2);
    await settle(r);
    expect(screen.getByText('No communities match “Keystone Pk”.')).toBeTruthy();
    expect(screen.queryByText(/has no communities yet/)).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Archived, 2 communities' }));
    await settle(r);
    expect(screen.getByText('No Archived communities match “Keystone Pk”.')).toBeTruthy();

    company.getCommunities.and.resolveTo(portfolioPage([BRIAR], { total: 1, counts: COUNTS }));
    fireEvent.click(screen.getByRole('button', { name: 'Clear search' }));
    await settle(r);
    expect(search.value).toBe('');
    expect(lastQuery().search).toBeUndefined();
    expect(document.activeElement).toBe(search);
  });

  it('says "No Archived communities." for a filter with no rows', async () => {
    const r = await setup();
    company.getCommunities.and.resolveTo(portfolioPage([], { total: 0, counts: { ...COUNTS, archived: 0 } }));
    fireEvent.click(screen.getByRole('button', { name: 'Archived, 2 communities' }));
    await settle(r);
    expect(screen.getByText('No Archived communities.')).toBeTruthy();
  });

  it('sorts by the Community, Status and Parent headers, with aria-sort (US6-S7)', async () => {
    const r = await setup();
    const header = (name: string) => screen.getAllByRole('columnheader').find(h => h.textContent!.trim() === name)!;
    expect(header('Community').getAttribute('aria-sort')).toBe('ascending');
    expect(header('Status').getAttribute('aria-sort')).toBe('none');
    expect(header('Legal name').hasAttribute('aria-sort')).toBeFalse();
    expect(lastQuery()).toEqual(jasmine.objectContaining({ sort: 'name', dir: 'asc' }));

    fireEvent.click(within(header('Status')).getByRole('button', { name: 'Status' }));
    await settle(r);
    expect(lastQuery()).toEqual(jasmine.objectContaining({ sort: 'status', dir: 'asc', offset: 0 }));
    expect(header('Status').getAttribute('aria-sort')).toBe('ascending');
    expect(header('Community').getAttribute('aria-sort')).toBe('none');

    fireEvent.click(within(header('Status')).getByRole('button', { name: 'Status' }));
    await settle(r);
    expect(lastQuery()).toEqual(jasmine.objectContaining({ sort: 'status', dir: 'desc' }));
    expect(header('Status').getAttribute('aria-sort')).toBe('descending');

    fireEvent.click(within(header('Parent')).getByRole('button', { name: 'Parent' }));
    await settle(r);
    expect(lastQuery()).toEqual(jasmine.objectContaining({ sort: 'parent', dir: 'asc' }));
  });
});
