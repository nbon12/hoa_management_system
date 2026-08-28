import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { BoardNavigationService, BoardNavGroup, BoardNavItem } from './board-navigation.service';
import { AuthService } from './auth.service';
import { CurrentUser, CommunityMembershipSummary } from '../models';

// Helpers to read items out of the derived nav groups.
function allItems(groups: BoardNavGroup[]): BoardNavItem[] {
  return groups.flatMap(g => g.items);
}
function item(groups: BoardNavGroup[], label: string): BoardNavItem | undefined {
  return allItems(groups).find(i => i.label === label);
}

describe('BoardNavigationService.buildNav (pure derivation)', () => {
  it('omits "My Communities" at 0 communities', () => {
    const nav = BoardNavigationService.buildNav({ roles: [], communityCount: 0 });
    expect(item(nav, 'My Communities')).toBeUndefined();
  });

  it('omits "My Communities" at exactly 1 community (FR-025)', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['BoardMember'], communityCount: 1 });
    expect(item(nav, 'My Communities')).toBeUndefined();
    // Community Home is always present for a board user.
    expect(item(nav, 'Community Home')).toBeDefined();
  });

  it('renders "My Communities" at 2+ communities (FR-025)', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['BoardMember'], communityCount: 3 });
    expect(item(nav, 'My Communities')).toBeDefined();
  });

  it('locks Finance items for a board-only role, visible but disabled with a lock (FR-040)', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['BoardMember'], communityCount: 1 });
    const ap = item(nav, 'AP Ledger')!;
    expect(ap).toBeDefined();          // rendered, not hidden
    expect(ap.locked).toBeTrue();      // lock affordance
    expect(ap.disabled).toBeTrue();    // not navigable
  });

  it('unlocks Finance items for a manager', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['CommunityManager'], communityCount: 1 });
    const aging = item(nav, 'Vendor Aging')!;
    expect(aging.locked).toBeFalse();
  });

  it('unlocks Finance items for an accountant', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['Accountant'], communityCount: 1 });
    expect(item(nav, 'AP Ledger')!.locked).toBeFalse();
  });

  it('gates the manager-only Memberships entry (locked for a board member)', () => {
    const board = BoardNavigationService.buildNav({ roles: ['BoardMember'], communityCount: 1 });
    expect(item(board, 'Memberships')!.locked).toBeTrue();
    const mgr = BoardNavigationService.buildNav({ roles: ['CommunityManager'], communityCount: 1 });
    expect(item(mgr, 'Memberships')!.locked).toBeFalse();
    expect(item(mgr, 'Memberships')!.route).toBe('/app/board/memberships');
  });

  it('resolves the union of two roles held in one community (Clarifications 2026-08-23)', () => {
    // A user who is BOTH a board member and an accountant sees the union: Finance unlocked
    // (from Accountant) AND everything a board member can reach.
    const nav = BoardNavigationService.buildNav({
      roles: ['BoardMember', 'Accountant'], communityCount: 1,
    });
    expect(item(nav, 'AP Ledger')!.locked).toBeFalse();     // conferred by Accountant
    expect(item(nav, 'Vendor Aging')!.locked).toBeFalse();  // conferred by Accountant
    // Memberships still requires CommunityManager, which neither role confers → locked.
    expect(item(nav, 'Memberships')!.locked).toBeTrue();
  });

  it('a plain resident role confers nothing board-specific (Finance stays locked)', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['Resident'], communityCount: 1 });
    expect(item(nav, 'AP Ledger')!.locked).toBeTrue();
    expect(item(nav, 'Memberships')!.locked).toBeTrue();
  });
});

describe('BoardNavigationService (signal-derived)', () => {
  const userSig = signal<CurrentUser | null>(null);

  function setUser(memberships: CommunityMembershipSummary[]) {
    userSig.set({
      id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB',
      lastActiveMode: 'Board', memberships,
    });
  }

  let svc: BoardNavigationService;

  beforeEach(() => {
    userSig.set(null);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { user: userSig.asReadonly() } },
      ],
    });
    svc = TestBed.inject(BoardNavigationService);
  });

  it('counts distinct communities', () => {
    setUser([
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
      { communityId: 'c1', communityName: 'One', role: 'Accountant' },
      { communityId: 'c2', communityName: 'Two', role: 'BoardMember' },
    ]);
    expect(svc.communityCount()).toBe(2);
    expect(item(svc.nav(), 'My Communities')).toBeDefined();
  });

  it('single-community board member: no My Communities, Finance locked', () => {
    setUser([{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]);
    expect(svc.communityCount()).toBe(1);
    expect(item(svc.nav(), 'My Communities')).toBeUndefined();
    expect(item(svc.nav(), 'AP Ledger')!.locked).toBeTrue();
  });

  it('derives the union of roles for the active community only', () => {
    setUser([
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
      { communityId: 'c1', communityName: 'One', role: 'CommunityManager' },
      { communityId: 'c2', communityName: 'Two', role: 'BoardMember' },
    ]);
    svc.setActiveCommunity('c1');
    // c1 union includes CommunityManager → Memberships unlocked.
    expect(item(svc.nav(), 'Memberships')!.locked).toBeFalse();

    svc.setActiveCommunity('c2');
    // c2 is board-only → Memberships locked again.
    expect(item(svc.nav(), 'Memberships')!.locked).toBeTrue();
  });
});
