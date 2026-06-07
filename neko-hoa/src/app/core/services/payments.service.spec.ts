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
    it('GETs the paginated /payments/drafts envelope and maps items', async () => {
      const promise = svc.getDrafts();
      const req = http.expectOne(r => r.url.includes('/payments/drafts') && r.method === 'GET');
      req.flush({
        items: [
          { id: '1', draftDate: '2026-01-01', sourceLabel: 'Bank •••• 6789', amount: 250, status: 'Paid',      transactionStatus: 'Succeeded' },
          { id: '2', draftDate: '2026-02-01', sourceLabel: 'Bank •••• 6789', amount: 250, status: 'Scheduled', transactionStatus: null },
        ],
        totalCount: 2, limit: 50, offset: 0,
      });
      const drafts = await promise;
      expect(drafts.length).toBe(2);
      expect(drafts[0].source).toBe('Bank •••• 6789');
      expect(drafts[0].transactionStatus).toBe('Succeeded');
      expect(drafts[1].transactionStatus).toBeNull();
    });
  });

  describe('createSetupIntent()', () => {
    it('POSTs to /payments/recurring/setup-intent and returns the client secret', async () => {
      const promise = svc.createSetupIntent();
      const req = http.expectOne(r => r.url.endsWith('/payments/recurring/setup-intent') && r.method === 'POST');
      req.flush({ setupIntentId: 'seti_1', clientSecret: 'seti_1_secret', publishableKey: 'pk_test_x' });
      const setup = await promise;
      expect(setup.setupIntentId).toBe('seti_1');
      expect(setup.clientSecret).toBe('seti_1_secret');
    });
  });

  describe('getRecurring()', () => {
    it('GETs /payments/recurring and returns the enrollment', async () => {
      const promise = svc.getRecurring();
      const req = http.expectOne(r => r.url.endsWith('/payments/recurring') && r.method === 'GET');
      req.flush({
        id: 'rec1', amountType: 'assessment', fixedAmount: null, method: 'card', draftDay: 1,
        status: 'active', processingFee: 1.95, maskedMethod: 'Visa •••• 4242',
        nextDraftDate: '2026-07-01', nextDraftAmount: 251.95, mandateAcceptedAt: '2026-06-07T00:00:00Z',
      });
      const rec = await promise;
      expect(rec?.status).toBe('active');
      expect(rec?.maskedMethod).toBe('Visa •••• 4242');
    });

    it('returns null when the resident has no enrollment (204 No Content)', async () => {
      const promise = svc.getRecurring();
      const req = http.expectOne(r => r.url.endsWith('/payments/recurring') && r.method === 'GET');
      req.flush(null, { status: 204, statusText: 'No Content' });
      expect(await promise).toBeNull();
    });
  });

  describe('saveRecurring()', () => {
    it('PUTs the enrollment + mandate to /payments/recurring', async () => {
      const promise = svc.saveRecurring({
        amountType: 'fixed', fixedAmount: 100, draftDay: 5, setupIntentId: 'seti_1',
        mandateAccepted: true, mandateText: 'I authorize…', mandateVersion: '2026-06-v1',
      });
      const req = http.expectOne(r => r.url.endsWith('/payments/recurring') && r.method === 'PUT');
      expect(req.request.body).toEqual({
        amountType: 'fixed', fixedAmount: 100, draftDay: 5, setupIntentId: 'seti_1',
        mandateAccepted: true, mandateText: 'I authorize…', mandateVersion: '2026-06-v1',
      });
      req.flush({
        id: 'rec1', amountType: 'fixed', fixedAmount: 100, method: 'card', draftDay: 5,
        status: 'active', processingFee: 1.95, maskedMethod: 'Visa •••• 4242',
        nextDraftDate: '2026-07-05', nextDraftAmount: 101.95, mandateAcceptedAt: '2026-06-07T00:00:00Z',
      });
      const rec = await promise;
      expect(rec.draftDay).toBe(5);
      expect(rec.fixedAmount).toBe(100);
    });
  });

  describe('cancelRecurring()', () => {
    it('DELETEs /payments/recurring', async () => {
      const promise = svc.cancelRecurring();
      const req = http.expectOne(r => r.url.endsWith('/payments/recurring') && r.method === 'DELETE');
      req.flush({});
      await promise;
      expect().nothing();
    });
  });

  describe('getPaymentOptions()', () => {
    it('GETs /payments/options and returns the fee policy + balances', async () => {
      const promise = svc.getPaymentOptions();
      const req = http.expectOne(r => r.url.endsWith('/payments/options') && r.method === 'GET');
      req.flush({
        currentBalance: 300, creditBalance: 0, nextAssessment: 250, nextAssessmentDueDate: '2026-07-01',
        cardFeeType: 'Flat', cardFeeValue: 1.95, cardScope: 'All', surchargingEnabled: true, achFeeValue: 0,
      });
      const opts = await promise;
      expect(opts.currentBalance).toBe(300);
      expect(opts.cardFeeValue).toBe(1.95);
    });
  });

  describe('createIntent()', () => {
    it('POSTs amount + method to /payments/intent and returns the server-authoritative intent', async () => {
      const promise = svc.createIntent(300, 'card');
      const req = http.expectOne(r => r.url.endsWith('/payments/intent') && r.method === 'POST');
      expect(req.request.body).toEqual({ amount: 300, method: 'card' });
      req.flush({ paymentIntentId: 'pi_1', clientSecret: 'pi_1_secret', amount: 300, fee: 1.95, total: 301.95 });
      const intent = await promise;
      expect(intent.paymentIntentId).toBe('pi_1');
      expect(intent.clientSecret).toBe('pi_1_secret');
      expect(intent.total).toBe(301.95);
    });
  });

  describe('confirmPayment()', () => {
    it('POSTs the paymentIntentId to /payments/one-time/confirm and returns the receipt detail', async () => {
      const promise = svc.confirmPayment('pi_1');
      const req = http.expectOne(r => r.url.endsWith('/payments/one-time/confirm') && r.method === 'POST');
      expect(req.request.body).toEqual({ paymentIntentId: 'pi_1' });
      req.flush({
        transactionId: 't1', status: 'Succeeded', grossAmount: 300, feeAmount: 1.95, total: 301.95,
        maskedMethod: 'Visa •••• 4242', confirmationNumber: 'NEKO-ABC123', receiptId: 'r1',
      });
      const result = await promise;
      expect(result.confirmationNumber).toBe('NEKO-ABC123');
      expect(result.maskedMethod).toBe('Visa •••• 4242');
    });
  });
});
