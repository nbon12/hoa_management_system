import { landingTargetFor } from './landing';
import { CommunityMembershipSummary, CurrentUser, UserMode } from '../models';

// 025 FR-026: the one landing rule shared by the sign-in path and the `/app` entry guard.

function user(lastActiveMode: UserMode, memberships: CommunityMembershipSummary[]): CurrentUser {
  return {
    id: 'u1', firstName: 'A', lastName: 'B', email: 'a@b.dev', initials: 'AB',
    lastActiveMode, memberships,
  };
}

describe('landingTargetFor (FR-026 default landing)', () => {
  it('resident mode lands on the resident dashboard', () => {
    const target = landingTargetFor(user('Resident', [
      { communityId: 'c1', communityName: 'One', role: 'Resident' },
    ]));
    expect(target.commands).toEqual(['/app/dashboard']);
    expect(target.activeCommunityId).toBeNull();
  });

  it('resident mode lands on the dashboard even for a board-eligible user', () => {
    // Board eligibility alone must not move a user who left the app in resident mode.
    const target = landingTargetFor(user('Resident', [
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
    ]));
    expect(target.commands).toEqual(['/app/dashboard']);
  });

  it('board mode + exactly one community lands on that community home (FR-026)', () => {
    const target = landingTargetFor(user('Board', [
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
    ]));
    expect(target.commands).toEqual(['/app/board/home']);
    expect(target.activeCommunityId).toBe('c1');
  });

  it('board mode + two roles in the SAME community is still one community', () => {
    const target = landingTargetFor(user('Board', [
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
      { communityId: 'c1', communityName: 'One', role: 'Accountant' },
    ]));
    expect(target.commands).toEqual(['/app/board/home']);
    expect(target.activeCommunityId).toBe('c1');
  });

  it('board mode + two communities lands on the My Communities list (FR-025)', () => {
    const target = landingTargetFor(user('Board', [
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
      { communityId: 'c2', communityName: 'Two', role: 'CommunityManager' },
    ]));
    expect(target.commands).toEqual(['/app/board/communities']);
    // No single community to act within until the user picks one.
    expect(target.activeCommunityId).toBeNull();
  });

  it('board mode with no memberships at all falls back to the resident dashboard', () => {
    const target = landingTargetFor(user('Board', []));
    expect(target.commands).toEqual(['/app/dashboard']);
    expect(target.activeCommunityId).toBeNull();
  });

  it('board mode with only Resident memberships falls back to the resident dashboard', () => {
    // Not board-eligible (FR-020) — boardGuard would refuse /app/board/* anyway.
    const target = landingTargetFor(user('Board', [
      { communityId: 'c1', communityName: 'One', role: 'Resident' },
      { communityId: 'c2', communityName: 'Two', role: 'Resident' },
    ]));
    expect(target.commands).toEqual(['/app/dashboard']);
  });

  it('no signed-in user lands on the dashboard', () => {
    expect(landingTargetFor(null).commands).toEqual(['/app/dashboard']);
  });
});
