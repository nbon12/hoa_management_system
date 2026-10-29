import { PickerOption } from './combo-picker.component';

// The time zones the architectural review form offers: IANA names, as the API stores them. The
// browser supplies the list; the server accepts any IANA name (ArcRuleValidator). Each option shows
// its name, with the zone's generic name and current UTC offset under it, and matches on all three,
// so "Chicago", "Central" and "UTC-5" all find America/Chicago.

/** The zones most US communities use, east to west. They come first. */
export const COMMON_TIME_ZONES: readonly string[] = [
  'America/New_York', 'America/Chicago', 'America/Denver', 'America/Phoenix', 'America/Los_Angeles',
  'America/Anchorage', 'Pacific/Honolulu',
];

/**
 * Chrome lists these zones by their old names; Firefox and Safari use the current ones. The list
 * uses the current names, so a community saves the same name whichever browser it uses. The old
 * name still matches a search. The server accepts both (its tzdata keeps the old names as links).
 */
export const RENAMED_TIME_ZONES: Readonly<Record<string, string>> = {
  'Africa/Asmera': 'Africa/Asmara',
  'America/Buenos_Aires': 'America/Argentina/Buenos_Aires',
  'America/Catamarca': 'America/Argentina/Catamarca',
  'America/Coral_Harbour': 'America/Atikokan',
  'America/Cordoba': 'America/Argentina/Cordoba',
  'America/Godthab': 'America/Nuuk',
  'America/Indianapolis': 'America/Indiana/Indianapolis',
  'America/Jujuy': 'America/Argentina/Jujuy',
  'America/Louisville': 'America/Kentucky/Louisville',
  'America/Mendoza': 'America/Argentina/Mendoza',
  'Asia/Calcutta': 'Asia/Kolkata',
  'Asia/Katmandu': 'Asia/Kathmandu',
  'Asia/Rangoon': 'Asia/Yangon',
  'Asia/Saigon': 'Asia/Ho_Chi_Minh',
  'Atlantic/Faeroe': 'Atlantic/Faroe',
  'Europe/Kiev': 'Europe/Kyiv',
  'Pacific/Enderbury': 'Pacific/Kanton',
  'Pacific/Ponape': 'Pacific/Pohnpei',
  'Pacific/Truk': 'Pacific/Chuuk',
};

const OLD_NAMES: Readonly<Record<string, string>> = Object.fromEntries(
  Object.entries(RENAMED_TIME_ZONES).map(([old, current]) => [current, old]));

/** "Eastern Time" (`longGeneric`) or "GMT-4" (`shortOffset`) for this zone at this instant. */
function zoneName(zone: string, when: Date, style: 'longGeneric' | 'shortOffset'): string {
  try {
    return new Intl.DateTimeFormat('en-US', { timeZone: zone, timeZoneName: style })
      .formatToParts(when).find(p => p.type === 'timeZoneName')?.value ?? '';
  } catch {
    return '';
  }
}

/** America/New_York → "America/New_York", with "Eastern Time · UTC-4" under it. */
export function timeZoneOption(zone: string, when: Date = new Date()): PickerOption {
  const rawOffset = zoneName(zone, when, 'shortOffset');
  const offset = rawOffset === 'GMT' ? 'UTC+0' : rawOffset.replace(/^GMT/, 'UTC');
  const generic = zone === 'UTC' ? 'Coordinated Universal Time' : zoneName(zone, when, 'longGeneric');
  // A zone with no generic name gets its offset again ("GMT-03:00"); show the offset once.
  const names = [/^GMT/.test(generic) ? '' : generic, offset].filter(Boolean);
  const words = (id: string) => id.replace(/[_/]/g, ' ');
  return {
    id: zone,
    label: zone,
    detail: names.join(' · ') || null,
    keywords: [words(zone), ...(OLD_NAMES[zone] ? [words(OLD_NAMES[zone])] : []), ...names].join(' '),
  };
}

function supportedZones(): string[] {
  const intl = Intl as { supportedValuesOf?: (key: 'timeZone') => string[] };
  const zones = typeof intl.supportedValuesOf === 'function' ? intl.supportedValuesOf('timeZone') : [];
  return zones.length ? zones.map(z => RENAMED_TIME_ZONES[z] ?? z) : [...COMMON_TIME_ZONES];
}

let cached: PickerOption[] | null = null;

/**
 * Every zone: the common US zones first, then the rest A–Z, with UTC. Built once per page load.
 * `current` is kept even when the browser doesn't list it (an older name, say), so the field still
 * shows the saved value.
 */
export function timeZoneOptions(current?: string | null): PickerOption[] {
  if (!cached) {
    const now = new Date();
    const zones = new Set([...COMMON_TIME_ZONES, ...supportedZones(), 'UTC']);
    const rest = [...zones].filter(z => !COMMON_TIME_ZONES.includes(z)).sort((a, b) => a.localeCompare(b, 'en'));
    cached = [...COMMON_TIME_ZONES, ...rest].map(z => timeZoneOption(z, now));
  }
  const saved = current?.trim();
  return saved && !cached.some(o => o.id === saved) ? [timeZoneOption(saved), ...cached] : cached;
}
