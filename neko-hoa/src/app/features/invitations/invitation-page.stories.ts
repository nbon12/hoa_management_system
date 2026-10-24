import type { Meta, StoryObj } from '@storybook/angular';
import { HttpErrorResponse } from '@angular/common/http';
import { InvitationPageComponent } from './invitation-page.component';
import { InvitationPreview, InvitationService } from '../../core/services/invitation.service';
import { SessionRefreshService } from '../../core/services/session-refresh';
import { CurrentUser } from '../../core/models';
import { JORDAN, refused, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 10: the invitation page's states. The link's token is in the address bar's
// fragment (`#t=…`), so each story puts one there before the page reads it.
const meta: Meta<InvitationPageComponent> = { title: 'Invitations/InvitationPage', component: InvitationPageComponent };
export default meta;
type Story = StoryObj<InvitationPageComponent>;
const at = storyAt<InvitationPageComponent>();

const MANAGER_OFFER: InvitationPreview = {
  kind: 'Membership', communityName: 'Keystone Park', offer: { role: 'CommunityManager', termEndDate: null, propertyAddress: null },
  sentBy: 'Robin Hale', expiresAt: '2026-10-15T14:00:00Z', timeZoneId: 'America/New_York', invitedEmail: 'pat@example.com', hasAccount: false,
};
const RESIDENT_OFFER: InvitationPreview = {
  ...MANAGER_OFFER, kind: 'Residency', communityName: 'Sakura Heights', offer: { role: null, termEndDate: null, propertyAddress: '711 Keystone Park Dr #29' },
  sentBy: 'Jordan Blake', invitedEmail: 'kim@example.com',
};

function withLink(preview: () => Promise<InvitationPreview>, user: CurrentUser | null = null, token = 'story-token') {
  return [
    (storyFn: () => unknown) => {
      // The page strips the fragment once read; put it back for every render.
      history.replaceState(history.state, '', `${window.location.pathname}${window.location.search}${token ? `#t=${token}` : ''}`);
      return storyFn();
    },
    withServices({
      user,
      extra: [
        { provide: InvitationService, useValue: { preview, register: () => new Promise(() => undefined), accept: () => new Promise(() => undefined) } },
        { provide: SessionRefreshService, useValue: { refresh: () => Promise.resolve(null) } },
      ],
    }),
  ] as Story['decorators'];
}

const base: Story = { decorators: withLink(() => Promise.resolve(MANAGER_OFFER)) };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const ResidentInvitation: Story = at('phone', { decorators: withLink(() => Promise.resolve(RESIDENT_OFFER)) });
export const HasAnAccount: Story = at('desktop', { decorators: withLink(() => Promise.resolve({ ...MANAGER_OFFER, hasAccount: true })) });
export const SignedInConfirm: Story = at('desktop', {
  decorators: withLink(() => Promise.resolve({ ...MANAGER_OFFER, invitedEmail: null, hasAccount: null }), JORDAN),
});
export const Expired: Story = at('phone', { decorators: withLink(() => Promise.reject(refused(410, 'INVITATION_EXPIRED'))) });
export const NoLongerValid: Story = at('phone', { decorators: withLink(() => Promise.reject(refused(410, 'INVITATION_INVALID'))) });
export const NoToken: Story = at('phone', { decorators: withLink(() => Promise.reject(new HttpErrorResponse({ status: 0 })), null, '') });
