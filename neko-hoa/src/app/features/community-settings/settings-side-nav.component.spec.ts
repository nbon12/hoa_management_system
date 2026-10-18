import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { render, screen, fireEvent, within } from '@testing-library/angular';
import { SettingsSideNavComponent } from './settings-side-nav.component';
import { SETTINGS_REGISTRY, SectionDef, searchSettings, sectionsFor } from './settings-registry';
import { ViewportService } from '../../core/services/viewport.service';
import { CommunitySettingsContext } from './community-settings-context';

// 030 T067 / FR-029 / brief 04 §4.1 (owner decision 2026-10-14): the side list with a search box.
// Search filters to section and setting labels from a static registry; choosing a result opens the
// section and asks for focus on that setting. Below 768 px the list collapses into a menu that
// keeps the search (constitution §6).

const ALL = sectionsFor(['profile', 'managers-and-memberships', 'architectural-review']);
const BASE = ['/app/company/communities', 'c-sh', 'settings'];

describe('SettingsSideNavComponent (030 T067)', () => {
  const narrow = signal(false);

  async function setup(opts: { sections?: SectionDef[]; current?: string; isNarrow?: boolean } = {}) {
    narrow.set(opts.isNarrow ?? false);
    const settingChosen = jasmine.createSpy('settingChosen');
    const sectionChosen = jasmine.createSpy('sectionChosen');
    const r = await render(SettingsSideNavComponent, {
      componentInputs: { sections: opts.sections ?? ALL, current: opts.current ?? 'profile', basePath: BASE },
      providers: [provideRouter([]), { provide: ViewportService, useValue: { isNarrow: narrow.asReadonly() } }],
    });
    r.fixture.componentInstance.settingChosen.subscribe(settingChosen);
    r.fixture.componentInstance.sectionChosen.subscribe(sectionChosen);
    return { r, settingChosen, sectionChosen };
  }

  /** Links inside the side list only (the Jasmine HTML reporter adds its own links to the page). */
  function links(): HTMLElement[] {
    return within(screen.getByRole('navigation', { name: 'Community settings sections' })).queryAllByRole('link');
  }

  function search(r: Awaited<ReturnType<typeof setup>>['r'], text: string) {
    fireEvent.input(screen.getByRole('searchbox', { name: 'Search settings' }), { target: { value: text } });
    r.fixture.detectChanges();
  }

  it('is a navigation landmark listing the sections in order, with the current one marked', async () => {
    await setup({ current: 'managers-and-memberships' });
    const items = links();
    expect(items.map(a => a.textContent!.trim())).toEqual(['Profile', 'Managers and memberships', 'Architectural review']);
    expect(items[1].getAttribute('aria-current')).toBe('page');
    expect(items[0].hasAttribute('aria-current')).toBeFalse();
    expect(items[2].getAttribute('href')).toBe('/app/company/communities/c-sh/settings/architectural-review');
    expect(screen.getByRole('searchbox', { name: 'Search settings' }).getAttribute('placeholder')).toBe('Search settings');
  });

  it('never shows Notifications', async () => {
    await setup({ sections: sectionsFor(['profile', 'notifications']) });
    expect(screen.queryByText(/Notifications/)).toBeNull();
    expect(links().length).toBe(1);
  });

  it('filters to matching sections and setting labels, and announces the count (8.04.b)', async () => {
    const { r } = await setup();
    search(r, 'review');
    const labels = links().map(a => a.textContent!.trim());
    expect(labels).toEqual([
      'Architectural review',
      'Architectural review › Review period',
      'Architectural review › When the review period ends without a decision',
    ]);
    expect(screen.getByRole('status').textContent!.trim()).toBe('3 settings match');
    // Matching ignores case and matches part of a word.
    search(r, 'FORMAT');
    expect(links().map(a => a.textContent!.trim())).toEqual(['Profile › Formation date']);
    expect(screen.getByRole('status').textContent!.trim()).toBe('1 setting matches');
  });

  it('says so when nothing matches', async () => {
    const { r } = await setup();
    search(r, 'parking');
    expect(screen.getByText('No settings match “parking”.', { selector: '.ssn__empty' })).toBeTruthy();
    expect(links().length).toBe(0);
    expect(screen.getByRole('status').textContent!.trim()).toBe('No settings match “parking”.');
  });

  it('searches only the sections this person can see', async () => {
    const { r } = await setup({ sections: sectionsFor(['profile']) });
    search(r, 'review');
    expect(screen.getByText('No settings match “review”.', { selector: '.ssn__empty' })).toBeTruthy();
  });

  it('choosing a result links to its section, asks for focus on the setting and clears the search', async () => {
    const { r, settingChosen } = await setup();
    search(r, 'time');
    const result = screen.getByRole('link', { name: 'Architectural review › Time zone' });
    expect(result.getAttribute('href')).toBe('/app/company/communities/c-sh/settings/architectural-review');
    fireEvent.click(result);
    r.fixture.detectChanges();
    expect(settingChosen).toHaveBeenCalledWith({ sectionId: 'architectural-review', settingId: 'time-zone' });
    expect((screen.getByRole('searchbox', { name: 'Search settings' }) as HTMLInputElement).value).toBe('');
    expect(links().length).toBe(3);
  });

  it('choosing a section asks for focus on its heading', async () => {
    const { sectionChosen, settingChosen } = await setup();
    fireEvent.click(screen.getByRole('link', { name: 'Architectural review' }));
    expect(sectionChosen).toHaveBeenCalledWith('architectural-review');
    expect(settingChosen).not.toHaveBeenCalled();
  });

  it('choosing a section result from search asks for its heading', async () => {
    const { r, sectionChosen } = await setup();
    search(r, 'managers');
    fireEvent.click(screen.getByRole('link', { name: 'Managers and memberships' }));
    expect(sectionChosen).toHaveBeenCalledWith('managers-and-memberships');
  });

  describe('below 768 px', () => {
    it('collapses into a menu button naming the current section', async () => {
      await setup({ isNarrow: true, current: 'architectural-review' });
      const toggle = screen.getByRole('button', { name: 'Sections: Architectural review' });
      expect(toggle.getAttribute('aria-expanded')).toBe('false');
      expect(screen.queryByRole('searchbox')).toBeNull();
      expect(links().length).toBe(0);
    });

    it('opens to show the search and the sections, and closes after a choice', async () => {
      const { r, settingChosen } = await setup({ isNarrow: true });
      const toggle = screen.getByRole('button', { name: 'Sections: Profile' });
      fireEvent.click(toggle);
      r.fixture.detectChanges();
      expect(toggle.getAttribute('aria-expanded')).toBe('true');
      expect(toggle.getAttribute('aria-controls')).toBe(document.querySelector('.ssn__panel')!.id);
      search(r, 'review period');
      fireEvent.click(screen.getByRole('link', { name: 'Architectural review › Review period' }));
      r.fixture.detectChanges();
      expect(settingChosen).toHaveBeenCalledWith({ sectionId: 'architectural-review', settingId: 'review-period' });
      expect(toggle.getAttribute('aria-expanded')).toBe('false');
    });

    it('Esc closes the menu and returns focus to its button', async () => {
      const { r } = await setup({ isNarrow: true });
      const toggle = screen.getByRole('button', { name: 'Sections: Profile' });
      fireEvent.click(toggle);
      r.fixture.detectChanges();
      fireEvent.keyDown(screen.getByRole('searchbox'), { key: 'Escape' });
      r.fixture.detectChanges();
      expect(toggle.getAttribute('aria-expanded')).toBe('false');
      expect(document.activeElement).toBe(toggle);
    });
  });
});

describe('CommunitySettingsContext', () => {
  it('reloads nothing before the page sets a reloader, and words refusals by mode', async () => {
    const c = new CommunitySettingsContext();
    await expectAsync(c.reload()).toBeResolved();
    expect(c.communityId()).toBeNull();
    expect(c.readOnly()).toBeFalse();
    expect(c.audience()).toBe('companyAdmin');
    c.mode.set('board');
    expect(c.audience()).toBe('staff');
    c.requestLifecycle('archive');
    c.requestLifecycle('archive');
    expect(c.lifecycleRequest()).toEqual({ action: 'archive', seq: 2 });
  });
});

describe('settings registry', () => {
  it('lists Profile, Managers and memberships and Architectural review, in that order, and no Notifications', () => {
    expect(SETTINGS_REGISTRY.map(s => s.label)).toEqual(['Profile', 'Managers and memberships', 'Architectural review']);
  });

  it('keeps the server order and drops ids it does not know', () => {
    expect(sectionsFor(['architectural-review', 'notifications', 'profile']).map(s => s.id)).toEqual(['architectural-review', 'profile']);
  });

  it('returns nothing for a blank query', () => {
    expect(searchSettings('   ', ALL)).toEqual([]);
  });
});
