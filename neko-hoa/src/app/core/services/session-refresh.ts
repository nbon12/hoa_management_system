import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TokenService } from './token.service';
import { AuthService, AuthSessionResponse } from './auth.service';

// 020-D FR-D1/FR-D5 (research D-R2/D-R3): silent refresh against the HttpOnly cookie with
// single-flight semantics — in-tab via a shared promise, cross-tab via the Web Locks API so the
// backend's strict one-time-use rotation is never raced. Tabs that lose the race adopt the
// winner's token from the BroadcastChannel instead of spending a rotation.
@Injectable({ providedIn: 'root' })
export class SessionRefreshService {
  private readonly http = inject(HttpClient);
  private readonly tokens = inject(TokenService);
  private readonly auth = inject(AuthService);

  private inFlight: Promise<string | null> | null = null;

  /** Resolves the new access token, or null when the session could not be refreshed. */
  refresh(): Promise<string | null> {
    this.inFlight ??= this.doRefresh().finally(() => { this.inFlight = null; });
    return this.inFlight;
  }

  private async doRefresh(): Promise<string | null> {
    const before = this.tokens.getAccessToken();

    const run = async (): Promise<string | null> => {
      // While waiting on the lock another tab may have refreshed — adopt its broadcast token.
      const current = this.tokens.getAccessToken();
      if (current && current !== before && !this.tokens.isTokenExpired(current)) return current;

      try {
        const res = await firstValueFrom(this.http.post<AuthSessionResponse>(
          `${environment.apiBaseUrl}/auth/refresh`, {}, { withCredentials: true }));
        this.auth.applySession(res);
        this.tokens.broadcastToken(res.token);
        return res.token;
      } catch (err) {
        if (err instanceof HttpErrorResponse && err.status === 401) {
          // Cookie invalid/rotated away — the session is over for every tab.
          this.auth.clearSession();
          this.tokens.broadcastLogout();
        }
        // Transient failures (network blip) do not end the session; caller sees null.
        return null;
      }
    };

    const locks = (navigator as unknown as { locks?: { request<T>(name: string, cb: () => Promise<T>): Promise<T> } }).locks;
    return locks?.request ? locks.request('neko-refresh', run) : run();
  }
}

/** Hint-gated startup re-hydration (research D-R2): no hint → no network call. */
export function sessionRefreshInitializer(tokens: TokenService, session: SessionRefreshService): () => Promise<void> {
  return async () => {
    if (tokens.hasSessionHint()) {
      await session.refresh();
    }
  };
}
