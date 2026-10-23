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
import { ToastService } from '../services/toast.service';
import { CommunityMembershipSummary, CurrentUser, Landing } from '../models';

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
          useValue: { activeCommunityId: activeCommunitySig.asReadonly() },
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

// 030 T044 / FR-036b: Onboarding memberships count for board mode; Archived ones don't. A user who
// isn't board-eligible goes to their FR-036a landing with the page refusal (brief 01 §6, 8.01.h).
describe('boardGuard (030 FR-036a, FR-036b)', () => {
  function signInAs(landing: Landing, memberships: CommunityMembershipSummary[], over: Partial<CurrentUser> = {}): void {
    userSig.set({
      id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB',
      lastActiveMode: 'Resident', memberships, properties: [], isCompanyAdministrator: false, landing, ...over,
    });
  }

  beforeEach(() => {
    userSig.set(null);
    activeCommunitySig.set(null);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { user: userSig.asReadonly() } },
        { provide: BoardNavigationService, useValue: { activeCommunityId: activeCommunitySig.asReadonly() } },
      ],
    });
  });

  afterEach(() => TestBed.inject(ToastService).clear());

  it('allows a Community Manager of an Onboarding community, with no home (US9-S2)', () => {
    signInAs('Board', [{ communityId: 'kp', communityName: 'Keystone Park', role: 'CommunityManager', communityStatus: 'Onboarding' }]);

    expect(runGuard()).toBeTrue();
    expect(runGuard(['CommunityManager'])).toBeTrue();
  });

  it('sends a Company Administrator with no membership to the portfolio, with the page refusal (8.01.h)', () => {
    signInAs('Company', [], { isCompanyAdministrator: true });

    expect(redirectPath(runGuard())).toBe('/app/company/portfolio');
    expect(TestBed.inject(ToastService).toasts().map(t => t.message)).toEqual(["You don't have access to that page."]);
  });

  it('sends a user with no home and no membership to /app/no-home (US9-S3)', () => {
    signInAs('NoHome', []);

    expect(redirectPath(runGuard())).toBe('/app/no-home');
  });

  it('sends a user whose homes are all Onboarding to /app/not-live (US9-S5)', () => {
    signInAs('NotLive', [{ communityId: 'br', communityName: 'Briar Hollow', role: 'Resident', communityStatus: 'Onboarding' }]);

    expect(redirectPath(runGuard())).toBe('/app/not-live');
  });

  it('does not count a membership in an Archived community (FR-036b)', () => {
    signInAs('Resident', [{ communityId: 'old', communityName: 'Old Mill Commons', role: 'Accountant', communityStatus: 'Archived' }]);

    expect(redirectPath(runGuard())).toBe('/app/dashboard');
  });

  it('never bounces a stale Board landing back into the board guard', () => {
    signInAs('Board', []);

    expect(redirectPath(runGuard())).toBe('/app/dashboard');
  });

  it('checks any community for a roleScope "any" route (Add community, FR-036)', () => {
    signInAs('Board', [
      { communityId: 'sh', communityName: 'Sakura Heights', role: 'BoardMember', communityStatus: 'Active' },
      { communityId: 'kr', communityName: 'Keystone Ridge', role: 'CommunityManager', communityStatus: 'Onboarding' },
    ]);
    activeCommunitySig.set('sh');
    const route = { data: { requiredRoles: ['CommunityManager'], roleScope: 'any' } } as unknown as ActivatedRouteSnapshot;
    expect(TestBed.runInInjectionContext(() => boardGuard(route, {} as RouterStateSnapshot))).toBeTrue();
    expect(redirectPath(runGuard(['CommunityManager']))).toBe('/app/board/home');
  });

  it('shows the page refusal when a Board Member opens a manager-only page (8.04.z)', () => {
    signInAs('Board', [{ communityId: 'sh', communityName: 'Sakura Heights', role: 'BoardMember', communityStatus: 'Active' }]);

    expect(redirectPath(runGuard(['CommunityManager']))).toBe('/app/board/home');
    expect(TestBed.inject(ToastService).toasts().map(t => t.message)).toEqual(["You don't have access to that page."]);
  });
});
