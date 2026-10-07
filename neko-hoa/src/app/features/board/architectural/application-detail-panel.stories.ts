import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig } from '@storybook/angular';
import { ApplicationDetailPanelComponent } from './application-detail-panel.component';
import { ArcDetail, ArchitecturalService } from '../../../core/services/architectural.service';
import { SAMPLE_DETAIL } from './arc-fixtures';

// 027 T043/T075 — the detail panel from the BoardArchApps wireframe: details, attachments, votes and
// the "Cast your vote" card; plus a v2 revision and the manager's outcome form.
function withDetail(detail: ArcDetail) {
  return applicationConfig({
    providers: [{
      provide: ArchitecturalService,
      useValue: { detail: () => Promise.resolve(detail), attachmentUrl: () => Promise.resolve({ url: '#', expiresAt: '' }) },
    }],
  });
}

const meta: Meta<ApplicationDetailPanelComponent> = {
  title: 'Board/Architectural/DetailPanel',
  component: ApplicationDetailPanelComponent,
  args: { communityId: 'c1', applicationId: 'a1042', manager: false },
};
export default meta;
type Story = StoryObj<ApplicationDetailPanelComponent>;

export const CanVote: Story = { decorators: [withDetail(SAMPLE_DETAIL)] };

export const RevisionV2: Story = {
  decorators: [withDetail({
    ...SAMPLE_DETAIL, revision: 2,
    revisions: [
      { id: 'v1', revision: 1, receivedDate: '2026-04-01', decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' } },
      { id: 'a1042', revision: 2, receivedDate: '2026-05-28', decision: null },
    ],
  })],
};

export const ManagerRecordsDenial: Story = {
  args: { manager: true },
  decorators: [withDetail({
    ...SAMPLE_DETAIL, status: 'DecisionReached', myVote: { state: 'NotEligible' },
    decision: { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' },
  })],
};

export const NoAttachments: Story = { decorators: [withDetail({ ...SAMPLE_DETAIL, attachments: [], attachmentCount: 0 })] };
