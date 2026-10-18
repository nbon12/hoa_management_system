import { Component, NgZone, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, RouterOutlet, provideRouter } from '@angular/router';
import { render, screen, fireEvent } from '@testing-library/angular';
import { ArchivedNoticeComponent } from './archived-notice.component';
import { authInterceptor } from '../core/interceptors/auth.interceptor';
import { AuthService } from '../core/services/auth.service';
import { TokenService } from '../core/services/token.service';
import { CurrentUser, UserPropertySummary } from '../core/models';
import { environment } from '../../environments/environment';

// 030 T102 / US3-S9 / brief 11 §4.3: a resident page refused with 409 COMMUNITY_ARCHIVED shows the
// archived notice instead of the page. Statement stays readable, with the notice as a banner.

@Component({ selector: 'app-host', standalone: true, imports: [ArchivedNoticeComponent, RouterOutlet], template: '<app-archived-notice><router-outlet /></app-archived-notice>' })
class HostComponent {}

@Component({ selector: 'app-dashboard-stub', standalone: true, template: '<h1>Dashboard page</h1>' })
class DashboardStubComponent {}

@Component({ selector: 'app-one-time-stub', standalone: true, template: '<h1>Pay your dues</h1>' })
class OneTimeStubComponent {}

@Component({ selector: 'app-statement-stub', standalone: true, template: '<h1>Billing &amp; payments</h1>' })
class StatementStubComponent {}

@Component({ selector: 'app-board-stub', standalone: true, template: '<h1>Board page</h1>' })
class BoardStubComponent {}

function jwt(claims: Record<string, unknown>): string {
  const b64 = (o: unknown) => btoa(JSON.stringify(o)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${b64({ alg: 'none' })}.${b64(claims)}.sig`;
}

function home(status: UserPropertySummary['communityStatus']): UserPropertySummary {
  return { id: 'p16', accountNumber: 'A-16', address: '16 Old Mill Ln', communityId: 'c-om', communityName: 'Old Mill Commons', communityStatus: status };
}

const PAGE_TEXT = "The management company no longer manages Old Mill Commons. You can still view this home's ledger, statements, receipts and transactions.";
const PAYMENTS_TEXT = "Payments and autopay aren't available for this home, because the management company no longer manages Old Mill Commons.";

describe('ArchivedNoticeComponent (030 T102)', () => {
  const userSig = signal<CurrentUser | null>(null);
  let refreshCurrentUser: jasmine.Spy;

  async function setup(status: UserPropertySummary['communityStatus'] = 'Active') {
    userSig.set({
      id: 'u-sam', firstName: 'Sam', lastName: 'Ortiz', email: 'sam@example.com', initials: 'SO', lastActiveMode: 'Resident',
      memberships: [], properties: [home(status)], isCompanyAdministrator: false, landing: 'Resident',
    });
    refreshCurrentUser = jasmine.createSpy('refreshCurrentUser').and.callFake(async () => {
      userSig.update(u => ({ ...u!, properties: [home('Archived')] }));
      return userSig();
    });
    const r = await render(HostComponent, {
      providers: [
        provideRouter([
          { path: 'app/dashboard', component: DashboardStubComponent },
          { path: 'app/payments/one-time', component: OneTimeStubComponent },
          { path: 'app/payments/statement', component: StatementStubComponent },
          { path: 'app/board/home', component: BoardStubComponent },
        ]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { user: userSig.asReadonly(), refreshCurrentUser } },
      ],
    });
    TestBed.inject(TokenService).setAccessToken(jwt({ propertyId: 'p16', exp: Math.floor(Date.now() / 1000) + 3600 }));
    return r;
  }

  async function go(r: Awaited<ReturnType<typeof setup>>, url: string) {
    await TestBed.inject(NgZone).run(() => TestBed.inject(Router).navigateByUrl(url));
    await r.fixture.whenStable();
    r.fixture.detectChanges();
  }

  async function refuse(r: Awaited<ReturnType<typeof setup>>, method: 'GET' | 'POST', path: string, code = 'COMMUNITY_ARCHIVED') {
    const http = TestBed.inject(HttpClient);
    const url = `${environment.apiBaseUrl}${path}`;
    (method === 'GET' ? http.get(url) : http.post(url, {})).subscribe({ error: () => undefined });
    TestBed.inject(HttpTestingController).expectOne(url).flush({ code, message: 'Refused.' }, { status: 409, statusText: 'Conflict' });
    await r.fixture.whenStable();
    r.fixture.detectChanges();
  }

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    const tokens = TestBed.inject(TokenService);
    tokens.clearAccessToken();
    tokens.setSessionHint(false);
  });

  it('shows the page until a request is refused', async () => {
    const r = await setup();
    await go(r, '/app/dashboard');
    expect(screen.getByRole('heading', { name: 'Dashboard page' })).toBeTruthy();
    expect(screen.queryByText(PAGE_TEXT)).toBeNull();
  });

  it('a 409 COMMUNITY_ARCHIVED on a resident page shows the archived notice instead of the page (US3-S9, 8.11.e)', async () => {
    const r = await setup();
    await go(r, '/app/dashboard');
    await refuse(r, 'GET', '/dashboard/summary');

    expect(screen.getByText(PAGE_TEXT)).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Dashboard page' })).toBeNull();
    expect(screen.queryByText('No payment was made.')).toBeNull();
    expect(screen.getByRole('link', { name: 'View billing history' }).getAttribute('href')).toBe('/app/payments/statement');
    // The home list learns the home is no longer managed.
    expect(refreshCurrentUser).toHaveBeenCalledTimes(1);
  });

  it('One-time shows the payments notice, and an old-tab payment adds "No payment was made." (8.11.f, 8.11.g)', async () => {
    const r = await setup();
    await go(r, '/app/payments/one-time');
    await refuse(r, 'POST', '/payments/one-time');
    expect(screen.getByText(PAYMENTS_TEXT)).toBeTruthy();
    expect(screen.getByText('No payment was made.')).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Pay your dues' })).toBeNull();
  });

  it('a refused read on a payments page has no payment line', async () => {
    const r = await setup();
    await go(r, '/app/payments/one-time');
    await refuse(r, 'GET', '/payments/methods');
    expect(screen.getByText(PAYMENTS_TEXT)).toBeTruthy();
    expect(screen.queryByText('No payment was made.')).toBeNull();
  });

  it('"View billing history" opens Statement, which stays readable with the notice as a banner (8.11.d)', async () => {
    const r = await setup();
    await go(r, '/app/dashboard');
    await refuse(r, 'GET', '/dashboard/summary');
    fireEvent.click(screen.getByRole('link', { name: 'View billing history' }));
    await r.fixture.whenStable();
    r.fixture.detectChanges();

    expect(TestBed.inject(Router).url).toBe('/app/payments/statement');
    expect(screen.getByRole('heading', { name: 'Billing & payments' })).toBeTruthy();
    expect(screen.getByText(PAGE_TEXT)).toBeTruthy();
    expect(screen.queryByRole('link', { name: 'View billing history' })).toBeNull();
  });

  it('Statement for a home already known to be archived shows the banner', async () => {
    const r = await setup('Archived');
    await go(r, '/app/payments/statement');
    expect(screen.getByText(PAGE_TEXT)).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'Billing & payments' })).toBeTruthy();
  });

  it('moving to another page shows that page again until it is refused too', async () => {
    const r = await setup();
    await go(r, '/app/dashboard');
    await refuse(r, 'GET', '/dashboard/summary');
    await go(r, '/app/payments/one-time');
    expect(screen.getByRole('heading', { name: 'Pay your dues' })).toBeTruthy();
    expect(screen.queryByText(PAYMENTS_TEXT)).toBeNull();
  });

  it('ignores other codes, and refusals outside resident pages', async () => {
    const r = await setup();
    await go(r, '/app/dashboard');
    await refuse(r, 'GET', '/dashboard/summary', 'INVALID_TRANSITION');
    expect(screen.getByRole('heading', { name: 'Dashboard page' })).toBeTruthy();

    await go(r, '/app/board/home');
    await refuse(r, 'GET', '/board/metrics');
    expect(screen.getByRole('heading', { name: 'Board page' })).toBeTruthy();
    expect(screen.queryByText(/no longer manages/)).toBeNull();
    expect(refreshCurrentUser).not.toHaveBeenCalled();
  });
});
