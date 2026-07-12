import { Injectable, OnDestroy, signal } from '@angular/core';
import { Subject } from 'rxjs';

// 020-D FR-D1: the access token lives in memory only; the refresh token never reaches script
// (HttpOnly cookie). The only localStorage artifact is a non-credential "has session" hint that
// gates the startup silent refresh (research D-R2). Legacy persisted credentials are wiped once.
const HINT_KEY = 'neko_has_session';
const LEGACY_KEYS = ['neko_token', 'neko_refresh', 'neko_user'];

export type SessionEvent = { type: 'token'; token: string } | { type: 'logout' };

@Injectable({ providedIn: 'root' })
export class TokenService implements OnDestroy {
  private readonly accessToken = signal<string | null>(null);
  // Cross-tab session channel (research D-R3): one tab's refresh outcome is adopted by the rest.
  private readonly channel: BroadcastChannel | null =
    typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('neko-auth') : null;
  private readonly _sessionEvents = new Subject<SessionEvent>();
  readonly sessionEvents$ = this._sessionEvents.asObservable();

  constructor() {
    LEGACY_KEYS.forEach(k => localStorage.removeItem(k));
    this.channel?.addEventListener('message', (e: MessageEvent<SessionEvent>) => {
      const msg = e.data;
      if (msg?.type === 'token') this.accessToken.set(msg.token);
      if (msg?.type === 'logout') {
        this.accessToken.set(null);
        this.setSessionHint(false);
      }
      this._sessionEvents.next(msg);
    });
  }

  ngOnDestroy(): void { this.channel?.close(); }

  getAccessToken(): string | null { return this.accessToken(); }

  setAccessToken(token: string): void {
    this.accessToken.set(token);
    this.setSessionHint(true);
  }

  clearAccessToken(): void { this.accessToken.set(null); }

  hasSessionHint(): boolean { return localStorage.getItem(HINT_KEY) === '1'; }

  setSessionHint(on: boolean): void {
    if (on) localStorage.setItem(HINT_KEY, '1');
    else localStorage.removeItem(HINT_KEY);
  }

  broadcastToken(token: string): void { this.channel?.postMessage({ type: 'token', token }); }
  broadcastLogout(): void { this.channel?.postMessage({ type: 'logout' }); }

  isTokenExpired(token: string): boolean {
    try {
      // 020-D FR-D6: JWT payloads are base64url — normalize before atob or valid tokens
      // containing '-'/'_' are misread as expired.
      const segment = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = segment.padEnd(segment.length + ((4 - (segment.length % 4)) % 4), '=');
      const payload = JSON.parse(atob(padded));
      return Date.now() >= payload.exp * 1000;
    } catch {
      return true;
    }
  }
}
