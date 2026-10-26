import { SettingsSectionId } from '../../core/services/community-settings.service';

// 030 FR-029 / brief 04 §4.1: the static registry of Community settings sections and their setting
// labels, in side-list order. Settings search matches these labels only — never people or values
// (D1: labels only, not helper text). Notifications is not listed (FR-029).
//
// Section components mark each setting's focus target with `data-setting-id="<id>"` so that
// choosing a search result can open the section and move focus to that field.

export interface SettingDef {
  id: string;
  label: string;
}

export interface SectionDef {
  id: SettingsSectionId;
  label: string;
  settings: SettingDef[];
}

export const SETTINGS_REGISTRY: readonly SectionDef[] = [
  {
    id: 'profile',
    label: 'Profile',
    settings: [
      { id: 'legal-name', label: 'Legal name' },
      { id: 'community-name', label: 'Community name' },
      { id: 'county', label: 'County' },
      { id: 'formation-date', label: 'Formation date' },
      { id: 'management-start-date', label: 'Management start date' },
      { id: 'description', label: 'Description' },
      { id: 'status', label: 'Status' },
      { id: 'parent', label: 'Parent association' },
      { id: 'sub-associations', label: 'Sub-associations' },
    ],
  },
  {
    id: 'managers-and-memberships',
    label: 'Managers and memberships',
    settings: [
      { id: 'managers', label: 'Managers' },
      { id: 'board-members-and-accountants', label: 'Board members and accountants' },
      { id: 'residents', label: 'Residents' },
      { id: 'invitations', label: 'Invitations' },
    ],
  },
  {
    id: 'architectural-review',
    label: 'Architectural review',
    settings: [
      // Labels from today's ARC settings page (conventions §5.3).
      { id: 'review-period', label: 'Review period' },
      { id: 'reminder-days', label: 'Reminder to the board' },
      { id: 'lapse-rule', label: 'When the review period ends without a decision' },
      { id: 'decision-rule', label: 'How a decision is reached' },
      { id: 'time-zone', label: 'Time zone' },
      { id: 'formal-disapproval-statement', label: 'Formal disapproval statement' },
    ],
  },
];

/** The registry entries for the section ids the server sent, in the server's order; unknown ids are dropped. */
export function sectionsFor(ids: readonly string[]): SectionDef[] {
  return ids
    .map(id => SETTINGS_REGISTRY.find(s => s.id === id))
    .filter((s): s is SectionDef => !!s);
}

export interface SettingsSearchResult {
  key: string;
  sectionId: SettingsSectionId;
  /** Null when the section itself matched. */
  settingId: string | null;
  /** "Architectural review", or "Architectural review › Review period". */
  label: string;
}

/** Case-insensitive, part-of-a-word matching over section and setting labels. */
export function searchSettings(query: string, sections: readonly SectionDef[]): SettingsSearchResult[] {
  const q = query.trim().toLowerCase();
  if (!q) return [];
  const results: SettingsSearchResult[] = [];
  for (const section of sections) {
    if (section.label.toLowerCase().includes(q)) {
      results.push({ key: section.id, sectionId: section.id, settingId: null, label: section.label });
    }
    for (const setting of section.settings) {
      if (setting.label.toLowerCase().includes(q)) {
        results.push({
          key: `${section.id}/${setting.id}`, sectionId: section.id, settingId: setting.id,
          label: `${section.label} › ${setting.label}`,
        });
      }
    }
  }
  return results;
}
