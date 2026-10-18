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
