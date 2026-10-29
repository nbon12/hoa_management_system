import { render, screen, fireEvent } from '@testing-library/angular';
import { VoteChoiceComponent } from './vote-choice.component';

describe('VoteChoiceComponent (031 items 3, 5)', () => {
  async function setup(submitLabel = 'Save') {
    const submitted = jasmine.createSpy('submitted');
    const r = await render(VoteChoiceComponent, {
      componentInputs: { submitLabel },
      componentOutputs: { submitted: { emit: submitted } as any },
    });
    return { r, submitted };
  }

  // 031 US1-S4: "Given the quick-vote card, When it renders, Then no option is pre-selected and no Save button shows."
  it('pre-selects nothing and hides the submit button', async () => {
    await setup();
    for (const name of ['Approve', 'Request revisions', 'Deny'])
      expect(screen.getByRole('button', { name }).getAttribute('aria-pressed')).toBe('false');
    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull();
  });

  // 031 US1-S4: "When I pick Approve, Then Save appears and nothing is cast until I click it."
  it('shows Save after a pick and emits only on Save', async () => {
    const { submitted } = await setup();
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(submitted).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(submitted).toHaveBeenCalledOnceWith('Approve');
  });

  it('switches or clears the pick', async () => {
    const { r } = await setup('Submit');
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    fireEvent.click(screen.getByRole('button', { name: 'Deny' }));
    expect(r.fixture.componentInstance.selected()).toBe('Deny');
    fireEvent.click(screen.getByRole('button', { name: 'Deny' }));
    expect(r.fixture.componentInstance.selected()).toBeNull();
    expect(screen.queryByRole('button', { name: 'Submit' })).toBeNull();
  });

  // 031 US1-S5 (item 4): the options use the shared .vote-choice class, which carries the hover background.
  it('uses the shared hover-styled choice buttons', async () => {
    await setup();
    expect(screen.getByRole('button', { name: 'Deny' }).classList).toContain('vote-choice');
  });
});
