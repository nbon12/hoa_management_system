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

  it('gates the manager-only Community settings entry (locked for a board member)', () => {
    const board = BoardNavigationService.buildNav({ roles: ['BoardMember'], communityCount: 1 });
    expect(item(board, 'Community settings')!.locked).toBeTrue();
    const mgr = BoardNavigationService.buildNav({ roles: ['CommunityManager'], communityCount: 1 });
    expect(item(mgr, 'Community settings')!.locked).toBeFalse();
    // 030 T091: the old Memberships page is a section of Community settings now.
    expect(item(mgr, 'Memberships')).toBeUndefined();
  });

  it('resolves the union of two roles held in one community (Clarifications 2026-08-23)', () => {
    // A user who is BOTH a board member and an accountant sees the union: Finance unlocked
    // (from Accountant) AND everything a board member can reach.
    const nav = BoardNavigationService.buildNav({
      roles: ['BoardMember', 'Accountant'], communityCount: 1,
    });
    expect(item(nav, 'AP Ledger')!.locked).toBeFalse();     // conferred by Accountant
    expect(item(nav, 'Vendor Aging')!.locked).toBeFalse();  // conferred by Accountant
    // Community settings still requires CommunityManager, which neither role confers → locked.
    expect(item(nav, 'Community settings')!.locked).toBeTrue();
  });

  it('027: Architectural Applications is live for board members and managers, locked for accountants', () => {
    for (const role of ['BoardMember', 'CommunityManager']) {
      const arc = item(BoardNavigationService.buildNav({ roles: [role], communityCount: 1 }), 'Architectural Applications')!;
      expect(arc.locked).toBeFalse();
      expect(arc.disabled).toBeFalse();
      expect(arc.route).toBe('/app/board/architectural');
    }
    const accountant = item(BoardNavigationService.buildNav({ roles: ['Accountant'], communityCount: 1 }), 'Architectural Applications')!;
    expect(accountant.locked).toBeTrue();
  });

  it('027: ARC Settings is manager-only', () => {
    expect(item(BoardNavigationService.buildNav({ roles: ['BoardMember'], communityCount: 1 }), 'ARC Settings')!.locked).toBeTrue();
    const mgr = item(BoardNavigationService.buildNav({ roles: ['CommunityManager'], communityCount: 1 }), 'ARC Settings')!;
    expect(mgr.locked).toBeFalse();
    expect(mgr.route).toBe('/app/board/arc-settings');
  });

  it('a plain resident role confers nothing board-specific (Finance stays locked)', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['Resident'], communityCount: 1 });
    expect(item(nav, 'AP Ledger')!.locked).toBeTrue();
    expect(item(nav, 'Community settings')!.locked).toBeTrue();
  });
});

// 030 T069 / brief 01 §4.1, FR-029, FR-036: the board nav gains "Add community" (top block, after
// My Communities) and "Community settings" (last in Community management) for Community Managers.
// No "Portfolio" item exists for anyone, locked or not (FR-035).
describe('BoardNavigationService.buildNav (030 board-nav entries)', () => {
  function labels(groups: BoardNavGroup[]): string[] {
    return allItems(groups).map(i => i.label);
  }

  it('unlocks Community settings and Add community for a Community Manager', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['CommunityManager'], communityCount: 1 });
    const settings = item(nav, 'Community settings')!;
    expect(settings.locked).toBeFalse();
    expect(settings.disabled).toBeFalse();
    expect(settings.route).toBe('/app/board/settings/profile');
    const add = item(nav, 'Add community')!;
    expect(add.locked).toBeFalse();
    expect(add.route).toBe('/app/board/communities/new');
  });

  it('places Add community after My Communities and before Community Home, and Community settings last in its group (D1)', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['CommunityManager'], communityCount: 5 });
    expect(labels(nav).slice(0, 3)).toEqual(['My Communities', 'Add community', 'Community Home']);
    const management = nav.find(g => g.group === 'Community management')!;
    expect(management.items[management.items.length - 1].label).toBe('Community settings');
  });

  it('shows both entries locked for a Board Member or an Accountant (spec 025 FR-040, 8.01.k, 8.01.o)', () => {
    for (const role of ['BoardMember', 'Accountant']) {
      const nav = BoardNavigationService.buildNav({ roles: [role], communityCount: 1 });
      expect(item(nav, 'Community settings')!.locked).withContext(role).toBeTrue();
      expect(item(nav, 'Add community')!.locked).withContext(role).toBeTrue();
    }
  });

  it('unlocks both after a Board Member is appointed co-manager (union of roles, 8.01.l)', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['BoardMember', 'CommunityManager'], communityCount: 1 });
    expect(item(nav, 'Community settings')!.locked).toBeFalse();
    expect(item(nav, 'Add community')!.locked).toBeFalse();
  });

  it('unlocks Add community from a manager role in another community; Community settings follows the active one', () => {
    const nav = BoardNavigationService.buildNav({ roles: ['BoardMember'], communityCount: 2, anyRoles: ['BoardMember', 'CommunityManager'] });
    expect(item(nav, 'Add community')!.locked).toBeFalse();
    expect(item(nav, 'Community settings')!.locked).toBeTrue();
  });

  it('never offers a Portfolio item (FR-035)', () => {
    for (const roles of [['CommunityManager'], ['BoardMember'], ['Accountant']]) {
      expect(labels(BoardNavigationService.buildNav({ roles, communityCount: 3 }))).not.toContain('Portfolio');
    }
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
    // c1 union includes CommunityManager → Community settings unlocked.
    expect(item(svc.nav(), 'Community settings')!.locked).toBeFalse();

    svc.setActiveCommunity('c2');
    // c2 is board-only → Community settings locked again.
    expect(item(svc.nav(), 'Community settings')!.locked).toBeTrue();
  });
});

describe('BoardNavigationService (030 signal-derived)', () => {
  const userSig = signal<CurrentUser | null>(null);
  let svc: BoardNavigationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { user: userSig.asReadonly() } }],
    });
    svc = TestBed.inject(BoardNavigationService);
  });

  it('Add community follows any community; Community settings follows the active one', () => {
    userSig.set({
      id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB', lastActiveMode: 'Board',
      memberships: [
        { communityId: 'c1', communityName: 'One', role: 'BoardMember', communityStatus: 'Active' },
        { communityId: 'c2', communityName: 'Two', role: 'CommunityManager', communityStatus: 'Onboarding' },
      ],
    });
    svc.setActiveCommunity('c1');
    expect(item(svc.nav(), 'Add community')!.locked).toBeFalse();
    expect(item(svc.nav(), 'Community settings')!.locked).toBeTrue();
    svc.setActiveCommunity('c2');
    expect(item(svc.nav(), 'Community settings')!.locked).toBeFalse();
  });

  it('does not count or unlock from an Archived community (FR-036b)', () => {
    userSig.set({
      id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB', lastActiveMode: 'Board',
      memberships: [
        { communityId: 'c1', communityName: 'One', role: 'Accountant', communityStatus: 'Active' },
        { communityId: 'old', communityName: 'Old Mill Commons', role: 'CommunityManager', communityStatus: 'Archived' },
      ],
    });
    expect(svc.communityCount()).toBe(1);
    expect(item(svc.nav(), 'My Communities')).toBeUndefined();
    expect(item(svc.nav(), 'Add community')!.locked).toBeTrue();
  });
});
