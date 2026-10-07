import { signal } from '@angular/core';
import { render, screen, fireEvent, waitFor } from '@testing-library/angular';
import { until } from './arc-testing';
import { ArcSettingsComponent } from './arc-settings.component';
import { ArcSettings, ArchitecturalService } from '../../../core/services/architectural.service';
import { AuthService } from '../../../core/services/auth.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';

const SETTINGS: ArcSettings = {
  reviewPeriodDays: 30, lapseRule: 'FlagOverdueOnly', decisionRule: 'MajorityOfMembers', reminderDays: 7,
  timeZoneId: 'America/New_York', formalDisapprovalStatement: 'Formal.', updatedAt: null,
};

describe('ArcSettingsComponent (027 FR-029)', () => {
  let arc: jasmine.SpyObj<ArchitecturalService>;

  async function setup() {
    arc = jasmine.createSpyObj<ArchitecturalService>('ArchitecturalService', ['getSettings', 'putSettings']);
    arc.getSettings.and.returnValue(Promise.resolve({ ...SETTINGS }));
    arc.putSettings.and.callFake((_c, s) => Promise.resolve(s));
    const r = await render(ArcSettingsComponent, {
      providers: [
        { provide: ArchitecturalService, useValue: arc },
        { provide: AuthService, useValue: { user: signal({ memberships: [{ communityId: 'c1', communityName: 'One', role: 'CommunityManager' }] }).asReadonly() } },
        { provide: BoardNavigationService, useValue: { activeCommunityId: signal<string | null>(null).asReadonly() } },
      ],
    });
    await waitFor(() => expect(screen.getByLabelText('Review period (days)')).toBeTruthy());
    return r;
  }

  it('saves the five settings (US6-S18)', async () => {
    const r = await setup();
    const f = r.fixture.componentInstance.form()!;
    f.reviewPeriodDays = 45;
    f.lapseRule = 'DeemedApproved';
    fireEvent.click(screen.getByRole('button', { name: 'Save settings' }));
    await until(() => arc.putSettings.calls.count() > 0);
    expect(arc.putSettings.calls.mostRecent().args[1]).toEqual(jasmine.objectContaining({ reviewPeriodDays: 45, lapseRule: 'DeemedApproved' }));
    await until(() => !!screen.queryByRole('status')?.textContent?.includes('Settings saved.'));
  });

  it('validates ranges and required text before saving', async () => {
    const r = await setup();
    const c = r.fixture.componentInstance;
    expect(c.validate({ ...SETTINGS, reviewPeriodDays: 0 })).toContain('between 1 and 365');
    expect(c.validate({ ...SETTINGS, reminderDays: 31 })).toContain('between 0 and 30');
    expect(c.validate({ ...SETTINGS, formalDisapprovalStatement: ' ' })).toContain('required');
    c.form.set({ ...SETTINGS, reviewPeriodDays: 400 });
    r.fixture.detectChanges();
    fireEvent.click(screen.getByRole('button', { name: 'Save settings' }));
    await until(() => !!screen.queryByRole('alert')?.textContent?.includes('between 1 and 365'));
    expect(arc.putSettings).not.toHaveBeenCalled();
  });
});
