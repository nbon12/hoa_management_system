import { TestBed } from '@angular/core/testing';
import { TokenService } from './token.service';

// 020-D FR-D1 (T004): the access token is memory-only; the sole localStorage artifact is the
// non-credential has-session hint; legacy persisted credentials are wiped on init.
describe('TokenService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  it('wipes legacy persisted credentials on init', () => {
    localStorage.setItem('neko_token', 'legacy-access');
    localStorage.setItem('neko_refresh', 'legacy-refresh');
    localStorage.setItem('neko_user', '{"id":"u1"}');

    TestBed.inject(TokenService);

    expect(localStorage.getItem('neko_token')).toBeNull();
    expect(localStorage.getItem('neko_refresh')).toBeNull();
    expect(localStorage.getItem('neko_user')).toBeNull();
  });

  it('holds the access token in memory only and sets the session hint', () => {
    const svc = TestBed.inject(TokenService);

    svc.setAccessToken('access-abc');

    expect(svc.getAccessToken()).toBe('access-abc');
    expect(localStorage.getItem('neko_has_session')).toBe('1');
    // No credential material lands in storage.
    expect(Object.keys(localStorage).filter(k => k !== 'neko_has_session')).toEqual([]);
  });

  it('clearAccessToken drops the token without touching the hint', () => {
    const svc = TestBed.inject(TokenService);
    svc.setAccessToken('access-abc');

    svc.clearAccessToken();

    expect(svc.getAccessToken()).toBeNull();
    expect(svc.hasSessionHint()).toBeTrue();
  });

  it('setSessionHint(false) removes the hint', () => {
    const svc = TestBed.inject(TokenService);
    svc.setAccessToken('access-abc');

    svc.setSessionHint(false);

    expect(svc.hasSessionHint()).toBeFalse();
    expect(localStorage.getItem('neko_has_session')).toBeNull();
  });
});
