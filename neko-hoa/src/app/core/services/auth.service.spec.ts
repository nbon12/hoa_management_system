import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { LoginComponent } from '../../features/auth/login.component';
import { environment } from '../../../environments/environment';

const BASE = environment.apiBaseUrl;

const MOCK_AUTH_RESPONSE = {
  token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTl9.fake',
  refreshToken: 'refresh-token-abc',
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

    it('persists user in localStorage', async () => {
      const promise = svc.login('resident@nekohoa.dev', 'Password1!');
      http.expectOne(`${BASE}/auth/login`).flush(MOCK_AUTH_RESPONSE);
      await promise;
      expect(localStorage.getItem('neko_user')).not.toBeNull();
    });
  });

  describe('register()', () => {
    it('sets user after successful registration', async () => {
      const promise = svc.register('new@example.com', 'pass', 'Jane', 'Doe', 'SAKURA-001');
      http.expectOne(`${BASE}/auth/register`).flush(MOCK_AUTH_RESPONSE);
      await promise;
      expect(svc.isLoggedIn()).toBeTrue();
    });
  });

  describe('logout()', () => {
    it('clears user and localStorage', async () => {
      const promise = svc.login('resident@nekohoa.dev', 'Password1!');
      http.expectOne(`${BASE}/auth/login`).flush(MOCK_AUTH_RESPONSE);
      await promise;
      svc.logout();
      http.expectOne(`${BASE}/auth/logout`).flush({});
      expect(svc.isLoggedIn()).toBeFalse();
      expect(localStorage.getItem('neko_user')).toBeNull();
    });
  });
});
