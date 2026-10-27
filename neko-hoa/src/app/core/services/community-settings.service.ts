import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CommunityStatus } from '../models';
import { Paged } from './board.service';
import { ArcSettings } from './architectural.service';
import {
  ArcRuleFields, CommunityRef, InvitationEffectiveState, InvitationRevokeReason, InvitationState,
} from './company.service';

// 030: data layer for one community's Community settings page
// (specs/030-company-administration/contracts/community-settings.md). Every route is community
// scoped and authorized server-side with DecideAsync: 403 FORBIDDEN, 409 COMMUNITY_ARCHIVED or
// 409 COMMUNITY_NOT_ACTIVE come back as `{ code, message }`. `can.*` drives the UI only.

// ─── GET /communities/{id}/settings (the page shell) ────────────────────────

/** Section ids, in the order the side list shows them. `notifications` never appears (FR-029). */
export type SettingsSectionId = 'profile' | 'managers-and-memberships' | 'architectural-review';

export interface CapabilityWithReason {
  allowed: boolean;
  /** Why it isn't allowed, when that helps (for example `NO_ACTIVE_MANAGER`); otherwise null. */
  reasonCode: string | null;
}

export interface CommunitySettingsCan {
  editProfile: boolean;
  changeParent: boolean;
  activate: CapabilityWithReason;
  archive: CapabilityWithReason;
  restore: CapabilityWithReason;
  manageManagers: boolean;
  replaceLastManager: boolean;
  manageMemberships: boolean;
  manageResidents: boolean;
  editArcSettings: boolean;
  endMembershipsWhileArchived: boolean;
}

export interface CommunitySettings {
  id: string;
  communityName: string;
  /** Empty for a community backfilled by spec 025. */
  legalName: string | null;
  status: CommunityStatus;
  parent: CommunityRef | null;
  /** Shown only to Company Administrators; `null` for everyone else. */
  noManager: boolean | null;
  missingRequiredFields: string[];
  /** Server order; unknown ids are ignored by the page. */
  sections: string[];
  can: CommunitySettingsCan;
}

// ─── Profile (saved per field, FR-017) ──────────────────────────────────────

export interface CommunityProfile {
  legalName: string | null;
  communityName: string;
  county: string | null;
  formationDate: string | null;
  managementStartDate: string | null;
  description: string | null;
  missingRequiredFields: string[];
}

export type ProfileField = 'legalName' | 'communityName' | 'county' | 'formationDate' | 'managementStartDate' | 'description';
export type ProfilePatch = Partial<Pick<CommunityProfile, ProfileField>>;

// ─── Managers and memberships ───────────────────────────────────────────────

/** 025's MembershipDto, extended by spec 030 (contracts: "each item gains …"). */
export interface SettingsMembership {
  id: string;
  userId: string;
  userDisplayName: string;
  role: string;
  status: string;
  startDate: string;
  endDate: string | null;
  openEnded: boolean;
  effective: boolean;
  canEnd: boolean;
  canDowngrade: boolean;
  canEditEndDate: boolean;
  /** Why a `can*` above is false, for example `LAST_MANAGER`; otherwise null. */
  reasonCode: string | null;
}

export interface CommunityPerson {
  userId: string;
  name: string;
  rolesHere: string[];
  residentOf: string[];
}

/** Exactly one of `userId` (direct appointment) or `email` (an invitation). */
export type AppointRequest =
  | { role: string; userId: string; startDate?: string; endDate?: string | null }
  | { role: string; email: string; endDate?: string | null };

export interface InvitationSent {
  invitationId: string;
  state: InvitationState;
  expiresAt: string | null;
}

export type MembershipChange = { end: true } | { downgradeTo: 'BoardMember' } | { endDate: string | null };

export interface ReplaceManagerRequest {
  currentManagerUserId: string;
  replacementUserId: string;
}

export interface ReplaceManagerResult {
  ended: SettingsMembership;
  appointed: SettingsMembership;
}

export interface CommunityInvitation {
  id: string;
  kind: string;
  email: string;
  role: string | null;
  termEndDate: string | null;
  state: InvitationState;
  effectiveState: InvitationEffectiveState;
  revokeReason: InvitationRevokeReason | null;
  sentBy: string | null;
  sentAt: string | null;
  expiresAt: string | null;
  lastSendError: string | null;
  canResend: boolean;
  canRevoke: boolean;
  /** `COMMUNITY_NOT_ACTIVE` for a scheduled invitation. */
  resendBlockedReason: string | null;
}

export interface CommunityInvitationPage extends Paged<CommunityInvitation> {
  /** Times are UTC; show them in this zone. */
  timeZoneId?: string;
}

// ─── Residents (US8) ────────────────────────────────────────────────────────

export type ResidentsFilter = 'noResident' | 'invitationPending' | 'invitationExpired';

export interface ResidentRow {
  propertyId: string;
  address: string;
  residentNames: string[];
  /** Set only when the search matched a resident's email. */
  matchedEmail: string | null;
  invitationSummary: { scheduled: number; pending: number; expired: number };
}

export interface ResidentsPage extends Paged<ResidentRow> {
  counts: { all: number; noResident: number; invitationPending: number; invitationExpired: number };
  /** False when the community has no properties: show the empty state and no invite action. */
  propertiesLoaded: boolean;
}

export interface PropertyResident {
  userId: string;
  name: string;
  email: string;
  residentSince: string;
}

export interface PropertyInvitation {
  id: string;
  email: string;
  state: InvitationState;
  effectiveState: InvitationEffectiveState;
  sentBy: string | null;
  sentAt: string | null;
  expiresAt: string | null;
  revokeReason: InvitationRevokeReason | null;
  canResend: boolean;
  canRevoke: boolean;
  resendBlockedReason: string | null;
}

export interface ResidentProperty {
  propertyId: string;
  address: string;
  residents: PropertyResident[];
  invitations: PropertyInvitation[];
  timeZoneId: string;
  canInvite: boolean;
}

export interface RemovalPreview {
  autopay: 'WillCancel' | 'Unaffected' | 'None';
  autopayReason: 'EnrolledByThisResident' | 'NoEnrollerRecorded' | 'EnrolledByAnotherResident' | null;
  otherResidentsEmailed: number;
}

export interface RemoveResidentResult {
  removed: boolean;
  autopayCancelled: boolean;
}

// ─── Architectural review section (FR-030, FR-031) ──────────────────────────

export interface SettingsArcSettings extends ArcSettings {
  companyDefaults: ArcRuleFields;
  /** Field names that differ from the company defaults, for example `["reviewPeriodDays"]`. */
  differsFromDefaults: string[];
  canEdit: boolean;
}

function pageParams(limit = 25, offset = 0): HttpParams {
  return new HttpParams().set('limit', String(limit)).set('offset', String(offset));
}

@Injectable({ providedIn: 'root' })
export class CommunitySettingsService {
  private http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  private url(communityId: string, path = ''): string {
    return `${this.base}/communities/${encodeURIComponent(communityId)}${path}`;
  }

  // The page shell

  getSettings(communityId: string): Promise<CommunitySettings> {
    return firstValueFrom(this.http.get<CommunitySettings>(this.url(communityId, '/settings')));
  }

  // Profile

  getProfile(communityId: string): Promise<CommunityProfile> {
    return firstValueFrom(this.http.get<CommunityProfile>(this.url(communityId, '/profile')));
  }

  /** The UI sends one field per request, when the person leaves the field. */
  patchProfile(communityId: string, patch: ProfilePatch): Promise<CommunityProfile> {
    return firstValueFrom(this.http.patch<CommunityProfile>(this.url(communityId, '/profile'), patch));
  }

  // Managers and memberships

  getMemberships(communityId: string, limit = 25, offset = 0): Promise<Paged<SettingsMembership>> {
    return firstValueFrom(this.http.get<Paged<SettingsMembership>>(
      this.url(communityId, '/memberships'), { params: pageParams(limit, offset) }));
  }

  getPeople(communityId: string, query: { search?: string; limit?: number; offset?: number } = {}): Promise<Paged<CommunityPerson>> {
    let params = pageParams(query.limit, query.offset);
    if (query.search?.trim()) params = params.set('search', query.search.trim());
    return firstValueFrom(this.http.get<Paged<CommunityPerson>>(this.url(communityId, '/people'), { params }));
  }

  /** 201 is a `SettingsMembership` for a direct appointment, `InvitationSent` for an email. */
  appoint(communityId: string, body: AppointRequest): Promise<SettingsMembership | InvitationSent> {
    return firstValueFrom(this.http.post<SettingsMembership | InvitationSent>(this.url(communityId, '/memberships'), body));
  }

  updateMembership(communityId: string, membershipId: string, change: MembershipChange): Promise<SettingsMembership> {
    return firstValueFrom(this.http.patch<SettingsMembership>(
      this.url(communityId, `/memberships/${encodeURIComponent(membershipId)}`), change));
  }

  replaceManager(communityId: string, body: ReplaceManagerRequest): Promise<ReplaceManagerResult> {
    return firstValueFrom(this.http.post<ReplaceManagerResult>(this.url(communityId, '/managers/replace'), body));
  }

  // Invitations (membership kind by default; residency invitations come with the residents)

  getInvitations(communityId: string, query: { kind?: string; state?: string; limit?: number; offset?: number } = {}): Promise<CommunityInvitationPage> {
    let params = pageParams(query.limit, query.offset).set('kind', query.kind ?? 'membership');
    if (query.state) params = params.set('state', query.state);
    return firstValueFrom(this.http.get<CommunityInvitationPage>(this.url(communityId, '/invitations'), { params }));
  }

  resendInvitation(communityId: string, invitationId: string): Promise<CommunityInvitation> {
    return firstValueFrom(this.http.post<CommunityInvitation>(
      this.url(communityId, `/invitations/${encodeURIComponent(invitationId)}/resend`), {}));
  }

  revokeInvitation(communityId: string, invitationId: string): Promise<CommunityInvitation> {
    return firstValueFrom(this.http.post<CommunityInvitation>(
      this.url(communityId, `/invitations/${encodeURIComponent(invitationId)}/revoke`), {}));
  }

  // Residents

  getResidents(communityId: string, query: { search?: string; filter?: ResidentsFilter; limit?: number; offset?: number } = {}): Promise<ResidentsPage> {
    let params = pageParams(query.limit, query.offset);
    if (query.search?.trim()) params = params.set('search', query.search.trim());
    if (query.filter) params = params.set('filter', query.filter);
    return firstValueFrom(this.http.get<ResidentsPage>(this.url(communityId, '/residents'), { params }));
  }

  getResidentProperty(communityId: string, propertyId: string): Promise<ResidentProperty> {
    return firstValueFrom(this.http.get<ResidentProperty>(
      this.url(communityId, `/residents/${encodeURIComponent(propertyId)}`)));
  }

  /** Scheduled in an Onboarding community, sent at once otherwise; identical whether or not the email has an account. */
  inviteResident(communityId: string, propertyId: string, email: string): Promise<InvitationSent> {
    return firstValueFrom(this.http.post<InvitationSent>(
      this.url(communityId, '/residents/invitations'), { propertyId, email: email.trim() }));
  }

  getRemovalPreview(communityId: string, propertyId: string, userId: string): Promise<RemovalPreview> {
    return firstValueFrom(this.http.get<RemovalPreview>(
      this.url(communityId, `/residents/${encodeURIComponent(propertyId)}/removal-preview`),
      { params: new HttpParams().set('userId', userId) }));
  }

  removeResident(communityId: string, propertyId: string, userId: string): Promise<RemoveResidentResult> {
    return firstValueFrom(this.http.delete<RemoveResidentResult>(
      this.url(communityId, `/residents/${encodeURIComponent(propertyId)}/${encodeURIComponent(userId)}`)));
  }

  // Architectural review

  getArcSettings(communityId: string): Promise<SettingsArcSettings> {
    return firstValueFrom(this.http.get<SettingsArcSettings>(this.url(communityId, '/architectural-settings')));
  }

  /** One Save for the section. Sends the six rule fields only. */
  putArcSettings(communityId: string, body: ArcRuleFields): Promise<SettingsArcSettings> {
    const fields: ArcRuleFields = {
      reviewPeriodDays: body.reviewPeriodDays,
      lapseRule: body.lapseRule,
      decisionRule: body.decisionRule,
      reminderDays: body.reminderDays,
      timeZoneId: body.timeZoneId,
      formalDisapprovalStatement: body.formalDisapprovalStatement,
    };
    return firstValueFrom(this.http.put<SettingsArcSettings>(this.url(communityId, '/architectural-settings'), fields));
  }

  /** "Reset architectural review to company defaults" (FR-031). Returns the GET body. */
  resetArc(communityId: string): Promise<SettingsArcSettings> {
    return firstValueFrom(this.http.post<SettingsArcSettings>(this.url(communityId, '/architectural-settings/reset'), {}));
  }
}
