import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { fireEvent, render, screen } from '@testing-library/angular';
import { RequestDetailComponent } from './request-detail.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { APPROVED, DENIED, DETAIL, MORE_INFO } from './resident-arc-fixtures';
import { ResidentArcDetail } from '../../../core/models';
import { until } from '../../board/architectural/arc-testing';

describe('RequestDetailComponent (029 US2–US4, US6)', () => {
  let api: jasmine.SpyObj<ResidentArchitecturalService>;

  async function setup(detail: ResidentArcDetail) {
    api = jasmine.createSpyObj<ResidentArchitecturalService>('ResidentArchitecturalService', ['detail', 'attachmentUrl', 'withdraw', 'reply']);
    api.detail.and.resolveTo(detail);
    await render(RequestDetailComponent, {
      providers: [
        provideRouter([]),
        { provide: ResidentArchitecturalService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: detail.id }) } } },
      ],
    });
    await until(() => !!screen.queryByText(new RegExp(detail.displayId!)));
  }

  // US2 AS2: submitted fields, timeline and files.
  it('shows what was submitted, the timeline and the files', async () => {
    await setup(DETAIL);
    expect(screen.getByText('Replace the rear fence with 6ft cedar.')).toBeTruthy();
    expect(screen.getByText(/Cedar & Co/)).toBeTruthy();
    expect(screen.getByText(/fence-plan\.pdf/)).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'Timeline' })).toBeTruthy();
    expect(screen.getByText('Submitted / Under review')).toBeTruthy();
  });

  // US2 AS3: a file opens through the short-lived link endpoint, in a new tab.
  it('opens a file through the link endpoint', async () => {
    await setup(DETAIL);
    api.attachmentUrl.and.resolveTo({ url: 'https://signed.example/x?X-Amz-Expires=300', expiresAt: '' });
    const open = spyOn(window, 'open');
    fireEvent.click(screen.getByRole('button', { name: 'Open fence-plan.pdf' }));
    await until(() => open.calls.count() === 1);
    expect(api.attachmentUrl).toHaveBeenCalledWith('a1', 'f1');
    expect(open).toHaveBeenCalledWith('https://signed.example/x?X-Amz-Expires=300', '_blank', 'noopener');
  });

  // US2 AS4: a denial shows the wording, the reason, the formal statement and Revise and resubmit.
  it('shows the denial wording, reason, formal statement and the revise link', async () => {
    await setup(DENIED);
    expect(screen.getByRole('heading', { name: 'Denied · revisions requested' })).toBeTruthy();
    expect(screen.getByText('Lower the fence to 5ft per Guideline 4.2')).toBeTruthy();
    expect(screen.getByText('This is a formal disapproval of the plans as submitted.')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Revise and resubmit' }).getAttribute('href')).toBe('/app/property/architectural/a1/revise');
    expect(screen.queryByRole('button', { name: 'Withdraw request' })).toBeNull();
  });

  it('shows approval conditions', async () => {
    await setup(APPROVED);
    expect(screen.getByRole('heading', { name: 'Approved' })).toBeTruthy();
    expect(screen.getByText('Stain to match the existing color')).toBeTruthy();
    expect(screen.queryByRole('link', { name: 'Revise and resubmit' })).toBeNull();
  });

  // US2 AS5 / FR-016: even if a response carried board data, the page has nowhere to render it.
  it('never renders votes, voters or board comments', async () => {
    await setup({ ...DETAIL, votes: [{ voterName: 'Board Voter', comment: 'SECRET-COMMENT' }] } as unknown as ResidentArcDetail);
    expect(document.body.textContent).not.toContain('Board Voter');
    expect(document.body.textContent).not.toContain('SECRET-COMMENT');
    expect(document.body.textContent?.toLowerCase()).not.toContain('vote');
  });

  // US3 AS1: an unanswered question appears with a reply box.
  it('shows the board question with a reply form', async () => {
    await setup(MORE_INFO);
    expect(screen.getByText('Please attach a plat survey')).toBeTruthy();
    expect(screen.getByLabelText('Your reply')).toBeTruthy();
  });

  // US4 AS1: Withdraw opens the confirm dialog; confirming withdraws and shows the new status.
  it('withdraws after confirmation', async () => {
    await setup(DETAIL);
    api.withdraw.and.resolveTo({ ...DETAIL, status: 'Withdrawn', canWithdraw: false });
    fireEvent.click(screen.getByRole('button', { name: 'Withdraw request' }));
    expect(screen.getByRole('dialog', { name: 'Withdraw ARC-1042?' })).toBeTruthy();
    fireEvent.click(screen.getAllByRole('button', { name: /Withdraw request/ }).at(-1)!);
    await until(() => !!screen.queryByText('Withdrawn'));
    expect(api.withdraw).toHaveBeenCalledWith('a1');
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Withdraw request' })).toBeNull();
  });
});
