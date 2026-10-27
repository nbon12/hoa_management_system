import { DestroyRef, Injectable, inject, signal } from '@angular/core';

/** Below this width the property drawer goes full width and the settings side list collapses (constitution §6). */
export const NARROW_QUERY = '(max-width: 767.98px)';

// 030 R20: one source for the phone/tablet breakpoint, so components react to it in code (and
// specs can set it) instead of each reading `matchMedia` on its own.
@Injectable({ providedIn: 'root' })
export class ViewportService {
  private readonly _narrow = signal(false);
  /** True below 768 CSS px. */
  readonly isNarrow = this._narrow.asReadonly();

  constructor() {
    const mql = typeof window !== 'undefined' && typeof window.matchMedia === 'function'
      ? window.matchMedia(NARROW_QUERY)
      : null;
    if (!mql) return;
    this._narrow.set(mql.matches);
    const onChange = (e: MediaQueryListEvent) => this._narrow.set(e.matches);
    mql.addEventListener('change', onChange);
    inject(DestroyRef).onDestroy(() => mql.removeEventListener('change', onChange));
  }
}
