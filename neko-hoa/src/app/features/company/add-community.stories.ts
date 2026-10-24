import type { Meta, StoryObj } from '@storybook/angular';
import { AddCommunityComponent } from './add-community.component';
import { JORDAN, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 03: Add community, in the company area and in board mode.
const meta: Meta<AddCommunityComponent> = { title: 'Company/AddCommunity', component: AddCommunityComponent };
export default meta;
type Story = StoryObj<AddCommunityComponent>;
const at = storyAt<AddCommunityComponent>();

const base: Story = { args: { variant: 'company' }, decorators: [withServices()] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const CommunityManagerInBoardMode: Story = at('desktop', { args: { variant: 'board' }, decorators: [withServices({ user: JORDAN })] });
