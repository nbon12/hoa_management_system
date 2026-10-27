import type { Meta, StoryObj } from '@storybook/angular';
import { LifecycleDialogsComponent } from './lifecycle-dialogs.component';
import { LifecycleAction } from '../../core/services/company.service';
import { CommunitySettings } from '../../core/services/community-settings.service';
import { KEYSTONE_PARK, OLD_MILL, OPEN_WORK, SAKURA, contextFor, settings, withServices } from '../../../storybook/company-fixtures';
import { storyAt } from '../../../storybook/variants';

// 030 T149 — brief 04 §5.4–§5.7: the Mark Active, Archive and Restore dialogs.
const meta: Meta<LifecycleDialogsComponent> = { title: 'CommunitySettings/LifecycleDialogs', component: LifecycleDialogsComponent };
export default meta;
type Story = StoryObj<LifecycleDialogsComponent>;
const at = storyAt<LifecycleDialogsComponent>();

function opened(s: CommunitySettings, action: LifecycleAction, company: Record<string, unknown> = {}) {
  const ctx = contextFor(s);
  ctx.requestLifecycle(action);
  return [withServices({ context: ctx, company })];
}

const base: Story = { decorators: opened(SAKURA, 'archive') };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
export const MarkActive: Story = at('desktop', { decorators: opened(settings({ id: 'c-kr', communityName: 'Keystone Ridge', status: 'Onboarding' }), 'activate') });
export const MarkActiveRefused: Story = at('desktop', {
  decorators: opened(KEYSTONE_PARK, 'activate', {
    getLifecyclePreview: () => Promise.resolve({ action: 'activate', allowed: false, consequences: null, refusal: { code: 'NO_ACTIVE_MANAGER', message: 'x' } }),
  }),
});
export const ArchiveRefusedOpenWork: Story = at('desktop', { decorators: opened(SAKURA, 'archive', { getLifecyclePreview: () => Promise.resolve(OPEN_WORK) }) });
export const Restore: Story = at('desktop', { decorators: opened(OLD_MILL, 'restore') });
export const RestoreOnAPhone: Story = at('phone', { decorators: opened(OLD_MILL, 'restore') });
