import { HttpErrorResponse } from '@angular/common/http';

// 030 FR-042: every user-visible refusal says why in plain language. This is the one place that
// turns an error code from contracts/invitations-and-auth.md § Error codes into the words of
// design/00-conventions.md §6. Codes are never shown to people.
//
// Disclosure rules (FR-004a, FR-007, FR-018, FR-022a): the non-disclosing codes below never
// interpolate a name, an address or an email from the request, whatever the context holds.

/** Every error code the spec 030 contracts define or reuse. */
export const REFUSAL_CODES = [
  'FORBIDDEN',
  'COMMUNITY_NAME_TAKEN',
  'NO_ACTIVE_MANAGER',
  'OPEN_WORK',
  'INVALID_TRANSITION',
  'COMMUNITY_ARCHIVED',
  'COMMUNITY_NOT_ACTIVE',
  'PARENT_ARCHIVED',
  'PARENT_CYCLE',
  'INVITATION_INVALID',
  'INVITATION_EXPIRED',
  'INVITATION_EMAIL_MISMATCH',
  'INVITATION_PENDING',
  'ALREADY_MEMBER',
  'ALREADY_RESIDENT',
  'SELF_GRANT_NOT_ALLOWED',
  'LAST_MANAGER',
  'LAST_COMPANY_ADMIN',
  'PROPERTY_ACCESS_DENIED',
  'REGISTRATION_FAILED',
  'RATE_LIMITED',
  'VALIDATION_ERROR',
  'NOT_FOUND',
] as const;

export type RefusalCode = typeof REFUSAL_CODES[number];

/**
 * Codes whose message must not reveal that a community, property, account or invitation exists,
 * or anything about it (conventions §6.1 "Disclosure rule"; §6.2).
 */
export const NON_DISCLOSING_CODES: readonly RefusalCode[] = [
  'FORBIDDEN',
  'INVITATION_INVALID',
  'INVITATION_EXPIRED',
  'INVITATION_EMAIL_MISMATCH',
  'PROPERTY_ACCESS_DENIED',
  'REGISTRATION_FAILED',
  'RATE_LIMITED',
];

/** Who is reading. Some codes word the same refusal differently per person (conventions §6.1). */
export type RefusalAudience = 'companyAdmin' | 'staff' | 'resident' | 'invitee';

export interface RefusalContext {
  audience?: RefusalAudience;
  /** `page`: a whole page or route was refused. `action`: a button or a save was. */
  surface?: 'page' | 'action';
  /**
   * Picks a wording where one code has several:
   * - `COMMUNITY_ARCHIVED`: `payments` (resident payments and autopay);
   * - `COMMUNITY_NOT_ACTIVE`: `resend`, `claimCode`;
   * - `PARENT_ARCHIVED`: `restore`;
   * - `INVITATION_PENDING`: `resident`, `scheduled`, `companyAdmins`;
   * - `SELF_GRANT_NOT_ALLOWED` and `ALREADY_MEMBER`: `companyAdmins`;
   * - `LAST_MANAGER`: `downgrade`, `endDate`;
   * - `LAST_COMPANY_ADMIN`: `race`.
   */
  variant?: string;
  /** The community the refusal is about. */
  communityName?: string | null;
  /** The management company's name. "The management company" is used when it isn't known. */
  companyName?: string | null;
  /** `COMMUNITY_NAME_TAKEN`: the community that already has the name (Company Administrators only). */
  otherCommunityName?: string | null;
  otherCommunityArchived?: boolean;
  /** `PARENT_CYCLE` and `PARENT_ARCHIVED`: the community chosen, or the archived parent. */
  parentName?: string | null;
  /** A person named by the refusal: the last manager, or the last Company Administrator. */
  personName?: string | null;
  /** The invitee's email (`INVITATION_PENDING`, `ALREADY_MEMBER`, `ALREADY_RESIDENT`). */
  email?: string | null;
  /** A role, as the API names it (`CommunityManager`) or as people read it. */
  role?: string | null;
  propertyAddress?: string | null;
  /** `INVITATION_EMAIL_MISMATCH`: the viewer's own signed-in email, never the invited one. */
  signedInEmail?: string | null;
  /** `PROPERTY_ACCESS_DENIED`: the viewer's other home the app now shows. */
  nowShowingAddress?: string | null;
  /** The server's `message`, used only where the server's wording is the right one. */
  serverMessage?: string | null;
}

export interface Refusal {
  /** Set for refusals shown as a whole page (conventions §6.1 "Page"). */
  title: string | null;
  message: string;
}

export const RATE_LIMITED_MESSAGE = 'Too many requests. Wait a few minutes, then try again.';
export const SAVE_FAILED_MESSAGE = "Couldn't save. Check your connection and try again.";

const ROLE_LABELS: Record<string, string> = {
  CommunityManager: 'Community Manager',
  BoardMember: 'Board Member',
  Accountant: 'Accountant',
  Resident: 'Resident',
  CompanyAdministrator: 'Company Administrator',
};

/** "CommunityManager" → "Community Manager". Labels people already read pass through. */
export function roleLabel(role: string | null | undefined): string {
  if (!role) return '';
  return ROLE_LABELS[role] ?? role;
}

/** "a Board Member", "an Accountant". */
export function withArticle(label: string): string {
  return `${/^[AEIOU]/i.test(label) ? 'an' : 'a'} ${label}`;
}

function company(ctx: RefusalContext, capital: boolean): string {
  if (ctx.companyName) return ctx.companyName;
  return capital ? 'The management company' : 'the management company';
}

function community(ctx: RefusalContext, fallback = 'This community'): string {
  return ctx.communityName || fallback;
}

/** The title and message for a refusal code (conventions §6). Unknown codes get the generic save failure. */
export function refusal(code: string | null | undefined, ctx: RefusalContext = {}): Refusal {
  const plain = (message: string): Refusal => ({ title: null, message });

  switch (code) {
    case 'FORBIDDEN':
      return plain(ctx.surface === 'page'
        ? "You don't have access to that page."
        : "You don't have permission to do that.");

    case 'COMMUNITY_NAME_TAKEN':
      // FR-007: names the other community only for a Company Administrator.
      if (ctx.audience === 'companyAdmin') {
        if (ctx.otherCommunityName) {
          return plain(ctx.otherCommunityArchived
            ? `${ctx.otherCommunityName} is the name of an archived community. Archived communities keep their names. Choose a different community name.`
            : `${ctx.otherCommunityName} is already the name of another community. Choose a different community name.`);
        }
        if (ctx.serverMessage) return plain(ctx.serverMessage);
      }
      return plain('That community name is unavailable.');

    case 'NO_ACTIVE_MANAGER':
      return plain(`${community(ctx)} can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted.`);

    case 'OPEN_WORK':
      // The items follow, one line each (`openWorkItemLine`).
      return plain(`${community(ctx)} can't be archived until this work is closed:`);

    case 'INVALID_TRANSITION':
      return plain(`${community(ctx)} can't do that in its current status. Refresh the page to see its status now.`);

    case 'COMMUNITY_ARCHIVED':
      if (ctx.audience === 'companyAdmin') {
        return plain(`${community(ctx)} is archived, so it can't be changed. Restore it to make changes.`);
      }
      if (ctx.audience === 'resident') {
        return plain(ctx.variant === 'payments'
          ? `Payments and autopay aren't available for this home, because ${company(ctx, false)} no longer manages ${community(ctx, 'this community')}.`
          : `${company(ctx, true)} no longer manages ${community(ctx, 'this community')}. You can still view this home's ledger, statements, receipts and transactions.`);
      }
      return plain(`${company(ctx, true)} no longer manages ${community(ctx, 'this community')}, so it can't be changed.`);

    case 'COMMUNITY_NOT_ACTIVE':
      if (ctx.variant === 'resend') {
        return plain(`This invitation will be sent when ${community(ctx, 'the community')} is marked Active. It can't be sent before then.`);
      }
      if (ctx.variant === 'claimCode') {
        return plain("This home's community isn't live yet, so the claim code can't be used now. Your code has not been used up.");
      }
      return plain("This home's community isn't live yet.");

    case 'PARENT_ARCHIVED':
      if (ctx.variant === 'restore') {
        const parent = ctx.parentName || 'its parent community';
        return plain(`${community(ctx)} can't be restored while its parent, ${parent}, is archived. Restore ${parent} first.`);
      }
      return plain(`${ctx.parentName || 'That community'} is archived, so it can't be a parent.`);

    case 'PARENT_CYCLE': {
      const chosen = ctx.parentName || 'That community';
      const self = community(ctx, 'this community');
      return plain(`${chosen} can't be the parent of ${self}, because ${chosen} is already under ${self}. Choose a different parent.`);
    }

    case 'INVITATION_EXPIRED':
      return {
        title: 'This invitation has expired',
        message: 'Invitation links work for 72 hours. Ask the person who invited you to send a new link.',
      };

    case 'INVITATION_INVALID':
      if (ctx.audience === 'staff' || ctx.audience === 'companyAdmin') {
        return plain('This invitation was already accepted or revoked. The list is now up to date.');
      }
      return {
        title: "This invitation link can't be used",
        message: 'It may have been cancelled, already used, or replaced by a newer link. If you still need access, ask the person who invited you.',
      };

    case 'INVITATION_EMAIL_MISMATCH': {
      // Never the invited email (FR-022): only the viewer's own account.
      const who = ctx.signedInEmail ? `You're signed in as ${ctx.signedInEmail}.` : "You're signed in with a different account.";
      return {
        title: 'This invitation is for a different email address',
        message: `${who} Sign out, then open the link again and sign in or create an account with the email address the invitation was sent to.`,
      };
    }

    case 'INVITATION_PENDING': {
      const email = ctx.email || 'This email';
      if (ctx.variant === 'scheduled') {
        return plain(`${email} already has a scheduled invitation to ${ctx.propertyAddress || 'this home'}. It will be sent when ${community(ctx, 'the community')} is marked Active.`);
      }
      if (ctx.variant === 'resident') {
        return plain(`${email} already has a pending invitation to ${ctx.propertyAddress || 'this home'}. Resend it instead?`);
      }
      if (ctx.variant === 'companyAdmins') {
        return plain(`${email} already has a pending Company Administrator invitation. Resend it instead?`);
      }
      const role = roleLabel(ctx.role);
      return plain(`${email} already has a pending ${role ? role + ' ' : ''}invitation for ${community(ctx, 'this community')}. Resend it instead?`);
    }

    case 'ALREADY_MEMBER': {
      const email = ctx.email || 'This email';
      if (ctx.variant === 'companyAdmins') return plain(`${email} is already a Company Administrator.`);
      return plain(`${email} is already ${withArticle(roleLabel(ctx.role) || 'member')} of ${community(ctx, 'this community')}.`);
    }

    case 'ALREADY_RESIDENT':
      return plain(`${ctx.email || 'This email'} is already a resident of ${ctx.propertyAddress || 'this home'}.`);

    case 'SELF_GRANT_NOT_ALLOWED':
      return plain(ctx.variant === 'companyAdmins'
        ? "You can't invite yourself. Ask another Company Administrator."
        : "You can't give yourself a role or a home. Ask another Company Administrator or this community's Community Manager.");

    case 'LAST_COMPANY_ADMIN':
      if (ctx.variant === 'race' && ctx.personName) {
        return plain(`${ctx.personName} is now the only Company Administrator, so the role can't be revoked.`);
      }
      return plain("You're the only Company Administrator, so your role can't be revoked. Invite another Company Administrator first. Once they accept, your role can be revoked.");

    case 'LAST_MANAGER': {
      const name = ctx.personName || 'This manager';
      const where = community(ctx, 'this community');
      if (ctx.variant === 'downgrade') {
        return plain(`${name} can't be downgraded. They're the only Community Manager of ${where} with no end date. Appoint another manager first.`);
      }
      if (ctx.variant === 'endDate') {
        return plain(`${name} can't be given an end date while they're the only Community Manager of ${where} with no end date.`);
      }
      const base = `${name} is the only Community Manager of ${where} with no end date. A community must always have one. Appoint another manager first.`;
      return plain(ctx.audience === 'companyAdmin' ? `${base} Or use Replace manager.` : base);
    }

    case 'PROPERTY_ACCESS_DENIED':
      // Says "that home", never the address of the home that was lost.
      return plain(ctx.nowShowingAddress
        ? `You no longer have access to that home. Now showing ${ctx.nowShowingAddress}.`
        : 'You no longer have access to that home.');

    case 'REGISTRATION_FAILED':
      // Spec 017 FR-A5: generic on purpose; never says the email is taken.
      return plain("We couldn't create an account with these details. If you already have an account, sign in to accept the invitation.");

    case 'RATE_LIMITED':
      // One message for every limit, so a per-recipient cap hints at nothing (conventions §6.2).
      return ctx.surface === 'page'
        ? { title: 'Too many requests', message: 'Wait a few minutes, then try again.' }
        : plain(RATE_LIMITED_MESSAGE);

    case 'VALIDATION_ERROR':
      return plain(ctx.serverMessage || 'Check these fields.');

    case 'NOT_FOUND':
      return plain('That was already changed. Refresh the page to see the latest.');

    default:
      return plain(SAVE_FAILED_MESSAGE);
  }
}

/** The message alone, for inline, banner, dialog and toast refusals. */
export function refusalMessage(code: string | null | undefined, ctx: RefusalContext = {}): string {
  return refusal(code, ctx).message;
}

/** One `OPEN_WORK` line: "ARC-1041 · Open", "Keystone Crossing SF · sub-association, Active". */
export function openWorkItemLine(item: { kind: string; label: string; status: string }): string {
  const status = item.status === 'DecisionReached' ? 'Decision Reached' : item.status;
  return item.kind === 'SubAssociation'
    ? `${item.label} · sub-association, ${status}`
    : `${item.label} · ${status}`;
}

/** The `{ code }` of an API error, or `RATE_LIMITED` for a bare 429; `null` for a network failure. */
export function refusalCodeOf(err: unknown): string | null {
  const e = err as Partial<HttpErrorResponse> | null | undefined;
  const code = (e?.error as { code?: unknown } | null | undefined)?.code;
  if (typeof code === 'string' && code) return code;
  if (e?.status === 429) return 'RATE_LIMITED';
  if (e?.status === 403) return 'FORBIDDEN';
  return null;
}

/** The server's `message`, when the error body has one. */
export function serverMessageOf(err: unknown): string | null {
  const body = (err as Partial<HttpErrorResponse> | null | undefined)?.error as { message?: unknown } | null | undefined;
  return typeof body?.message === 'string' && body.message ? body.message : null;
}
