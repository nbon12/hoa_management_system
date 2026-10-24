import type { Meta, StoryObj } from '@storybook/angular';
import { CompanyDefaultsComponent } from './company-defaults.component';
import { DEFAULTS, JORDAN, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 08: the company defaults, editable for a Company Administrator and read-only
// for a Community Manager in board mode.
const meta: Meta<CompanyDefaultsComponent> = { title: 'Company/CompanyDefaults', component: CompanyDefaultsComponent };
export default meta;
type Story = StoryObj<CompanyDefaultsComponent>;
const at = storyAt<CompanyDefaultsComponent>();

const base: Story = { args: { mode: 'company' }, decorators: [withServices()] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const ReadOnlyForACommunityManager: Story = at('desktop', {
  args: { mode: 'board' },
  decorators: [withServices({ user: JORDAN, company: { getDefaults: () => Promise.resolve({ ...DEFAULTS, updatedAt: null, canEdit: false }) } })],
});
export const ReadOnlyOnAPhone: Story = at('phone', ReadOnlyForACommunityManager);
