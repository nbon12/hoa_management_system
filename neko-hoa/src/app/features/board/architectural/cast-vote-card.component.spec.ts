import { render, screen, fireEvent } from '@testing-library/angular';
import { CastVoteCardComponent } from './cast-vote-card.component';

describe('CastVoteCardComponent (027 US2/US4, 031)', () => {
  async function setup() {
    const voted = jasmine.createSpy('voted');
    const infoRequested = jasmine.createSpy('infoRequested');
    const r = await render(CastVoteCardComponent, {
      componentInputs: { dueDate: '2026-06-27', ruleText: 'Three of five votes decide. The manager records the outcome and notifies the owner.' },
      componentOutputs: { voted: { emit: voted } as any, infoRequested: { emit: infoRequested } as any },
    });
    return { r, voted, infoRequested };
  }

  // 031 US1-S3: "Given the Cast your vote card, When it renders, Then Approve, Request revisions and Deny are offered,
  // none is selected, and no Submit button shows until I pick one."
  it('offers three unselected choices, Ask question and the comment box', async () => {
    await setup();
    for (const name of ['Approve', 'Request revisions', 'Deny'])
      expect(screen.getByRole('button', { name }).getAttribute('aria-pressed')).toBe('false');
    expect(screen.getByRole('button', { name: 'Ask question' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Submit vote' })).toBeNull();
    expect(screen.getByLabelText('Comment to the board').classList).toContain('field');
  });

  // US2-S5 / 031 US1-S3: comment + Deny + Submit vote → vote({choice:'Deny', comment}); picking alone casts nothing.
  it('casts Deny with the comment only after Submit vote (US2-S5)', async () => {
    const { voted } = await setup();
    fireEvent.input(screen.getByLabelText('Comment to the board'), { target: { value: 'Fence height exceeds the 5ft limit in §4.2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Deny' }));
    expect(voted).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Deny' }).getAttribute('aria-pressed')).toBe('true');
    fireEvent.click(screen.getByRole('button', { name: 'Submit vote' }));
    expect(voted).toHaveBeenCalledWith({ choice: 'Deny', comment: 'Fence height exceeds the 5ft limit in §4.2' });
  });

  // US2-S6: the comment is optional. 031 item 14: "Request revisions" casts RevisionsNeeded.
  it('emits a vote with a null comment when empty (US2-S6)', async () => {
    const { voted } = await setup();
    fireEvent.click(screen.getByRole('button', { name: 'Request revisions' }));
    fireEvent.click(screen.getByRole('button', { name: 'Submit vote' }));
    expect(voted).toHaveBeenCalledWith({ choice: 'RevisionsNeeded', comment: null });
  });

  // US4-S3 (client side) / US4-S5 / 031 US2-S4: Ask question needs a message and shows the due-date notice.
  it('refuses an empty question and shows the due-date notice (US4-S5, 031 item 13)', async () => {
    const { infoRequested } = await setup();
    fireEvent.click(screen.getByRole('button', { name: 'Ask question' }));
    expect(infoRequested).not.toHaveBeenCalled();
    expect(screen.getByRole('alert').textContent).toContain('Type your question for the owner');
    expect(screen.getByRole('note').textContent).toBe(
      'Board vote result due 06/27/26. Info requests do not delay the due date of the board vote result. Failure to ' +
      'approve or deny by the due date may result in auto-approval. To request revisions, please select "Request revisions".');
  });

  it('emits the question (US4-S2, 031 item 11)', async () => {
    const { infoRequested } = await setup();
    fireEvent.input(screen.getByLabelText('Comment to the board'), { target: { value: 'Please attach a plat survey' } });
    fireEvent.click(screen.getByRole('button', { name: 'Ask question' }));
    expect(infoRequested).toHaveBeenCalledWith('Please attach a plat survey');
  });

  it('focusForInfo focuses the comment box and shows the notice (US4-S1)', async () => {
    const { r } = await setup();
    r.fixture.componentInstance.focusForInfo();
    r.fixture.detectChanges();
    expect(document.activeElement).toBe(screen.getByLabelText('Comment to the board'));
    expect(screen.getByRole('note')).toBeTruthy();
  });

  it('reset clears the comment and the picked choice', async () => {
    const { r } = await setup();
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    r.fixture.componentInstance.comment = 'x';
    r.fixture.componentInstance.reset();
    r.fixture.detectChanges();
    expect(r.fixture.componentInstance.comment).toBe('');
    expect(screen.queryByRole('button', { name: 'Submit vote' })).toBeNull();
  });
});
