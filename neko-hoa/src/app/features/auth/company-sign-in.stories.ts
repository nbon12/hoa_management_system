import type { Meta, StoryObj } from '@storybook/angular';
import { CompanySignInComponent } from './company-sign-in.component';
import { withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 01 / FR-036c: the bookmarkable company sign-in page.
const meta: Meta<CompanySignInComponent> = { title: 'Auth/CompanySignIn', component: CompanySignInComponent };
export default meta;
type Story = StoryObj<CompanySignInComponent>;
const at = storyAt<CompanySignInComponent>();

const base: Story = { decorators: [withServices({ user: null })] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
