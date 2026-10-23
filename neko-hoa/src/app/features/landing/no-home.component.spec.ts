import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { render, screen, fireEvent } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { NoHomeComponent } from './no-home.component';
import { AuthService } from '../../core/services/auth.service';
import { PageNoticeService } from '../../core/services/page-notice.service';
import { CurrentUser } from '../../core/models';

// 030 T122 / FR-036a step 4 / brief 11 8.11.a: the landing for a person with no home, membership
// or Company Administrator role. It names no community and offers "Sign out".

function person(email: string): CurrentUser {
  return {
    id: 'u1', firstName: 'Dana', lastName: 'Fox', email, initials: 'DF', lastActiveMode: 'Resident',
    memberships: [], properties: [], isCompanyAdministrator: false, landing: 'NoHome',
  };
}

async function setup(component: typeof NoHomeComponent, email: string) {
  const logout = jasmine.createSpy('logout');
  const r = await render(component, {
    providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { user: signal(person(email)).asReadonly(), logout } },
    ],
  });
  return { r, logout };
}

describe('NoHomeComponent (030 T122, US9-S3)', () => {
  it('says the account has no home and tells the person to use an invitation link or contact their manager', async () => {
    await setup(NoHomeComponent, 'dana@example.com');
    const heading = screen.getByRole('heading', { level: 1 });
    expect(heading.textContent!.trim()).toBe('No home linked to this account');
    expect(screen.getByText("You're signed in as dana@example.com, but this account isn't linked to a home.")).toBeTruthy();
    expect(screen.getByText('If you received an invitation, open the link in that email.')).toBeTruthy();
    expect(screen.getByText('Otherwise, contact your community manager.')).toBeTruthy();
    // No claim-code field, and no community named (brief 11 §1).
    expect(screen.queryByLabelText(/claim code/i)).toBeNull();
    expect(screen.queryByRole('textbox')).toBeNull();
    await until(() => document.activeElement === heading);
  });

  it('signs out', async () => {
    const { logout } = await setup(NoHomeComponent, 'dana@example.com');
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));
    expect(logout).toHaveBeenCalled();
  });

  it('shows only the logo and avatar above the card: no app nav (brief 11 D1)', async () => {
    await setup(NoHomeComponent, 'dana@example.com');
    expect(screen.getByText('DF')).toBeTruthy();
    expect(screen.queryByRole('navigation')).toBeNull();
    expect(document.body.textContent).not.toContain('Enter board mode');
  });

  it('shows a page notice the app moved the person here with', async () => {
    const { r } = await setup(NoHomeComponent, 'dana@example.com');
    const notices = r.fixture.debugElement.injector.get(PageNoticeService);
    notices.show('You no longer have access to that home.');
    r.fixture.detectChanges();
    expect(screen.getByText('You no longer have access to that home.')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Dismiss message' }));
    r.fixture.detectChanges();
    expect(screen.queryByText('You no longer have access to that home.')).toBeNull();
  });
});
