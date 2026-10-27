import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { authInterceptor } from './auth.interceptor';
import { TokenService } from '../services/token.service';
import { PageNoticeService } from '../services/page-notice.service';
import { AuthService } from '../services/auth.service';

const API = environment.apiBaseUrl;
const REFRESH_URL = `${API}/auth/refresh`;

const SESSION = {
  token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fresh',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'u1', firstName: 'Rin', lastName: 'Neko', email: 'resident@nekohoa.dev',
    initials: 'RN', properties: [],
  },
};

const settle = () => new Promise<void>(r => setTimeout(r));
async function takeRequests(http: HttpTestingController, url: string, count = 1) {
  const taken: ReturnType<HttpTestingController['match']> = [];
  for (let i = 0; i < 200 && taken.length < count; i++) {
    taken.push(...http.match(url));
    if (taken.length < count) await settle();
  }
  return taken;
}

// 020-D FR-D4/FR-D5 (T015): bearer only on API-origin requests; 401s funnel through the
// single-flight session-refresh coordinator.
describe('authInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let tokens: TokenService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenService);
  });

  afterEach(() => http.verify());

  it('attaches the bearer to API-origin requests', () => {
    tokens.setAccessToken('tok-1');

    client.get(`${API}/dashboard`).subscribe();

    const req = http.expectOne(`${API}/dashboard`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok-1');
    req.flush({});
  });

  it('never attaches the bearer to non-API origins (FR-D4)', () => {
    tokens.setAccessToken('tok-1');

    client.get('https://elsewhere.example/resource').subscribe({ error: () => {} });

    const req = http.expectOne('https://elsewhere.example/resource');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('on 401 refreshes once and retries with the new token', async () => {
    tokens.setAccessToken('stale');
    let result: unknown;
    client.get(`${API}/dashboard`).subscribe(r => (result = r));

    http.expectOne(`${API}/dashboard`).flush({}, { status: 401, statusText: 'Unauthorized' });

    const [refresh] = await takeRequests(http, REFRESH_URL);
    expect(refresh).toBeDefined();
    refresh.flush(SESSION);

    const retried = (await takeRequests(http, `${API}/dashboard`))[0];
    expect(retried.request.headers.get('Authorization')).toBe(`Bearer ${SESSION.token}`);
    retried.flush({ ok: true });
    await settle();
    expect(result).toEqual({ ok: true });
  });

  it('concurrent 401s share a single refresh (FR-D5)', async () => {
    tokens.setAccessToken('stale');
    client.get(`${API}/a`).subscribe({ error: () => {} });
    client.get(`${API}/b`).subscribe({ error: () => {} });

    http.expectOne(`${API}/a`).flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne(`${API}/b`).flush({}, { status: 401, statusText: 'Unauthorized' });

    const refreshes = await takeRequests(http, REFRESH_URL);
    expect(refreshes.length).toBe(1);
    refreshes[0].flush(SESSION);

    const retries = await takeRequests(http, `${API}/a`).then(async a =>
      a.concat(await takeRequests(http, `${API}/b`)));
    retries.forEach(r => r.flush({}));
    await settle();
    expect(http.match(REFRESH_URL).length).toBe(0);
  });

  it('propagates the original 401 when the refresh fails', async () => {
    tokens.setAccessToken('stale');
    let status = 0;
    client.get(`${API}/dashboard`).subscribe({ error: e => (status = e.status) });

    http.expectOne(`${API}/dashboard`).flush({}, { status: 401, statusText: 'Unauthorized' });

    const [refresh] = await takeRequests(http, REFRESH_URL);
    refresh.flush({ code: 'INVALID_REFRESH_TOKEN' }, { status: 401, statusText: 'Unauthorized' });

    await settle();
    expect(status).toBe(401);
  });

  it('does not try to refresh for failed /auth/ requests themselves', () => {
    let status = 0;
    client.post(`${API}/auth/login`, {}).subscribe({ error: e => (status = e.status) });

    http.expectOne(`${API}/auth/login`).flush({}, { status: 401, statusText: 'Unauthorized' });

    http.expectNone(REFRESH_URL);
    expect(status).toBe(401);
  });
});

@Component({ standalone: true, template: '' })
class StubPageComponent {}

/** A JWT the client can read (the signature is never checked client-side). */
function jwt(claims: Record<string, unknown>): string {
  const b64 = (o: unknown) => btoa(JSON.stringify(o)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${b64({ alg: 'HS256', typ: 'JWT' })}.${b64({ exp: 9_999_999_999, ...claims })}.sig`;
}

function me(over: Record<string, unknown>) {
  return {
    id: 'u1', firstName: 'Sam', lastName: 'Ortiz', email: 'sam@example.com', initials: 'SO',
    properties: [], lastActiveMode: 'Resident', memberships: [], isCompanyAdministrator: false, landing: 'Resident',
    ...over,
  };
}

const OLD_MILL = { id: 'p-old-mill', accountNumber: 'OMC-016', address: '16 Old Mill Ln', communityId: 'c-omc', communityName: 'Old Mill Commons', communityStatus: 'Archived' };
const DENIED = { code: 'PROPERTY_ACCESS_DENIED', message: 'You no longer have access to that home.' };

// 030 T122 / FR-024g / US9-S4 / brief 11 §6: a 403 PROPERTY_ACCESS_DENIED re-reads /auth/me and
// moves the person to another home or to their landing, with brief 11's notice, and never loops.
describe('authInterceptor — PROPERTY_ACCESS_DENIED (030 T122)', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let tokens: TokenService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', component: StubPageComponent }]),
      ],
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenService);
  });

  afterEach(() => http.verify());

  function deny(url = `${API}/payments/statements`) {
    let status = 0;
    client.get(url).subscribe({ error: e => (status = e.status) });
    http.expectOne(url).flush(DENIED, { status: 403, statusText: 'Forbidden' });
    return () => status;
  }

  async function until(cond: () => boolean) {
    for (let i = 0; i < 200 && !cond(); i++) await settle();
  }

  function notice(): string | null {
    return TestBed.inject(PageNoticeService).message();
  }

  it('moves a resident with another home there, with the "Now showing" notice (8.11.h)', async () => {
    tokens.setAccessToken(jwt({ propertyId: 'p-29' }));
    const status = deny();

    const [meReq] = await takeRequests(http, `${API}/auth/me`);
    expect(meReq.request.method).toBe('GET');
    meReq.flush(me({ properties: [OLD_MILL] }));

    // The token still names the lost home, so a new one is minted for the next usable home.
    const [refresh] = await takeRequests(http, REFRESH_URL);
    refresh.flush({ token: jwt({ propertyId: 'p-old-mill' }), expiresAt: '2099-01-01T00:00:00Z', user: me({ properties: [OLD_MILL] }) });

    await until(() => TestBed.inject(Router).url === '/app/dashboard');
    expect(TestBed.inject(Router).url).toBe('/app/dashboard');
    expect(notice()).toBe('You no longer have access to that home. Now showing 16 Old Mill Ln.');
    expect(status()).toBe(403);
    expect(tokens.claim(tokens.getAccessToken(), 'propertyId')).toBe('p-old-mill');
  });

  it('moves a Board Member with no home left to board mode, naming no home (8.11.i, US8-S6)', async () => {
    tokens.setAccessToken(jwt({ propertyId: 'p-29' }));
    deny();

    const [meReq] = await takeRequests(http, `${API}/auth/me`);
    meReq.flush(me({
      firstName: 'Jane', landing: 'Board', lastActiveMode: 'Board',
      memberships: [{ communityId: 'c-sh', communityName: 'Sakura Heights', role: 'BoardMember', communityStatus: 'Active' }],
    }));

    await until(() => TestBed.inject(Router).url === '/app/board/home');
    expect(TestBed.inject(Router).url).toBe('/app/board/home');
    expect(notice()).toBe('You no longer have access to that home.');
    http.expectNone(REFRESH_URL);
  });

  it('sends a person who never had a home to "No home linked", with no notice (US9-S4, brief 11 D8)', async () => {
    tokens.setAccessToken(jwt({}));
    deny();

    const [meReq] = await takeRequests(http, `${API}/auth/me`);
    meReq.flush(me({ landing: 'NoHome' }));

    await until(() => TestBed.inject(Router).url === '/app/no-home');
    expect(TestBed.inject(Router).url).toBe('/app/no-home');
    expect(notice()).toBeNull();
  });

  it('shares one recovery between concurrent denials, and never loops after it', async () => {
    tokens.setAccessToken(jwt({ propertyId: 'p-29' }));
    deny(`${API}/payments/statements`);
    deny(`${API}/dashboard`);

    const meCalls = await takeRequests(http, `${API}/auth/me`);
    expect(meCalls.length).toBe(1);
    meCalls[0].flush(me({ landing: 'NoHome' }));
    await until(() => TestBed.inject(Router).url === '/app/no-home');

    // The page it lands on is denied again: passed through, not recovered again.
    deny(`${API}/property`);
    await settle();
    http.expectNone(`${API}/auth/me`);
  });

  it('stops quietly when /auth/me itself fails, without recursing', async () => {
    tokens.setAccessToken(jwt({ propertyId: 'p-29' }));
    deny();
    const [meReq] = await takeRequests(http, `${API}/auth/me`);
    meReq.flush({ code: 'PROPERTY_ACCESS_DENIED' }, { status: 403, statusText: 'Forbidden' });
    await settle();
    http.expectNone(`${API}/auth/me`);
    expect(notice()).toBeNull();
    expect(TestBed.inject(AuthService).user()).toBeNull();
  });

  it('leaves other 403s alone', async () => {
    tokens.setAccessToken(jwt({ propertyId: 'p-29' }));
    let status = 0;
    client.get(`${API}/communities/c1/settings`).subscribe({ error: e => (status = e.status) });
    http.expectOne(`${API}/communities/c1/settings`).flush({ code: 'FORBIDDEN', message: 'x' }, { status: 403, statusText: 'Forbidden' });
    await settle();
    http.expectNone(`${API}/auth/me`);
    expect(status).toBe(403);
  });
});

describe('TokenService.claim', () => {
  it('reads a claim, and returns null for a missing claim or an unreadable token', () => {
    TestBed.configureTestingModule({});
    const svc = TestBed.inject(TokenService);
    const token = jwt({ propertyId: 'p-1', count: 3 });
    expect(svc.claim(token, 'propertyId')).toBe('p-1');
    expect(svc.claim(token, 'communityId')).toBeNull();
    expect(svc.claim(token, 'count')).toBeNull();
    expect(svc.claim('not-a-jwt', 'propertyId')).toBeNull();
    expect(svc.claim(null, 'propertyId')).toBeNull();
    expect(svc.isTokenExpired(jwt({ exp: 1 }))).toBeTrue();
  });
});
