import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TokenService } from './token.service';
import { CommunityStatus, CurrentUser, Landing, UserMode } from '../models';

// 020-D FR-D1 (contracts/auth-session.md): auth responses carry the access token + user only —
// the refresh token arrives as an HttpOnly cookie, so /auth calls use withCredentials.
/** `CurrentUserDto`: `/auth/me`, and `user` in every `AuthResponse` (contracts/invitations-and-auth.md). */
export interface CurrentUserDto {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  initials: string;
  properties: {
    id: string;
    accountNumber: string;
    address: string;
    // 030 R11: filled by backends that ship spec 030.
    communityId?: string;
    communityName?: string;
    communityStatus?: CommunityStatus;
  }[];
  // 025: present on backends that ship the board feature; older tokens omit them.
  lastActiveMode?: string;
  memberships?: { communityId: string; communityName: string; role: string; communityStatus?: CommunityStatus }[];
  // 030 R11 / FR-036a: present on backends that ship spec 030.
  isCompanyAdministrator?: boolean;
  landing?: Landing | null;
}

export interface AuthSessionResponse {
  token: string;
  expiresAt: string;
  user: CurrentUserDto;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private static readonly LANDINGS = new Set<Landing>(['Board', 'Company', 'Resident', 'NotLive', 'NoHome']);

  private readonly _user = signal<CurrentUser | null>(null);
  readonly user = this._user.asReadonly();

  private readonly base = environment.apiBaseUrl;

  constructor(
    private http: HttpClient,
    private tokens: TokenService,
    private router: Router,
  ) {
    // Cross-tab logout (research D-R3): another tab ending the session ends it here too.
    // Startup re-hydration is handled by the hint-gated APP_INITIALIZER (session-refresh.ts).
    this.tokens.sessionEvents$.subscribe(evt => {
      if (evt.type === 'logout') {
        this._user.set(null);
        this.router.navigate(['/login']);
      }
    });
  }

  isLoggedIn(): boolean {
    return this._user() !== null;
  }

  async login(email: string, password: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<AuthSessionResponse>(`${this.base}/auth/login`, { email, password }, { withCredentials: true })
    );
    this.applySession(res);
  }

  // 020-D FR-D9 (017-A register contract): registration binds to a property via an
  // email-verification proof + a single-use claim code — never an account number.
  async requestEmailVerification(email: string): Promise<void> {
    await firstValueFrom(
      this.http.post(`${this.base}/auth/verify-email/request`, { email })
    );
  }

  /** Returns the opaque verification proof, or null on a (generic) confirm failure. */
  async confirmEmailVerification(email: string, code: string): Promise<string | null> {
    try {
      const res = await firstValueFrom(
        this.http.post<{ verificationToken: string }>(
          `${this.base}/auth/verify-email/confirm`, { email, code })
      );
      return res.verificationToken;
    } catch {
      return null;
    }
  }

  async register(verificationToken: string, password: string, firstName: string, lastName: string, claimCode: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<AuthSessionResponse>(`${this.base}/auth/register`, {
        verificationToken, password, firstName, lastName, claimCode
      }, { withCredentials: true })
    );
    this.applySession(res);
  }

  logout(): void {
    const token = this.tokens.getAccessToken();
    if (token) {
      // Best-effort — don't block UI on network. Server revokes + clears the cookie.
      this.http.post(`${this.base}/auth/logout`, {}, { withCredentials: true }).subscribe({ error: () => {} });
    }
    this.clearSession();
    this.tokens.broadcastLogout();
    this.router.navigate(['/login']);
  }

  /** Adopt a fresh session (login/register/silent refresh). */
  applySession(res: AuthSessionResponse): void {
    this._user.set(AuthService.toCurrentUser(res.user));
    this.tokens.setAccessToken(res.token);
  }

  /**
   * 030 R11: re-read `GET /auth/me` and adopt its user (homes, memberships, the Company
   * Administrator flag and the server's landing) without touching the access token.
   */
  async refreshCurrentUser(): Promise<CurrentUser> {
    const dto = await firstValueFrom(this.http.get<CurrentUserDto>(`${this.base}/auth/me`));
    const user = AuthService.toCurrentUser(dto);
    this._user.set(user);
    return user;
  }

  /** Maps the wire DTO to the client model, defaulting fields that pre-030 backends omit. */
  private static toCurrentUser(dto: CurrentUserDto): CurrentUser {
    return {
      id:        dto.id,
      firstName: dto.firstName,
      lastName:  dto.lastName,
      email:     dto.email,
      initials:  dto.initials,
      lastActiveMode: dto.lastActiveMode === 'Board' ? 'Board' : 'Resident',
      memberships: dto.memberships ?? [],
      properties: dto.properties ?? [],
      isCompanyAdministrator: dto.isCompanyAdministrator === true,
      landing: AuthService.LANDINGS.has(dto.landing as Landing) ? dto.landing as Landing : null,
    };
  }

  /**
   * 025 FR-018/FR-022: switch the active UI mode in place (no separate portal).
   * The server verifies board eligibility and persists the choice; the response is
   * a fresh session, adopted like any other.
   */
  async switchMode(mode: UserMode): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<AuthSessionResponse>(`${this.base}/auth/board-mode`, { mode }, { withCredentials: true })
    );
    this.applySession(res);
  }

  /** Drop the local session (no navigation, no cross-tab broadcast). */
  clearSession(): void {
    this._user.set(null);
    this.tokens.clearAccessToken();
    this.tokens.setSessionHint(false);
  }
}
