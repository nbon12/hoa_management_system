import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService, AuthSessionResponse } from './auth.service';
import { Landing } from '../models';

// 030 R6 (contracts/invitations-and-auth.md § Invitation links): the invitation token travels in
// request BODIES only. It is never put in a URL, a query string or a header, so it never reaches
// server, edge or proxy logs. These endpoints are authorized by the token, not the resolver.

export type InvitationKind = 'Membership' | 'Residency' | 'CompanyAdministrator';

export interface InvitationOffer {
  /** `CommunityManager`, `BoardMember` or `Accountant` for a membership; otherwise null. */
  role: string | null;
  /** A membership's term end date (`2027-12-31`), or null when open-ended. */
  termEndDate: string | null;
  /** A residency's property address; otherwise null. */
  propertyAddress: string | null;
}

/** `POST /invitations/preview` 200: only for a valid, unexpired link. */
export interface InvitationPreview {
  kind: InvitationKind;
  /** The community's name; the company's name for a Company Administrator invitation. */
  communityName: string;
  offer: InvitationOffer;
  /** The sender's display name. */
  sentBy: string | null;
  /** UTC. */
  expiresAt: string;
  /** The community's time zone, or the company default's for a Company Administrator invitation (FR-022). */
  timeZoneId: string;
  /** Set only for an anonymous caller (FR-022a); `null` when signed in. */
  invitedEmail: string | null;
  /** Set only for an anonymous caller; `null` when signed in. */
  hasAccount: boolean | null;
}

/** `POST /invitations/accept` 200. */
export interface InvitationAcceptResult {
  accepted: boolean;
  /** The account already held the role or home: marked accepted, nothing duplicated (FR-022). */
  alreadyHeld: boolean;
  landing: Landing;
}

export interface InvitationRegistration {
  firstName: string;
  lastName: string;
  password: string;
}

@Injectable({ providedIn: 'root' })
export class InvitationService {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private readonly base = environment.apiBaseUrl;

  /** What a link offers, or a refusal (410 `INVITATION_INVALID` / `INVITATION_EXPIRED`, 429). */
  preview(token: string): Promise<InvitationPreview> {
    return firstValueFrom(this.http.post<InvitationPreview>(`${this.base}/invitations/preview`, { token }));
  }

  /**
   * FR-022a: create an account for the invited email from the link, accept the invitation and
   * sign in. The refresh token arrives as an HttpOnly cookie, so this call uses credentials.
   */
  async register(token: string, details: InvitationRegistration): Promise<AuthSessionResponse> {
    const res = await firstValueFrom(this.http.post<AuthSessionResponse>(
      `${this.base}/invitations/register`,
      { token, firstName: details.firstName, lastName: details.lastName, password: details.password },
      { withCredentials: true }));
    this.auth.applySession(res);
    return res;
  }

  /** Accept as the signed-in caller. The caller then refreshes the session (contract: then `/auth/me`). */
  accept(token: string): Promise<InvitationAcceptResult> {
    return firstValueFrom(this.http.post<InvitationAcceptResult>(`${this.base}/invitations/accept`, { token }));
  }
}
