import { formatDateTimeInZone, formatPlainDate, formatTodayUtc } from './date-format';

// 030 conventions §7.9: "Oct 14, 2026, 9:15 AM EDT" in the community's time zone; plain dates "Oct 14, 2026".
describe('date-format (030 conventions §7.9)', () => {
  it('shows a UTC instant in the community time zone with its abbreviation', () => {
    expect(formatDateTimeInZone('2026-10-14T13:15:00Z', 'America/New_York')).toBe('Oct 14, 2026, 9:15 AM EDT');
    expect(formatDateTimeInZone('2026-10-14T13:15:00Z', 'America/Los_Angeles')).toBe('Oct 14, 2026, 6:15 AM PDT');
    // After the DST change, the abbreviation follows.
    expect(formatDateTimeInZone('2026-11-14T14:15:00Z', 'America/New_York')).toBe('Nov 14, 2026, 9:15 AM EST');
  });

  it('falls back to UTC for a missing or unknown zone', () => {
    expect(formatDateTimeInZone('2026-10-14T13:15:00Z', null)).toBe('Oct 14, 2026, 1:15 PM UTC');
    expect(formatDateTimeInZone('2026-10-14T13:15:00Z', 'Not/AZone')).toBe('Oct 14, 2026, 1:15 PM UTC');
  });

  it('returns an empty string for an unreadable instant', () => {
    expect(formatDateTimeInZone('soon', 'America/New_York')).toBe('');
    expect(formatPlainDate('someday')).toBe('');
  });

  it('shows a plain date without shifting it to another day', () => {
    expect(formatPlainDate('2027-12-31')).toBe('Dec 31, 2027');
    expect(formatPlainDate('2026-10-12T23:59:00Z')).toBe('Oct 12, 2026');
  });

  it("shows today's UTC date", () => {
    expect(formatTodayUtc(new Date('2026-10-12T14:00:00Z'))).toBe('Oct 12, 2026');
  });
});
