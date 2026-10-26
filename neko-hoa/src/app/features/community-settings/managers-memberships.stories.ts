import type { Meta, StoryObj } from '@storybook/angular';
import { ManagersMembershipsSectionComponent } from './managers-memberships.component';
import { OLD_MILL, SAKURA, contextFor, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 05: Managers and memberships (the Residents card holds brief 06's area).
const meta: Meta<ManagersMembershipsSectionComponent> = { title: 'CommunitySettings/ManagersAndMemberships', component: ManagersMembershipsSectionComponent };
export default meta;
type Story = StoryObj<ManagersMembershipsSectionComponent>;
const at = storyAt<ManagersMembershipsSectionComponent>();

const base: Story = { decorators: [withServices({ context: contextFor(SAKURA) })] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
/** 8.04.q: only "End membership" while archived. */
export const Archived: Story = at('desktop', { decorators: [withServices({ context: contextFor(OLD_MILL) })] });
