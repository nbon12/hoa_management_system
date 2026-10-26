import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { ManagersMembershipsSectionComponent } from './managers-memberships.component';
import { CommunitySettingsContext, SettingsMode } from './community-settings-context';
import {
  CommunityInvitation, CommunitySettings, CommunitySettingsService, SettingsMembership,
} from '../../core/services/community-settings.service';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { PageNoticeService } from '../../core/services/page-notice.service';
import { CurrentUser } from '../../core/models';

// 030 T083 / US2 / brief 05: the Managers and memberships section of Community settings.

const TZ = 'America/New_York';

function membership(over: Partial<SettingsMembership> & { id: string; userDisplayName: string; role: string }): SettingsMembership {
  return {
    userId: `u-${over.id}`, status: 'Active', startDate: '2024-01-01', endDate: null, openEnded: true, effective: true,
    canEnd: true, canDowngrade: true, canEditEndDate: true, reasonCode: null, ...over,
  };
}

const LAST = { canEnd: false, canDowngrade: false, canEditEndDate: false, reasonCode: 'LAST_MANAGER' };
const JORDAN = membership({ id: 'm-jordan', userId: 'u-jordan', userDisplayName: 'Jordan Blake', role: 'CommunityManager', ...LAST });
const TAYLOR = membership({ id: 'm-taylor', userId: 'u-taylor', userDisplayName: 'Taylor Grant', role: 'CommunityManager', endDate: '2026-12-31', openEnded: false });
const LEE = membership({ id: 'm-lee', userId: 'u-lee', userDisplayName: 'Lee Morales', role: 'BoardMember', endDate: '2027-12-31', openEnded: false });
const AVERY = membership({ id: 'm-avery', userId: 'u-avery', userDisplayName: 'Avery Quinn', role: 'Accountant' });
const DANA = membership({ id: 'm-dana', userId: 'u-dana', userDisplayName: 'Dana Fox', role: 'BoardMember', endDate: '2026-06-30', openEnded: false, effective: false, status: 'Inactive', canEnd: false, canDowngrade: false, canEditEndDate: false });

function invitation(over: Partial<CommunityInvitation> & { id: string; email: string }): CommunityInvitation {
  return {
    kind: 'Membership', role: 'BoardMember', termEndDate: null, state: 'Pending', effectiveState: 'Pending', revokeReason: null,
    sentBy: 'Jordan Blake', sentAt: '2026-10-11T14:30:00Z', expiresAt: '2026-10-14T14:30:00Z', lastSendError: null,
    canResend: true, canRevoke: true, resendBlockedReason: null, ...over,
  };
}

const NEW = invitation({ id: 'i-new', email: 'new@example.com', termEndDate: '2027-12-31' });
const ALEX = invitation({
  id: 'i-alex', email: 'alex@example.com', role: 'CommunityManager', effectiveState: 'Expired',
  sentAt: '2026-10-05T15:00:00Z', expiresAt: '2026-10-08T15:00:00Z',
});
const BOOKS = invitation({ id: 'i-books', email: 'books@example.com', state: 'Revoked', effectiveState: 'Revoked', revokeReason: 'SenderLostAuthority', canResend: false, canRevoke: false });

function settings(over: Partial<CommunitySettings> = {}, can: Partial<CommunitySettings['can']> = {}): CommunitySettings {
  return {
    id: 'c-sh', communityName: 'Sakura Heights', legalName: 'Sakura Heights Homeowners Association, Inc.', status: 'Active',
    parent: null, noManager: null, missingRequiredFields: [], sections: ['profile', 'managers-and-memberships', 'architectural-review'],
    can: {
      editProfile: true, changeParent: false, activate: { allowed: false, reasonCode: null }, archive: { allowed: false, reasonCode: null },
      restore: { allowed: false, reasonCode: null }, manageManagers: true, replaceLastManager: false, manageMemberships: true,
      manageResidents: true, editArcSettings: true, endMembershipsWhileArchived: false, ...can,
    },
    ...over,
  };
}

function user(id: string, isCompanyAdministrator = false): CurrentUser {
  return {
    id, firstName: 'X', lastName: 'Y', email: `${id}@example.com`, initials: 'XY', lastActiveMode: 'Board', properties: [],
    isCompanyAdministrator, landing: 'Board',
    memberships: [{ communityId: 'c-sh', communityName: 'Sakura Heights', role: 'CommunityManager', communityStatus: 'Active' }],
  };
}

describe('ManagersMembershipsSectionComponent (030 T083)', () => {
  let svc: jasmine.SpyObj<CommunitySettingsService>;
  let ctx: CommunitySettingsContext;
  let refreshCurrentUser: jasmine.Spy;

  async function setup(opts: {
    memberships?: SettingsMembership[] | HttpErrorResponse; invitations?: CommunityInvitation[] | HttpErrorResponse;
    settings?: CommunitySettings; mode?: SettingsMode; me?: CurrentUser;
  } = {}) {
    svc = jasmine.createSpyObj<CommunitySettingsService>('CommunitySettingsService', [
      'getMemberships', 'getInvitations', 'getPeople', 'appoint', 'updateMembership', 'replaceManager', 'resendInvitation', 'revokeInvitation',
    ]);
    const m = opts.memberships ?? [JORDAN, TAYLOR, LEE, AVERY, DANA];
    if (m instanceof HttpErrorResponse) svc.getMemberships.and.rejectWith(m);
    else svc.getMemberships.and.resolveTo({ items: m, total: m.length, limit: 100, offset: 0 });
    const i = opts.invitations ?? [NEW, ALEX, BOOKS];
    if (i instanceof HttpErrorResponse) svc.getInvitations.and.rejectWith(i);
    else svc.getInvitations.and.resolveTo({ items: i, total: i.length, limit: 100, offset: 0, timeZoneId: TZ });
    svc.getPeople.and.resolveTo({
      items: [
        { userId: 'u-lee', name: 'Lee Morales', rolesHere: ['BoardMember'], residentOf: [] },
        { userId: 'u-taylor', name: 'Taylor Grant', rolesHere: ['CommunityManager'], residentOf: [] },
        { userId: 'u-sam', name: 'Sam Ortiz', rolesHere: [], residentOf: ['711 Keystone Park Dr #29'] },
      ],
      total: 3, limit: 25, offset: 0,
    });
    ctx = new CommunitySettingsContext();
    ctx.settings.set(opts.settings ?? settings());
    ctx.mode.set(opts.mode ?? 'board');
    spyOn(ctx, 'reload').and.resolveTo();
    const me = opts.me ?? user('u-jordan');
    refreshCurrentUser = jasmine.createSpy('refreshCurrentUser').and.resolveTo({ ...me, memberships: [] });
    const r = await render(ManagersMembershipsSectionComponent, {
      providers: [
        provideRouter([]),
        { provide: CommunitySettingsService, useValue: svc },
        { provide: CommunitySettingsContext, useValue: ctx },
        { provide: AuthService, useValue: { user: signal(me).asReadonly(), refreshCurrentUser } },
      ],
    });
    await r.fixture.whenStable();
    r.fixture.detectChanges();
    return r;
  }

  async function settle(r: Awaited<ReturnType<typeof setup>>) {
    await r.fixture.whenStable();
    r.fixture.detectChanges();
  }

  function table(name: string): HTMLElement {
    return screen.getByRole('table', { name });
  }

  function rowOf(tableName: string, text: string): HTMLElement {
    return within(table(tableName)).getAllByRole('row').find(row => row.textContent!.includes(text))!;
  }

  /** A row's data cells in column order (the row header is not a cell), whitespace collapsed. */
  function cellTexts(row: HTMLElement): string[] {
    return within(row).getAllByRole('cell').map(c => c.textContent!.replace(/\s+/g, ' ').trim());
  }

  async function openActions(r: Awaited<ReturnType<typeof setup>>, person: string) {
    fireEvent.click(screen.getByRole('button', { name: `Actions for ${person}` }));
    await settle(r);
    return screen.getByRole('menu', { name: `Actions for ${person}` });
  }

  function toasts(): string[] {
    return TestBed.inject(ToastService).toasts().map(t => t.message);
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  describe('lists', () => {
    it('lists managers with their terms, open-ended first, and the rule always shown', async () => {
      await setup();
      expect(screen.getByRole('heading', { level: 2, name: 'Managers and memberships' })).toBeTruthy();
      expect(screen.getByText('A community always needs at least one Community Manager with no end date. Managers with an end date don\'t count toward this.')).toBeTruthy();
      const managers = table('Community Managers of Sakura Heights');
      const rows = within(managers).getAllByRole('row').slice(1);
      expect(rows[0].textContent).toContain('Jordan Blake (you)');
      expect(rows[0].textContent).toContain('Open-ended');
      expect(rows[0].textContent).toContain('No end date');
      expect(rows[1].textContent).toContain('Taylor Grant');
      expect(rows[1].textContent).toContain('Ends Dec 31, 2026');
      expect(rows[1].textContent).toContain("Doesn't count toward the rule above");
    });

    it('lists board members and accountants, Board Members first, with ended rows under "Ended"', async () => {
      await setup();
      const board = table('Board members and accountants of Sakura Heights');
      const rows = within(board).getAllByRole('row').map(r => r.textContent!.replace(/\s+/g, ' ').trim());
      const lee = rows.findIndex(t => t.includes('Lee Morales'));
      const avery = rows.findIndex(t => t.includes('Avery Quinn'));
      const ended = rows.findIndex(t => t === 'Ended');
      const dana = rows.findIndex(t => t.includes('Dana Fox'));
      expect(lee).toBeLessThan(avery);
      expect(rows[lee]).toContain('Board Member');
      expect(rows[lee]).toContain('Ends Dec 31, 2027');
      expect(rows[avery]).toContain('Accountant');
      expect(rows[avery]).toContain('No end date');
      expect(ended).toBeGreaterThan(avery);
      expect(dana).toBeGreaterThan(ended);
      expect(rows[dana]).toContain('Ended Jun 30, 2026');
      expect(within(rowOf('Board members and accountants of Sakura Heights', 'Dana Fox')).queryByRole('button')).toBeNull();
    });

    // US5-S8: a pending Board Member invitation and an expired Community Manager invitation are both
    // listed with email, role, sent by, sent at and expires at, each with Resend and Revoke.
    it('lists role invitations with email, role, sender, times in the community zone and status, with Resend and Revoke', async () => {
      await setup();
      const invitations = table('Role invitations for Sakura Heights');
      expect(within(invitations).getAllByRole('columnheader').map(h => h.textContent!.trim()))
        .toEqual(['Email', 'Role', 'Sent by', 'Sent', 'Expires', 'Status', 'Actions']);
      // Cells are read by column (Role, Sent by, Sent, Expires, Status), so Sent and Expires can't be swapped.
      // The term end is the role cell's second line, after a <br />, so its text runs straight on.
      const pending = rowOf('Role invitations for Sakura Heights', 'new@example.com');
      expect(within(pending).getByRole('rowheader').textContent!.trim()).toBe('new@example.com');
      expect(cellTexts(pending).slice(0, 5)).toEqual([
        'Board Member' + 'Term ends Dec 31, 2027', 'Jordan Blake', 'Oct 11, 2026, 10:30 AM EDT', 'Oct 14, 2026, 10:30 AM EDT', 'Pending',
      ]);
      const expired = rowOf('Role invitations for Sakura Heights', 'alex@example.com');
      expect(within(expired).getByRole('rowheader').textContent!.trim()).toBe('alex@example.com');
      expect(cellTexts(expired).slice(0, 5)).toEqual([
        'Community Manager', 'Jordan Blake', 'Oct 5, 2026, 11:00 AM EDT', 'Oct 8, 2026, 11:00 AM EDT', 'Expired',
      ]);
      // Both rows offer a usable Resend (no disabled reason) and Revoke, and nothing else.
      for (const [row, email] of [[pending, 'new@example.com'], [expired, 'alex@example.com']] as const) {
        expect(within(row).getAllByRole('button').map(b => b.getAttribute('aria-label')))
          .toEqual([`Resend invitation to ${email}`, `Revoke invitation to ${email}`]);
        expect(within(row).getByRole('button', { name: `Resend invitation to ${email}` }).getAttribute('aria-disabled')).toBeNull();
      }
      const revoked = rowOf('Role invitations for Sakura Heights', 'books@example.com');
      expect(revoked.textContent).toContain('Revoked');
      expect(revoked.textContent).toContain("Sender's role ended");
      expect(within(revoked).queryByRole('button')).toBeNull();
      expect(svc.getInvitations).toHaveBeenCalledWith('c-sh', jasmine.objectContaining({ kind: 'membership' }));
    });

    it('says when there is no manager yet, with the pending invitation that does not count', async () => {
      await setup({
        settings: settings({ communityName: 'Keystone Park', status: 'Onboarding' }),
        memberships: [], invitations: [invitation({ id: 'i-pat', email: 'pat@example.com', role: 'CommunityManager' })],
      });
      expect(screen.getByText('Keystone Park has no Community Manager yet.')).toBeTruthy();
      expect(screen.getByText("Keystone Park can't be marked Active until it has a Community Manager with no end date.")).toBeTruthy();
      expect(screen.getByText("Invitation pending: pat@example.com. It doesn't count until it's accepted.")).toBeTruthy();
      expect(screen.getByText('No board members or accountants yet.')).toBeTruthy();
    });

    it('shows a load error in the area that failed, with Try again', async () => {
      const r = await setup({ memberships: new HttpErrorResponse({ status: 0 }), invitations: new HttpErrorResponse({ status: 0 }) });
      expect(screen.getByText("Couldn't load managers.")).toBeTruthy();
      expect(screen.getByText("Couldn't load board members and accountants.")).toBeTruthy();
      expect(screen.getByText("Couldn't load invitations.")).toBeTruthy();
      svc.getInvitations.and.resolveTo({ items: [NEW], total: 1, limit: 100, offset: 0, timeZoneId: TZ });
      fireEvent.click(within(screen.getByText("Couldn't load invitations.").parentElement!).getByRole('button', { name: 'Try again' }));
      await until(() => svc.getInvitations.calls.count() === 2);
      await settle(r);
      expect(table('Role invitations for Sakura Heights')).toBeTruthy();
    });
  });

  describe('row actions', () => {
    it('shows End and Downgrade disabled with the LAST_MANAGER reason on the only open-ended manager', async () => {
      const r = await setup({ me: user('u-taylor') });
      const menu = await openActions(r, 'Jordan Blake');
      const end = within(menu).getByRole('menuitem', { name: 'End membership' });
      expect(end.getAttribute('aria-disabled')).toBe('true');
      const tip = document.getElementById(end.getAttribute('aria-describedby')!)!;
      expect(tip.textContent!.trim()).toBe('Jordan Blake is the only Community Manager of Sakura Heights with no end date. A community must always have one. Appoint another manager first.');
      const downgrade = within(menu).getByRole('menuitem', { name: 'Downgrade to Board Member' });
      expect(downgrade.getAttribute('aria-disabled')).toBe('true');
      expect(document.getElementById(downgrade.getAttribute('aria-describedby')!)!.textContent)
        .toContain("Jordan Blake can't be downgraded.");
      fireEvent.click(end);
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
    });

    it('offers Replace manager only to a Company Administrator, on the only open-ended manager', async () => {
      let r = await setup({ mode: 'board' });
      let menu = await openActions(r, 'Jordan Blake');
      expect(within(menu).queryByRole('menuitem', { name: 'Replace manager' })).toBeNull();
      r.fixture.destroy();
      TestBed.resetTestingModule();

      r = await setup({ mode: 'company', me: user('u-robin', true), settings: settings({}, { replaceLastManager: true }) });
      menu = await openActions(r, 'Jordan Blake');
      expect(within(menu).getByRole('menuitem', { name: 'Replace manager' })).toBeTruthy();
      // The Company Administrator's refusal copy adds the way out.
      const end = within(menu).getByRole('menuitem', { name: 'End membership' });
      expect(document.getElementById(end.getAttribute('aria-describedby')!)!.textContent).toContain('Or use Replace manager.');
      fireEvent.keyDown(menu, { key: 'Escape' });
      await settle(r);
      menu = await openActions(r, 'Taylor Grant');
      expect(within(menu).queryByRole('menuitem', { name: 'Replace manager' })).toBeNull();
    });

    it('replaces the manager in one action, choosing from the people list', async () => {
      const r = await setup({ mode: 'company', me: user('u-robin', true), settings: settings({}, { replaceLastManager: true }) });
      svc.replaceManager.and.resolveTo({ ended: JORDAN, appointed: membership({ id: 'm-lee-cm', userId: 'u-lee', userDisplayName: 'Lee Morales', role: 'CommunityManager' }) });
      const menu = await openActions(r, 'Jordan Blake');
      fireEvent.click(within(menu).getByRole('menuitem', { name: 'Replace manager' }));
      await settle(r);
      const dialog = screen.getByRole('dialog', { name: 'Replace Jordan Blake as Community Manager of Sakura Heights?' });
      const picker = within(dialog).getByRole('combobox', { name: 'Replacement (required)' });
      fireEvent.input(picker, { target: { value: 'lee' } });
      await settle(r);
      fireEvent.click(within(dialog).getByRole('option', { name: /Lee Morales/ }));
      await settle(r);
      expect(within(dialog).getByText('Lee Morales becomes a Community Manager of Sakura Heights now, with no end date.')).toBeTruthy();
      expect(within(dialog).getByText('Both happen together. If one can\'t happen, neither does.')).toBeTruthy();
      fireEvent.click(within(dialog).getByRole('button', { name: 'Replace manager' }));
      await until(() => svc.replaceManager.calls.count() > 0);
      expect(svc.replaceManager).toHaveBeenCalledWith('c-sh', { currentManagerUserId: 'u-jordan', replacementUserId: 'u-lee' });
      await until(() => toasts().length > 0);
      expect(toasts()).toEqual(['Lee Morales replaced Jordan Blake as Community Manager of Sakura Heights.']);
      expect(ctx.reload).toHaveBeenCalled();
    });

    it('ends a co-manager after confirming', async () => {
      const r = await setup();
      svc.updateMembership.and.resolveTo({ ...TAYLOR, effective: false });
      const menu = await openActions(r, 'Taylor Grant');
      fireEvent.click(within(menu).getByRole('menuitem', { name: 'End membership' }));
      await settle(r);
      const dialog = screen.getByRole('dialog', { name: "End Taylor Grant's Community Manager membership?" });
      expect(within(dialog).getByText(/It ends today, .+\. It was set to end Dec 31, 2026\./)).toBeTruthy();
      expect(within(dialog).getByText('Sakura Heights still has a Community Manager with no end date: Jordan Blake (you).')).toBeTruthy();
      fireEvent.click(within(dialog).getByRole('button', { name: 'End membership' }));
      await until(() => toasts().length > 0);
      expect(svc.updateMembership).toHaveBeenCalledWith('c-sh', 'm-taylor', { end: true });
      expect(toasts()).toEqual(["Taylor Grant's Community Manager membership ended."]);
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
    });

    it('keeps the dialog open with the refusal at the top when a race is lost, and offers Close', async () => {
      const r = await setup();
      svc.updateMembership.and.rejectWith(new HttpErrorResponse({ status: 422, error: { code: 'LAST_MANAGER', message: 'x' } }));
      const menu = await openActions(r, 'Taylor Grant');
      fireEvent.click(within(menu).getByRole('menuitem', { name: 'End membership' }));
      await settle(r);
      const dialog = screen.getByRole('dialog');
      fireEvent.click(within(dialog).getByRole('button', { name: 'End membership' }));
      await until(() => !!within(dialog).queryByRole('alert'));
      await settle(r);
      expect(within(dialog).getByRole('alert').textContent).toContain('Taylor Grant is the only Community Manager of Sakura Heights with no end date.');
      expect(within(dialog).queryByRole('button', { name: 'End membership' })).toBeNull();
      fireEvent.click(within(dialog).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(svc.getMemberships.calls.count()).toBeGreaterThan(1);
    });

    it('downgrades a co-manager to Board Member', async () => {
      const r = await setup();
      svc.updateMembership.and.resolveTo(TAYLOR);
      const menu = await openActions(r, 'Taylor Grant');
      fireEvent.click(within(menu).getByRole('menuitem', { name: 'Downgrade to Board Member' }));
      await settle(r);
      const dialog = screen.getByRole('dialog', { name: 'Downgrade Taylor Grant to Board Member?' });
      expect(within(dialog).getByText(/Taylor Grant becomes a Board Member of Sakura Heights today, with no end date\./)).toBeTruthy();
      fireEvent.click(within(dialog).getByRole('button', { name: 'Downgrade to Board Member' }));
      await until(() => toasts().length > 0);
      expect(svc.updateMembership).toHaveBeenCalledWith('c-sh', 'm-taylor', { downgradeTo: 'BoardMember' });
      expect(toasts()).toEqual(['Taylor Grant is now a Board Member only.']);
    });

    it('changes, and clears, an end date', async () => {
      const r = await setup();
      svc.updateMembership.and.resolveTo(TAYLOR);
      let menu = await openActions(r, 'Taylor Grant');
      fireEvent.click(within(menu).getByRole('menuitem', { name: 'Change end date' }));
      await settle(r);
      let dialog = screen.getByRole('dialog', { name: 'Change end date' });
      expect(within(dialog).getByText('Now: ends Dec 31, 2026')).toBeTruthy();
      fireEvent.input(within(dialog).getByLabelText('End date (optional)'), { target: { value: '2099-11-30' } });
      fireEvent.click(within(dialog).getByRole('button', { name: 'Save end date' }));
      await until(() => toasts().length > 0);
      expect(svc.updateMembership).toHaveBeenCalledWith('c-sh', 'm-taylor', { endDate: '2099-11-30' });
      expect(toasts()).toEqual(["Taylor Grant's end date is now Nov 30, 2099."]);
      await settle(r);

      menu = await openActions(r, 'Taylor Grant');
      fireEvent.click(within(menu).getByRole('menuitem', { name: 'Change end date' }));
      await settle(r);
      dialog = screen.getByRole('dialog', { name: 'Change end date' });
      fireEvent.input(within(dialog).getByLabelText('End date (optional)'), { target: { value: '' } });
      fireEvent.click(within(dialog).getByRole('button', { name: 'Save end date' }));
      await until(() => toasts().length > 1);
      expect(svc.updateMembership.calls.mostRecent().args[2]).toEqual({ endDate: null });
      expect(toasts()[1]).toBe('Taylor Grant now has no end date.');
    });

    it('refuses an end date before today without asking the server', async () => {
      const r = await setup();
      const menu = await openActions(r, 'Lee Morales');
      fireEvent.click(within(menu).getByRole('menuitem', { name: 'Change end date' }));
      await settle(r);
      const dialog = screen.getByRole('dialog', { name: 'Change end date' });
      fireEvent.input(within(dialog).getByLabelText('End date (optional)'), { target: { value: '2001-01-01' } });
      fireEvent.click(within(dialog).getByRole('button', { name: 'Save end date' }));
      await settle(r);
      expect(within(dialog).getByText('Choose a date after today. To end a membership today, use End membership.')).toBeTruthy();
      expect(svc.updateMembership).not.toHaveBeenCalled();
    });

    it('after ending your own membership, goes to your landing with a notice', async () => {
      const own = membership({ id: 'm-jordan', userId: 'u-jordan', userDisplayName: 'Jordan Blake', role: 'CommunityManager' });
      const taylor = membership({ id: 'm-taylor', userId: 'u-taylor', userDisplayName: 'Taylor Grant', role: 'CommunityManager' });
      const r = await setup({ memberships: [own, taylor, LEE] });
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
      svc.updateMembership.and.resolveTo(own);
      const menu = await openActions(r, 'Jordan Blake');
      // Downgrading yourself would grant you a role, so it isn't offered without a Board Member membership (brief 05 Q7).
      expect(within(menu).queryByRole('menuitem', { name: 'Downgrade to Board Member' })).toBeNull();
      fireEvent.click(within(menu).getByRole('menuitem', { name: 'End membership' }));
      await settle(r);
      const dialog = screen.getByRole('dialog', { name: 'End your Community Manager membership in Sakura Heights?' });
      expect(within(dialog).getByText("You lose access to Sakura Heights' Community settings at once.")).toBeTruthy();
      expect(within(dialog).getByText('Sakura Heights still has a Community Manager with no end date: Taylor Grant.')).toBeTruthy();
      fireEvent.click(within(dialog).getByRole('button', { name: 'End my membership' }));
      await until(() => navigate.calls.count() > 0);
      expect(svc.updateMembership).toHaveBeenCalledWith('c-sh', 'm-jordan', { end: true });
      expect(refreshCurrentUser).toHaveBeenCalled();
      expect(TestBed.inject(PageNoticeService).message()).toBe("You're no longer a Community Manager of Sakura Heights.");
      TestBed.inject(PageNoticeService).dismiss();
    });
  });

  describe('invitations', () => {
    it('resends with no confirmation and says the old link no longer works', async () => {
      const r = await setup();
      svc.resendInvitation.and.resolveTo(ALEX);
      fireEvent.click(screen.getByRole('button', { name: 'Resend invitation to alex@example.com' }));
      await until(() => toasts().length > 0);
      expect(svc.resendInvitation).toHaveBeenCalledWith('c-sh', 'i-alex');
      expect(toasts()).toEqual(['Invitation sent again to alex@example.com. The earlier link no longer works.']);
      await settle(r);
    });

    it('tells the sender the list is up to date when the invitation was already accepted or revoked', async () => {
      await setup();
      svc.resendInvitation.and.rejectWith(new HttpErrorResponse({ status: 410, error: { code: 'INVITATION_INVALID', message: 'x' } }));
      fireEvent.click(screen.getByRole('button', { name: 'Resend invitation to alex@example.com' }));
      await until(() => toasts().length > 0);
      expect(toasts()).toEqual(['This invitation was already accepted or revoked. The list is now up to date.']);
      expect(svc.getInvitations.calls.count()).toBe(2);
    });

    it('shows the rate-limit message next to Resend', async () => {
      const r = await setup();
      svc.resendInvitation.and.rejectWith(new HttpErrorResponse({ status: 429, error: { code: 'RATE_LIMITED', message: 'x' } }));
      fireEvent.click(screen.getByRole('button', { name: 'Resend invitation to alex@example.com' }));
      await until(() => !!screen.queryByText('Too many requests. Wait a few minutes, then try again.'));
      await settle(r);
    });

    it('revokes after confirming', async () => {
      const r = await setup();
      svc.revokeInvitation.and.resolveTo({ ...NEW, state: 'Revoked', effectiveState: 'Revoked', revokeReason: 'Manual' });
      fireEvent.click(screen.getByRole('button', { name: 'Revoke invitation to new@example.com' }));
      await settle(r);
      const dialog = screen.getByRole('dialog', { name: 'Revoke the invitation to new@example.com?' });
      expect(within(dialog).getByText('The link in the invitation email stops working at once.')).toBeTruthy();
      expect(within(dialog).getByText("new@example.com can't use it to become a Board Member of Sakura Heights.")).toBeTruthy();
      fireEvent.click(within(dialog).getByRole('button', { name: 'Revoke invitation' }));
      await until(() => toasts().length > 0);
      expect(svc.revokeInvitation).toHaveBeenCalledWith('c-sh', 'i-new');
      expect(toasts()).toEqual(['Invitation to new@example.com revoked.']);
    });
  });

  describe('appoint', () => {
    async function openAppoint(r: Awaited<ReturnType<typeof setup>>, button: string) {
      fireEvent.click(screen.getByRole('button', { name: button }));
      await settle(r);
      return screen.getByRole('dialog');
    }

    it('appoints someone already in the community directly, from the people list', async () => {
      const r = await setup();
      svc.appoint.and.resolveTo(membership({ id: 'm-lee-cm', userId: 'u-lee', userDisplayName: 'Lee Morales', role: 'CommunityManager' }));
      const dialog = await openAppoint(r, 'Appoint a Community Manager for Sakura Heights');
      expect(within(dialog).getByRole('heading', { name: 'Appoint a Community Manager' })).toBeTruthy();
      expect((within(dialog).getByRole('radio', { name: /Choose someone already in Sakura Heights/ }) as HTMLInputElement).checked).toBeTrue();
      const picker = within(dialog).getByRole('combobox', { name: 'Person (required)' });
      fireEvent.focus(picker);
      fireEvent.keyDown(picker, { key: 'ArrowDown' });
      await settle(r);
      const options = within(dialog).getAllByRole('option').map(o => o.textContent!);
      // People who are already Community Managers aren't listed.
      expect(options.some(o => o.includes('Taylor Grant'))).toBeFalse();
      fireEvent.click(within(dialog).getByRole('option', { name: /Lee Morales/ }));
      await settle(r);
      expect(within(dialog).getByText('Lee Morales becomes a Community Manager of Sakura Heights now, with no end date. No invitation is sent.')).toBeTruthy();
      fireEvent.click(within(dialog).getByRole('button', { name: 'Appoint Lee Morales' }));
      await until(() => toasts().length > 0);
      expect(svc.appoint).toHaveBeenCalledWith('c-sh', jasmine.objectContaining({ role: 'CommunityManager', userId: 'u-lee', endDate: null }));
      expect(toasts()).toEqual(['Lee Morales is now a Community Manager of Sakura Heights.']);
    });

    it('asks for a person on the direct path', async () => {
      const r = await setup();
      const dialog = await openAppoint(r, 'Appoint a Community Manager for Sakura Heights');
      fireEvent.click(within(dialog).getByRole('button', { name: /^Appoint/ }));
      await settle(r);
      expect(within(dialog).getByText('Choose a person.')).toBeTruthy();
      expect(svc.appoint).not.toHaveBeenCalled();
    });

    it('invites by email, which shows only the email and "pending", never a name (FR-018)', async () => {
      const r = await setup();
      svc.appoint.and.resolveTo({ invitationId: 'i-pat', state: 'Pending', expiresAt: '2026-10-15T14:00:00Z' });
      const dialog = await openAppoint(r, 'Appoint a board member or accountant for Sakura Heights');
      fireEvent.click(within(dialog).getByRole('radio', { name: 'Accountant' }));
      fireEvent.click(within(dialog).getByRole('radio', { name: /Invite someone by email/ }));
      await settle(r);
      const email = within(dialog).getByLabelText('Email address (required)');
      expect(email.getAttribute('type')).toBe('email');
      fireEvent.input(email, { target: { value: 'not-an-email' } });
      fireEvent.click(within(dialog).getByRole('button', { name: 'Send invitation' }));
      await settle(r);
      expect(within(dialog).getByText('Enter a valid email address.')).toBeTruthy();

      fireEvent.input(email, { target: { value: 'pat@example.com' } });
      fireEvent.input(within(dialog).getByLabelText('Term end date (optional)'), { target: { value: '2099-12-31' } });
      await settle(r);
      expect(within(dialog).getByText(/We'll email pat@example\.com an invitation to be an Accountant of Sakura Heights, with a term ending Dec 31, 2099\./)).toBeTruthy();
      svc.getInvitations.and.resolveTo({
        items: [invitation({ id: 'i-pat', email: 'pat@example.com', role: 'Accountant' }), NEW], total: 2, limit: 100, offset: 0, timeZoneId: TZ,
      });
      fireEvent.click(within(dialog).getByRole('button', { name: 'Send invitation' }));
      await until(() => toasts().length > 0);
      expect(svc.appoint).toHaveBeenCalledWith('c-sh', { role: 'Accountant', email: 'pat@example.com', endDate: '2099-12-31' });
      expect(toasts()).toEqual(['Invitation sent to pat@example.com.']);
      await settle(r);
      const row = rowOf('Role invitations for Sakura Heights', 'pat@example.com');
      expect(row.textContent).toContain('Pending');
      expect(row.textContent).not.toContain('Pat');
    });

    it('shows INVITATION_PENDING under the email with "Resend invitation"', async () => {
      const r = await setup();
      svc.appoint.and.rejectWith(new HttpErrorResponse({ status: 409, error: { code: 'INVITATION_PENDING', message: 'x', invitationId: 'i-new' } }));
      svc.resendInvitation.and.resolveTo(NEW);
      const dialog = await openAppoint(r, 'Appoint a board member or accountant for Sakura Heights');
      fireEvent.click(within(dialog).getByRole('radio', { name: /Invite someone by email/ }));
      await settle(r);
      fireEvent.input(within(dialog).getByLabelText('Email address (required)'), { target: { value: 'new@example.com' } });
      fireEvent.click(within(dialog).getByRole('button', { name: 'Send invitation' }));
      await until(() => !!within(dialog).queryByText(/already has a pending Board Member invitation/));
      await settle(r);
      expect(within(dialog).getByText('new@example.com already has a pending Board Member invitation for Sakura Heights. Resend it instead?')).toBeTruthy();
      fireEvent.click(within(dialog).getByRole('button', { name: 'Resend invitation' }));
      await until(() => toasts().length > 0);
      expect(svc.resendInvitation).toHaveBeenCalledWith('c-sh', 'i-new');
      expect(toasts()).toEqual(['Invitation sent again to new@example.com. The earlier link no longer works.']);
    });

    it('shows SELF_GRANT_NOT_ALLOWED and ALREADY_MEMBER under the email field', async () => {
      const r = await setup();
      const dialog = await openAppoint(r, 'Appoint a Community Manager for Sakura Heights');
      fireEvent.click(within(dialog).getByRole('radio', { name: /Invite someone by email/ }));
      await settle(r);
      fireEvent.input(within(dialog).getByLabelText('Email address (required)'), { target: { value: 'jordan@example.com' } });
      svc.appoint.and.rejectWith(new HttpErrorResponse({ status: 422, error: { code: 'SELF_GRANT_NOT_ALLOWED', message: 'x' } }));
      fireEvent.click(within(dialog).getByRole('button', { name: 'Send invitation' }));
      await until(() => !!within(dialog).queryByText(/You can't give yourself a role or a home/));
      fireEvent.input(within(dialog).getByLabelText('Email address (required)'), { target: { value: 'taylor@example.com' } });
      svc.appoint.and.rejectWith(new HttpErrorResponse({ status: 409, error: { code: 'ALREADY_MEMBER', message: 'x' } }));
      fireEvent.click(within(dialog).getByRole('button', { name: 'Send invitation' }));
      await until(() => !!within(dialog).queryByText('taylor@example.com is already a Community Manager of Sakura Heights.'));
      await settle(r);
      const field = within(dialog).getByLabelText('Email address (required)');
      expect(field.getAttribute('aria-invalid')).toBe('true');
    });

    it('allows a manager end date only while another open-ended manager exists (FR-020)', async () => {
      let r = await setup();
      let dialog = await openAppoint(r, 'Appoint a Community Manager for Sakura Heights');
      expect(within(dialog).getByLabelText('End date (optional)')).toBeTruthy();
      r.fixture.destroy();
      TestBed.resetTestingModule();

      r = await setup({ settings: settings({ communityName: 'Keystone Park', status: 'Onboarding' }), memberships: [] });
      svc.getPeople.and.resolveTo({ items: [], total: 0, limit: 25, offset: 0 });
      dialog = await openAppoint(r, 'Appoint a Community Manager for Keystone Park');
      expect(within(dialog).queryByLabelText('End date (optional)')).toBeNull();
      expect(within(dialog).getByText("No end date. Keystone Park has no other Community Manager without an end date, so this one can't have one.")).toBeTruthy();
      expect(within(dialog).getByText('Keystone Park is Onboarding. Role invitations are still emailed now. Only resident invitations wait until it\'s marked Active.')).toBeTruthy();
    });

    it('offers only the email path when no one is in the community yet', async () => {
      const r = await setup({ settings: settings({ communityName: 'Keystone Park' }), memberships: [] });
      svc.getPeople.and.resolveTo({ items: [], total: 0, limit: 25, offset: 0 });
      const dialog = await openAppoint(r, 'Appoint a Community Manager for Keystone Park');
      await settle(r);
      const direct = within(dialog).getByRole('radio', { name: /Choose someone already in Keystone Park/ }) as HTMLInputElement;
      expect(direct.disabled).toBeTrue();
      expect(within(dialog).getByText('No one is in Keystone Park yet, so you can only invite by email.')).toBeTruthy();
      expect((within(dialog).getByRole('radio', { name: /Invite someone by email/ }) as HTMLInputElement).checked).toBeTrue();
    });

    it('Cancel closes the dialog and sends nothing', async () => {
      const r = await setup();
      const dialog = await openAppoint(r, 'Appoint a Community Manager for Sakura Heights');
      fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(svc.appoint).not.toHaveBeenCalled();
    });
  });

  describe('archived (read-only, FR-012)', () => {
    it('offers only End membership, with the archived helper, and no appoint, resend or revoke', async () => {
      await setup({
        mode: 'company', me: user('u-robin', true),
        settings: settings({ communityName: 'Old Mill Commons', status: 'Archived' }, {
          manageManagers: false, manageMemberships: false, endMembershipsWhileArchived: true,
        }),
        invitations: [{ ...BOOKS, revokeReason: 'CommunityArchived' }],
      });
      expect(screen.queryByRole('button', { name: /^Appoint/ })).toBeNull();
      expect(screen.queryByRole('button', { name: /^Actions for/ })).toBeNull();
      expect(screen.getByRole('button', { name: "End Jordan Blake's Community Manager membership" })).toBeTruthy();
      expect(screen.getByText(/The rule that a community needs a Community Manager with no end date doesn't apply while it's archived\./)).toBeTruthy();
      expect(screen.queryByText(/A community always needs at least one Community Manager/)).toBeNull();
      expect(rowOf('Role invitations for Old Mill Commons', 'books@example.com').textContent).toContain('Revoked — community archived');
      expect(screen.queryByRole('button', { name: /^Resend/ })).toBeNull();
    });
  });
});
