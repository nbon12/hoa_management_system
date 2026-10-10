import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

// 027: data layer for board architectural review
// (specs/027-board-arc-review/contracts/architectural-applications.md).
// Authorization is server-side; these calls carry no client-derived scope.

export type ArcVoteChoice = 'Approve' | 'RevisionsNeeded' | 'Deny';
export type ArcVoteState = 'CanVote' | 'Voted' | 'Recused' | 'NotEligible';
export type ArcStatus = 'Open' | 'DecisionReached' | 'Closed';

export interface ArcTally {
  approve: number;
  revisionsNeeded: number;
  deny: number;
  notVoted: number;
  eligible: number;
}

export interface ArcMyVote {
  state: ArcVoteState;
  choice?: ArcVoteChoice | null;
}

export interface ArcDecision {
  /** Withdrawn (029) is a resident withdrawal, shown only with the "Show withdrawn" filter. */
  outcome: 'Approved' | 'Denied' | 'Withdrawn';
  wording: 'RevisionsRequested' | 'Denied' | null;
  source: 'Votes' | 'Lapse' | null;
}

export interface ArcListItem {
  id: string;
  displayId: string;
  revision: number;
  propertyAddress: string;
  ownerName: string;
  projectTitle: string;
  attachmentCount: number;
  receivedDate: string;
  dueDate: string;
  overdue: boolean;
  status: ArcStatus;
  decision: ArcDecision | null;
  infoRequested: boolean;
  tally: ArcTally;
  myVote: ArcMyVote;
}

export interface ArcCounts {
  open: number;
  closed: number;
  awaitingMyVote: number;
}

export interface ArcListResponse {
  items: ArcListItem[];
  total: number;
  limit: number;
  offset: number;
  counts: ArcCounts;
}

export interface ArcAttachment {
  id: string;
  fileName: string;
  sizeBytes: number;
  contentType: string;
  /** 029: set when the owner added the file with an info-request reply. */
  infoRequestId?: string | null;
}

export interface ArcVote {
  voterName: string;
  choice: ArcVoteChoice;
  comment: string | null;
  castAt: string;
}

export interface ArcInfoRequest {
  id: string;
  requestedBy: string;
  message: string;
  requestedAt: string;
  respondedAt: string | null;
  /** 029: the owner's reply, visible to board members and managers. */
  responseMessage?: string | null;
}

export interface ArcRevisionSummary {
  id: string;
  revision: number;
  receivedDate: string;
  decision: ArcDecision | null;
}

export interface ArcDetail extends ArcListItem {
  projectType: string;
  description: string;
  attachments: ArcAttachment[];
  votes: ArcVote[];
  infoRequests: ArcInfoRequest[];
  ruleText: string;
  conditionsOfApproval: string | null;
  ownerReason: string | null;
  closedAt: string | null;
  ownerEmailStatus: 'Pending' | 'Sent' | 'Failed' | 'NoOwnerEmail' | null;
  revisions: ArcRevisionSummary[];
}

export interface ArcSettings {
  reviewPeriodDays: number;
  lapseRule: 'FlagOverdueOnly' | 'DeemedApproved' | 'DeemedDenied';
  decisionRule: 'MajorityOfMembers' | 'MajorityOfVotesCastWithQuorum';
  reminderDays: number;
  timeZoneId: string;
  formalDisapprovalStatement: string;
  updatedAt?: string | null;
}

export interface ArcListParams {
  status?: 'open' | 'closed';
  search?: string;
  awaitingMyVote?: boolean;
  /** 029: include resident-withdrawn applications in the closed tab. */
  includeWithdrawn?: boolean;
  limit?: number;
  offset?: number;
}

export interface ArcOutcomeRequest {
  ownerReason?: string | null;
  conditionsOfApproval?: string | null;
  wording?: 'RevisionsRequested' | 'Denied' | null;
}

@Injectable({ providedIn: 'root' })
export class ArchitecturalService {
  private http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  private apps(communityId: string): string {
    return `${this.base}/communities/${communityId}/architectural-applications`;
  }

  list(communityId: string, p: ArcListParams = {}): Promise<ArcListResponse> {
    let params = new HttpParams()
      .set('status', p.status ?? 'open')
      .set('limit', String(p.limit ?? 25))
      .set('offset', String(p.offset ?? 0));
    if (p.search) params = params.set('search', p.search);
    if (p.awaitingMyVote) params = params.set('awaitingMyVote', 'true');
    if (p.includeWithdrawn) params = params.set('includeWithdrawn', 'true');
    return firstValueFrom(this.http.get<ArcListResponse>(this.apps(communityId), { params }));
  }

  detail(communityId: string, applicationId: string): Promise<ArcDetail> {
    return firstValueFrom(this.http.get<ArcDetail>(`${this.apps(communityId)}/${applicationId}`));
  }

  attachmentUrl(communityId: string, applicationId: string, attachmentId: string): Promise<{ url: string; expiresAt: string }> {
    return firstValueFrom(this.http.get<{ url: string; expiresAt: string }>(
      `${this.apps(communityId)}/${applicationId}/attachments/${attachmentId}/url`));
  }

  vote(communityId: string, applicationId: string, choice: ArcVoteChoice, comment?: string | null): Promise<ArcListItem> {
    return firstValueFrom(this.http.post<ArcListItem>(
      `${this.apps(communityId)}/${applicationId}/votes`, { choice, comment: comment || null }));
  }

  requestInfo(communityId: string, applicationId: string, message: string): Promise<ArcInfoRequest & { dueDate: string }> {
    return firstValueFrom(this.http.post<ArcInfoRequest & { dueDate: string }>(
      `${this.apps(communityId)}/${applicationId}/info-requests`, { message }));
  }

  recordOutcome(communityId: string, applicationId: string, body: ArcOutcomeRequest): Promise<ArcDetail> {
    return firstValueFrom(this.http.post<ArcDetail>(`${this.apps(communityId)}/${applicationId}/outcome`, body));
  }

  resendOutcomeEmail(communityId: string, applicationId: string): Promise<{ ownerEmailStatus: string | null }> {
    return firstValueFrom(this.http.post<{ ownerEmailStatus: string | null }>(
      `${this.apps(communityId)}/${applicationId}/outcome/resend-email`, {}));
  }

  getSettings(communityId: string): Promise<ArcSettings> {
    return firstValueFrom(this.http.get<ArcSettings>(`${this.base}/communities/${communityId}/architectural-settings`));
  }

  putSettings(communityId: string, settings: ArcSettings): Promise<ArcSettings> {
    return firstValueFrom(this.http.put<ArcSettings>(`${this.base}/communities/${communityId}/architectural-settings`, settings));
  }
}
