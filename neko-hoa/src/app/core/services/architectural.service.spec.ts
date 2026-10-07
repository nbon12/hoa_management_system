import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ArchitecturalService } from './architectural.service';
import { environment } from '../../../environments/environment';

const BASE = environment.apiBaseUrl;
const C = 'c1';
const A = 'a1';

describe('ArchitecturalService (027)', () => {
  let svc: ArchitecturalService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    svc = TestBed.inject(ArchitecturalService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list() defaults to open, limit 25, offset 0', async () => {
    const p = svc.list(C);
    const req = http.expectOne(r => r.url === `${BASE}/communities/${C}/architectural-applications`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('open');
    expect(req.request.params.get('limit')).toBe('25');
    expect(req.request.params.get('offset')).toBe('0');
    expect(req.request.params.has('search')).toBeFalse();
    req.flush({ items: [], total: 0, limit: 25, offset: 0, counts: { open: 0, closed: 0, awaitingMyVote: 0 } });
    expect((await p).total).toBe(0);
  });

  it('list() passes status, search and awaitingMyVote', async () => {
    const p = svc.list(C, { status: 'closed', search: 'Pattyam', awaitingMyVote: true });
    const req = http.expectOne(r => r.url.endsWith('/architectural-applications'));
    expect(req.request.params.get('status')).toBe('closed');
    expect(req.request.params.get('search')).toBe('Pattyam');
    expect(req.request.params.get('awaitingMyVote')).toBe('true');
    req.flush({ items: [], total: 0, limit: 25, offset: 0, counts: { open: 0, closed: 0, awaitingMyVote: 0 } });
    await p;
  });

  it('vote() posts choice and comment', async () => {
    const p = svc.vote(C, A, 'RevisionsNeeded', 'Fence must be 5ft max');
    const req = http.expectOne(`${BASE}/communities/${C}/architectural-applications/${A}/votes`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ choice: 'RevisionsNeeded', comment: 'Fence must be 5ft max' });
    req.flush({});
    await p;
  });

  it('vote() sends a null comment when empty', async () => {
    const p = svc.vote(C, A, 'Deny', '');
    const req = http.expectOne(r => r.url.endsWith('/votes'));
    expect(req.request.body).toEqual({ choice: 'Deny', comment: null });
    req.flush({});
    await p;
  });

  it('detail() calls the application endpoint', async () => {
    const p = svc.detail(C, A);
    http.expectOne(`${BASE}/communities/${C}/architectural-applications/${A}`).flush({ id: A });
    expect((await p).id).toBe(A);
  });

  it('attachmentUrl() calls the per-attachment url endpoint', async () => {
    const p = svc.attachmentUrl(C, A, 'f1');
    http.expectOne(`${BASE}/communities/${C}/architectural-applications/${A}/attachments/f1/url`)
      .flush({ url: 'https://signed', expiresAt: '2026-06-01T00:05:00Z' });
    expect((await p).url).toBe('https://signed');
  });

  it('requestInfo(), recordOutcome(), resendOutcomeEmail() hit their endpoints', async () => {
    const p1 = svc.requestInfo(C, A, 'Please attach a plat survey');
    const r1 = http.expectOne(r => r.url.endsWith(`/${A}/info-requests`));
    expect(r1.request.body).toEqual({ message: 'Please attach a plat survey' });
    r1.flush({});
    await p1;

    const p2 = svc.recordOutcome(C, A, { ownerReason: 'Lower the fence' });
    const r2 = http.expectOne(r => r.url.endsWith(`/${A}/outcome`));
    expect(r2.request.body).toEqual({ ownerReason: 'Lower the fence' });
    r2.flush({});
    await p2;

    const p3 = svc.resendOutcomeEmail(C, A);
    http.expectOne(r => r.url.endsWith(`/${A}/outcome/resend-email`)).flush({ ownerEmailStatus: 'Pending' });
    expect((await p3).ownerEmailStatus).toBe('Pending');
  });

  it('getSettings()/putSettings() use the community settings endpoint', async () => {
    const settings = {
      reviewPeriodDays: 45, lapseRule: 'DeemedApproved' as const, decisionRule: 'MajorityOfMembers' as const,
      reminderDays: 7, timeZoneId: 'America/New_York', formalDisapprovalStatement: 'Formal.',
    };
    const p1 = svc.getSettings(C);
    http.expectOne(`${BASE}/communities/${C}/architectural-settings`).flush(settings);
    expect((await p1).reviewPeriodDays).toBe(45);

    const p2 = svc.putSettings(C, settings);
    const r = http.expectOne(`${BASE}/communities/${C}/architectural-settings`);
    expect(r.request.method).toBe('PUT');
    expect(r.request.body).toEqual(settings);
    r.flush(settings);
    await p2;
  });
});
