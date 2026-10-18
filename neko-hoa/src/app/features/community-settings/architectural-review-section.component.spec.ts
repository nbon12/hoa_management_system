import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { ArchitecturalReviewSectionComponent } from './architectural-review-section.component';
import { CommunitySettingsContext, SettingsMode } from './community-settings-context';
import { CommunitySettings, CommunitySettingsService, SettingsArcSettings } from '../../core/services/community-settings.service';
import { ArcRuleFields } from '../../core/services/company.service';
import { ToastService } from '../../core/services/toast.service';

// 030 T130 / US5 / brief 07: spec 027's ARC settings, moved into Community settings as the
// Architectural review section. Each differing field is flagged against the company defaults, the
// "Company defaults" link opens that page (FR-027), and a reset copies the defaults (FR-031).

const NONE = { allowed: false, reasonCode: null };
const STATEMENT = "This is a formal disapproval of the plans as submitted under the community's governing documents. You may revise and resubmit.";

const DEFAULTS: ArcRuleFields = {
  reviewPeriodDays: 30, reminderDays: 7, lapseRule: 'FlagOverdueOnly', decisionRule: 'MajorityOfMembers',
  timeZoneId: 'America/New_York', formalDisapprovalStatement: STATEMENT,
};

function arc(over: Partial<SettingsArcSettings> = {}): SettingsArcSettings {
  return {
    ...DEFAULTS, reviewPeriodDays: 45, reminderDays: 10, updatedAt: '2026-09-01T12:00:00Z',
    companyDefaults: DEFAULTS, differsFromDefaults: ['reviewPeriodDays', 'reminderDays'], canEdit: true, ...over,
  };
}

const MATCHING = arc({ reviewPeriodDays: 30, reminderDays: 7, differsFromDefaults: [] });

function settings(over: Partial<CommunitySettings> = {}): CommunitySettings {
  return {
    id: 'c-sf', communityName: 'Keystone Crossing SF', legalName: 'Keystone Crossing SF Association, Inc.', status: 'Active',
    parent: { id: 'c-kcm', communityName: 'Keystone Crossing Master' }, noManager: false, missingRequiredFields: [],
    sections: ['profile', 'managers-and-memberships', 'architectural-review'],
    can: {
      editProfile: true, changeParent: true, activate: NONE, archive: { allowed: true, reasonCode: null }, restore: NONE,
      manageManagers: true, replaceLastManager: true, manageMemberships: true, manageResidents: true, editArcSettings: true,
      endMembershipsWhileArchived: false,
    },
    ...over,
  };
}

function httpError(status: number, error: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('ArchitecturalReviewSectionComponent (030 T130)', () => {
  let svc: jasmine.SpyObj<CommunitySettingsService>;
  let ctx: CommunitySettingsContext;

  async function setup(opts: { result?: SettingsArcSettings | HttpErrorResponse; mode?: SettingsMode; settings?: CommunitySettings } = {}) {
    svc = jasmine.createSpyObj<CommunitySettingsService>('CommunitySettingsService', ['getArcSettings', 'putArcSettings', 'resetArc']);
    const result = opts.result ?? arc();
    if (result instanceof HttpErrorResponse) svc.getArcSettings.and.rejectWith(result);
    else svc.getArcSettings.and.resolveTo(result);
    ctx = new CommunitySettingsContext();
    ctx.settings.set(opts.settings ?? settings());
    ctx.mode.set(opts.mode ?? 'company');
    spyOn(ctx, 'reload').and.resolveTo();
    const r = await render(ArchitecturalReviewSectionComponent, {
      providers: [
        provideRouter([]),
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

  function rules(): HTMLElement {
    return screen.getByRole('region', { name: 'Review rules' });
  }

  function valueOf(settingId: string): HTMLElement {
    return rules().querySelector<HTMLElement>(`dd[data-setting-id="${settingId}"]`)!;
  }

  function field(label: string): HTMLInputElement {
    return screen.getByLabelText(label) as HTMLInputElement;
  }

  function toasts(): string[] {
    return TestBed.inject(ToastService).toasts().map(t => t.message);
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  describe('the read view', () => {
    it('shows the rules with "Differs from defaults" and the company default for each differing field (US5-S1, 8.07.a)', async () => {
      await setup();
      expect(svc.getArcSettings).toHaveBeenCalledOnceWith('c-sf');
      const heading = screen.getByRole('heading', { level: 2, name: 'Architectural review' });
      expect(heading.hasAttribute('data-section-heading')).toBeTrue();
      expect(screen.getByText('Set these to match your governing documents. Changes apply to applications received from now on.')).toBeTruthy();
      expect(screen.getByText('2 settings differ from the company defaults: Review period and Reminder to the board.')).toBeTruthy();

      const period = valueOf('review-period');
      expect(within(period).getByText('45')).toBeTruthy();
      expect(within(period).getByText('Differs from defaults')).toBeTruthy();
      expect(within(period).getByText('Company default: 30')).toBeTruthy();
      expect(within(valueOf('reminder-days')).getByText('Company default: 7')).toBeTruthy();
      for (const id of ['lapse-rule', 'decision-rule', 'time-zone', 'formal-disapproval-statement']) {
        expect(within(valueOf(id)).queryByText('Differs from defaults')).withContext(id).toBeNull();
      }
      expect(within(valueOf('lapse-rule')).getByText('Flag as overdue only')).toBeTruthy();
      expect(within(valueOf('formal-disapproval-statement')).getByText(`“${STATEMENT}”`)).toBeTruthy();
      expect(within(rules()).getByRole('button', { name: 'Edit architectural review' })).toBeTruthy();
    });

    it('links a Company Administrator to the editable Company defaults page (FR-027, D6)', async () => {
      await setup();
      const link = screen.getByRole('link', { name: 'Company defaults' });
      expect(link.getAttribute('href')).toBe('/app/company/defaults');
      expect(screen.getByText("New communities start with these. Changing them never changes this community's settings or applications already received.")).toBeTruthy();
    });

    it("links a Community Manager to the read-only Company defaults page (US4-S3, 8.07.b)", async () => {
      await setup({ mode: 'board' });
      const link = screen.getByRole('link', { name: 'Company defaults' });
      expect(link.getAttribute('href')).toBe('/app/board/company-defaults');
      expect(screen.getByText("New communities start with these. You can read them; only a Company Administrator can change them. Changing them never changes this community's settings.")).toBeTruthy();
      expect(screen.getByRole('button', { name: 'Edit architectural review' })).toBeTruthy();
    });

    it('says when everything matches, and keeps Reset disabled with its reason beside it (D4, 8.07.c)', async () => {
      await setup({ result: MATCHING, mode: 'board' });
      expect(screen.getByText('These settings match the company defaults.')).toBeTruthy();
      expect(screen.queryByText('Differs from defaults')).toBeNull();
      const reset = screen.getByRole('button', { name: 'Reset architectural review to company defaults' });
      expect(reset.getAttribute('aria-disabled')).toBe('true');
      const reason = document.getElementById(reset.getAttribute('aria-describedby')!.split(' ')[0])!;
      expect(reason.textContent!.trim()).toBe('These settings already match the company defaults.');
      fireEvent.click(reset);
      expect(screen.queryByRole('dialog')).toBeNull();
    });

    it('words one differing field', async () => {
      await setup({ result: arc({ reviewPeriodDays: 30, reminderDays: 7, timeZoneId: 'America/Chicago', differsFromDefaults: ['timeZoneId'] }) });
      expect(screen.getByText('1 setting differs from the company defaults: Time zone.')).toBeTruthy();
    });

    it('is read-only in an Archived community: no Edit, no Reset, no Company defaults link (US5-S7, 8.04.r)', async () => {
      await setup({ settings: settings({ status: 'Archived' }), result: arc({ canEdit: false }) });
      expect(within(rules()).queryByRole('button')).toBeNull();
      expect(screen.queryByRole('button', { name: /Reset/ })).toBeNull();
      expect(screen.queryByRole('link', { name: 'Company defaults' })).toBeNull();
      expect(screen.queryByRole('textbox')).toBeNull();
      expect(within(valueOf('review-period')).getByText('45')).toBeTruthy();
    });

    it('says when the settings could not load, and tries again', async () => {
      const r = await setup({ result: httpError(0) });
      expect(screen.getByText("Couldn't load the architectural review settings.")).toBeTruthy();
      svc.getArcSettings.and.resolveTo(arc());
      fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
      await settle(r);
      expect(within(valueOf('review-period')).getByText('45')).toBeTruthy();
    });
  });

  describe('the form (moved from 027)', () => {
    async function edit(r: Awaited<ReturnType<typeof setup>>) {
      fireEvent.click(screen.getByRole('button', { name: 'Edit architectural review' }));
      await settle(r);
    }

    it('opens one form with each field\'s company default, and hides the reset (D1, D3, 8.07.d)', async () => {
      const r = await setup({ mode: 'board' });
      await edit(r);
      expect(screen.getByText('All fields are required.')).toBeTruthy();
      expect(field('Review period (days)').value).toBe('45');
      expect(screen.getByText("1 to 365. An application is due this many days after it's received. Company default: 30.")).toBeTruthy();
      expect(screen.getByText('0 to 30. 0 turns reminders off. Company default: 7.')).toBeTruthy();
      expect(screen.getByText('Company default: Flag as overdue only.')).toBeTruthy();
      expect(screen.getByText("A time zone name, such as America/New_York. Used for due dates and for the expiry times on this community's invitations. Company default: America/New_York.")).toBeTruthy();
      expect(screen.queryByRole('button', { name: /Reset/ })).toBeNull();
      await until(() => document.activeElement === field('Review period (days)'));
    });

    it('saves the six settings with one Save, then shows the read view again (027 US6-S18)', async () => {
      const r = await setup();
      await edit(r);
      fireEvent.input(field('Review period (days)'), { target: { value: '60' } });
      const decision = screen.getByLabelText('How a decision is reached') as HTMLSelectElement;
      decision.value = 'MajorityOfVotesCastWithQuorum';
      fireEvent.change(decision);
      svc.putArcSettings.and.resolveTo(arc({ reviewPeriodDays: 60, decisionRule: 'MajorityOfVotesCastWithQuorum', differsFromDefaults: ['reviewPeriodDays', 'reminderDays', 'decisionRule'] }));
      fireEvent.click(screen.getByRole('button', { name: 'Save architectural review' }));
      await settle(r);
      expect(svc.putArcSettings).toHaveBeenCalledOnceWith('c-sf', {
        reviewPeriodDays: 60, lapseRule: 'FlagOverdueOnly', decisionRule: 'MajorityOfVotesCastWithQuorum', reminderDays: 10,
        timeZoneId: 'America/New_York', formalDisapprovalStatement: STATEMENT,
      });
      expect(toasts()).toEqual(['Architectural review saved.']);
      expect(within(valueOf('review-period')).getByText('60')).toBeTruthy();
      expect(within(valueOf('decision-rule')).getByText('Differs from defaults')).toBeTruthy();
      expect(screen.getByText('3 settings differ from the company defaults: Review period, Reminder to the board and How a decision is reached.')).toBeTruthy();
      await until(() => document.activeElement === screen.getByRole('button', { name: 'Edit architectural review' }));
    });

    it('validates ranges and required text before saving (US5-S4 form, 8.07.e)', async () => {
      const r = await setup();
      await edit(r);
      fireEvent.input(field('Review period (days)'), { target: { value: '0' } });
      fireEvent.input(field('Reminder to the board (days before due, 0 = off)'), { target: { value: '31' } });
      fireEvent.input(field('Formal disapproval statement (included in every denial email)'), { target: { value: 'y'.repeat(1032) } });
      fireEvent.click(screen.getByRole('button', { name: 'Save architectural review' }));
      await settle(r);
      const summary = screen.getByRole('alert');
      expect(within(summary).getByRole('heading', { name: "The architectural review settings weren't saved" })).toBeTruthy();
      expect(within(summary).getAllByRole('link').length).toBe(3);
      expect(svc.putArcSettings).not.toHaveBeenCalled();
    });

    it("shows the server's field message, and the save failure, on the form", async () => {
      const r = await setup();
      await edit(r);
      fireEvent.input(field('Time zone'), { target: { value: 'America/Chicago' } });
      svc.putArcSettings.and.rejectWith(httpError(422, { code: 'VALIDATION_ERROR', message: 'Invalid.', errors: [{ field: 'TimeZoneId', message: "Time zone isn't recognized. Use a name such as America/New_York." }] }));
      fireEvent.click(screen.getByRole('button', { name: 'Save architectural review' }));
      await settle(r);
      await until(() => !!screen.queryByRole('heading', { name: "The architectural review settings weren't saved" }));
      expect(field('Time zone').getAttribute('aria-invalid')).toBe('true');

      svc.putArcSettings.and.rejectWith(httpError(0));
      fireEvent.input(field('Time zone'), { target: { value: 'America/Denver' } });
      fireEvent.click(screen.getByRole('button', { name: 'Save architectural review' }));
      await settle(r);
      expect(screen.getByText("Couldn't save. Check your connection and try again.")).toBeTruthy();
      expect(field('Time zone').value).toBe('America/Denver');
    });

    it('a save in a community archived in another tab shows the banner and re-reads the page', async () => {
      const r = await setup();
      await edit(r);
      fireEvent.input(field('Review period (days)'), { target: { value: '50' } });
      svc.putArcSettings.and.rejectWith(httpError(409, { code: 'COMMUNITY_ARCHIVED', message: 'Archived.' }));
      fireEvent.click(screen.getByRole('button', { name: 'Save architectural review' }));
      await settle(r);
      expect(screen.getByText("Keystone Crossing SF is archived, so it can't be changed. Restore it to make changes.")).toBeTruthy();
      expect(ctx.reload).toHaveBeenCalled();
    });

    it('Cancel drops changes; Save with nothing changed sends nothing', async () => {
      const r = await setup();
      await edit(r);
      fireEvent.input(field('Review period (days)'), { target: { value: '99' } });
      fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
      await settle(r);
      expect(within(valueOf('review-period')).getByText('45')).toBeTruthy();
      await edit(r);
      fireEvent.click(screen.getByRole('button', { name: 'Save architectural review' }));
      await settle(r);
      expect(svc.putArcSettings).not.toHaveBeenCalled();
      expect(screen.queryByLabelText('Review period (days)')).toBeNull();
    });
  });

  describe('reset to company defaults (FR-031)', () => {
    async function openReset(r: Awaited<ReturnType<typeof setup>>) {
      fireEvent.click(screen.getByRole('button', { name: 'Reset architectural review to company defaults' }));
      await settle(r);
      return screen.getByRole('dialog', { name: "Reset Keystone Crossing SF's architectural review to the company defaults?" });
    }

    it('confirms first, listing only what changes, then resets (US5-S5, 8.07.f, 8.07.g)', async () => {
      const r = await setup();
      expect(screen.getByText("Sets every rule above to the current company defaults. Applications already received keep their rules, and application numbering doesn't change.")).toBeTruthy();
      const dlg = await openReset(r);
      // The dialog asks the server again, for the current defaults (Q2).
      expect(svc.getArcSettings).toHaveBeenCalledTimes(2);
      const changes = within(dlg).getByRole('list', { name: 'These settings change:' });
      expect(within(changes).getAllByRole('listitem').map(li => li.textContent!.replace(/\s+/g, ' ').trim())).toEqual([
        'Review period (days): 45 → 30 from 45 to 30',
        'Reminder to the board (days before due, 0 = off): 10 → 7 from 10 to 7',
      ]);
      const text = dlg.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain("These already match and don't change: When the review period ends without a decision, How a decision is reached, Time zone and Formal disapproval statement.");
      expect(text).toContain('The new settings apply to applications received from now on.');
      expect(text).toContain("Applications already received keep their rules. Their due dates, lapse rule and decision rule don't change.");
      expect(text).toContain("Application numbering doesn't change. The next application gets the number it would have had.");

      svc.resetArc.and.resolveTo(MATCHING);
      fireEvent.click(within(dlg).getByRole('button', { name: 'Reset architectural review' }));
      await settle(r);
      expect(svc.resetArc).toHaveBeenCalledOnceWith('c-sf');
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(toasts()).toEqual(['Architectural review reset to the company defaults.']);
      expect(screen.getByText('These settings match the company defaults.')).toBeTruthy();
      expect(within(valueOf('review-period')).getByText('30')).toBeTruthy();
      await until(() => document.activeElement === screen.getByRole('heading', { level: 2, name: 'Architectural review' }));
    });

    it('a dialog that cannot load says so, with the primary disabled', async () => {
      const r = await setup();
      svc.getArcSettings.and.rejectWith(httpError(0));
      const dlg = await openReset(r);
      expect(within(dlg).getByText("Couldn't load what this will change.")).toBeTruthy();
      expect((within(dlg).getByRole('button', { name: 'Reset architectural review' }) as HTMLButtonElement).disabled).toBeTrue();
      svc.getArcSettings.and.resolveTo(arc());
      fireEvent.click(within(dlg).getByRole('button', { name: 'Try again' }));
      await settle(r);
      expect((within(dlg).getByRole('button', { name: 'Reset architectural review' }) as HTMLButtonElement).disabled).toBeFalse();
    });

    it('says when the reset failed, or was refused; Cancel changes nothing', async () => {
      const r = await setup();
      const dlg = await openReset(r);
      svc.resetArc.and.rejectWith(httpError(0));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Reset architectural review' }));
      await settle(r);
      expect(within(dlg).getByText("Couldn't reset the architectural review. Check your connection and try again.")).toBeTruthy();

      svc.resetArc.and.rejectWith(httpError(429));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Reset architectural review' }));
      await settle(r);
      expect(within(dlg).getByText('Too many requests. Wait a few minutes, then try again.')).toBeTruthy();

      svc.resetArc.and.rejectWith(httpError(403, { code: 'FORBIDDEN', message: 'No.' }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Reset architectural review' }));
      await settle(r);
      expect(within(dlg).getByText("You don't have permission to do that.")).toBeTruthy();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(ctx.reload).toHaveBeenCalled();

      const again = await openReset(r);
      fireEvent.click(within(again).getByRole('button', { name: 'Cancel' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
    });
  });
});
