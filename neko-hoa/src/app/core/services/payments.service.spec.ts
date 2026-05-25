import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { PaymentsService } from './payments.service';
import { environment } from '../../../environments/environment';

const BASE = environment.apiBaseUrl;

describe('PaymentsService', () => {
  let svc: PaymentsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    svc  = TestBed.inject(PaymentsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should be created', () => expect(svc).toBeTruthy());

  describe('getLedger()', () => {
    it('calls /payments/ledger and maps items', async () => {
      const promise = svc.getLedger();
      const req = http.expectOne(r => r.url.includes('/payments/ledger'));
      req.flush({
        items: [
          { id: '1', entryDate: '2026-01-01', description: 'Regular Assessment', entryType: 'RegularAssessment', chargeAmount: 250, paymentAmount: 0, runningBalance: 250, documentNumber: 'RA202601' },
          { id: '2', entryDate: '2026-01-05', description: 'Payment',            entryType: 'Payment',          chargeAmount: 0,   paymentAmount: 250, runningBalance: 0,   documentNumber: '' },
        ],
        totalCount: 2, page: 1, pageSize: 100,
      });
      const entries = await promise;
      expect(entries.length).toBe(2);
      expect(entries[0].type).toBe('Regular Assessment');
      expect(entries[0].charge).toBe(250);
    });

    it('returns empty array on empty response', async () => {
      const promise = svc.getLedger();
      http.expectOne(r => r.url.includes('/payments/ledger'))
          .flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
      const entries = await promise;
      expect(entries.length).toBe(0);
    });
  });

  describe('getDrafts()', () => {
    it('calls /payments/drafts and maps items', async () => {
      const promise = svc.getDrafts();
      http.expectOne(r => r.url.includes('/payments/drafts'))
          .flush([
            { id: '1', draftDate: '2026-01-01', sourceLabel: 'ACH', amount: 250, status: 'paid' },
            { id: '2', draftDate: '2026-02-01', sourceLabel: 'ACH', amount: 250, status: 'scheduled' },
          ]);
      const drafts = await promise;
      expect(drafts.length).toBe(2);
      expect(drafts[0].status).toBe('paid');
    });
  });

  describe('submitPayment()', () => {
    it('posts to /payments/one-time and returns confirmation', async () => {
      const promise = svc.submitPayment(250, 'ach');
      http.expectOne(r => r.url.includes('/payments/one-time') && r.method === 'POST')
          .flush({ confirmationNumber: 'CONF123', amount: 250, processedAt: '2026-01-01T00:00:00Z' });
      const result = await promise;
      expect(result.confirmationNumber).toBe('CONF123');
      expect(result.amount).toBe(250);
    });
  });
});
