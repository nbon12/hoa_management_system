import {
  AfterViewInit, Component, ElementRef, EventEmitter, HostBinding, Input, OnDestroy, Output, ViewChild, inject,
} from '@angular/core';
import { ViewportService } from '../core/services/viewport.service';

let nextId = 1;

/**
 * 030 R20 / brief 06 / conventions §8 "Side panels": the property drawer, following spec 025's
 * glossary panel. Render it with `@if` while it is open. It sits beside the list (full width below
 * 768 px), moves focus to its heading when it opens, closes on Esc or its close button (emitting
 * `close`), and returns focus to the control that opened it — or `returnFocusTo` — when it goes.
 */
@Component({
  selector: 'app-property-drawer',
  standalone: true,
  template: `
    <aside class="drawer" [class.drawer--full]="narrow()" role="dialog" aria-modal="false"
           [attr.aria-labelledby]="headingId" (keydown.escape)="requestClose()">
      <div class="drawer__head">
        <h2 class="section-title drawer__title" #title [id]="headingId" tabindex="-1">{{ heading }}</h2>
        <button type="button" class="drawer__close" [attr.aria-label]="'Close ' + heading" (click)="requestClose()">✕</button>
      </div>
      <ng-content />
    </aside>
  `,
  styles: [`
    :host { display: block; flex: 0 0 auto; }
    :host(.drawer-host--full) { flex: none; }
    .drawer {
      width: 360px; max-width: 100%; height: 100%; overflow-y: auto;
      border-left: 1.5px dashed var(--line); background: var(--lav); padding: 16px 14px;
    }
    .drawer--full { position: fixed; inset: 0; width: 100%; z-index: 900; border-left: none; }
    .drawer__head { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; }
    .drawer__title { margin: 0; }
    .drawer__title:focus-visible, .drawer__close:focus-visible { outline: 2px solid var(--violet); outline-offset: 2px; }
    .drawer__close {
      margin-left: auto; border: none; background: transparent; cursor: pointer; color: var(--ink-soft);
      font: inherit; font-size: 14px; line-height: 1; min-width: 44px; min-height: 44px;
    }
  `],
})
export class PropertyDrawerComponent implements AfterViewInit, OnDestroy {
  /** The drawer's heading and accessible name, for example the property address. */
  @Input({ required: true }) heading = '';
  /** Where focus goes when the drawer closes. Defaults to whatever had focus when it opened. */
  @Input() returnFocusTo: HTMLElement | null = null;
  @Output() readonly close = new EventEmitter<void>();

  @ViewChild('title', { static: true }) private title!: ElementRef<HTMLElement>;

  readonly narrow = inject(ViewportService).isNarrow;
  readonly headingId = `property-drawer-${nextId++}`;

  /** The row (or other control) that had focus when the drawer opened. */
  private readonly opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;

  @HostBinding('class.drawer-host--full') get fullWidth(): boolean {
    return this.narrow();
  }

  ngAfterViewInit(): void {
    // After this check, so moving focus can't change bindings that were just checked.
    setTimeout(() => this.title.nativeElement.focus());
  }

  ngOnDestroy(): void {
    const target = this.returnFocusTo ?? this.opener;
    if (target && target.isConnected) target.focus();
  }

  requestClose(): void {
    this.close.emit();
  }
}
