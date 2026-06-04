import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { provideRouter } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

function runGuard(isLoggedIn: boolean) {
  const fakeAuth = { isLoggedIn: () => isLoggedIn } as Partial<AuthService>;
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: AuthService, useValue: fakeAuth },
    ],
  });
  return TestBed.runInInjectionContext(() =>
    authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)
  );
}

describe('authGuard', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('returns true when user is logged in', () => {
    const result = runGuard(true);
    expect(result).toBeTrue();
  });

  it('returns UrlTree (redirect) when user is not logged in', () => {
    const result = runGuard(false);
    // UrlTree has a toString() method or is a UrlTree instance
    expect(result).toBeTruthy();
    expect(typeof result).not.toBe('boolean');
  });
});
