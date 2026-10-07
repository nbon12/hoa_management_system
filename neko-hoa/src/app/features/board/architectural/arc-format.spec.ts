import { decisionReachedLabel, fmtDate, fmtSize, infoNotice, myVoteLabel, outcomeLabel, shortProject, tallyLabel } from './arc-format';

describe('arc-format (027)', () => {
  it('formats date-only strings without time-zone drift', () => {
    expect(fmtDate('2026-06-27')).toBe('06/27/26');
    expect(fmtDate(null)).toBe('—');
  });

  it('formats sizes like the wireframe', () => {
    expect(fmtSize(1_258_291)).toBe('1.2 MB');
    expect(fmtSize(860_160)).toBe('840 KB');
    expect(fmtSize(512)).toBe('512 B');
  });

  it('builds the FR-007 tally label with zero segments kept (US1-S5)', () => {
    expect(tallyLabel({ approve: 2, revisionsNeeded: 0, deny: 0, notVoted: 3, eligible: 5 }))
      .toBe('2 approve · 0 revisions needed · 0 deny · 3 not voted');
    expect(tallyLabel({ approve: 2, revisionsNeeded: 1, deny: 0, notVoted: 2, eligible: 5 }))
      .toBe('2 approve · 1 revisions needed · 0 deny · 2 not voted');
  });

  it('labels decisions for the open and closed tabs (FR-024/FR-025)', () => {
    expect(decisionReachedLabel({ outcome: 'Approved', wording: null, source: 'Votes' })).toBe('decision reached: approve');
    expect(decisionReachedLabel({ outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' }))
      .toBe('decision reached: denied · revisions requested');
    expect(decisionReachedLabel({ outcome: 'Denied', wording: 'Denied', source: 'Votes' })).toBe('decision reached: denied');
    expect(decisionReachedLabel({ outcome: 'Approved', wording: null, source: 'Lapse' }))
      .toBe('decision reached: approve (by default — review period lapsed)');
    expect(outcomeLabel({ outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' })).toBe('Denied · revisions requested');
    expect(outcomeLabel({ outcome: 'Denied', wording: 'Denied', source: 'Votes' })).toBe('Denied');
    expect(outcomeLabel({ outcome: 'Approved', wording: null, source: 'Votes' })).toBe('Approved');
  });

  it('labels my vote and the short project title', () => {
    expect(myVoteLabel('RevisionsNeeded')).toBe('you voted revisions needed');
    expect(myVoteLabel('Approve')).toBe('you voted approve');
    expect(myVoteLabel('Deny')).toBe('you voted deny');
    expect(shortProject('Fence replacement — 6ft cedar')).toBe('fence replacement');
  });

  it('builds the US4-S5 info notice with the due date', () => {
    expect(infoNotice('2026-06-27')).toBe(
      "Questions don't pause the review period (due 06/27/26). To require changes before approval, vote Revisions needed — it counts as a formal denial and invites the owner to resubmit.");
  });

  it('handles odd inputs', () => {
    expect(fmtDate('not-a-date')).toBe('not-a-date');
    expect(myVoteLabel(null)).toBe('');
    expect(shortProject('Detached shed, 10x12')).toBe('detached shed, 10x12');
  });
});
