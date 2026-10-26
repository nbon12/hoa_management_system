import type { Meta, StoryObj } from '@storybook/angular';
import { PortfolioComponent } from './portfolio.component';
import { PORTFOLIO, refused, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 02: the portfolio, with its flags, filters, search and sortable columns.
const meta: Meta<PortfolioComponent> = { title: 'Company/Portfolio', component: PortfolioComponent };
export default meta;
type Story = StoryObj<PortfolioComponent>;
const at = storyAt<PortfolioComponent>();

const base: Story = { decorators: [withServices()] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const NoCommunitiesYet: Story = at('desktop', {
  decorators: [withServices({ company: { getCommunities: () => Promise.resolve({ ...PORTFOLIO, items: [], total: 0, counts: { all: 0, onboarding: 0, active: 0, archived: 0, noManager: 0 } }) } })],
});
export const LoadError: Story = at('desktop', {
  decorators: [withServices({ company: { getCommunities: () => Promise.reject(refused(0, '')) } })],
});
