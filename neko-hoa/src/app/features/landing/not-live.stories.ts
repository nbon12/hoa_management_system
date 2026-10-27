import type { Meta, StoryObj } from '@storybook/angular';
import { NotLiveComponent } from './not-live.component';
import { DANA, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 11 8.11.b: "Your community isn't live yet".
const meta: Meta<NotLiveComponent> = { title: 'Landing/NotLive', component: NotLiveComponent };
export default meta;
type Story = StoryObj<NotLiveComponent>;
const at = storyAt<NotLiveComponent>();

const ANN = { ...DANA, id: 'u-ann', firstName: 'Ann', lastName: 'Brooks', email: 'ann@example.com', initials: 'AB', landing: 'NotLive' as const };
const base: Story = { decorators: [withServices({ user: ANN })] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
