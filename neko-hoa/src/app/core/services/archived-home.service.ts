import { Injectable, Injector, inject, signal } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './auth.service';
import { CurrentHomeService } from './current-home';

/** What a resident page is, for the archived notice: Statement stays readable (FR-012a). */
export type ResidentPageKind = 'statement' | 'payments' | 'page';

const PAYMENT_PAGES = ['/app/payments/one-time', '/app/payments/recurring'];
const RESIDENT_PREFIXES = ['/app/dashboard', '/app/payments/', '/app/property/', '/app/community/'];

export function pathOf(url: string): string {
  return url.split(/[?#]/)[0];
}

/** `null` for pages that aren't resident pages (board mode, the company area, sign-in). */
export function residentPageKind(path: string): ResidentPageKind | null {
  if (path === '/app/payments/statement' || path.startsWith('/app/payments/statement/')) return 'statement';
  if (PAYMENT_PAGES.some(p => path === p || path.startsWith(`${p}/`))) return 'payments';
  return RESIDENT_PREFIXES.some(p => path === p.replace(/\/$/, '') || path.startsWith(p)) ? 'page' : null;
}

export interface ArchivedRefusal {
  /** The page that was refused; the notice goes when the person moves to another page. */
  path: string;
  /** A payment or enrolment was sent and refused (8.11.g, "No payment was made."). */
  noPaymentMade: boolean;
}

/**
 * 030 US3-S9 / brief 11 §4.3: the auth interceptor reports each 409 `COMMUNITY_ARCHIVED`. On a
 * resident page, `ArchivedNoticeComponent` then shows the archived notice instead of the page, and
 * the home list is re-read so the home reads "No longer managed".
 */
@Injectable({ providedIn: 'root' })
export class ArchivedHomeService {
  private router = inject(Router);
  private injector = inject(Injector);

  readonly refusal = signal<ArchivedRefusal | null>(null);

  constructor() {
    this.router.events.pipe(filter((e): e is NavigationStart => e instanceof NavigationStart)).subscribe(e => {
      const current = this.refusal();
      if (current && pathOf(e.url) !== current.path) this.refusal.set(null);
    });
  }

  report(method: string): void {
    const path = pathOf(this.router.url);
    const kind = residentPageKind(path);
    if (!kind) return;
    this.refusal.set({ path, noPaymentMade: kind === 'payments' && method !== 'GET' });
    if (!this.injector.get(CurrentHomeService).archived()) {
      void this.injector.get(AuthService).refreshCurrentUser().catch(() => undefined);
    }
  }
}
