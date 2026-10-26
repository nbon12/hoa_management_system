import { formatDateTimeInZone } from './date-format';

/** The fields every invitation row has (community, residency and company lists). */
export interface InvitationLike {
  state: string;
  effectiveState: string;
  revokeReason?: string | null;
  sentAt?: string | null;
  expiresAt?: string | null;
}

/** Conventions §5.2: the status chip's words. */
export function invitationStatusLabel(inv: InvitationLike): string {
  if (inv.effectiveState === 'Revoked' || inv.state === 'Revoked') {
    return inv.revokeReason === 'CommunityArchived' ? 'Revoked — community archived' : 'Revoked';
  }
  return inv.effectiveState || inv.state;
}

/**
 * Conventions §5.2 / brief 05 §5: the status's second line. The list DTOs carry no revoker or
 * revocation time, so a revoked row says why when the reason helps, and nothing else.
 */
export function invitationStatusDetail(inv: InvitationLike, timeZoneId: string | null | undefined, communityName?: string | null): string | null {
  switch (invitationStatusLabel(inv)) {
    case 'Scheduled': return `Sent when ${communityName || 'the community'} is marked Active`;
    case 'Pending': return inv.expiresAt ? `Expires ${formatDateTimeInZone(inv.expiresAt, timeZoneId)}` : null;
    case 'Expired': return inv.expiresAt ? `Expired ${formatDateTimeInZone(inv.expiresAt, timeZoneId)}` : null;
    case 'Revoked':
      if (inv.revokeReason === 'SenderLostAuthority') return "Sender's role ended";
      if (inv.revokeReason === 'Superseded') return 'Replaced by a newer invitation';
      return null;
    default: return null;
  }
}

/** A time shown in the community's zone, or "Not sent yet" for a Scheduled invitation. */
export function invitationTime(iso: string | null | undefined, timeZoneId: string | null | undefined): string {
  return iso ? formatDateTimeInZone(iso, timeZoneId) : 'Not sent yet';
}

/** Open invitations can be resent or revoked; accepted and revoked ones are final (FR-022). */
export function isOpenInvitation(inv: InvitationLike): boolean {
  return ['Scheduled', 'Pending', 'Expired'].includes(inv.effectiveState || inv.state);
}
