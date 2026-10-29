import { COMMON_TIME_ZONES, timeZoneOption, timeZoneOptions } from './time-zones';

// The architectural review form's time zone dropdown: IANA names from the browser, the common US
// zones first, each searchable by city, generic name and offset.

describe('time zone options', () => {
  it('lists the common US zones first, then every other zone A–Z, with UTC, each once', () => {
    const ids = timeZoneOptions().map(o => o.id);
    expect(ids.slice(0, COMMON_TIME_ZONES.length)).toEqual([...COMMON_TIME_ZONES]);
    const rest = ids.slice(COMMON_TIME_ZONES.length);
    expect(rest).toEqual([...rest].sort((a, b) => a.localeCompare(b, 'en')));
    expect(ids).toContain('UTC');
    expect(ids).toContain('Europe/London');
    expect(ids.length).toBeGreaterThan(300);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('shows the IANA name, with the generic name and the offset under it', () => {
    const winter = new Date('2026-01-15T12:00:00Z');
    const ny = timeZoneOption('America/New_York', winter);
    expect([ny.id, ny.label, ny.detail]).toEqual(['America/New_York', 'America/New_York', 'Eastern Time · UTC-5']);
    expect(timeZoneOption('America/New_York', new Date('2026-07-15T12:00:00Z')).detail).toBe('Eastern Time · UTC-4');
    expect(timeZoneOption('Asia/Kolkata', winter).detail).toBe('India Standard Time · UTC+5:30');
    expect(timeZoneOption('UTC', winter).detail).toBe('Coordinated Universal Time · UTC+0');
  });

  it("uses the current IANA names, whichever browser lists the old ones, and still finds the old name", () => {
    const ids = timeZoneOptions().map(o => o.id);
    for (const [old, current] of [['Asia/Calcutta', 'Asia/Kolkata'], ['Europe/Kiev', 'Europe/Kyiv'], ['America/Indianapolis', 'America/Indiana/Indianapolis']]) {
      expect(ids).withContext(current).toContain(current);
      expect(ids).withContext(old).not.toContain(old);
    }
    const india = timeZoneOptions().find(o => o.id === 'Asia/Kolkata')!;
    expect(india.keywords).toContain('Asia Calcutta');
    expect(india.keywords).toContain('India Standard Time');
  });

  it('matches both seasons\' offsets and abbreviations, whatever the date, and big US cities', () => {
    for (const when of [new Date('2026-01-15T12:00:00Z'), new Date('2026-07-15T12:00:00Z')]) {
      const ny = timeZoneOption('America/New_York', when).keywords!;
      for (const word of ['America New York', 'Eastern Time', 'UTC-5', 'UTC-4', 'EST', 'EDT', 'Boston', 'Atlanta']) {
        expect(ny).withContext(`${when.toISOString()} ${word}`).toContain(word);
      }
    }
    expect(timeZoneOption('America/Chicago').keywords).toContain('Dallas');
    expect(timeZoneOption('America/Chicago').keywords).toContain('CST');
    expect(timeZoneOption('America/Los_Angeles').keywords).toContain('PST');
    expect(timeZoneOption('America/Phoenix').keywords).toContain('Arizona');
    expect(timeZoneOption('Pacific/Honolulu').keywords).toContain('Hawaii');
    // Abbreviations that are only an offset ("GMT+1") aren't repeated.
    expect(timeZoneOption('Europe/Berlin').keywords).not.toContain('GMT');
  });

  it('keeps a saved zone the browser does not list, first, so the field still shows it', () => {
    const options = timeZoneOptions('US/Eastern');
    expect(options[0].id).toBe('US/Eastern');
    expect(options.filter(o => o.id === 'US/Eastern').length).toBe(1);
    expect(timeZoneOptions('America/Chicago').filter(o => o.id === 'America/Chicago').length).toBe(1);
    expect(timeZoneOptions('  ')[0].id).toBe('America/New_York');
  });

  it('does not throw for an unknown zone', () => {
    expect(timeZoneOption('Mars/Olympus_Mons')).toEqual({
      id: 'Mars/Olympus_Mons', label: 'Mars/Olympus_Mons', detail: null, keywords: 'Mars Olympus Mons',
    });
  });

  it('offers only zones this browser resolves', () => {
    for (const o of timeZoneOptions()) {
      expect(() => new Intl.DateTimeFormat('en-US', { timeZone: o.id })).withContext(o.id).not.toThrow();
    }
  });
});
