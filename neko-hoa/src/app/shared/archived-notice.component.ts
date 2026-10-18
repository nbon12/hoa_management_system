import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map } from 'rxjs';
import { ArchivedHomeService, pathOf, residentPageKind } from '../core/services/archived-home.service';
import { CurrentHomeService } from '../core/services/current-home';
import { refusalMessage } from './refusal-messages';

/**
 * 030 US3-S9 / FR-012a / brief 11 §4.3: wraps the resident pages (the shell puts it around its
 * router outlet). After a 409 `COMMUNITY_ARCHIVED` on a resident page, it shows the archived notice
 * instead of the page, with "View billing history". One-time and Auto-pay get the payments notice,
 * and a refused payment adds "No payment was made." Statement stays readable: the notice is a
 * banner above it, also whenever the selected home is known to be "No longer managed".
 */
@Component({
  selector: 'app-archived-notice',
  standalone: true,
  imports: [RouterLink],
  template: `
    @switch (kind()) {
      @case ('banner') {
        <div class="card card--lav an__banner" role="status">{{ message() }}</div>
      }
      @case ('page') {
        <section class="card an__card" role="status">
          <p>{{ message() }}</p>
          @if (noPaymentMade()) { <p>No payment was made.</p> }
          <a class="btn btn--primary an__action" routerLink="/app/payments/statement">View billing history</a>
        </section>
      }
    }
    <div class="an__content" [class.an__content--hidden]="replacing()"><ng-content /></div>
  `,
  styles: [`
    :host { display: contents; }
    .an__card { display: flex; flex-direction: column; align-items: flex-start; gap: 10px; }
    .an__action { text-decoration: none; }
    .an__content { display: contents; }
    .an__content--hidden { display: none; }
  `],
})
export class ArchivedNoticeComponent {
  private router = inject(Router);
  private archived = inject(ArchivedHomeService);
  private home = inject(CurrentHomeService);

  private readonly path = toSignal(
    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd), map(e => pathOf(e.urlAfterRedirects))),
    { initialValue: pathOf(this.router.url) },
  );

  private readonly refusedHere = computed(() => {
    const r = this.archived.refusal();
    return r && r.path === this.path() ? r : null;
  });

  /** `banner` on Statement, `page` instead of a refused page, `null` otherwise. */
  readonly kind = computed<'banner' | 'page' | null>(() => {
    const page = residentPageKind(this.path());
    if (page === 'statement') return this.refusedHere() || this.home.archived() ? 'banner' : null;
    return page && this.refusedHere() ? 'page' : null;
  });

  readonly replacing = computed(() => this.kind() === 'page');
  readonly noPaymentMade = computed(() => this.refusedHere()?.noPaymentMade === true);

  readonly message = computed(() => refusalMessage('COMMUNITY_ARCHIVED', {
    audience: 'resident',
    communityName: this.home.current()?.communityName ?? null,
    variant: residentPageKind(this.path()) === 'payments' ? 'payments' : undefined,
  }));
}
