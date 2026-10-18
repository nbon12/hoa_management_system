import { Component, inject } from '@angular/core';
import { ToastService } from '../core/services/toast.service';

// 030 R20 / conventions §7.8, §8: renders ToastService's toasts in one polite live region that is
// always in the page (so the first toast is announced), never takes focus, and needs no action.
// Mounted once, in AppComponent, so a toast survives the navigation that follows it.
@Component({
  selector: 'app-toast-host',
  standalone: true,
  template: `
    <div class="toasts" role="status" aria-live="polite" aria-atomic="false">
      @for (t of toasts(); track t.id) {
        <div class="toast">
          <span>{{ t.message }}</span>
          <button type="button" class="toast__close" aria-label="Dismiss notification" (click)="dismiss(t.id)">✕</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .toasts {
      position: fixed; z-index: 1000; top: 16px; left: 50%; transform: translateX(-50%);
      display: flex; flex-direction: column; gap: 8px; width: min(420px, calc(100vw - 32px));
      pointer-events: none;
    }
    .toast {
      pointer-events: auto; display: flex; align-items: flex-start; gap: 10px;
      padding: 10px 12px; border-radius: 10px; font-size: 12.5px;
      background: var(--ink); color: var(--paper); border: 1.5px solid var(--ink);
    }
    .toast span { flex: 1; }
    .toast__close {
      border: none; background: transparent; color: var(--paper); cursor: pointer;
      font: inherit; line-height: 1; min-width: 24px; min-height: 24px;
    }
    .toast__close:focus-visible { outline: 2px solid var(--violet); outline-offset: 2px; }
  `],
})
export class ToastHostComponent {
  private toastService = inject(ToastService);
  readonly toasts = this.toastService.toasts;

  dismiss(id: number): void {
    this.toastService.dismiss(id);
  }
}
