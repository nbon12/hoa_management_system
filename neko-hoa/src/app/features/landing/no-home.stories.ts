import type { Meta, StoryObj } from '@storybook/angular';
import { NoHomeComponent } from './no-home.component';
import { DANA, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 11 8.11.a: "No home linked to this account".
const meta: Meta<NoHomeComponent> = { title: 'Landing/NoHome', component: NoHomeComponent };
export default meta;
type Story = StoryObj<NoHomeComponent>;
const at = storyAt<NoHomeComponent>();

const base: Story = { decorators: [withServices({ user: DANA })] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
