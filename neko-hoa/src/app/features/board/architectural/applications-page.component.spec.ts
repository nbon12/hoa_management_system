import { signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/angular';
import { until } from './arc-testing';
import { ApplicationsPageComponent } from './applications-page.component';
import { ArcListItem, ArcListResponse, ArchitecturalService } from '../../../core/services/architectural.service';
import { AuthService } from '../../../core/services/auth.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';
import { CurrentUser } from '../../../core/models';

function row(over: Partial<ArcListItem> = {}): ArcListItem {
  return {
    id: 'a1', displayId: 'ARC-1042', revision: 1, propertyAddress: '711 Keystone Park Dr #29', ownerName: 'Praneeth Pattyam',
    projectTitle: 'Fence replacement — 6ft cedar', attachmentCount: 3, receivedDate: '2026-05-28', dueDate: '2026-06-27',
    overdue: false, status: 'Open', decision: null, infoRequested: false,
    tally: { approve: 2, revisionsNeeded: 0, deny: 0, notVoted: 3, eligible: 5 }, myVote: { state: 'CanVote' }, ...over,
  };
}

function page(items: ArcListItem[], counts = { open: 4, closed: 27, awaitingMyVote: 1 }): ArcListResponse {
  return { items, total: items.length, limit: 25, offset: 0, counts };
}

describe('ApplicationsPageComponent (027 US1/US2)', () => {
  let arc: jasmine.SpyObj<ArchitecturalService>;
  const active = signal<string | null>('c1');
  const user = signal<CurrentUser | null>({
    id: 'u1', firstName: 'Bianca', lastName: 'Board', email: 'board@nekohoa.dev', initials: 'BB', lastActiveMode: 'Board',
    memberships: [
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
      { communityId: 'c2', communityName: 'Two', role: 'BoardMember' },
    ],
  });

  async function setup(items: ArcListItem[] = [row(), row({ id: 'a2', displayId: 'ARC-1041', attachmentCount: 0 })], query: Record<string, string> = {},
                       counts = { open: 4, closed: 27, awaitingMyVote: 1 }) {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService', ['list', 'vote', 'undoVote', 'detail', 'attachmentUrl', 'requestInfo']);
    arc.list.and.returnValue(Promise.resolve(page(items, counts)));
    arc.detail.and.returnValue(new Promise(() => {}));
    active.set('c1');
    const result = await render(ApplicationsPageComponent, {
      providers: [
        { provide: ArchitecturalService, useValue: arc },
        { provide: AuthService, useValue: { user: user.asReadonly() } },
        { provide: BoardNavigationService, useValue: { activeCommunityId: active.asReadonly() } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) } } },
      ],
    });
    await until(() => arc.list.calls.count() > 0);
    await result.fixture.whenStable();
    result.fixture.detectChanges();
    return result;
  }

  it('renders the heading and the column headers in FR-002 order (US1-S4)', async () => {
    await setup();
    expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Architectural');
    expect(screen.getAllByRole('columnheader').map(h => h.textContent!.trim()))
      .toEqual(['ID', 'Property', 'Project', 'Attachments', 'Due', 'Board votes', 'Your vote', 'Final decision']);
  });

  // US1-S1: tabs read "Open · 4" and "Closed · 27", Open selected, the open rows listed.
  it('shows Open · 4 / Closed · 27 with Open selected (US1-S1)', async () => {
    await setup();
    const open = screen.getByRole('tab', { name: 'Open · 4' });
    expect(open.getAttribute('aria-selected')).toBe('true');
    expect(screen.getByRole('tab', { name: 'Closed · 27' }).getAttribute('aria-selected')).toBe('false');
    expect(arc.list).toHaveBeenCalledWith('c1', jasmine.objectContaining({ status: 'open' }));
    expect(screen.getAllByRole('row').length).toBe(3); // header + 2
  });

  it('requests status=closed when the Closed tab is selected (US1-S2)', async () => {
    await setup();
    fireEvent.click(screen.getByRole('tab', { name: 'Closed · 27' }));
    await until(() => arc.list.calls.allArgs().some(a => a[0] === 'c1' && a[1]?.status === 'closed'));
  });

  it('searches by address or owner (US1-S3)', async () => {
    await setup();
    const box = screen.getByRole('searchbox', { name: 'Search address or owner' });
    fireEvent.input(box, { target: { value: 'Pattyam' } });
    await until(() => arc.list.calls.allArgs().some(a => a[0] === 'c1' && a[1]?.search === 'Pattyam'));
  });

  it('renders ID, address with owner, project, attachments, due and tally (US1-S4)', async () => {
    await setup();
    const first = screen.getAllByRole('row')[1];
    expect(within(first).getByText('ARC-1042')).toBeTruthy();
    expect(within(first).getByText('711 Keystone Park Dr #29')).toBeTruthy();
    expect(within(first).getByText('Praneeth Pattyam')).toBeTruthy();
    expect(first.textContent).toContain('📎 3 files');
    expect(first.textContent).toContain('06/27/26');
    expect(within(first).getByRole('img', { name: '2 approve · 0 request revisions · 0 deny · 3 not voted' })).toBeTruthy();
    expect(screen.getAllByRole('row')[2].textContent).toContain('none');
  });

  it('shows "1 awaiting your vote" and hides the pill at 0 (US1-S6)', async () => {
    await setup();
    expect(screen.getByText('1 awaiting your vote')).toBeTruthy();
    arc.list.and.returnValue(Promise.resolve(page([], { open: 0, closed: 0, awaitingMyVote: 0 })));
    fireEvent.click(screen.getByRole('tab', { name: 'Closed · 27' }));
    await until(() => screen.queryByText(/awaiting your vote/) === null);
  });

  it('names the search term when nothing matches', async () => {
    const r = await setup([]);
    r.fixture.componentInstance.search.set('Zzz');
    r.fixture.detectChanges();
    expect(screen.getByText('No applications match “Zzz”.')).toBeTruthy();
  });

  // Edge case "board member of several communities": only the active community is requested.
  it('requests only the active community and re-requests when it changes', async () => {
    const r = await setup();
    expect(arc.list.calls.mostRecent().args[0]).toBe('c1');
    active.set('c2');
    r.fixture.detectChanges();
    await until(() => arc.list.calls.mostRecent().args[0] === 'c2');
  });

  // US2-S1 (031 wording): Approve, Request revisions, Deny and Ask question render for CanVote; nothing is selected.
  it('offers Approve, Request revisions, Deny and Ask question when I can vote (US2-S1)', async () => {
    await setup([row()]);
    const r = screen.getAllByRole('row')[1];
    for (const name of ['Approve', 'Request revisions', 'Deny', 'Ask question'])
      expect(within(r).getByRole('button', { name })).toBeTruthy();
    expect(within(r).getByRole('button', { name: 'Approve' }).getAttribute('aria-pressed')).toBe('false');
    expect(within(r).queryByRole('button', { name: 'Submit' })).toBeNull();
  });

  // 031 US1-S1: "Given the applications table, When I click the whitespace of a row, Then that application opens."
  it('opens an application when its row is clicked (031 item 1)', async () => {
    const r = await setup([row(), row({ id: 'a2', displayId: 'ARC-1041' })]);
    fireEvent.click(within(screen.getAllByRole('row')[2]).getByText('711 Keystone Park Dr #29'));
    expect(r.fixture.componentInstance.selectedId()).toBe('a2');
    // A click on a control inside the row keeps the control's own action.
    r.fixture.componentInstance.selectedId.set(null);
    fireEvent.click(within(screen.getAllByRole('row')[1]).getByRole('button', { name: 'Approve' }));
    expect(r.fixture.componentInstance.selectedId()).toBeNull();
  });

  // 031 US1-S2: "When I pick Deny on a row, Then nothing is cast until I click Submit."
  it('casts a row vote only after Submit (031 item 5)', async () => {
    await setup([row()]);
    const r = screen.getAllByRole('row')[1];
    fireEvent.click(within(r).getByRole('button', { name: 'Deny' }));
    expect(arc.vote).not.toHaveBeenCalled();
    arc.vote.and.returnValue(Promise.resolve(row({ myVote: { state: 'Voted', choice: 'Deny' }, canUndoVote: true })));
    fireEvent.click(within(r).getByRole('button', { name: 'Submit' }));
    await waitFor(() => expect(screen.getByText('you voted deny')).toBeTruthy());
    expect(arc.vote).toHaveBeenCalledWith('c1', 'a1', 'Deny');
  });

  // 031 US3-S1: "Given I voted and the vote can still change, When the row renders, Then an undo link follows my vote;
  // clicking it removes my vote and offers the choices again."
  it('offers undo while my vote can change and recasts after it (031 item 5)', async () => {
    await setup([row({ myVote: { state: 'Voted', choice: 'Approve' }, canUndoVote: true,
      status: 'DecisionReached', decision: { outcome: 'Approved', wording: null, source: 'Votes' },
      votesLockAt: '2026-06-20T16:00:00Z' })]);
    const r = screen.getAllByRole('row')[1];
    expect(within(r).getByText('you voted approve')).toBeTruthy();
    expect(r.textContent).toContain('until 06/20/26');
    arc.undoVote.and.returnValue(Promise.resolve(row()));
    fireEvent.click(within(r).getByRole('button', { name: 'Undo your vote on ARC-1042' }));
    await waitFor(() => expect(within(screen.getAllByRole('row')[1]).getByRole('button', { name: 'Approve' })).toBeTruthy());
    expect(arc.undoVote).toHaveBeenCalledWith('c1', 'a1');
  });

  // 031 US1-S6: "Given finalized decisions, Then the Final decision column shows ✓ / ✕ / yellow ↻ and no undo link."
  it('shows the final-decision mark instead of undo once final (031 item 8)', async () => {
    await setup([
      row({ id: 'f1', status: 'Closed', decisionFinal: true, myVote: { state: 'Voted', choice: 'Approve' },
        decision: { outcome: 'Approved', wording: null, source: 'Votes' } }),
      row({ id: 'f2', status: 'Closed', decisionFinal: true, decision: { outcome: 'Denied', wording: 'Denied', source: 'Votes' } }),
      row({ id: 'f3', status: 'DecisionReached', decisionFinal: true,
        decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' } }),
    ]);
    expect(screen.getByRole('img', { name: 'Final decision: Application Approved' })).toBeTruthy();
    expect(screen.getByRole('img', { name: 'Final decision: Denied' })).toBeTruthy();
    expect(screen.getByRole('img', { name: /Final decision: Revisions Requested/ })).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Undo your vote/ })).toBeNull();
  });

  // 031 US1-S7 (item 7): "Given 1 application awaits my vote, When I vote on it from the detail panel, Then the header
  // pill disappears" — counts are re-read from the server after every change.
  it('refreshes "awaiting your vote" after a change from the panel (031 item 7)', async () => {
    const r = await setup([row()], {}, { open: 4, closed: 27, awaitingMyVote: 1 });
    expect(screen.getByText('1 awaiting your vote')).toBeTruthy();
    arc.list.and.returnValue(Promise.resolve(page([], { open: 4, closed: 27, awaitingMyVote: 0 })));
    r.fixture.componentInstance.changed(row({ myVote: { state: 'Voted', choice: 'Approve' } }));
    await until(() => screen.queryByText(/awaiting your vote/) === null);
    expect(arc.list).toHaveBeenCalledWith('c1', jasmine.objectContaining({ awaitingMyVote: true }));
  });

  // US2-S2: from 1 approve / 0 deny / 4 not voted, Approve → "you voted approve" and 2 / 0 / 3.
  // US2 Independent Test: "…and the "awaiting your vote" count went down by one."
  it('votes Approve from the row and re-renders it (US2-S2)', async () => {
    await setup([row({ tally: { approve: 1, revisionsNeeded: 0, deny: 0, notVoted: 4, eligible: 5 } })], {},
      { open: 4, closed: 27, awaitingMyVote: 2 });
    expect(screen.getByText('2 awaiting your vote')).toBeTruthy();
    arc.vote.and.returnValue(Promise.resolve(row({
      myVote: { state: 'Voted', choice: 'Approve' }, tally: { approve: 2, revisionsNeeded: 0, deny: 0, notVoted: 3, eligible: 5 },
    })));
    arc.list.and.returnValue(Promise.resolve(page([], { open: 4, closed: 27, awaitingMyVote: 1 })));
    fireEvent.click(within(screen.getAllByRole('row')[1]).getByRole('button', { name: 'Approve' }));
    fireEvent.click(within(screen.getAllByRole('row')[1]).getByRole('button', { name: 'Submit' }));
    await waitFor(() => expect(screen.getByText('you voted approve')).toBeTruthy());
    expect(arc.vote).toHaveBeenCalledWith('c1', 'a1', 'Approve');
    expect(screen.getByRole('img', { name: '2 approve · 0 request revisions · 0 deny · 3 not voted' })).toBeTruthy();
    await until(() => !!screen.queryByText('1 awaiting your vote'));
  });

  it('shows "you voted deny" with no buttons (US2-S3)', async () => {
    await setup([row({ myVote: { state: 'Voted', choice: 'Deny' } })]);
    const r = screen.getAllByRole('row')[1];
    expect(within(r).getByText('you voted deny')).toBeTruthy();
    expect(within(r).queryByRole('button', { name: 'Approve' })).toBeNull();
  });

  it('shows "you voted to request revisions" and the formal-denial note after voting (US2-S4)', async () => {
    await setup([row()]);
    arc.vote.and.returnValue(Promise.resolve(row({ myVote: { state: 'Voted', choice: 'RevisionsNeeded' } })));
    fireEvent.click(within(screen.getAllByRole('row')[1]).getByRole('button', { name: 'Request revisions' }));
    fireEvent.click(within(screen.getAllByRole('row')[1]).getByRole('button', { name: 'Submit' }));
    await waitFor(() => expect(screen.getByText('you voted to request revisions')).toBeTruthy());
    expect(screen.getByText('Request revisions counts as a formal denial; the owner is invited to revise and resubmit.')).toBeTruthy();
  });

  it('shows recused instead of vote buttons (US2-S8)', async () => {
    await setup([row({ myVote: { state: 'Recused' } })]);
    const r = screen.getAllByRole('row')[1];
    expect(within(r).getByText('recused')).toBeTruthy();
    expect(within(r).queryByRole('button', { name: 'Approve' })).toBeNull();
  });

  // US6: decision labels, closed wording, v2 badge, overdue and info-requested markers.
  it('labels decisions, the v2 badge, overdue and info requested', async () => {
    await setup([
      row({ id: 'd1', status: 'DecisionReached', decision: { outcome: 'Approved', wording: null, source: 'Votes' } }),
      row({ id: 'd2', status: 'DecisionReached', decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' } }),
      row({ id: 'd3', status: 'DecisionReached', decision: { outcome: 'Denied', wording: 'Denied', source: 'Votes' } }),
      row({ id: 'c1', status: 'Closed', decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' } }),
      row({ id: 'v2', revision: 2, overdue: true, infoRequested: true }),
      row({ id: 'l1', status: 'DecisionReached', overdue: true, decision: { outcome: 'Approved', wording: null, source: 'Lapse' } }),
      row({ id: 'l2', status: 'DecisionReached', overdue: true, decision: { outcome: 'Denied', wording: 'Denied', source: 'Lapse' } }),
    ]);
    expect(screen.getByText('decision reached: approved by default (review period lapsed)')).toBeTruthy();
    expect(screen.getByText('decision reached: denied by default (review period lapsed)')).toBeTruthy();
    expect(screen.getByText('decision reached: approve')).toBeTruthy();
    expect(screen.getByText('decision reached: denied · revisions requested')).toBeTruthy();
    expect(screen.getByText('decision reached: denied')).toBeTruthy();
    expect(screen.getByText('Denied · revisions requested')).toBeTruthy();
    expect(screen.getByLabelText('version 2').textContent!.trim()).toBe('v2');
    expect(screen.getAllByText('overdue').length).toBe(3);
    expect(screen.getByText('info requested')).toBeTruthy();
  });

  it('opens the application from ?open=', async () => {
    const r = await setup([row()], { open: 'a1' });
    expect(r.fixture.componentInstance.selectedId()).toBe('a1');
  });

  it('shows the server message and reloads when a vote fails', async () => {
    await setup([row()]);
    arc.vote.and.returnValue(Promise.reject({ error: { message: 'You have already voted on this application.' } }));
    const before = arc.list.calls.count();
    fireEvent.click(within(screen.getAllByRole('row')[1]).getByRole('button', { name: 'Deny' }));
    fireEvent.click(within(screen.getAllByRole('row')[1]).getByRole('button', { name: 'Submit' }));
    await until(() => !!screen.queryByRole('alert')?.textContent?.includes('already voted'));
    expect(arc.list.calls.count()).toBeGreaterThan(before);
  });

  it('shows an error when the list cannot be loaded', async () => {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService', ['list']);
    arc.list.and.returnValue(Promise.reject({ status: 500 }));
    await render(ApplicationsPageComponent, {
      providers: [
        { provide: ArchitecturalService, useValue: arc },
        { provide: AuthService, useValue: { user: user.asReadonly() } },
        { provide: BoardNavigationService, useValue: { activeCommunityId: signal<string | null>('c1').asReadonly() } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      ],
    });
    await until(() => !!screen.queryByRole('alert')?.textContent?.includes('could not be loaded'));
  });

  it('opens the detail panel from the ID and from Ask question (031 item 11)', async () => {
    const r = await setup([row()]);
    fireEvent.click(screen.getByRole('button', { name: 'ARC-1042' }));
    expect(r.fixture.componentInstance.selectedId()).toBe('a1');
    r.fixture.componentInstance.selectedId.set(null);
    fireEvent.click(screen.getByRole('button', { name: 'Ask question' }));
    expect(r.fixture.componentInstance.selectedId()).toBe('a1');
    expect(r.fixture.componentInstance.infoFor()).toBe('a1');
    fireEvent.click(screen.getByRole('button', { name: '📎 3 files' }));
    r.fixture.destroy();
  });

  it('asks for a community when none is active', async () => {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService', ['list']);
    await render(ApplicationsPageComponent, {
      providers: [
        { provide: ArchitecturalService, useValue: arc },
        { provide: AuthService, useValue: { user: signal<CurrentUser | null>(null).asReadonly() } },
        { provide: BoardNavigationService, useValue: { activeCommunityId: signal<string | null>(null).asReadonly() } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      ],
    });
    expect(screen.getByText('No active community selected.')).toBeTruthy();
    expect(arc.list).not.toHaveBeenCalled();
  });

  // 029 (Clarifications 2026-10-08): withdrawn requests are hidden from the Closed tab unless the board
  // turns on "Show withdrawn"; the toggle only exists on the Closed tab.
  it('offers a Show withdrawn toggle on the Closed tab that adds includeWithdrawn (029)', async () => {
    await setup();
    expect(screen.queryByLabelText('Show withdrawn')).toBeNull();
    fireEvent.click(screen.getByRole('tab', { name: /Closed/ }));
    await until(() => !!screen.queryByLabelText('Show withdrawn'));
    expect(arc.list.calls.mostRecent().args[1]).toEqual(jasmine.objectContaining({ status: 'closed', includeWithdrawn: undefined }));

    fireEvent.click(screen.getByLabelText('Show withdrawn'));
    await until(() => arc.list.calls.mostRecent().args[1]?.includeWithdrawn === true);
    expect(arc.list.calls.mostRecent().args[1]).toEqual(jasmine.objectContaining({ status: 'closed', includeWithdrawn: true }));
  });
});
