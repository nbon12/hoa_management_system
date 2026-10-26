import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { companyAdminGuard } from './company-admin.guard';
import { AuthService } from '../services/auth.service';
import { BoardNavigationService } from '../services/board-navigation.service';
import { ToastService } from '../services/toast.service';
import { CommunityMembershipSummary, CurrentUser, Landing } from '../models';

// 030 T062 / FR-035, FR-036, US6-S6: the company area opens only for a Company Administrator.
// Anyone else is refused and sent to their own landing, with the page-refusal toast.

const userSig = signal<CurrentUser | null>(null);
const activeSig = signal<string | null>(null);
let setActiveCommunity: jasmine.Spy;

function signIn(landing: Landing, isCompanyAdministrator: boolean, memberships: CommunityMembershipSummary[] = []): void {
  userSig.set({
    id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB', lastActiveMode: 'Board',
    memberships, properties: [], isCompanyAdministrator, landing,
  });
}

function run(): boolean | UrlTree {
  return TestBed.runInInjectionContext(() =>
    companyAdminGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)) as boolean | UrlTree;
}

function redirectPath(result: boolean | UrlTree): string {
  expect(result instanceof UrlTree).withContext('expected a redirect').toBeTrue();
  return TestBed.inject(Router).serializeUrl(result as UrlTree);
}

function toasts(): string[] {
  return TestBed.inject(ToastService).toasts().map(t => t.message);
}

describe('companyAdminGuard (030 T062)', () => {
  beforeEach(() => {
    userSig.set(null);
    activeSig.set(null);
    setActiveCommunity = jasmine.createSpy('setActiveCommunity');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { user: userSig.asReadonly() } },
        { provide: BoardNavigationService, useValue: { activeCommunityId: activeSig.asReadonly(), setActiveCommunity } },
      ],
    });
  });

  afterEach(() => TestBed.inject(ToastService).clear());

  it('allows a Company Administrator with no membership and no home (US9-S1)', () => {
    signIn('Company', true);
    expect(run()).toBeTrue();
    expect(toasts()).toEqual([]);
  });

  it('allows a Company Administrator who also sits on a board (Casey, FR-036)', () => {
    signIn('Board', true, [{ communityId: 'kcm', communityName: 'Keystone Crossing Master', role: 'BoardMember', communityStatus: 'Active' }]);
    expect(run()).toBeTrue();
  });

  it('sends a Community Manager to My Communities with the refusal toast (8.01.q, US6-S6)', () => {
    signIn('Board', false, [
      { communityId: 'sh', communityName: 'Sakura Heights', role: 'CommunityManager', communityStatus: 'Active' },
      { communityId: 'kr', communityName: 'Keystone Ridge', role: 'CommunityManager', communityStatus: 'Onboarding' },
    ]);
    expect(redirectPath(run())).toBe('/app/board/communities');
    expect(toasts()).toEqual(["You don't have access to that page."]);
    expect(setActiveCommunity).toHaveBeenCalledWith(null);
  });

  it('sends a single-community board member to that community home, made active', () => {
    signIn('Board', false, [{ communityId: 'sh', communityName: 'Sakura Heights', role: 'BoardMember', communityStatus: 'Active' }]);
    expect(redirectPath(run())).toBe('/app/board/home');
    expect(setActiveCommunity).toHaveBeenCalledWith('sh');
  });

  it('sends a resident to the dashboard, and a user with no home to /app/no-home', () => {
    signIn('Resident', false);
    expect(redirectPath(run())).toBe('/app/dashboard');
    signIn('NoHome', false);
    expect(redirectPath(run())).toBe('/app/no-home');
  });

  it('never loops on a stale Company landing without the grant', () => {
    signIn('Company', false);
    expect(redirectPath(run())).toBe('/app/dashboard');
  });

  it('sends a signed-out visitor to sign in, with no toast', () => {
    expect(redirectPath(run())).toBe('/login');
    expect(toasts()).toEqual([]);
  });
});
