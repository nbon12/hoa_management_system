import { NgZone, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { AuthService } from './core/services/auth.service';
import { CurrentUser } from './core/models';

// 030 T130: the app's own routes, navigated for real (no outlet, so no page is rendered).

const JORDAN: CurrentUser = {
  id: 'u-jordan', firstName: 'Jordan', lastName: 'Blake', email: 'jordan@example.com', initials: 'JB', lastActiveMode: 'Board',
  properties: [], isCompanyAdministrator: false, landing: 'Board',
  memberships: [{ communityId: 'c-sf', communityName: 'Keystone Crossing SF', role: 'CommunityManager', communityStatus: 'Active' }],
};

const ROBIN: CurrentUser = {
  id: 'u-robin', firstName: 'Robin', lastName: 'Hale', email: 'owner@example.com', initials: 'RH', lastActiveMode: 'Resident',
  properties: [], isCompanyAdministrator: true, landing: 'Company', memberships: [],
};

describe('app routes (030)', () => {
  const user = signal<CurrentUser | null>(JORDAN);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: AuthService, useValue: { isLoggedIn: () => user() !== null, user: user.asReadonly() } },
      ],
    });
  });

  async function go(url: string): Promise<string> {
    const router = TestBed.inject(Router);
    await TestBed.inject(NgZone).run(() => router.navigateByUrl(url));
    return router.url;
  }

  it('redirects the old ARC settings address to the Architectural review section (US5-S6, FR-030)', async () => {
    user.set(JORDAN);
    expect(await go('/app/board/arc-settings')).toBe('/app/board/settings/architectural-review');
  });

  it('opens the read-only Company defaults page for a Community Manager in board mode (US4-S3)', async () => {
    user.set(JORDAN);
    expect(await go('/app/board/company-defaults')).toBe('/app/board/company-defaults');
  });

  it('opens the Company defaults page in the company area for a Company Administrator only (US4)', async () => {
    user.set(JORDAN);
    expect(await go('/app/company/defaults')).not.toBe('/app/company/defaults');
    user.set(ROBIN);
    expect(await go('/app/company/defaults')).toBe('/app/company/defaults');
  });

  it('opens Company administrators for a Company Administrator only (US7-S5)', async () => {
    user.set(JORDAN);
    expect(await go('/app/company/administrators')).not.toBe('/app/company/administrators');
    user.set(ROBIN);
    expect(await go('/app/company/administrators')).toBe('/app/company/administrators');
  });
});
