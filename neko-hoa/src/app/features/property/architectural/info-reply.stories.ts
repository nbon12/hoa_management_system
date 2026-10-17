import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig } from '@storybook/angular';
import { InfoReplyComponent } from './info-reply.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { DETAIL, MORE_INFO } from './resident-arc-fixtures';

// 029 T066 — answering the board's question.
const meta: Meta<InfoReplyComponent> = {
  title: 'Property/Architectural/InfoReply',
  component: InfoReplyComponent,
  decorators: [applicationConfig({ providers: [{ provide: ResidentArchitecturalService, useValue: {
    reply: () => Promise.resolve(DETAIL),
    uploadReplyAttachment: () => Promise.resolve({ id: 'f2', fileName: 'survey.pdf', sizeBytes: 1000, contentType: 'application/pdf' }),
  } }] })],
  args: { applicationId: 'a1', request: MORE_INFO.infoRequests[0] },
};
export default meta;
type Story = StoryObj<InfoReplyComponent>;

export const Unanswered: Story = {};
