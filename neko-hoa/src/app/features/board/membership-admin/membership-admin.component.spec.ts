import { signal } from '@angular/core';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { MembershipAdminComponent } from './membership-admin.component';
import { AuthService } from '../../../core/services/auth.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';
import { BoardService, Membership, Paged } from '../../../core/services/board.service';
import { CurrentUser } from '../../../core/models';

const MEMBER: Membership = {
  id: 'm1', userId: 'u9', userDisplayName: 'Pat Manager', role: 'BoardMember',
  status: 'Active', startDate: '2026-01-01', endDate: null,
};

function page(items: Membership[]): Paged<Membership> {
  return { items, total: items.length, limit: 25, offset: 0 };
}

describe('MembershipAdminComponent', () => {
  let fixture: ComponentFixture<MembershipAdminComponent>;
  let comp: MembershipAdminComponent;
  let el: HTMLElement;
  let board: jasmine.SpyObj<BoardService>;

  const userSig = signal<CurrentUser | null>({
    id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB',
    lastActiveMode: 'Board',
    memberships: [{ communityId: 'c1', communityName: 'One', role: 'CommunityManager' }],
  });

  async function build() {
    fixture = TestBed.createComponent(MembershipAdminComponent);
    comp = fixture.componentInstance;
    el = fixture.nativeElement;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(() => {
    board = jasmine.createSpyObj<BoardService>('BoardService', [
      'getMemberships', 'createMembership', 'updateMembership',
    ]);
    board.getMemberships.and.returnValue(Promise.resolve(page([MEMBER])));
    board.createMembership.and.returnValue(Promise.resolve({ ...MEMBER, id: 'm2', userId: 'newUser' }));
    board.updateMembership.and.returnValue(Promise.resolve({ ...MEMBER, role: 'CommunityManager' }));

    TestBed.configureTestingModule({
      imports: [MembershipAdminComponent],
      providers: [
        { provide: AuthService, useValue: { user: userSig.asReadonly() } },
        {
          provide: BoardNavigationService,
          useValue: {
            activeCommunityId: signal('c1').asReadonly(),
            // The component reads its community from the service's shared computed.
            effectiveCommunityId: signal('c1').asReadonly(),
          },
        },
        { provide: BoardService, useValue: board },
      ],
    });
  });

  it('lists memberships for the active community', async () => {
    await build();
    expect(board.getMemberships).toHaveBeenCalledWith('c1');
    expect(el.querySelector('[data-membership-id="m1"]')).toBeTruthy();
    expect(el.textContent).toContain('Pat Manager');
  });

  it('creates a membership (happy path) and reloads the roster', async () => {
    await build();
    comp.form.userId = 'newUser';
    comp.form.role = 'Accountant';
    comp.form.startDate = '2026-02-01';
    await comp.submit();
    expect(board.createMembership).toHaveBeenCalledWith('c1', {
      userId: 'newUser', role: 'Accountant', startDate: '2026-02-01', endDate: null,
    });
    // Reload after create.
    expect(board.getMemberships).toHaveBeenCalledTimes(2);
  });

  it('edits a membership (happy path) via the inline edit form', async () => {
    await build();
    comp.startEdit(MEMBER);
    expect(comp.editingId()).toBe('m1');
    comp.form.role = 'CommunityManager';
    comp.form.status = 'Active';
    await comp.submit();
    expect(board.updateMembership).toHaveBeenCalledWith('c1', 'm1', {
      role: 'CommunityManager', status: 'Active', endDate: null,
    });
    expect(comp.editingId()).toBeNull(); // form resets after save
  });

  it('surfaces a 403 as a manager-gating message (Scenario 5)', async () => {
    board.createMembership.and.returnValue(Promise.reject({ error: { code: 'FORBIDDEN' } }));
    await build();
    comp.form.userId = 'newUser';
    comp.form.startDate = '2026-02-01';
    await comp.submit();
    fixture.detectChanges();
    expect(comp.error()).toContain('not a manager');
    expect(el.querySelector('.ma-error')?.textContent).toContain('not a manager');
  });

  it('surfaces the LAST_MANAGER denial (constitution §3)', async () => {
    board.updateMembership.and.returnValue(Promise.reject({ error: { code: 'LAST_MANAGER' } }));
    await build();
    comp.startEdit(MEMBER);
    comp.form.status = 'Inactive';
    await comp.submit();
    fixture.detectChanges();
    expect(comp.error()).toContain('last active community manager');
  });
});
