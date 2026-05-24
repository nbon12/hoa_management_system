import { TestBed } from '@angular/core/testing';
import { PaymentsService } from './payments.service';
import { MockDataService } from './mock-data.service';

describe('PaymentsService', () => {
  let svc: PaymentsService;
  let mockData: MockDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    svc = TestBed.inject(PaymentsService);
    mockData = TestBed.inject(MockDataService);
  });

  it('should be created', () => expect(svc).toBeTruthy());

  // ── getLedger ─────────────────────────────────────────────────────────────
  describe('getLedger()', () => {
    it('returns all entries when no filter', () => {
      expect(svc.getLedger().length).toBe(mockData.ledger.length);
    });

    it('filters by startDate', () => {
      const entries = svc.getLedger('2026-01-01');
      entries.forEach(e => expect(e.date >= '2026-01-01').toBeTrue());
    });

    it('filters by endDate', () => {
      const entries = svc.getLedger(undefined, '2025-12-31');
      entries.forEach(e => expect(e.date <= '2025-12-31').toBeTrue());
    });

    it('filters by date range', () => {
      const entries = svc.getLedger('2025-06-01', '2025-09-30');
      entries.forEach(e => {
        expect(e.date >= '2025-06-01').toBeTrue();
        expect(e.date <= '2025-09-30').toBeTrue();
      });
    });

    it('returns empty array for future date range', () => {
      expect(svc.getLedger('2099-01-01', '2099-12-31').length).toBe(0);
    });
  });

  // ── balance / assessment ──────────────────────────────────────────────────
  describe('currentBalance', () => {
    it('returns a positive number', () => {
      expect(svc.currentBalance).toBeGreaterThan(0);
    });
    it('matches mockData currentBalance', () => {
      expect(svc.currentBalance).toBe(mockData.currentBalance);
    });
  });

  describe('nextAssessment', () => {
    it('returns monthly assessment amount', () => {
      expect(svc.nextAssessment).toBe(mockData.property.monthlyAssessment);
    });
  });

  describe('processingFee', () => {
    it('returns a fee > 0', () => {
      expect(svc.processingFee).toBeGreaterThan(0);
    });
  });

  // ── getDrafts ─────────────────────────────────────────────────────────────
  describe('getDrafts()', () => {
    it('returns draft history', () => {
      expect(svc.getDrafts().length).toBeGreaterThan(0);
    });
    it('first draft has paid status', () => {
      expect(svc.getDrafts()[0].status).toBe('paid');
    });
    it('remaining drafts are scheduled', () => {
      svc.getDrafts().slice(1).forEach(d => expect(d.status).toBe('scheduled'));
    });
  });

  // ── submitPayment ─────────────────────────────────────────────────────────
  describe('submitPayment()', () => {
    it('resolves with a confirmation number', async () => {
      const result = await svc.submitPayment(35, 'ach');
      expect(result.confirmationNumber).toBeTruthy();
      expect(result.confirmationNumber.length).toBeGreaterThan(4);
    });

    it('resolves with correct amount', async () => {
      const result = await svc.submitPayment(70, 'card');
      expect(result.amount).toBe(70);
    });

    it('includes a date', async () => {
      const result = await svc.submitPayment(35, 'ach');
      expect(Date.parse(result.date)).not.toBeNaN();
    });
  });

  // ── saveRecurring ─────────────────────────────────────────────────────────
  describe('saveRecurring()', () => {
    it('updates the recurring signal', async () => {
      await svc.saveRecurring({ draftDay: 15 });
      expect(svc.recurring().draftDay).toBe(15);
    });

    it('merges partial updates', async () => {
      const originalMethod = svc.recurring().method;
      await svc.saveRecurring({ draftDay: 5 });
      expect(svc.recurring().method).toBe(originalMethod);
    });
  });

  // ── cancelRecurring ───────────────────────────────────────────────────────
  describe('cancelRecurring()', () => {
    it('sets status to inactive', async () => {
      await svc.cancelRecurring();
      expect(svc.recurring().status).toBe('inactive');
    });
  });
});
