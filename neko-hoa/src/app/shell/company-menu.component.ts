import { Component, ElementRef, HostListener, Input, ViewChild, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

/** 030 FR-036 / brief 01 §4.1: the company area's pages, in this order, with no locks. */
export const COMPANY_NAV: readonly { label: string; route: string }[] = [
  { label: 'Portfolio', route: '/app/company/portfolio' },
  { label: 'Add community', route: '/app/company/communities/new' },
  { label: 'Company defaults', route: '/app/company/defaults' },
  { label: 'Company administrators', route: '/app/company/administrators' },
];

/**
 * 030 FR-036 / brief 01 (owner decision 2026-10-14): the "Company" menu in the top-bar account
 * cluster, shown by the shell to Company Administrators only. A menu button ("Company area menu")
 * that says whether it is open; arrow keys move through the items, Home/End jump, Enter chooses,
 * Esc closes and returns focus to the button, and a click outside closes it. In the company shell
 * the button shows as current.
 */
@Component({
  selector: 'app-company-menu',
  standalone: true,
  imports: [RouterLink],
  template: `
    <button type="button" #button class="btn cmenu__btn" [class.cmenu__btn--current]="current"
            aria-label="Company area menu" aria-haspopup="menu" aria-controls="company-menu"
            [attr.aria-expanded]="open()" [attr.aria-describedby]="current ? 'company-menu-current' : null"
            (click)="toggle()" (keydown.arrowdown)="openMenu($event)">Company</button>
    @if (current) { <span class="sr-only" id="company-menu-current">You're in the company area.</span> }
    @if (open()) {
      <div class="cmenu__panel" id="company-menu" (keydown)="onMenuKey($event)">
        <p class="cmenu__heading" id="company-menu-heading">Company area</p>
        <ul role="menu" aria-labelledby="company-menu-heading">
          @for (item of items; track item.route) {
            <li role="none">
              <a role="menuitem" class="cmenu__item" tabindex="-1" [routerLink]="item.route" (click)="close(false)">{{ item.label }}</a>
            </li>
          }
        </ul>
      </div>
    }
  `,
  styles: [`
    :host { position: relative; display: inline-flex; }
    .cmenu__btn--current { background: var(--lav-2); font-weight: 600; }
    .cmenu__panel {
      position: absolute; right: 0; top: calc(100% + 6px); z-index: 50; min-width: 220px;
      background: var(--paper); border: 1.5px solid var(--ink); border-radius: 12px; padding: 8px;
    }
    .cmenu__panel ul { list-style: none; }
    .cmenu__heading { font-size: 10.5px; font-weight: 600; text-transform: uppercase; color: var(--ink-mute); padding: 4px 8px; }
    .cmenu__item { display: block; padding: 8px; border-radius: 8px; color: var(--ink); text-decoration: none; }
    .cmenu__item:hover, .cmenu__item:focus { background: var(--lav); outline: none; }
  `],
})
export class CompanyMenuComponent {
  /** True inside the company area. */
  @Input() current = false;

  @ViewChild('button', { static: true }) private button!: ElementRef<HTMLButtonElement>;

  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly items = COMPANY_NAV;
  readonly open = signal(false);

  toggle(): void {
    if (this.open()) this.close(true);
    else this.openMenu();
  }

  /** Opens the menu with focus on its first item (menu-button pattern). */
  openMenu(event?: Event): void {
    event?.preventDefault();
    this.open.set(true);
    setTimeout(() => this.menuItems()[0]?.focus());
  }

  close(returnFocus: boolean): void {
    this.open.set(false);
    if (returnFocus) this.button.nativeElement.focus();
  }

  onMenuKey(event: KeyboardEvent): void {
    const items = this.menuItems();
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

  private menuItems(): HTMLElement[] {
    return Array.from(this.host.nativeElement.querySelectorAll<HTMLElement>('.cmenu__item'));
  }
}
