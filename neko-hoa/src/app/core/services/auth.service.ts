import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TokenService } from './token.service';
import { CurrentUser } from '../models';

// 020-D FR-D1 (contracts/auth-session.md): auth responses carry the access token + user only —
// the refresh token arrives as an HttpOnly cookie, so /auth calls use withCredentials.
export interface AuthSessionResponse {
  token: string;
  expiresAt: string;
  user: {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    initials: string;
    properties: { id: string; accountNumber: string; address: string }[];
  };
}

@Injectable({ providedIn: 'root' })
export class AuthService {
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
    const user: CurrentUser = {
      id:        res.user.id,
      firstName: res.user.firstName,
      lastName:  res.user.lastName,
      email:     res.user.email,
      initials:  res.user.initials,
    };
    this._user.set(user);
    this.tokens.setAccessToken(res.token);
  }

  /** Drop the local session (no navigation, no cross-tab broadcast). */
  clearSession(): void {
    this._user.set(null);
    this.tokens.clearAccessToken();
    this.tokens.setSessionHint(false);
  }
}
