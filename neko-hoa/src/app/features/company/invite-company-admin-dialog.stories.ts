import type { Meta, StoryObj } from '@storybook/angular';
import { InviteCompanyAdminDialogComponent } from './invite-company-admin-dialog.component';
import { withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 09 8.09.b: "Invite a Company Administrator".
const meta: Meta<InviteCompanyAdminDialogComponent> = { title: 'Company/InviteCompanyAdminDialog', component: InviteCompanyAdminDialogComponent };
export default meta;
type Story = StoryObj<InviteCompanyAdminDialogComponent>;
const at = storyAt<InviteCompanyAdminDialogComponent>();

const base: Story = { decorators: [withServices()] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
