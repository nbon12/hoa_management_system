import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig } from '@storybook/angular';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { ShellComponent } from './shell.component';
import { AuthService } from '../core/services/auth.service';
import { PropertyService } from '../core/services/property.service';
import { CurrentUser, CommunityMembershipSummary, Property, UserMode } from '../core/models';

// 025 T042 (US3) — visual-regression story for the board shell. The shell renders the resident
// or board chrome from the acting user's persisted mode + memberships: in board mode the violet
// banner shows and the sidebar is the role-derived board nav (BoardNavigationService), with
// role-locked sections rendered disabled (🔒), not absent (FR-040).

const PROPERTY: Property = {
  accountNumber: 'SAKURA-001', communityId: 'c1', communityName: 'Sakura Heights HOA',
  address: '1 Sakura Drive', city: 'San Jose', state: 'CA', zip: '95101',
  lot: 'A1', phase: null, section: '1', block: null,
  fiscalYear: 2026, yearBuilt: 2005, status: 'active',
  monthlyAssessment: 250, annualAssessment: 3000, assessmentDueDay: 1,
  lateFeeAmount: 50, lateFeeGraceDays: 15, financeChargeRate: 1.5,
};

function currentUser(mode: UserMode, memberships: CommunityMembershipSummary[]): CurrentUser {
  return {
    id: 'u1', firstName: 'Bea', lastName: 'Board', email: 'board@nekohoa.dev',
    initials: 'BB', lastActiveMode: mode, memberships,
  };
}

/** A stubbed AuthService exposing the acting user as a signal; the real BoardNavigationService
 *  (providedIn: 'root') derives the board nav from it. */
function authProvider(user: CurrentUser) {
  return {
    provide: AuthService,
    useValue: {
      user: signal(user).asReadonly(),
      logout: () => {},
      switchMode: () => Promise.resolve(),
    },
  };
}

const propertyProvider = {
  provide: PropertyService,
  useValue: { getProperty: () => Promise.resolve(PROPERTY) },
};

const meta: Meta<ShellComponent> = {
  title: 'Shell/AppShell',
  component: ShellComponent,
};

export default meta;
type Story = StoryObj<ShellComponent>;

/** Resident mode: the resident sidebar, no board banner, and (since this user is board-eligible)
 *  the "Enter board mode" control in the top bar. */
export const ResidentMode: Story = {
  decorators: [
    applicationConfig({
      providers: [
        provideRouter([]),
        authProvider(currentUser('Resident', [
          { communityId: 'c1', communityName: 'Sakura Heights HOA', role: 'BoardMember' },
        ])),
        propertyProvider,
      ],
    }),
  ],
};

/** Board mode as a Community Manager: the violet banner shows and the board nav renders with
 *  every manager-gated section (Memberships) unlocked. */
export const BoardModeManager: Story = {
  decorators: [
    applicationConfig({
      providers: [
        provideRouter([]),
        authProvider(currentUser('Board', [
          { communityId: 'c1', communityName: 'Sakura Heights HOA', role: 'CommunityManager' },
        ])),
        propertyProvider,
      ],
    }),
  ],
};

/** Board mode as a Board Member: manager-only sections (Memberships) render locked (🔒), not
 *  absent (FR-040). */
export const BoardModeMember: Story = {
  decorators: [
    applicationConfig({
      providers: [
        provideRouter([]),
        authProvider(currentUser('Board', [
          { communityId: 'c1', communityName: 'Sakura Heights HOA', role: 'BoardMember' },
        ])),
        propertyProvider,
      ],
    }),
  ],
};
