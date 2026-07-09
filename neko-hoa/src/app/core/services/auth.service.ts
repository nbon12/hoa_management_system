import { Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiClient } from '../api/api-client';
import { TokenService } from './token.service';
import { CurrentUser } from '../models';

interface AuthResponse {
  token: string;
  refreshToken: string;
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

  constructor(
    private api: ApiClient,
    private tokens: TokenService,
    private router: Router,
  ) {
    // Restore session from localStorage on startup
    const stored = localStorage.getItem('neko_user');
    const token  = this.tokens.getAccessToken();
    if (stored && token && !this.tokens.isTokenExpired(token)) {
      this._user.set(JSON.parse(stored));
    } else if (stored && this.tokens.getRefreshToken()) {
      // Token expired but refresh exists — will be silently refreshed on next API call
      this._user.set(JSON.parse(stored));
    } else {
      this.tokens.clearTokens();
    }
  }

  isLoggedIn(): boolean {
    return this._user() !== null;
  }

  async login(email: string, password: string): Promise<void> {
    const res = await this.api.post<AuthResponse>('/auth/login', { email, password });
    this._applyAuth(res);
  }

  async register(email: string, password: string, firstName: string, lastName: string, accountNumber: string): Promise<void> {
    const res = await this.api.post<AuthResponse>('/auth/register', {
      email, password, firstName, lastName, accountNumber
    });
    this._applyAuth(res);
  }

  logout(): void {
    const token = this.tokens.getAccessToken();
    if (token) {
      // Best-effort — don't block UI on network
      this.api.post('/auth/logout', {}).catch(() => {});
    }
    this._user.set(null);
    this.tokens.clearTokens();
    this.router.navigate(['/login']);
  }

  private _applyAuth(res: AuthResponse): void {
    const user: CurrentUser = {
      id:        res.user.id,
      firstName: res.user.firstName,
      lastName:  res.user.lastName,
      email:     res.user.email,
      initials:  res.user.initials,
    };
    this._user.set(user);
    this.tokens.setTokens(res.token, res.refreshToken);
    localStorage.setItem('neko_user', JSON.stringify(user));
  }
}
