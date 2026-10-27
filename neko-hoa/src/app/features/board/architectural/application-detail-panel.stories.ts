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

// 031: skeletons while loading, the vote-change grace period, a lapse approval citing the governing documents,
// and the homeowner's comments.
export const Loading: Story = {
  decorators: [applicationConfig({ providers: [{ provide: ArchitecturalService, useValue: { detail: () => new Promise(() => {}) } }] })],
};

export const VotedCanUndo: Story = {
  decorators: [withDetail({
    ...SAMPLE_DETAIL, status: 'DecisionReached', decision: { outcome: 'Approved', wording: null, source: 'Votes' },
    tally: { approve: 3, revisionsNeeded: 0, deny: 0, notVoted: 2, eligible: 5 },
    myVote: { state: 'Voted', choice: 'Approve' }, canUndoVote: true, decisionFinal: false, votesLockAt: '2026-06-20T16:00:00Z',
    votes: [...SAMPLE_DETAIL.votes, { voterName: 'Bianca Board', choice: 'Approve', comment: null, castAt: '2026-06-19T16:00:00Z' }],
  })],
};

export const ApprovedByDefault: Story = {
  decorators: [withDetail({
    ...SAMPLE_DETAIL, displayId: 'ARC-1040', projectTitle: 'Backyard pergola — cedar, 12x14', status: 'Closed', decisionFinal: true,
    decision: { outcome: 'Approved', wording: null, source: 'Lapse' }, myVote: { state: 'NotEligible' },
    tally: { approve: 1, revisionsNeeded: 0, deny: 0, notVoted: 4, eligible: 5 },
    lapseStatement: 'Approved by default: the board did not approve or deny this application by its due date (06/27/26). Under ' +
      'Article IX, Section 9.4 of the Declaration of Covenants, Conditions and Restrictions, an application the board does not ' +
      'act on within the 30-day review period is deemed approved.',
  })],
};

export const WithHomeownerComments: Story = {
  decorators: [withDetail({
    ...SAMPLE_DETAIL,
    ownerComments: [{
      id: 'c1', authorName: 'Praneeth Pattyam', body: 'Lowered the fence to 5ft — revised plan attached.', createdAt: '2026-06-05T15:00:00Z',
      attachments: [{ id: 'c1f', fileName: 'revised-plan.pdf', sizeBytes: 812_000, contentType: 'application/pdf', ownerCommentId: 'c1' }],
    }],
  })],
};
