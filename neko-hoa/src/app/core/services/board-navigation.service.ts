import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { CommunityMembershipSummary } from '../models';

// 025 FR-024/FR-025/FR-027/FR-040: the board sidebar is derived from data, never
// hand-built in the shell. This service owns that derivation so sibling specs (2-6)
// add sections by editing the descriptor list below — not the shell component.

/** Backend `CommunityRole` names (data-model.md). `Resident` confers no board capability. */
export const RESIDENT_ROLE = 'Resident';

/** A single sidebar entry, already resolved for the acting user's role set. */
export interface BoardNavItem {
  label: string;
  /** Router link, or `null` for a not-yet-built stub section. */
  route: string | null;
  /** Backend role names that unlock this item. Absent → available to every board user. */
  requiredRoles?: string[];
  /** True when the item is a placeholder for a sibling spec that has not shipped yet. */
  stub?: boolean;
  /** Resolved: role set does not confer this capability → show disabled with a 🔒 (FR-040). */
  locked: boolean;
  /** Resolved: item cannot be navigated to (locked, or a stub with no route). */
  disabled: boolean;
}

export interface BoardNavGroup {
  group: string | null;
  items: BoardNavItem[];
}

interface NavItemDescriptor {
  label: string;
  route: string | null;
  requiredRoles?: string[];
  stub?: boolean;
  /**
   * 030: `any` unlocks the item from a role held in ANY of the user's communities, not just the
   * active one ("Add community" depends on no particular community, brief 01 §4.1).
   */
  roleScope?: 'active' | 'any';
}

interface NavGroupDescriptor {
  group: string | null;
  items: NavItemDescriptor[];
}

/**
 * The board navigation descriptor list. Mirrors `boardNav()` in the wireframe bundle
 * (`wf-board.jsx`). Sibling specs extend this array; the shell renders whatever it returns.
 * The "My Communities" item is injected separately per FR-025 (only when >1 community).
 */
const BOARD_NAV: NavGroupDescriptor[] = [
  { group: null, items: [
    // 030 FR-036 (brief 01 D1): after "My Communities" when that item is shown. Unlocked by an
    // effective Community Manager membership in any Onboarding or Active community.
    { label: 'Add community', route: '/app/board/communities/new', requiredRoles: ['CommunityManager'], roleScope: 'any' },
    { label: 'Community Home', route: '/app/board/home' },
  ]},
  { group: 'Community management', items: [
    // 027: board members vote; managers record outcomes. Accountants have no ARC access.
    { label: 'Architectural Applications', route: '/app/board/architectural', requiredRoles: ['BoardMember', 'CommunityManager'] },
    { label: 'Board Approvals',            route: null, stub: true },
    { label: 'Announcements',              route: null, stub: true },
    // 030 FR-029: the active community's Community settings page; its Community Manager only.
    { label: 'Community settings',         route: '/app/board/settings/profile', requiredRoles: ['CommunityManager'] },
  ]},
  { group: 'Finance', items: [
    { label: 'AP Ledger',   route: null, stub: true, requiredRoles: ['CommunityManager', 'Accountant'] },
    { label: 'Vendor Aging', route: null, stub: true, requiredRoles: ['CommunityManager', 'Accountant'] },
  ]},
  { group: 'Vendors', items: [
    { label: 'Vendor Management', route: null, stub: true },
  ]},
  { group: null, items: [
    { label: 'Reports', route: null, stub: true },
  ]},
];

@Injectable({ providedIn: 'root' })
export class BoardNavigationService {
  private auth = inject(AuthService);

  /**
   * The community the board shell is currently acting within. For a single-community
   * user this is their only membership; a multi-community user selects one from the
   * My Communities list. `null` means "no community chosen yet".
   */
  private readonly _activeCommunityId = signal<string | null>(null);
  readonly activeCommunityId = this._activeCommunityId.asReadonly();

  setActiveCommunity(communityId: string | null): void {
    this._activeCommunityId.set(communityId);
  }

  /** Distinct communities the user holds an active membership in (FR-025). */
  readonly communityCount = computed(() => this.distinctCommunityIds(this.memberships()).length);

  /** The nav set for the current user + active community, ready to render. */
  readonly nav = computed<BoardNavGroup[]>(() => {
    const memberships = this.memberships();
    const communityIds = this.distinctCommunityIds(memberships);
    // Fall back to the sole community when none is explicitly selected (single-community land).
    const active = this._activeCommunityId() ?? (communityIds.length === 1 ? communityIds[0] : null);
    const roles = this.rolesForCommunity(memberships, active);
    const anyRoles = this.rolesForCommunity(memberships, null);
    return BoardNavigationService.buildNav({ roles, communityCount: communityIds.length, anyRoles });
  });

  /** 030 FR-036b: memberships in Archived communities confer nothing and are not counted. */
  private memberships(): CommunityMembershipSummary[] {
    return (this.auth.user()?.memberships ?? []).filter(m => m.communityStatus !== 'Archived');
  }

  private distinctCommunityIds(memberships: CommunityMembershipSummary[]): string[] {
    return Array.from(new Set(memberships.map(m => m.communityId)));
  }

  /**
   * The union of the user's active roles in the given community (Clarifications 2026-08-23).
   * When no community is selected, unions roles across every membership so a multi-community
   * user still sees a sensible superset until they pick one.
   */
  private rolesForCommunity(memberships: CommunityMembershipSummary[], communityId: string | null): string[] {
    const relevant = communityId
      ? memberships.filter(m => m.communityId === communityId)
      : memberships;
    return Array.from(new Set(relevant.map(m => m.role)));
  }

  /**
   * Pure nav derivation — the union of `roles` decides which items are unlocked, and
   * `communityCount` decides whether the My Communities item is rendered (FR-025).
   * Kept static and side-effect-free so it is exhaustively unit-testable (T035).
   */
  static buildNav({ roles, communityCount, anyRoles }: { roles: string[]; communityCount: number; anyRoles?: string[] }): BoardNavGroup[] {
    const roleSet = new Set(roles);
    // Roles across every community; without it, only the active community's roles count.
    const anyRoleSet = new Set([...(anyRoles ?? []), ...roles]);
    const groups: BoardNavGroup[] = [];

    if (communityCount > 1) {
      groups.push({
        group: null,
        items: [BoardNavigationService.resolveItem(
          { label: 'My Communities', route: '/app/board/communities' }, roleSet, anyRoleSet)],
      });
    }

    for (const g of BOARD_NAV) {
      groups.push({
        group: g.group,
        items: g.items.map(item => BoardNavigationService.resolveItem(item, roleSet, anyRoleSet)),
      });
    }

    return groups;
  }

  private static resolveItem(item: NavItemDescriptor, roleSet: Set<string>, anyRoleSet: Set<string>): BoardNavItem {
    const scope = item.roleScope === 'any' ? anyRoleSet : roleSet;
    const locked = !!item.requiredRoles && !item.requiredRoles.some(r => scope.has(r));
    return {
      label: item.label,
      route: item.route,
      requiredRoles: item.requiredRoles,
      stub: item.stub,
      locked,
      // A locked item is never navigable; a stub with no route is disabled but not locked.
      disabled: locked || item.route === null,
    };
  }
}
