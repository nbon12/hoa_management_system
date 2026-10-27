import type { Meta, StoryObj } from '@storybook/angular';
import { InviteResidentDialogComponent } from './invite-resident-dialog.component';
import { SAKURA, contextFor, withServices } from '../../../../storybook/company-fixtures';
import { storyAt } from '../../../../storybook/variants';

// 030 T149 — brief 06 8.06.f, h, l: invite a resident, from the drawer or from the header.
const meta: Meta<InviteResidentDialogComponent> = { title: 'CommunitySettings/InviteResidentDialog', component: InviteResidentDialogComponent };
export default meta;
type Story = StoryObj<InviteResidentDialogComponent>;
const at = storyAt<InviteResidentDialogComponent>();

const fixed = { propertyId: 'p29', address: '711 Keystone Park Dr #29', residentCount: 2 };
const base: Story = {
  args: { communityId: 'c-sh', communityName: 'Sakura Heights', fixedProperty: fixed },
  decorators: [withServices({ context: contextFor(SAKURA) })],
};

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const ChooseTheProperty: Story = at('desktop', { ...base, args: { communityId: 'c-sh', communityName: 'Sakura Heights', fixedProperty: null } });
export const Onboarding: Story = at('desktop', {
  ...base, args: { communityId: 'c-kr', communityName: 'Keystone Ridge', onboarding: true, fixedProperty: { propertyId: 'r8', address: '8 Ridgeview Ct', residentCount: 0 } },
});
