import { boardMemberships, isBoardMode, landingTargetFor } from './landing';
import { CommunityMembershipSummary, CurrentUser, Landing, UserMode } from '../models';

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

// 030 T044 / R11 / FR-036a: the server's LandingRule decides; the client follows `landing`.
// The five landings, the Onboarding case (FR-036b), and the guard against a stale `Board`.
describe('landingTargetFor (030 server landing)', () => {
  function withLanding(landing: Landing, memberships: CommunityMembershipSummary[] = [], over: Partial<CurrentUser> = {}): CurrentUser {
    return { ...user('Resident', memberships), properties: [], isCompanyAdministrator: false, landing, ...over };
  }

  it('Company → the portfolio (US9-S1)', () => {
    const target = landingTargetFor(withLanding('Company', [], { isCompanyAdministrator: true }));
    expect(target.commands).toEqual(['/app/company/portfolio']);
    expect(target.activeCommunityId).toBeNull();
  });

  it('NoHome → "No home linked to this account" (US9-S3)', () => {
    expect(landingTargetFor(withLanding('NoHome')).commands).toEqual(['/app/no-home']);
  });

  it('NotLive → "Your community isn\'t live yet" (US9-S5)', () => {
    expect(landingTargetFor(withLanding('NotLive')).commands).toEqual(['/app/not-live']);
  });

  it('Resident → the resident dashboard, even with a board membership', () => {
    const target = landingTargetFor(withLanding('Resident', [{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }]));
    expect(target.commands).toEqual(['/app/dashboard']);
    expect(target.activeCommunityId).toBeNull();
  });

  it('Board with one Onboarding community → that community home, whatever lastActiveMode says (US9-S2, FR-036b)', () => {
    // Pat Rivera: no home, Community Manager of Keystone Park only, which is Onboarding.
    const target = landingTargetFor(withLanding('Board', [
      { communityId: 'kp', communityName: 'Keystone Park', role: 'CommunityManager', communityStatus: 'Onboarding' },
    ]));
    expect(target.commands).toEqual(['/app/board/home']);
    expect(target.activeCommunityId).toBe('kp');
  });

  it('Board with two communities → My Communities', () => {
    const target = landingTargetFor(withLanding('Board', [
      { communityId: 'c1', communityName: 'One', role: 'CommunityManager', communityStatus: 'Active' },
      { communityId: 'c2', communityName: 'Two', role: 'CommunityManager', communityStatus: 'Onboarding' },
    ]));
    expect(target.commands).toEqual(['/app/board/communities']);
    expect(target.activeCommunityId).toBeNull();
  });

  it('Board does not count an Archived community (FR-036b)', () => {
    const target = landingTargetFor(withLanding('Board', [
      { communityId: 'c1', communityName: 'One', role: 'Accountant', communityStatus: 'Active' },
      { communityId: 'old', communityName: 'Old Mill Commons', role: 'Accountant', communityStatus: 'Archived' },
    ]));
    expect(target.commands).toEqual(['/app/board/home']);
    expect(target.activeCommunityId).toBe('c1');
  });

  it('a stale Board landing with no board membership falls back to the dashboard (no guard loop)', () => {
    expect(landingTargetFor(withLanding('Board')).commands).toEqual(['/app/dashboard']);
  });

  it('boardMemberships keeps non-resident memberships outside Archived communities', () => {
    const u = withLanding('Board', [
      { communityId: 'c1', communityName: 'One', role: 'Resident', communityStatus: 'Active' },
      { communityId: 'c2', communityName: 'Two', role: 'BoardMember', communityStatus: 'Onboarding' },
      { communityId: 'c3', communityName: 'Three', role: 'BoardMember', communityStatus: 'Archived' },
    ]);
    expect(boardMemberships(u).map(m => m.communityId)).toEqual(['c2']);
    expect(boardMemberships(null)).toEqual([]);
  });
});

describe('isBoardMode (025 FR-022, 030 FR-036a)', () => {
  it('follows lastActiveMode for a user with a home', () => {
    expect(isBoardMode(user('Board', []))).toBeTrue();
    expect(isBoardMode({ ...user('Resident', []), landing: 'Resident', properties: [{ id: 'p', accountNumber: 'A', address: '1 St' }] })).toBeFalse();
  });

  it('treats a no-home user whose landing is Board as in board mode', () => {
    expect(isBoardMode({ ...user('Resident', []), landing: 'Board', properties: [] })).toBeTrue();
  });

  it('is false with a home and a Board landing in resident mode, and for nobody', () => {
    expect(isBoardMode({ ...user('Resident', []), landing: 'Board', properties: [{ id: 'p', accountNumber: 'A', address: '1 St' }] })).toBeFalse();
    expect(isBoardMode(null)).toBeFalse();
  });
});
