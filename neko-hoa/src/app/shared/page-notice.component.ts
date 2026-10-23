import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { PageNoticeService, QUERY_NOTICES } from '../core/services/page-notice.service';

/**
 * 030 conventions §6.1 / brief 11 §5–§7: the page notice. A polite live region that is always in the
 * page, so a notice is announced on arrival without taking focus, with a labelled "Dismiss". It also
 * turns a known `?notice=` key into its notice and removes the key from the address.
 */
@Component({
  selector: 'app-page-notice',
  standalone: true,
  template: `
    <div role="status" aria-live="polite">
      @if (notices.message(); as message) {
        <div class="card card--lav page-notice">
          <p>{{ message }}</p>
          <button type="button" class="btn btn--ghost" aria-label="Dismiss message" (click)="notices.dismiss()">Dismiss</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .page-notice { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .page-notice p { flex: 1; min-width: 200px; }
    .page-notice .btn { min-height: 36px; }
  `],
})
export class PageNoticeComponent implements OnInit {
  readonly notices = inject(PageNoticeService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const key = params.get('notice');
      if (!key) return;
      const message = QUERY_NOTICES[key];
      if (message) this.notices.show(message);
      void this.router.navigate([], { queryParams: { notice: null }, queryParamsHandling: 'merge', replaceUrl: true });
    });
  }
}
