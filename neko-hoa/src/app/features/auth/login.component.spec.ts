import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { LoginComponent } from './login.component';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { CommunityMembershipSummary, CurrentUser, UserMode } from '../../core/models';

// 025 FR-026/FR-022: signing in resumes the mode the user was last in. Before this, sign-in
// always went to /app/dashboard, so a board member who signed out in board mode came back to
// their own balance under the board banner.

describe('LoginComponent sign-in landing', () => {
  const userSig = signal<CurrentUser | null>(null);
  let navigate: jasmine.Spy;
  let component: LoginComponent;

  /** The session the fake /auth/login returns. */
  function session(lastActiveMode: UserMode, memberships: CommunityMembershipSummary[]): CurrentUser {
    return {
      id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB',
      lastActiveMode, memberships,
    };
  }

  function setUp(loggedIn: CurrentUser): void {
    userSig.set(null);
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    const auth = {
      user: userSig.asReadonly(),
      // Mirrors AuthService.login: the session (and its lastActiveMode) is adopted first.
      login: jasmine.createSpy('login').and.callFake(async () => { userSig.set(loggedIn); }),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: { navigate } },
      ],
    });
    component = TestBed.runInInjectionContext(() => new LoginComponent());
    component.email = 'a@b.dev';
    component.password = 'pw';
  }

  afterEach(() => TestBed.resetTestingModule());

  it('lands a resident on the dashboard (unchanged behaviour)', async () => {
    setUp(session('Resident', [{ communityId: 'c1', communityName: 'One', role: 'Resident' }]));
    await component.submit();
    expect(navigate).toHaveBeenCalledWith(['/app/dashboard']);
  });

  it('lands a single-community board member on that community home (FR-026)', async () => {
    setUp(session('Board', [{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]));
    await component.submit();
    expect(navigate).toHaveBeenCalledWith(['/app/board/home']);
    // The sole community is made active, exactly as entering board mode does.
    expect(TestBed.inject(BoardNavigationService).activeCommunityId()).toBe('c1');
  });

  it('lands a multi-community board member on the My Communities list', async () => {
    setUp(session('Board', [
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
      { communityId: 'c2', communityName: 'Two', role: 'CommunityManager' },
    ]));
    await component.submit();
    expect(navigate).toHaveBeenCalledWith(['/app/board/communities']);
    expect(TestBed.inject(BoardNavigationService).activeCommunityId()).toBeNull();
  });

  it('lands a board-mode user holding no memberships on the dashboard', async () => {
    setUp(session('Board', []));
    await component.submit();
    expect(navigate).toHaveBeenCalledWith(['/app/dashboard']);
  });

  it('does not navigate when the credentials are rejected', async () => {
    setUp(session('Board', [{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]));
    (TestBed.inject(AuthService).login as unknown as jasmine.Spy).and.rejectWith(new Error('401'));
    await component.submit();
    expect(navigate).not.toHaveBeenCalled();
    expect(component.error()).toBe('Invalid email or password.');
  });
});
