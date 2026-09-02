import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { CommunityMembershipSummary } from '../models';

// 025 FR-024/FR-025/FR-027/FR-040: the board sidebar is derived from data, never
// hand-built in the shell. This service owns that derivation so sibling specs (2-6)
// add sections by editing the descriptor list below — not the shell component.

/** Backend `CommunityRole` names (data-model.md). `Resident` confers no board capability. */
export const RESIDENT_ROLE = 'Resident';

/**
 * Board eligibility: the user holds at least one active non-resident membership. The single
 * rule behind the mode toggle (FR-020), the landing decision (FR-026) and `boardGuard`
 * (FR-028), so those three can never disagree about who is a board user.
 */
export function isBoardEligible(memberships: CommunityMembershipSummary[]): boolean {
  return memberships.some(m => m.role !== RESIDENT_ROLE);
}

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
    { label: 'Community Home', route: '/app/board/home' },
  ]},
  { group: 'Community management', items: [
    { label: 'Architectural Applications', route: null, stub: true },
    { label: 'Board Approvals',            route: null, stub: true },
    { label: 'Announcements',              route: null, stub: true },
    { label: 'Memberships',                route: '/app/board/memberships', requiredRoles: ['CommunityManager'] },
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

  /**
   * The community a board *page* should load data for: the explicitly selected one, or —
   * before anything is selected — the user's first membership so the page has something to
   * query. Deliberately looser than the `nav` fallback below, which only assumes a community
   * when the user has exactly one; a multi-community user landing here is corrected as soon
   * as they pick from My Communities.
   */
  readonly effectiveCommunityId = computed<string | null>(() => {
    const memberships = this.memberships();
    const active = this._activeCommunityId();
    if (active) return active;
    return memberships.length ? memberships[0].communityId : null;
  });

  /** The nav set for the current user + active community, ready to render. */
  readonly nav = computed<BoardNavGroup[]>(() => {
    const memberships = this.memberships();
    const communityIds = this.distinctCommunityIds(memberships);
    // Fall back to the sole community when none is explicitly selected (single-community land).
    const active = this._activeCommunityId() ?? (communityIds.length === 1 ? communityIds[0] : null);
    const roles = this.rolesForCommunity(memberships, active);
    return BoardNavigationService.buildNav({ roles, communityCount: communityIds.length });
  });

  private memberships(): CommunityMembershipSummary[] {
    return this.auth.user()?.memberships ?? [];
  }

  private distinctCommunityIds(memberships: CommunityMembershipSummary[]): string[] {
    return Array.from(new Set(memberships.map(m => m.communityId)));
  }

  /**
   * The union of the user's active roles in the given community (Clarifications 2026-08-23).
   * When no community is selected, unions roles across every membership so a multi-community
   * user still sees a sensible superset until they pick one.
   *
   * Public because `boardGuard` scopes its role check the same way — one derivation, so the
   * guard and the sidebar cannot disagree about which roles are in play.
   */
  rolesForCommunity(memberships: CommunityMembershipSummary[], communityId: string | null): string[] {
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
  static buildNav({ roles, communityCount }: { roles: string[]; communityCount: number }): BoardNavGroup[] {
    const roleSet = new Set(roles);
    const groups: BoardNavGroup[] = [];

    if (communityCount > 1) {
      groups.push({
        group: null,
        items: [BoardNavigationService.resolveItem(
          { label: 'My Communities', route: '/app/board/communities' }, roleSet)],
      });
    }

    for (const g of BOARD_NAV) {
      groups.push({
        group: g.group,
        items: g.items.map(item => BoardNavigationService.resolveItem(item, roleSet)),
      });
    }

    return groups;
  }

  private static resolveItem(item: NavItemDescriptor, roleSet: Set<string>): BoardNavItem {
    const locked = !!item.requiredRoles && !item.requiredRoles.some(r => roleSet.has(r));
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
