import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CommunitySettingsService } from './community-settings.service';
import { environment } from '../../../environments/environment';

const BASE = environment.apiBaseUrl;
const C = 'c-keystone-park';
const URL = `${BASE}/communities/${C}`;

const RULES = {
  reviewPeriodDays: 30, lapseRule: 'FlagOverdueOnly' as const, decisionRule: 'MajorityOfMembers' as const,
  reminderDays: 7, timeZoneId: 'America/New_York', formalDisapprovalStatement: 'Formal.',
};

// 030 T061: CommunitySettingsService calls exactly the routes in contracts/community-settings.md.
describe('CommunitySettingsService (030 contracts/community-settings.md)', () => {
  let svc: CommunitySettingsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    svc = TestBed.inject(CommunitySettingsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getSettings() reads the page shell', async () => {
    const p = svc.getSettings(C);
    const req = http.expectOne(`${URL}/settings`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: C, communityName: 'Keystone Park', sections: ['profile'] });
    expect((await p).communityName).toBe('Keystone Park');
  });

  it('getProfile() and patchProfile() use /profile, one field per PATCH', async () => {
    const g = svc.getProfile(C);
    http.expectOne(`${URL}/profile`).flush({ communityName: 'Keystone Park', missingRequiredFields: [] });
    await g;

    const p = svc.patchProfile(C, { county: 'Wake County, NC' });
    const req = http.expectOne(`${URL}/profile`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ county: 'Wake County, NC' });
    req.flush({ county: 'Wake County, NC' });
    await p;
  });

  it('getMemberships() is paged', async () => {
    const p = svc.getMemberships(C);
    const req = http.expectOne(r => r.url === `${URL}/memberships`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('limit')).toBe('25');
    expect(req.request.params.get('offset')).toBe('0');
    req.flush({ items: [], total: 0, limit: 25, offset: 0 });
    await p;
  });

  it('getPeople() passes a trimmed search and paging', async () => {
    const p = svc.getPeople(C, { search: ' Lee ', limit: 10, offset: 10 });
    const req = http.expectOne(r => r.url === `${URL}/people`);
    expect(req.request.params.get('search')).toBe('Lee');
    expect(req.request.params.get('limit')).toBe('10');
    expect(req.request.params.get('offset')).toBe('10');
    req.flush({ items: [], total: 0, limit: 10, offset: 10 });
    await p;

    const q = svc.getPeople(C);
    const req2 = http.expectOne(r => r.url === `${URL}/people`);
    expect(req2.request.params.has('search')).toBeFalse();
    req2.flush({ items: [], total: 0, limit: 25, offset: 0 });
    await q;
  });

  it('appoint() POSTs a direct appointment or an email invitation', async () => {
    const a = svc.appoint(C, { role: 'BoardMember', userId: 'u-lee', startDate: '2026-10-12', endDate: '2027-12-31' });
    const ra = http.expectOne(`${URL}/memberships`);
    expect(ra.request.method).toBe('POST');
    expect(ra.request.body).toEqual({ role: 'BoardMember', userId: 'u-lee', startDate: '2026-10-12', endDate: '2027-12-31' });
    ra.flush({ id: 'm1' }, { status: 201, statusText: 'Created' });
    await a;

    const b = svc.appoint(C, { role: 'CommunityManager', email: 'pat@example.com' });
    const rb = http.expectOne(`${URL}/memberships`);
    expect(rb.request.body).toEqual({ role: 'CommunityManager', email: 'pat@example.com' });
    rb.flush({ invitationId: 'i1', state: 'Pending', expiresAt: '2026-10-14T13:15:00Z' }, { status: 201, statusText: 'Created' });
    await b;
  });

  it('updateMembership() PATCHes end, downgrade or end date', async () => {
    for (const change of [{ end: true as const }, { downgradeTo: 'BoardMember' as const }, { endDate: null }]) {
      const p = svc.updateMembership(C, 'm1', change);
      const req = http.expectOne(`${URL}/memberships/m1`);
      expect(req.request.method).toBe('PATCH');
      expect(req.request.body).toEqual(change);
      req.flush({ id: 'm1' });
      await p;
    }
  });

  it('replaceManager() POSTs both user ids', async () => {
    const p = svc.replaceManager(C, { currentManagerUserId: 'u-jordan', replacementUserId: 'u-lee' });
    const req = http.expectOne(`${URL}/managers/replace`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ currentManagerUserId: 'u-jordan', replacementUserId: 'u-lee' });
    req.flush({ ended: { id: 'm1' }, appointed: { id: 'm2' } });
    expect((await p).appointed.id).toBe('m2');
  });

  it('getInvitations() defaults to the membership kind and filters by state', async () => {
    const p = svc.getInvitations(C);
    const req = http.expectOne(r => r.url === `${URL}/invitations`);
    expect(req.request.params.get('kind')).toBe('membership');
    expect(req.request.params.has('state')).toBeFalse();
    req.flush({ items: [], total: 0, limit: 25, offset: 0, timeZoneId: 'America/New_York' });
    await p;

    const q = svc.getInvitations(C, { kind: 'residency', state: 'Pending', limit: 100 });
    const req2 = http.expectOne(r => r.url === `${URL}/invitations`);
    expect(req2.request.params.get('kind')).toBe('residency');
    expect(req2.request.params.get('state')).toBe('Pending');
    expect(req2.request.params.get('limit')).toBe('100');
    req2.flush({ items: [], total: 0, limit: 100, offset: 0 });
    await q;
  });

  it('resendInvitation() and revokeInvitation() POST to the community routes', async () => {
    const a = svc.resendInvitation(C, 'i1');
    const ra = http.expectOne(`${URL}/invitations/i1/resend`);
    expect(ra.request.method).toBe('POST');
    ra.flush({ id: 'i1' });
    await a;

    const b = svc.revokeInvitation(C, 'i1');
    const rb = http.expectOne(`${URL}/invitations/i1/revoke`);
    expect(rb.request.method).toBe('POST');
    rb.flush({ id: 'i1' });
    await b;
  });

  it('getResidents() passes search, filter and paging', async () => {
    const p = svc.getResidents(C, { search: ' 711 ', filter: 'noResident', offset: 25 });
    const req = http.expectOne(r => r.url === `${URL}/residents`);
    expect(req.request.params.get('search')).toBe('711');
    expect(req.request.params.get('filter')).toBe('noResident');
    expect(req.request.params.get('offset')).toBe('25');
    req.flush({ items: [], total: 0, limit: 25, offset: 25, counts: { all: 0, noResident: 0, invitationPending: 0, invitationExpired: 0 }, propertiesLoaded: true });
    await p;

    const q = svc.getResidents(C);
    const req2 = http.expectOne(r => r.url === `${URL}/residents`);
    expect(req2.request.params.keys().sort()).toEqual(['limit', 'offset']);
    req2.flush({ items: [], total: 0, limit: 25, offset: 0, counts: { all: 0, noResident: 0, invitationPending: 0, invitationExpired: 0 }, propertiesLoaded: false });
    expect((await q).propertiesLoaded).toBeFalse();
  });

  it('getResidentProperty(), inviteResident(), getRemovalPreview() and removeResident() use their routes', async () => {
    const a = svc.getResidentProperty(C, 'p29');
    http.expectOne(`${URL}/residents/p29`).flush({ propertyId: 'p29' });
    await a;

    const b = svc.inviteResident(C, 'p29', ' kim@example.com ');
    const rb = http.expectOne(`${URL}/residents/invitations`);
    expect(rb.request.method).toBe('POST');
    expect(rb.request.body).toEqual({ propertyId: 'p29', email: 'kim@example.com' });
    rb.flush({ invitationId: 'i2', state: 'Scheduled', expiresAt: null }, { status: 201, statusText: 'Created' });
    expect((await b).state).toBe('Scheduled');

    const c = svc.getRemovalPreview(C, 'p29', 'u-jane');
    const rc = http.expectOne(r => r.url === `${URL}/residents/p29/removal-preview`);
    expect(rc.request.params.get('userId')).toBe('u-jane');
    rc.flush({ autopay: 'WillCancel', autopayReason: 'EnrolledByThisResident', otherResidentsEmailed: 1 });
    expect((await c).autopay).toBe('WillCancel');

    const d = svc.removeResident(C, 'p29', 'u-jane');
    const rd = http.expectOne(`${URL}/residents/p29/u-jane`);
    expect(rd.request.method).toBe('DELETE');
    rd.flush({ removed: true, autopayCancelled: true });
    expect((await d).removed).toBeTrue();
  });

  it('getArcSettings(), putArcSettings() (six fields only) and resetArc() use /architectural-settings', async () => {
    const g = svc.getArcSettings(C);
    http.expectOne(`${URL}/architectural-settings`).flush({ ...RULES, companyDefaults: RULES, differsFromDefaults: [], canEdit: true });
    expect((await g).canEdit).toBeTrue();

    const p = svc.putArcSettings(C, { ...RULES, companyDefaults: RULES, differsFromDefaults: [], canEdit: true } as never);
    const req = http.expectOne(`${URL}/architectural-settings`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(RULES);
    req.flush({ ...RULES });
    await p;

    const r = svc.resetArc(C);
    const rr = http.expectOne(`${URL}/architectural-settings/reset`);
    expect(rr.request.method).toBe('POST');
    rr.flush({ ...RULES, differsFromDefaults: [] });
    await r;
  });

  it('passes the refusal body through on an error', async () => {
    const p = svc.getSettings(C);
    http.expectOne(`${URL}/settings`).flush({ code: 'FORBIDDEN', message: 'You do not have access to this community.' }, { status: 403, statusText: 'Forbidden' });
    await expectAsync(p).toBeRejectedWith(jasmine.objectContaining({ status: 403, error: jasmine.objectContaining({ code: 'FORBIDDEN' }) }));
  });
});
