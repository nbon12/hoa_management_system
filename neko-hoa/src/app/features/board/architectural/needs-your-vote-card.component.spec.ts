import { signal } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/angular';
import { until } from './arc-testing';
import { NeedsYourVoteCardComponent } from './needs-your-vote-card.component';
import { ArcListItem, ArchitecturalService } from '../../../core/services/architectural.service';
import { AuthService } from '../../../core/services/auth.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';
import { CurrentUser } from '../../../core/models';

const ITEM: ArcListItem = {
  id: 'a1', displayId: 'ARC-1042', revision: 1, propertyAddress: '711 Keystone Park Dr #29', ownerName: 'Praneeth Pattyam',
  projectTitle: 'Fence replacement — 6ft cedar', attachmentCount: 3, receivedDate: '2026-05-28', dueDate: '2026-06-27',
  overdue: false, status: 'Open', decision: null, infoRequested: false,
  tally: { approve: 2, revisionsNeeded: 0, deny: 0, notVoted: 3, eligible: 5 }, myVote: { state: 'CanVote' },
};

describe('NeedsYourVoteCardComponent (027 US5)', () => {
  let arc: jasmine.SpyObj<ArchitecturalService>;
  const user = signal<CurrentUser | null>({
    id: 'u1', firstName: 'B', lastName: 'B', email: 'b@b.dev', initials: 'BB', lastActiveMode: 'Board',
    memberships: [{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }],
  });

  async function setup(items: ArcListItem[]) {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService', ['list', 'vote']);
    arc.list.and.returnValue(Promise.resolve({ items, total: items.length, limit: 25, offset: 0,
      counts: { open: items.length, closed: 0, awaitingMyVote: items.length } }));
    await render(NeedsYourVoteCardComponent, {
      providers: [
        provideRouter([]),
        { provide: ArchitecturalService, useValue: arc },
        { provide: AuthService, useValue: { user: user.asReadonly() } },
        { provide: BoardNavigationService, useValue: { activeCommunityId: signal<string | null>(null).asReadonly() } },
      ],
    });
    await until(() => arc.list.calls.count() > 0);
    expect(arc.list).toHaveBeenCalledWith('c1', { awaitingMyVote: true, limit: 25 });
  }

  // US5-S1: "1 open" and the row with ID, project, address · owner, attachments, tally, due and the buttons.
  it('lists what needs my vote with all FR-032 fields (US5-S1)', async () => {
    await setup([ITEM]);
    await waitFor(() => expect(screen.getByText('1 open')).toBeTruthy());
    expect(screen.getByRole('heading', { name: 'Needs your vote' })).toBeTruthy();
    const rowEl = screen.getByText('ARC-1042').closest('li')!;
    expect(within(rowEl).getByText('Fence replacement — 6ft cedar')).toBeTruthy();
    expect(rowEl.textContent).toContain('711 Keystone Park Dr #29 · Praneeth Pattyam');
    expect(rowEl.textContent).toContain('📎 3');
    expect(rowEl.textContent).toContain('due 06/27/26');
    expect(within(rowEl).getByRole('img', { name: '2 approve · 0 request revisions · 0 deny · 3 not voted' })).toBeTruthy();
    // 031 item 3: nothing is pre-selected and no Save shows until a choice is picked.
    for (const name of ['Approve', 'Request revisions', 'Deny'])
      expect(within(rowEl).getByRole('button', { name }).getAttribute('aria-pressed')).toBe('false');
    expect(within(rowEl).queryByRole('button', { name: 'Save' })).toBeNull();
  });

  // 031 US1-S8 (item 2): "Given the Needs your vote card, When I click a row, Then I am taken to that application."
  it('opens the application when a row is clicked (031 item 2)', async () => {
    await setup([ITEM]);
    await until(() => !!screen.queryByText('ARC-1042'));
    const router = TestBed.inject(Router);
    const nav = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    fireEvent.click(screen.getByText('Fence replacement — 6ft cedar'));
    expect(nav).toHaveBeenCalledWith(['/app/board/architectural'], { queryParams: { open: 'a1' } });
    nav.calls.reset();
    fireEvent.click(screen.getByRole('button', { name: 'Deny' }));
    expect(nav).not.toHaveBeenCalled();
    expect(screen.getByRole('link', { name: 'Open ARC-1042' }).getAttribute('href')).toBe('/app/board/architectural?open=a1');
  });

  it('links to all architectural applications (US5-S2)', async () => {
    await setup([ITEM]);
    const link = screen.getByRole('link', { name: 'All architectural applications →' });
    expect(link.getAttribute('href')).toBe('/app/board/architectural');
  });

  // US5-S3 / 031 US1-S4: pick Approve, then Save; the vote is saved and the row leaves the card.
  it('approves with Approve then Save and removes the row (US5-S3, 031 item 3)', async () => {
    await setup([ITEM]);
    arc.vote.and.returnValue(Promise.resolve({ ...ITEM, myVote: { state: 'Voted', choice: 'Approve' } }));
    await waitFor(() => expect(screen.getByText('ARC-1042')).toBeTruthy());
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
    expect(arc.vote).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await until(() => screen.queryByText('ARC-1042') === null);
    expect(arc.vote).toHaveBeenCalledWith('c1', 'a1', 'Approve');
  });

  it('shows an empty state when nothing needs my vote (US5-S4)', async () => {
    await setup([]);
    await waitFor(() => expect(screen.getByText('Nothing needs your vote right now.')).toBeTruthy());
  });

  it('shows the server message and reloads when a vote fails', async () => {
    await setup([ITEM]);
    await until(() => !!screen.queryByText('ARC-1042'));
    arc.vote.and.returnValue(Promise.reject({ error: { message: 'A decision has already been reached on this application.' } }));
    fireEvent.click(screen.getByRole('button', { name: 'Deny' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await until(() => !!screen.queryByRole('alert')?.textContent?.includes('decision has already been reached'));
    expect(arc.list.calls.count()).toBeGreaterThan(1);
  });

  it('shows an error when the feed cannot be loaded', async () => {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService', ['list', 'vote']);
    arc.list.and.returnValue(Promise.reject({ status: 500 }));
    await render(NeedsYourVoteCardComponent, {
      providers: [
        provideRouter([]),
        { provide: ArchitecturalService, useValue: arc },
        { provide: AuthService, useValue: { user: user.asReadonly() } },
        { provide: BoardNavigationService, useValue: { activeCommunityId: signal<string | null>(null).asReadonly() } },
      ],
    });
    await until(() => !!screen.queryByRole('alert')?.textContent?.includes('could not be loaded'));
  });
});
