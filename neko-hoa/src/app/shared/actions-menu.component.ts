import { Component, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild, inject, signal } from '@angular/core';
import { DisabledReasonDirective } from './disabled-reason.directive';

export interface MenuAction {
  key: string;
  label: string;
  /** When set, the item is shown unavailable with this reason (conventions §7.10). */
  reason?: string | null;
}

let nextId = 1;

/**
 * 030 brief 05 §7: a row's labelled "Actions" menu button. Enter or Down opens it; Up and Down move;
 * Enter chooses; Esc closes and returns focus to the button. An item with a reason stays in the
 * menu, aria-disabled, with the reason on hover and focus (DisabledReasonDirective).
 */
@Component({
  selector: 'app-actions-menu',
  standalone: true,
  imports: [DisabledReasonDirective],
  template: `
    <button type="button" #button class="btn am__btn" aria-haspopup="menu" [attr.aria-expanded]="open()" [attr.aria-controls]="menuId"
            [attr.aria-label]="accessibleName" (click)="toggle()" (keydown.arrowdown)="openMenu($event)">Actions</button>
    @if (open()) {
      <ul role="menu" class="am__menu" [id]="menuId" [attr.aria-label]="accessibleName" (keydown)="onKey($event)">
        @for (a of actions; track a.key) {
          <li role="none">
            <button type="button" role="menuitem" class="am__item" tabindex="-1" [appDisabledReason]="a.reason"
                    (click)="choose(a)">{{ a.label }}</button>
          </li>
        }
      </ul>
    }
  `,
  styles: [`
    :host { position: relative; display: inline-block; }
    .am__btn { min-height: 32px; }
    .am__menu {
      list-style: none; position: absolute; right: 0; top: calc(100% + 4px); z-index: 40; min-width: 220px;
      background: var(--paper); border: 1.5px solid var(--ink); border-radius: 10px; padding: 4px;
    }
    .am__item {
      display: block; width: 100%; text-align: left; padding: 8px 10px; border: none; border-radius: 8px;
      background: transparent; font: inherit; color: var(--ink); cursor: pointer;
    }
    .am__item:hover, .am__item:focus { background: var(--lav); outline: none; }
    .am__item[aria-disabled="true"] { color: var(--ink-mute); cursor: not-allowed; }
  `],
})
export class ActionsMenuComponent {
  @Input({ required: true }) actions: MenuAction[] = [];
  /** "Actions for Jordan Blake". */
  @Input({ required: true }) accessibleName = 'Actions';
  @Output() readonly chosen = new EventEmitter<string>();

  @ViewChild('button', { static: true }) private button!: ElementRef<HTMLButtonElement>;
  private host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly menuId = `am-menu-${nextId++}`;
  readonly open = signal(false);

  /** The menu button, so a dialog opened from here can return focus to it. */
  get buttonElement(): HTMLButtonElement {
    return this.button.nativeElement;
  }

  toggle(): void {
    if (this.open()) this.close(true);
    else this.openMenu();
  }

  openMenu(event?: Event): void {
    event?.preventDefault();
    this.open.set(true);
    setTimeout(() => this.items()[0]?.focus());
  }

  close(returnFocus: boolean): void {
    this.open.set(false);
    if (returnFocus) this.button.nativeElement.focus();
  }

  choose(action: MenuAction): void {
    if (action.reason) return;
    this.close(true);
    this.chosen.emit(action.key);
  }

  onKey(event: KeyboardEvent): void {
    const items = this.items();
    const index = items.indexOf(document.activeElement as HTMLElement);
    const move = (to: number) => { event.preventDefault(); items[(to + items.length) % items.length]?.focus(); };
    switch (event.key) {
      case 'ArrowDown': return move(index + 1);
      case 'ArrowUp': return move(index - 1);
      case 'Home': return move(0);
      case 'End': return move(items.length - 1);
      case 'Escape': event.preventDefault(); return this.close(true);
      case 'Tab': return this.close(false);
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.close(false);
  }

  private items(): HTMLElement[] {
    return Array.from(this.host.nativeElement.querySelectorAll<HTMLElement>('[role="menuitem"]'));
  }
}
