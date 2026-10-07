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

  async function setup(items: ArcListItem[] = [row(), row({ id: 'a2', displayId: 'ARC-1041', attachmentCount: 0 })], query: Record<string, string> = {}) {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService', ['list', 'vote', 'detail', 'attachmentUrl', 'requestInfo']);
    arc.list.and.returnValue(Promise.resolve(page(items)));
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
      .toEqual(['ID', 'Property', 'Project', 'Attachments', 'Due', 'Board votes', 'Your vote']);
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
    expect(within(first).getByRole('img', { name: '2 approve · 0 revisions needed · 0 deny · 3 not voted' })).toBeTruthy();
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

  // US2-S1: Approve, Revisions needed, Deny and Info render for CanVote.
  it('offers Approve, Revisions needed, Deny and Info when I can vote (US2-S1)', async () => {
    await setup([row()]);
    const r = screen.getAllByRole('row')[1];
    for (const name of ['Approve', 'Revisions needed', 'Deny', 'Info'])
      expect(within(r).getByRole('button', { name })).toBeTruthy();
  });

  // US2-S2: Approve → "you voted approve" and the new tally.
  it('votes Approve from the row and re-renders it (US2-S2)', async () => {
    await setup([row()]);
    arc.vote.and.returnValue(Promise.resolve(row({
      myVote: { state: 'Voted', choice: 'Approve' }, tally: { approve: 3, revisionsNeeded: 0, deny: 0, notVoted: 2, eligible: 5 },
    })));
    fireEvent.click(within(screen.getAllByRole('row')[1]).getByRole('button', { name: 'Approve' }));
    await waitFor(() => expect(screen.getByText('you voted approve')).toBeTruthy());
    expect(arc.vote).toHaveBeenCalledWith('c1', 'a1', 'Approve');
    expect(screen.getByRole('img', { name: '3 approve · 0 revisions needed · 0 deny · 2 not voted' })).toBeTruthy();
  });

  it('shows "you voted deny" with no buttons (US2-S3)', async () => {
    await setup([row({ myVote: { state: 'Voted', choice: 'Deny' } })]);
    const r = screen.getAllByRole('row')[1];
    expect(within(r).getByText('you voted deny')).toBeTruthy();
    expect(within(r).queryByRole('button', { name: 'Approve' })).toBeNull();
  });

  it('shows "you voted revisions needed" and the formal-denial note after voting (US2-S4)', async () => {
    await setup([row()]);
    arc.vote.and.returnValue(Promise.resolve(row({ myVote: { state: 'Voted', choice: 'RevisionsNeeded' } })));
    fireEvent.click(within(screen.getAllByRole('row')[1]).getByRole('button', { name: 'Revisions needed' }));
    await waitFor(() => expect(screen.getByText('you voted revisions needed')).toBeTruthy());
    expect(screen.getByText('Revisions needed counts as a formal denial; the owner is invited to revise and resubmit.')).toBeTruthy();
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
    ]);
    expect(screen.getByText('decision reached: approve')).toBeTruthy();
    expect(screen.getByText('decision reached: denied · revisions requested')).toBeTruthy();
    expect(screen.getByText('decision reached: denied')).toBeTruthy();
    expect(screen.getByText('Denied · revisions requested')).toBeTruthy();
    expect(screen.getByLabelText('version 2').textContent!.trim()).toBe('v2');
    expect(screen.getByText('overdue')).toBeTruthy();
    expect(screen.getByText('info requested')).toBeTruthy();
  });

  it('opens the application from ?open=', async () => {
    const r = await setup([row()], { open: 'a1' });
    expect(r.fixture.componentInstance.selectedId()).toBe('a1');
  });
});
