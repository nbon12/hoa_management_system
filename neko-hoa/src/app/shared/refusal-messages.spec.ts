import { HttpErrorResponse } from '@angular/common/http';
import {
  NON_DISCLOSING_CODES, REFUSAL_CODES, RATE_LIMITED_MESSAGE, RefusalAudience, RefusalContext, SAVE_FAILED_MESSAGE,
  openWorkItemLine, refusal, refusalCodeOf, refusalMessage, roleLabel, serverMessageOf, withArticle,
} from './refusal-messages';

// 030 T034 / FR-042: every refusal is plain language from design/00-conventions.md §6, and the
// denials that must not reveal a community, property, account or invitation never name one.

/** The codes in contracts/invitations-and-auth.md § Error codes, plus NOT_FOUND from the other contracts. */
const CONTRACT_CODES = [
  'FORBIDDEN', 'COMMUNITY_NAME_TAKEN', 'NO_ACTIVE_MANAGER', 'OPEN_WORK', 'INVALID_TRANSITION',
  'COMMUNITY_ARCHIVED', 'COMMUNITY_NOT_ACTIVE', 'PARENT_ARCHIVED', 'PARENT_CYCLE', 'INVITATION_INVALID',
  'INVITATION_EXPIRED', 'INVITATION_EMAIL_MISMATCH', 'INVITATION_PENDING', 'ALREADY_MEMBER', 'ALREADY_RESIDENT',
  'SELF_GRANT_NOT_ALLOWED', 'LAST_MANAGER', 'LAST_COMPANY_ADMIN', 'PROPERTY_ACCESS_DENIED', 'REGISTRATION_FAILED',
  'RATE_LIMITED', 'VALIDATION_ERROR', 'NOT_FOUND',
];

/** A context full of names, addresses and emails from the request: none may leak for a non-disclosing code. */
const LEAKY: RefusalContext = {
  communityName: 'Keystone Park',
  otherCommunityName: 'Old Mill Commons',
  parentName: 'Keystone Crossing Master',
  personName: 'Jordan Blake',
  email: 'ann@example.com',
  role: 'CommunityManager',
  propertyAddress: '711 Keystone Park Dr #29',
  serverMessage: 'Keystone Park belongs to ann@example.com',
};
const LEAKY_VALUES = [
  'Keystone', 'Old Mill', 'Jordan', 'Blake', 'ann@example.com', '@', 'Community Manager', '711', 'belongs to',
];

const AUDIENCES: (RefusalAudience | undefined)[] = [undefined, 'companyAdmin', 'staff', 'resident', 'invitee'];
const VARIANTS = [undefined, 'payments', 'resend', 'claimCode', 'restore', 'resident', 'scheduled', 'companyAdmins', 'downgrade', 'endDate', 'race'];

describe('refusalMessage (030 FR-042)', () => {
  it('knows every contract code', () => {
    expect([...REFUSAL_CODES as readonly string[]].sort()).toEqual([...CONTRACT_CODES].sort());
  });

  it('gives every contract code its own plain-language message, never the generic save failure', () => {
    for (const code of CONTRACT_CODES) {
      const message = refusalMessage(code, { ...LEAKY, signedInEmail: 'bob@example.com' });
      expect(message.length).withContext(code).toBeGreaterThan(10);
      expect(message).withContext(code).not.toBe(SAVE_FAILED_MESSAGE);
      // Codes are never shown to people.
      expect(message).withContext(code).not.toContain(code);
    }
  });

  it('never interpolates a name, an address or an email for a non-disclosing code', () => {
    for (const code of NON_DISCLOSING_CODES) {
      for (const audience of AUDIENCES) {
        for (const surface of ['page', 'action'] as const) {
          for (const variant of VARIANTS) {
            const r = refusal(code, { ...LEAKY, audience, surface, variant });
            const text = `${r.title ?? ''} ${r.message}`;
            for (const value of LEAKY_VALUES) {
              expect(text).withContext(`${code} ${audience} ${surface} ${variant} → "${value}"`).not.toContain(value);
            }
          }
        }
      }
    }
  });

  it('INVITATION_EMAIL_MISMATCH names only the signed-in account, never the invited email (US2-S7)', () => {
    const r = refusal('INVITATION_EMAIL_MISMATCH', { email: 'ann@example.com', signedInEmail: 'bob@example.com' });
    expect(r.title).toBe('This invitation is for a different email address');
    expect(r.message).toBe("You're signed in as bob@example.com. Sign out, then open the link again and sign in or create an account with the email address the invitation was sent to.");
    expect(r.message).not.toContain('ann@example.com');
    expect(refusalMessage('INVITATION_EMAIL_MISMATCH')).toContain("You're signed in with a different account.");
  });

  it('words FORBIDDEN for a page and for an action, naming nothing', () => {
    expect(refusalMessage('FORBIDDEN', { surface: 'page' })).toBe("You don't have access to that page.");
    expect(refusalMessage('FORBIDDEN')).toBe("You don't have permission to do that.");
  });

  it('names the other community only for a Company Administrator (FR-007)', () => {
    expect(refusalMessage('COMMUNITY_NAME_TAKEN', { audience: 'staff', otherCommunityName: 'Keystone Park' }))
      .toBe('That community name is unavailable.');
    expect(refusalMessage('COMMUNITY_NAME_TAKEN', { otherCommunityName: 'Keystone Park', serverMessage: 'Keystone Park …' }))
      .toBe('That community name is unavailable.');
    expect(refusalMessage('COMMUNITY_NAME_TAKEN', { audience: 'companyAdmin', otherCommunityName: 'Keystone Park' }))
      .toBe('Keystone Park is already the name of another community. Choose a different community name.');
    expect(refusalMessage('COMMUNITY_NAME_TAKEN', { audience: 'companyAdmin', otherCommunityName: 'Old Mill Commons', otherCommunityArchived: true }))
      .toBe('Old Mill Commons is the name of an archived community. Archived communities keep their names. Choose a different community name.');
    expect(refusalMessage('COMMUNITY_NAME_TAKEN', { audience: 'companyAdmin', serverMessage: 'Server words.' })).toBe('Server words.');
    expect(refusalMessage('COMMUNITY_NAME_TAKEN', { audience: 'companyAdmin' })).toBe('That community name is unavailable.');
  });

  it('uses the conventions §6.1 copy for the community and lifecycle codes', () => {
    expect(refusalMessage('NO_ACTIVE_MANAGER', { communityName: 'Keystone Park' }))
      .toBe("Keystone Park can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted.");
    expect(refusalMessage('OPEN_WORK', { communityName: 'Keystone Crossing Master' }))
      .toBe("Keystone Crossing Master can't be archived until this work is closed:");
    expect(refusalMessage('INVALID_TRANSITION', { communityName: 'Keystone Park' })).toContain('current status');
    expect(refusalMessage('PARENT_CYCLE', { communityName: 'Keystone Crossing Master', parentName: 'Keystone Crossing TH' }))
      .toBe("Keystone Crossing TH can't be the parent of Keystone Crossing Master, because Keystone Crossing TH is already under Keystone Crossing Master. Choose a different parent.");
    expect(refusalMessage('PARENT_ARCHIVED', { parentName: 'Old Mill Commons' }))
      .toBe("Old Mill Commons is archived, so it can't be a parent.");
    expect(refusalMessage('PARENT_ARCHIVED', { variant: 'restore', communityName: 'Old Mill Commons Villas', parentName: 'Old Mill Commons' }))
      .toBe("Old Mill Commons Villas can't be restored while its parent, Old Mill Commons, is archived. Restore Old Mill Commons first.");
  });

  it('words COMMUNITY_ARCHIVED per reader', () => {
    const ctx = { communityName: 'Old Mill Commons', companyName: 'Juniper Row Community Management' };
    expect(refusalMessage('COMMUNITY_ARCHIVED', { ...ctx, audience: 'companyAdmin' }))
      .toBe("Old Mill Commons is archived, so it can't be changed. Restore it to make changes.");
    expect(refusalMessage('COMMUNITY_ARCHIVED', { ...ctx, audience: 'staff' }))
      .toBe("Juniper Row Community Management no longer manages Old Mill Commons, so it can't be changed.");
    expect(refusalMessage('COMMUNITY_ARCHIVED', { ...ctx, audience: 'resident' }))
      .toBe("Juniper Row Community Management no longer manages Old Mill Commons. You can still view this home's ledger, statements, receipts and transactions.");
    expect(refusalMessage('COMMUNITY_ARCHIVED', { ...ctx, audience: 'resident', variant: 'payments' }))
      .toBe("Payments and autopay aren't available for this home, because Juniper Row Community Management no longer manages Old Mill Commons.");
    // Without a company name the sentence still reads.
    expect(refusalMessage('COMMUNITY_ARCHIVED', { communityName: 'Old Mill Commons' }))
      .toBe("The management company no longer manages Old Mill Commons, so it can't be changed.");
  });

  it('words COMMUNITY_NOT_ACTIVE for a resend, a claim code and a resident', () => {
    expect(refusalMessage('COMMUNITY_NOT_ACTIVE', { variant: 'resend', communityName: 'Keystone Ridge' }))
      .toBe("This invitation will be sent when Keystone Ridge is marked Active. It can't be sent before then.");
    expect(refusalMessage('COMMUNITY_NOT_ACTIVE', { variant: 'claimCode' }))
      .toBe("This home's community isn't live yet, so the claim code can't be used now. Your code has not been used up.");
    expect(refusalMessage('COMMUNITY_NOT_ACTIVE')).toBe("This home's community isn't live yet.");
  });

  it('gives the invitee page refusals a title and the brief 10 body', () => {
    expect(refusal('INVITATION_EXPIRED')).toEqual({
      title: 'This invitation has expired',
      message: 'Invitation links work for 72 hours. Ask the person who invited you to send a new link.',
    });
    expect(refusal('INVITATION_INVALID')).toEqual({
      title: "This invitation link can't be used",
      message: 'It may have been cancelled, already used, or replaced by a newer link. If you still need access, ask the person who invited you.',
    });
    expect(refusalMessage('INVITATION_INVALID', { audience: 'staff' }))
      .toBe('This invitation was already accepted or revoked. The list is now up to date.');
  });

  it('words the invitation duplicates with the invitee email (the sender can already see it)', () => {
    expect(refusalMessage('INVITATION_PENDING', { email: 'pat@example.com', role: 'CommunityManager', communityName: 'Keystone Park' }))
      .toBe('pat@example.com already has a pending Community Manager invitation for Keystone Park. Resend it instead?');
    expect(refusalMessage('INVITATION_PENDING', { variant: 'resident', email: 'kim@example.com', propertyAddress: '711 Keystone Park Dr #29' }))
      .toBe('kim@example.com already has a pending invitation to 711 Keystone Park Dr #29. Resend it instead?');
    expect(refusalMessage('INVITATION_PENDING', { variant: 'scheduled', email: 'lena@example.com', propertyAddress: '2 Ridgeview Ct', communityName: 'Keystone Ridge' }))
      .toBe('lena@example.com already has a scheduled invitation to 2 Ridgeview Ct. It will be sent when Keystone Ridge is marked Active.');
    expect(refusalMessage('INVITATION_PENDING', { variant: 'companyAdmins', email: 'ops@example.com' }))
      .toBe('ops@example.com already has a pending Company Administrator invitation. Resend it instead?');
    expect(refusalMessage('ALREADY_MEMBER', { email: 'lee@example.com', role: 'BoardMember', communityName: 'Sakura Heights' }))
      .toBe('lee@example.com is already a Board Member of Sakura Heights.');
    expect(refusalMessage('ALREADY_MEMBER', { variant: 'companyAdmins', email: 'ops@example.com' }))
      .toBe('ops@example.com is already a Company Administrator.');
    expect(refusalMessage('ALREADY_RESIDENT', { email: 'jane@example.com', propertyAddress: '711 Keystone Park Dr #29' }))
      .toBe('jane@example.com is already a resident of 711 Keystone Park Dr #29.');
  });

  it('words the self-grant and last-person refusals', () => {
    expect(refusalMessage('SELF_GRANT_NOT_ALLOWED'))
      .toBe("You can't give yourself a role or a home. Ask another Company Administrator or this community's Community Manager.");
    expect(refusalMessage('SELF_GRANT_NOT_ALLOWED', { variant: 'companyAdmins' }))
      .toBe("You can't invite yourself. Ask another Company Administrator.");
    expect(refusalMessage('LAST_COMPANY_ADMIN')).toContain("You're the only Company Administrator");
    expect(refusalMessage('LAST_COMPANY_ADMIN', { variant: 'race', personName: 'Robin Hale' }))
      .toBe("Robin Hale is now the only Company Administrator, so the role can't be revoked.");
    const mgr = { personName: 'Jordan Blake', communityName: 'Sakura Heights' };
    expect(refusalMessage('LAST_MANAGER', mgr))
      .toBe('Jordan Blake is the only Community Manager of Sakura Heights with no end date. A community must always have one. Appoint another manager first.');
    expect(refusalMessage('LAST_MANAGER', { ...mgr, audience: 'companyAdmin' })).toContain('Or use Replace manager.');
    expect(refusalMessage('LAST_MANAGER', { ...mgr, variant: 'downgrade' }))
      .toBe("Jordan Blake can't be downgraded. They're the only Community Manager of Sakura Heights with no end date. Appoint another manager first.");
    expect(refusalMessage('LAST_MANAGER', { ...mgr, variant: 'endDate' }))
      .toBe("Jordan Blake can't be given an end date while they're the only Community Manager of Sakura Heights with no end date.");
  });

  it('says "that home" for PROPERTY_ACCESS_DENIED, adding only the home now shown', () => {
    expect(refusalMessage('PROPERTY_ACCESS_DENIED')).toBe('You no longer have access to that home.');
    expect(refusalMessage('PROPERTY_ACCESS_DENIED', { nowShowingAddress: '16 Old Mill Ln' }))
      .toBe('You no longer have access to that home. Now showing 16 Old Mill Ln.');
  });

  it('keeps account-creation, rate-limit and validation refusals generic (conventions §6.2)', () => {
    expect(refusalMessage('REGISTRATION_FAILED'))
      .toBe("We couldn't create an account with these details. If you already have an account, sign in to accept the invitation.");
    expect(refusalMessage('RATE_LIMITED')).toBe(RATE_LIMITED_MESSAGE);
    expect(refusal('RATE_LIMITED', { surface: 'page' })).toEqual({ title: 'Too many requests', message: 'Wait a few minutes, then try again.' });
    expect(refusalMessage('VALIDATION_ERROR')).toBe('Check these fields.');
    expect(refusalMessage('VALIDATION_ERROR', { serverMessage: 'Enter the county.' })).toBe('Enter the county.');
    expect(refusalMessage('NOT_FOUND')).toContain('Refresh the page');
  });

  it('falls back to the generic save failure for an unknown or missing code', () => {
    expect(refusalMessage('SOMETHING_NEW')).toBe(SAVE_FAILED_MESSAGE);
    expect(refusalMessage(null)).toBe(SAVE_FAILED_MESSAGE);
  });
});

describe('refusal helpers', () => {
  it('formats OPEN_WORK items one line each', () => {
    expect(openWorkItemLine({ kind: 'ArcApplication', label: 'ARC-1041', status: 'Open' })).toBe('ARC-1041 · Open');
    expect(openWorkItemLine({ kind: 'ArcApplication', label: 'ARC-1042', status: 'DecisionReached' })).toBe('ARC-1042 · Decision Reached');
    expect(openWorkItemLine({ kind: 'SubAssociation', label: 'Keystone Crossing SF', status: 'Active' }))
      .toBe('Keystone Crossing SF · sub-association, Active');
  });

  it('labels roles the way people read them', () => {
    expect(roleLabel('CommunityManager')).toBe('Community Manager');
    expect(roleLabel('BoardMember')).toBe('Board Member');
    expect(roleLabel('Accountant')).toBe('Accountant');
    expect(roleLabel('Something')).toBe('Something');
    expect(roleLabel(null)).toBe('');
  });

  it('picks the article for a role', () => {
    expect(withArticle('Board Member')).toBe('a Board Member');
    expect(withArticle('Accountant')).toBe('an Accountant');
    expect(refusalMessage('ALREADY_MEMBER', { email: 'avery@example.com', role: 'Accountant', communityName: 'Sakura Heights' }))
      .toBe('avery@example.com is already an Accountant of Sakura Heights.');
  });

  it('reads the code from an API error body, a bare 429 or a bare 403', () => {
    expect(refusalCodeOf(new HttpErrorResponse({ status: 409, error: { code: 'COMMUNITY_ARCHIVED', message: 'x' } })))
      .toBe('COMMUNITY_ARCHIVED');
    expect(refusalCodeOf(new HttpErrorResponse({ status: 429 }))).toBe('RATE_LIMITED');
    expect(refusalCodeOf(new HttpErrorResponse({ status: 403 }))).toBe('FORBIDDEN');
    expect(refusalCodeOf(new HttpErrorResponse({ status: 0 }))).toBeNull();
    expect(refusalCodeOf(undefined)).toBeNull();
  });

  it('reads the server message when there is one', () => {
    expect(serverMessageOf(new HttpErrorResponse({ status: 422, error: { code: 'VALIDATION_ERROR', message: 'Enter the county.' } })))
      .toBe('Enter the county.');
    expect(serverMessageOf(new HttpErrorResponse({ status: 500 }))).toBeNull();
    expect(serverMessageOf(null)).toBeNull();
  });
});
