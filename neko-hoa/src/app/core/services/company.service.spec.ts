import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CompanyService } from './company.service';
import { environment } from '../../../environments/environment';

const BASE = environment.apiBaseUrl;
const C = 'c-keystone-park';

const RULES = {
  reviewPeriodDays: 30, lapseRule: 'FlagOverdueOnly' as const, decisionRule: 'MajorityOfMembers' as const,
  reminderDays: 7, timeZoneId: 'America/New_York', formalDisapprovalStatement: 'Formal.',
};

// 030 T061: CompanyService calls exactly the routes in contracts/company.md.
describe('CompanyService (030 contracts/company.md)', () => {
  let svc: CompanyService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    svc = TestBed.inject(CompanyService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getCommunities() defaults to limit 25, offset 0 and sends no filters', async () => {
    const p = svc.getCommunities();
    const req = http.expectOne(r => r.url === `${BASE}/company/communities`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.keys().sort()).toEqual(['limit', 'offset']);
    expect(req.request.params.get('limit')).toBe('25');
    expect(req.request.params.get('offset')).toBe('0');
    const body = { items: [], total: 0, limit: 25, offset: 0, counts: { all: 0, onboarding: 0, active: 0, archived: 0, noManager: 0 } };
    req.flush(body);
    expect(await p).toEqual(body);
  });

  it('getCommunities() passes status, noManager, a trimmed search, sort and dir', async () => {
    const p = svc.getCommunities({ status: 'onboarding', noManager: true, search: '  Keystone ', sort: 'parent', dir: 'desc', limit: 50, offset: 25 });
    const req = http.expectOne(r => r.url === `${BASE}/company/communities`);
    expect(req.request.params.get('status')).toBe('onboarding');
    expect(req.request.params.get('noManager')).toBe('true');
    expect(req.request.params.get('search')).toBe('Keystone');
    expect(req.request.params.get('sort')).toBe('parent');
    expect(req.request.params.get('dir')).toBe('desc');
    expect(req.request.params.get('limit')).toBe('50');
    expect(req.request.params.get('offset')).toBe('25');
    req.flush({ items: [], total: 0, limit: 50, offset: 25, counts: { all: 0, onboarding: 0, active: 0, archived: 0, noManager: 0 } });
    await p;
  });

  it('getCommunities() leaves out a blank search', async () => {
    const p = svc.getCommunities({ search: '   ' });
    const req = http.expectOne(r => r.url === `${BASE}/company/communities`);
    expect(req.request.params.has('search')).toBeFalse();
    req.flush({ items: [], total: 0, limit: 25, offset: 0, counts: { all: 0, onboarding: 0, active: 0, archived: 0, noManager: 0 } });
    await p;
  });

  it('createCommunity() POSTs the contract body to /communities', async () => {
    const body = {
      legalName: 'Keystone Park HOA, Inc.', communityName: 'Keystone Park', county: 'Hamilton',
      formationDate: '2004-03-14', managementStartDate: '2026-11-01', description: null, parentCommunityId: null,
    };
    const p = svc.createCommunity(body);
    const req = http.expectOne(`${BASE}/communities`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ id: C, communityName: 'Keystone Park', status: 'Onboarding', settingsPath: `/app/company/communities/${C}/settings/profile` },
      { status: 201, statusText: 'Created' });
    expect((await p).status).toBe('Onboarding');
  });

  it('getLifecyclePreview() asks the preview route for the action', async () => {
    for (const action of ['activate', 'archive', 'restore'] as const) {
      const p = svc.getLifecyclePreview(C, action);
      const req = http.expectOne(`${BASE}/communities/${C}/lifecycle/${action}/preview`);
      expect(req.request.method).toBe('GET');
      req.flush({ action, allowed: true, refusal: null, consequences: {} });
      expect((await p).action).toBe(action);
    }
  });

  it('activate(), archive() and restore() POST to their routes', async () => {
    const a = svc.activate(C);
    const ra = http.expectOne(`${BASE}/communities/${C}/activate`);
    expect(ra.request.method).toBe('POST');
    ra.flush({ status: 'Active', invitationsSent: 2, invitationsFailedToSend: 0, invitationsRevoked: 0 });
    expect((await a).invitationsSent).toBe(2);

    const b = svc.archive(C);
    const rb = http.expectOne(`${BASE}/communities/${C}/archive`);
    expect(rb.request.method).toBe('POST');
    rb.flush({ status: 'Archived', autopayCancelled: 3, residentsEmailed: 79, invitationsRevoked: 2 });
    expect((await b).autopayCancelled).toBe(3);

    const c = svc.restore(C);
    const rc = http.expectOne(`${BASE}/communities/${C}/restore`);
    expect(rc.request.method).toBe('POST');
    rc.flush({ status: 'Onboarding', reason: 'NoOpenEndedManager' });
    expect((await c).reason).toBe('NoOpenEndedManager');
  });

  it('changeParent() PUTs the parent id, or null to remove it', async () => {
    const p = svc.changeParent(C, 'c-master');
    const req = http.expectOne(`${BASE}/communities/${C}/parent`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ parentCommunityId: 'c-master' });
    req.flush({});
    await p;

    const q = svc.changeParent(C, null);
    const req2 = http.expectOne(`${BASE}/communities/${C}/parent`);
    expect(req2.request.body).toEqual({ parentCommunityId: null });
    req2.flush({});
    await q;
  });

  it('getDefaults() and putDefaults() use /company/defaults, sending the six fields only', async () => {
    const g = svc.getDefaults();
    http.expectOne(`${BASE}/company/defaults`).flush({ ...RULES, updatedAt: null, canEdit: true });
    expect((await g).canEdit).toBeTrue();

    const p = svc.putDefaults({ ...RULES, updatedAt: 'x', canEdit: true } as never);
    const req = http.expectOne(`${BASE}/company/defaults`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(RULES);
    req.flush({ ...RULES, updatedAt: '2026-10-12T14:00:00Z', canEdit: true });
    await p;
  });

  it('getAdministrators() is paged', async () => {
    const p = svc.getAdministrators(50, 50);
    const req = http.expectOne(r => r.url === `${BASE}/company/administrators`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('limit')).toBe('50');
    expect(req.request.params.get('offset')).toBe('50');
    req.flush({ items: [], total: 2, limit: 50, offset: 50 });
    expect((await p).total).toBe(2);
  });

  it('getInvitations() is paged and filters by state when given', async () => {
    const p = svc.getInvitations({ state: 'Pending' });
    const req = http.expectOne(r => r.url === `${BASE}/company/invitations`);
    expect(req.request.params.get('state')).toBe('Pending');
    expect(req.request.params.get('limit')).toBe('25');
    req.flush({ items: [], total: 0, limit: 25, offset: 0 });
    await p;

    const q = svc.getInvitations();
    const req2 = http.expectOne(r => r.url === `${BASE}/company/invitations`);
    expect(req2.request.params.has('state')).toBeFalse();
    req2.flush({ items: [], total: 0, limit: 25, offset: 0 });
    await q;
  });

  it('inviteAdministrator() POSTs a trimmed email', async () => {
    const p = svc.inviteAdministrator(' ops@example.com ');
    const req = http.expectOne(`${BASE}/company/administrators/invitations`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'ops@example.com' });
    req.flush({ id: 'i1', state: 'Pending', expiresAt: '2026-10-15T12:30:00Z' }, { status: 201, statusText: 'Created' });
    expect((await p).state).toBe('Pending');
  });

  it('revokeAdministrator(), resendInvitation() and revokeInvitation() POST to their routes', async () => {
    const a = svc.revokeAdministrator('u-casey');
    const ra = http.expectOne(`${BASE}/company/administrators/u-casey/revoke`);
    expect(ra.request.method).toBe('POST');
    ra.flush({ revoked: true, invitationsRevoked: 1 });
    expect((await a).invitationsRevoked).toBe(1);

    const b = svc.resendInvitation('i1');
    const rb = http.expectOne(`${BASE}/company/invitations/i1/resend`);
    expect(rb.request.method).toBe('POST');
    rb.flush({ id: 'i1' });
    await b;

    const c = svc.revokeInvitation('i1');
    const rc = http.expectOne(`${BASE}/company/invitations/i1/revoke`);
    expect(rc.request.method).toBe('POST');
    rc.flush({ id: 'i1' });
    await c;
  });

  it('passes the refusal body through on an error', async () => {
    const p = svc.createCommunity({
      legalName: 'x', communityName: 'Keystone Park', county: 'x', formationDate: '2004-03-14',
      managementStartDate: '2026-11-01', description: null, parentCommunityId: null,
    });
    http.expectOne(`${BASE}/communities`).flush(
      { code: 'COMMUNITY_NAME_TAKEN', message: 'That community name is unavailable.' }, { status: 409, statusText: 'Conflict' });
    await expectAsync(p).toBeRejectedWith(jasmine.objectContaining({ status: 409, error: jasmine.objectContaining({ code: 'COMMUNITY_NAME_TAKEN' }) }));
  });
});
