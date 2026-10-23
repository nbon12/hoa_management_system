import { Injectable, signal } from '@angular/core';

// 030 conventions §6.1 / brief 11 §6: a notice at the top of the page the app has just moved the
// person to — "You no longer have access to that home." — that stays until dismissed (a notice,
// not a toast, so it isn't missed). `PageNoticeComponent` renders it.

/** Notices a page can be opened with through `?notice=<key>` (brief 01 FR-036c). */
export const QUERY_NOTICES: Readonly<Record<string, string>> = {
  'not-company-admin': "This account isn't a Company Administrator, so you've been signed in to your usual page.",
};

@Injectable({ providedIn: 'root' })
export class PageNoticeService {
  private readonly _message = signal<string | null>(null);
  readonly message = this._message.asReadonly();

  show(message: string): void {
    this._message.set(message);
  }

  dismiss(): void {
    this._message.set(null);
  }
}
