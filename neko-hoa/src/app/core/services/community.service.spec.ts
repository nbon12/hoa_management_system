import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CommunityService } from './community.service';
import { environment } from '../../../environments/environment';

const BASE = environment.apiBaseUrl;

const ANNOUNCEMENTS_RESPONSE = {
  items: [
    { id: 'a1', title: 'Board Meeting',   body: 'Meeting on June 10', category: 'Board',  publishedAt: '2026-05-01T00:00:00Z', pinned: true,  likeCount: 5, commentCount: 2, authorName: 'Board President' },
    { id: 'a2', title: 'Pool Closing',    body: 'Pool closed June 1', category: 'Maintenance', publishedAt: '2026-04-20T00:00:00Z', pinned: false, likeCount: 0, commentCount: 0, authorName: 'Facilities' },
  ],
  totalCount: 2, page: 1, pageSize: 50,
};

describe('CommunityService', () => {
  let svc: CommunityService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    svc  = TestBed.inject(CommunityService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should be created', () => expect(svc).toBeTruthy());

  describe('getAnnouncements()', () => {
    it('calls /community/announcements and returns mapped items', async () => {
      const promise = svc.getAnnouncements();
      http.expectOne(r => r.url.includes('/community/announcements'))
          .flush(ANNOUNCEMENTS_RESPONSE);
      const items = await promise;
      expect(items.length).toBe(2);
      expect(items[0].title).toBe('Board Meeting');
      expect(items[0].category).toBe('Board');
      expect(items[0].pinned).toBeTrue();
    });

    it('passes category as query param', async () => {
      const promise = svc.getAnnouncements('Board' as any);
      const req = http.expectOne(r => r.url.includes('/community/announcements'));
      expect(req.request.params.get('category')).toBe('Board');
      req.flush({ ...ANNOUNCEMENTS_RESPONSE, items: [ANNOUNCEMENTS_RESPONSE.items[0]] });
      await promise;
    });
  });

  describe('getPoll()', () => {
    it('calls /community/poll and maps to poll model', async () => {
      const promise = svc.getPoll();
      http.expectOne(`${BASE}/community/poll`).flush({
        id: 'p1', question: 'Which improvement?', closingLabel: 'Closes June 30',
        totalVotes: 42,
        options: [
          { optionIndex: 0, optionText: 'Playground', voteCount: 18, percentage: 42.86 },
          { optionIndex: 1, optionText: 'Tennis',     voteCount: 24, percentage: 57.14 },
        ],
      });
      const poll = await promise;
      expect(poll).toBeTruthy();
      expect(poll!.question).toBe('Which improvement?');
      expect(poll!.options.length).toBe(2);
      expect(poll!.totalVotes).toBe(42);
    });

    it('returns null on error', async () => {
      const promise = svc.getPoll();
      http.expectOne(`${BASE}/community/poll`).flush('Not Found', { status: 404, statusText: 'Not Found' });
      const poll = await promise;
      expect(poll).toBeNull();
    });
  });

  describe('getViolations()', () => {
    it('calls /community/violations and returns items', async () => {
      const promise = svc.getViolations();
      http.expectOne(r => r.url.includes('/community/violations')).flush({
        items: [
          { id: 'v1', title: 'Overgrown hedges', description: null, category: 'Landscape', status: 'Open',   issuedDate: '2026-05-01', resolvedDate: null, dueDate: '2026-06-01', fineAmount: null },
          { id: 'v2', title: 'Parking violation', description: null, category: 'Parking',  status: 'Closed', issuedDate: '2026-02-01', resolvedDate: '2026-02-10', dueDate: null, fineAmount: null },
        ],
        totalCount: 2, page: 1, pageSize: 100,
      });
      const items = await promise;
      expect(items.length).toBe(2);
      expect(items[0].issue).toBe('Overgrown hedges');
      expect(items[0].status).toBe('open');
      expect(items[1].status).toBe('closed');
    });

    it('passes status as query param', async () => {
      const promise = svc.getViolations('open');
      const req = http.expectOne(r => r.url.includes('/community/violations'));
      expect(req.request.params.get('status')).toBe('open');
      req.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
      await promise;
    });
  });

  describe('getDocuments()', () => {
    it('calls /community/documents and returns items', async () => {
      const promise = svc.getDocuments();
      http.expectOne(r => r.url.includes('/community/documents')).flush({
        items: [
          { id: 'd1', name: '2026 Budget', category: 'Budgets', effectiveDate: '2026-01-01', fileSizeLabel: '1.2 MB', pinned: true },
          { id: 'd2', name: 'CC&R', category: 'Governing', effectiveDate: '2005-06-01', fileSizeLabel: '5.0 MB', pinned: false },
        ],
        totalCount: 2, page: 1, pageSize: 200,
      });
      const docs = await promise;
      expect(docs.length).toBe(2);
      expect(docs[0].name).toBe('2026 Budget');
      expect(docs[0].pinned).toBeTrue();
    });
  });

  describe('searchDocuments()', () => {
    it('passes search as query param', async () => {
      const promise = svc.searchDocuments('budget');
      const req = http.expectOne(r => r.url.includes('/community/documents'));
      expect(req.request.params.get('search')).toBe('budget');
      req.flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
      await promise;
    });
  });

  describe('getDocumentDownloadUrl()', () => {
    it('calls /community/documents/{id}/download and returns presigned url', async () => {
      const promise = svc.getDocumentDownloadUrl('doc-123');
      const req = http.expectOne(`${BASE}/community/documents/doc-123/download`);
      expect(req.request.method).toBe('GET');
      req.flush({
        url: 'http://localhost:9000/hoa-documents/documents/governing/ccr.pdf?X-Amz-Signature=abc',
        expiresAt: '2026-05-25T12:00:00Z',
      });
      const result = await promise;
      expect(result.url).toContain('ccr.pdf');
      expect(result.expiresAt).toBeTruthy();
    });
  });
});
