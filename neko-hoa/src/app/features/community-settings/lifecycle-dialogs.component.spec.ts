import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { LifecycleDialogsComponent } from './lifecycle-dialogs.component';
import { CommunitySettingsContext, SettingsMode } from './community-settings-context';
import { CommunitySettings, CommunitySettingsService } from '../../core/services/community-settings.service';
import { CompanyService, LifecyclePreview, MembershipKeptPreview } from '../../core/services/company.service';
import { ToastService } from '../../core/services/toast.service';
import { formatDateTimeInZone, formatTodayUtc } from '../../shared/date-format';

// 030 T102 / US3 / brief 04 §5.4–§5.7: the Mark Active, Archive and Restore dialogs. Each asks the
// server what will change when it opens (D4); a refusal replaces the consequences and offers "Close".

@Component({ standalone: true, template: '' })
class StubPageComponent {}

const NONE = { allowed: false, reasonCode: null };

function settings(over: Partial<CommunitySettings> = {}): CommunitySettings {
  return {
    id: 'c-om', communityName: 'Old Mill Commons', legalName: 'Old Mill Commons Association, Inc.', status: 'Active',
    parent: null, noManager: false, missingRequiredFields: [], sections: ['profile', 'managers-and-memberships', 'architectural-review'],
    can: {
      editProfile: true, changeParent: true, activate: NONE, archive: { allowed: true, reasonCode: null }, restore: { allowed: true, reasonCode: null },
      manageManagers: true, replaceLastManager: true, manageMemberships: true, manageResidents: true, editArcSettings: true,
      endMembershipsWhileArchived: true,
    },
    ...over,
  };
}

function kept(membershipId: string, name: string, role: string, endDate: string | null = null): MembershipKeptPreview {
  return { membershipId, userId: `u-${membershipId}`, name, role, endDate };
}

const MORGAN = kept('m-morgan', 'Morgan Ellis', 'CommunityManager');
const ROWAN = kept('m-rowan', 'Rowan Hayes', 'BoardMember');
const JESSE = kept('m-jesse', 'Jesse Lin', 'BoardMember');
const SKYLER = kept('m-skyler', 'Skyler Diaz', 'BoardMember');
const AVERY = kept('m-avery', 'Avery Quinn', 'Accountant');

function allowed(action: LifecyclePreview['action'], consequences: LifecyclePreview['consequences']): LifecyclePreview {
  return { action, allowed: true, refusal: null, consequences };
}

function refused(action: LifecyclePreview['action'], code: string, items?: { kind: string; label: string; status: string }[]): LifecyclePreview {
  return { action, allowed: false, refusal: { code, message: `${code} from the server`, items }, consequences: null };
}

function httpError(status: number, error: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('LifecycleDialogsComponent (030 T102)', () => {
  let company: jasmine.SpyObj<CompanyService>;
  let svc: jasmine.SpyObj<CommunitySettingsService>;
  let ctx: CommunitySettingsContext;

  async function setup(opts: { settings?: CommunitySettings; preview?: LifecyclePreview | HttpErrorResponse; mode?: SettingsMode } = {}) {
    company = jasmine.createSpyObj<CompanyService>('CompanyService', ['getLifecyclePreview', 'activate', 'archive', 'restore']);
    const p = opts.preview ?? allowed('archive', {});
    if (p instanceof HttpErrorResponse) company.getLifecyclePreview.and.rejectWith(p);
    else company.getLifecyclePreview.and.resolveTo(p);
    svc = jasmine.createSpyObj<CommunitySettingsService>('CommunitySettingsService', ['updateMembership']);
    ctx = new CommunitySettingsContext();
    ctx.settings.set(opts.settings ?? settings());
    ctx.mode.set(opts.mode ?? 'company');
    spyOn(ctx, 'reload').and.resolveTo();
    const r = await render(LifecycleDialogsComponent, {
      providers: [
        provideRouter([{ path: '**', component: StubPageComponent }]),
        { provide: CompanyService, useValue: company },
        { provide: CommunitySettingsService, useValue: svc },
        { provide: CommunitySettingsContext, useValue: ctx },
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

  async function request(r: Awaited<ReturnType<typeof setup>>, action: 'activate' | 'archive' | 'restore') {
    ctx.requestLifecycle(action);
    r.fixture.detectChanges();
    await settle(r);
  }

  function dialog(name: string): HTMLElement {
    return screen.getByRole('dialog', { name });
  }

  function lines(el: HTMLElement): string[] {
    return within(el).getAllByRole('listitem').map(li => li.textContent!.replace(/\s+/g, ' ').trim());
  }

  function toasts(): string[] {
    return TestBed.inject(ToastService).toasts().map(t => t.message);
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  it('renders nothing until a status action is requested', async () => {
    await setup();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(company.getLifecyclePreview).not.toHaveBeenCalled();
  });

  describe('Archive', () => {
    const PREVIEW = allowed('archive', {
      autopayCancelled: 3,
      residentsEmailed: 79,
      invitationsRevoked: [
        { id: 'i-drew', email: 'drew@example.com', kind: 'Membership', role: 'CommunityManager' },
        { id: 'i-rae', email: 'rae@example.com', kind: 'Residency', role: null },
      ],
      membershipsKept: [MORGAN, ROWAN, JESSE, SKYLER, AVERY],
    });

    it("lists the preview's counts and names before confirming (US3-S5, US3-S7, 8.04.n)", async () => {
      const r = await setup({ preview: PREVIEW });
      await request(r, 'archive');
      expect(company.getLifecyclePreview).toHaveBeenCalledOnceWith('c-om', 'archive');
      const dlg = dialog('Archive Old Mill Commons?');
      const text = dlg.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('The management company will no longer manage Old Mill Commons. This is what happens:');
      expect(text).toContain('3 autopay schedules are cancelled. No new automatic charge starts. A charge already submitted settles normally.');
      expect(text).toContain('79 residents are emailed once. The email says the management company no longer manages Old Mill Commons, that autopay has ended where it applied, and that their payment history stays readable.');
      expect(text).toContain('Residents keep their homes, marked “No longer managed”. They can still read the ledger, statements, receipts and transactions. Nothing else is open to them.');
      expect(text).toContain('2 invitations are revoked, and their links stop working: drew@example.com (Community Manager) and rae@example.com (resident).');
      expect(text).toContain('5 memberships are kept but give no access while it is archived: Morgan Ellis (Community Manager), Rowan Hayes, Jesse Lin and Skyler Diaz (Board Members), and Avery Quinn (Accountant).');
      expect(text).toContain('It leaves every community switcher and My Communities.');
      expect(within(dlg).getByRole('heading', { name: "What doesn't change" })).toBeTruthy();
      expect(text).toContain('Nothing is deleted. The profile, settings, memberships, residents and invitations are kept.');
      expect(text).toContain('You can restore it later. Autopay won\'t restart, and revoked invitations stay revoked.');

      company.archive.and.resolveTo({ status: 'Archived', autopayCancelled: 3, residentsEmailed: 79, invitationsRevoked: 2 });
      fireEvent.click(within(dlg).getByRole('button', { name: 'Archive community' }));
      await settle(r);
      expect(company.archive).toHaveBeenCalledOnceWith('c-om');
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(toasts()).toEqual(['Old Mill Commons is archived.']);
      expect(ctx.reload).toHaveBeenCalledTimes(1);
    });

    it('words zero counts and one of each', async () => {
      const r = await setup({ preview: allowed('archive', { autopayCancelled: 0, residentsEmailed: 1, invitationsRevoked: [], membershipsKept: [MORGAN] }) });
      await request(r, 'archive');
      const text = dialog('Archive Old Mill Commons?').textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('No autopay schedules are active.');
      expect(text).toContain('1 resident is emailed once.');
      expect(text).toContain('No invitations are pending.');
      expect(text).toContain('1 membership is kept but gives no access while it is archived: Morgan Ellis (Community Manager).');
    });

    it('says no one is emailed when no residents are linked', async () => {
      const r = await setup({ preview: allowed('archive', { autopayCancelled: 1, residentsEmailed: 0, invitationsRevoked: [], membershipsKept: [] }) });
      await request(r, 'archive');
      const text = dialog('Archive Old Mill Commons?').textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('1 autopay schedule is cancelled.');
      expect(text).toContain('No residents are linked, so no one is emailed.');
      expect(text).toContain('No memberships are kept.');
    });

    it('shows only the OPEN_WORK refusal and its list, with "Close" (US3-S10, US3-S11, 8.04.o)', async () => {
      const r = await setup({
        settings: settings({ id: 'c-kcm', communityName: 'Keystone Crossing Master' }),
        preview: refused('archive', 'OPEN_WORK', [
          { kind: 'ArcApplication', label: 'ARC-1041', status: 'Open' },
          { kind: 'ArcApplication', label: 'ARC-1042', status: 'DecisionReached' },
          { kind: 'SubAssociation', label: 'Keystone Crossing SF', status: 'Active' },
          { kind: 'SubAssociation', label: 'Keystone Crossing TH', status: 'Active' },
        ]),
      });
      await request(r, 'archive');
      const dlg = dialog('Archive Keystone Crossing Master?');
      expect(within(dlg).getByText("Keystone Crossing Master can't be archived until this work is closed:")).toBeTruthy();
      expect(lines(within(dlg).getByRole('list', { name: 'Open work' }))).toEqual([
        'ARC-1041 · Open', 'ARC-1042 · Decision Reached',
        'Keystone Crossing SF · sub-association, Active', 'Keystone Crossing TH · sub-association, Active',
      ]);
      expect(within(dlg).getByText('The board decides open architectural applications, and the Community Manager records each outcome in board mode. Archive each sub-association first.')).toBeTruthy();
      expect(within(dlg).queryByRole('link')).toBeNull();
      expect(within(dlg).queryByRole('button', { name: 'Archive community' })).toBeNull();
      expect(within(dlg).queryByText(/autopay/)).toBeNull();

      fireEvent.click(within(dlg).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(company.archive).not.toHaveBeenCalled();
      expect(ctx.reload).toHaveBeenCalledTimes(1);
    });

    it('a refusal after confirming looks the same (a race, D4)', async () => {
      const r = await setup({ preview: PREVIEW });
      await request(r, 'archive');
      company.archive.and.rejectWith(httpError(409, { code: 'OPEN_WORK', message: 'Open work.', items: [{ kind: 'ArcApplication', label: 'ARC-1050', status: 'Open' }] }));
      fireEvent.click(screen.getByRole('button', { name: 'Archive community' }));
      await settle(r);
      const dlg = dialog('Archive Old Mill Commons?');
      expect(within(dlg).getByText("Old Mill Commons can't be archived until this work is closed:")).toBeTruthy();
      expect(lines(within(dlg).getByRole('list', { name: 'Open work' }))).toEqual(['ARC-1050 · Open']);
      expect(within(dlg).getByRole('button', { name: 'Close' })).toBeTruthy();
    });

    it('says when archiving failed for a network reason, and keeps the dialog open', async () => {
      const r = await setup({ preview: PREVIEW });
      await request(r, 'archive');
      company.archive.and.rejectWith(httpError(0));
      fireEvent.click(screen.getByRole('button', { name: 'Archive community' }));
      await settle(r);
      expect(within(dialog('Archive Old Mill Commons?')).getByText("Couldn't archive Old Mill Commons. Check your connection and try again.")).toBeTruthy();
      expect(screen.getByRole('button', { name: 'Archive community' })).toBeTruthy();
    });

    it('says when it is rate limited', async () => {
      const r = await setup({ preview: PREVIEW });
      await request(r, 'archive');
      company.archive.and.rejectWith(httpError(429));
      fireEvent.click(screen.getByRole('button', { name: 'Archive community' }));
      await settle(r);
      expect(screen.getByText('Too many requests. Wait a few minutes, then try again.')).toBeTruthy();
    });

    it('a preview that cannot load says so, keeps the primary disabled, and tries again (§5.4)', async () => {
      const r = await setup({ preview: httpError(0) });
      await request(r, 'archive');
      const dlg = dialog('Archive Old Mill Commons?');
      expect(within(dlg).getByText("Couldn't load what this will change.")).toBeTruthy();
      expect((within(dlg).getByRole('button', { name: 'Archive community' }) as HTMLButtonElement).disabled).toBeTrue();
      company.getLifecyclePreview.and.resolveTo(PREVIEW);
      fireEvent.click(within(dlg).getByRole('button', { name: 'Try again' }));
      await settle(r);
      expect((within(dlg).getByRole('button', { name: 'Archive community' }) as HTMLButtonElement).disabled).toBeFalse();
    });

    it('Cancel and Esc close without archiving', async () => {
      const r = await setup({ preview: PREVIEW });
      await request(r, 'archive');
      fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();

      await request(r, 'archive');
      const dlg = dialog('Archive Old Mill Commons?');
      dlg.dispatchEvent(new Event('cancel', { cancelable: true }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(company.archive).not.toHaveBeenCalled();
    });
  });

  describe('Mark Active', () => {
    const EXPIRES = '2026-10-15T14:00:00Z';

    it('lists the scheduled invitations it emails, with the link expiry (US3-S3, 8.04.j)', async () => {
      const r = await setup({
        settings: settings({ id: 'c-kr', communityName: 'Keystone Ridge', status: 'Onboarding' }),
        preview: allowed('activate', {
          scheduledInvitationsToSend: [
            { email: 'lena@example.com', propertyAddress: '2 Ridgeview Ct', expiresAtIfSentNow: EXPIRES },
            { email: 'omar@example.com', propertyAddress: '8 Ridgeview Ct', expiresAtIfSentNow: EXPIRES },
          ],
          scheduledInvitationsRevoked: [],
        }),
      });
      await request(r, 'activate');
      const dlg = dialog('Mark Keystone Ridge Active?');
      const text = dlg.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('Keystone Ridge goes live. Residents linked to its homes can use them from their next visit.');
      const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
      expect(text).toContain(`2 scheduled resident invitations are emailed now. Each link works for 72 hours, until ${formatDateTimeInZone(EXPIRES, zone)}:`);
      expect(lines(within(dlg).getByRole('list', { name: 'Invitations emailed now' }))).toEqual(['lena@example.com · 2 Ridgeview Ct', 'omar@example.com · 8 Ridgeview Ct']);

      company.activate.and.resolveTo({ status: 'Active', invitationsSent: 2, invitationsFailedToSend: 0, invitationsRevoked: 0 });
      fireEvent.click(within(dlg).getByRole('button', { name: 'Mark community Active' }));
      await settle(r);
      expect(company.activate).toHaveBeenCalledOnceWith('c-kr');
      expect(toasts()).toEqual(['Keystone Ridge is now Active. 2 resident invitations were emailed.']);
      expect(ctx.reload).toHaveBeenCalled();
    });

    it('says when none are scheduled, and names the ones revoked instead', async () => {
      const r = await setup({
        settings: settings({ communityName: 'Keystone Ridge', status: 'Onboarding' }),
        preview: allowed('activate', {
          scheduledInvitationsToSend: [],
          scheduledInvitationsRevoked: [{ email: 'kim@example.com', propertyAddress: '5 Ridgeview Ct', expiresAtIfSentNow: EXPIRES }],
        }),
      });
      await request(r, 'activate');
      const text = dialog('Mark Keystone Ridge Active?').textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('No resident invitations are scheduled.');
      expect(text).toContain('1 scheduled invitation is revoked instead, because the person who sent it can no longer send it: kim@example.com · 5 Ridgeview Ct.');

      company.activate.and.resolveTo({ status: 'Active', invitationsSent: 0, invitationsFailedToSend: 0, invitationsRevoked: 1 });
      fireEvent.click(screen.getByRole('button', { name: 'Mark community Active' }));
      await settle(r);
      expect(toasts()).toEqual(['Keystone Ridge is now Active.']);
    });

    it('a NO_ACTIVE_MANAGER refusal links to Managers and memberships (US3-S4, 8.04.m, D9)', async () => {
      const r = await setup({ settings: settings({ id: 'c-kp', communityName: 'Keystone Park', status: 'Onboarding' }) });
      company.getLifecyclePreview.and.resolveTo(refused('activate', 'NO_ACTIVE_MANAGER'));
      await request(r, 'activate');
      const dlg = dialog('Mark Keystone Park Active?');
      expect(within(dlg).getByText("Keystone Park can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted.")).toBeTruthy();
      const link = within(dlg).getByRole('link', { name: 'Open Managers and memberships' });
      expect(link.getAttribute('href')).toBe('/app/company/communities/c-kp/settings/managers-and-memberships');
      expect(within(dlg).queryByRole('button', { name: 'Mark community Active' })).toBeNull();
      fireEvent.click(link);
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
    });

    it('says when marking Active failed for a network reason', async () => {
      const r = await setup({ settings: settings({ communityName: 'Keystone Ridge', status: 'Onboarding' }), preview: allowed('activate', { scheduledInvitationsToSend: [] }) });
      await request(r, 'activate');
      company.activate.and.rejectWith(httpError(0));
      fireEvent.click(screen.getByRole('button', { name: 'Mark community Active' }));
      await settle(r);
      expect(screen.getByText("Couldn't mark Keystone Ridge Active. Check your connection and try again.")).toBeTruthy();
    });

    it('a refusal after confirming shows only the refusal', async () => {
      const r = await setup({ settings: settings({ communityName: 'Keystone Ridge', status: 'Onboarding' }), preview: allowed('activate', { scheduledInvitationsToSend: [] }) });
      await request(r, 'activate');
      company.activate.and.rejectWith(httpError(403, { code: 'FORBIDDEN', message: 'Forbidden.' }));
      fireEvent.click(screen.getByRole('button', { name: 'Mark community Active' }));
      await settle(r);
      expect(screen.getByText("You don't have permission to do that.")).toBeTruthy();
      expect(screen.queryByRole('button', { name: 'Mark community Active' })).toBeNull();
    });
  });

  describe('Restore', () => {
    const ARCHIVED = settings({ status: 'Archived' });
    const RETURNS_ACTIVE = allowed('restore', { returnsTo: 'Active', reason: 'WasActiveWithManager', membershipsRegainingAccess: [MORGAN, ROWAN, JESSE, SKYLER, AVERY] });
    const RETURNS_ONBOARDING = allowed('restore', { returnsTo: 'Onboarding', reason: 'NoOpenEndedManager', membershipsRegainingAccess: [ROWAN, JESSE, SKYLER, AVERY] });

    it('says where it returns to and lists the memberships that regain access, each with End (US3-S12, FR-015, 8.04.t)', async () => {
      const r = await setup({ settings: ARCHIVED, preview: RETURNS_ACTIVE });
      await request(r, 'restore');
      const dlg = dialog('Restore Old Mill Commons?');
      expect(within(dlg).getByText('Old Mill Commons returns to Active, because it was Active before it was archived and it has a Community Manager with no end date: Morgan Ellis.').tagName).toBe('STRONG');
      const list = within(dlg).getByRole('list', { name: 'These memberships will give access again:' });
      expect(within(list).getAllByRole('listitem').map(li => li.querySelector('.lc__row-text')!.textContent!.replace(/\s+/g, ' ').trim())).toEqual([
        'Morgan Ellis · Community Manager · no end date', 'Rowan Hayes · Board Member', 'Jesse Lin · Board Member',
        'Skyler Diaz · Board Member', 'Avery Quinn · Accountant',
      ]);
      expect(within(list).getAllByRole('button', { name: /^End .* membership before restoring$/ }).length).toBe(5);
      expect(within(list).getByRole('button', { name: "End Morgan Ellis's Community Manager membership before restoring" })).toBeTruthy();
      const text = dlg.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('It appears again in the community switchers and My Communities of the people above.');
      expect(text).toContain('Autopay cancelled by the archive doesn\'t restart. Residents enroll again themselves.');
      expect(text).toContain('Invitations revoked by the archive stay revoked.');

      company.restore.and.resolveTo({ status: 'Active', reason: 'WasActiveWithManager' });
      fireEvent.click(within(dlg).getByRole('button', { name: 'Restore community' }));
      await settle(r);
      expect(company.restore).toHaveBeenCalledOnceWith('c-om');
      expect(toasts()).toEqual(['Old Mill Commons is restored and Active.']);
      expect(ctx.reload).toHaveBeenCalled();
    });

    it('a community archived while Onboarding returns to Onboarding (US3-S13)', async () => {
      const r = await setup({ settings: ARCHIVED, preview: allowed('restore', { returnsTo: 'Onboarding', reason: 'NeverActivated', membershipsRegainingAccess: [] }) });
      await request(r, 'restore');
      const dlg = dialog('Restore Old Mill Commons?');
      expect(within(dlg).getByText("Old Mill Commons returns to Onboarding, because it had never been Active before it was archived. Residents won't see it until it is marked Active.")).toBeTruthy();
      expect(within(dlg).getByText('No memberships will give access again.')).toBeTruthy();
      company.restore.and.resolveTo({ status: 'Onboarding', reason: 'NeverActivated' });
      fireEvent.click(screen.getByRole('button', { name: 'Restore community' }));
      await settle(r);
      expect(toasts()).toEqual(['Old Mill Commons is restored. It stays Onboarding until it has a Community Manager with no end date.']);
    });

    it('with no open-ended manager it returns to Onboarding (US3-S14)', async () => {
      const r = await setup({ settings: settings({ status: 'Archived', communityName: 'Briar Hollow' }), preview: RETURNS_ONBOARDING });
      await request(r, 'restore');
      const text = dialog('Restore Briar Hollow?').textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('Briar Hollow returns to Onboarding, because it has no Community Manager with no end date. It can be marked Active once it has one.');
      expect(text).toContain('It appears again in the community switchers and My Communities of the people above, marked Onboarding.');
      expect(text).toContain("Residents can't see their homes, including payment history, until it is marked Active.");
    });

    it('ends a membership first, inside the dialog, then shows the refreshed restore view (US3-S15, D5, 8.04.u)', async () => {
      const r = await setup({ settings: ARCHIVED, preview: RETURNS_ACTIVE });
      await request(r, 'restore');
      fireEvent.click(screen.getByRole('button', { name: "End Morgan Ellis's Community Manager membership before restoring" }));
      await settle(r);

      const step = dialog("End Morgan Ellis's Community Manager membership first?");
      const today = formatTodayUtc();
      const text = step.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain(`It ends today, ${today}.`);
      expect(text).toContain("Morgan Ellis won't get access back when Old Mill Commons is restored.");
      expect(text).toContain('Old Mill Commons will then have no Community Manager with no end date, so it returns to Onboarding when restored.');
      expect(text).toContain("Morgan Ellis's account and other memberships don't change.");
      expect(company.restore).not.toHaveBeenCalled();

      svc.updateMembership.and.resolveTo({} as never);
      company.getLifecyclePreview.and.resolveTo(RETURNS_ONBOARDING);
      fireEvent.click(within(step).getByRole('button', { name: 'End membership' }));
      await settle(r);
      expect(svc.updateMembership).toHaveBeenCalledOnceWith('c-om', 'm-morgan', { end: true });
      expect(company.getLifecyclePreview).toHaveBeenCalledTimes(2);

      const dlg = dialog('Restore Old Mill Commons?');
      expect(within(dlg).getByRole('status').textContent!.trim()).toBe("Morgan Ellis's Community Manager membership ended.");
      expect(within(dlg).getByText('Old Mill Commons returns to Onboarding, because it has no Community Manager with no end date. It can be marked Active once it has one.')).toBeTruthy();
      expect(within(dlg).getAllByRole('button', { name: /before restoring$/ }).length).toBe(4);
      expect(lines(within(dlg).getByRole('list', { name: 'These memberships have ended and stay ended:' })))
        .toEqual([`Morgan Ellis · Community Manager · Ended ${today}`]);
    });

    it('"Back" returns to the restore view without ending anything; a Board Member has no last-manager line', async () => {
      const r = await setup({ settings: ARCHIVED, preview: RETURNS_ACTIVE });
      await request(r, 'restore');
      fireEvent.click(screen.getByRole('button', { name: "End Rowan Hayes's Board Member membership before restoring" }));
      await settle(r);
      const step = dialog("End Rowan Hayes's Board Member membership first?");
      expect(step.textContent).not.toContain('returns to Onboarding when restored');
      fireEvent.click(within(step).getByRole('button', { name: 'Back' }));
      await settle(r);
      expect(dialog('Restore Old Mill Commons?')).toBeTruthy();
      expect(svc.updateMembership).not.toHaveBeenCalled();
    });

    it('says when ending the membership failed', async () => {
      const r = await setup({ settings: ARCHIVED, preview: RETURNS_ACTIVE });
      await request(r, 'restore');
      fireEvent.click(screen.getByRole('button', { name: "End Rowan Hayes's Board Member membership before restoring" }));
      await settle(r);
      svc.updateMembership.and.rejectWith(httpError(0));
      fireEvent.click(screen.getByRole('button', { name: 'End membership' }));
      await settle(r);
      expect(screen.getByText("Couldn't end the membership. Check your connection and try again.")).toBeTruthy();
      expect(dialog("End Rowan Hayes's Board Member membership first?")).toBeTruthy();
    });

    it('refuses while the parent is archived, with a link to the parent (US3-S16, 8.04.v)', async () => {
      const r = await setup({
        settings: settings({ id: 'c-omv', communityName: 'Old Mill Commons Villas', status: 'Archived', parent: { id: 'c-om', communityName: 'Old Mill Commons' } }),
        preview: refused('restore', 'PARENT_ARCHIVED'),
      });
      await request(r, 'restore');
      const dlg = dialog('Restore Old Mill Commons Villas?');
      expect(within(dlg).getByText("Old Mill Commons Villas can't be restored while its parent, Old Mill Commons, is archived. Restore Old Mill Commons first.")).toBeTruthy();
      expect(within(dlg).getByRole('link', { name: 'Open Old Mill Commons' }).getAttribute('href')).toBe('/app/company/communities/c-om/settings/profile');
      expect(within(dlg).queryByRole('list')).toBeNull();
      expect(within(dlg).queryByRole('button', { name: 'Restore community' })).toBeNull();
    });

    it('says when restoring failed for a network reason', async () => {
      const r = await setup({ settings: ARCHIVED, preview: RETURNS_ACTIVE });
      await request(r, 'restore');
      company.restore.and.rejectWith(httpError(0));
      fireEvent.click(screen.getByRole('button', { name: 'Restore community' }));
      await settle(r);
      expect(screen.getByText("Couldn't restore Old Mill Commons. Check your connection and try again.")).toBeTruthy();
    });
  });
});
