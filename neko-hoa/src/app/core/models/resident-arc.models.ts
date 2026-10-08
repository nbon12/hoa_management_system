// 029: resident architectural requests
// (specs/029-resident-arc-requests/contracts/resident-architectural-applications.md).
// Resident shapes have no vote, voter, tally or comment fields by design (FR-016).

export type ArcProjectType =
  | 'Fence' | 'Solar' | 'ExteriorPaint' | 'Outbuilding' | 'Landscaping' | 'WindowsDoors' | 'Addition' | 'Other';

export type ResidentArcStatus = 'Draft' | 'Submitted' | 'MoreInfoRequested' | 'Approved' | 'Denied' | 'Withdrawn';

export interface ResidentArcAttachment {
  id: string;
  fileName: string;
  sizeBytes: number;
  contentType: string;
  infoRequestId?: string | null;
}

export interface ResidentArcDraftBody {
  projectType: ArcProjectType;
  projectTitle: string;
  description: string;
  plannedStartDate: string | null;
  plannedCompletionDate: string | null;
  contractorName: string | null;
  contractorContact: string | null;
  acknowledged: boolean;
  removedCarriedAttachmentIds?: string[];
}

export interface ResidentArcDraft extends ResidentArcDraftBody {
  id: string;
  previousRevisionId: string | null;
  previousDisplayId: string | null;
  attachments: ResidentArcAttachment[];
  carriedAttachments: ResidentArcAttachment[];
  removedCarriedAttachmentIds: string[];
  updatedAt: string;
}

export interface ResidentArcListItem {
  kind: 'Draft' | 'Application';
  id: string;
  displayId: string | null;
  revision: number;
  projectType: ArcProjectType;
  projectTitle: string;
  status: ResidentArcStatus;
  attachmentCount: number;
  receivedDate: string | null;
  dueDate: string | null;
  updatedAt: string;
}

export interface ResidentArcListResponse {
  items: ResidentArcListItem[];
  total: number;
  limit: number;
  offset: number;
}

export interface ResidentArcInfoRequest {
  id: string;
  message: string;
  requestedAt: string;
  responseMessage: string | null;
  respondedAt: string | null;
}

export interface ResidentArcDetail extends ResidentArcListItem {
  description: string;
  plannedStartDate: string | null;
  plannedCompletionDate: string | null;
  contractorName: string | null;
  contractorContact: string | null;
  acknowledgedAt: string | null;
  attachments: ResidentArcAttachment[];
  infoRequests: ResidentArcInfoRequest[];
  timeline: { event: 'Submitted' | 'InfoRequested' | 'InfoReplied' | 'Approved' | 'Denied' | 'Withdrawn'; at: string }[];
  decision: { outcome: 'Approved' | 'Denied'; wording: 'RevisionsRequested' | 'Denied' | null } | null;
  ownerReason: string | null;
  formalDisapprovalStatement: string | null;
  conditionsOfApproval: string | null;
  closedAt: string | null;
  canWithdraw: boolean;
  canRevise: boolean;
  revisions: { id: string; revision: number; status: ResidentArcStatus; receivedDate: string }[];
}

export interface ResidentArcLink {
  url: string;
  expiresAt: string;
}
