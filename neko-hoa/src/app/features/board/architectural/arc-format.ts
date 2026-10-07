import { ArcDecision, ArcTally } from '../../../core/services/architectural.service';

// 027: pure display helpers shared by the architectural review components.

/** "2026-06-27" → "06/27/26". Parsed by hand so no time-zone shift can move the day. */
export function fmtDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  return m ? `${m[2]}/${m[3]}/${m[1].slice(2)}` : iso;
}

/** 1258291 → "1.2 MB", 860160 → "840 KB", 512 → "512 B". */
export function fmtSize(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  if (bytes >= 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${bytes} B`;
}

/** FR-007 accessible label; zero segments are kept so screen readers hear every count. */
export function tallyLabel(t: ArcTally): string {
  return `${t.approve} approve · ${t.revisionsNeeded} revisions needed · ${t.deny} deny · ${t.notVoted} not voted`;
}

/** FR-024 open-tab label, e.g. "decision reached: denied · revisions requested". */
export function decisionReachedLabel(d: ArcDecision): string {
  const base = d.outcome === 'Approved'
    ? 'approve'
    : d.wording === 'RevisionsRequested' ? 'denied · revisions requested' : 'denied';
  return `decision reached: ${base}${d.source === 'Lapse' ? ' (by default — review period lapsed)' : ''}`;
}

/** Closed-tab label (FR-025): the legal outcome, with the owner wording for denials. */
export function outcomeLabel(d: ArcDecision): string {
  if (d.outcome === 'Approved') return 'Approved';
  return d.wording === 'RevisionsRequested' ? 'Denied · revisions requested' : 'Denied';
}

export function myVoteLabel(choice: string | null | undefined): string {
  switch (choice) {
    case 'Approve': return 'you voted approve';
    case 'RevisionsNeeded': return 'you voted revisions needed';
    case 'Deny': return 'you voted deny';
    default: return '';
  }
}

/** Short project name for the panel title: "Fence replacement — 6ft cedar" → "fence replacement". */
export function shortProject(title: string): string {
  return title.split(/\s+[—–-]\s+/)[0].toLowerCase();
}

export const FORMAL_DENIAL_NOTE =
  'Revisions needed counts as a formal denial; the owner is invited to revise and resubmit.';

export function infoNotice(dueDate: string): string {
  return `Questions don't pause the review period (due ${fmtDate(dueDate)}). ` +
    `To require changes before approval, vote Revisions needed — it counts as a formal denial and invites the owner to resubmit.`;
}
