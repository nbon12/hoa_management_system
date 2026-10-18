// 030 conventions §7.9: times read "Oct 14, 2026, 9:15 AM EDT" in the community's time zone (the
// company default's for Company Administrator invitations, FR-022); plain dates read "Oct 14, 2026".

const DATE_TIME: Intl.DateTimeFormatOptions = {
  month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit', timeZoneName: 'short',
};
const DATE: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric', year: 'numeric' };

/** ICU may separate "9:15" and "AM" with a narrow no-break space; the copy uses a plain space. */
function normalizeSpaces(text: string): string {
  return text.replace(/[  ]/g, ' ');
}

/** A UTC instant shown in `timeZoneId`, with the zone's abbreviation. An unknown zone falls back to UTC. */
export function formatDateTimeInZone(isoInstant: string, timeZoneId: string | null | undefined): string {
  const when = new Date(isoInstant);
  if (Number.isNaN(when.getTime())) return '';
  try {
    return normalizeSpaces(new Intl.DateTimeFormat('en-US', { ...DATE_TIME, timeZone: timeZoneId || 'UTC' }).format(when));
  } catch {
    return normalizeSpaces(new Intl.DateTimeFormat('en-US', { ...DATE_TIME, timeZone: 'UTC' }).format(when));
  }
}

/** A plain calendar date (`2027-12-31`, or the date part of an instant) with no time-zone shift. */
export function formatPlainDate(isoDate: string): string {
  const day = new Date(`${isoDate.slice(0, 10)}T00:00:00Z`);
  if (Number.isNaN(day.getTime())) return '';
  return new Intl.DateTimeFormat('en-US', { ...DATE, timeZone: 'UTC' }).format(day);
}

/** Today's date in UTC as a plain date, for "starts today, Oct 12, 2026" (memberships start on the UTC date, FR-022). */
export function formatTodayUtc(now: Date = new Date()): string {
  return formatPlainDate(now.toISOString());
}
