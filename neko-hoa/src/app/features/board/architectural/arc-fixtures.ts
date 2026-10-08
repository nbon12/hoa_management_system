import { ArcDetail, ArcListItem } from '../../../core/services/architectural.service';

// 027: wireframe-shaped sample data for Storybook (BoardArchApps / NeedsYourVote).
export const SAMPLE_ROW: ArcListItem = {
  id: 'a1042', displayId: 'ARC-1042', revision: 1, propertyAddress: '711 Keystone Park Dr #29', ownerName: 'Praneeth Pattyam',
  projectTitle: 'Fence replacement — 6ft cedar', attachmentCount: 3, receivedDate: '2026-05-28', dueDate: '2026-06-27',
  overdue: false, status: 'Open', decision: null, infoRequested: false,
  tally: { approve: 2, revisionsNeeded: 0, deny: 0, notVoted: 3, eligible: 5 }, myVote: { state: 'CanVote' },
};

export const SAMPLE_DETAIL: ArcDetail = {
  ...SAMPLE_ROW, projectType: 'Fence', description: 'Replace the rear fence with 6ft cedar along the lot line.',
  attachments: [
    { id: 'f1', fileName: 'fence-plan.pdf', sizeBytes: 1_258_291, contentType: 'application/pdf' },
    { id: 'f2', fileName: 'elevation.jpg', sizeBytes: 860_160, contentType: 'image/jpeg' },
    { id: 'f3', fileName: 'plat-survey.pdf', sizeBytes: 2_202_009, contentType: 'application/pdf' },
  ],
  votes: [
    { voterName: 'Aaliyah Brooks', choice: 'Approve', comment: null, castAt: '2026-06-01T14:03:00Z' },
    { voterName: 'Marcus Chen', choice: 'Approve', comment: 'Matches the existing fence line.', castAt: '2026-06-02T09:10:00Z' },
  ],
  infoRequests: [],
  ruleText: 'Three of five votes decide. The manager records the outcome and notifies the owner.',
  conditionsOfApproval: null, ownerReason: null, closedAt: null, ownerEmailStatus: null,
  revisions: [],
};
