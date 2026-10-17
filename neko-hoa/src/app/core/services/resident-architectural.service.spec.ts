import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ResidentArchitecturalService } from './resident-architectural.service';
import { environment } from '../../../environments/environment';

const BASE = `${environment.apiBaseUrl}/property/architectural-applications`;

describe('ResidentArchitecturalService (029)', () => {
  let svc: ResidentArchitecturalService;
  let http: HttpTestingController;
  const file = new File(['%PDF-1.7'], 'plan.pdf', { type: 'application/pdf' });

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    svc = TestBed.inject(ResidentArchitecturalService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  // Each call hits exactly the contract's verb and path.
  const cases: [string, () => Promise<unknown>, string, string][] = [
    ['detail', () => svc.detail('a1'), 'GET', `${BASE}/a1`],
    ['attachmentUrl', () => svc.attachmentUrl('a1', 'f1'), 'GET', `${BASE}/a1/attachments/f1/url`],
    ['getDraft', () => svc.getDraft('d1'), 'GET', `${BASE}/drafts/d1`],
    ['deleteDraft', () => svc.deleteDraft('d1'), 'DELETE', `${BASE}/drafts/d1`],
    ['deleteDraftAttachment', () => svc.deleteDraftAttachment('d1', 'f1'), 'DELETE', `${BASE}/drafts/d1/attachments/f1`],
    ['draftAttachmentUrl', () => svc.draftAttachmentUrl('d1', 'f1'), 'GET', `${BASE}/drafts/d1/attachments/f1/url`],
    ['submitDraft', () => svc.submitDraft('d1'), 'POST', `${BASE}/drafts/d1/submit`],
    ['withdraw', () => svc.withdraw('a1'), 'POST', `${BASE}/a1/withdraw`],
    ['revise', () => svc.revise('a1'), 'POST', `${BASE}/a1/revise`],
  ];
  for (const [name, call, method, url] of cases) {
    it(`${name}() → ${method} ${url.replace(BASE, '…')}`, async () => {
      const p = call();
      const req = http.expectOne(url);
      expect(req.request.method).toBe(method);
      req.flush({});
      await p;
    });
  }

  it('list() sends limit and offset', async () => {
    const p = svc.list(10, 20);
    const req = http.expectOne(r => r.url === BASE);
    expect(req.request.params.get('limit')).toBe('10');
    expect(req.request.params.get('offset')).toBe('20');
    req.flush({ items: [], total: 0, limit: 10, offset: 20 });
    expect((await p).offset).toBe(20);
  });

  it('createDraft() and updateDraft() send the body as JSON', async () => {
    const body = { projectType: 'Fence' as const, projectTitle: 'T', description: 'D', plannedStartDate: null,
      plannedCompletionDate: null, contractorName: null, contractorContact: null, acknowledged: false };
    const created = svc.createDraft(body);
    const post = http.expectOne(`${BASE}/drafts`);
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual(body);
    post.flush({ id: 'd1' });
    await created;

    const updated = svc.updateDraft('d1', body);
    const put = http.expectOne(`${BASE}/drafts/d1`);
    expect(put.request.method).toBe('PUT');
    put.flush({ id: 'd1' });
    await updated;
  });

  it('uploads send multipart FormData with the file under "file"', async () => {
    const draftUpload = svc.uploadDraftAttachment('d1', file);
    const req1 = http.expectOne(`${BASE}/drafts/d1/attachments`);
    expect(req1.request.body instanceof FormData).toBeTrue();
    expect((req1.request.body as FormData).get('file')).toBeTruthy();
    req1.flush({ id: 'f1' });
    await draftUpload;

    const replyUpload = svc.uploadReplyAttachment('a1', 'q1', file);
    const req2 = http.expectOne(`${BASE}/a1/info-requests/q1/attachments`);
    expect(req2.request.method).toBe('POST');
    expect((req2.request.body as FormData).get('file')).toBeTruthy();
    req2.flush({ id: 'f2' });
    await replyUpload;
  });

  it('reply() posts the response message', async () => {
    const p = svc.reply('a1', 'q1', 'Survey attached');
    const req = http.expectOne(`${BASE}/a1/info-requests/q1/reply`);
    expect(req.request.body).toEqual({ responseMessage: 'Survey attached' });
    req.flush({});
    await p;
  });
});
