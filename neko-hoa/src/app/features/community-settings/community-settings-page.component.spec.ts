import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { screen, fireEvent, within } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { CommunitySettingsPageComponent } from './community-settings-page.component';
import { CommunitySettingsContext } from './community-settings-context';
import { CommunitySettings, CommunitySettingsService } from '../../core/services/community-settings.service';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { ViewportService } from '../../core/services/viewport.service';
import { CurrentUser } from '../../core/models';

// 030 T067 / US5 / brief 04 §4.1: the Community settings page shell. Sections come from the
// server's `sections[]`, controls follow `can.*`, a 403 sends the person to their landing, and
// the side list's search opens a section and focuses the chosen setting.

@Component({ standalone: true, template: '<h1>Landing</h1>' })
class LandingStubComponent {}

const NONE = { allowed: false, reasonCode: null };

function settings(over: Partial<CommunitySettings> = {}, can: Partial<CommunitySettings['can']> = {}): CommunitySettings {
  return {
    id: 'c-kp', communityName: 'Keystone Park', legalName: 'Keystone Park HOA, Inc.', status: 'Onboarding',
    parent: null, noManager: true, missingRequiredFields: [],
    sections: ['profile', 'managers-and-memberships', 'architectural-review'],
    can: {
      editProfile: true, changeParent: true,
      activate: { allowed: false, reasonCode: 'NO_ACTIVE_MANAGER' },
      archive: { allowed: true, reasonCode: null },
      restore: NONE,
      manageManagers: true, replaceLastManager: true, manageMemberships: true,
      manageResidents: true, editArcSettings: true, endMembershipsWhileArchived: false,
      ...can,
    },
    ...over,
  };
}

/** What a Community Manager gets (US5-S2): no lifecycle, no parent change, no "No manager". */
const MANAGER_CAN = { changeParent: false, activate: NONE, archive: NONE, restore: NONE, replaceLastManager: false };

function user(over: Partial<CurrentUser> = {}): CurrentUser {
  return {
    id: 'u1', firstName: 'Jordan', lastName: 'Blake', email: 'jordan@example.com', initials: 'JB',
    lastActiveMode: 'Board', properties: [], isCompanyAdministrator: false, landing: 'Board',
    memberships: [{ communityId: 'c-kr', communityName: 'Keystone Ridge', role: 'CommunityManager', communityStatus: 'Onboarding' }],
    ...over,
  };
}

describe('CommunitySettingsPageComponent (030 T067)', () => {
  let svc: jasmine.SpyObj<CommunitySettingsService>;
  const userSig = signal<CurrentUser | null>(null);
  const narrow = signal(false);

  async function open(url: string, opts: { result?: CommunitySettings | HttpErrorResponse; signedIn?: CurrentUser; isNarrow?: boolean; activeCommunity?: string | null } = {}) {
    svc = jasmine.createSpyObj<CommunitySettingsService>('CommunitySettingsService', ['getSettings']);
    const result = opts.result ?? settings();
    if (result instanceof HttpErrorResponse) svc.getSettings.and.rejectWith(result);
    else svc.getSettings.and.resolveTo(result);
    userSig.set(opts.signedIn ?? user({ isCompanyAdministrator: true, landing: 'Company', memberships: [] }));
    narrow.set(opts.isNarrow ?? false);

    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'app/company/communities/:communityId/settings/:section', component: CommunitySettingsPageComponent, data: { settingsMode: 'company' } },
          { path: 'app/board/settings/:section', component: CommunitySettingsPageComponent, data: { settingsMode: 'board' } },
          { path: '**', component: LandingStubComponent },
        ]),
        { provide: CommunitySettingsService, useValue: svc },
        { provide: AuthService, useValue: { user: userSig.asReadonly() } },
        { provide: ViewportService, useValue: { isNarrow: narrow.asReadonly() } },
      ],
    });
    if (opts.activeCommunity !== undefined) TestBed.inject(BoardNavigationService).setActiveCommunity(opts.activeCommunity);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    await settle(harness);
    return harness;
  }

  async function settle(harness: RouterTestingHarness) {
    await harness.fixture.whenStable();
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  function url(): string {
    return TestBed.inject(Router).url;
  }

  function context(harness: RouterTestingHarness): CommunitySettingsContext {
    return harness.routeDebugElement!.injector.get(CommunitySettingsContext);
  }

  function toasts(): string[] {
    return TestBed.inject(ToastService).toasts().map(t => t.message);
  }

  afterEach(() => TestBed.inject(ToastService).clear());

  describe('as a Company Administrator (company area)', () => {
    const URL = '/app/company/communities/c-kp/settings/profile';

    it('loads the community named in the address and renders the header (8.04.a)', async () => {
      await open(URL, { result: settings({ parent: { id: 'c-kcm', communityName: 'Keystone Crossing Master' } }) });
      expect(svc.getSettings).toHaveBeenCalledOnceWith('c-kp');

      const crumbs = screen.getByRole('navigation', { name: 'Breadcrumb' });
      expect(within(crumbs).getByRole('link', { name: 'Portfolio' }).getAttribute('href')).toBe('/app/company/portfolio');
      expect(within(crumbs).getByText('Keystone Park').getAttribute('aria-current')).toBe('page');

      const h1 = screen.getByRole('heading', { level: 1 });
      expect(h1.textContent!.replace(/\s+/g, ' ').trim()).toBe('Keystone Park, Onboarding, No manager');
      expect(screen.getByText('Keystone Park HOA, Inc.')).toBeTruthy();
      expect(screen.getByText('Sub-association of Keystone Crossing Master')).toBeTruthy();
    });

    it('flags a missing legal name in words', async () => {
      await open(URL, { result: settings({ legalName: null }) });
      expect(screen.getByText('Legal name: Missing — required')).toBeTruthy();
    });

    it('renders the sections from sections[] and no others (US5-S1, FR-029)', async () => {
      await open(URL, { result: settings({ sections: ['profile', 'architectural-review', 'notifications'] }) });
      const nav = screen.getByRole('navigation', { name: 'Community settings sections' });
      expect(within(nav).getAllByRole('link').map(a => a.textContent!.trim())).toEqual(['Profile', 'Architectural review']);
      expect(screen.queryByText(/Notifications/)).toBeNull();
      expect(screen.getByRole('heading', { level: 2, name: 'Profile' })).toBeTruthy();
    });

    it('opens the section the address names', async () => {
      await open('/app/company/communities/c-kp/settings/architectural-review');
      expect(screen.getByRole('heading', { level: 2, name: 'Architectural review' })).toBeTruthy();
      const nav = screen.getByRole('navigation', { name: 'Community settings sections' });
      expect(within(nav).getByRole('link', { name: 'Architectural review' }).getAttribute('aria-current')).toBe('page');
    });

    it('sends an unknown or hidden section to the first section', async () => {
      const h = await open('/app/company/communities/c-kp/settings/notifications');
      await until(() => url() === URL);
      await settle(h);
      expect(screen.getByRole('heading', { level: 2, name: 'Profile' })).toBeTruthy();
    });

    it('shows Mark Active disabled with its reason, and Archive, for an Onboarding community (8.04.l)', async () => {
      const h = await open(URL);
      const mark = screen.getByRole('button', { name: 'Mark Keystone Park Active' });
      expect(mark.getAttribute('aria-disabled')).toBe('true');
      const tip = document.getElementById(mark.getAttribute('aria-describedby')!.split(' ').pop()!)!;
      expect(tip.textContent!.trim()).toBe("Keystone Park can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted.");
      fireEvent.click(mark);
      expect(context(h).lifecycleRequest()).toBeNull();

      fireEvent.click(screen.getByRole('button', { name: 'Archive Keystone Park' }));
      expect(context(h).lifecycleRequest()).toEqual({ action: 'archive', seq: 1 });
      expect(screen.queryByRole('button', { name: /^Restore/ })).toBeNull();
    });

    it('shows Mark Active enabled when it is allowed', async () => {
      const h = await open(URL, { result: settings({ noManager: false }, { activate: { allowed: true, reasonCode: null } }) });
      const mark = screen.getByRole('button', { name: 'Mark Keystone Park Active' });
      expect(mark.hasAttribute('aria-disabled')).toBeFalse();
      fireEvent.click(mark);
      expect(context(h).lifecycleRequest()?.action).toBe('activate');
      expect(screen.getByRole('heading', { level: 1 }).textContent).not.toContain('No manager');
    });

    it('shows only Archive for an Active community', async () => {
      await open(URL, { result: settings({ status: 'Active', noManager: false }, { activate: NONE }) });
      expect(screen.getByRole('button', { name: 'Archive Keystone Park' })).toBeTruthy();
      expect(screen.queryByRole('button', { name: /Active$/ })).toBeNull();
    });

    it('shows the read-only banner and only Restore for an Archived community (8.04.p)', async () => {
      const h = await open(URL, {
        result: settings({ communityName: 'Old Mill Commons', status: 'Archived', noManager: false }, {
          activate: NONE, archive: NONE, restore: { allowed: true, reasonCode: null }, editProfile: false,
        }),
      });
      expect(screen.getByText('Old Mill Commons is archived. The management company no longer manages it. Everything here is read-only. Nothing has been deleted.')).toBeTruthy();
      expect(screen.queryByRole('button', { name: /^Archive/ })).toBeNull();
      fireEvent.click(screen.getByRole('button', { name: 'Restore Old Mill Commons' }));
      expect(context(h).lifecycleRequest()?.action).toBe('restore');
      expect(context(h).readOnly()).toBeTrue();
      expect(context(h).audience()).toBe('companyAdmin');
    });

    it('does not show the Community Manager Onboarding notice to a Company Administrator', async () => {
      await open(URL);
      expect(screen.queryByText(/is being set up/)).toBeNull();
    });

    it('choosing a search result opens its section and focuses the setting (FR-029)', async () => {
      const h = await open(URL);
      fireEvent.input(screen.getByRole('searchbox', { name: 'Search settings' }), { target: { value: 'time zone' } });
      h.detectChanges();
      fireEvent.click(screen.getByRole('link', { name: 'Architectural review › Time zone' }));
      await until(() => url() === '/app/company/communities/c-kp/settings/architectural-review');
      await settle(h);
      await until(() => document.activeElement?.getAttribute('data-setting-id') === 'time-zone');
      expect(screen.getByRole('heading', { level: 2, name: 'Architectural review' })).toBeTruthy();
    });

    it('choosing a section moves focus to its heading', async () => {
      const h = await open(URL);
      fireEvent.click(screen.getByRole('link', { name: 'Managers and memberships' }));
      await until(() => url().endsWith('/managers-and-memberships'));
      await settle(h);
      await until(() => document.activeElement === screen.getByRole('heading', { level: 2, name: 'Managers and memberships' }));
      expect(document.activeElement!.hasAttribute('data-section-heading')).toBeTrue();
    });

    it('collapses the side list into a menu that keeps the search below 768 px', async () => {
      const h = await open(URL, { isNarrow: true });
      const toggle = screen.getByRole('button', { name: 'Sections: Profile' });
      expect(toggle.getAttribute('aria-expanded')).toBe('false');
      fireEvent.click(toggle);
      h.detectChanges();
      expect(screen.getByRole('searchbox', { name: 'Search settings' })).toBeTruthy();
    });

    it('says when the page could not load, and tries again', async () => {
      const h = await open(URL, { result: new HttpErrorResponse({ status: 0 }) });
      expect(screen.getByText("Couldn't load Community settings.")).toBeTruthy();
      svc.getSettings.and.resolveTo(settings());
      fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
      await until(() => svc.getSettings.calls.count() === 2);
      await settle(h);
      expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Keystone Park');
      expect(screen.queryByText("Couldn't load Community settings.")).toBeNull();
    });

    it('reload() through the context re-reads the shell', async () => {
      const h = await open(URL);
      svc.getSettings.and.resolveTo(settings({ communityName: 'Keystone Park West' }));
      await context(h).reload();
      await settle(h);
      expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Keystone Park West');
    });
  });

  describe('as a Community Manager (board mode)', () => {
    const MANAGER = user();

    it('opens the active community and hides lifecycle and parent controls (US5-S2)', async () => {
      const h = await open('/app/board/settings/profile', {
        signedIn: MANAGER, activeCommunity: 'c-kr',
        result: settings({ id: 'c-kr', communityName: 'Keystone Ridge', noManager: null }, MANAGER_CAN),
      });
      expect(svc.getSettings).toHaveBeenCalledOnceWith('c-kr');
      expect(screen.queryByRole('navigation', { name: 'Breadcrumb' })).toBeNull();
      expect(screen.queryByRole('button', { name: /Mark .* Active|^Archive|^Restore/ })).toBeNull();
      expect(screen.getByRole('heading', { level: 1 }).textContent).not.toContain('No manager');
      expect(context(h).audience()).toBe('staff');
      expect(screen.getByRole('link', { name: 'Architectural review' }).getAttribute('href')).toBe('/app/board/settings/architectural-review');
    });

    it('shows the Onboarding notice on Profile only (FR-029, 8.04.f)', async () => {
      const h = await open('/app/board/settings/profile', {
        signedIn: MANAGER, activeCommunity: 'c-kr',
        result: settings({ id: 'c-kr', communityName: 'Keystone Ridge', noManager: null }, MANAGER_CAN),
      });
      expect(screen.getByText('Keystone Ridge is being set up. A Company Administrator must mark it Active. Resident invitations are sent then.')).toBeTruthy();

      fireEvent.click(screen.getByRole('link', { name: 'Architectural review' }));
      await until(() => url() === '/app/board/settings/architectural-review');
      await settle(h);
      expect(screen.queryByText(/is being set up/)).toBeNull();
    });

    it('uses the sole managed community when none is active', async () => {
      await open('/app/board/settings/profile', {
        signedIn: user({ memberships: [
          { communityId: 'c-sh', communityName: 'Sakura Heights', role: 'BoardMember', communityStatus: 'Active' },
          { communityId: 'c-kr', communityName: 'Keystone Ridge', role: 'CommunityManager', communityStatus: 'Onboarding' },
        ] }),
        result: settings({ id: 'c-kr', communityName: 'Keystone Ridge', noManager: null }, MANAGER_CAN),
      });
      expect(svc.getSettings).toHaveBeenCalledOnceWith('c-kr');
    });

    it('sends a manager of several communities with none chosen to My Communities', async () => {
      await open('/app/board/settings/profile', {
        signedIn: user({ memberships: [
          { communityId: 'c-sh', communityName: 'Sakura Heights', role: 'CommunityManager', communityStatus: 'Active' },
          { communityId: 'c-kr', communityName: 'Keystone Ridge', role: 'CommunityManager', communityStatus: 'Onboarding' },
        ] }),
      });
      await until(() => url() === '/app/board/communities');
      expect(svc.getSettings).not.toHaveBeenCalled();
    });

    it('a 403 sends the person to their landing with the page refusal (US5-S3, FR-032)', async () => {
      await open('/app/board/settings/profile', {
        signedIn: user({ memberships: [{ communityId: 'c-sh', communityName: 'Sakura Heights', role: 'CommunityManager', communityStatus: 'Active' }] }),
        activeCommunity: 'c-sh',
        result: new HttpErrorResponse({ status: 403, error: { code: 'FORBIDDEN', message: 'You do not have access to this community.' } }),
      });
      await until(() => url() === '/app/board/home');
      expect(toasts()).toEqual(["You don't have access to that page."]);
      // Nothing about the community appears, not even briefly.
      expect(screen.queryByRole('heading', { name: /Sakura|Keystone/ })).toBeNull();
    });

    it('explains an archived community to a manager from an old tab', async () => {
      await open('/app/board/settings/profile', {
        signedIn: MANAGER, activeCommunity: 'c-kr',
        result: new HttpErrorResponse({ status: 409, error: { code: 'COMMUNITY_ARCHIVED', message: 'x' } }),
      });
      expect(screen.getByRole('alert').textContent).toContain("no longer manages this community, so it can't be changed.");
      expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();
    });
  });

  it('a 403 in the company area sends a non-administrator to their landing', async () => {
    await open('/app/company/communities/c-kp/settings/profile', {
      signedIn: user({ landing: 'NoHome', memberships: [] }),
      result: new HttpErrorResponse({ status: 403, error: { code: 'FORBIDDEN', message: 'x' } }),
    });
    await until(() => url() === '/app/no-home');
    expect(toasts()).toEqual(["You don't have access to that page."]);
  });
});
