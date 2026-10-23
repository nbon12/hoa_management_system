import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { until } from '../../board/architectural/arc-testing';
import { ResidentsSectionComponent } from './residents-section.component';
import { CommunitySettingsContext, SettingsMode } from '../community-settings-context';
import {
  CommunitySettings, CommunitySettingsService, PropertyInvitation, ResidentProperty, ResidentRow, ResidentsPage,
} from '../../../core/services/community-settings.service';
import { ToastService } from '../../../core/services/toast.service';
import { ViewportService } from '../../../core/services/viewport.service';
import { signal } from '@angular/core';
import { formatDateTimeInZone } from '../../../shared/date-format';

// 030 T116 / US8 / brief 06: the Residents area of Managers and memberships. Rows are short; every
// action is in the property drawer. Invitations by email only; removal takes effect at once.

const TZ = 'America/New_York';
const NONE = { allowed: false, reasonCode: null };

function settings(over: Partial<CommunitySettings> = {}): CommunitySettings {
  return {
    id: 'c-sh', communityName: 'Sakura Heights', legalName: 'Sakura Heights Homeowners Association, Inc.', status: 'Active',
    parent: null, noManager: false, missingRequiredFields: [], sections: ['profile', 'managers-and-memberships', 'architectural-review'],
    can: {
      editProfile: true, changeParent: false, activate: NONE, archive: NONE, restore: NONE, manageManagers: true,
      replaceLastManager: false, manageMemberships: true, manageResidents: true, editArcSettings: true, endMembershipsWhileArchived: false,
    },
    ...over,
  };
}

function row(propertyId: string, address: string, residentNames: string[], inv: Partial<ResidentRow['invitationSummary']> = {}, matchedEmail: string | null = null): ResidentRow {
  return { propertyId, address, residentNames, matchedEmail, invitationSummary: { scheduled: 0, pending: 0, expired: 0, ...inv } };
}

const R29 = row('p29', '711 Keystone Park Dr #29', ['Jane Alvarez', 'Sam Ortiz'], { pending: 1 });
const R31 = row('p31', '715 Keystone Park Dr #31', [], { expired: 1 });
const R35 = row('p35', '719 Keystone Park Dr #35', [], { pending: 1 });
const FILLER = Array.from({ length: 22 }, (_, i) => row(`pf${i}`, `${801 + i} Keystone Park Dr`, [`Filler ${i}`]));
const BASELINE = [R29, R31, R35, ...FILLER];

function page(items: ResidentRow[], over: Partial<ResidentsPage> = {}): ResidentsPage {
  return {
    items, total: over.total ?? 120, limit: 25, offset: 0,
    counts: { all: 120, noResident: 7, invitationPending: 2, invitationExpired: 1 }, propertiesLoaded: true, ...over,
  };
}

function invitation(over: Partial<PropertyInvitation> & { id: string; email: string }): PropertyInvitation {
  return {
    state: 'Pending', effectiveState: 'Pending', sentBy: 'Jordan Blake', sentAt: '2026-10-10T20:40:00Z', expiresAt: '2099-10-13T20:40:00Z',
    revokeReason: null, canResend: true, canRevoke: true, resendBlockedReason: null, ...over,
  };
}

const KIM = invitation({ id: 'i-kim', email: 'kim@example.com' });
const OLD = invitation({ id: 'i-old', email: 'old@example.com', state: 'Revoked', effectiveState: 'Revoked', revokeReason: 'Manual', sentAt: '2026-10-11T20:40:00Z', canResend: false, canRevoke: false });

function property(over: Partial<ResidentProperty> = {}): ResidentProperty {
  return {
    propertyId: 'p29', address: '711 Keystone Park Dr #29',
    residents: [
      { userId: 'u-jane', name: 'Jane Alvarez', email: 'jane@example.com', residentSince: '2024-03-02' },
      { userId: 'u-sam', name: 'Sam Ortiz', email: 'sam@example.com', residentSince: '2026-10-09' },
    ],
    invitations: [OLD, KIM], timeZoneId: TZ, canInvite: true, ...over,
  };
}

function httpError(status: number, error: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('ResidentsSectionComponent (030 T116)', () => {
  let svc: jasmine.SpyObj<CommunitySettingsService>;
  let ctx: CommunitySettingsContext;

  async function setup(opts: { settings?: CommunitySettings; page?: ResidentsPage | HttpErrorResponse; property?: ResidentProperty; mode?: SettingsMode } = {}) {
    svc = jasmine.createSpyObj<CommunitySettingsService>('CommunitySettingsService', [
      'getResidents', 'getResidentProperty', 'inviteResident', 'getRemovalPreview', 'removeResident', 'resendInvitation', 'revokeInvitation',
    ]);
    const p = opts.page ?? page(BASELINE);
    if (p instanceof HttpErrorResponse) svc.getResidents.and.rejectWith(p);
    else svc.getResidents.and.resolveTo(p);
    svc.getResidentProperty.and.resolveTo(opts.property ?? property());
    ctx = new CommunitySettingsContext();
    ctx.settings.set(opts.settings ?? settings());
    ctx.mode.set(opts.mode ?? 'board');
    spyOn(ctx, 'reload').and.resolveTo();
    const r = await render(ResidentsSectionComponent, {
      providers: [
        provideRouter([]),
        { provide: CommunitySettingsService, useValue: svc },
        { provide: CommunitySettingsContext, useValue: ctx },
        { provide: ViewportService, useValue: { isNarrow: signal(false).asReadonly() } },
      ],
    });
    await settle(r);
    return r;
  }

  async function settle(r: Awaited<ReturnType<typeof setup>>) {
    await r.fixture.whenStable();
    r.fixture.detectChanges();
    await r.fixture.whenStable();
    r.fixture.detectChanges();
  }

  function table(): HTMLElement {
    return screen.getByRole('table', { name: /^Properties in .+, with residents and invitations$/ });
  }

  function bodyRows(): HTMLElement[] {
    return within(table()).getAllByRole('row').slice(1);
  }

  async function openDrawer(r: Awaited<ReturnType<typeof setup>>, address = '711 Keystone Park Dr #29') {
    fireEvent.click(within(table()).getByRole('button', { name: address }));
    await settle(r);
    return screen.getByRole('dialog', { name: address });
  }

  function toasts(): string[] {
    return TestBed.inject(ToastService).toasts().map(t => t.message);
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  describe('the list', () => {
    it('shows 25 rows with the address, resident names and invitations in words (8.06.a)', async () => {
      await setup();
      expect(svc.getResidents).toHaveBeenCalledOnceWith('c-sh', { search: undefined, filter: undefined, limit: 25, offset: 0 });
      const rows = bodyRows();
      expect(rows.length).toBe(25);
      expect(within(table()).getAllByRole('columnheader').map(h => h.textContent!.trim())).toEqual(['Property', 'Residents', 'Invitations']);
      const r29 = within(rows[0]);
      expect(r29.getByRole('rowheader').textContent).toContain('711 Keystone Park Dr #29');
      expect(rows[0].textContent).toContain('Jane Alvarez, Sam Ortiz');
      expect(r29.getByText('1 pending')).toBeTruthy();
      expect(within(rows[1]).getByText('No resident')).toBeTruthy();
      expect(within(rows[1]).getByText('1 expired')).toBeTruthy();
      expect(within(rows[3]).getByText('None')).toBeTruthy();
      expect(within(rows[0]).queryByRole('button', { name: /Remove|Resend|Revoke/ })).toBeNull();
      expect(screen.getByText('Showing 1–25 of 120')).toBeTruthy();
      expect(screen.getByText(/^Every property in Sakura Heights, its residents and its resident invitations\./)).toBeTruthy();
      expect(screen.getByRole('button', { name: 'Invite a resident to a property in Sakura Heights' })).toBeTruthy();
    });

    it('reads a scheduled invitation as "Scheduled" text, with the Onboarding helper (8.06.m, 8.06.n)', async () => {
      await setup({
        settings: settings({ communityName: 'Keystone Ridge', status: 'Onboarding' }),
        page: page([row('r2', '2 Ridgeview Ct', [], { scheduled: 1 }), row('r8', '8 Ridgeview Ct', [], { scheduled: 1, expired: 1 })], { total: 2 }),
      });
      expect(within(bodyRows()[0]).getByText('1 scheduled')).toBeTruthy();
      expect(bodyRows()[1].textContent!.replace(/\s+/g, ' ')).toContain('1 scheduled · 1 expired');
      expect(screen.getByText('Keystone Ridge is Onboarding. Resident invitations are saved as Scheduled and emailed when it is marked Active.')).toBeTruthy();
    });

    it('shows the email a search matched under the name', async () => {
      await setup({ page: page([row('p29', '711 Keystone Park Dr #29', ['Jane Alvarez'], {}, 'jane@example.com')], { total: 1 }) });
      expect(within(bodyRows()[0]).getByText('jane@example.com')).toBeTruthy();
    });

    it('searches as the person types, back on page 1 (8.06.c)', async () => {
      const r = await setup();
      svc.getResidents.and.resolveTo(page([R29], { total: 1 }));
      const search = screen.getByRole('searchbox', { name: 'Search residents' }) as HTMLInputElement;
      expect(search.placeholder).toBe('Search by address, resident name or email');
      fireEvent.input(search, { target: { value: 'keystone park dr #29' } });
      await until(() => svc.getResidents.calls.count() === 2);
      await settle(r);
      expect(svc.getResidents.calls.mostRecent().args).toEqual(['c-sh', { search: 'keystone park dr #29', filter: undefined, limit: 25, offset: 0 }]);
      expect(bodyRows().length).toBe(1);
      expect(screen.getByText('Showing 1–1 of 1')).toBeTruthy();
      expect(screen.getByRole('button', { name: 'All · 120' }).getAttribute('aria-pressed')).toBe('true');
    });

    it('offers the three filters with their counts, one at a time (8.06.d, 8.06.n)', async () => {
      const r = await setup();
      const group = screen.getByRole('group', { name: 'Filter properties' });
      expect(within(group).getAllByRole('button').map(b => b.textContent!.replace(/\s+/g, ' ').trim()))
        .toEqual(['All · 120', 'No resident · 7', 'Invitation pending · 2', 'Invitation expired · 1']);

      svc.getResidents.and.resolveTo(page([R29, R35], { total: 2 }));
      fireEvent.click(within(group).getByRole('button', { name: 'Invitation pending · 2' }));
      await settle(r);
      expect(svc.getResidents.calls.mostRecent().args).toEqual(['c-sh', { search: undefined, filter: 'invitationPending', limit: 25, offset: 0 }]);
      expect(within(group).getByRole('button', { name: 'Invitation pending · 2' }).getAttribute('aria-pressed')).toBe('true');
      expect(within(group).getByRole('button', { name: 'All · 120' }).getAttribute('aria-pressed')).toBe('false');
      expect(screen.getByText('Includes scheduled invitations.')).toBeTruthy();

      svc.getResidents.and.resolveTo(page([], { total: 0 }));
      fireEvent.click(within(group).getByRole('button', { name: 'No resident · 7' }));
      await settle(r);
      expect(svc.getResidents.calls.mostRecent().args[1]).toEqual({ search: undefined, filter: 'noResident', limit: 25, offset: 0 });
      expect(screen.getByText('Every property has a resident.')).toBeTruthy();

      fireEvent.click(within(group).getByRole('button', { name: 'Invitation expired · 1' }));
      await settle(r);
      expect(screen.getByText('No properties have an expired invitation.')).toBeTruthy();
    });

    it('says when nothing matches, and "Clear search" restores the list (8.06.e)', async () => {
      const r = await setup();
      svc.getResidents.and.resolveTo(page([], { total: 0 }));
      const search = screen.getByRole('searchbox', { name: 'Search residents' }) as HTMLInputElement;
      fireEvent.input(search, { target: { value: 'jane alvarex' } });
      await until(() => svc.getResidents.calls.count() === 2);
      await settle(r);
      expect(screen.getByText('No properties match “jane alvarex”.')).toBeTruthy();
      expect(screen.queryByRole('table')).toBeNull();

      svc.getResidents.and.resolveTo(page(BASELINE));
      fireEvent.click(screen.getByRole('button', { name: 'Clear search' }));
      await settle(r);
      expect(search.value).toBe('');
      expect(bodyRows().length).toBe(25);
      expect(document.activeElement).toBe(search);
    });

    it('pages through the list', async () => {
      const r = await setup();
      svc.getResidents.and.resolveTo(page(FILLER.slice(0, 5), { offset: 25 }));
      fireEvent.click(screen.getByRole('button', { name: 'Next page' }));
      await settle(r);
      expect(svc.getResidents.calls.mostRecent().args[1]).toEqual({ search: undefined, filter: undefined, limit: 25, offset: 25 });
    });

    it('shows the empty state and no invite action when properties are not loaded (US8-S14, 8.06.u)', async () => {
      await setup({ settings: settings({ communityName: 'Keystone Park', status: 'Onboarding' }), page: page([], { total: 0, propertiesLoaded: false }) });
      expect(screen.getByText("Properties haven't been loaded for Keystone Park yet.")).toBeTruthy();
      expect(screen.getByText('Residents can be invited once its properties are loaded.')).toBeTruthy();
      expect(screen.queryByRole('button', { name: /Invite/ })).toBeNull();
      expect(screen.queryByRole('searchbox')).toBeNull();
      expect(screen.queryByRole('group', { name: 'Filter properties' })).toBeNull();
      expect(screen.queryByRole('table')).toBeNull();
    });

    it('says when residents could not load, and tries again', async () => {
      const r = await setup({ page: httpError(0) });
      expect(screen.getByText("Couldn't load residents.")).toBeTruthy();
      svc.getResidents.and.resolveTo(page(BASELINE));
      fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
      await settle(r);
      expect(bodyRows().length).toBe(25);
    });
  });

  describe('the property drawer', () => {
    it('opens on a row with residents, invitations, and Invite, Resend, Revoke and Remove (8.06.y)', async () => {
      const r = await setup();
      const drawer = await openDrawer(r);
      expect(svc.getResidentProperty).toHaveBeenCalledOnceWith('c-sh', 'p29');
      const residents = within(drawer).getByRole('list', { name: 'Residents' });
      expect(within(residents).getAllByRole('listitem').length).toBe(2);
      expect(within(residents).getByText('jane@example.com')).toBeTruthy();
      expect(within(residents).getByText('Resident since Mar 2, 2024')).toBeTruthy();
      expect(within(drawer).getByRole('button', { name: 'Remove Jane Alvarez from 711 Keystone Park Dr #29' })).toBeTruthy();
      expect(within(drawer).getByRole('button', { name: 'Remove Sam Ortiz from 711 Keystone Park Dr #29' })).toBeTruthy();

      const invitations = within(drawer).getByRole('list', { name: 'Invitations' });
      const items = within(invitations).getAllByRole('listitem');
      // Newest first, revoked last.
      expect(items[0].textContent).toContain('kim@example.com');
      expect(items[0].textContent).toContain('Pending');
      expect(items[0].textContent).toContain(`Sent by Jordan Blake · ${formatDateTimeInZone('2026-10-10T20:40:00Z', TZ)}`);
      expect(items[0].textContent).toContain(`Expires ${formatDateTimeInZone('2099-10-13T20:40:00Z', TZ)}`);
      expect(items[1].textContent).toContain('old@example.com');
      expect(items[1].textContent).toContain('Revoked');
      expect(within(items[1]).queryByRole('button')).toBeNull();
      expect(within(drawer).getByRole('button', { name: 'Resend invitation to kim@example.com' })).toBeTruthy();
      expect(within(drawer).getByRole('button', { name: 'Revoke invitation to kim@example.com' })).toBeTruthy();
      expect(within(drawer).getByRole('button', { name: 'Invite a resident to 711 Keystone Park Dr #29' })).toBeTruthy();
      expect(within(table()).getByRole('button', { name: '711 Keystone Park Dr #29' }).getAttribute('aria-expanded')).toBe('true');

      fireEvent.click(within(drawer).getByRole('button', { name: 'Close 711 Keystone Park Dr #29' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
    });

    it('says "No residents yet." and "No invitations." for an empty property', async () => {
      const r = await setup({ property: property({ propertyId: 'p31', address: '715 Keystone Park Dr #31', residents: [], invitations: [] }) });
      const drawer = await openDrawer(r, '715 Keystone Park Dr #31');
      expect(within(drawer).getByText('No residents yet.')).toBeTruthy();
      expect(within(drawer).getByText('No invitations.')).toBeTruthy();
      expect(within(drawer).getByText('No resident')).toBeTruthy();
    });

    it('a scheduled invitation keeps Resend disabled with its reason (8.06.m)', async () => {
      const r = await setup({
        settings: settings({ communityName: 'Keystone Ridge', status: 'Onboarding' }),
        page: page([row('r2', '2 Ridgeview Ct', [], { scheduled: 1 })], { total: 1 }),
        property: property({
          propertyId: 'r2', address: '2 Ridgeview Ct', residents: [],
          invitations: [invitation({ id: 'i-lena', email: 'lena@example.com', state: 'Scheduled', effectiveState: 'Scheduled', sentAt: null, expiresAt: null, canResend: false, resendBlockedReason: 'COMMUNITY_NOT_ACTIVE' })],
        }),
      });
      const drawer = await openDrawer(r, '2 Ridgeview Ct');
      expect(drawer.textContent).toContain('Sent by Jordan Blake · Not sent yet');
      expect(within(drawer).getAllByText('Sent when Keystone Ridge is marked Active').length).toBeGreaterThan(0);
      const resend = within(drawer).getByRole('button', { name: 'Resend invitation to lena@example.com' });
      expect(resend.getAttribute('aria-disabled')).toBe('true');
      fireEvent.click(resend);
      expect(svc.resendInvitation).not.toHaveBeenCalled();
    });

    it('Resend sends again, with no confirmation (8.06.p)', async () => {
      const r = await setup();
      const drawer = await openDrawer(r);
      svc.resendInvitation.and.resolveTo({} as never);
      fireEvent.click(within(drawer).getByRole('button', { name: 'Resend invitation to kim@example.com' }));
      await settle(r);
      expect(svc.resendInvitation).toHaveBeenCalledOnceWith('c-sh', 'i-kim');
      expect(toasts()).toEqual(['Invitation sent again to kim@example.com. The earlier link no longer works.']);
      expect(svc.getResidentProperty).toHaveBeenCalledTimes(2);
      expect(svc.getResidents).toHaveBeenCalledTimes(2);
    });

    it('a resend of an invitation accepted meanwhile says the list is up to date', async () => {
      const r = await setup();
      const drawer = await openDrawer(r);
      svc.resendInvitation.and.rejectWith(httpError(409, { code: 'INVITATION_INVALID', message: 'Invalid.' }));
      fireEvent.click(within(drawer).getByRole('button', { name: 'Resend invitation to kim@example.com' }));
      await settle(r);
      expect(toasts()).toEqual(['This invitation was already accepted or revoked. The list is now up to date.']);
      expect(svc.getResidentProperty).toHaveBeenCalledTimes(2);
    });

    it('Revoke asks first, then revokes (8.06.q)', async () => {
      const r = await setup();
      const drawer = await openDrawer(r);
      fireEvent.click(within(drawer).getByRole('button', { name: 'Revoke invitation to kim@example.com' }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: 'Revoke the invitation to kim@example.com?' });
      expect(dlg.textContent).toContain('The link in the invitation email stops working at once.');
      expect(dlg.textContent).toContain("kim@example.com can't use it to become a resident of 711 Keystone Park Dr #29.");
      expect(dlg.textContent).toContain('You can send kim@example.com a new invitation later.');
      svc.revokeInvitation.and.resolveTo({} as never);
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke invitation' }));
      await settle(r);
      expect(svc.revokeInvitation).toHaveBeenCalledOnceWith('c-sh', 'i-kim');
      expect(toasts()).toEqual(['Invitation to kim@example.com revoked.']);
      expect(screen.queryByRole('dialog', { name: /Revoke/ })).toBeNull();
    });
  });

  describe('the invite form', () => {
    async function openInvite(r: Awaited<ReturnType<typeof setup>>) {
      const drawer = await openDrawer(r);
      fireEvent.click(within(drawer).getByRole('button', { name: 'Invite a resident to 711 Keystone Park Dr #29' }));
      await settle(r);
      return screen.getByRole('dialog', { name: 'Invite a resident' });
    }

    async function send(r: Awaited<ReturnType<typeof setup>>, dlg: HTMLElement, email: string, button = 'Send invitation') {
      const field = within(dlg).getByLabelText('Email address (required)') as HTMLInputElement;
      fireEvent.input(field, { target: { value: email } });
      r.fixture.detectChanges();
      fireEvent.click(within(dlg).getByRole('button', { name: button }));
      await settle(r);
      return field;
    }

    function errorFor(field: HTMLElement): string {
      const ids = (field.getAttribute('aria-describedby') ?? '').split(/\s+/);
      return ids.map(id => document.getElementById(id)).filter(n => n?.classList.contains('field-error')).map(n => n!.textContent!.trim()).join(' ');
    }

    it('from the drawer: the property is fixed, and an invitation is sent by email only (US8-S1, 8.06.f, 8.06.g)', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      expect(dlg.textContent).toContain('Sakura Heights');
      expect(dlg.textContent).toContain('Property: 711 Keystone Park Dr #29');
      expect(dlg.textContent).toContain('This property has 2 residents. Whoever accepts is added next to them.');
      expect(within(dlg).queryByRole('combobox')).toBeNull();
      expect(within(dlg).getByText('Until they accept, the invitation shows only this email address.')).toBeTruthy();

      const field = within(dlg).getByLabelText('Email address (required)') as HTMLInputElement;
      fireEvent.input(field, { target: { value: 'kim@example.com' } });
      r.fixture.detectChanges();
      expect(dlg.textContent).toContain("We'll email kim@example.com an invitation to be a resident of 711 Keystone Park Dr #29. It's valid for 72 hours. If they already have an account, they sign in to accept. Nothing changes until they accept.");

      svc.inviteResident.and.resolveTo({ invitationId: 'i-kim', state: 'Pending', expiresAt: '2026-10-13T20:40:00Z' });
      fireEvent.click(within(dlg).getByRole('button', { name: 'Send invitation' }));
      await settle(r);
      expect(svc.inviteResident).toHaveBeenCalledOnceWith('c-sh', 'p29', 'kim@example.com');
      expect(screen.queryByRole('dialog', { name: 'Invite a resident' })).toBeNull();
      expect(toasts()).toEqual(['Invitation sent to kim@example.com.']);
      expect(svc.getResidents).toHaveBeenCalledTimes(2);
      await until(() => document.activeElement === within(table()).getByRole('button', { name: '711 Keystone Park Dr #29' }));
    });

    it('checks the email before sending', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      const field = await send(r, dlg, 'not-an-email');
      expect(svc.inviteResident).not.toHaveBeenCalled();
      expect(errorFor(field)).toBe('Enter a valid email address.');
    });

    it('refuses an existing resident under the email field, keeping the input (ALREADY_RESIDENT, 8.06.h)', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      svc.inviteResident.and.rejectWith(httpError(409, { code: 'ALREADY_RESIDENT', message: 'Already.' }));
      const field = await send(r, dlg, 'jane@example.com');
      expect(errorFor(field)).toBe('jane@example.com is already a resident of 711 Keystone Park Dr #29.');
      expect(field.value).toBe('jane@example.com');
      expect(field.getAttribute('aria-invalid')).toBe('true');
    });

    it('offers "Resend invitation" for a pending one (INVITATION_PENDING, 8.06.i)', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      svc.inviteResident.and.rejectWith(httpError(409, { code: 'INVITATION_PENDING', message: 'Pending.', invitationId: 'i-kim' }));
      const field = await send(r, dlg, 'kim@example.com');
      expect(errorFor(field)).toBe('kim@example.com already has a pending invitation to 711 Keystone Park Dr #29. Resend it instead?');
      svc.resendInvitation.and.resolveTo({} as never);
      fireEvent.click(within(dlg).getByRole('button', { name: 'Resend invitation' }));
      await settle(r);
      expect(svc.resendInvitation).toHaveBeenCalledOnceWith('c-sh', 'i-kim');
      expect(screen.queryByRole('dialog', { name: 'Invite a resident' })).toBeNull();
      expect(toasts()).toEqual(['Invitation sent again to kim@example.com. The earlier link no longer works.']);
    });

    it('refuses your own email (SELF_GRANT_NOT_ALLOWED, 8.06.j)', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      svc.inviteResident.and.rejectWith(httpError(422, { code: 'SELF_GRANT_NOT_ALLOWED', message: 'Self.' }));
      const field = await send(r, dlg, 'jordan@example.com');
      expect(errorFor(field)).toBe("You can't give yourself a role or a home. Ask another Company Administrator or this community's Community Manager.");
    });

    it('says when it is rate limited, next to the button (8.06.k)', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      svc.inviteResident.and.rejectWith(httpError(429));
      await send(r, dlg, 'kim@example.com');
      expect(within(dlg).getByText('Too many requests. Wait a few minutes, then try again.')).toBeTruthy();
    });

    it('says when the save failed, at the top', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      svc.inviteResident.and.rejectWith(httpError(0));
      await send(r, dlg, 'kim@example.com');
      expect(within(dlg).getByText("Couldn't save. Check your connection and try again.")).toBeTruthy();
    });

    it('a FORBIDDEN refusal shows at the top with "Close", which reloads the page (US8-S12)', async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      svc.inviteResident.and.rejectWith(httpError(403, { code: 'FORBIDDEN', message: 'No.' }));
      await send(r, dlg, 'kim@example.com');
      expect(within(dlg).getByText("You don't have permission to do that.")).toBeTruthy();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(screen.queryByRole('dialog', { name: 'Invite a resident' })).toBeNull();
      expect(ctx.reload).toHaveBeenCalled();
    });

    it("shows the server's email problem under the field, other problems at the top, and Cancel closes", async () => {
      const r = await setup();
      const dlg = await openInvite(r);
      svc.inviteResident.and.rejectWith(httpError(422, { code: 'VALIDATION_ERROR', message: 'Invalid.', errors: [{ field: 'email', message: 'Email can be at most 254 characters.' }] }));
      const field = await send(r, dlg, 'kim@example.com');
      expect(errorFor(field)).toBe('Email can be at most 254 characters.');
      svc.inviteResident.and.rejectWith(httpError(422, { code: 'VALIDATION_ERROR', message: 'Invalid.', errors: [{ field: 'propertyId', message: 'Choose a property in this community.' }] }));
      await send(r, dlg, 'kim@example.com');
      expect(within(dlg).getByText('Choose a property in this community.')).toBeTruthy();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Cancel' }));
      await settle(r);
      expect(screen.queryByRole('dialog', { name: 'Invite a resident' })).toBeNull();
    });

    it('while Onboarding, saves the invitation as Scheduled (US8-S4, 8.06.l, 8.06.m)', async () => {
      const r = await setup({ settings: settings({ communityName: 'Keystone Ridge', status: 'Onboarding' }), property: property({ propertyId: 'r8', address: '8 Ridgeview Ct', residents: [], invitations: [] }), page: page([row('r8', '8 Ridgeview Ct', [])], { total: 1 }) });
      const drawer = await openDrawer(r, '8 Ridgeview Ct');
      fireEvent.click(within(drawer).getByRole('button', { name: 'Invite a resident to 8 Ridgeview Ct' }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: 'Invite a resident' });
      expect(dlg.textContent).toContain('Keystone Ridge is Onboarding, so nothing is emailed yet. The invitation is saved as Scheduled and emailed when a Company Administrator marks Keystone Ridge Active. The link then works for 72 hours.');
      const field = within(dlg).getByLabelText('Email address (required)') as HTMLInputElement;
      fireEvent.input(field, { target: { value: 'omar@example.com' } });
      r.fixture.detectChanges();
      expect(dlg.textContent).toContain("We'll save an invitation for omar@example.com to be a resident of 8 Ridgeview Ct. It's emailed when Keystone Ridge is marked Active.");
      svc.inviteResident.and.resolveTo({ invitationId: 'i-omar', state: 'Scheduled', expiresAt: null });
      fireEvent.click(within(dlg).getByRole('button', { name: 'Save invitation' }));
      await settle(r);
      expect(toasts()).toEqual(['Invitation saved. It will be sent when Keystone Ridge is marked Active.']);
    });

    it('a scheduled duplicate is refused with no Resend button', async () => {
      const r = await setup({ settings: settings({ communityName: 'Keystone Ridge', status: 'Onboarding' }) });
      const dlg = await openInvite(r);
      svc.inviteResident.and.rejectWith(httpError(409, { code: 'INVITATION_PENDING', message: 'Pending.', invitationId: 'i-lena' }));
      const field = await send(r, dlg, 'lena@example.com', 'Save invitation');
      expect(errorFor(field)).toBe('lena@example.com already has a scheduled invitation to 711 Keystone Park Dr #29. It will be sent when Keystone Ridge is marked Active.');
      expect(within(dlg).queryByRole('button', { name: 'Resend invitation' })).toBeNull();
    });

    it('from the header: the property is chosen from this community\'s properties (8.06.h)', async () => {
      const r = await setup({ mode: 'company' });
      fireEvent.click(screen.getByRole('button', { name: 'Invite a resident to a property in Sakura Heights' }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: 'Invite a resident' });
      const field = await send(r, dlg, 'kim@example.com');
      expect(svc.inviteResident).not.toHaveBeenCalled();
      const combo = within(dlg).getByRole('combobox', { name: 'Property (required)' });
      expect(combo.getAttribute('aria-invalid')).toBe('true');
      expect(dlg.textContent).toContain('Choose a property.');
      expect(dlg.textContent).toContain('Only properties in Sakura Heights are listed.');

      fireEvent.focus(combo);
      fireEvent.keyDown(combo, { key: 'ArrowDown', altKey: true });
      r.fixture.detectChanges();
      fireEvent.click(within(dlg).getByRole('option', { name: /^711 Keystone Park Dr #29/ }));
      await settle(r);
      svc.inviteResident.and.resolveTo({ invitationId: 'i-kim', state: 'Pending', expiresAt: null });
      fireEvent.click(within(dlg).getByRole('button', { name: 'Send invitation' }));
      await settle(r);
      expect(field).toBeTruthy();
      expect(svc.inviteResident).toHaveBeenCalledOnceWith('c-sh', 'p29', 'kim@example.com');
    });
  });

  describe('the remove dialog', () => {
    async function openRemove(r: Awaited<ReturnType<typeof setup>>, name: string) {
      const drawer = await openDrawer(r);
      fireEvent.click(within(drawer).getByRole('button', { name: `Remove ${name} from 711 Keystone Park Dr #29` }));
      await settle(r);
      return screen.getByRole('dialog', { name: `Remove ${name} from 711 Keystone Park Dr #29?` });
    }

    it("shows the removal preview's lines: autopay cancelled and who is emailed (US8-S8, 8.06.r)", async () => {
      const r = await setup({ mode: 'company' });
      svc.getRemovalPreview.and.resolveTo({ autopay: 'WillCancel', autopayReason: 'EnrolledByThisResident', otherResidentsEmailed: 1 });
      const dlg = await openRemove(r, 'Jane Alvarez');
      expect(svc.getRemovalPreview).toHaveBeenCalledOnceWith('c-sh', 'p29', 'u-jane');
      const text = dlg.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain("Jane Alvarez loses access to 711 Keystone Park Dr #29 at once, even if they're signed in right now.");
      expect(text).toContain("This property's autopay is cancelled, because Jane Alvarez enrolled it. No new automatic payment starts. A payment already in progress finishes normally.");
      expect(text).toContain('Jane Alvarez is emailed that they were removed and that autopay ended.');
      expect(text).toContain('The other resident, Sam Ortiz, is emailed that autopay ended.');
      expect(text).toContain("The owner of record and the balance of 711 Keystone Park Dr #29 don't change.");
      expect(text).toContain("Jane Alvarez's account, any other homes and roles don't change. They can still sign in.");

      svc.removeResident.and.resolveTo({ removed: true, autopayCancelled: true });
      svc.getResidentProperty.and.resolveTo(property({ residents: [property().residents[1]] }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Remove resident' }));
      await settle(r);
      expect(svc.removeResident).toHaveBeenCalledOnceWith('c-sh', 'p29', 'u-jane');
      expect(toasts()).toEqual(['Jane Alvarez was removed from 711 Keystone Park Dr #29. Its autopay was cancelled.']);
      expect(screen.queryByRole('button', { name: 'Remove Jane Alvarez from 711 Keystone Park Dr #29' })).toBeNull();
    });

    it('autopay stays when the resident did not enroll it (8.06.t)', async () => {
      const r = await setup();
      svc.getRemovalPreview.and.resolveTo({ autopay: 'Unaffected', autopayReason: 'EnrolledByAnotherResident', otherResidentsEmailed: 0 });
      const dlg = await openRemove(r, 'Sam Ortiz');
      const text = dlg.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('Sam Ortiz is emailed that they were removed.');
      expect(text).not.toContain('autopay ended');
      expect(text).toContain("This property's autopay stays on. Sam Ortiz didn't enroll it.");
      svc.removeResident.and.resolveTo({ removed: true, autopayCancelled: false });
      fireEvent.click(within(dlg).getByRole('button', { name: 'Remove resident' }));
      await settle(r);
      expect(toasts()).toEqual(['Sam Ortiz was removed from 711 Keystone Park Dr #29.']);
    });

    it('words no recorded enroller, no autopay and the last resident', async () => {
      const r = await setup({ property: property({ residents: [property().residents[0]] }) });
      svc.getRemovalPreview.and.resolveTo({ autopay: 'WillCancel', autopayReason: 'NoEnrollerRecorded', otherResidentsEmailed: 0 });
      let dlg = await openRemove(r, 'Jane Alvarez');
      expect(dlg.textContent).toContain("This property's autopay is cancelled. It was set up before the app recorded who enrolled it, so removing any resident cancels it.");
      expect(dlg.textContent).toContain('711 Keystone Park Dr #29 will have no resident and shows “No resident”.');
      fireEvent.click(within(dlg).getByRole('button', { name: 'Cancel' }));
      await settle(r);

      svc.getRemovalPreview.and.resolveTo({ autopay: 'None', autopayReason: null, otherResidentsEmailed: 0 });
      fireEvent.click(screen.getByRole('button', { name: 'Remove Jane Alvarez from 711 Keystone Park Dr #29' }));
      await settle(r);
      dlg = screen.getByRole('dialog', { name: 'Remove Jane Alvarez from 711 Keystone Park Dr #29?' });
      expect(dlg.textContent).toContain('This property has no active autopay.');
    });

    it('a FORBIDDEN refusal shows only the refusal and "Close" (US8-S12, 8.06.w)', async () => {
      const r = await setup();
      svc.getRemovalPreview.and.resolveTo({ autopay: 'None', autopayReason: null, otherResidentsEmailed: 0 });
      const dlg = await openRemove(r, 'Sam Ortiz');
      svc.removeResident.and.rejectWith(httpError(403, { code: 'FORBIDDEN', message: 'No.' }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Remove resident' }));
      await settle(r);
      expect(within(dlg).getByText("You don't have permission to do that.")).toBeTruthy();
      expect(within(dlg).queryByText(/loses access/)).toBeNull();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(ctx.reload).toHaveBeenCalled();
    });

    it('someone already removed says the list is up to date', async () => {
      const r = await setup();
      svc.getRemovalPreview.and.resolveTo({ autopay: 'None', autopayReason: null, otherResidentsEmailed: 0 });
      const dlg = await openRemove(r, 'Jane Alvarez');
      svc.removeResident.and.rejectWith(httpError(404, { code: 'NOT_FOUND', message: 'Not linked.' }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Remove resident' }));
      await settle(r);
      expect(toasts()).toEqual(['Jane Alvarez is no longer a resident of 711 Keystone Park Dr #29. The list is now up to date.']);
      expect(screen.queryByRole('dialog', { name: /^Remove/ })).toBeNull();
    });

    it('a refused preview shows only the refusal; a network failure and a rate limit say so', async () => {
      const r = await setup();
      svc.getRemovalPreview.and.rejectWith(httpError(403, { code: 'FORBIDDEN', message: 'No.' }));
      let dlg = await openRemove(r, 'Jane Alvarez');
      expect(within(dlg).getByText("You don't have permission to do that.")).toBeTruthy();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Close' }));
      await settle(r);

      svc.getRemovalPreview.and.resolveTo({ autopay: 'None', autopayReason: null, otherResidentsEmailed: 0 });
      fireEvent.click(screen.getByRole('button', { name: 'Remove Sam Ortiz from 711 Keystone Park Dr #29' }));
      await settle(r);
      dlg = screen.getByRole('dialog', { name: 'Remove Sam Ortiz from 711 Keystone Park Dr #29?' });
      svc.removeResident.and.rejectWith(httpError(0));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Remove resident' }));
      await settle(r);
      expect(within(dlg).getByText("Couldn't remove the resident. Check your connection and try again.")).toBeTruthy();
      svc.removeResident.and.rejectWith(httpError(429));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Remove resident' }));
      await settle(r);
      expect(within(dlg).getByText('Too many requests. Wait a few minutes, then try again.')).toBeTruthy();
    });

    it('revoking an invitation accepted meanwhile says the list is up to date; a network failure says so', async () => {
      const r = await setup();
      const drawer = await openDrawer(r);
      fireEvent.click(within(drawer).getByRole('button', { name: 'Revoke invitation to kim@example.com' }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: 'Revoke the invitation to kim@example.com?' });
      svc.revokeInvitation.and.rejectWith(httpError(0));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke invitation' }));
      await settle(r);
      expect(within(dlg).getByText("Couldn't revoke the invitation. Check your connection and try again.")).toBeTruthy();
      svc.revokeInvitation.and.rejectWith(httpError(409, { code: 'INVITATION_INVALID', message: 'Invalid.' }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Revoke invitation' }));
      await settle(r);
      expect(toasts()).toEqual(['This invitation was already accepted or revoked. The list is now up to date.']);
    });

    it('a preview that cannot load says so and keeps "Remove resident" disabled', async () => {
      const r = await setup();
      svc.getRemovalPreview.and.rejectWith(httpError(0));
      const dlg = await openRemove(r, 'Jane Alvarez');
      expect(within(dlg).getByText("Couldn't load what this will change.")).toBeTruthy();
      expect((within(dlg).getByRole('button', { name: 'Remove resident' }) as HTMLButtonElement).disabled).toBeTrue();
    });
  });

  describe('an Archived community (US5-S7)', () => {
    it('is read-only: search, filters and pages work; no Invite, Remove, Resend or Revoke (8.06.v)', async () => {
      const r = await setup({
        mode: 'company',
        settings: settings({ communityName: 'Old Mill Commons', status: 'Archived' }),
        property: property({ invitations: [invitation({ id: 'i-rae', email: 'rae@example.com', state: 'Revoked', effectiveState: 'Revoked', revokeReason: 'CommunityArchived', canResend: false, canRevoke: false }), KIM] }),
      });
      expect(screen.queryByRole('button', { name: /Invite/ })).toBeNull();
      expect(screen.getByRole('searchbox', { name: 'Search residents' })).toBeTruthy();
      expect(screen.getByRole('group', { name: 'Filter properties' })).toBeTruthy();
      const drawer = await openDrawer(r);
      expect(within(drawer).queryByRole('button', { name: /^(Remove|Resend|Revoke|Invite)/ })).toBeNull();
      expect(drawer.textContent).toContain('Revoked — community archived');
      expect(drawer.textContent).toContain('Jane Alvarez');
    });
  });
});
