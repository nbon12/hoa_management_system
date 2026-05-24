import { TestBed } from '@angular/core/testing';
import { CommunityService } from './community.service';
import { MockDataService } from './mock-data.service';

describe('CommunityService', () => {
  let svc: CommunityService;
  let mock: MockDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    svc  = TestBed.inject(CommunityService);
    mock = TestBed.inject(MockDataService);
  });

  it('should be created', () => expect(svc).toBeTruthy());

  // ── announcements ─────────────────────────────────────────────────────────
  describe('getAnnouncements()', () => {
    it('returns all when no category filter', () => {
      expect(svc.getAnnouncements().length).toBe(mock.announcements.length);
    });
    it('filters by category', () => {
      const board = svc.getAnnouncements('Board');
      board.forEach(a => expect(a.category).toBe('Board'));
    });
    it('returns empty array for category with no announcements', () => {
      expect(svc.getAnnouncements('Emergencies').length).toBe(0);
    });
    it('returns a copy', () => {
      expect(svc.getAnnouncements()).not.toBe(svc.getAnnouncements());
    });
  });

  describe('getPoll()', () => {
    it('returns a poll object', () => {
      expect(svc.getPoll()).toBeTruthy();
    });
    it('poll options sum to ~100%', () => {
      const sum = svc.getPoll().options.reduce((s, o) => s + o.percent, 0);
      expect(sum).toBe(100);
    });
    it('has a question', () => {
      expect(svc.getPoll().question.length).toBeGreaterThan(0);
    });
  });

  // ── violations ────────────────────────────────────────────────────────────
  describe('getViolations()', () => {
    it('returns all when no status filter', () => {
      expect(svc.getViolations().length).toBe(mock.violations.length);
    });
    it('returns only open violations', () => {
      svc.getViolations('open').forEach(v => expect(v.status).toBe('open'));
    });
    it('returns only closed violations', () => {
      svc.getViolations('closed').forEach(v => expect(v.status).toBe('closed'));
    });
    it('open + closed = total', () => {
      const total  = svc.getViolations().length;
      const open   = svc.getViolations('open').length;
      const closed = svc.getViolations('closed').length;
      expect(open + closed).toBe(total);
    });
  });

  // ── calendar events ───────────────────────────────────────────────────────
  describe('getCalendarEvents()', () => {
    it('returns all events when no filter', () => {
      expect(svc.getCalendarEvents().length).toBe(mock.calendarEvents.length);
    });
    it('filters by category Board', () => {
      svc.getCalendarEvents('Board').forEach(e => expect(e.category).toBe('Board'));
    });
    it('filters by category Amenity', () => {
      svc.getCalendarEvents('Amenity').forEach(e => expect(e.category).toBe('Amenity'));
    });
    it('returns empty array for Maintenance (none in mock)', () => {
      const maintenance = svc.getCalendarEvents('Maintenance');
      expect(maintenance.length).toBe(0);
    });
  });

  // ── documents ─────────────────────────────────────────────────────────────
  describe('getDocuments()', () => {
    it('returns all when category is All', () => {
      expect(svc.getDocuments('All').length).toBe(mock.documents.length);
    });
    it('returns all when no category', () => {
      expect(svc.getDocuments().length).toBe(mock.documents.length);
    });
    it('filters by category', () => {
      svc.getDocuments('Forms').forEach(d => expect(d.category).toBe('Forms'));
    });
    it('returns Pinned docs for Pinned category', () => {
      svc.getDocuments('Pinned').forEach(d => expect(d.pinned).toBeTrue());
    });
  });

  describe('searchDocuments()', () => {
    it('returns matching documents', () => {
      const results = svc.searchDocuments('budget');
      expect(results.length).toBeGreaterThan(0);
      results.forEach(d => expect(d.name.toLowerCase()).toContain('budget'));
    });
    it('is case-insensitive', () => {
      const lower = svc.searchDocuments('acc');
      const upper = svc.searchDocuments('ACC');
      expect(lower.length).toBe(upper.length);
    });
    it('returns empty array for no match', () => {
      expect(svc.searchDocuments('xyzzy_nonexistent').length).toBe(0);
    });
    it('partial match works', () => {
      const results = svc.searchDocuments('ins');
      expect(results.length).toBeGreaterThan(0);
    });
  });
});
