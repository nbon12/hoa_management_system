import {
  AfterViewInit, Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild,
} from '@angular/core';

let nextId = 1;

/**
 * 030 conventions §7.4, §8 "Dialogs": a modal dialog on the native `<dialog>`. Render it with `@if`
 * while it is open. On open, focus moves to its title and stays inside (the browser makes the
 * rest of the page inert). Esc acts as Cancel (`dismiss`). When it goes, focus returns to the
 * control that opened it, or to `fallbackFocus` when that control no longer exists.
 *
 * Content is projected; the shared classes in styles.scss (`dlg-*`) lay out "What happens",
 * "What doesn't change", the refusal at the top and the buttons.
 */
@Component({
  selector: 'app-dialog',
  standalone: true,
  template: `
    <dialog #dialog class="dlg" [attr.aria-labelledby]="titleId" [attr.aria-describedby]="subheading ? subId : null"
            (cancel)="onCancel($event)">
      <h2 class="section-title dlg__title" #title [id]="titleId" tabindex="-1">{{ heading }}</h2>
      @if (subheading) { <p class="muted dlg__sub" [id]="subId">{{ subheading }}</p> }
      <ng-content />
    </dialog>
  `,
  styles: [`
    .dlg {
      width: min(560px, calc(100vw - 32px)); max-height: calc(100vh - 32px); overflow-y: auto;
      border: 1.5px solid var(--ink); border-radius: 16px; padding: 20px; background: var(--paper); color: var(--ink);
    }
    .dlg::backdrop { background: color-mix(in srgb, var(--ink) 45%, transparent); }
    .dlg__title { font-size: 16px; margin: 0 0 4px; }
    .dlg__title:focus-visible { outline: 2px solid var(--violet); outline-offset: 2px; }
    .dlg__sub { margin-bottom: 10px; }
  `],
})
export class DialogComponent implements AfterViewInit, OnDestroy {
  @Input({ required: true }) heading = '';
  @Input() subheading = '';
  /** Where focus goes if the opener is gone when the dialog closes (for example the area heading). */
  @Input() fallbackFocus: HTMLElement | null = null;
  /** Esc, or the browser's own cancel. */
  @Output() readonly dismiss = new EventEmitter<void>();

  @ViewChild('dialog', { static: true }) private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('title', { static: true }) private title!: ElementRef<HTMLElement>;

  private readonly uid = nextId++;
  readonly titleId = `dlg-title-${this.uid}`;
  readonly subId = `dlg-sub-${this.uid}`;
  private readonly opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;

  ngAfterViewInit(): void {
    const dialog = this.dialog.nativeElement;
    if (dialog.isConnected && !dialog.open) dialog.showModal();
    setTimeout(() => this.title.nativeElement.focus());
  }

  ngOnDestroy(): void {
    const dialog = this.dialog.nativeElement;
    if (dialog.open) dialog.close();
    const target = this.opener?.isConnected ? this.opener : this.fallbackFocus;
    if (target?.isConnected) setTimeout(() => target.focus());
  }

  /** Moves focus to an element inside the dialog, such as a refusal that just appeared. */
  focus(selector: string): void {
    setTimeout(() => this.dialog.nativeElement.querySelector<HTMLElement>(selector)?.focus());
  }

  onCancel(event: Event): void {
    event.preventDefault();
    this.dismiss.emit();
  }
}
