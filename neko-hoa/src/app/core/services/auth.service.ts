import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
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

  private readonly base = environment.apiBaseUrl;

  constructor(
    private http: HttpClient,
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
    const res = await firstValueFrom(
      this.http.post<AuthResponse>(`${this.base}/auth/login`, { email, password })
    );
    this._applyAuth(res);
  }

  async register(email: string, password: string, firstName: string, lastName: string, accountNumber: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<AuthResponse>(`${this.base}/auth/register`, {
        email, password, firstName, lastName, accountNumber
      })
    );
    this._applyAuth(res);
  }

  logout(): void {
    const token = this.tokens.getAccessToken();
    if (token) {
      // Best-effort — don't block UI on network
      this.http.post(`${this.base}/auth/logout`, {}).subscribe({ error: () => {} });
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
