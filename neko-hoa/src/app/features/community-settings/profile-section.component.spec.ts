import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { ProfileSectionComponent } from './profile-section.component';
import { CommunitySettingsPageComponent } from './community-settings-page.component';
import { CommunitySettingsContext, SettingsMode } from './community-settings-context';
import { CommunityProfile, CommunitySettings, CommunitySettingsService } from '../../core/services/community-settings.service';
import { CompanyService, PortfolioItem } from '../../core/services/company.service';
import { BoardService } from '../../core/services/board.service';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { ViewportService } from '../../core/services/viewport.service';
import { CurrentUser } from '../../core/models';

// 030 T102 / US3 / brief 04 §4.2: the Profile section saves one field at a time (FR-017), flags
// empty required fields, and shows the status and the parent, which only a Company Administrator
// can change.

const NONE = { allowed: false, reasonCode: null };

function settings(over: Partial<CommunitySettings> = {}, can: Partial<CommunitySettings['can']> = {}): CommunitySettings {
  return {
    id: 'c-sh', communityName: 'Sakura Heights', legalName: 'Sakura Heights Homeowners Association, Inc.', status: 'Active',
    parent: null, noManager: false, missingRequiredFields: [], sections: ['profile', 'managers-and-memberships', 'architectural-review'],
    can: {
      editProfile: true, changeParent: true, activate: NONE, archive: { allowed: true, reasonCode: null }, restore: NONE,
      manageManagers: true, replaceLastManager: true, manageMemberships: true, manageResidents: true, editArcSettings: true,
      endMembershipsWhileArchived: false, ...can,
    },
    ...over,
  };
}

const MANAGER_CAN = { changeParent: false, activate: NONE, archive: NONE, restore: NONE, replaceLastManager: false };

function profile(over: Partial<CommunityProfile> = {}): CommunityProfile {
  return {
    legalName: 'Sakura Heights Homeowners Association, Inc.', communityName: 'Sakura Heights', county: 'Wake County, NC',
    formationDate: '1998-06-09', managementStartDate: '2016-07-01', description: null, missingRequiredFields: [], ...over,
  };
}

function portfolioItem(id: string, communityName: string, status: PortfolioItem['status'], parent: PortfolioItem['parent']): PortfolioItem {
  return {
    id, communityName, legalName: `${communityName} HOA`, status, parent, managers: [], pendingManagerInvitations: [],
    noManager: false, differsFromDefaults: false,
  };
}

function httpError(status: number, error: unknown = null): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('ProfileSectionComponent (030 T102)', () => {
  let svc: jasmine.SpyObj<CommunitySettingsService>;
  let company: jasmine.SpyObj<CompanyService>;
  let ctx: CommunitySettingsContext;

  async function setup(opts: { settings?: CommunitySettings; profile?: CommunityProfile | HttpErrorResponse; mode?: SettingsMode; portfolio?: PortfolioItem[] } = {}) {
    svc = jasmine.createSpyObj<CommunitySettingsService>('CommunitySettingsService', ['getProfile', 'patchProfile']);
    const p = opts.profile ?? profile();
    if (p instanceof HttpErrorResponse) svc.getProfile.and.rejectWith(p);
    else svc.getProfile.and.resolveTo(p);
    company = jasmine.createSpyObj<CompanyService>('CompanyService', ['getCommunities', 'changeParent']);
    const items = opts.portfolio ?? [];
    company.getCommunities.and.resolveTo({
      items, total: items.length, limit: 100, offset: 0, counts: { all: items.length, onboarding: 0, active: items.length, archived: 0, noManager: 0 },
    });
    ctx = new CommunitySettingsContext();
    ctx.settings.set(opts.settings ?? settings());
    ctx.mode.set(opts.mode ?? 'company');
    spyOn(ctx, 'reload').and.resolveTo();
    const r = await render(ProfileSectionComponent, {
      providers: [
        provideRouter([]),
        { provide: CommunitySettingsService, useValue: svc },
        { provide: CompanyService, useValue: company },
        { provide: BoardService, useValue: jasmine.createSpyObj('BoardService', ['getMyCommunities']) },
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

  function field(label: RegExp | string): HTMLInputElement | HTMLTextAreaElement {
    return screen.getByLabelText(label) as HTMLInputElement | HTMLTextAreaElement;
  }

  async function type(r: Awaited<ReturnType<typeof setup>>, el: HTMLInputElement | HTMLTextAreaElement, value: string) {
    el.focus();
    fireEvent.focus(el);
    el.value = value;
    fireEvent.input(el);
    if (el instanceof HTMLInputElement && el.type === 'date') fireEvent.change(el);
    else fireEvent.blur(el);
    await settle(r);
  }

  function errorUnder(el: HTMLElement): HTMLElement | null {
    const ids = (el.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(Boolean);
    return ids.map(id => document.getElementById(id)).find(n => n?.getAttribute('role') === 'alert') ?? null;
  }

  function toasts(): string[] {
    return TestBed.inject(ToastService).toasts().map(t => t.message);
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  it('shows the six fields as inputs for someone who may edit, with the section heading (8.04.a)', async () => {
    await setup();
    expect(screen.getByRole('heading', { level: 2, name: 'Profile' }).hasAttribute('data-section-heading')).toBeTrue();
    expect(field('Legal name (required)').value).toBe('Sakura Heights Homeowners Association, Inc.');
    expect(field('Community name (required)').value).toBe('Sakura Heights');
    expect(field('County (required)').value).toBe('Wake County, NC');
    expect(field('Formation date (required)').value).toBe('1998-06-09');
    expect(field('Management start date (required)').value).toBe('2016-07-01');
    expect(field('Description (optional)').value).toBe('');
    expect(field('County (required)').getAttribute('data-setting-id')).toBe('county');
    expect(screen.getByText('0 / 2,000')).toBeTruthy();
    expect(screen.queryByRole('button', { name: /save/i })).toBeNull();
    expect(svc.getProfile).toHaveBeenCalledOnceWith('c-sh');
  });

  it('saves one field when the person leaves it and shows "✓ Saved" beside its label (US3-S17, 8.04.c)', async () => {
    const r = await setup({ mode: 'board', settings: settings({}, MANAGER_CAN) });
    const description = 'Sakura Heights is a community of 120 homes in Wake County, NC.';
    svc.patchProfile.and.resolveTo(profile({ description }));
    await type(r, field('Description (optional)'), description);

    expect(svc.patchProfile).toHaveBeenCalledOnceWith('c-sh', { description });
    const label = screen.getByText('Description (optional)');
    const status = label.nextElementSibling as HTMLElement;
    expect(status.getAttribute('role')).toBe('status');
    expect(status.textContent).toContain('✓ Saved');
    expect(within(status).getByText('Description saved').classList).toContain('sr-only');
    expect(screen.getByText('62 / 2,000')).toBeTruthy();
    expect(ctx.reload).not.toHaveBeenCalled();
  });

  it('a field that fails to save shows its error under it and keeps what was typed; the others are unaffected (US3-S17, 8.04.d)', async () => {
    const r = await setup({ mode: 'board', settings: settings({}, MANAGER_CAN) });
    svc.patchProfile.and.rejectWith(httpError(0));
    const description = field('Description (optional)');
    await type(r, description, 'Townhomes.');

    expect(description.value).toBe('Townhomes.');
    expect(description.getAttribute('aria-invalid')).toBe('true');
    const error = errorUnder(description)!;
    expect(error.textContent).toContain("Couldn't save. Check your connection and try again.");
    expect(within(error).getByRole('button', { name: 'Try again' })).toBeTruthy();
    expect(screen.getAllByRole('alert').length).toBe(1);
    expect(field('County (required)').hasAttribute('aria-invalid')).toBeFalse();

    svc.patchProfile.and.resolveTo(profile({ description: 'Townhomes.' }));
    fireEvent.click(within(error).getByRole('button', { name: 'Try again' }));
    await settle(r);
    expect(svc.patchProfile).toHaveBeenCalledTimes(2);
    expect(errorUnder(description)).toBeNull();
  });

  it('refuses to clear a required field that has a value, without saving it (FR-017, 8.04.e)', async () => {
    const r = await setup({ mode: 'board', settings: settings({}, MANAGER_CAN) });
    const county = field('County (required)');
    await type(r, county, '   ');

    expect(svc.patchProfile).not.toHaveBeenCalled();
    expect(errorUnder(county)!.textContent!.trim()).toBe("County is required and can't be left empty.");
    expect(within(errorUnder(county)!).queryByRole('button', { name: 'Try again' })).toBeNull();
    fireEvent.keyDown(county, { key: 'Escape' });
    await settle(r);
    expect(county.value).toBe('Wake County, NC');
    expect(errorUnder(county)).toBeNull();
  });

  it('checks lengths and the formation date before saving', async () => {
    const r = await setup();
    await type(r, field('Community name (required)'), 'K');
    expect(errorUnder(field('Community name (required)'))!.textContent!.trim()).toBe('Community name must be 2 to 80 characters.');
    await type(r, field('Formation date (required)'), '2999-01-01');
    expect(errorUnder(field('Formation date (required)'))!.textContent!.trim()).toBe("Formation date can't be in the future.");
    expect(svc.patchProfile).not.toHaveBeenCalled();
  });

  it('trims a text field, and saving the name or legal name re-reads the page header', async () => {
    const r = await setup();
    svc.patchProfile.and.resolveTo(profile({ communityName: 'Sakura Heights East' }));
    await type(r, field('Community name (required)'), '  Sakura Heights East ');
    expect(svc.patchProfile).toHaveBeenCalledOnceWith('c-sh', { communityName: 'Sakura Heights East' });
    expect(ctx.reload).toHaveBeenCalledTimes(1);
    expect(field('Community name (required)').value).toBe('Sakura Heights East');
  });

  it('sends a cleared description as null', async () => {
    const r = await setup({ profile: profile({ description: 'Old text' }) });
    svc.patchProfile.and.resolveTo(profile({ description: null }));
    await type(r, field('Description (optional)'), '');
    expect(svc.patchProfile).toHaveBeenCalledOnceWith('c-sh', { description: null });
  });

  it("a taken name reads \"That community name is unavailable.\" for a Community Manager and isn't saved (US3-S18, 8.04.g)", async () => {
    const r = await setup({ mode: 'board', settings: settings({ communityName: 'Keystone Ridge', status: 'Onboarding' }, MANAGER_CAN), profile: profile({ communityName: 'Keystone Ridge' }) });
    svc.patchProfile.and.rejectWith(httpError(409, { code: 'COMMUNITY_NAME_TAKEN', message: 'Keystone Park is already the name of another community. Choose a different community name.' }));
    const name = field('Community name (required)');
    await type(r, name, ' KEYSTONE PARK');

    expect(errorUnder(name)!.textContent!.trim()).toBe('That community name is unavailable.');
    expect(document.body.textContent).not.toContain('Keystone Park is already');
    expect(ctx.reload).not.toHaveBeenCalled();
  });

  it('a Company Administrator sees which community has the name', async () => {
    const r = await setup();
    svc.patchProfile.and.rejectWith(httpError(409, { code: 'COMMUNITY_NAME_TAKEN', message: 'Keystone Park is already the name of another community. Choose a different community name.' }));
    await type(r, field('Community name (required)'), 'Keystone Park');
    expect(errorUnder(field('Community name (required)'))!.textContent!.trim())
      .toBe('Keystone Park is already the name of another community. Choose a different community name.');
  });

  it('shows the server\'s field message for a VALIDATION_ERROR', async () => {
    const r = await setup();
    svc.patchProfile.and.rejectWith(httpError(400, { code: 'VALIDATION_ERROR', message: 'Invalid.', errors: [{ field: 'county', message: 'County can be at most 100 characters.' }] }));
    await type(r, field('County (required)'), 'Somewhere');
    expect(errorUnder(field('County (required)'))!.textContent!.trim()).toBe('County can be at most 100 characters.');
  });

  it('a save in a community archived in another tab shows the banner and re-reads the page', async () => {
    const r = await setup();
    svc.patchProfile.and.rejectWith(httpError(409, { code: 'COMMUNITY_ARCHIVED', message: 'Archived.' }));
    await type(r, field('County (required)'), 'Durham County, NC');
    expect(screen.getAllByText('Sakura Heights is archived, so it can\'t be changed. Restore it to make changes.').length).toBeGreaterThan(0);
    expect(ctx.reload).toHaveBeenCalled();
  });

  it('flags missing required fields with a notice and per field, and other fields still save (US3-S19, 8.04.h)', async () => {
    const missing = profile({
      legalName: null, communityName: 'Keystone Crossing TH', county: null, formationDate: null, managementStartDate: null,
      description: null, missingRequiredFields: ['legalName', 'county', 'formationDate', 'managementStartDate'],
    });
    const r = await setup({ mode: 'board', settings: settings({ communityName: 'Keystone Crossing TH', legalName: null }, MANAGER_CAN), profile: missing });

    expect(screen.getByText('4 required fields are empty: Legal name, County, Formation date and Management start date. Add them when you have them.')).toBeTruthy();
    expect(screen.getAllByText('Missing — required').length).toBe(4);
    expect(screen.getAllByText("Still empty. You can save other changes without it. Once it has a value, it can't be cleared.").length).toBe(4);
    const legal = field('Legal name (required)');
    expect(legal.getAttribute('aria-describedby')).toContain('pf-legalName-missing');

    const description = 'Townhome sub-association of Keystone Crossing. 168 homes.';
    svc.patchProfile.and.resolveTo({ ...missing, description });
    await type(r, field('Description (optional)'), description);
    expect(svc.patchProfile).toHaveBeenCalledOnceWith('c-sh', { description });
    expect(screen.getByText('Description (optional)').nextElementSibling!.textContent).toContain('✓ Saved');
    expect(screen.getAllByText('Missing — required').length).toBe(4);
  });

  it('words the notice for one missing field, and re-reads the page once it is filled', async () => {
    const r = await setup({ profile: profile({ county: null, missingRequiredFields: ['county'] }) });
    expect(screen.getByText('1 required field is empty: County. Add it when you have it.')).toBeTruthy();
    svc.patchProfile.and.resolveTo(profile({ county: 'Wake County, NC', missingRequiredFields: [] }));
    await type(r, field('County (required)'), 'Wake County, NC');
    expect(screen.queryByText(/required field is empty/)).toBeNull();
    expect(ctx.reload).toHaveBeenCalledTimes(1);
  });

  it('shows the status and parent as plain text for a Community Manager (US5-S2, 8.04.c)', async () => {
    await setup({ mode: 'board', settings: settings({ parent: { id: 'c-kcm', communityName: 'Keystone Crossing Master' } }, MANAGER_CAN) });
    const card = screen.getByRole('region', { name: 'Status and parent' });
    expect(within(card).getByText('Live. Residents can use their homes.')).toBeTruthy();
    expect(within(card).getByText('Keystone Crossing Master')).toBeTruthy();
    expect(within(card).queryByRole('button')).toBeNull();
    expect(within(card).getByText('Only a Company Administrator can change the status or the parent association.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Mark .* Active|Archive|Restore/ })).toBeNull();
    expect(company.getCommunities).not.toHaveBeenCalled();
  });

  it('gives a Company Administrator "Change parent" and the sub-associations (8.04.a, 8.04.o)', async () => {
    await setup({
      settings: settings({ id: 'c-kcm', communityName: 'Keystone Crossing Master' }),
      portfolio: [
        portfolioItem('c-sf', 'Keystone Crossing SF', 'Active', { id: 'c-kcm', communityName: 'Keystone Crossing Master' }),
        portfolioItem('c-th', 'Keystone Crossing TH', 'Active', { id: 'c-kcm', communityName: 'Keystone Crossing Master' }),
        portfolioItem('c-sh', 'Sakura Heights', 'Active', null),
      ],
    });
    const card = screen.getByRole('region', { name: 'Status and parent' });
    expect(within(card).getByText('None')).toBeTruthy();
    expect(within(card).getByRole('button', { name: 'Change parent of Keystone Crossing Master' })).toBeTruthy();
    const subs = within(card).getByRole('list', { name: 'Sub-associations' });
    expect(within(subs).getAllByRole('listitem').map(li => li.textContent!.replace(/\s+/g, ' ').trim()))
      .toEqual(['Keystone Crossing SF · Active', 'Keystone Crossing TH · Active']);
    expect(within(card).queryByText(/Only a Company Administrator/)).toBeNull();
  });

  it('shows the Onboarding sentence', async () => {
    await setup({ settings: settings({ status: 'Onboarding' }) });
    expect(screen.getByText('Being set up. Hidden from residents. Resident invitations are scheduled and sent when it is marked Active.')).toBeTruthy();
  });

  it('shows an archived community read-only, with no "Change parent" (8.04.p)', async () => {
    await setup({ settings: settings({ status: 'Archived', communityName: 'Old Mill Commons' }, { editProfile: false }), profile: profile({ communityName: 'Old Mill Commons', description: null }) });
    expect(screen.queryByRole('textbox')).toBeNull();
    expect(screen.getByText('Jun 9, 1998')).toBeTruthy();
    expect(screen.getByText('No description')).toBeTruthy();
    expect(screen.getByText('No longer managed by the management company. Read-only.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Change parent/ })).toBeNull();
  });

  it('reads an empty required field as missing in the read view', async () => {
    await setup({ settings: settings({ status: 'Archived' }, { editProfile: false }), profile: profile({ county: null, missingRequiredFields: ['county'] }) });
    const value = screen.getByText('missing, required').closest('dd')!;
    expect(value.getAttribute('data-setting-id')).toBe('county');
  });

  it('says when the profile could not load, and tries again', async () => {
    const r = await setup({ profile: httpError(0) });
    expect(screen.getByText("Couldn't load the profile.")).toBeTruthy();
    svc.getProfile.and.resolveTo(profile());
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await settle(r);
    expect(field('County (required)').value).toBe('Wake County, NC');
  });

  describe('Change parent (FR-016, 8.04.w, 8.04.x)', () => {
    const MASTER = settings({ id: 'c-kcm', communityName: 'Keystone Crossing Master' });
    const PORTFOLIO = [
      portfolioItem('c-th', 'Keystone Crossing TH', 'Active', { id: 'c-kcm', communityName: 'Keystone Crossing Master' }),
      portfolioItem('c-sh', 'Sakura Heights', 'Active', null),
      portfolioItem('c-om', 'Old Mill Commons', 'Archived', null),
      portfolioItem('c-kcm', 'Keystone Crossing Master', 'Active', null),
    ];

    async function openDialog() {
      const r = await setup({ settings: MASTER, portfolio: PORTFOLIO });
      fireEvent.click(screen.getByRole('button', { name: 'Change parent of Keystone Crossing Master' }));
      await settle(r);
      return r;
    }

    function dialog(): HTMLElement {
      return screen.getByRole('dialog', { name: 'Change the parent of Keystone Crossing Master?' });
    }

    function openPicker(combo: HTMLElement) {
      fireEvent.focus(combo);
      fireEvent.keyDown(combo, { key: 'ArrowDown', altKey: true });
    }

    async function choose(r: Awaited<ReturnType<typeof setup>>, name: string) {
      openPicker(within(dialog()).getByRole('combobox', { name: 'New parent association' }));
      await settle(r);
      fireEvent.click(within(dialog()).getByRole('option', { name: new RegExp(`^${name}`) }));
      await settle(r);
    }

    it('lists "No parent" first and every non-archived community but this one, A–Z (D7)', async () => {
      const r = await openDialog();
      expect(within(dialog()).getByText('Current parent association: None.')).toBeTruthy();
      openPicker(within(dialog()).getByRole('combobox', { name: 'New parent association' }));
      await settle(r);
      expect(within(dialog()).getAllByRole('option').map(o => o.textContent!.split(',')[0].trim()))
        .toEqual(['No parent', 'Keystone Crossing TH', 'Sakura Heights']);
      expect(within(dialog()).getByText("Archived communities can't be a parent, so they aren't listed.")).toBeTruthy();
      expect(within(dialog()).getByText('No one gains or loses access. Access is set for each community on its own, never through the parent.')).toBeTruthy();
    });

    it('refuses a loop under the field, and the person can choose again (PARENT_CYCLE)', async () => {
      const r = await openDialog();
      await choose(r, 'Keystone Crossing TH');
      company.changeParent.and.rejectWith(httpError(409, { code: 'PARENT_CYCLE', message: 'Cycle.' }));
      fireEvent.click(within(dialog()).getByRole('button', { name: 'Change parent' }));
      await settle(r);
      expect(company.changeParent).toHaveBeenCalledOnceWith('c-kcm', 'c-th');
      expect(within(dialog()).getByText("Keystone Crossing TH can't be the parent of Keystone Crossing Master, because Keystone Crossing TH is already under Keystone Crossing Master. Choose a different parent.")).toBeTruthy();

      company.changeParent.and.resolveTo({});
      await choose(r, 'Sakura Heights');
      fireEvent.click(within(dialog()).getByRole('button', { name: 'Change parent' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
      expect(toasts()).toContain('Keystone Crossing Master is now a sub-association of Sakura Heights.');
      expect(ctx.reload).toHaveBeenCalled();
    });

    it('an archived parent from an out-of-date list is refused, and the list reloads', async () => {
      const r = await openDialog();
      await choose(r, 'Sakura Heights');
      company.changeParent.and.rejectWith(httpError(409, { code: 'PARENT_ARCHIVED', message: 'Archived.' }));
      fireEvent.click(within(dialog()).getByRole('button', { name: 'Change parent' }));
      await settle(r);
      expect(within(dialog()).getByText("Sakura Heights is archived, so it can't be a parent.")).toBeTruthy();
      expect(company.getCommunities.calls.count()).toBeGreaterThan(2);
    });

    it('asks for a different choice when nothing changed, and Cancel closes', async () => {
      const r = await openDialog();
      fireEvent.click(within(dialog()).getByRole('button', { name: 'Change parent' }));
      await settle(r);
      expect(company.changeParent).not.toHaveBeenCalled();
      expect(within(dialog()).getByText('Choose a new parent association.')).toBeTruthy();
      fireEvent.click(within(dialog()).getByRole('button', { name: 'Cancel' }));
      await settle(r);
      expect(screen.queryByRole('dialog')).toBeNull();
    });

    it('removing the parent says so', async () => {
      const r = await setup({ settings: settings({ id: 'c-th', communityName: 'Keystone Crossing TH', parent: { id: 'c-kcm', communityName: 'Keystone Crossing Master' } }), portfolio: PORTFOLIO });
      fireEvent.click(screen.getByRole('button', { name: 'Change parent of Keystone Crossing TH' }));
      await settle(r);
      const dlg = screen.getByRole('dialog', { name: 'Change the parent of Keystone Crossing TH?' });
      expect(within(dlg).getByText('Current parent association: Keystone Crossing Master.')).toBeTruthy();
      openPicker(within(dlg).getByRole('combobox', { name: 'New parent association' }));
      await settle(r);
      fireEvent.click(within(dlg).getByRole('option', { name: /^No parent/ }));
      await settle(r);
      company.changeParent.and.resolveTo({});
      fireEvent.click(within(dlg).getByRole('button', { name: 'Change parent' }));
      await settle(r);
      expect(company.changeParent).toHaveBeenCalledOnceWith('c-th', null);
      expect(toasts()).toContain('Keystone Crossing TH no longer has a parent association.');
    });

    it('says when it is rate limited, and shows other refusals at the top', async () => {
      const r = await openDialog();
      await choose(r, 'Sakura Heights');
      company.changeParent.and.rejectWith(httpError(429));
      fireEvent.click(within(dialog()).getByRole('button', { name: 'Change parent' }));
      await settle(r);
      expect(within(dialog()).getByText('Too many requests. Wait a few minutes, then try again.')).toBeTruthy();
      company.changeParent.and.rejectWith(httpError(403, { code: 'FORBIDDEN', message: 'Forbidden.' }));
      fireEvent.click(within(dialog()).getByRole('button', { name: 'Change parent' }));
      await settle(r);
      expect(within(dialog()).getByText("You don't have permission to do that.")).toBeTruthy();
    });

    it('says when the change failed for a network reason', async () => {
      const r = await openDialog();
      await choose(r, 'Sakura Heights');
      company.changeParent.and.rejectWith(httpError(0));
      fireEvent.click(within(dialog()).getByRole('button', { name: 'Change parent' }));
      await settle(r);
      expect(within(dialog()).getByText("Couldn't change the parent. Check your connection and try again.")).toBeTruthy();
    });
  });
});

// US3-S4 / brief 04 8.04.l: the status actions sit in the page header (brief 04 §4.1), above the
// Profile section. Mark Active is disabled with its reason on hover and on keyboard focus.
@Component({ standalone: true, template: '<h1>Landing</h1>' })
class LandingStubComponent {}

describe('Profile page header: Mark Active disabled with its reason (030 T102, US3-S4)', () => {
  async function open() {
    const svc = jasmine.createSpyObj<CommunitySettingsService>('CommunitySettingsService', ['getSettings', 'getProfile']);
    svc.getSettings.and.resolveTo(settings({ id: 'c-kp', communityName: 'Keystone Park', status: 'Onboarding', noManager: true },
      { activate: { allowed: false, reasonCode: 'NO_ACTIVE_MANAGER' } }));
    svc.getProfile.and.resolveTo(profile({ communityName: 'Keystone Park' }));
    const me: CurrentUser = {
      id: 'u-robin', firstName: 'Robin', lastName: 'Hale', email: 'robin@example.com', initials: 'RH', lastActiveMode: 'Resident',
      memberships: [], properties: [], isCompanyAdministrator: true, landing: 'Company',
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'app/company/communities/:communityId/settings/:section', component: CommunitySettingsPageComponent, data: { settingsMode: 'company' } },
          { path: '**', component: LandingStubComponent },
        ]),
        { provide: CommunitySettingsService, useValue: svc },
        { provide: CompanyService, useValue: jasmine.createSpyObj('CompanyService', ['getCommunities', 'getLifecyclePreview']) },
        { provide: AuthService, useValue: { user: signal(me).asReadonly() } },
        { provide: ViewportService, useValue: { isNarrow: signal(false).asReadonly() } },
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/app/company/communities/c-kp/settings/profile');
    await harness.fixture.whenStable();
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
    return harness;
  }

  it('shows the reason on hover and on keyboard focus, and keeps the button in the Tab order (8.04.l)', async () => {
    await open();
    expect(screen.getByRole('heading', { level: 2, name: 'Profile' })).toBeTruthy();
    const mark = screen.getByRole('button', { name: 'Mark Keystone Park Active' });
    expect(mark.getAttribute('aria-disabled')).toBe('true');
    expect(mark.hasAttribute('disabled')).toBeFalse();
    const tip = document.getElementById(mark.getAttribute('aria-describedby')!.split(' ').pop()!)!;
    expect(tip.textContent!.trim()).toBe("Keystone Park can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted.");
    expect(tip.hidden).toBeTrue();

    fireEvent.mouseEnter(mark);
    expect(tip.hidden).toBeFalse();
    fireEvent.mouseLeave(mark);
    await until(() => tip.hidden);

    mark.focus();
    fireEvent.focus(mark);
    expect(tip.hidden).toBeFalse();
    fireEvent.blur(mark);
    await until(() => tip.hidden);
    expect(screen.getByRole('button', { name: 'Archive Keystone Park' }).getAttribute('aria-disabled')).toBeNull();
  });
});
