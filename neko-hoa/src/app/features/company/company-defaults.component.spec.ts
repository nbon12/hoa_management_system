import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { CompanyDefaultsComponent } from './company-defaults.component';
import { CompanyDefaults, CompanyService } from '../../core/services/company.service';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { CurrentUser } from '../../core/models';

// 030 T125 / US4 / brief 08: the Company defaults page. One section, "Architectural review".
// Editable by a Company Administrator; read-only for a Community Manager in board mode. A change
// applies only to communities added afterwards: nothing is ever pushed to existing ones (FR-026).

const STATEMENT = "This is a formal disapproval of the plans as submitted under the community's governing documents. You may revise and resubmit.";

function defaults(over: Partial<CompanyDefaults> = {}): CompanyDefaults {
  return {
    reviewPeriodDays: 30, reminderDays: 7, lapseRule: 'FlagOverdueOnly', decisionRule: 'MajorityOfMembers',
    timeZoneId: 'America/New_York', formalDisapprovalStatement: STATEMENT, updatedAt: null, canEdit: true, ...over,
  };
}

const JORDAN: CurrentUser = {
  id: 'u-jordan', firstName: 'Jordan', lastName: 'Blake', email: 'jordan@example.com', initials: 'JB', lastActiveMode: 'Board',
  properties: [], isCompanyAdministrator: false, landing: 'Board',
  memberships: [{ communityId: 'c-sf', communityName: 'Keystone Crossing SF', role: 'CommunityManager', communityStatus: 'Active' }],
};

@Component({ selector: 'app-landing-stub', standalone: true, template: '<h1>Landing</h1>' })
class LandingStubComponent {}

function httpError(status: number, error: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('CompanyDefaultsComponent (030 T125)', () => {
  let company: jasmine.SpyObj<CompanyService>;

  async function setup(opts: { mode?: 'company' | 'board'; result?: CompanyDefaults | HttpErrorResponse } = {}) {
    company = jasmine.createSpyObj<CompanyService>('CompanyService', ['getDefaults', 'putDefaults']);
    const result = opts.result ?? defaults({ canEdit: opts.mode !== 'board' });
    if (result instanceof HttpErrorResponse) company.getDefaults.and.rejectWith(result);
    else company.getDefaults.and.resolveTo(result);
    const r = await render(CompanyDefaultsComponent, {
      componentInputs: { mode: opts.mode ?? 'company' },
      providers: [
        provideRouter([{ path: '**', component: LandingStubComponent }]),
        { provide: CompanyService, useValue: company },
        { provide: AuthService, useValue: { user: signal(JORDAN).asReadonly() } },
      ],
    });
    TestBed.inject(BoardNavigationService).setActiveCommunity('c-sf');
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

  async function edit(r: Awaited<ReturnType<typeof setup>>) {
    fireEvent.click(screen.getByRole('button', { name: 'Edit company defaults' }));
    await settle(r);
  }

  function field(label: string): HTMLInputElement {
    return screen.getByLabelText(label) as HTMLInputElement;
  }

  /** Opens the time zone dropdown, searches for `search` and chooses `zone`. */
  async function chooseTimeZone(r: Awaited<ReturnType<typeof setup>>, search: string, zone: string) {
    const tz = screen.getByRole('combobox', { name: 'Time zone' });
    fireEvent.click(tz);
    fireEvent.input(tz, { target: { value: search } });
    await settle(r);
    const option = within(screen.getByRole('listbox', { name: 'Time zone' })).getAllByRole('option')
      .find(o => o.textContent!.trim().startsWith(zone));
    expect(option).withContext(`${zone} is listed for “${search}”`).toBeDefined();
    fireEvent.click(option!);
    await settle(r);
  }

  function errorUnder(el: HTMLElement): string {
    return (el.getAttribute('aria-describedby') ?? '').split(/\s+/).map(id => document.getElementById(id))
      .filter(n => n?.classList.contains('field-error')).map(n => n!.textContent!.trim()).join(' ');
  }

  function toasts(): string[] {
    return TestBed.inject(ToastService).toasts().map(t => t.message);
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  describe('as a Company Administrator', () => {
    it('shows exactly one section, "Architectural review", with the note and the saved values (US4-S6, 8.08.a)', async () => {
      await setup();
      expect(screen.getByRole('heading', { level: 1, name: 'Company defaults' })).toBeTruthy();
      expect(screen.getByText('Every community added from now on starts with these settings. Each community can then change its own.')).toBeTruthy();
      expect(screen.getAllByRole('heading', { level: 2 }).map(h => h.textContent!.trim())).toEqual(['Architectural review']);
      expect(screen.queryByRole('navigation', { name: /sections/i })).toBeNull();
      const note = screen.getByRole('region', { name: 'Changes apply only to new communities' });
      expect(note.textContent).toContain('A change here applies only to communities added after you save it.');
      expect(note.textContent).toContain("A change can't be applied to existing communities.");

      const view = within(rules());
      expect(view.getByText('30')).toBeTruthy();
      expect(view.getByText('7')).toBeTruthy();
      expect(view.getByText('No automatic action')).toBeTruthy();
      expect(view.getByText('Majority of board members')).toBeTruthy();
      expect(view.getByText('America/New_York')).toBeTruthy();
      expect(view.getByText(`“${STATEMENT}”`)).toBeTruthy();
      expect(view.getByRole('button', { name: 'Edit company defaults' })).toBeTruthy();
      expect(view.queryByText('Read-only')).toBeNull();
      expect(screen.queryByRole('link', { name: /Back to/ })).toBeNull();
    });

    it('edits in one form and saves after the confirmation, which never offers to change existing communities (US4-S1, US4-S2, FR-026, 8.08.b–e)', async () => {
      const r = await setup();
      await edit(r);
      expect(screen.getByText('All fields are required.')).toBeTruthy();
      expect(field('Review period (days)').value).toBe('30');
      await until(() => document.activeElement === field('Review period (days)'));
      expect(screen.getByText('126 / 1,000')).toBeTruthy();

      const lapse = screen.getByLabelText('After the review period ends without a decision') as HTMLSelectElement;
      lapse.value = 'DeemedApproved';
      fireEvent.change(lapse);
      fireEvent.click(screen.getByRole('button', { name: 'Save company defaults' }));
      await settle(r);

      const dlg = screen.getByRole('dialog', { name: 'Save the company defaults?' });
      const changes = within(dlg).getByRole('list', { name: 'These settings change:' });
      expect(within(changes).getAllByRole('listitem').map(li => li.textContent!.replace(/\s+/g, ' ').trim()))
        .toEqual(['After the review period ends without a decision: No automatic action → Auto approve from No automatic action to Auto approve']);
      const text = dlg.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('Communities added from now on start with these settings.');
      expect(text).toContain('Existing communities whose settings now differ will show “Differs from defaults” in the portfolio.');
      expect(text).toContain('Existing communities keep their own architectural review settings, including communities that never changed them.');
      expect(text).toContain('Applications already received keep their rules.');
      // FR-026: no checkbox, toggle or second button for existing communities.
      expect(within(dlg).queryByRole('checkbox')).toBeNull();
      expect(within(dlg).queryByRole('switch')).toBeNull();
      expect(within(dlg).getAllByRole('button').map(b => b.textContent!.trim())).toEqual(['Save company defaults', 'Cancel']);

      company.putDefaults.and.resolveTo(defaults({ lapseRule: 'DeemedApproved' }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      expect(company.putDefaults).toHaveBeenCalledOnceWith({
        reviewPeriodDays: 30, lapseRule: 'DeemedApproved', decisionRule: 'MajorityOfMembers', reminderDays: 7,
        timeZoneId: 'America/New_York', formalDisapprovalStatement: STATEMENT,
      });
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(toasts()).toEqual(['Company defaults saved.']);
      expect(within(rules()).getByText('Auto approve')).toBeTruthy();
      await until(() => document.activeElement === screen.getByRole('button', { name: 'Edit company defaults' }));
    });

    it('shows each validation message in the summary and under its field, and opens no dialog (US4-S5, 8.08.c)', async () => {
      const r = await setup();
      await edit(r);
      fireEvent.input(field('Review period (days)'), { target: { value: '400' } });
      fireEvent.input(field('Reminder to the board (days before due, 0 = off)'), { target: { value: '31' } });
      fireEvent.input(field('Formal disapproval statement (included in every denial email)'), { target: { value: 'x'.repeat(1032) } });
      fireEvent.click(screen.getByRole('button', { name: 'Save company defaults' }));
      await settle(r);

      const summary = screen.getByRole('alert');
      expect(within(summary).getByRole('heading', { name: "The company defaults weren't saved" })).toBeTruthy();
      expect(within(summary).getAllByRole('link').map(a => a.textContent!.trim())).toEqual([
        'Review period must be between 1 and 365 days.', 'Reminder days must be between 0 and 30.', 'The statement can be at most 1,000 characters.',
      ]);
      await until(() => document.activeElement === summary);
      expect(errorUnder(field('Review period (days)'))).toBe('Review period must be between 1 and 365 days.');
      expect(errorUnder(field('Reminder to the board (days before due, 0 = off)'))).toBe('Reminder days must be between 0 and 30.');
      expect(field('Review period (days)').getAttribute('aria-invalid')).toBe('true');
      expect(screen.getByText('1,032 / 1,000')).toBeTruthy();
      expect(screen.getByText('The statement is over 1,000 characters.')).toBeTruthy();
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(company.putDefaults).not.toHaveBeenCalled();

      fireEvent.click(within(summary).getByRole('link', { name: 'Reminder days must be between 0 and 30.' }));
      expect(document.activeElement).toBe(field('Reminder to the board (days before due, 0 = off)'));
    });

    it('checks the time zone and the statement: a saved zone the browser does not recognize must be changed', async () => {
      const r = await setup({ result: defaults({ timeZoneId: 'Mars/Olympus_Mons' }) });
      await edit(r);
      expect(field('Time zone').value).toBe('Mars/Olympus_Mons');
      fireEvent.input(field('Formal disapproval statement (included in every denial email)'), { target: { value: '  ' } });
      fireEvent.click(screen.getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      expect(errorUnder(field('Time zone'))).toBe("Time zone isn't recognized. Choose one from the list.");
      expect(errorUnder(field('Formal disapproval statement (included in every denial email)'))).toBe('The formal disapproval statement is required.');
      expect(company.putDefaults).not.toHaveBeenCalled();

      await chooseTimeZone(r, 'denver', 'America/Denver');
      expect(errorUnder(field('Time zone'))).toBe('');
    });

    it('requires a time zone when none is saved, and typing alone never sets one', async () => {
      const r = await setup({ result: defaults({ timeZoneId: '' }) });
      await edit(r);
      const tz = field('Time zone');
      fireEvent.click(tz);
      fireEvent.input(tz, { target: { value: 'America/Chicago' } });
      fireEvent.blur(tz);
      await settle(r);
      expect(tz.value).toBe('');
      fireEvent.click(screen.getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      expect(errorUnder(field('Time zone'))).toBe('Time zone is required.');
      expect(field('Time zone').getAttribute('aria-invalid')).toBe('true');
      const summary = screen.getByRole('alert');
      fireEvent.click(within(summary).getByRole('link', { name: 'Time zone is required.' }));
      expect(document.activeElement).toBe(field('Time zone'));
    });

    it('sends nothing when no value changed, and Cancel returns to the read view', async () => {
      const r = await setup();
      await edit(r);
      fireEvent.click(screen.getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(company.putDefaults).not.toHaveBeenCalled();
      expect(screen.queryByLabelText('Review period (days)')).toBeNull();

      await edit(r);
      fireEvent.input(field('Review period (days)'), { target: { value: '45' } });
      fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
      await settle(r);
      expect(within(rules()).getByText('30')).toBeTruthy();
      await until(() => document.activeElement === screen.getByRole('button', { name: 'Edit company defaults' }));
    });

    it('a server validation refusal returns to the form with the message under its field', async () => {
      const r = await setup();
      await edit(r);
      await chooseTimeZone(r, 'chicago', 'America/Chicago');
      fireEvent.click(screen.getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: 'Save the company defaults?' });
      expect(within(within(dlg).getByRole('list', { name: 'These settings change:' })).getByRole('listitem').textContent).toContain('Time zone: America/New_York → America/Chicago');
      company.putDefaults.and.rejectWith(httpError(422, { code: 'VALIDATION_ERROR', message: 'Invalid.', errors: [{ field: 'timeZoneId', message: "Time zone isn't recognized. Use a name such as America/New_York." }] }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      expect(within(dlg).getByText("Time zone isn't recognized. Use a name such as America/New_York.")).toBeTruthy();
      fireEvent.click(within(dlg).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(errorUnder(field('Time zone'))).toBe("Time zone isn't recognized. Use a name such as America/New_York.");
      expect(field('Time zone').value).toBe('America/Chicago');
    });

    it('a changed statement shows the old and new text in full', async () => {
      const r = await setup();
      await edit(r);
      fireEvent.input(field('Formal disapproval statement (included in every denial email)'), { target: { value: 'New words.' } });
      fireEvent.click(screen.getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      const row = within(within(screen.getByRole('dialog')).getByRole('list', { name: 'These settings change:' })).getByRole('listitem');
      expect(row.textContent).toContain(`Formal disapproval statement (included in every denial email): “${STATEMENT}” → “New words.”`);
    });

    it('a save that fails, or is rate limited, stays in the dialog; FORBIDDEN offers "Close"', async () => {
      const r = await setup();
      await edit(r);
      fireEvent.input(field('Review period (days)'), { target: { value: '45' } });
      fireEvent.click(screen.getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: 'Save the company defaults?' });
      company.putDefaults.and.rejectWith(httpError(0));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      expect(within(dlg).getByText("Couldn't save. Check your connection and try again.")).toBeTruthy();

      company.putDefaults.and.rejectWith(httpError(429));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      expect(within(dlg).getByText('Too many requests. Wait a few minutes, then try again.')).toBeTruthy();

      company.putDefaults.and.rejectWith(httpError(403, { code: 'FORBIDDEN', message: 'No.' }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      expect(within(dlg).getByText("You don't have permission to do that.")).toBeTruthy();
      company.getDefaults.and.resolveTo(defaults({ canEdit: false }));
      fireEvent.click(within(dlg).getByRole('button', { name: 'Close' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(company.getDefaults).toHaveBeenCalledTimes(2);
      expect(within(rules()).queryByRole('button', { name: 'Edit company defaults' })).toBeNull();
    });

    it('Cancel in the dialog changes nothing and returns to the form', async () => {
      const r = await setup();
      await edit(r);
      fireEvent.input(field('Review period (days)'), { target: { value: '45' } });
      fireEvent.click(screen.getByRole('button', { name: 'Save company defaults' }));
      await settle(r);
      fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancel' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(company.putDefaults).not.toHaveBeenCalled();
      expect(field('Review period (days)').value).toBe('45');
    });

    it('says when the defaults could not load, and tries again', async () => {
      const r = await setup({ result: httpError(0) });
      expect(screen.getByText("Couldn't load the company defaults.")).toBeTruthy();
      company.getDefaults.and.resolveTo(defaults());
      fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
      await settle(r);
      expect(within(rules()).getByText('30')).toBeTruthy();
    });

    it('a page refusal goes to the landing with the page message', async () => {
      await setup({ result: httpError(403, { code: 'FORBIDDEN', message: 'No.' }) });
      expect(toasts()).toContain("You don't have access to that page.");
      expect(TestBed.inject(Router).url).toMatch(/^\/app\/board\//);
    });
  });

  describe('as a Community Manager in board mode', () => {
    it('is read-only, with no Save, no inputs and no disabled controls (US4-S3, FR-027, 8.08.f)', async () => {
      await setup({ mode: 'board' });
      expect(screen.getAllByRole('heading', { level: 2 }).map(h => h.textContent!.trim())).toEqual(['Architectural review']);
      const card = rules();
      expect(within(card).getByText('Read-only')).toBeTruthy();
      expect(within(card).getByText('You can read the company defaults. Only a Company Administrator can change them.')).toBeTruthy();
      expect(within(card).queryAllByRole('button')).toEqual([]);
      expect(screen.queryByRole('textbox')).toBeNull();
      expect(screen.queryByRole('spinbutton')).toBeNull();
      expect(screen.queryByRole('combobox')).toBeNull();
      expect(within(card).getByText('No automatic action')).toBeTruthy();
      const note = screen.getByRole('region', { name: 'Changes apply only to new communities' });
      expect(note.textContent).toContain('A change here applies only to communities added afterwards. Existing communities, including yours, keep their own settings');
    });

    it('offers "Back to architectural review" for the active community', async () => {
      await setup({ mode: 'board' });
      const back = screen.getByRole('link', { name: "Back to Keystone Crossing SF's architectural review" });
      expect(back.textContent!.trim()).toBe('Back to architectural review');
      expect(back.getAttribute('href')).toBe('/app/board/settings/architectural-review');
    });

    it('stays read-only even when the server says it may edit', async () => {
      await setup({ mode: 'board', result: defaults({ canEdit: true }) });
      expect(screen.queryByRole('button', { name: 'Edit company defaults' })).toBeNull();
    });
  });
});
