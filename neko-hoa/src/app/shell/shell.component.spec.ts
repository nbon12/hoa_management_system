import { Component, NgZone, signal } from '@angular/core';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { ShellComponent } from './shell.component';
import { AuthService } from '../core/services/auth.service';
import { PropertyService } from '../core/services/property.service';
import { CurrentUser, CommunityMembershipSummary, UserMode } from '../core/models';

// 025 T039 (US3) — the shell renders the correct navigation per role. The real
// BoardNavigationService (providedIn: 'root') derives the board sidebar from the acting user's
// memberships, so this drives it through a stubbed AuthService and asserts the rendered chrome:
// resident nav vs. board nav, the mode control's presence, role-locked sections (FR-040), and
// the "My Communities" rule for 2+ communities (FR-025).

const userSig = signal<CurrentUser | null>(null);

function setUser(memberships: CommunityMembershipSummary[], mode: UserMode = 'Resident') {
  userSig.set({
    id: 'u1', firstName: 'Bea', lastName: 'Board', email: 'b@b.dev', initials: 'BB',
    lastActiveMode: mode, memberships,
  });
}

function sideItems(el: HTMLElement): HTMLElement[] {
  return Array.from(el.querySelectorAll<HTMLElement>('.shell__side-item'));
}

function findItem(el: HTMLElement, label: string): HTMLElement | undefined {
  return sideItems(el).find(n => (n.textContent ?? '').includes(label));
}

describe('ShellComponent — nav per role (025 T039)', () => {
  let fixture: ComponentFixture<ShellComponent>;
  let el: HTMLElement;

  beforeEach(async () => {
    userSig.set(null);
    await TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: { user: userSig.asReadonly(), logout: () => {}, switchMode: () => Promise.resolve() },
        },
        { provide: PropertyService, useValue: { getProperty: () => Promise.resolve(null) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ShellComponent);
    el = fixture.nativeElement;
  });

  it('resident-only user in resident mode: resident nav, no banner, no mode control', () => {
    setUser([{ communityId: 'c1', communityName: 'One', role: 'Resident' }]);
    fixture.detectChanges();

    // Resident sidebar entries render as links.
    const dashboard = findItem(el, 'Dashboard');
    expect(dashboard).withContext('Dashboard nav item').toBeTruthy();
    expect(findItem(el, 'Statement')).toBeTruthy();
    // No board chrome.
    expect(el.querySelector('.board-banner')).toBeNull();
    expect(findItem(el, 'Community Home')).toBeUndefined();
    // A resident-only user is not board-eligible → no "Enter board mode" control.
    expect(el.textContent).not.toContain('Enter board mode');
  });

  it('board-eligible user in resident mode: still resident nav, but the mode control is offered', () => {
    setUser([{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }], 'Resident');
    fixture.detectChanges();

    expect(el.querySelector('.board-banner')).toBeNull();
    expect(findItem(el, 'Dashboard')).toBeTruthy();
    expect(findItem(el, 'Community Home')).toBeUndefined();
    expect(el.textContent).toContain('Enter board mode');
  });

  it('board mode as Board Member: board nav + banner; manager-only Memberships renders locked (FR-040)', () => {
    setUser([{ communityId: 'c1', communityName: 'One', role: 'BoardMember' }], 'Board');
    fixture.detectChanges();

    // Banner and Resident/Board toggle present.
    expect(el.querySelector('.board-banner')).toBeTruthy();
    expect(el.querySelector('.mode-seg')).toBeTruthy();
    // Board landing entry present as a link.
    const home = findItem(el, 'Community Home');
    expect(home).toBeTruthy();
    expect(home!.tagName).toBe('A');
    // Manager-only section is present but LOCKED (disabled div with 🔒), not absent and not a link.
    const memberships = findItem(el, 'Memberships');
    expect(memberships).withContext('Memberships item is rendered, not hidden').toBeTruthy();
    expect(memberships!.tagName).not.toBe('A');
    expect(memberships!.classList).toContain('shell__side-item--locked');
    expect(memberships!.textContent).toContain('🔒');
    // A board member holding one community sees no "My Communities" entry.
    expect(findItem(el, 'My Communities')).toBeUndefined();
  });

  it('board mode as Community Manager: manager-only Memberships is unlocked (a navigable link)', () => {
    setUser([{ communityId: 'c1', communityName: 'One', role: 'CommunityManager' }], 'Board');
    fixture.detectChanges();

    const memberships = findItem(el, 'Memberships');
    expect(memberships).toBeTruthy();
    expect(memberships!.tagName).toBe('A');
    expect(memberships!.textContent).not.toContain('🔒');
    expect(memberships!.getAttribute('href')).toContain('/app/board/memberships');
  });

  it('board mode with 2+ communities: the My Communities entry is rendered (FR-025)', () => {
    setUser([
      { communityId: 'c1', communityName: 'One', role: 'BoardMember' },
      { communityId: 'c2', communityName: 'Two', role: 'CommunityManager' },
    ], 'Board');
    fixture.detectChanges();

    expect(findItem(el, 'My Communities')).toBeTruthy();
  });
});

@Component({ standalone: true, template: '<h1>Page</h1>' })
class PageStubComponent {}

// 030 T069 / FR-036 / brief 01: the shell's company-area state, the company nav, and the Company
// menu in the account cluster for Company Administrators only.
describe('ShellComponent — company area (030 T069)', () => {
  let fixture: ComponentFixture<ShellComponent>;
  let el: HTMLElement;

  function signIn(over: Partial<CurrentUser>) {
    userSig.set({
      id: 'u1', firstName: 'Robin', lastName: 'Hale', email: 'company@nekohoa.dev', initials: 'RH',
      lastActiveMode: 'Resident', memberships: [], properties: [], isCompanyAdministrator: false, landing: 'Resident',
      ...over,
    });
  }

  const ROBIN: Partial<CurrentUser> = { isCompanyAdministrator: true, landing: 'Company' };
  const CASEY: Partial<CurrentUser> = {
    firstName: 'Casey', initials: 'CN', isCompanyAdministrator: true, landing: 'Board', lastActiveMode: 'Board',
    memberships: [{ communityId: 'kcm', communityName: 'Keystone Crossing Master', role: 'BoardMember', communityStatus: 'Active' }],
  };

  async function at(url: string) {
    const router = TestBed.inject(Router);
    await TestBed.inject(NgZone).run(() => router.navigateByUrl(url));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function nav(name: string): HTMLElement | null {
    return el.querySelector<HTMLElement>(`nav[aria-label="${name}"]`);
  }

  function navLabels(name: string): string[] {
    return Array.from(nav(name)?.querySelectorAll('.shell__side-item') ?? []).map(n => n.textContent!.trim());
  }

  function companyButton(): HTMLButtonElement | null {
    return el.querySelector<HTMLButtonElement>('button[aria-label="Company area menu"]');
  }

  beforeEach(async () => {
    userSig.set(null);
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([{ path: '**', component: PageStubComponent }]),
        { provide: AuthService, useValue: { user: userSig.asReadonly(), logout: () => {}, switchMode: () => Promise.resolve() } },
        { provide: PropertyService, useValue: { getProperty: () => Promise.resolve(null) } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(ShellComponent);
    el = fixture.nativeElement;
    document.body.appendChild(el);
  });

  afterEach(() => el.remove());

  it('shows the company nav, the company strip and no board banner under /app/company/** (8.01.f)', async () => {
    signIn(ROBIN);
    await at('/app/company/portfolio');
    expect(navLabels('Company area navigation')).toEqual(['Portfolio', 'Add community', 'Company defaults', 'Company administrators']);
    expect(nav('Board navigation')).toBeNull();
    expect(el.querySelector('.board-banner')).toBeNull();
    expect(el.querySelector('.shell__strip')!.textContent).toContain('Company area');
    // No locks in the company area: every entry is a link.
    expect(Array.from(nav('Company area navigation')!.querySelectorAll('.shell__side-item')).every(n => n.tagName === 'A')).toBeTrue();
    const portfolio = nav('Company area navigation')!.querySelector<HTMLAnchorElement>('a[href="/app/company/portfolio"]')!;
    expect(portfolio.getAttribute('aria-current')).toBe('page');
  });

  it('keeps Portfolio current while a community settings page is open; Add community on its own page (D6)', async () => {
    signIn(ROBIN);
    await at('/app/company/communities/c-kp/settings/profile');
    const company = nav('Company area navigation')!;
    expect(company.querySelector('a[href="/app/company/portfolio"]')!.getAttribute('aria-current')).toBe('page');
    await at('/app/company/communities/new');
    expect(company.querySelector('a[href="/app/company/portfolio"]')!.hasAttribute('aria-current')).toBeFalse();
    expect(company.querySelector('a[href="/app/company/communities/new"]')!.getAttribute('aria-current')).toBe('page');
  });

  it('shows no board banner in the company area even for a Board Member in board mode (Casey, 8.01.e)', async () => {
    signIn(CASEY);
    await at('/app/company/portfolio');
    expect(el.querySelector('.board-banner')).toBeNull();
    expect(navLabels('Company area navigation').length).toBe(4);
    // Casey keeps the spec 025 control, offered as "Enter board mode" outside board mode.
    expect(el.textContent).toContain('Enter board mode');
    expect(el.querySelector('.mode-seg')).toBeNull();
  });

  it('offers the Company menu in the account cluster only to Company Administrators (FR-036)', async () => {
    signIn(ROBIN);
    await at('/app/dashboard');
    expect(companyButton()).toBeTruthy();
    expect(companyButton()!.textContent!.trim()).toBe('Company');

    signIn({ memberships: [{ communityId: 'sh', communityName: 'Sakura Heights', role: 'CommunityManager', communityStatus: 'Active' }], lastActiveMode: 'Board', landing: 'Board' });
    fixture.detectChanges();
    expect(companyButton()).toBeNull();
    // Jordan's board nav has no Portfolio item, locked or not (FR-035).
    expect(navLabels('Board navigation')).not.toContain('Portfolio');
  });

  it('the grant alone shows no board-mode control and no board nav (FR-036, 8.01.f)', async () => {
    signIn(ROBIN);
    await at('/app/company/portfolio');
    expect(el.textContent).not.toContain('Enter board mode');
    expect(el.querySelector('.mode-seg')).toBeNull();
    expect(nav('Board navigation')).toBeNull();
  });

  it('marks the Company button as current inside the company area', async () => {
    signIn(ROBIN);
    await at('/app/company/defaults');
    expect(companyButton()!.classList).toContain('cmenu__btn--current');
    expect(document.getElementById(companyButton()!.getAttribute('aria-describedby')!)!.textContent).toContain("You're in the company area.");
    await at('/app/dashboard');
    expect(companyButton()!.classList).not.toContain('cmenu__btn--current');
    expect(companyButton()!.hasAttribute('aria-describedby')).toBeFalse();
  });

  it('the Company menu opens with its four items, moves with the arrow keys and closes with Esc (brief 01 §6–7)', async () => {
    signIn(CASEY);
    await at('/app/board/home');
    const button = companyButton()!;
    expect(button.getAttribute('aria-haspopup')).toBe('menu');
    expect(button.getAttribute('aria-expanded')).toBe('false');

    button.click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(button.getAttribute('aria-expanded')).toBe('true');
    const menu = el.querySelector<HTMLElement>('[role="menu"]')!;
    expect(document.getElementById(menu.getAttribute('aria-labelledby')!)!.textContent).toBe('Company area');
    const items = Array.from(menu.querySelectorAll<HTMLElement>('[role="menuitem"]'));
    expect(items.map(i => i.textContent!.trim())).toEqual(['Portfolio', 'Add community', 'Company defaults', 'Company administrators']);
    expect(document.activeElement).toBe(items[0]);

    const key = (k: string) => menu.parentElement!.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true }));
    key('ArrowDown');
    expect(document.activeElement).toBe(items[1]);
    key('ArrowUp'); key('ArrowUp');
    expect(document.activeElement).toBe(items[3]);
    key('Home');
    expect(document.activeElement).toBe(items[0]);
    key('End');
    expect(document.activeElement).toBe(items[3]);
    key('Escape');
    fixture.detectChanges();
    expect(el.querySelector('[role="menu"]')).toBeNull();
    expect(document.activeElement).toBe(button);
  });

  it('choosing an item opens that page in the company shell', async () => {
    signIn(CASEY);
    await at('/app/board/home');
    companyButton()!.click();
    fixture.detectChanges();
    (el.querySelector<HTMLElement>('[role="menuitem"][href="/app/company/defaults"]')!).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(TestBed.inject(Router).url).toBe('/app/company/defaults');
    expect(el.querySelector('[role="menu"]')).toBeNull();
    expect(navLabels('Company area navigation').length).toBe(4);
  });

  it('closes the menu on a click outside it, and on Tab', async () => {
    signIn(ROBIN);
    await at('/app/company/portfolio');
    companyButton()!.click();
    fixture.detectChanges();
    document.body.click();
    fixture.detectChanges();
    expect(el.querySelector('[role="menu"]')).toBeNull();

    companyButton()!.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    fixture.detectChanges();
    expect(el.querySelector('[role="menu"]')).toBeTruthy();
    el.querySelector('.cmenu__panel')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }));
    fixture.detectChanges();
    expect(el.querySelector('[role="menu"]')).toBeNull();

    // The button toggles it, too.
    companyButton()!.click();
    fixture.detectChanges();
    companyButton()!.click();
    fixture.detectChanges();
    expect(el.querySelector('[role="menu"]')).toBeNull();
  });

  it('names the board and resident navigation landmarks', async () => {
    signIn({ memberships: [{ communityId: 'sh', communityName: 'Sakura Heights', role: 'BoardMember', communityStatus: 'Active' }], lastActiveMode: 'Board', landing: 'Board' });
    await at('/app/board/home');
    expect(nav('Board navigation')).toBeTruthy();
    const settings = Array.from(nav('Board navigation')!.querySelectorAll<HTMLElement>('.shell__side-item')).find(n => n.textContent!.includes('Community settings'))!;
    expect(settings.textContent).toContain('not available to your role');

    signIn({});
    await at('/app/dashboard');
    expect(nav('Resident navigation')).toBeTruthy();
  });
});
