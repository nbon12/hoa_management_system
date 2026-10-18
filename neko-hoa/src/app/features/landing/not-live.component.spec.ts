import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { render, screen, fireEvent } from '@testing-library/angular';
import { until } from '../board/architectural/arc-testing';
import { NotLiveComponent } from './not-live.component';
import { AuthService } from '../../core/services/auth.service';
import { CurrentUser } from '../../core/models';

// 030 T122 / FR-036a step 3 / brief 11 8.11.b: the landing for a person whose only homes are in
// Onboarding communities. It names no community and no home, and offers "Sign out".

function person(email: string): CurrentUser {
  return {
    id: 'u1', firstName: 'Dana', lastName: 'Fox', email, initials: 'DF', lastActiveMode: 'Resident',
    memberships: [], properties: [], isCompanyAdministrator: false, landing: 'NoHome',
  };
}

async function setup(component: typeof NotLiveComponent, email: string) {
  const logout = jasmine.createSpy('logout');
  const r = await render(component, {
    providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { user: signal(person(email)).asReadonly(), logout } },
    ],
  });
  return { r, logout };
}

describe('NotLiveComponent (030 T122, US9-S5)', () => {
  it('says the community is not live yet, with no address, community or error styling (8.11.b, D7)', async () => {
    await setup(NotLiveComponent, 'ann@example.com');
    const heading = screen.getByRole('heading', { level: 1 });
    expect(heading.textContent!.trim()).toBe("Your community isn't live yet");
    expect(screen.getByText("Your management company is still setting up your community. You'll see your home here once it's live.")).toBeTruthy();
    expect(screen.getByText("There's nothing you need to do now.")).toBeTruthy();
    expect(screen.getByText('If you received an invitation, open the link in that email.')).toBeTruthy();
    expect(document.querySelector('.alert--error')).toBeNull();
    expect(screen.queryByRole('alert')).toBeNull();
    for (const word of ['Onboarding', 'Active', 'Archived', 'Briar Hollow']) {
      expect(document.body.textContent).not.toContain(word);
    }
    await until(() => document.activeElement === heading);
  });

  it('signs out', async () => {
    const { logout } = await setup(NotLiveComponent, 'ann@example.com');
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));
    expect(logout).toHaveBeenCalled();
  });
});
