import { CommunityMembershipSummary, CurrentUser } from '../models';
import { RESIDENT_ROLE } from './board-navigation.service';

// 025 FR-026: where a user lands when they arrive without asking for a specific page —
// signing in, or entering the app at the bare `/app` path. The rule is the same one
// `ModeToggleComponent.switch()` applies when a user enters board mode; every default
// landing goes through this one function so the two cannot drift apart.
//
// 030 R11 / FR-036a: the server now computes the landing (`LandingRule`) and sends it as
// `CurrentUserDto.landing`. The client follows it, so there is one rule, tested in both places.
// When `landing` is absent (a pre-030 backend) the 025 rule below still applies.

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

const DASHBOARD: LandingTarget = { commands: ['/app/dashboard'], activeCommunityId: null };

/** The memberships that count for board mode: non-resident, in an Onboarding or Active community (FR-036b). */
export function boardMemberships(user: CurrentUser | null | undefined): CommunityMembershipSummary[] {
  return (user?.memberships ?? []).filter(m => m.role !== RESIDENT_ROLE && m.communityStatus !== 'Archived');
}

/**
 * Board landing: one community → its home (FR-026); two or more → My Communities (FR-025).
 * Distinct communities are counted exactly as BoardNavigationService counts them for the
 * "My Communities" entry, so the landing agrees with the sidebar it renders.
 * Returns `null` when the user holds no board membership at all.
 */
function boardTarget(user: CurrentUser | null | undefined): LandingTarget | null {
  const communityIds = Array.from(new Set(boardMemberships(user).map(m => m.communityId)));
  if (communityIds.length === 0) return null;
  return communityIds.length === 1
    ? { commands: ['/app/board/home'], activeCommunityId: communityIds[0] }
    : { commands: ['/app/board/communities'], activeCommunityId: null };
}

/**
 * Pure landing decision for the acting user.
 *
 * With the server's `landing` (spec 030):
 * - `Company` → the portfolio; `NoHome` → `/app/no-home`; `NotLive` → `/app/not-live`;
 * - `Resident` → the resident dashboard;
 * - `Board` → that community's home, or My Communities (falls back to the dashboard when the
 *   client holds no board membership, so a stale user can never loop through `boardGuard`).
 *
 * Without it (025): board mode + a board membership → the board landing; anything else → the dashboard.
 */
export function landingTargetFor(user: CurrentUser | null | undefined): LandingTarget {
  switch (user?.landing) {
    case 'Company':  return { commands: ['/app/company/portfolio'], activeCommunityId: null };
    case 'NoHome':   return { commands: ['/app/no-home'], activeCommunityId: null };
    case 'NotLive':  return { commands: ['/app/not-live'], activeCommunityId: null };
    case 'Resident': return DASHBOARD;
    case 'Board':    return boardTarget(user) ?? DASHBOARD;
  }

  // 025 rule. Board mode is only meaningful for a user holding ≥1 active non-resident
  // membership — the same eligibility ModeToggleComponent renders on (FR-020) and boardGuard enforces.
  if (user?.lastActiveMode !== 'Board') return DASHBOARD;
  return boardTarget(user) ?? DASHBOARD;
}

/**
 * Whether the shell shows board mode. 025: the persisted `lastActiveMode`. 030 FR-036a: a user
 * with no usable home whose landing is board mode is in board mode whatever their last mode was,
 * because they have no resident home to show.
 */
export function isBoardMode(user: CurrentUser | null | undefined): boolean {
  if (!user) return false;
  if (user.lastActiveMode === 'Board') return true;
  return user.landing === 'Board' && (user.properties?.length ?? 0) === 0;
}
