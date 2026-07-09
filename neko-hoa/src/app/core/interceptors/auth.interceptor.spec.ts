import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { authInterceptor } from './auth.interceptor';
import { TokenService } from '../services/token.service';

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
