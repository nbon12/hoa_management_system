import { ArcDecision, ArcTally, ArcVoteChoice } from '../../../core/services/architectural.service';

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

/** 031: an instant in the viewer's time zone, e.g. "10/12/26 3:05 PM". */
export function fmtDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  return d.toLocaleString('en-US', {
    month: '2-digit', day: '2-digit', year: '2-digit', hour: 'numeric', minute: '2-digit',
  });
}

/** FR-007 accessible label; zero segments are kept so screen readers hear every count. */
export function tallyLabel(t: ArcTally): string {
  return `${t.approve} approve · ${t.revisionsNeeded} request revisions · ${t.deny} deny · ${t.notVoted} not voted`;
}

/**
 * FR-024 open-tab label, e.g. "decision reached: denied · revisions requested". A lapse decision
 * (FR-027, US6-S15/S16) reads "decision reached: approved by default (review period lapsed)".
 */
export function decisionReachedLabel(d: ArcDecision): string {
  if (d.source === 'Lapse')
    return `decision reached: ${d.outcome === 'Approved' ? 'approved' : 'denied'} by default (review period lapsed)`;
  const base = d.outcome === 'Approved'
    ? 'approve'
    : d.wording === 'RevisionsRequested' ? 'denied · revisions requested' : 'denied';
  return `decision reached: ${base}`;
}

/** Closed-tab label (FR-025): the legal outcome, with the owner wording for denials. */
export function outcomeLabel(d: ArcDecision): string {
  if (d.outcome === 'Approved') return 'Approved';
  if (d.outcome === 'Withdrawn') return 'Withdrawn';
  return d.wording === 'RevisionsRequested' ? 'Denied · revisions requested' : 'Denied';
}

/** 031 (item 14): the RevisionsNeeded choice is presented as "Request revisions". */
export const CHOICE_LABELS: Record<ArcVoteChoice, string> = {
  Approve: 'Approve',
  RevisionsNeeded: 'Request revisions',
  Deny: 'Deny',
};

/** "voted approve" / "voted to request revisions" / "voted deny" — for anyone's vote (031 item 10). */
export function votedLabel(choice: string | null | undefined): string {
  switch (choice) {
    case 'Approve': return 'voted approve';
    case 'RevisionsNeeded': return 'voted to request revisions';
    case 'Deny': return 'voted deny';
    default: return '';
  }
}

/** The caller's own vote: "you voted approve". */
export function myVoteLabel(choice: string | null | undefined): string {
  const voted = votedLabel(choice);
  return voted ? `you ${voted}` : '';
}

export type FinalIcon = 'approved' | 'revisions' | 'denied' | 'withdrawn';

/** 031 (item 8): the icon for a final decision — ✓ approved, ↻ revisions requested, ✕ denied. */
export function finalIcon(d: ArcDecision | null | undefined): FinalIcon | null {
  if (!d) return null;
  if (d.outcome === 'Approved') return 'approved';
  if (d.outcome === 'Withdrawn') return 'withdrawn';
  return d.wording === 'RevisionsRequested' ? 'revisions' : 'denied';
}

/** 031 (item 10): "Board final result: …". */
export function finalResultLabel(d: ArcDecision | null | undefined): string {
  switch (finalIcon(d)) {
    case 'approved':
      return d!.source === 'Lapse' ? 'Application Approved (by default — review period lapsed)' : 'Application Approved';
    case 'revisions': return 'Revisions Requested (effectively denied; the resident is asked to revise and resubmit)';
    case 'denied': return d!.source === 'Lapse' ? 'Denied (by default — review period lapsed)' : 'Denied';
    case 'withdrawn': return 'Withdrawn by the owner';
    default: return 'Not yet decided';
  }
}

/** Short project name for the panel title: "Fence replacement — 6ft cedar" → "fence replacement". */
export function shortProject(title: string): string {
  return title.split(/\s+[—–-]\s+/)[0].toLowerCase();
}

export const FORMAL_DENIAL_NOTE =
  'Request revisions counts as a formal denial; the owner is invited to revise and resubmit.';

/** 031 (item 13): shown with every question to the owner. */
export const INFO_DUE_NOTICE =
  'Info requests do not delay the due date of the board vote result. Failure to approve or deny by the due date ' +
  'may result in auto-approval. To request revisions, please select "Request revisions".';

export function infoNotice(dueDate: string): string {
  return `Board vote result due ${fmtDate(dueDate)}. ${INFO_DUE_NOTICE}`;
}
