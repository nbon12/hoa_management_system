import { ArcRuleFields } from '../core/services/company.service';

// 030 briefs 07 and 08: the six architectural review rule fields, shared by a community's
// Architectural review section and the Company defaults page. Labels keep spec 027's words, except
// the lapse rule's, which use plain words: "Auto approve" for spec 027's "deemed approved", "Auto
// reject" for "deemed denied" and "No automatic action" for "flag overdue only". The API values
// are unchanged.

export type ArcRuleField = keyof ArcRuleFields;

export const ARC_RULE_FIELDS: readonly ArcRuleField[] = [
  'reviewPeriodDays', 'reminderDays', 'lapseRule', 'decisionRule', 'timeZoneId', 'formalDisapprovalStatement',
];

export const ARC_RULE_LABELS: Record<ArcRuleField, string> = {
  reviewPeriodDays: 'Review period (days)',
  reminderDays: 'Reminder to the board (days before due, 0 = off)',
  lapseRule: 'After the review period ends without a decision',
  decisionRule: 'How a decision is reached',
  timeZoneId: 'Time zone',
  formalDisapprovalStatement: 'Formal disapproval statement (included in every denial email)',
};

/** The words in summaries ("2 settings differ …: Review period and Reminder to the board."). */
export const ARC_RULE_SHORT_LABELS: Record<ArcRuleField, string> = {
  reviewPeriodDays: 'Review period',
  reminderDays: 'Reminder to the board',
  lapseRule: 'After the review period ends without a decision',
  decisionRule: 'How a decision is reached',
  timeZoneId: 'Time zone',
  formalDisapprovalStatement: 'Formal disapproval statement',
};

/** The settings registry's ids, for settings search (`data-setting-id`). */
export const ARC_RULE_SETTING_IDS: Record<ArcRuleField, string> = {
  reviewPeriodDays: 'review-period',
  reminderDays: 'reminder-days',
  lapseRule: 'lapse-rule',
  decisionRule: 'decision-rule',
  timeZoneId: 'time-zone',
  formalDisapprovalStatement: 'formal-disapproval-statement',
};

export const LAPSE_RULE_OPTIONS: readonly { value: ArcRuleFields['lapseRule']; label: string }[] = [
  { value: 'DeemedApproved', label: 'Auto approve' },
  { value: 'DeemedDenied', label: 'Auto reject' },
  { value: 'FlagOverdueOnly', label: 'No automatic action' },
];

export const DECISION_RULE_OPTIONS: readonly { value: ArcRuleFields['decisionRule']; label: string }[] = [
  { value: 'MajorityOfMembers', label: 'Majority of board members' },
  { value: 'MajorityOfVotesCastWithQuorum', label: 'Majority of votes cast (with a quorum)' },
];

export const STATEMENT_MAX = 1000;

/** A value as people read it: "No automatic action", "45", the time zone name, the statement. */
export function arcRuleValueLabel(field: ArcRuleField, value: ArcRuleFields[ArcRuleField] | null | undefined): string {
  if (value === null || value === undefined) return '';
  if (field === 'lapseRule') return LAPSE_RULE_OPTIONS.find(o => o.value === value)?.label ?? String(value);
  if (field === 'decisionRule') return DECISION_RULE_OPTIONS.find(o => o.value === value)?.label ?? String(value);
  return String(value);
}

/** The six fields only, in the API's order (no `updatedAt`, `canEdit`, …). */
export function pickArcRules(source: ArcRuleFields): ArcRuleFields {
  return {
    reviewPeriodDays: source.reviewPeriodDays,
    lapseRule: source.lapseRule,
    decisionRule: source.decisionRule,
    reminderDays: source.reminderDays,
    timeZoneId: source.timeZoneId,
    formalDisapprovalStatement: source.formalDisapprovalStatement,
  };
}

/** The fields whose values differ, in display order. */
export function changedArcRules(before: ArcRuleFields, after: ArcRuleFields): ArcRuleField[] {
  return ARC_RULE_FIELDS.filter(f => before[f] !== after[f]);
}

function isKnownTimeZone(zone: string): boolean {
  try {
    new Intl.DateTimeFormat('en-US', { timeZone: zone });
    return true;
  } catch {
    return false;
  }
}

/** Brief 07 §5.2 / brief 08 §5.2: client checks that mirror the server's (027 FR-029, R13). */
export function validateArcRules(v: ArcRuleFields): Partial<Record<ArcRuleField, string>> {
  const problems: Partial<Record<ArcRuleField, string>> = {};
  if (!Number.isInteger(v.reviewPeriodDays) || v.reviewPeriodDays < 1 || v.reviewPeriodDays > 365) {
    problems.reviewPeriodDays = 'Review period must be between 1 and 365 days.';
  }
  if (!Number.isInteger(v.reminderDays) || v.reminderDays < 0 || v.reminderDays > 30) {
    problems.reminderDays = 'Reminder days must be between 0 and 30.';
  }
  const zone = (v.timeZoneId ?? '').trim();
  if (!zone) problems.timeZoneId = 'Time zone is required.';
  else if (!isKnownTimeZone(zone)) problems.timeZoneId = "Time zone isn't recognized. Choose one from the list.";
  const statement = (v.formalDisapprovalStatement ?? '').trim();
  if (!statement) problems.formalDisapprovalStatement = 'The formal disapproval statement is required.';
  else if (v.formalDisapprovalStatement.length > STATEMENT_MAX) problems.formalDisapprovalStatement = 'The statement can be at most 1,000 characters.';
  return problems;
}

/** A server `VALIDATION_ERROR`'s `errors[]`, keyed by field (names in any case). */
export function arcRuleServerErrors(err: unknown): Partial<Record<ArcRuleField, string>> {
  const list = (err as { error?: { errors?: { field?: string; message?: string }[] } })?.error?.errors;
  const out: Partial<Record<ArcRuleField, string>> = {};
  if (!Array.isArray(list)) return out;
  for (const e of list) {
    const field = ARC_RULE_FIELDS.find(f => f.toLowerCase() === (e.field ?? '').toLowerCase());
    if (field && e.message) out[field] = e.message;
  }
  return out;
}
