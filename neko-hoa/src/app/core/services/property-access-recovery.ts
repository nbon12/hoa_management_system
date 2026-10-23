import { Injectable, NgZone, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { BoardNavigationService } from './board-navigation.service';
import { PageNoticeService } from './page-notice.service';
import { SessionRefreshService } from './session-refresh';
import { TokenService } from './token.service';
import { landingTargetFor } from './landing';
import { refusalMessage } from '../../shared/refusal-messages';

/** A second denial this soon after a recovery is passed through, never recovered again (no loop). */
export const RECOVERY_COOLDOWN_MS = 10_000;

/**
 * 030 FR-024g / US9-S4 / brief 11 §6: a resident request refused with 403 `PROPERTY_ACCESS_DENIED`
 * means the selected home is gone. Recovery, once at a time:
 *
 * 1. `GET /auth/me` for the person's homes and landing now;
 * 2. if a usable home is left, `POST /auth/refresh`: the token still names the lost home, and the
 *    server selects the next usable one (Active first, then Archived) for the new token;
 * 3. go to the landing, with "You no longer have access to that home." ("Now showing …" when the
 *    new token selects another home). A person whose token named no home (they never had one,
 *    brief 11 D8) is moved without the notice.
 *
 * Concurrent denials share one recovery, and a denial within RECOVERY_COOLDOWN_MS of a finished
 * recovery is not recovered again, so a stale or inconsistent session can't loop.
 */
@Injectable({ providedIn: 'root' })
export class PropertyAccessRecoveryService {
  private auth = inject(AuthService);
  private session = inject(SessionRefreshService);
  private tokens = inject(TokenService);
  private notices = inject(PageNoticeService);
  private nav = inject(BoardNavigationService);
  private router = inject(Router);
  private zone = inject(NgZone);

  private inFlight: Promise<void> | null = null;
  private finishedAt = 0;

  recover(): Promise<void> {
    if (this.inFlight) return this.inFlight;
    if (Date.now() - this.finishedAt < RECOVERY_COOLDOWN_MS) return Promise.resolve();
    this.inFlight = this.run().finally(() => {
      this.inFlight = null;
      this.finishedAt = Date.now();
    });
    return this.inFlight;
  }

  private async run(): Promise<void> {
    const hadHome = !!this.tokens.claim(this.tokens.getAccessToken(), 'propertyId');

    let user;
    try {
      user = await this.auth.refreshCurrentUser();
    } catch {
      return; // Signed out or offline: the 401 path or the page's own error handles it.
    }

    let nowShowing: string | null = null;
    if ((user.properties?.length ?? 0) > 0) {
      const token = await this.session.refresh().catch(() => null);
      const propertyId = this.tokens.claim(token, 'propertyId');
      const refreshed = this.auth.user() ?? user;
      nowShowing = refreshed.properties?.find(p => p.id === propertyId)?.address ?? null;
      user = refreshed;
    }

    if (hadHome) this.notices.show(refusalMessage('PROPERTY_ACCESS_DENIED', { nowShowingAddress: nowShowing }));
    const target = landingTargetFor(user);
    this.nav.setActiveCommunity(target.activeCommunityId);
    await this.zone.run(() => this.router.navigate(target.commands));
  }
}
