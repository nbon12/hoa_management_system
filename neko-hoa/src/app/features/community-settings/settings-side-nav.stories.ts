import type { Meta, StoryObj } from '@storybook/angular';
import { SettingsSideNavComponent } from './settings-side-nav.component';
import { sectionsFor } from './settings-registry';
import { withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 04 §4.1: the searchable side list; a menu below 768 px.
const meta: Meta<SettingsSideNavComponent> = { title: 'CommunitySettings/SideNav', component: SettingsSideNavComponent };
export default meta;
type Story = StoryObj<SettingsSideNavComponent>;
const at = storyAt<SettingsSideNavComponent>();

const base: Story = {
  args: { sections: sectionsFor(['profile', 'managers-and-memberships', 'architectural-review']), current: 'profile', basePath: ['/app/company/communities', 'c-sh', 'settings'] },
  decorators: [withServices()],
};

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
