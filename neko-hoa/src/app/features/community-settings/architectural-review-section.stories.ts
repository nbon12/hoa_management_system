import type { Meta, StoryObj } from '@storybook/angular';
import { ArchitecturalReviewSectionComponent } from './architectural-review-section.component';
import { ARC, DEFAULTS, OLD_MILL, SAKURA, contextFor, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 07: the Architectural review section, with the defaults comparison and reset.
const meta: Meta<ArchitecturalReviewSectionComponent> = { title: 'CommunitySettings/ArchitecturalReviewSection', component: ArchitecturalReviewSectionComponent };
export default meta;
type Story = StoryObj<ArchitecturalReviewSectionComponent>;
const at = storyAt<ArchitecturalReviewSectionComponent>();

const base: Story = { decorators: [withServices({ context: contextFor(SAKURA) })] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
/** 8.07.c: everything matches, so Reset is disabled with its reason beside it. */
export const MatchesDefaults: Story = at('desktop', {
  decorators: [withServices({ context: contextFor(SAKURA, 'board'), settings: { getArcSettings: () => Promise.resolve({ ...ARC, ...DEFAULTS, differsFromDefaults: [] }) } })],
});
/** 8.04.r: plain text while archived. */
export const Archived: Story = at('desktop', {
  decorators: [withServices({ context: contextFor(OLD_MILL), settings: { getArcSettings: () => Promise.resolve({ ...ARC, canEdit: false }) } })],
});
