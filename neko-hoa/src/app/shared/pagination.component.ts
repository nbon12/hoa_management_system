import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';

type PageSlot = number | 'gap';

/**
 * 030 conventions §7.6: "Showing 1–25 of 120", with Previous, page numbers and Next. The summary is
 * a polite live region, so filter, search and page changes are announced ("Showing 1–5 of 5
 * communities."). Emits the new offset; the list loads it and moves focus to its heading.
 */
@Component({
  selector: 'app-pagination',
  standalone: true,
  template: `
    @if (total() > 0) {
      <div class="pg">
        <p class="pg__summary" role="status" aria-live="polite">Showing {{ from() }}–{{ to() }} of {{ total() }}<span class="sr-only"> {{ noun }}.</span></p>
        @if (pageCount() > 1) {
          <nav aria-label="Pages">
            <ul class="pg__list">
              <li><button type="button" class="btn" aria-label="Previous page" [disabled]="page() === 1" (click)="go(page() - 1)">Previous</button></li>
              @for (slot of slots(); track $index) {
                @if (slot === 'gap') {
                  <li aria-hidden="true" class="pg__gap">…</li>
                } @else {
                  <li>
                    <button type="button" class="btn" [class.pg__current]="slot === page()" [attr.aria-label]="'Page ' + slot"
                            [attr.aria-current]="slot === page() ? 'page' : null" (click)="go(slot)">{{ slot }}</button>
                  </li>
                }
              }
              <li><button type="button" class="btn" aria-label="Next page" [disabled]="page() === pageCount()" (click)="go(page() + 1)">Next</button></li>
            </ul>
          </nav>
        }
      </div>
    }
  `,
  styles: [`
    .pg { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; padding: 10px 12px; }
    .pg__summary { font-size: 12px; color: var(--ink-soft); margin-right: auto; }
    .pg__list { list-style: none; display: flex; flex-wrap: wrap; gap: 4px; align-items: center; }
    .pg__list .btn { min-width: 36px; min-height: 36px; justify-content: center; }
    .pg__current { background: var(--ink); color: var(--paper); font-weight: 600; }
    .pg__gap { padding: 0 4px; color: var(--ink-mute); }
  `],
})
export class PaginationComponent {
  @Input({ required: true }) set totalItems(v: number) { this.total.set(Math.max(0, v ?? 0)); }
  @Input({ required: true }) set limit(v: number) { this.size.set(Math.max(1, v ?? 25)); }
  @Input({ required: true }) set offset(v: number) { this.start.set(Math.max(0, v ?? 0)); }
  /** What the rows are, for the announcement: "communities", "properties". */
  @Input() noun = 'items';
  @Output() readonly pageChange = new EventEmitter<number>();

  readonly total = signal(0);
  private readonly size = signal(25);
  private readonly start = signal(0);

  readonly page = computed(() => Math.floor(this.start() / this.size()) + 1);
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / this.size())));
  readonly from = computed(() => Math.min(this.total(), this.start() + 1));
  readonly to = computed(() => Math.min(this.total(), this.start() + this.size()));

  /** Every page up to 7; beyond that the first, the last and two either side of the current one. */
  readonly slots = computed<PageSlot[]>(() => {
    const count = this.pageCount();
    const current = this.page();
    if (count <= 7) return Array.from({ length: count }, (_, i) => i + 1);
    const wanted = new Set([1, count, current - 2, current - 1, current, current + 1, current + 2]);
    const pages = Array.from(wanted).filter(p => p >= 1 && p <= count).sort((a, b) => a - b);
    const slots: PageSlot[] = [];
    pages.forEach((p, i) => {
      if (i > 0 && p - pages[i - 1] > 1) slots.push('gap');
      slots.push(p);
    });
    return slots;
  });

  go(page: number): void {
    const target = Math.min(Math.max(1, page), this.pageCount());
    if (target === this.page()) return;
    this.pageChange.emit((target - 1) * this.size());
  }
}
