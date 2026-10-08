import { render, screen, fireEvent } from '@testing-library/angular';
import { CastVoteCardComponent } from './cast-vote-card.component';

describe('CastVoteCardComponent (027 US2/US4)', () => {
  async function setup() {
    const voted = jasmine.createSpy('voted');
    const infoRequested = jasmine.createSpy('infoRequested');
    const r = await render(CastVoteCardComponent, {
      componentInputs: { dueDate: '2026-06-27', ruleText: 'Three of five votes decide. The manager records the outcome and notifies the owner.' },
      componentOutputs: { voted: { emit: voted } as any, infoRequested: { emit: infoRequested } as any },
    });
    return { r, voted, infoRequested };
  }

  it('has the three vote buttons and the comment box', async () => {
    await setup();
    for (const name of ['✓ Approve', '↻ Revisions needed', '✕ Deny', 'Request info'])
      expect(screen.getByRole('button', { name })).toBeTruthy();
    expect(screen.getByLabelText('Comment to the board')).toBeTruthy();
  });

  // US2-S5: comment + Deny → vote({choice:'Deny', comment}).
  it('emits Deny with the comment (US2-S5)', async () => {
    const { voted } = await setup();
    fireEvent.input(screen.getByLabelText('Comment to the board'), { target: { value: 'Fence height exceeds the 5ft limit in §4.2' } });
    fireEvent.click(screen.getByRole('button', { name: '✕ Deny' }));
    expect(voted).toHaveBeenCalledWith({ choice: 'Deny', comment: 'Fence height exceeds the 5ft limit in §4.2' });
  });

  // US2-S6: the comment is optional.
  it('emits a vote with a null comment when empty (US2-S6)', async () => {
    const { voted } = await setup();
    fireEvent.click(screen.getByRole('button', { name: '✕ Deny' }));
    expect(voted).toHaveBeenCalledWith({ choice: 'Deny', comment: null });
  });

  // US4-S3 (client side) / US4-S5: Request info needs a message and shows the clock notice.
  it('refuses an empty info request and shows the review-period notice (US4-S5)', async () => {
    const { infoRequested } = await setup();
    fireEvent.click(screen.getByRole('button', { name: 'Request info' }));
    expect(infoRequested).not.toHaveBeenCalled();
    expect(screen.getByRole('alert').textContent).toContain('Say what information is needed.');
    expect(screen.getByRole('note').textContent).toBe(
      "Questions don't pause the review period (due 06/27/26). To require changes before approval, vote Revisions needed — it counts as a formal denial and invites the owner to resubmit.");
  });

  it('emits the info request message (US4-S2)', async () => {
    const { infoRequested } = await setup();
    fireEvent.input(screen.getByLabelText('Comment to the board'), { target: { value: 'Please attach a plat survey' } });
    fireEvent.click(screen.getByRole('button', { name: 'Request info' }));
    expect(infoRequested).toHaveBeenCalledWith('Please attach a plat survey');
  });

  it('focusForInfo focuses the comment box and shows the notice (US4-S1)', async () => {
    const { r } = await setup();
    r.fixture.componentInstance.focusForInfo();
    r.fixture.detectChanges();
    expect(document.activeElement).toBe(screen.getByLabelText('Comment to the board'));
    expect(screen.getByRole('note')).toBeTruthy();
  });
});
