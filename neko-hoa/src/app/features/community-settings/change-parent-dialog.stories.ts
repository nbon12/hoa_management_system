import type { Meta, StoryObj } from '@storybook/angular';
import { ChangeParentDialogComponent } from './change-parent-dialog.component';
import { withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 04 8.04.w: "Change the parent of …?".
const meta: Meta<ChangeParentDialogComponent> = { title: 'CommunitySettings/ChangeParentDialog', component: ChangeParentDialogComponent };
export default meta;
type Story = StoryObj<ChangeParentDialogComponent>;
const at = storyAt<ChangeParentDialogComponent>();

const base: Story = { args: { communityId: 'c-kcm', communityName: 'Keystone Crossing Master', currentParent: null }, decorators: [withServices()] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const WithAParent: Story = at('desktop', {
  ...base, args: { communityId: 'c-th', communityName: 'Keystone Crossing TH', currentParent: { id: 'c-kcm', communityName: 'Keystone Crossing Master' } },
});
