import { Injectable, NgZone, inject, signal } from '@angular/core';

// 030 R20 / conventions §7.8: a short message for a finished action whose result is also visible
// on the page. It never carries a refusal the person must act on, never takes focus, and goes away
// by itself. `ToastHostComponent` renders the list inside one polite live region (conventions §8).

export interface Toast {
  id: number;
  message: string;
}

export interface ToastOptions {
  /** How long the toast stays, in milliseconds. Long enough to read (conventions §8). */
  durationMs?: number;
}

export const DEFAULT_TOAST_MS = 6000;

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly _toasts = signal<Toast[]>([]);
  readonly toasts = this._toasts.asReadonly();

  private zone = inject(NgZone);
  private nextId = 1;
  private readonly timers = new Map<number, ReturnType<typeof setTimeout>>();

  /** Shows a toast and schedules its dismissal. Returns its id. */
  show(message: string, options: ToastOptions = {}): number {
    const id = this.nextId++;
    this._toasts.update(list => [...list, { id, message }]);
    const ms = options.durationMs ?? DEFAULT_TOAST_MS;
    // The timer runs outside Angular's zone so a waiting toast never holds the app "unstable"
    // (whenStable, e2e waits); the dismissal re-enters the zone to update the view.
    this.zone.runOutsideAngular(() => {
      this.timers.set(id, setTimeout(() => this.zone.run(() => this.dismiss(id)), ms));
    });
    return id;
  }

  dismiss(id: number): void {
    const timer = this.timers.get(id);
    if (timer !== undefined) {
      clearTimeout(timer);
      this.timers.delete(id);
    }
    this._toasts.update(list => list.filter(t => t.id !== id));
  }

  clear(): void {
    for (const id of Array.from(this.timers.keys())) this.dismiss(id);
    this._toasts.set([]);
  }
}
