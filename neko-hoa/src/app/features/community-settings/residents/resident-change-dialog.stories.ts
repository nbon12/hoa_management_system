import type { Meta, StoryObj } from '@storybook/angular';
import { ResidentChangeDialogComponent } from './resident-change-dialog.component';
import { RESIDENT_PROPERTY, SAKURA, contextFor, withServices } from '../../../../storybook/company-fixtures';
import { storyAt } from '../../../../storybook/variants';

// 030 T149 — brief 06 §5: remove a resident (lines from the removal preview), revoke an invitation.
const meta: Meta<ResidentChangeDialogComponent> = { title: 'CommunitySettings/ResidentChangeDialog', component: ResidentChangeDialogComponent };
export default meta;
type Story = StoryObj<ResidentChangeDialogComponent>;
const at = storyAt<ResidentChangeDialogComponent>();

const common = { communityId: 'c-sh', communityName: 'Sakura Heights' };
const base: Story = {
  args: { ...common, change: { kind: 'remove', resident: RESIDENT_PROPERTY.residents[0], property: RESIDENT_PROPERTY } },
  decorators: [withServices({ context: contextFor(SAKURA) })],
};

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const RemoveAutopayStays: Story = at('desktop', {
  args: { ...common, change: { kind: 'remove', resident: RESIDENT_PROPERTY.residents[1], property: RESIDENT_PROPERTY } },
  decorators: [withServices({
    context: contextFor(SAKURA),
    settings: { getRemovalPreview: () => Promise.resolve({ autopay: 'Unaffected', autopayReason: 'EnrolledByAnotherResident', otherResidentsEmailed: 0 }) },
  })],
});
export const RevokeInvitation: Story = at('desktop', {
  ...base, args: { ...common, change: { kind: 'revoke', invitation: RESIDENT_PROPERTY.invitations[0], property: RESIDENT_PROPERTY } },
});
