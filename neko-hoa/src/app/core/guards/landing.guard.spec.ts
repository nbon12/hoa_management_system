import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideLocationMocks } from '@angular/common/testing';
import { Router, provideRouter } from '@angular/router';
import { routes } from '../../app.routes';
import { AuthService } from '../services/auth.service';
import { BoardNavigationService } from '../services/board-navigation.service';
import { CommunityMembershipSummary, CurrentUser, UserMode } from '../models';

// 025 FR-026: `/app` (the default landing) must resolve per the user's last-used mode, while
// explicit deep links keep working untouched. Exercised against the REAL route table.

const userSig = signal<CurrentUser | null>(null);

function signIn(lastActiveMode: UserMode, memberships: CommunityMembershipSummary[]): void {
  userSig.set({
    id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB',
    lastActiveMode, memberships,
  });
}

/**
 * Navigate and wait for the router to settle. A guard that returns a `UrlTree` cancels the
 * first navigation and starts a second one, so `navigateByUrl` can resolve before the final
 * URL is in place. Gives up after ~1s and returns whatever the router actually landed on, so
 * a wrong landing fails the assertion rather than hanging.
 */
async function landAt(url: string, expected: string): Promise<string> {
  const router = TestBed.inject(Router);
  await router.navigateByUrl(url);
  for (let i = 0; i < 100 && router.url !== expected; i++) {
    await new Promise(resolve => setTimeout(resolve, 10));
  }
  return router.url;
}

describe('landingGuard on /app (FR-026)', () => {
  beforeEach(() => {
    userSig.set(null);
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideLocationMocks(),
        {
          provide: AuthService,
          useValue: { user: userSig.asReadonly(), isLoggedIn: () => userSig() !== null },
        },
      ],
    });
  });

  it('sends a resident to the dashboard', async () => {
    signIn('Resident', [{ communityId: 'c1', communityName: 'One', role: 'Resident' }]);
    expect(await landAt('/app', '/app/dashboard')).toBe('/app/dashboard');
  });

  it('sends a single-community board user to that community home', async () => {
    signIn('Board', [{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]);
    expect(await landAt('/app', '/app/board/home')).toBe('/app/board/home');
    // The sole community becomes the active one, as it does on a mode switch.
    expect(TestBed.inject(BoardNavigationService).activeCommunityId()).toBe('c1');
  });

  it('sends a multi-community board user to the My Communities list', async () => {
    signIn('Board', [
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
      { communityId: 'c2', communityName: 'Two', role: 'CommunityManager' },
    ]);
    expect(await landAt('/app', '/app/board/communities')).toBe('/app/board/communities');
    expect(TestBed.inject(BoardNavigationService).activeCommunityId()).toBeNull();
  });

  it('sends a board-mode user with no memberships to the dashboard', async () => {
    signIn('Board', []);
    expect(await landAt('/app', '/app/dashboard')).toBe('/app/dashboard');
  });

  it('does NOT hijack an explicit deep link', async () => {
    // A board-mode user typing a resident URL must get that page, not the board landing.
    signIn('Board', [{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]);
    expect(await landAt('/app/payments/statement', '/app/payments/statement'))
      .toBe('/app/payments/statement');
  });

  it('does NOT hijack an explicit /app/dashboard deep link for a board-mode user', async () => {
    signIn('Board', [{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]);
    expect(await landAt('/app/dashboard', '/app/dashboard')).toBe('/app/dashboard');
  });
});
