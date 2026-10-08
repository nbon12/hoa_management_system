import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig } from '@storybook/angular';
import { WithdrawDialogComponent } from './withdraw-dialog.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { DETAIL } from './resident-arc-fixtures';

// 029 T066 — confirming a withdrawal.
const meta: Meta<WithdrawDialogComponent> = {
  title: 'Property/Architectural/WithdrawDialog',
  component: WithdrawDialogComponent,
  decorators: [applicationConfig({ providers: [{ provide: ResidentArchitecturalService, useValue: {
    withdraw: () => Promise.resolve({ ...DETAIL, status: 'Withdrawn' }),
  } }] })],
  args: { applicationId: 'a1', displayId: 'ARC-1042' },
};
export default meta;
type Story = StoryObj<WithdrawDialogComponent>;

export const Open: Story = {};
