import { HttpErrorResponse } from '@angular/common/http';
import { fireEvent, render, screen } from '@testing-library/angular';
import { WithdrawDialogComponent } from './withdraw-dialog.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { DETAIL } from './resident-arc-fixtures';
import { until } from '../../board/architectural/arc-testing';

describe('WithdrawDialogComponent (029 US4)', () => {
  async function setup() {
    const api = jasmine.createSpyObj<ResidentArchitecturalService>('ResidentArchitecturalService', ['withdraw']);
    const withdrawn = jasmine.createSpy('withdrawn');
    const cancelled = jasmine.createSpy('cancelled');
    await render(WithdrawDialogComponent, {
      componentInputs: { applicationId: 'a1', displayId: 'ARC-1042' },
      componentOutputs: { withdrawn: { emit: withdrawn } as never, cancelled: { emit: cancelled } as never },
      providers: [{ provide: ResidentArchitecturalService, useValue: api }],
    });
    return { api, withdrawn, cancelled };
  }

  it('is a labelled modal dialog that starts focus on the safe choice', async () => {
    await setup();
    const dialog = screen.getByRole('dialog', { name: 'Withdraw ARC-1042?' });
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(document.activeElement?.textContent).toContain('Keep request');
  });

  it('Escape cancels', async () => {
    const { cancelled, api } = await setup();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(cancelled).toHaveBeenCalled();
    expect(api.withdraw).not.toHaveBeenCalled();
  });

  it('keeps Tab inside the dialog', async () => {
    await setup();
    const [keep, withdraw] = screen.getAllByRole('button');
    withdraw.focus();
    fireEvent.keyDown(document, { key: 'Tab' });
    expect(document.activeElement).toBe(keep);
    fireEvent.keyDown(document, { key: 'Tab', shiftKey: true });
    expect(document.activeElement).toBe(withdraw);
  });

  it('confirming withdraws and emits the updated request', async () => {
    const { api, withdrawn } = await setup();
    api.withdraw.and.resolveTo({ ...DETAIL, status: 'Withdrawn' });
    fireEvent.click(screen.getByRole('button', { name: /Withdraw request/ }));
    await until(() => withdrawn.calls.count() === 1);
    expect(api.withdraw).toHaveBeenCalledWith('a1');
  });

  // US4 AS2 (UX): a refusal (already decided) is shown, not swallowed.
  it('shows a refusal', async () => {
    const { api, withdrawn } = await setup();
    api.withdraw.and.rejectWith(new HttpErrorResponse({ status: 409, error: { code: 'APPLICATION_DECIDED' } }));
    fireEvent.click(screen.getByRole('button', { name: /Withdraw request/ }));
    await until(() => !!screen.queryByRole('alert'));
    expect(screen.getByRole('alert').textContent).toContain('already reached a decision');
    expect(withdrawn).not.toHaveBeenCalled();
  });
});
