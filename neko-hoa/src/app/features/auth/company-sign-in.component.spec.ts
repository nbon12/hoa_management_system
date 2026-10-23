import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { render, screen, fireEvent } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { CompanySignInComponent } from './company-sign-in.component';
import { AuthService } from '../../core/services/auth.service';
import { PageNoticeService } from '../../core/services/page-notice.service';
import { PageNoticeComponent } from '../../shared/page-notice.component';
import { environment } from '../../../environments/environment';

// 030 T121 / FR-036c / brief 01 8.01.a–c: the bookmarkable company sign-in page. It uses the usual
// POST /auth/login and grants nothing: a Company Administrator lands on the portfolio; anyone else
// lands on their usual page with a notice. Failures stay as generic as on the usual page.

const BASE = environment.apiBaseUrl;

@Component({ standalone: true, imports: [PageNoticeComponent], template: '<app-page-notice /><h1>Landing</h1>' })
class LandingStubComponent {}

function session(over: Record<string, unknown>) {
  return {
    token: 'h.eyJleHAiOjk5OTk5OTk5OTl9.s', expiresAt: '2099-01-01T00:00:00Z',
    user: {
      id: 'u1', firstName: 'Robin', lastName: 'Hale', email: 'company@nekohoa.dev', initials: 'RH',
      properties: [], lastActiveMode: 'Resident', memberships: [], isCompanyAdministrator: true, landing: 'Company',
      ...over,
    },
  };
}

describe('CompanySignInComponent (030 T121)', () => {
  let http: HttpTestingController;

  async function setup() {
    localStorage.clear();
    const r = await render(CompanySignInComponent, {
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', component: LandingStubComponent }]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    return r;
  }

  afterEach(() => {
    http.verify();
    TestBed.inject(PageNoticeService).dismiss();
  });

  function signIn(email = 'company@nekohoa.dev', password = 'Juniper#2026') {
    fireEvent.input(screen.getByLabelText('Email'), { target: { value: email } });
    fireEvent.input(screen.getByLabelText('Password'), { target: { value: password } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  }

  function url(): string {
    return TestBed.inject(Router).url;
  }

  it('is titled "Company sign-in", names no company and links to the usual page (8.01.a)', async () => {
    await setup();
    expect(screen.getByRole('heading', { level: 1 }).textContent!.trim()).toBe('Company sign-in');
    expect(screen.getByText('For company staff. Bookmark this page.')).toBeTruthy();
    expect(document.body.textContent).not.toContain('Juniper');
    expect(screen.getByRole('link', { name: 'Use the usual sign-in page.' }).getAttribute('href')).toBe('/login');
    expect(screen.getByText(/Not company staff\?/)).toBeTruthy();
  });

  it('calls POST /auth/login, then routes a Company Administrator to the portfolio (US9-S7)', async () => {
    await setup();
    signIn();
    const req = http.expectOne(`${BASE}/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'company@nekohoa.dev', password: 'Juniper#2026' });
    expect(req.request.withCredentials).toBeTrue();
    req.flush(session({}));
    await until(() => url() === '/app/company/portfolio');
    expect(TestBed.inject(AuthService).user()!.isCompanyAdministrator).toBeTrue();
  });

  it('routes anyone else to their usual landing with ?notice=not-company-admin, which shows the notice (US9-S8, 8.01.b)', async () => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        provideRouter([
          { path: 'company/sign-in', component: CompanySignInComponent },
          { path: '**', component: LandingStubComponent },
        ]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create('/company/sign-in');
    signIn('jordan@example.com', 'Sakura#2026');
    http.expectOne(`${BASE}/auth/login`).flush(session({
      email: 'jordan@example.com', isCompanyAdministrator: false, landing: 'Board', lastActiveMode: 'Board',
      memberships: [
        { communityId: 'c-sh', communityName: 'Sakura Heights', role: 'CommunityManager', communityStatus: 'Active' },
        { communityId: 'c-kr', communityName: 'Keystone Ridge', role: 'CommunityManager', communityStatus: 'Onboarding' },
      ],
    }));
    // The harness doesn't detect changes on its own; the landing page runs once it is checked.
    await until(() => { harness.detectChanges(); return url() === '/app/board/communities'; });
    await harness.fixture.whenStable();
    harness.detectChanges();
    // The landing page read the notice key, showed it, and took the key out of the address.
    expect(screen.getByText("This account isn't a Company Administrator, so you've been signed in to your usual page.")).toBeTruthy();
    expect(url()).toBe('/app/board/communities');
  });

  it('asks for the landing with the notice key before it is removed', async () => {
    await setup();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    signIn('avery@example.com', 'Sakura#2026');
    http.expectOne(`${BASE}/auth/login`).flush(session({ isCompanyAdministrator: false, landing: 'Resident' }));
    await until(() => navigate.calls.count() > 0);
    expect(navigate).toHaveBeenCalledWith(['/app/dashboard'], { queryParams: { notice: 'not-company-admin' } });
  });

  it('shows the usual generic failure and goes nowhere (8.01.c)', async () => {
    await setup();
    signIn('company@nekohoa.dev', 'wrong');
    http.expectOne(`${BASE}/auth/login`).flush({ code: 'INVALID_CREDENTIALS', message: 'x' }, { status: 401, statusText: 'Unauthorized' });
    await until(() => !!screen.queryByText('Invalid email or password.'));
    expect(screen.getByText('Invalid email or password.').closest('[role="alert"]')).toBeTruthy();
    expect(url()).toBe('/');
  });

  it('shows the rate-limit message when sign-in is rate limited', async () => {
    await setup();
    signIn();
    http.expectOne(`${BASE}/auth/login`).flush({}, { status: 429, statusText: 'Too Many Requests' });
    await until(() => !!screen.queryByText('Too many requests. Wait a few minutes, then try again.'));
  });

  it('asks for both fields before calling the server', async () => {
    await setup();
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(screen.getByText('Please enter your email and password.')).toBeTruthy();
    http.expectNone(`${BASE}/auth/login`);
  });

  it('sends a Company Administrator who is already signed in straight to the portfolio', async () => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: '**', component: LandingStubComponent }])],
    });
    TestBed.inject(AuthService).applySession(session({}) as never);
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(CompanySignInComponent);
    fixture.detectChanges();
    await until(() => url() === '/app/company/portfolio');
    http.expectNone(`${BASE}/auth/login`);
  });
});
