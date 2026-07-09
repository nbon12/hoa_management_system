import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { LoginComponent } from '../../features/auth/login.component';
import { environment } from '../../../environments/environment';

const BASE = environment.apiBaseUrl;

// 020-D FR-D1: no refreshToken in the body — it arrives as an HttpOnly cookie.
const MOCK_AUTH_RESPONSE = {
  token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'u1', firstName: 'Jane', lastName: 'Resident',
    email: 'resident@nekohoa.dev', initials: 'JR',
    properties: [{ id: 'p1', accountNumber: 'SAKURA-001', address: '1 Sakura Drive' }],
  },
};

describe('AuthService', () => {
  let svc: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports:   [HttpClientTestingModule],
      providers: [
        provideRouter([{ path: 'login', component: LoginComponent }]),
      ],
    });
    svc  = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('should be created', () => expect(svc).toBeTruthy());

  it('is not logged in initially', () => {
    expect(svc.isLoggedIn()).toBeFalse();
  });

  it('user signal is null initially', () => {
    expect(svc.user()).toBeNull();
  });

  describe('login()', () => {
    it('sets user on successful login', async () => {
      const promise = svc.login('resident@nekohoa.dev', 'Password1!');
      http.expectOne(`${BASE}/auth/login`).flush(MOCK_AUTH_RESPONSE);
      await promise;
      expect(svc.isLoggedIn()).toBeTrue();
      expect(svc.user()?.firstName).toBe('Jane');
    });

    it('sets user initials after login', async () => {
      const promise = svc.login('resident@nekohoa.dev', 'Password1!');
      http.expectOne(`${BASE}/auth/login`).flush(MOCK_AUTH_RESPONSE);
      await promise;
      expect(svc.user()?.initials).toBe('JR');
    });

    it('rejects on HTTP 401', async () => {
      const promise = svc.login('bad@example.com', 'wrong');
      http.expectOne(`${BASE}/auth/login`)
          .flush({ code: 'INVALID_CREDENTIALS' }, { status: 401, statusText: 'Unauthorized' });
      await expectAsync(promise).toBeRejected();
      expect(svc.isLoggedIn()).toBeFalse();
    });

    it('does not persist any credential or user material in localStorage (FR-D1)', async () => {
      const promise = svc.login('resident@nekohoa.dev', 'Password1!');
      http.expectOne(`${BASE}/auth/login`).flush(MOCK_AUTH_RESPONSE);
      await promise;
      expect(localStorage.getItem('neko_user')).toBeNull();
      expect(localStorage.getItem('neko_token')).toBeNull();
      expect(localStorage.getItem('neko_refresh')).toBeNull();
      // Only the non-credential session hint remains.
      expect(localStorage.getItem('neko_has_session')).toBe('1');
    });

    it('sends login with credentials so the HttpOnly cookie is accepted', async () => {
      const promise = svc.login('resident@nekohoa.dev', 'Password1!');
      const req = http.expectOne(`${BASE}/auth/login`);
      expect(req.request.withCredentials).toBeTrue();
      req.flush(MOCK_AUTH_RESPONSE);
      await promise;
    });
  });

  describe('register()', () => {
    // 020-D FR-D9: register consumes a verification proof + claim code, never an account number.
    it('posts proof + claim code and sets the user on success', async () => {
      const promise = svc.register('proof-token', 'pass', 'Jane', 'Doe', 'HOA-CLAIM-1234');
      const req = http.expectOne(`${BASE}/auth/register`);
      expect(req.request.body).toEqual({
        verificationToken: 'proof-token', password: 'pass',
        firstName: 'Jane', lastName: 'Doe', claimCode: 'HOA-CLAIM-1234',
      });
      expect(req.request.withCredentials).toBeTrue();
      req.flush(MOCK_AUTH_RESPONSE);
      await promise;
      expect(svc.isLoggedIn()).toBeTrue();
    });

    it('requestEmailVerification posts the email', async () => {
      const promise = svc.requestEmailVerification('new@example.com');
      const req = http.expectOne(`${BASE}/auth/verify-email/request`);
      expect(req.request.body).toEqual({ email: 'new@example.com' });
      req.flush({ status: 'sent' });
      await promise;
    });

    it('confirmEmailVerification returns the proof, or null on generic failure', async () => {
      const ok = svc.confirmEmailVerification('new@example.com', '123456');
      http.expectOne(`${BASE}/auth/verify-email/confirm`).flush({ verificationToken: 'proof-1' });
      expect(await ok).toBe('proof-1');

      const bad = svc.confirmEmailVerification('new@example.com', '000000');
      http.expectOne(`${BASE}/auth/verify-email/confirm`).flush(
        { code: 'VERIFICATION_FAILED' }, { status: 400, statusText: 'Bad Request' });
      expect(await bad).toBeNull();
    });
  });

  describe('logout()', () => {
    it('clears user, access token, and the session hint', async () => {
      const promise = svc.login('resident@nekohoa.dev', 'Password1!');
      http.expectOne(`${BASE}/auth/login`).flush(MOCK_AUTH_RESPONSE);
      await promise;
      svc.logout();
      http.expectOne(`${BASE}/auth/logout`).flush({});
      expect(svc.isLoggedIn()).toBeFalse();
      expect(localStorage.getItem('neko_has_session')).toBeNull();
    });
  });
});
