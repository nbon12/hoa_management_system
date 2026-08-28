import {
  AfterViewChecked, Component, ElementRef, EventEmitter, Input, Output, ViewChildren, QueryList,
} from '@angular/core';
import { MetricDescriptor } from '../../../core/services/board.service';

// 025 FR-033/FR-034: the right-side glossary panel. Content is derived from the SAME
// descriptors the table renders, so a definition can never drift from its metric. Opening
// from a row scrolls to and visually distinguishes that entry, moves focus to it, and
// returns focus to the trigger on close (Accessibility; keyboard-operable).
@Component({
  selector: 'app-glossary-panel',
  standalone: true,
  template: `
    <aside class="glossary" role="dialog" aria-label="Metric definitions" (keydown.escape)="requestClose()">
      <div class="glossary__head">
        <b>What this means</b>
        <button type="button" class="glossary__close" aria-label="Close glossary" (click)="requestClose()">✕</button>
      </div>
      <p class="muted" style="font-size:11px;margin:0 0 8px;">Definitions for every metric on this page.</p>

      @for (m of descriptors; track m.id) {
        <div #entry
             class="glossary__item"
             [class.glossary__item--target]="m.id === target"
             [attr.data-glossary-id]="m.id"
             [attr.tabindex]="m.id === target ? -1 : null">
          @if (m.id === target) {
            <div class="glossary__jumped">jumped here</div>
          }
          <div class="glossary__label">{{ m.label }}</div>
          <div class="glossary__def">{{ m.definitionText }}</div>
        </div>
      }
    </aside>
  `,
  styles: [`
    .glossary {
      width: 280px; flex-shrink: 0; overflow-y: auto;
      border-left: 1.5px dashed var(--line); background: var(--lav);
      padding: 16px 14px;
    }
    .glossary__head { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; }
    .glossary__head b { font-size: 13px; }
    .glossary__close {
      margin-left: auto; border: none; background: transparent; cursor: pointer;
      color: var(--ink-mute); font-size: 14px; font-family: inherit; line-height: 1;
    }
    .glossary__close:focus-visible { outline: 2px solid var(--violet); outline-offset: 2px; }
    .glossary__item {
      padding: 10px 11px; border-radius: 10px; margin-bottom: 8px;
      border: 1.5px solid transparent;
    }
    .glossary__item--target {
      background: var(--paper); border-color: var(--violet);
    }
    .glossary__item:focus-visible { outline: 2px solid var(--violet); outline-offset: 2px; }
    .glossary__jumped {
      font-size: 9.5px; letter-spacing: .07em; text-transform: uppercase;
      color: var(--violet); font-weight: 700; margin-bottom: 3px;
    }
    .glossary__label { font-weight: 600; font-size: 12px; margin-bottom: 3px; }
    .glossary__def { font-size: 11.5px; color: var(--ink-soft); line-height: 1.55; }
  `]
})
export class GlossaryPanelComponent implements AfterViewChecked {
  @Input() descriptors: MetricDescriptor[] = [];
  @Input() target: string | null = null;
  @Output() close = new EventEmitter<void>();

  @ViewChildren('entry') entries!: QueryList<ElementRef<HTMLElement>>;

  private focusedTarget: string | null = null;

  ngAfterViewChecked(): void {
    // FR-033: when a target is set (or changes), move focus to that entry and scroll it in.
    if (this.target && this.target !== this.focusedTarget) {
      const el = this.entries?.find(e => e.nativeElement.getAttribute('data-glossary-id') === this.target);
      if (el) {
        this.focusedTarget = this.target;
        el.nativeElement.scrollIntoView({ block: 'start' });
        el.nativeElement.focus();
      }
    } else if (!this.target) {
      this.focusedTarget = null;
    }
  }

  requestClose(): void {
    this.close.emit();
  }
}
