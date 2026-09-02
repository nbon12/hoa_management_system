import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { boardGuard } from './board.guard';
import { AuthService } from '../services/auth.service';
import { BoardNavigationService } from '../services/board-navigation.service';
import { CommunityMembershipSummary, CurrentUser } from '../models';

/**
 * 025 US3, acceptance scenario 5 (FR-028): "Given I navigate directly to a route my role cannot
 * use, When the route resolves, Then I am refused and redirected to a permitted page rather than
 * shown an empty screen."
 *
 * `e2e/board-role-gate.spec.ts` covers the same scenario end-to-end, but that suite only runs in
 * the per-PR environment workflow. The guard is pure and synchronous, so these Karma specs give
 * the refusal path coverage in the default PR checks too.
 *
 * Every redirect assertion checks the SERIALIZED destination of the returned `UrlTree`, so the
 * test proves where the user actually lands — not merely that some redirect happened.
 */

const userSig = signal<CurrentUser | null>(null);
const activeCommunitySig = signal<string | null>(null);

function signIn(memberships: CommunityMembershipSummary[]): void {
  userSig.set({
    id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB',
    lastActiveMode: 'Board', memberships,
  });
}

/** Runs the CanActivateFn against a route snapshot carrying `data.requiredRoles`. */
function runGuard(requiredRoles?: string[]): boolean | UrlTree {
  const route = { data: requiredRoles ? { requiredRoles } : {} } as unknown as ActivatedRouteSnapshot;
  return TestBed.runInInjectionContext(
    () => boardGuard(route, {} as RouterStateSnapshot),
  ) as boolean | UrlTree;
}

/** The path a refusal redirected to, e.g. `/app/dashboard`. Fails loudly if not a redirect. */
function redirectPath(result: boolean | UrlTree): string {
  expect(result instanceof UrlTree)
    .withContext('expected the guard to refuse with a redirect UrlTree')
    .toBeTrue();
  return TestBed.inject(Router).serializeUrl(result as UrlTree);
}

describe('boardGuard (US3 scenario 5 / FR-028)', () => {
  beforeEach(() => {
    userSig.set(null);
    activeCommunitySig.set(null);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { user: userSig.asReadonly() } },
        {
          provide: BoardNavigationService,
          useValue: {
            activeCommunityId: activeCommunitySig.asReadonly(),
            // The guard delegates its role derivation to the service; wire the real
            // implementation (a pure function of its arguments) so the stub cannot drift.
            rolesForCommunity: BoardNavigationService.prototype.rolesForCommunity,
          },
        },
      ],
    });
  });

  it('redirects a resident-only user off a board route to /app/dashboard', () => {
    signIn([{ communityId: 'c1', communityName: 'One', role: 'Resident' }]);

    expect(redirectPath(runGuard())).toBe('/app/dashboard');
  });

  it('redirects a resident-only user off a manager-only route to /app/dashboard', () => {
    signIn([{ communityId: 'c1', communityName: 'One', role: 'Resident' }]);

    expect(redirectPath(runGuard(['CommunityManager']))).toBe('/app/dashboard');
  });

  it('redirects a user with no memberships at all to /app/dashboard', () => {
    signIn([]);

    expect(redirectPath(runGuard())).toBe('/app/dashboard');
  });

  it('redirects when there is no signed-in user', () => {
    userSig.set(null);

    expect(redirectPath(runGuard())).toBe('/app/dashboard');
  });

  it('redirects a BoardMember off a manager-only route to /app/board/home, not a blank screen', () => {
    signIn([{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]);

    // Refused, and sent to a board page the role CAN use (FR-028) — not the resident dashboard.
    expect(redirectPath(runGuard(['CommunityManager']))).toBe('/app/board/home');
  });

  it('allows a CommunityManager on that same manager-only route', () => {
    signIn([{ communityId: 'c1', communityName: 'One', role: 'CommunityManager' }]);

    expect(runGuard(['CommunityManager'])).toBeTrue();
  });

  it('allows a BoardMember on a board route with no requiredRoles', () => {
    signIn([{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]);

    expect(runGuard()).toBeTrue();
  });

  it('allows when any one of several requiredRoles is held', () => {
    signIn([{ communityId: 'c1', communityName: 'One', role: 'Accountant' }]);

    expect(runGuard(['CommunityManager', 'Accountant'])).toBeTrue();
  });

  it('scopes the role check to the active community: manager elsewhere is still refused', () => {
    signIn([
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
      { communityId: 'c2', communityName: 'Two', role: 'CommunityManager' },
    ]);
    activeCommunitySig.set('c1'); // acting in the community where the user is board-only

    expect(redirectPath(runGuard(['CommunityManager']))).toBe('/app/board/home');

    activeCommunitySig.set('c2'); // acting where the user IS the manager
    expect(runGuard(['CommunityManager'])).toBeTrue();
  });
});
