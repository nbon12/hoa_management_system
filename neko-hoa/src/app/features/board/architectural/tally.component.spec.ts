import { render, screen } from '@testing-library/angular';
import { TallyComponent } from './tally.component';

describe('TallyComponent (027 FR-007)', () => {
  // US1-S5: "…the tally shows 2 approve, 0 deny and 3 not voted, reads "2/5", and its accessible label reads
  // "2 approve · 0 deny · 3 not voted"" (revisions-needed segment kept, per FR-007).
  it('reads "2/5" with the full accessible label', async () => {
    await render(TallyComponent, {
      componentInputs: { tally: { approve: 2, revisionsNeeded: 0, deny: 0, notVoted: 3, eligible: 5 } },
    });
    const tally = screen.getByRole('img', { name: '2 approve · 0 revisions needed · 0 deny · 3 not voted' });
    expect(tally.textContent).toContain('2/5');
    expect(tally.querySelectorAll('.tally__dot--approve').length).toBe(2);
    expect(tally.querySelectorAll('.tally__dot--none').length).toBe(3);
  });

  it('shows a revisions-needed segment', async () => {
    await render(TallyComponent, {
      componentInputs: { tally: { approve: 2, revisionsNeeded: 1, deny: 0, notVoted: 2, eligible: 5 } },
    });
    const tally = screen.getByRole('img', { name: '2 approve · 1 revisions needed · 0 deny · 2 not voted' });
    expect(tally.querySelectorAll('.tally__dot--revisions').length).toBe(1);
  });
});
