import {
  CHOICE_LABELS, decisionReachedLabel, finalIcon, finalResultLabel, fmtDate, fmtDateTime, fmtSize, infoNotice, myVoteLabel,
  outcomeLabel, shortProject, tallyLabel, votedLabel,
} from './arc-format';

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
      .toBe('2 approve · 0 request revisions · 0 deny · 3 not voted');
    expect(tallyLabel({ approve: 2, revisionsNeeded: 1, deny: 0, notVoted: 2, eligible: 5 }))
      .toBe('2 approve · 1 request revisions · 0 deny · 2 not voted');
  });

  it('labels decisions for the open and closed tabs (FR-024/FR-025)', () => {
    expect(decisionReachedLabel({ outcome: 'Approved', wording: null, source: 'Votes' })).toBe('decision reached: approve');
    expect(decisionReachedLabel({ outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' }))
      .toBe('decision reached: denied · revisions requested');
    expect(decisionReachedLabel({ outcome: 'Denied', wording: 'Denied', source: 'Votes' })).toBe('decision reached: denied');
    // US6-S15 / US6-S16: deemed approved / deemed denied.
    expect(decisionReachedLabel({ outcome: 'Approved', wording: null, source: 'Lapse' }))
      .toBe('decision reached: approved by default (review period lapsed)');
    expect(decisionReachedLabel({ outcome: 'Denied', wording: 'Denied', source: 'Lapse' }))
      .toBe('decision reached: denied by default (review period lapsed)');
    expect(decisionReachedLabel({ outcome: 'Denied', wording: 'RevisionsRequested', source: 'Lapse' }))
      .toBe('decision reached: denied by default (review period lapsed)');
    expect(outcomeLabel({ outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' })).toBe('Denied · revisions requested');
    expect(outcomeLabel({ outcome: 'Denied', wording: 'Denied', source: 'Votes' })).toBe('Denied');
    expect(outcomeLabel({ outcome: 'Approved', wording: null, source: 'Votes' })).toBe('Approved');
  });

  it('labels my vote and the short project title', () => {
    expect(myVoteLabel('RevisionsNeeded')).toBe('you voted to request revisions');
    expect(myVoteLabel('Approve')).toBe('you voted approve');
    expect(myVoteLabel('Deny')).toBe('you voted deny');
    expect(shortProject('Fence replacement — 6ft cedar')).toBe('fence replacement');
  });

  it('builds the info notice with the due date (027 US4-S5, 031 US2-S4)', () => {
    expect(infoNotice('2026-06-27')).toBe(
      'Board vote result due 06/27/26. Info requests do not delay the due date of the board vote result. Failure to ' +
      'approve or deny by the due date may result in auto-approval. To request revisions, please select "Request revisions".');
  });

  // 031 US2-S1: "Given the board's votes, When the Details section renders, Then each voter is listed as
  // '<name> — voted approve' (never 'you voted' for someone else)."
  it('labels anyone\'s vote without "you" (031 item 10)', () => {
    expect(votedLabel('Approve')).toBe('voted approve');
    expect(votedLabel('RevisionsNeeded')).toBe('voted to request revisions');
    expect(votedLabel('Deny')).toBe('voted deny');
    expect(votedLabel(undefined)).toBe('');
  });

  // 031 US2-S2: "Board final result: Not yet decided / Application Approved / Revisions Requested (…) / Denied".
  it('labels the board final result (031 item 10)', () => {
    expect(finalResultLabel(null)).toBe('Not yet decided');
    expect(finalResultLabel({ outcome: 'Approved', wording: null, source: 'Votes' })).toBe('Application Approved');
    expect(finalResultLabel({ outcome: 'Approved', wording: null, source: 'Lapse' }))
      .toBe('Application Approved (by default — review period lapsed)');
    expect(finalResultLabel({ outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' }))
      .toBe('Revisions Requested (effectively denied; the resident is asked to revise and resubmit)');
    expect(finalResultLabel({ outcome: 'Denied', wording: 'Denied', source: 'Votes' })).toBe('Denied');
    expect(finalResultLabel({ outcome: 'Withdrawn', wording: null, source: null })).toBe('Withdrawn by the owner');
  });

  // 031 US1-S6: final-decision icons — ✓ approved, ✕ denied, yellow ↻ revisions requested.
  it('maps decisions to final-decision icons (031 item 8)', () => {
    expect(finalIcon(null)).toBeNull();
    expect(finalIcon({ outcome: 'Approved', wording: null, source: 'Lapse' })).toBe('approved');
    expect(finalIcon({ outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' })).toBe('revisions');
    expect(finalIcon({ outcome: 'Denied', wording: 'Denied', source: 'Votes' })).toBe('denied');
    expect(finalIcon({ outcome: 'Withdrawn', wording: null, source: null })).toBe('withdrawn');
  });

  it('formats instants and labels choices (031)', () => {
    expect(fmtDateTime(null)).toBe('—');
    expect(fmtDateTime('nope')).toBe('nope');
    expect(fmtDateTime('2026-10-12T19:05:00Z')).toMatch(/^10\/1[23]\/26/);
    expect(CHOICE_LABELS.RevisionsNeeded).toBe('Request revisions');
  });

  it('handles odd inputs', () => {
    expect(fmtDate('not-a-date')).toBe('not-a-date');
    expect(myVoteLabel(null)).toBe('');
    expect(shortProject('Detached shed, 10x12')).toBe('detached shed, 10x12');
  });
});
