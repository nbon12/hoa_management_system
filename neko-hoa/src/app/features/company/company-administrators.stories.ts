import type { Meta, StoryObj } from '@storybook/angular';
import { CompanyAdministratorsComponent } from './company-administrators.component';
import { ADMINISTRATORS, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 09: Company administrators and their invitations.
const meta: Meta<CompanyAdministratorsComponent> = { title: 'Company/CompanyAdministrators', component: CompanyAdministratorsComponent };
export default meta;
type Story = StoryObj<CompanyAdministratorsComponent>;
const at = storyAt<CompanyAdministratorsComponent>();

const base: Story = { decorators: [withServices()] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
/** US7-S3: the last one's "Revoke role" is disabled, with its reason on hover and focus. */
export const LastAdministrator: Story = at('desktop', {
  decorators: [withServices({ company: {
    getAdministrators: () => Promise.resolve({ items: [ADMINISTRATORS[0]], total: 1, limit: 25, offset: 0 }),
    getInvitations: () => Promise.resolve({ items: [], total: 0, limit: 25, offset: 0 }),
  } })],
});
