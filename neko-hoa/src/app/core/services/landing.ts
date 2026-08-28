import { CurrentUser } from '../models';
import { isBoardEligible } from './board-navigation.service';

// 025 FR-026: where a user lands when they arrive without asking for a specific page —
// signing in, or entering the app at the bare `/app` path. The rule is the same one
// `ModeToggleComponent.switch()` applies when a user enters board mode; every default
// landing goes through this one function so the two cannot drift apart.

export interface LandingTarget {
  /** Router commands for the landing page. */
  commands: string[];
  /**
   * The community the board shell should act within on arrival: the user's sole community,
   * or `null` when there is no single one (resident landing, or a multi-community user who
   * still has to pick from My Communities). Applied via `BoardNavigationService`.
   */
  activeCommunityId: string | null;
}

/**
 * Pure landing decision for the acting user.
 *
 * - Board mode + exactly one active community → that community's home (FR-026).
 * - Board mode + two or more → the My Communities list (FR-025).
 * - Anything else (resident mode, or no active non-resident membership) → the resident dashboard.
 */
export function landingTargetFor(user: CurrentUser | null | undefined): LandingTarget {
  const memberships = user?.memberships ?? [];

  // Board mode is only meaningful for a user holding ≥1 active non-resident membership —
  // literally the same `isBoardEligible` predicate ModeToggleComponent renders on (FR-020)
  // and boardGuard enforces.
  if (user?.lastActiveMode !== 'Board' || !isBoardEligible(memberships)) {
    return { commands: ['/app/dashboard'], activeCommunityId: null };
  }

  // Distinct active communities, counted exactly as BoardNavigationService counts them for
  // the "My Communities" entry (FR-025), so the landing agrees with the sidebar it renders.
  const communityIds = Array.from(new Set(memberships.map(m => m.communityId)));
  return communityIds.length === 1
    ? { commands: ['/app/board/home'], activeCommunityId: communityIds[0] }
    : { commands: ['/app/board/communities'], activeCommunityId: null };
}
