import type { Meta, StoryObj } from '@storybook/angular';
import { of } from 'rxjs';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { CommunitySettingsPageComponent } from './community-settings-page.component';
import { CommunitySettings } from '../../core/services/community-settings.service';
import { JORDAN, KEYSTONE_PARK, MANAGER_CAN, OLD_MILL, SAKURA, settings, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — briefs 04–07: one community's Community settings page, at each section.
const meta: Meta<CommunitySettingsPageComponent> = { title: 'CommunitySettings/Page', component: CommunitySettingsPageComponent };
export default meta;
type Story = StoryObj<CommunitySettingsPageComponent>;
const at = storyAt<CommunitySettingsPageComponent>();

function page(s: CommunitySettings, section: string, mode: 'company' | 'board' = 'company') {
  const params = convertToParamMap({ communityId: s.id, section });
  return [withServices({
    // Board mode opens the sole community this manager manages.
    user: mode === 'board' ? { ...JORDAN, memberships: [JORDAN.memberships[0]] } : undefined,
    settings: { getSettings: () => Promise.resolve(s) },
    extra: [{ provide: ActivatedRoute, useValue: { paramMap: of(params), snapshot: { paramMap: params, data: { settingsMode: mode } } } }],
  })];
}

const base: Story = { decorators: page(SAKURA, 'profile') };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const ManagersAndMemberships: Story = at('desktop', { decorators: page(SAKURA, 'managers-and-memberships') });
export const ManagersAndMembershipsOnAPhone: Story = at('phone', { decorators: page(SAKURA, 'managers-and-memberships') });
export const ArchitecturalReview: Story = at('desktop', { decorators: page(SAKURA, 'architectural-review') });
/** 8.04.l: Mark Active is disabled with its reason; "No manager" shows in the header. */
export const OnboardingWithNoManager: Story = at('desktop', { decorators: page(KEYSTONE_PARK, 'profile') });
/** 8.04.p: an Archived community opens read-only, with only Restore. */
export const Archived: Story = at('desktop', { decorators: page(OLD_MILL, 'profile') });
/** 8.04.c: a Community Manager in board mode. */
export const CommunityManager: Story = at('desktop', { decorators: page(settings({}, MANAGER_CAN), 'profile', 'board') });
