import type { Meta, StoryObj } from '@storybook/angular';
import { ResidentsSectionComponent } from './residents-section.component';
import { OLD_MILL, RESIDENTS, SAKURA, contextFor, settings, withServices } from '../../../../storybook/company-fixtures';
import { storyAt } from '../../../../storybook/variants';

// 030 T149 — brief 06: the Residents area (rows, search, filters; actions in the property drawer).
const meta: Meta<ResidentsSectionComponent> = { title: 'CommunitySettings/Residents', component: ResidentsSectionComponent };
export default meta;
type Story = StoryObj<ResidentsSectionComponent>;
const at = storyAt<ResidentsSectionComponent>();

const base: Story = { decorators: [withServices({ context: contextFor(SAKURA) })] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
/** 8.06.u: no properties loaded yet, so no invite action. */
export const PropertiesNotLoaded: Story = at('desktop', {
  decorators: [withServices({
    context: contextFor(settings({ id: 'c-kp', communityName: 'Keystone Park', status: 'Onboarding' })),
    settings: { getResidents: () => Promise.resolve({ ...RESIDENTS, items: [], total: 0, propertiesLoaded: false }) },
  })],
});
/** 8.06.v: read-only while archived. */
export const Archived: Story = at('desktop', { decorators: [withServices({ context: contextFor(OLD_MILL) })] });
