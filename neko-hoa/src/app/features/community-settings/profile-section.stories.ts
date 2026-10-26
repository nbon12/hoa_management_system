import type { Meta, StoryObj } from '@storybook/angular';
import { ProfileSectionComponent } from './profile-section.component';
import { CommunitySettings } from '../../core/services/community-settings.service';
import { MANAGER_CAN, OLD_MILL, PROFILE, SAKURA, contextFor, settings, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 04 §4.2: the Profile section, saved per field.
const meta: Meta<ProfileSectionComponent> = { title: 'CommunitySettings/ProfileSection', component: ProfileSectionComponent };
export default meta;
type Story = StoryObj<ProfileSectionComponent>;
const at = storyAt<ProfileSectionComponent>();

function section(s: CommunitySettings, mode: 'company' | 'board' = 'company', profile: object = PROFILE) {
  return [withServices({ context: contextFor(s, mode), settings: { getProfile: () => Promise.resolve(profile) } })];
}

const base: Story = { decorators: section(SAKURA) };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
/** 8.04.h: required fields still empty from the 025 backfill. */
export const MissingRequiredFields: Story = at('desktop', {
  decorators: section(settings({}, MANAGER_CAN), 'board', {
    ...PROFILE, legalName: null, county: null, formationDate: null, managementStartDate: null,
    missingRequiredFields: ['legalName', 'county', 'formationDate', 'managementStartDate'],
  }),
});
/** 8.04.p: values as plain text. */
export const Archived: Story = at('desktop', { decorators: section(OLD_MILL) });
