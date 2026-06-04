import { TestBed } from '@angular/core/testing';
import { MockDataService } from './mock-data.service';

describe('MockDataService', () => {
  let svc: MockDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    svc = TestBed.inject(MockDataService);
  });

  it('should be created', () => {
    expect(svc).toBeTruthy();
  });

  // ── currentUser ──────────────────────────────────────────────────────────
  describe('currentUser', () => {
    it('has correct initials', () => {
      expect(svc.currentUser.initials).toBe('NB');
    });
    it('has a valid email', () => {
      expect(svc.currentUser.email).toContain('@');
    });
  });

  // ── property ─────────────────────────────────────────────────────────────
  describe('property', () => {
    it('has status active', () => {
      expect(svc.property.status).toBe('active');
    });
    it('annualAssessment equals 12 x monthly', () => {
      expect(svc.property.annualAssessment).toBe(svc.property.monthlyAssessment * 12);
    });
    it('has account number in expected format', () => {
      expect(svc.property.accountNumber).toMatch(/^R\d+L\d+$/);
    });
  });

  // ── ledger ────────────────────────────────────────────────────────────────
  describe('ledger', () => {
    it('has entries', () => {
      expect(svc.ledger.length).toBeGreaterThan(0);
    });
    it('currentBalance is the last entry balance', () => {
      const lastBalance = svc.ledger[svc.ledger.length - 1].balance;
      expect(svc.currentBalance).toBe(lastBalance);
    });
    it('all charges are positive numbers', () => {
      svc.ledger.filter(e => e.charge != null).forEach(e => {
        expect(e.charge!).toBeGreaterThan(0);
      });
    });
    it('all payments are positive numbers', () => {
      svc.ledger.filter(e => e.payment != null).forEach(e => {
        expect(e.payment!).toBeGreaterThan(0);
      });
    });
    it('each entry has charge OR payment, not both', () => {
      svc.ledger.forEach(e => {
        const hasCharge  = e.charge  != null;
        const hasPayment = e.payment != null;
        expect(hasCharge && hasPayment).toBeFalse();
      });
    });
  });

  // ── owner ─────────────────────────────────────────────────────────────────
  describe('owner', () => {
    it('matches property account number', () => {
      expect(svc.owner.accountNumber).toBe(svc.property.accountNumber);
    });
    it('addressHistory has events', () => {
      expect(svc.addressHistory.length).toBeGreaterThan(0);
    });
    it('first address history event is the most recent', () => {
      const dates = svc.addressHistory.map(h => h.date);
      expect(dates[0] >= dates[1]).toBeTrue();
    });
  });

  // ── violations ────────────────────────────────────────────────────────────
  describe('violations', () => {
    it('are all closed (0 open)', () => {
      const open = svc.violations.filter(v => v.status === 'open');
      expect(open.length).toBe(0);
    });
  });

  // ── announcements ─────────────────────────────────────────────────────────
  describe('announcements', () => {
    it('has at least one announcement', () => {
      expect(svc.announcements.length).toBeGreaterThan(0);
    });
    it('has exactly one pinned announcement', () => {
      const pinned = svc.announcements.filter(a => a.pinned);
      expect(pinned.length).toBe(1);
    });
    it('pinned announcement appears first', () => {
      expect(svc.announcements[0].pinned).toBeTrue();
    });
  });

  // ── calendar events ───────────────────────────────────────────────────────
  describe('calendarEvents', () => {
    it('all events have valid ISO dates', () => {
      svc.calendarEvents.forEach(e => {
        expect(Date.parse(e.date)).not.toBeNaN();
      });
    });
    it('categories are Board, Amenity, Social, or Maintenance', () => {
      const valid = new Set(['Board', 'Amenity', 'Social', 'Maintenance']);
      svc.calendarEvents.forEach(e => {
        expect(valid.has(e.category)).toBeTrue();
      });
    });
  });

  // ── documents ─────────────────────────────────────────────────────────────
  describe('documents', () => {
    it('all documents have a name and category', () => {
      svc.documents.forEach(d => {
        expect(d.name.length).toBeGreaterThan(0);
        expect(d.category.length).toBeGreaterThan(0);
      });
    });
    it('pinned documents exist', () => {
      expect(svc.documents.some(d => d.pinned)).toBeTrue();
    });
  });

  // ── dashboard summary ─────────────────────────────────────────────────────
  describe('getDashboardSummary()', () => {
    let summary: ReturnType<MockDataService['getDashboardSummary']>;
    beforeEach(() => { summary = svc.getDashboardSummary(); });

    it('returns a summary object', () => {
      expect(summary).toBeTruthy();
    });
    it('currentBalance matches ledger', () => {
      expect(summary.currentBalance).toBe(svc.currentBalance);
    });
    it('openViolations is 0', () => {
      expect(summary.openViolations).toBe(0);
    });
    it('has a pinned announcement', () => {
      expect(summary.pinnedAnnouncement).not.toBeNull();
    });
    it('recentActivity has at most 4 items', () => {
      expect(summary.recentActivity.length).toBeLessThanOrEqual(4);
    });
    it('communityExpenses amounts are all positive', () => {
      summary.communityExpenses.forEach(e => {
        expect(e.amount).toBeGreaterThan(0);
      });
    });
  });
});
