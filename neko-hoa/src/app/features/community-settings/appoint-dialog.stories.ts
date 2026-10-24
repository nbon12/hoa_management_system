import type { Meta, StoryObj } from '@storybook/angular';
import { AppointDialogComponent } from './appoint-dialog.component';
import { withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 05 §5.2–§5.3: appoint a Community Manager, or a board member or accountant.
const meta: Meta<AppointDialogComponent> = { title: 'CommunitySettings/AppointDialog', component: AppointDialogComponent };
export default meta;
type Story = StoryObj<AppointDialogComponent>;
const at = storyAt<AppointDialogComponent>();

const base: Story = {
  args: { kind: 'manager', communityId: 'c-sh', communityName: 'Sakura Heights', communityStatus: 'Active', hasOpenEndedManager: true },
  decorators: [withServices()],
};

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const BoardMemberOrAccountant: Story = at('desktop', { ...base, args: { ...base.args, kind: 'board' } });
export const NoOneInTheCommunityYet: Story = at('desktop', {
  args: { ...base.args, communityStatus: 'Onboarding', hasOpenEndedManager: false },
  decorators: [withServices({ settings: { getPeople: () => Promise.resolve({ items: [], total: 0, limit: 25, offset: 0 }) } })],
});
