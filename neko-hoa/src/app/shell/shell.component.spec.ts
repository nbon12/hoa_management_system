import { signal } from '@angular/core';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
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
