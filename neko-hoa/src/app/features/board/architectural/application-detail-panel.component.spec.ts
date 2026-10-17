import { render, screen, fireEvent, waitFor } from '@testing-library/angular';
import { until } from './arc-testing';
import { ApplicationDetailPanelComponent } from './application-detail-panel.component';
import { ArcDetail, ArchitecturalService } from '../../../core/services/architectural.service';

function detail(over: Partial<ArcDetail> = {}): ArcDetail {
  return {
    id: 'a1', displayId: 'ARC-1042', revision: 1, propertyAddress: '711 Keystone Park Dr #29', ownerName: 'Praneeth Pattyam',
    projectTitle: 'Fence replacement — 6ft cedar', projectType: 'Fence', description: 'Rear fence', attachmentCount: 3,
    receivedDate: '2026-05-28', dueDate: '2026-06-27', overdue: false, status: 'Open', decision: null, infoRequested: false,
    tally: { approve: 2, revisionsNeeded: 0, deny: 0, notVoted: 3, eligible: 5 }, myVote: { state: 'CanVote' },
    attachments: [
      { id: 'f1', fileName: 'fence-plan.pdf', sizeBytes: 1_258_291, contentType: 'application/pdf' },
      { id: 'f2', fileName: 'elevation.jpg', sizeBytes: 860_160, contentType: 'image/jpeg' },
      { id: 'f3', fileName: 'plat-survey.pdf', sizeBytes: 2_202_009, contentType: 'application/pdf' },
    ],
    votes: [], infoRequests: [],
    ruleText: 'Three of five votes decide. The manager records the outcome and notifies the owner.',
    conditionsOfApproval: null, ownerReason: null, closedAt: null, ownerEmailStatus: null, revisions: [],
    ...over,
  };
}

describe('ApplicationDetailPanelComponent (027 US3)', () => {
  let arc: jasmine.SpyObj<ArchitecturalService>;

  async function setup(d: ArcDetail = detail(), manager = false) {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService',
      ['detail', 'attachmentUrl', 'vote', 'requestInfo', 'recordOutcome', 'resendOutcomeEmail']);
    arc.detail.and.returnValue(Promise.resolve(d));
    const r = await render(ApplicationDetailPanelComponent, {
      componentInputs: { communityId: 'c1', applicationId: 'a1', manager },
      providers: [{ provide: ArchitecturalService, useValue: arc }],
    });
    await waitFor(() => expect(screen.getByRole('heading', { level: 2 })).toBeTruthy());
    return r;
  }

  // US3-S1: title "ARC-1042 · fence replacement", owner, received date, three files with sizes.
  it('shows the title, owner, received date and files with sizes (US3-S1)', async () => {
    await setup();
    expect(screen.getByRole('heading', { level: 2 }).textContent!.trim()).toBe('ARC-1042 · fence replacement');
    expect(screen.getByText('Praneeth Pattyam')).toBeTruthy();
    expect(screen.getByText('05/28/26')).toBeTruthy();
    expect(screen.getByText('fence-plan.pdf')).toBeTruthy();
    expect(screen.getByText('1.2 MB')).toBeTruthy();
    expect(screen.getByText('840 KB')).toBeTruthy();
    expect(screen.getByText('2.1 MB')).toBeTruthy();
  });

  // US3-S2: opening a file fetches a short-lived link and opens it in a new tab.
  it('opens an attachment in a new tab through a fetched link (US3-S2)', async () => {
    await setup();
    arc.attachmentUrl.and.returnValue(Promise.resolve({ url: 'https://signed.example/x', expiresAt: '' }));
    const open = spyOn(window, 'open');
    fireEvent.click(screen.getByRole('button', { name: 'Open fence-plan.pdf in a new tab' }));
    await until(() => open.calls.count() > 0);
    expect(open).toHaveBeenCalledWith('https://signed.example/x', '_blank', 'noopener');
    expect(arc.attachmentUrl).toHaveBeenCalledWith('c1', 'a1', 'f1');
  });

  it('says there are no attachments (US3-S5)', async () => {
    await setup(detail({ attachments: [], attachmentCount: 0 }));
    expect(screen.getByText('No attachments.')).toBeTruthy();
  });

  // US3-S6: no storage URL is ever rendered before a click.
  it('renders no storage links (US3-S6)', async () => {
    const r = await setup();
    const html = (r.fixture.nativeElement as HTMLElement).innerHTML;
    expect(html).not.toContain('http');
    expect(r.fixture.nativeElement.querySelectorAll('a[href]').length).toBe(0);
  });

  it('shows "Attachment unavailable" when the link fails', async () => {
    await setup();
    arc.attachmentUrl.and.returnValue(Promise.reject({ status: 404 }));
    fireEvent.click(screen.getByRole('button', { name: 'Open fence-plan.pdf in a new tab' }));
    await waitFor(() => expect(screen.getByText('Attachment unavailable')).toBeTruthy());
  });

  it('shows the rule text and renders a comment as text, never HTML', async () => {
    const r = await setup(detail({
      votes: [{ voterName: 'Aaliyah Brooks', choice: 'Deny', comment: '<script>alert(1)</script>', castAt: '2026-06-01T00:00:00Z' }],
    }));
    expect(screen.getByText('Three of five votes decide. The manager records the outcome and notifies the owner.')).toBeTruthy();
    expect(screen.getByText('<script>alert(1)</script>')).toBeTruthy();
    expect(r.fixture.nativeElement.querySelector('script')).toBeNull();
  });

  // US4-S2 / US4-S4: the info-requested marker with message, sender and time.
  it('shows info requests with sender (US4-S2, US4-S4)', async () => {
    await setup(detail({
      infoRequested: true,
      infoRequests: [{ id: 'i1', requestedBy: 'Bianca Board', message: 'Please attach a plat survey', requestedAt: '2026-06-02T10:00:00Z', respondedAt: null }],
    }));
    expect(screen.getByText('info requested')).toBeTruthy();
    expect(screen.getByText('Please attach a plat survey')).toBeTruthy();
    expect(screen.getByText(/Bianca Board/)).toBeTruthy();
    // the vote buttons stay available
    expect(screen.getByRole('button', { name: '✓ Approve' })).toBeTruthy();
  });

  // US4-S5: "…When I choose Request info, Then I see the notice "Questions don't pause the review period
  // (due <date>)…" and the info-requested marker shows the unchanged due date."
  it('shows the review-period notice on Request info and the unchanged due date on the marker (US4-S5)', async () => {
    const r = await setup(detail({
      infoRequested: true,
      infoRequests: [{ id: 'i1', requestedBy: 'Bianca Board', message: 'Please attach a plat survey', requestedAt: '2026-06-02T10:00:00Z', respondedAt: null }],
    }));
    r.fixture.componentInstance.focusForInfo();
    await until(() => !!r.fixture.nativeElement.querySelector('[role="note"]'));
    r.fixture.detectChanges();
    expect(screen.getByRole('note').textContent!.trim()).toBe(
      "Questions don't pause the review period (due 06/27/26). To require changes before approval, vote Revisions needed — it counts as a formal denial and invites the owner to resubmit.");
    const marker = r.fixture.nativeElement.querySelector('.adp__info') as HTMLElement;
    expect(marker.textContent).toContain('info requested');
    expect(marker.textContent).toContain('review still due 06/27/26');
  });

  it('submits a vote and refreshes', async () => {
    const r = await setup();
    arc.vote.and.returnValue(Promise.resolve({} as any));
    fireEvent.click(screen.getByRole('button', { name: '✓ Approve' }));
    await until(() => arc.vote.calls.count() > 0 && arc.detail.calls.count() > 1);
    expect(arc.vote).toHaveBeenCalledWith('c1', 'a1', 'Approve', null);
    expect(r).toBeTruthy();
  });

  // US6-S12 (v2 side): "…it is shown as "ARC-1042" with a "v2" badge, a new received date and due date, no
  // votes, and a link to v1 showing its decision…"
  it('shows the v2 badge and links to earlier versions (US6-S12)', async () => {
    const r = await setup(detail({
      id: 'a1', revision: 2,
      revisions: [
        { id: 'v1', revision: 1, receivedDate: '2026-04-01', decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' } },
        { id: 'a1', revision: 2, receivedDate: '2026-05-28', decision: null },
      ],
    }));
    expect(screen.getByRole('heading', { level: 2 }).textContent).toContain('ARC-1042');
    expect(screen.getByLabelText('version 2').textContent!.trim()).toBe('v2');
    expect(screen.getByText('05/28/26')).toBeTruthy();
    expect(screen.getByText(/06\/27\/26/)).toBeTruthy();
    expect(r.fixture.nativeElement.querySelector('.adp__votes')).toBeNull();
    expect(screen.getByRole('button', { name: 'v1' })).toBeTruthy();
    expect(screen.getByText(/Denied · revisions requested/)).toBeTruthy();
  });

  // US6-S12 (v1 side): following the link, v1 shows "its decision, reason and board comments".
  it('shows an earlier version with its decision, reason and board comments (US6-S12)', async () => {
    await setup(detail({
      id: 'v1', revision: 1, status: 'Closed', myVote: { state: 'NotEligible' },
      decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' },
      ownerReason: 'Lower the fence to 5ft per Guideline 4.2',
      votes: [{ voterName: 'Bianca Board', choice: 'RevisionsNeeded', comment: 'Fence must be 5ft max per Guideline 4.2', castAt: '2026-04-10T10:00:00Z' }],
      revisions: [
        { id: 'v1', revision: 1, receivedDate: '2026-04-01', decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' } },
        { id: 'a1', revision: 2, receivedDate: '2026-05-28', decision: null },
      ],
    }));
    expect(screen.getByText('Denied · revisions requested')).toBeTruthy();
    expect(screen.getByText('Lower the fence to 5ft per Guideline 4.2')).toBeTruthy();
    expect(screen.getByText('Fence must be 5ft max per Guideline 4.2')).toBeTruthy();
    expect(screen.getByText('Bianca Board')).toBeTruthy();
  });

  it('shows the conditions of an approval', async () => {
    await setup(detail({
      status: 'Closed', myVote: { state: 'NotEligible' },
      decision: { outcome: 'Approved', wording: null, source: 'Votes' },
      conditionsOfApproval: 'Fence must be stained to match the existing color',
    }));
    expect(screen.getByText('Approved')).toBeTruthy();
    expect(screen.getByText('Fence must be stained to match the existing color')).toBeTruthy();
  });

  it('shows the record-outcome form to managers on a reached decision', async () => {
    await setup(detail({ status: 'DecisionReached', myVote: { state: 'NotEligible' },
      decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' } }), true);
    expect(screen.getByLabelText('What would need to change for approval?')).toBeTruthy();
  });

  it('requests info, then refreshes', async () => {
    await setup();
    arc.requestInfo.and.returnValue(Promise.resolve({} as any));
    fireEvent.input(screen.getByLabelText('Comment to the board'), { target: { value: 'Please attach a plat survey' } });
    fireEvent.click(screen.getByRole('button', { name: 'Request info' }));
    await until(() => arc.requestInfo.calls.count() > 0 && arc.detail.calls.count() > 1);
    expect(arc.requestInfo).toHaveBeenCalledWith('c1', 'a1', 'Please attach a plat survey');
  });

  it('shows the server message when an action fails', async () => {
    await setup();
    arc.vote.and.returnValue(Promise.reject({ error: { message: 'A decision has already been reached on this application.' } }));
    fireEvent.click(screen.getByRole('button', { name: '✕ Deny' }));
    await until(() => !!screen.queryByRole('alert')?.textContent?.includes('A decision has already been reached'));
  });

  it('records the outcome and resends a failed email as a manager', async () => {
    await setup(detail({ status: 'DecisionReached', myVote: { state: 'NotEligible' },
      decision: { outcome: 'Approved', wording: null, source: 'Votes' } }), true);
    arc.recordOutcome.and.returnValue(Promise.resolve({} as any));
    arc.detail.and.returnValue(Promise.resolve(detail({ status: 'Closed', myVote: { state: 'NotEligible' },
      decision: { outcome: 'Approved', wording: null, source: 'Votes' }, ownerEmailStatus: 'Failed' })));
    fireEvent.click(screen.getByRole('button', { name: 'Record outcome and email owner' }));
    await until(() => !!screen.queryByRole('button', { name: 'Resend email' }));
    expect(arc.recordOutcome).toHaveBeenCalledWith('c1', 'a1', {});
    arc.resendOutcomeEmail.and.returnValue(Promise.resolve({ ownerEmailStatus: 'Pending' }));
    fireEvent.click(screen.getByRole('button', { name: 'Resend email' }));
    await until(() => arc.resendOutcomeEmail.calls.count() > 0);
  });

  it('shows my recorded vote, the formal-denial note, and recusal', async () => {
    await setup(detail({ myVote: { state: 'Voted', choice: 'RevisionsNeeded' } }));
    expect(screen.getByText('you voted revisions needed')).toBeTruthy();
    expect(screen.getByText('Revisions needed counts as a formal denial; the owner is invited to revise and resubmit.')).toBeTruthy();
  });

  it('explains recusal', async () => {
    await setup(detail({ myVote: { state: 'Recused' } }));
    expect(screen.getByText('You own this property, so you are recused.')).toBeTruthy();
  });

  it('shows decision and overdue markers, and opens an earlier version', async () => {
    const r = await setup(detail({
      overdue: true, status: 'DecisionReached', myVote: { state: 'NotEligible' },
      decision: { outcome: 'Approved', wording: null, source: 'Lapse' },
      revisions: [
        { id: 'v1', revision: 1, receivedDate: '2026-04-01', decision: null },
        { id: 'a1', revision: 2, receivedDate: '2026-05-28', decision: null },
      ],
    }));
    expect(screen.getByText('overdue')).toBeTruthy();
    expect(screen.getByText('decision reached: approved by default (review period lapsed)')).toBeTruthy();
    const opened = jasmine.createSpy('openRevision');
    r.fixture.componentInstance.openRevision.subscribe(opened);
    fireEvent.click(screen.getByRole('button', { name: 'v1' }));
    expect(opened).toHaveBeenCalledWith('v1');
  });

  it('shows an error when the application cannot be loaded', async () => {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService', ['detail']);
    arc.detail.and.returnValue(Promise.reject({ status: 403 }));
    await render(ApplicationDetailPanelComponent, {
      componentInputs: { communityId: 'c1', applicationId: 'a1' },
      providers: [{ provide: ArchitecturalService, useValue: arc }],
    });
    await until(() => !!screen.queryByRole('alert')?.textContent?.includes('could not be loaded'));
  });

  it('focuses the comment box when opened with startWithInfo (US4-S1)', async () => {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService', ['detail']);
    arc.detail.and.returnValue(Promise.resolve(detail()));
    await render(ApplicationDetailPanelComponent, {
      componentInputs: { communityId: 'c1', applicationId: 'a1', startWithInfo: true },
      providers: [{ provide: ArchitecturalService, useValue: arc }],
    });
    await until(() => document.activeElement === screen.queryByLabelText('Comment to the board'));
    await until(() => !!screen.queryByRole('note'));
  });

  it('queues focus for Request info until the vote card renders', async () => {
    const r = await setup();
    r.fixture.componentInstance.focusForInfo();
    r.fixture.detectChanges();
    expect(document.activeElement).toBe(screen.getByLabelText('Comment to the board'));
  });

  // 029 US3 AS2: the owner's reply to an info request is visible to the board.
  it("shows the owner's reply under an answered info request (029)", async () => {
    await setup(detail({ infoRequests: [{ id: 'q1', requestedBy: 'Nicholas Board', message: 'Please attach a plat survey',
      requestedAt: '2026-06-01T12:00:00Z', respondedAt: '2026-06-02T12:00:00Z', responseMessage: 'Survey attached' }] }));
    await waitFor(() => expect(screen.getByText(/Owner replied/)).toBeTruthy());
    expect(screen.getByText(/Owner replied/).textContent).toContain('Survey attached');
  });
});
