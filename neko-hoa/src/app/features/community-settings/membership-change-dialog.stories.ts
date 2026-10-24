import type { Meta, StoryObj } from '@storybook/angular';
import { MembershipChangeDialogComponent } from './membership-change-dialog.component';
import { MEMBERSHIPS, ROLE_INVITATIONS, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 05 §5.4–§5.8: end, downgrade, change the end date, replace, revoke.
const meta: Meta<MembershipChangeDialogComponent> = { title: 'CommunitySettings/MembershipChangeDialog', component: MembershipChangeDialogComponent };
export default meta;
type Story = StoryObj<MembershipChangeDialogComponent>;
const at = storyAt<MembershipChangeDialogComponent>();

const common = { communityId: 'c-sh', communityName: 'Sakura Heights', memberships: MEMBERSHIPS, currentUserId: 'u-robin', audience: 'companyAdmin' as const };
const base: Story = { args: { ...common, request: { kind: 'end', membership: MEMBERSHIPS[1] } }, decorators: [withServices()] };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const Downgrade: Story = at('desktop', { ...base, args: { ...common, request: { kind: 'downgrade', membership: MEMBERSHIPS[1] } } });
export const ChangeEndDate: Story = at('desktop', { ...base, args: { ...common, request: { kind: 'endDate', membership: MEMBERSHIPS[2] } } });
export const ReplaceTheLastManager: Story = at('desktop', { ...base, args: { ...common, request: { kind: 'replace', membership: MEMBERSHIPS[0] } } });
export const RevokeInvitation: Story = at('desktop', { ...base, args: { ...common, request: { kind: 'revoke', invitation: ROLE_INVITATIONS[0] } } });
export const EndWhileArchived: Story = at('desktop', { ...base, args: { ...common, archived: true, request: { kind: 'end', membership: MEMBERSHIPS[0] } } });
