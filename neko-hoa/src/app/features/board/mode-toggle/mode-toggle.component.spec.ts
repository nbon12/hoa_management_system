import { signal } from '@angular/core';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ModeToggleComponent } from './mode-toggle.component';
import { AuthService } from '../../../core/services/auth.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';
import { CurrentUser, CommunityMembershipSummary, UserMode } from '../../../core/models';

const userSig = signal<CurrentUser | null>(null);

function setUser(memberships: CommunityMembershipSummary[], mode: UserMode = 'Resident') {
  userSig.set({
    id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB',
    lastActiveMode: mode, memberships,
  });
}

describe('ModeToggleComponent', () => {
  let fixture: ComponentFixture<ModeToggleComponent>;
  let el: HTMLElement;
  let switchMode: jasmine.Spy;
  let navigate: jasmine.Spy;
  let setActiveCommunity: jasmine.Spy;

  beforeEach(async () => {
    userSig.set(null);
    switchMode = jasmine.createSpy('switchMode').and.callFake((mode: UserMode) => {
      setUser(userSig()?.memberships ?? [], mode);
      return Promise.resolve();
    });
    navigate = jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true));
    setActiveCommunity = jasmine.createSpy('setActiveCommunity');

    await TestBed.configureTestingModule({
      imports: [ModeToggleComponent],
      providers: [
        { provide: AuthService, useValue: { user: userSig.asReadonly(), switchMode } },
        { provide: BoardNavigationService, useValue: { setActiveCommunity } },
        { provide: Router, useValue: { navigate } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ModeToggleComponent);
    el = fixture.nativeElement;
  });

  it('is absent for a resident-only user (FR-020)', () => {
    setUser([{ communityId: 'c1', communityName: 'One', role: 'Resident' }]);
    fixture.detectChanges();
    expect(el.querySelector('button')).toBeNull();
    expect(el.textContent).not.toContain('Enter board mode');
  });

  it('is absent when the user has no memberships at all', () => {
    setUser([]);
    fixture.detectChanges();
    expect(el.querySelector('button')).toBeNull();
  });

  it('renders "Enter board mode" for a user with a non-resident membership (FR-020)', () => {
    setUser([{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]);
    fixture.detectChanges();
    expect(el.textContent).toContain('Enter board mode');
  });

  it('shows the Resident/Board segmented control while in board mode', () => {
    setUser([{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }], 'Board');
    fixture.detectChanges();
    const text = el.textContent ?? '';
    expect(text).toContain('Resident');
    expect(text).toContain('Board');
  });

  it('entering board mode as a single-community user switches and lands on Community Home', async () => {
    setUser([{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]);
    fixture.detectChanges();
    const btn = el.querySelector('button') as HTMLButtonElement;
    btn.click();
    await fixture.whenStable();
    expect(switchMode).toHaveBeenCalledWith('Board');
    expect(setActiveCommunity).toHaveBeenCalledWith('c1');
    expect(navigate).toHaveBeenCalledWith(['/app/board/home']);
  });

  it('entering board mode as a multi-community user lands on the My Communities list', async () => {
    setUser([
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
      { communityId: 'c2', communityName: 'Two', role: 'CommunityManager' },
    ]);
    fixture.detectChanges();
    (el.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(navigate).toHaveBeenCalledWith(['/app/board/communities']);
  });

  it('surfaces a graceful message when the server refuses (NO_ACTIVE_MEMBERSHIP)', async () => {
    setUser([{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]);
    switchMode.and.returnValue(Promise.reject({ error: { code: 'NO_ACTIVE_MEMBERSHIP' } }));
    fixture.detectChanges();
    (el.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el.textContent).toContain('not available');
    expect(navigate).not.toHaveBeenCalled();
  });
});
