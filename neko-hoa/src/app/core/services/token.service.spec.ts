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

  // 020-D FR-D6 (T019): JWT payloads are base64url — '-'/'_' instead of '+'/'/', no padding.
  // A decoder that feeds them straight to atob() misreads valid tokens as expired.
  describe('isTokenExpired with base64url payloads', () => {
    const b64url = (o: object) =>
      btoa(JSON.stringify(o)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

    it('a future-dated token whose payload needs URL-safe chars is NOT expired', () => {
      const svc = TestBed.inject(TokenService);
      // '???>>>' forces '/' and '+' into standard base64, i.e. '_' and '-' in base64url.
      const token = `h.${b64url({ exp: 9999999999, sub: '???>>>' })}.s`;
      expect(svc.isTokenExpired(token)).toBeFalse();
    });

    it('a past-dated base64url token IS expired', () => {
      const svc = TestBed.inject(TokenService);
      const token = `h.${b64url({ exp: 1000, sub: '???>>>' })}.s`;
      expect(svc.isTokenExpired(token)).toBeTrue();
    });

    it('garbage still reads as expired', () => {
      const svc = TestBed.inject(TokenService);
      expect(svc.isTokenExpired('not-a-jwt')).toBeTrue();
    });
  });

  it('setSessionHint(false) removes the hint', () => {
    const svc = TestBed.inject(TokenService);
    svc.setAccessToken('access-abc');

    svc.setSessionHint(false);

    expect(svc.hasSessionHint()).toBeFalse();
    expect(localStorage.getItem('neko_has_session')).toBeNull();
  });
});
