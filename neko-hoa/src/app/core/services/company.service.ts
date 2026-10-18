import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CommunityStatus } from '../models';
import { Paged } from './board.service';
import { ArcSettings } from './architectural.service';

// 030: data layer for the company area (specs/030-company-administration/contracts/company.md).
// Authorization is server-side; these calls carry no client-derived scope. Errors keep the
// `{ code, message }` body; `refusalMessage` turns a code into words.

/** A community reference (`parent`, sub-associations). Shape assumed: the contracts show only `null`. */
export interface CommunityRef {
  id: string;
  communityName: string;
}

// ─── POST /communities ──────────────────────────────────────────────────────

export interface CreateCommunityRequest {
  legalName: string;
  communityName: string;
  county: string;
  /** `YYYY-MM-DD`, not after today (UTC). */
  formationDate: string;
  managementStartDate: string;
  description: string | null;
  parentCommunityId: string | null;
}

export interface CreateCommunityResult {
  id: string;
  communityName: string;
  status: CommunityStatus;
  /** Where the app goes next: the company route for a Company Administrator, `/app/board/settings/profile` for a manager. */
  settingsPath: string;
}

// ─── GET /company/communities (portfolio) ───────────────────────────────────

export type PortfolioStatusFilter = 'onboarding' | 'active' | 'archived';
export type PortfolioSort = 'name' | 'status' | 'parent';

export interface PortfolioQuery {
  status?: PortfolioStatusFilter;
  noManager?: boolean;
  search?: string;
  sort?: PortfolioSort;
  dir?: 'asc' | 'desc';
  limit?: number;
  offset?: number;
}

export interface PortfolioManager {
  userId: string;
  name: string;
}

export interface PortfolioPendingInvitation {
  email: string;
  expiresAt: string;
}

export interface PortfolioItem {
  id: string;
  communityName: string;
  legalName: string;
  status: CommunityStatus;
  parent: CommunityRef | null;
  /** Open-ended managers only. */
  managers: PortfolioManager[];
  pendingManagerInvitations: PortfolioPendingInvitation[];
  noManager: boolean;
  differsFromDefaults: boolean;
}

export interface PortfolioCounts {
  all: number;
  onboarding: number;
  active: number;
  archived: number;
  noManager: number;
}

export interface PortfolioPage extends Paged<PortfolioItem> {
  /** Ignores `search` and paging. */
  counts: PortfolioCounts;
}

// ─── Lifecycle ──────────────────────────────────────────────────────────────

export type LifecycleAction = 'activate' | 'archive' | 'restore';

export interface OpenWorkItem {
  kind: 'ArcApplication' | 'SubAssociation' | string;
  label: string;
  status: string;
}

export interface LifecycleRefusal {
  code: string;
  message: string;
  items?: OpenWorkItem[];
}

export interface RevokedInvitationPreview {
  id: string;
  email: string;
  kind: string;
  role: string | null;
}

export interface MembershipKeptPreview {
  membershipId: string;
  userId: string;
  name: string;
  role: string;
  endDate: string | null;
}

export interface ScheduledInvitationPreview {
  email: string;
  propertyAddress: string;
  expiresAtIfSentNow: string;
}

/** Every consequence a preview can list; which ones are set depends on the action. */
export interface LifecycleConsequences {
  // archive
  autopayCancelled?: number;
  residentsEmailed?: number;
  invitationsRevoked?: RevokedInvitationPreview[];
  membershipsKept?: MembershipKeptPreview[];
  // activate
  scheduledInvitationsToSend?: ScheduledInvitationPreview[];
  scheduledInvitationsRevoked?: ScheduledInvitationPreview[];
  // restore
  returnsTo?: 'Active' | 'Onboarding';
  reason?: 'WasActiveWithManager' | 'NeverActivated' | 'NoOpenEndedManager';
  membershipsRegainingAccess?: MembershipKeptPreview[];
}

export interface LifecyclePreview {
  action: LifecycleAction;
  allowed: boolean;
  refusal: LifecycleRefusal | null;
  consequences: LifecycleConsequences | null;
}

export interface ActivateResult {
  status: CommunityStatus;
  invitationsSent: number;
  invitationsFailedToSend: number;
  invitationsRevoked: number;
}

export interface ArchiveResult {
  status: CommunityStatus;
  autopayCancelled: number;
  residentsEmailed: number;
  invitationsRevoked: number;
}

export interface RestoreResult {
  status: CommunityStatus;
  reason: 'WasActiveWithManager' | 'NeverActivated' | 'NoOpenEndedManager';
}

// ─── Company defaults ───────────────────────────────────────────────────────

/** The six ARC rule fields, as in spec 027's ARC settings PUT. */
export type ArcRuleFields = Pick<ArcSettings,
  'reviewPeriodDays' | 'lapseRule' | 'decisionRule' | 'reminderDays' | 'timeZoneId' | 'formalDisapprovalStatement'>;

export interface CompanyDefaults extends ArcRuleFields {
  updatedAt: string | null;
  /** A UX hint only. */
  canEdit: boolean;
}

// ─── Company Administrators ─────────────────────────────────────────────────

export type CompanyGrantSource = 'Setup' | 'Invitation' | 'Recovery';

export interface CompanyAdministrator {
  userId: string;
  name: string;
  email: string;
  grantedAt: string;
  /** `null` when the source is `Setup`. */
  grantedBy: string | null;
  source: CompanyGrantSource;
}

export type InvitationState = 'Scheduled' | 'Pending' | 'Accepted' | 'Revoked';
export type InvitationEffectiveState = InvitationState | 'Expired';
export type InvitationRevokeReason = 'Manual' | 'CommunityArchived' | 'SenderLostAuthority' | 'Superseded';

export interface CompanyInvitation {
  id: string;
  email: string;
  state: InvitationState;
  effectiveState: InvitationEffectiveState;
  revokeReason: InvitationRevokeReason | null;
  sentBy: string | null;
  sentAt: string | null;
  expiresAt: string | null;
  canResend: boolean;
  canRevoke: boolean;
}

export interface InvitationCreated {
  id: string;
  state: InvitationState;
  expiresAt: string | null;
}

export interface RevokeAdministratorResult {
  revoked: boolean;
  invitationsRevoked: number;
}

function pageParams(limit = 25, offset = 0): HttpParams {
  return new HttpParams().set('limit', String(limit)).set('offset', String(offset));
}

@Injectable({ providedIn: 'root' })
export class CompanyService {
  private http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  // Communities and the portfolio

  getCommunities(query: PortfolioQuery = {}): Promise<PortfolioPage> {
    let params = pageParams(query.limit, query.offset);
    if (query.status) params = params.set('status', query.status);
    if (query.noManager) params = params.set('noManager', 'true');
    if (query.search?.trim()) params = params.set('search', query.search.trim());
    if (query.sort) params = params.set('sort', query.sort);
    if (query.dir) params = params.set('dir', query.dir);
    return firstValueFrom(this.http.get<PortfolioPage>(`${this.base}/company/communities`, { params }));
  }

  createCommunity(body: CreateCommunityRequest): Promise<CreateCommunityResult> {
    return firstValueFrom(this.http.post<CreateCommunityResult>(`${this.base}/communities`, body));
  }

  // Lifecycle: the dialogs ask the server first, then act (brief 04 D4)

  getLifecyclePreview(communityId: string, action: LifecycleAction): Promise<LifecyclePreview> {
    return firstValueFrom(this.http.get<LifecyclePreview>(
      `${this.base}/communities/${communityId}/lifecycle/${action}/preview`));
  }

  activate(communityId: string): Promise<ActivateResult> {
    return firstValueFrom(this.http.post<ActivateResult>(`${this.base}/communities/${communityId}/activate`, {}));
  }

  archive(communityId: string): Promise<ArchiveResult> {
    return firstValueFrom(this.http.post<ArchiveResult>(`${this.base}/communities/${communityId}/archive`, {}));
  }

  restore(communityId: string): Promise<RestoreResult> {
    return firstValueFrom(this.http.post<RestoreResult>(`${this.base}/communities/${communityId}/restore`, {}));
  }

  /** `null` removes the parent. The contract's response body isn't specified; it is returned as-is. */
  changeParent(communityId: string, parentCommunityId: string | null): Promise<unknown> {
    return firstValueFrom(this.http.put<unknown>(`${this.base}/communities/${communityId}/parent`, { parentCommunityId }));
  }

  // Company defaults

  getDefaults(): Promise<CompanyDefaults> {
    return firstValueFrom(this.http.get<CompanyDefaults>(`${this.base}/company/defaults`));
  }

  putDefaults(body: ArcRuleFields): Promise<CompanyDefaults> {
    const fields: ArcRuleFields = {
      reviewPeriodDays: body.reviewPeriodDays,
      lapseRule: body.lapseRule,
      decisionRule: body.decisionRule,
      reminderDays: body.reminderDays,
      timeZoneId: body.timeZoneId,
      formalDisapprovalStatement: body.formalDisapprovalStatement,
    };
    return firstValueFrom(this.http.put<CompanyDefaults>(`${this.base}/company/defaults`, fields));
  }

  // Company Administrators

  getAdministrators(limit = 25, offset = 0): Promise<Paged<CompanyAdministrator>> {
    return firstValueFrom(this.http.get<Paged<CompanyAdministrator>>(
      `${this.base}/company/administrators`, { params: pageParams(limit, offset) }));
  }

  revokeAdministrator(userId: string): Promise<RevokeAdministratorResult> {
    return firstValueFrom(this.http.post<RevokeAdministratorResult>(
      `${this.base}/company/administrators/${userId}/revoke`, {}));
  }

  getInvitations(query: { state?: string; limit?: number; offset?: number } = {}): Promise<Paged<CompanyInvitation>> {
    let params = pageParams(query.limit, query.offset);
    if (query.state) params = params.set('state', query.state);
    return firstValueFrom(this.http.get<Paged<CompanyInvitation>>(`${this.base}/company/invitations`, { params }));
  }

  /** The response is the same whether or not the email has an account (FR-037). */
  inviteAdministrator(email: string): Promise<InvitationCreated> {
    return firstValueFrom(this.http.post<InvitationCreated>(
      `${this.base}/company/administrators/invitations`, { email: email.trim() }));
  }

  resendInvitation(invitationId: string): Promise<CompanyInvitation> {
    return firstValueFrom(this.http.post<CompanyInvitation>(`${this.base}/company/invitations/${invitationId}/resend`, {}));
  }

  revokeInvitation(invitationId: string): Promise<CompanyInvitation> {
    return firstValueFrom(this.http.post<CompanyInvitation>(`${this.base}/company/invitations/${invitationId}/revoke`, {}));
  }
}
