import { render, screen, fireEvent } from '@testing-library/angular';
import { RecordOutcomeComponent } from './record-outcome.component';
import { ArcDetail } from '../../../core/services/architectural.service';

function detail(over: Partial<ArcDetail>): ArcDetail {
  return {
    id: 'a1', displayId: 'ARC-1042', revision: 1, propertyAddress: 'x', ownerName: 'y', projectTitle: 'z', projectType: 'Fence',
    description: '', attachmentCount: 0, receivedDate: '2026-05-28', dueDate: '2026-06-27', overdue: false,
    status: 'DecisionReached', decision: { outcome: 'Approved', wording: null, source: 'Votes' }, infoRequested: false,
    tally: { approve: 3, revisionsNeeded: 0, deny: 0, notVoted: 2, eligible: 5 }, myVote: { state: 'NotEligible' },
    attachments: [], votes: [], infoRequests: [], ruleText: '', conditionsOfApproval: null, ownerReason: null,
    closedAt: null, ownerEmailStatus: null, revisions: [], ...over,
  };
}

describe('RecordOutcomeComponent (027 US6, FR-025/FR-026)', () => {
  async function setup(d: ArcDetail) {
    const recorded = jasmine.createSpy('recorded');
    const resend = jasmine.createSpy('resend');
    await render(RecordOutcomeComponent, {
      componentInputs: { detail: d },
      componentOutputs: { recorded: { emit: recorded } as any, resend: { emit: resend } as any },
    });
    return { recorded, resend };
  }

  it('requires a reason for a denial, with the FR-025 prompt', async () => {
    const { recorded } = await setup(detail({ decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' } }));
    expect(screen.getByLabelText('What would need to change for approval?')).toBeTruthy();
    expect(screen.queryByLabelText('Owner sees')).toBeNull();
    expect(screen.queryByLabelText('Conditions of approval (optional)')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Record outcome and email owner' }));
    expect(recorded).not.toHaveBeenCalled();
    expect(screen.getByRole('alert').textContent).toContain('reason');

    fireEvent.input(screen.getByLabelText('What would need to change for approval?'), { target: { value: 'Lower the fence to 5ft per Guideline 4.2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Record outcome and email owner' }));
    expect(recorded).toHaveBeenCalledWith({ ownerReason: 'Lower the fence to 5ft per Guideline 4.2' });
  });

  it('offers conditions only for approvals', async () => {
    const { recorded } = await setup(detail({}));
    fireEvent.input(screen.getByLabelText('Conditions of approval (optional)'), { target: { value: 'Stain to match' } });
    fireEvent.click(screen.getByRole('button', { name: 'Record outcome and email owner' }));
    expect(recorded).toHaveBeenCalledWith({ conditionsOfApproval: 'Stain to match' });
  });

  it('lets the manager pick the wording only for a denial by default (lapse)', async () => {
    const { recorded } = await setup(detail({ decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Lapse' } }));
    fireEvent.input(screen.getByLabelText('What would need to change for approval?'), { target: { value: 'Lapsed' } });
    fireEvent.change(screen.getByLabelText('Owner sees'), { target: { value: 'Denied' } });
    fireEvent.click(screen.getByRole('button', { name: 'Record outcome and email owner' }));
    expect(recorded).toHaveBeenCalledWith({ ownerReason: 'Lapsed', wording: 'Denied' });
  });

  it('offers Resend when the owner email failed, and explains a missing owner email', async () => {
    const { resend } = await setup(detail({ status: 'Closed', ownerEmailStatus: 'Failed' }));
    fireEvent.click(screen.getByRole('button', { name: 'Resend email' }));
    expect(resend).toHaveBeenCalled();
  });

  it('explains NoOwnerEmail without a resend button', async () => {
    await setup(detail({ status: 'Closed', ownerEmailStatus: 'NoOwnerEmail' }));
    expect(screen.getByText('No owner email on file — notify the owner another way.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Resend email' })).toBeNull();
  });
});
