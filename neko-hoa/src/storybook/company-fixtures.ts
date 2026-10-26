import { Provider, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { applicationConfig } from '@storybook/angular';
import { CurrentUser } from '../app/core/models';
import { AuthService } from '../app/core/services/auth.service';
import { BoardService } from '../app/core/services/board.service';
import {
  CommunitySettings, CommunitySettingsService, PropertyInvitation, ResidentProperty, ResidentsPage, SettingsArcSettings,
  SettingsMembership, CommunityInvitation,
} from '../app/core/services/community-settings.service';
import {
  ArcRuleFields, CompanyAdministrator, CompanyInvitation, CompanyService, LifecycleAction, LifecyclePreview, PortfolioItem,
  PortfolioPage,
} from '../app/core/services/company.service';
import { CommunitySettingsContext, SettingsMode } from '../app/features/community-settings/community-settings-context';

// 030 T149: the people, communities and values of conventions §4, for the company-administration
// stories. Services are stubbed: no story makes a request.

export const NOW = '2026-10-12T14:00:00Z';
const TZ = 'America/New_York';
const STATEMENT = "This is a formal disapproval of the plans as submitted under the community's governing documents. You may revise and resubmit.";

// ─── People ──────────────────────────────────────────────────────────────────

export const ROBIN: CurrentUser = {
  id: 'u-robin', firstName: 'Robin', lastName: 'Hale', email: 'owner@example.com', initials: 'RH', lastActiveMode: 'Resident',
  memberships: [], properties: [], isCompanyAdministrator: true, landing: 'Company',
};

export const JORDAN: CurrentUser = {
  id: 'u-jordan', firstName: 'Jordan', lastName: 'Blake', email: 'jordan@example.com', initials: 'JB', lastActiveMode: 'Board',
  properties: [], isCompanyAdministrator: false, landing: 'Board',
  memberships: [
    { communityId: 'c-sh', communityName: 'Sakura Heights', role: 'CommunityManager', communityStatus: 'Active' },
    { communityId: 'c-kr', communityName: 'Keystone Ridge', role: 'CommunityManager', communityStatus: 'Onboarding' },
  ],
};

export const DANA: CurrentUser = {
  id: 'u-dana', firstName: 'Dana', lastName: 'Fox', email: 'dana@example.com', initials: 'DF', lastActiveMode: 'Resident',
  memberships: [], properties: [], isCompanyAdministrator: false, landing: 'NoHome',
};

// ─── Communities ─────────────────────────────────────────────────────────────

const NONE = { allowed: false, reasonCode: null };
const YES = { allowed: true, reasonCode: null };

export function settings(over: Partial<CommunitySettings> = {}, can: Partial<CommunitySettings['can']> = {}): CommunitySettings {
  return {
    id: 'c-sh', communityName: 'Sakura Heights', legalName: 'Sakura Heights Homeowners Association, Inc.', status: 'Active',
    parent: null, noManager: false, missingRequiredFields: [], sections: ['profile', 'managers-and-memberships', 'architectural-review'],
    can: {
      editProfile: true, changeParent: true, activate: NONE, archive: YES, restore: NONE, manageManagers: true,
      replaceLastManager: true, manageMemberships: true, manageResidents: true, editArcSettings: true, endMembershipsWhileArchived: false,
      ...can,
    },
    ...over,
  };
}

export const SAKURA = settings();
export const KEYSTONE_PARK = settings(
  { id: 'c-kp', communityName: 'Keystone Park', legalName: 'Keystone Park HOA, Inc.', status: 'Onboarding', noManager: true },
  { activate: { allowed: false, reasonCode: 'NO_ACTIVE_MANAGER' } },
);
export const OLD_MILL = settings(
  { id: 'c-om', communityName: 'Old Mill Commons', legalName: 'Old Mill Commons Association, Inc.', status: 'Archived' },
  {
    editProfile: false, changeParent: false, archive: NONE, restore: YES, manageManagers: false, replaceLastManager: false,
    manageMemberships: false, manageResidents: false, editArcSettings: false, endMembershipsWhileArchived: true,
  },
);
export const MANAGER_CAN = { changeParent: false, activate: NONE, archive: NONE, restore: NONE, replaceLastManager: false };

export const PROFILE = {
  legalName: 'Sakura Heights Homeowners Association, Inc.', communityName: 'Sakura Heights', county: 'Wake County, NC',
  formationDate: '1998-06-09', managementStartDate: '2016-07-01', description: null, missingRequiredFields: [] as string[],
};

function member(id: string, userDisplayName: string, role: string, over: Partial<SettingsMembership> = {}): SettingsMembership {
  return {
    id, userId: `u-${id}`, userDisplayName, role, status: 'Active', startDate: '2024-01-01', endDate: null, openEnded: true,
    effective: true, canEnd: true, canDowngrade: true, canEditEndDate: true, reasonCode: null, ...over,
  };
}

export const MEMBERSHIPS: SettingsMembership[] = [
  member('m-jordan', 'Jordan Blake', 'CommunityManager', { canEnd: false, canDowngrade: false, canEditEndDate: false, reasonCode: 'LAST_MANAGER' }),
  member('m-taylor', 'Taylor Grant', 'CommunityManager', { endDate: '2026-12-31', openEnded: false }),
  member('m-lee', 'Lee Morales', 'BoardMember', { endDate: '2027-12-31', openEnded: false }),
  member('m-avery', 'Avery Quinn', 'Accountant'),
  member('m-dana', 'Dana Fox', 'BoardMember', { endDate: '2026-06-30', openEnded: false, effective: false, status: 'Inactive', canEnd: false, canDowngrade: false, canEditEndDate: false }),
];

export const ROLE_INVITATIONS: CommunityInvitation[] = [{
  id: 'i-new', kind: 'Membership', email: 'new@example.com', role: 'BoardMember', termEndDate: '2027-12-31', state: 'Pending',
  effectiveState: 'Pending', revokeReason: null, sentBy: 'Jordan Blake', sentAt: '2026-10-11T14:30:00Z', expiresAt: '2026-10-14T14:30:00Z',
  lastSendError: null, canResend: true, canRevoke: true, resendBlockedReason: null,
}];

export const RESIDENTS: ResidentsPage = {
  items: [
    { propertyId: 'p29', address: '711 Keystone Park Dr #29', residentNames: ['Jane Alvarez', 'Sam Ortiz'], matchedEmail: null, invitationSummary: { scheduled: 0, pending: 1, expired: 0 } },
    { propertyId: 'p31', address: '715 Keystone Park Dr #31', residentNames: [], matchedEmail: null, invitationSummary: { scheduled: 0, pending: 0, expired: 1 } },
    { propertyId: 'p35', address: '719 Keystone Park Dr #35', residentNames: [], matchedEmail: null, invitationSummary: { scheduled: 0, pending: 1, expired: 0 } },
    { propertyId: 'p41', address: '801 Keystone Park Dr', residentNames: ['Priya Shah'], matchedEmail: null, invitationSummary: { scheduled: 0, pending: 0, expired: 0 } },
  ],
  total: 120, limit: 25, offset: 0, counts: { all: 120, noResident: 7, invitationPending: 2, invitationExpired: 1 }, propertiesLoaded: true,
};

const KIM: PropertyInvitation = {
  id: 'i-kim', email: 'kim@example.com', state: 'Pending', effectiveState: 'Pending', sentBy: 'Jordan Blake',
  sentAt: '2026-10-10T20:40:00Z', expiresAt: '2026-10-13T20:40:00Z', revokeReason: null, canResend: true, canRevoke: true, resendBlockedReason: null,
};

export const RESIDENT_PROPERTY: ResidentProperty = {
  propertyId: 'p29', address: '711 Keystone Park Dr #29',
  residents: [
    { userId: 'u-jane', name: 'Jane Alvarez', email: 'jane@example.com', residentSince: '2024-03-02' },
    { userId: 'u-sam', name: 'Sam Ortiz', email: 'sam@example.com', residentSince: '2026-10-09' },
  ],
  invitations: [KIM], timeZoneId: TZ, canInvite: true,
};

export const DEFAULTS: ArcRuleFields = {
  reviewPeriodDays: 30, reminderDays: 7, lapseRule: 'FlagOverdueOnly', decisionRule: 'MajorityOfMembers',
  timeZoneId: TZ, formalDisapprovalStatement: STATEMENT,
};

export const ARC: SettingsArcSettings = {
  ...DEFAULTS, reviewPeriodDays: 45, reminderDays: 10, updatedAt: '2026-09-01T12:00:00Z',
  companyDefaults: DEFAULTS, differsFromDefaults: ['reviewPeriodDays', 'reminderDays'], canEdit: true,
};

function portfolioRow(id: string, communityName: string, status: PortfolioItem['status'], over: Partial<PortfolioItem> = {}): PortfolioItem {
  return {
    id, communityName, legalName: `${communityName} Association, Inc.`, status, parent: null,
    managers: [{ userId: 'u-jordan', name: 'Jordan Blake' }], pendingManagerInvitations: [], noManager: false, differsFromDefaults: false, ...over,
  };
}

export const PORTFOLIO: PortfolioPage = {
  items: [
    portfolioRow('c-bh', 'Briar Hollow', 'Onboarding', { managers: [], noManager: true }),
    portfolioRow('c-kcm', 'Keystone Crossing Master', 'Active'),
    portfolioRow('c-sf', 'Keystone Crossing SF', 'Active', { parent: { id: 'c-kcm', communityName: 'Keystone Crossing Master' }, differsFromDefaults: true }),
    portfolioRow('c-th', 'Keystone Crossing TH', 'Active', { legalName: '', parent: { id: 'c-kcm', communityName: 'Keystone Crossing Master' } }),
    portfolioRow('c-kp', 'Keystone Park', 'Onboarding', { managers: [], noManager: true, pendingManagerInvitations: [{ email: 'pat@example.com', expiresAt: '2026-10-15T14:00:00Z' }] }),
    portfolioRow('c-kr', 'Keystone Ridge', 'Onboarding'),
    portfolioRow('c-om', 'Old Mill Commons', 'Archived', { managers: [{ userId: 'u-morgan', name: 'Morgan Ellis' }] }),
    portfolioRow('c-sh', 'Sakura Heights', 'Active'),
  ],
  total: 31, limit: 25, offset: 0, counts: { all: 31, onboarding: 3, active: 26, archived: 2, noManager: 2 },
};

export const ADMINISTRATORS: CompanyAdministrator[] = [
  { userId: 'u-robin', name: 'Robin Hale', email: 'owner@example.com', grantedAt: '2026-10-01T14:00:00Z', grantedBy: null, source: 'Setup' },
  { userId: 'u-casey', name: 'Casey Novak', email: 'casey@example.com', grantedAt: '2026-10-02T14:00:00Z', grantedBy: 'Robin Hale', source: 'Invitation' },
];

export const ADMIN_INVITATIONS: CompanyInvitation[] = [{
  id: 'i-ops', email: 'ops@example.com', state: 'Pending', effectiveState: 'Pending', revokeReason: null, sentBy: 'Robin Hale',
  sentAt: '2026-10-12T12:30:00Z', expiresAt: '2026-10-15T12:30:00Z', canResend: true, canRevoke: true,
}];

const KEPT = [
  { membershipId: 'm-morgan', userId: 'u-morgan', name: 'Morgan Ellis', role: 'CommunityManager', endDate: null },
  { membershipId: 'm-rowan', userId: 'u-rowan', name: 'Rowan Hayes', role: 'BoardMember', endDate: null },
  { membershipId: 'm-avery', userId: 'u-avery', name: 'Avery Quinn', role: 'Accountant', endDate: null },
];

export const PREVIEWS: Record<LifecycleAction, LifecyclePreview> = {
  activate: {
    action: 'activate', allowed: true, refusal: null,
    consequences: {
      scheduledInvitationsToSend: [
        { email: 'lena@example.com', propertyAddress: '2 Ridgeview Ct', expiresAtIfSentNow: '2026-10-15T14:00:00Z' },
        { email: 'omar@example.com', propertyAddress: '8 Ridgeview Ct', expiresAtIfSentNow: '2026-10-15T14:00:00Z' },
      ],
      scheduledInvitationsRevoked: [],
    },
  },
  archive: {
    action: 'archive', allowed: true, refusal: null,
    consequences: {
      autopayCancelled: 3, residentsEmailed: 79, membershipsKept: KEPT,
      invitationsRevoked: [{ id: 'i-drew', email: 'drew@example.com', kind: 'Membership', role: 'CommunityManager' }],
    },
  },
  restore: {
    action: 'restore', allowed: true, refusal: null,
    consequences: { returnsTo: 'Active', reason: 'WasActiveWithManager', membershipsRegainingAccess: KEPT },
  },
};

export const OPEN_WORK: LifecyclePreview = {
  action: 'archive', allowed: false, consequences: null,
  refusal: {
    code: 'OPEN_WORK', message: 'Open work.', items: [
      { kind: 'ArcApplication', label: 'ARC-1041', status: 'Open' },
      { kind: 'SubAssociation', label: 'Keystone Crossing SF', status: 'Active' },
    ],
  },
};

// ─── Stubs ───────────────────────────────────────────────────────────────────

const ok = <T>(value: T) => () => Promise.resolve(value);
const paged = <T>(items: T[], limit = 25) => ({ items, total: items.length, limit, offset: 0 });

export function settingsServiceStub(over: Record<string, unknown> = {}): Partial<CommunitySettingsService> {
  return {
    getSettings: ok(SAKURA),
    getProfile: ok(PROFILE),
    patchProfile: (_: string, patch: object) => Promise.resolve({ ...PROFILE, ...patch }),
    getMemberships: ok(paged(MEMBERSHIPS, 100)),
    getPeople: ok(paged([{ userId: 'u-lee', name: 'Lee Morales', rolesHere: ['BoardMember'], residentOf: [] }])),
    appoint: ok({ invitationId: 'i-x', state: 'Pending', expiresAt: '2026-10-15T14:00:00Z' }) as never,
    updateMembership: ok(MEMBERSHIPS[1]),
    getInvitations: ok({ ...paged(ROLE_INVITATIONS, 100), timeZoneId: TZ }),
    resendInvitation: ok(ROLE_INVITATIONS[0]),
    revokeInvitation: ok(ROLE_INVITATIONS[0]),
    getResidents: ok(RESIDENTS),
    getResidentProperty: ok(RESIDENT_PROPERTY),
    inviteResident: ok({ invitationId: 'i-r', state: 'Pending', expiresAt: '2026-10-15T14:00:00Z' }),
    getRemovalPreview: ok({ autopay: 'WillCancel', autopayReason: 'EnrolledByThisResident', otherResidentsEmailed: 1 }),
    removeResident: ok({ removed: true, autopayCancelled: true }),
    getArcSettings: ok(ARC),
    putArcSettings: ok(ARC),
    resetArc: ok({ ...ARC, reviewPeriodDays: 30, reminderDays: 7, differsFromDefaults: [] }),
    ...over,
  } as Partial<CommunitySettingsService>;
}

export function companyServiceStub(over: Record<string, unknown> = {}): Partial<CompanyService> {
  return {
    getCommunities: ok(PORTFOLIO),
    createCommunity: ok({ id: 'c-new', communityName: 'Keystone Park', status: 'Onboarding', settingsPath: '/app/company/communities/c-new/settings/profile' }),
    getLifecyclePreview: (_: string, action: LifecycleAction) => Promise.resolve(PREVIEWS[action]),
    activate: ok({ status: 'Active', invitationsSent: 2, invitationsFailedToSend: 0, invitationsRevoked: 0 }),
    archive: ok({ status: 'Archived', autopayCancelled: 3, residentsEmailed: 79, invitationsRevoked: 1 }),
    restore: ok({ status: 'Active', reason: 'WasActiveWithManager' }),
    changeParent: ok({}),
    getDefaults: ok({ ...DEFAULTS, updatedAt: null, canEdit: true }),
    putDefaults: (body: ArcRuleFields) => Promise.resolve({ ...body, updatedAt: NOW, canEdit: true }),
    getAdministrators: ok(paged(ADMINISTRATORS)),
    getInvitations: ok(paged(ADMIN_INVITATIONS)),
    inviteAdministrator: ok({ id: 'i-ops', state: 'Pending', expiresAt: '2026-10-15T12:30:00Z' }),
    revokeAdministrator: ok({ revoked: true, invitationsRevoked: 0 }),
    resendInvitation: ok(ADMIN_INVITATIONS[0]),
    revokeInvitation: ok(ADMIN_INVITATIONS[0]),
    ...over,
  } as Partial<CompanyService>;
}

export function boardServiceStub(): Partial<BoardService> {
  return {
    getMyCommunities: ok(paged([
      { id: 'c-sh', communityName: 'Sakura Heights', role: 'CommunityManager', status: 'Active' },
      { id: 'c-kr', communityName: 'Keystone Ridge', role: 'CommunityManager', status: 'Onboarding' },
    ])),
  } as Partial<BoardService>;
}

export function authServiceStub(user: CurrentUser | null): Partial<AuthService> {
  const current = signal(user);
  return {
    user: current.asReadonly(),
    isLoggedIn: () => current() !== null,
    login: () => Promise.resolve(),
    logout: () => undefined,
    refreshCurrentUser: () => Promise.resolve(current()!),
    switchProperty: () => Promise.resolve(),
    switchMode: () => Promise.resolve(),
  } as Partial<AuthService>;
}

export function contextFor(s: CommunitySettings, mode: SettingsMode = 'company'): CommunitySettingsContext {
  const ctx = new CommunitySettingsContext();
  ctx.settings.set(s);
  ctx.mode.set(mode);
  ctx.setReloader(() => Promise.resolve());
  return ctx;
}

/** An HTTP refusal with the 030 `{ code, message }` body. */
export function refused(status: number, code: string): HttpErrorResponse {
  return new HttpErrorResponse({ status, error: { code, message: code } });
}

/** The app-level providers a company-administration story needs, with any overrides. */
export function withServices(opts: {
  user?: CurrentUser | null;
  settings?: Record<string, unknown>;
  company?: Record<string, unknown>;
  context?: CommunitySettingsContext;
  extra?: Provider[];
} = {}) {
  return applicationConfig({
    providers: [
      // A catch-all route, so the iframe's own address and in-story links never fail to match.
      provideRouter([{ path: '**', children: [] }]),
      { provide: AuthService, useValue: authServiceStub(opts.user === undefined ? ROBIN : opts.user) },
      { provide: CommunitySettingsService, useValue: settingsServiceStub(opts.settings) },
      { provide: CompanyService, useValue: companyServiceStub(opts.company) },
      { provide: BoardService, useValue: boardServiceStub() },
      ...(opts.context ? [{ provide: CommunitySettingsContext, useValue: opts.context }] : []),
      ...(opts.extra ?? []),
    ],
  });
}
