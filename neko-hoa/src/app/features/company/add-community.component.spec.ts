import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { AddCommunityComponent } from './add-community.component';
import { CompanyService, PortfolioItem } from '../../core/services/company.service';
import { BoardService } from '../../core/services/board.service';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { CurrentUser } from '../../core/models';

// 030 T073 / US1 / brief 03: the Add community form, for a Company Administrator (company area) and
// a Community Manager (board mode).

function row(id: string, communityName: string, status: PortfolioItem['status']): PortfolioItem {
  return { id, communityName, legalName: `${communityName} HOA, Inc.`, status, parent: null, managers: [], pendingManagerInvitations: [], noManager: false, differsFromDefaults: false };
}

const PORTFOLIO = [
  row('c-kcm', 'Keystone Crossing Master', 'Active'),
  row('c-kr', 'Keystone Ridge', 'Onboarding'),
  row('c-omc', 'Old Mill Commons', 'Archived'),
  row('c-sh', 'Sakura Heights', 'Active'),
];

const ROBIN: CurrentUser = {
  id: 'u-robin', firstName: 'Robin', lastName: 'Hale', email: 'robin@example.com', initials: 'RH', lastActiveMode: 'Resident',
  memberships: [], properties: [], isCompanyAdministrator: true, landing: 'Company',
};
const JORDAN: CurrentUser = {
  id: 'u-jordan', firstName: 'Jordan', lastName: 'Blake', email: 'jordan@example.com', initials: 'JB', lastActiveMode: 'Board',
  memberships: [], properties: [], isCompanyAdministrator: false, landing: 'Board',
};

describe('AddCommunityComponent (030 T073)', () => {
  let company: jasmine.SpyObj<CompanyService>;
  let board: jasmine.SpyObj<BoardService>;
  let refreshCurrentUser: jasmine.Spy;
  let navigate: jasmine.Spy;

  async function setup(variant: 'company' | 'board', user: CurrentUser = variant === 'company' ? ROBIN : JORDAN) {
    company = jasmine.createSpyObj<CompanyService>('CompanyService', ['getCommunities', 'createCommunity']);
    company.getCommunities.and.resolveTo({ items: PORTFOLIO, total: 4, limit: 100, offset: 0, counts: { all: 4, onboarding: 1, active: 2, archived: 1, noManager: 0 } });
    board = jasmine.createSpyObj<BoardService>('BoardService', ['getMyCommunities']);
    board.getMyCommunities.and.resolveTo({
      items: [
        { id: 'c-kcm', communityName: 'Keystone Crossing Master', role: 'CommunityManager', status: 'Active' },
        { id: 'c-sh', communityName: 'Sakura Heights', role: 'BoardMember', status: 'Active' },
        { id: 'c-kr', communityName: 'Keystone Ridge', role: 'CommunityManager', status: 'Onboarding' },
      ],
      total: 3, limit: 100, offset: 0,
    });
    refreshCurrentUser = jasmine.createSpy('refreshCurrentUser').and.resolveTo(user);
    const r = await render(AddCommunityComponent, {
      componentInputs: { variant },
      providers: [
        provideRouter([]),
        { provide: CompanyService, useValue: company },
        { provide: BoardService, useValue: board },
        { provide: AuthService, useValue: { user: signal(user).asReadonly(), refreshCurrentUser } },
      ],
    });
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    await r.fixture.whenStable();
    r.fixture.detectChanges();
    return r;
  }

  function type(label: string, value: string) {
    fireEvent.input(screen.getByLabelText(label), { target: { value } });
  }

  function fillValid() {
    type('Legal name (required)', 'Keystone Park HOA, Inc.');
    type('Community name (required)', 'Keystone Park');
    type('County (required)', 'Hamilton');
    type('Formation date (required)', '2004-03-14');
    type('Management start date (required)', '2026-11-01');
  }

  function submit() {
    fireEvent.click(screen.getByRole('button', { name: 'Add community' }));
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  describe('the form', () => {
    it('labels every field with (required) or (optional), with its helper text tied to it', async () => {
      await setup('company');
      for (const label of ['Legal name (required)', 'Community name (required)', 'County (required)', 'Formation date (required)',
        'Management start date (required)', 'Description (optional)']) {
        expect(screen.getByLabelText(label)).withContext(label).toBeTruthy();
      }
      expect(screen.getByRole('combobox', { name: 'Parent association (optional)' })).toBeTruthy();
      const legal = screen.getByLabelText('Legal name (required)');
      const help = document.getElementById(legal.getAttribute('aria-describedby')!.split(' ')[0])!;
      expect(help.textContent!.trim()).toBe('Up to 200 characters.');
      expect(screen.getByText('Architectural review settings are copied from the company defaults when you add the community. Later changes to the company defaults don\'t change them.')).toBeTruthy();
    });

    it('shows the company breadcrumb and intro, and no manager callout, for a Company Administrator', async () => {
      await setup('company');
      const crumbs = screen.getByRole('navigation', { name: 'Breadcrumb' });
      expect(within(crumbs).getByRole('link', { name: 'Portfolio' }).getAttribute('href')).toBe('/app/company/portfolio');
      expect(screen.getByRole('heading', { level: 1 }).textContent!.trim()).toBe('Add community');
      expect(screen.getByText(/It starts as Onboarding: residents can't see it until a Company Administrator marks it Active\. It has no Community Manager until one is appointed\./)).toBeTruthy();
      expect(screen.queryByText(/You'll be its first Community Manager/)).toBeNull();
    });

    it('tells a Community Manager they will be its first manager, with no breadcrumb', async () => {
      await setup('board');
      expect(screen.queryByRole('navigation', { name: 'Breadcrumb' })).toBeNull();
      expect(screen.getByText(/You'll be its first Community Manager, with no end date\./)).toBeTruthy();
    });

    it('lists every required error in a summary that takes focus, and under each field (US1-S9)', async () => {
      const r = await setup('company');
      submit();
      r.fixture.detectChanges();
      const summary = screen.getByRole('alert', { name: "The community wasn't added" });
      expect(within(summary).getAllByRole('link').map(a => a.textContent!.trim())).toEqual([
        'Legal name: Enter the legal name.',
        'Community name: Enter a community name.',
        'County: Enter the county.',
        'Formation date: Enter the formation date.',
        'Management start date: Enter the management start date.',
      ]);
      await until(() => document.activeElement === summary);
      const county = screen.getByLabelText('County (required)');
      expect(county.getAttribute('aria-invalid')).toBe('true');
      const error = screen.getByText('Enter the county.', { selector: '.field-error' });
      expect(county.getAttribute('aria-describedby')).toContain(error.id);
      expect(company.createCommunity).not.toHaveBeenCalled();
      fireEvent.click(within(summary).getByRole('link', { name: 'County: Enter the county.' }));
      expect(document.activeElement).toBe(county);
    });

    it('checks lengths and a formation date in the future', async () => {
      const r = await setup('company');
      fillValid();
      type('Legal name (required)', 'x'.repeat(201));
      type('Community name (required)', 'K');
      type('Formation date (required)', '2999-01-01');
      type('Description (optional)', 'd'.repeat(2001));
      submit();
      r.fixture.detectChanges();
      for (const message of ['Legal name can be at most 200 characters.', 'Community name must be 2 to 80 characters.',
        "Formation date can't be in the future.", 'Description can be at most 2,000 characters.']) {
        expect(screen.getByText(message, { selector: '.field-error' })).withContext(message).toBeTruthy();
      }
      expect(company.createCommunity).not.toHaveBeenCalled();
    });

    it('counts the description', async () => {
      const r = await setup('company');
      type('Description (optional)', 'Sakura Heights is a community of 120 homes in Wake County, NC.');
      r.fixture.detectChanges();
      expect(screen.getByText('62 / 2,000')).toBeTruthy();
      expect(screen.getByText('62 of 2,000 characters used')).toBeTruthy();
    });
  });

  describe('the parent picker (FR-016, US1-S8)', () => {
    async function openPicker(variant: 'company' | 'board') {
      const r = await setup(variant);
      const combo = screen.getByRole('combobox', { name: 'Parent association (optional)' });
      fireEvent.focus(combo);
      fireEvent.keyDown(combo, { key: 'ArrowDown', altKey: true });
      r.fixture.detectChanges();
      return { r, combo };
    }

    function options(): string[] {
      return within(screen.getByRole('listbox')).getAllByRole('option').map(o => o.textContent!.replace(/\s+/g, ' ').trim());
    }

    it('lists every non-archived community for a Company Administrator, A–Z, with "None" first', async () => {
      await openPicker('company');
      expect(options()).toEqual([
        'None — not a sub-association, ✓ Selected',
        'Keystone Crossing Master',
        'Keystone Ridge, Onboarding',
        'Sakura Heights',
      ]);
      expect(company.getCommunities).toHaveBeenCalledWith(jasmine.objectContaining({ limit: 100, offset: 0 }));
    });

    it('lists only the communities a Community Manager manages, from /me/communities', async () => {
      await openPicker('board');
      expect(options()).toEqual(['None — not a sub-association, ✓ Selected', 'Keystone Crossing Master', 'Keystone Ridge, Onboarding']);
      expect(board.getMyCommunities).toHaveBeenCalled();
      expect(company.getCommunities).not.toHaveBeenCalled();
    });

    it('filters as the person types, chooses with the keyboard, and Esc closes without changing it', async () => {
      const { r, combo } = await openPicker('company');
      fireEvent.input(combo, { target: { value: 'ridge' } });
      r.fixture.detectChanges();
      expect(options()).toEqual(['None — not a sub-association, ✓ Selected', 'Keystone Ridge, Onboarding']);
      expect(screen.getByText('1 community matches.')).toBeTruthy();
      fireEvent.keyDown(combo, { key: 'ArrowDown' });
      fireEvent.keyDown(combo, { key: 'ArrowDown' });
      r.fixture.detectChanges();
      expect(combo.getAttribute('aria-activedescendant')).toBe(within(screen.getByRole('listbox')).getAllByRole('option')[1].id);
      fireEvent.keyDown(combo, { key: 'Enter' });
      r.fixture.detectChanges();
      expect(screen.queryByRole('listbox')).toBeNull();
      expect((combo as HTMLInputElement).value).toBe('Keystone Ridge');
      expect(combo.getAttribute('aria-expanded')).toBe('false');

      fireEvent.keyDown(combo, { key: 'ArrowDown' });
      r.fixture.detectChanges();
      fireEvent.keyDown(combo, { key: 'Escape' });
      r.fixture.detectChanges();
      expect((combo as HTMLInputElement).value).toBe('Keystone Ridge');
    });

    it('says when nothing matches', async () => {
      const { r, combo } = await openPicker('board');
      fireEvent.input(combo, { target: { value: 'Oak' } });
      r.fixture.detectChanges();
      expect(screen.getByText('None of your communities match “Oak”.', { selector: '.cbp__empty' })).toBeTruthy();
      expect(screen.getByRole('status', { hidden: true }).textContent).toContain('None of your communities match “Oak”.');
    });
  });

  describe('saving', () => {
    it('submits the contract body, then routes to settingsPath with a toast (US1-S1)', async () => {
      const r = await setup('company');
      company.createCommunity.and.resolveTo({ id: 'c-kp', communityName: 'Keystone Park', status: 'Onboarding', settingsPath: '/app/company/communities/c-kp/settings/profile' });
      fillValid();
      const combo = screen.getByRole('combobox', { name: 'Parent association (optional)' });
      fireEvent.focus(combo);
      fireEvent.input(combo, { target: { value: 'master' } });
      r.fixture.detectChanges();
      fireEvent.click(within(screen.getByRole('listbox')).getByRole('option', { name: /Keystone Crossing Master/ }));
      submit();
      await until(() => navigate.calls.count() > 0);
      expect(company.createCommunity).toHaveBeenCalledOnceWith({
        legalName: 'Keystone Park HOA, Inc.', communityName: 'Keystone Park', county: 'Hamilton',
        formationDate: '2004-03-14', managementStartDate: '2026-11-01', description: null, parentCommunityId: 'c-kcm',
      });
      expect(navigate).toHaveBeenCalledWith(['/app/company/communities/c-kp/settings/profile']);
      expect(TestBed.inject(ToastService).toasts().map(t => t.message))
        .toEqual(["Keystone Park was added as Onboarding. Residents can't see it until it's marked Active."]);
    });

    it('as a Community Manager, refreshes the session and makes the new community active (US1-S4)', async () => {
      await setup('board');
      company.createCommunity.and.resolveTo({ id: 'c-kr2', communityName: 'Keystone Ridge West', status: 'Onboarding', settingsPath: '/app/board/settings/profile' });
      fillValid();
      type('Description (optional)', '  Townhomes.  ');
      submit();
      await until(() => navigate.calls.count() > 0);
      expect(company.createCommunity.calls.mostRecent().args[0].description).toBe('Townhomes.');
      expect(refreshCurrentUser).toHaveBeenCalled();
      expect(TestBed.inject(BoardNavigationService).activeCommunityId()).toBe('c-kr2');
      expect(navigate).toHaveBeenCalledWith(['/app/board/settings/profile']);
      expect(TestBed.inject(ToastService).toasts()[0].message).toBe("Keystone Ridge West was added. You're its Community Manager, with no end date.");
    });

    it('names the other community for a Company Administrator (US1-S2)', async () => {
      const r = await setup('company');
      company.createCommunity.and.rejectWith(new HttpErrorResponse({ status: 409, error: { code: 'COMMUNITY_NAME_TAKEN', message: 'Keystone Park is already the name of another community. Choose a different community name.' } }));
      fillValid();
      submit();
      await until(() => !!document.querySelector('.field-error'));
      r.fixture.detectChanges();
      const field = screen.getByLabelText('Community name (required)');
      expect(field.getAttribute('aria-invalid')).toBe('true');
      expect(screen.getByText('Keystone Park is already the name of another community. Choose a different community name.', { selector: '.field-error' })).toBeTruthy();
      expect(screen.getByRole('alert', { name: "The community wasn't added" })).toBeTruthy();
      expect((screen.getByLabelText('Legal name (required)') as HTMLInputElement).value).toBe('Keystone Park HOA, Inc.');
    });

    it('says only "That community name is unavailable." to a Community Manager (US1-S3)', async () => {
      const r = await setup('board');
      company.createCommunity.and.rejectWith(new HttpErrorResponse({ status: 409, error: { code: 'COMMUNITY_NAME_TAKEN', message: 'That community name is unavailable.' } }));
      fillValid();
      submit();
      await until(() => !!document.querySelector('.field-error'));
      r.fixture.detectChanges();
      expect(screen.getByText('That community name is unavailable.', { selector: '.field-error' })).toBeTruthy();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('puts a server validation error under its field, and an archived parent under the picker', async () => {
      const r = await setup('company');
      company.createCommunity.and.rejectWith(new HttpErrorResponse({ status: 422, error: { code: 'VALIDATION_ERROR', message: 'x', errors: [{ field: 'county', message: 'Enter the county.' }] } }));
      fillValid();
      submit();
      await until(() => !!screen.queryByText('Enter the county.', { selector: '.field-error' }));

      company.createCommunity.and.rejectWith(new HttpErrorResponse({ status: 409, error: { code: 'PARENT_ARCHIVED', message: 'Old Mill Commons is archived, so it can\'t be a parent.' } }));
      submit();
      await until(() => !!screen.queryByText("Old Mill Commons is archived, so it can't be a parent.", { selector: '.field-error' }));
      r.fixture.detectChanges();
      expect(screen.getByRole('combobox').getAttribute('aria-invalid')).toBe('true');
    });

    it('shows the permission banner on FORBIDDEN, the rate-limit line on 429, and the save-failed banner otherwise', async () => {
      const r = await setup('board');
      fillValid();
      company.createCommunity.and.rejectWith(new HttpErrorResponse({ status: 403, error: { code: 'FORBIDDEN', message: 'x' } }));
      submit();
      await until(() => !!screen.queryByText("You don't have permission to do that."));
      company.createCommunity.and.rejectWith(new HttpErrorResponse({ status: 429 }));
      submit();
      await until(() => !!screen.queryByText('Too many requests. Wait a few minutes, then try again.'));
      company.createCommunity.and.rejectWith(new HttpErrorResponse({ status: 0 }));
      submit();
      await until(() => !!screen.queryByText("Couldn't save. Check your connection and try again."));
      r.fixture.detectChanges();
      expect((screen.getByLabelText('County (required)') as HTMLInputElement).value).toBe('Hamilton');
    });

    it('Cancel goes back to the portfolio, or to board mode, without saving', async () => {
      await setup('company');
      fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
      expect(navigate).toHaveBeenCalledWith(['/app/company/portfolio']);
      expect(company.createCommunity).not.toHaveBeenCalled();
    });

    it('Cancel in board mode returns to the board landing', async () => {
      await setup('board');
      fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
      expect(navigate).toHaveBeenCalledWith(['/app/board/home']);
    });
  });
});
