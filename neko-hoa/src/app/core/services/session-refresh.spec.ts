import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { SessionRefreshService, sessionRefreshInitializer } from './session-refresh';
import { TokenService } from './token.service';
import { AuthService } from './auth.service';

const REFRESH_URL = `${environment.apiBaseUrl}/auth/refresh`;

// navigator.locks grants asynchronously (and not in a fixed number of ticks) — poll until the
// lock callback has issued its request. http.match() consumes matched requests, so collect them.
const settle = () => new Promise<void>(r => setTimeout(r));
async function takeRequests(http: HttpTestingController, url: string, count = 1) {
  const taken: ReturnType<HttpTestingController['match']> = [];
  for (let i = 0; i < 200 && taken.length < count; i++) {
    taken.push(...http.match(url));
    if (taken.length < count) await settle();
  }
  return taken;
}

const MOCK_RESPONSE = {
  token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'u1', firstName: 'Rin', lastName: 'Neko', email: 'resident@nekohoa.dev',
    initials: 'RN', properties: [],
  },
};

// 020-D FR-D1/FR-D5 (T005): hint-gated startup re-hydration + single-flight refresh.
describe('SessionRefreshService', () => {
  let http: HttpTestingController;
  let tokens: TokenService;
  let session: SessionRefreshService;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenService);
    session = TestBed.inject(SessionRefreshService);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => http.verify());

  describe('startup initializer (hint-gated, research D-R2)', () => {
    it('makes no refresh call when the has-session hint is absent', async () => {
      const init = sessionRefreshInitializer(tokens, session);

      await init();

      http.expectNone(REFRESH_URL);
    });

    it('refreshes with credentials and applies the session when the hint is present', async () => {
      tokens.setSessionHint(true);
      const init = sessionRefreshInitializer(tokens, session);

      const done = init();
      const [req] = await takeRequests(http, REFRESH_URL);
      expect(req).toBeDefined();
      expect(req.request.withCredentials).toBeTrue();
      req.flush(MOCK_RESPONSE);
      await done;

      expect(tokens.getAccessToken()).toBe(MOCK_RESPONSE.token);
      expect(auth.isLoggedIn()).toBeTrue();
    });
  });

  describe('refresh()', () => {
    it('is single-flight: concurrent callers share one HTTP request (FR-D5)', async () => {
      const p1 = session.refresh();
      const p2 = session.refresh();

      const reqs = await takeRequests(http, REFRESH_URL);
      expect(reqs.length).toBe(1);
      reqs[0].flush(MOCK_RESPONSE);

      expect(await p1).toBe(MOCK_RESPONSE.token);
      expect(await p2).toBe(MOCK_RESPONSE.token);
      // No second request may trail in after resolution.
      await settle();
      expect(http.match(REFRESH_URL).length).toBe(0);
    });

    it('on 401 ends the session: token dropped, hint cleared, null returned', async () => {
      tokens.setAccessToken('stale');
      const promise = session.refresh();

      const [req] = await takeRequests(http, REFRESH_URL);
      req.flush({ code: 'INVALID_REFRESH_TOKEN' }, { status: 401, statusText: 'Unauthorized' });

      expect(await promise).toBeNull();
      expect(tokens.getAccessToken()).toBeNull();
      expect(tokens.hasSessionHint()).toBeFalse();
    });

    it('on transient network error returns null without ending the session', async () => {
      tokens.setSessionHint(true);
      const promise = session.refresh();

      const [req] = await takeRequests(http, REFRESH_URL);
      req.error(new ProgressEvent('error'));

      expect(await promise).toBeNull();
      expect(tokens.hasSessionHint()).toBeTrue();
    });
  });
});
