import { ResidentArcDetail, ResidentArcDraft, ResidentArcListItem } from '../../../core/models';

// 029: shared fixtures for the resident architectural specs and Storybook stories.

export const LIST_ITEMS: ResidentArcListItem[] = [
  { kind: 'Draft', id: 'd1', displayId: null, revision: 1, projectType: 'Solar', projectTitle: 'Roof solar array',
    status: 'Draft', attachmentCount: 0, receivedDate: null, dueDate: null, updatedAt: '2026-10-08T12:00:00Z' },
  { kind: 'Application', id: 'a1', displayId: 'ARC-1042', revision: 1, projectType: 'Fence',
    projectTitle: 'Fence replacement — 6ft cedar', status: 'MoreInfoRequested', attachmentCount: 3,
    receivedDate: '2026-05-28', dueDate: '2026-06-27', updatedAt: '2026-06-01T12:00:00Z' },
  { kind: 'Application', id: 'a2', displayId: 'ARC-1039', revision: 2, projectType: 'ExteriorPaint',
    projectTitle: 'Exterior repaint — Sage 4021', status: 'Approved', attachmentCount: 1,
    receivedDate: '2026-04-01', dueDate: '2026-05-01', updatedAt: '2026-04-20T12:00:00Z' },
];

export const DETAIL: ResidentArcDetail = {
  kind: 'Application', id: 'a1', displayId: 'ARC-1042', revision: 1, projectType: 'Fence',
  projectTitle: 'Fence replacement — 6ft cedar', status: 'Submitted', attachmentCount: 1,
  receivedDate: '2026-05-28', dueDate: '2026-06-27', updatedAt: '2026-05-28T12:00:00Z',
  description: 'Replace the rear fence with 6ft cedar.', plannedStartDate: '2026-07-01', plannedCompletionDate: '2026-07-20',
  contractorName: 'Cedar & Co', contractorContact: '919-555-0100', acknowledgedAt: '2026-05-28T12:00:00Z',
  attachments: [{ id: 'f1', fileName: 'fence-plan.pdf', sizeBytes: 1258291, contentType: 'application/pdf', infoRequestId: null }],
  infoRequests: [],
  timeline: [{ event: 'Submitted', at: '2026-05-28T12:00:00Z' }],
  decision: null, ownerReason: null, formalDisapprovalStatement: null, conditionsOfApproval: null, closedAt: null,
  canWithdraw: true, canRevise: false,
  revisions: [{ id: 'a1', revision: 1, status: 'Submitted', receivedDate: '2026-05-28' }],
};

export const DENIED: ResidentArcDetail = {
  ...DETAIL, status: 'Denied', canWithdraw: false, canRevise: true, closedAt: '2026-06-20T12:00:00Z',
  decision: { outcome: 'Denied', wording: 'RevisionsRequested' },
  ownerReason: 'Lower the fence to 5ft per Guideline 4.2',
  formalDisapprovalStatement: 'This is a formal disapproval of the plans as submitted.',
  timeline: [{ event: 'Submitted', at: '2026-05-28T12:00:00Z' }, { event: 'Denied', at: '2026-06-20T12:00:00Z' }],
};

export const APPROVED: ResidentArcDetail = {
  ...DETAIL, status: 'Approved', canWithdraw: false, decision: { outcome: 'Approved', wording: null },
  conditionsOfApproval: 'Stain to match the existing color', closedAt: '2026-06-20T12:00:00Z',
};

export const MORE_INFO: ResidentArcDetail = {
  ...DETAIL, status: 'MoreInfoRequested',
  infoRequests: [{ id: 'q1', message: 'Please attach a plat survey', requestedAt: '2026-06-01T12:00:00Z', responseMessage: null, respondedAt: null }],
};

export const DRAFT: ResidentArcDraft = {
  id: 'd1', projectType: 'Fence', projectTitle: 'Fence replacement — 6ft cedar', description: 'Rear fence',
  plannedStartDate: '2026-07-01', plannedCompletionDate: '2026-07-20', contractorName: null, contractorContact: null,
  acknowledged: false, previousRevisionId: null, previousDisplayId: null, attachments: [], carriedAttachments: [],
  removedCarriedAttachmentIds: [], updatedAt: '2026-10-08T12:00:00Z',
};
