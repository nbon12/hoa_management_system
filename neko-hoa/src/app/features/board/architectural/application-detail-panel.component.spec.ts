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
      ['detail', 'attachmentUrl', 'vote', 'undoVote', 'requestInfo', 'recordOutcome', 'resendOutcomeEmail']);
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
    expect(screen.getByRole('button', { name: 'Approve' })).toBeTruthy();
  });

  // US4-S5 as amended by 031 US2-S4 (item 13): "…When I choose Ask question, Then I see the due date and 'Info requests
  // do not delay the due date of the board vote result…'", and the info-requested marker carries the same notice.
  it('shows the due-date notice on Ask question and on the info-requested marker (US4-S5, 031 item 13)', async () => {
    const r = await setup(detail({
      infoRequested: true,
      infoRequests: [{ id: 'i1', requestedBy: 'Bianca Board', message: 'Please attach a plat survey', requestedAt: '2026-06-02T10:00:00Z', respondedAt: null }],
    }));
    r.fixture.componentInstance.focusForInfo();
    await until(() => !!r.fixture.nativeElement.querySelector('[role="note"]'));
    r.fixture.detectChanges();
    expect(screen.getByRole('note').textContent!.trim()).toBe(
      'Board vote result due 06/27/26. Info requests do not delay the due date of the board vote result. Failure to ' +
      'approve or deny by the due date may result in auto-approval. To request revisions, please select "Request revisions".');
    const marker = r.fixture.nativeElement.querySelector('.adp__info') as HTMLElement;
    expect(marker.textContent).toContain('info requested');
    expect(marker.textContent).toContain('Info requests do not delay the due date of the board vote result.');
    expect(marker.textContent).not.toContain('review still due');
  });

  it('submits a vote and refreshes', async () => {
    const r = await setup();
    arc.vote.and.returnValue(Promise.resolve({} as any));
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(arc.vote).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Submit vote' }));
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
    expect(screen.getByText('Revisions Requested (effectively denied; the resident is asked to revise and resubmit)')).toBeTruthy();
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
    expect(screen.getByText('Application Approved')).toBeTruthy();
    expect(screen.getByRole('img', { name: 'Final decision: Application Approved' })).toBeTruthy();
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
    fireEvent.click(screen.getByRole('button', { name: 'Ask question' }));
    await until(() => arc.requestInfo.calls.count() > 0 && arc.detail.calls.count() > 1);
    expect(arc.requestInfo).toHaveBeenCalledWith('c1', 'a1', 'Please attach a plat survey');
  });

  it('shows the server message when an action fails', async () => {
    await setup();
    arc.vote.and.returnValue(Promise.reject({ error: { message: 'A decision has already been reached on this application.' } }));
    fireEvent.click(screen.getByRole('button', { name: 'Deny' }));
    fireEvent.click(screen.getByRole('button', { name: 'Submit vote' }));
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
    expect(screen.getByText('you voted to request revisions')).toBeTruthy();
    expect(screen.getByText('Request revisions counts as a formal denial; the owner is invited to revise and resubmit.')).toBeTruthy();
  });

  // 031 US2-S1 (item 10): "Given three board votes, When the Details section renders, Then it lists
  // 'Aaliyah Brooks — voted approve', 'Marcus Chen — voted approve', 'Bianca Board — voted to request revisions'."
  it('lists every vote as "— voted …", never "you voted" (031 item 10)', async () => {
    const r = await setup(detail({ votes: [
      { voterName: 'Aaliyah Brooks', choice: 'Approve', comment: null, castAt: '2026-06-01T14:03:00Z' },
      { voterName: 'Marcus Chen', choice: 'Approve', comment: null, castAt: '2026-06-02T09:10:00Z' },
      { voterName: 'Bianca Board', choice: 'RevisionsNeeded', comment: null, castAt: '2026-06-03T09:10:00Z' },
    ] }));
    const items = Array.from(r.fixture.nativeElement.querySelectorAll('.adp__votes li') as NodeListOf<HTMLElement>)
      .map(li => li.textContent!.replace(/\s+/g, ' ').trim());
    expect(items).toEqual(['Aaliyah Brooks — voted approve', 'Marcus Chen — voted approve', 'Bianca Board — voted to request revisions']);
    expect(r.fixture.nativeElement.querySelector('.adp__votes').textContent).not.toContain('you voted');
  });

  // 031 US2-S2 (item 10): "Board final result: Not yet decided" until final; then the outcome.
  it('shows the board final result (031 item 10)', async () => {
    await setup();
    expect(screen.getByText('Not yet decided')).toBeTruthy();
  });

  it('shows "Not yet decided" with a pending note while votes can still change (031 item 5)', async () => {
    await setup(detail({ status: 'DecisionReached', decision: { outcome: 'Approved', wording: null, source: 'Votes' },
      decisionFinal: false, votesLockAt: '2026-06-20T16:00:00Z', myVote: { state: 'Voted', choice: 'Approve' }, canUndoVote: true }));
    expect(screen.getByText('Not yet decided')).toBeTruthy();
    expect(screen.getByText(/board members can change their votes until 06\/20\/26/)).toBeTruthy();
  });

  // 031 US3-S1 from the panel: undo then refresh.
  it('undoes my vote from the panel (031 item 5)', async () => {
    await setup(detail({ myVote: { state: 'Voted', choice: 'Approve' }, canUndoVote: true, votesLockAt: null }));
    arc.undoVote.and.returnValue(Promise.resolve({} as any));
    fireEvent.click(screen.getByRole('button', { name: 'undo' }));
    await until(() => arc.undoVote.calls.count() > 0 && arc.detail.calls.count() > 1);
    expect(arc.undoVote).toHaveBeenCalledWith('c1', 'a1');
  });

  // 031 US6-S1 (item 12): "Given an application approved by default, When I open it, Then where the vote card would
  // be I see a statement citing the governing documents and the final result shows the green ✓."
  it('explains a lapse approval with the governing-document citation (031 item 12)', async () => {
    const statement = 'Approved by default: the board did not approve or deny this application by its due date (06/27/26). ' +
      'Under Article IX, Section 9.4 of the Declaration, an application the board does not act on within the 30-day review ' +
      'period is deemed approved.';
    await setup(detail({ status: 'Closed', decisionFinal: true, myVote: { state: 'NotEligible' },
      decision: { outcome: 'Approved', wording: null, source: 'Lapse' }, lapseStatement: statement }));
    expect(screen.getByText(statement)).toBeTruthy();
    expect(screen.getByText('Approved by default')).toBeTruthy();
    expect(screen.queryByText('Cast your vote')).toBeNull();
    expect(screen.getAllByRole('img', { name: /Final decision: Application Approved/ }).length).toBe(2);
  });

  // 031 US7-S2 (item 15): the homeowner's comments get their own section under the details.
  it("shows the homeowner's comments in their own section (031 item 15)", async () => {
    const r = await setup(detail({
      attachments: [
        { id: 'f1', fileName: 'fence-plan.pdf', sizeBytes: 1000, contentType: 'application/pdf' },
        { id: 'c1f', fileName: 'revised-plan.pdf', sizeBytes: 2048, contentType: 'application/pdf', ownerCommentId: 'c1' },
      ],
      ownerComments: [{ id: 'c1', authorName: 'Praneeth Pattyam', body: 'Lowered the fence to 5ft', createdAt: '2026-06-05T15:00:00Z',
        attachments: [{ id: 'c1f', fileName: 'revised-plan.pdf', sizeBytes: 2048, contentType: 'application/pdf', ownerCommentId: 'c1' }] }],
    }));
    const section = screen.getByRole('region', { name: 'Homeowner comments' });
    expect(section.textContent).toContain('Praneeth Pattyam');
    expect(section.textContent).toContain('Lowered the fence to 5ft');
    expect(section.textContent).toContain('revised-plan.pdf');
    // The comment's file is listed with the comment, not with the submitted attachments.
    expect(r.fixture.nativeElement.querySelectorAll('.adp__files li').length).toBe(1);
    arc.attachmentUrl.and.returnValue(Promise.resolve({ url: 'https://signed.example/x', expiresAt: '' }));
    const open = spyOn(window, 'open');
    fireEvent.click(screen.getByRole('button', { name: 'Open revised-plan.pdf in a new tab' }));
    await until(() => open.calls.count() > 0);
  });

  it('says when the homeowner has not commented', async () => {
    await setup();
    expect(screen.getByText("The homeowner hasn't commented on this application.")).toBeTruthy();
  });

  // 031 US1-S9 (item 9): "When I click an application, Then skeletons show in place of the details and the vote card
  // until it loads."
  it('shows two skeletons while the application loads (031 item 9)', async () => {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService', ['detail']);
    let resolve!: (d: ArcDetail) => void;
    arc.detail.and.returnValue(new Promise<ArcDetail>(res => (resolve = res)));
    const r = await render(ApplicationDetailPanelComponent, {
      componentInputs: { communityId: 'c1', applicationId: 'a1' },
      providers: [{ provide: ArchitecturalService, useValue: arc }],
    });
    expect(screen.getByTestId('detail-skeleton')).toBeTruthy();
    expect(screen.getByTestId('vote-skeleton')).toBeTruthy();
    resolve(detail());
    await waitFor(() => expect(screen.getByRole('heading', { level: 2 })).toBeTruthy());
    expect(screen.queryByTestId('detail-skeleton')).toBeNull();

    // Switching to another application shows the skeletons again at once.
    arc.detail.and.returnValue(new Promise<ArcDetail>(() => {}));
    r.fixture.componentRef.setInput('applicationId', 'a2');
    r.fixture.detectChanges();
    await until(() => !!screen.queryByTestId('detail-skeleton'));
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
