import { Component, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../core/services/auth.service';
import { CurrentHomeService } from '../core/services/current-home';
import { ToastService } from '../core/services/toast.service';
import { UserPropertySummary } from '../core/models';
import { refusalCodeOf, refusalMessage } from '../shared/refusal-messages';

/**
 * 030 FR-012a / brief 11 8.11.c (D2, D3, D6): the resident strip's home, and the home list.
 *
 * - The strip reads "<community> · <address>", with the chip "No longer managed" for a home in an
 *   Archived community.
 * - With two or more homes, the address is a menu button ("Switch home. Current: …"). The menu,
 *   "Your homes", lists homes in Active communities first, then "No longer managed" ones. A home in
 *   an Onboarding community is never listed.
 * - Choosing a home makes it the selected one and opens its Dashboard, or Statement for a
 *   "No longer managed" home. Arrow keys move, Esc or a click outside closes, focus returns.
 */
@Component({
  selector: 'app-home-switcher',
  standalone: true,
  template: `
    <b>{{ communityName() }}</b>
    @if (address()) {
      <span aria-hidden="true">·</span>
      @if (homes().length > 1) {
        <button type="button" #button class="hs__btn" aria-haspopup="menu" aria-controls="home-menu"
                [attr.aria-expanded]="open()" [attr.aria-label]="'Switch home. Current: ' + address()"
                (click)="toggle()" (keydown.arrowdown)="openMenu($event)">{{ address() }} ▾</button>
      } @else {
        <span>{{ address() }}</span>
      }
    }
    @if (archived()) { <span class="pill hs__chip">No longer managed</span> }
    @if (open()) {
      <div class="hs__panel" id="home-menu" (keydown)="onMenuKey($event)">
        <p class="hs__heading" aria-hidden="true">Your homes</p>
        <ul role="menu" aria-label="Your homes">
          @for (h of homes(); track h.id) {
            <li role="none">
              <button type="button" role="menuitem" class="hs__item" tabindex="-1" (click)="choose(h)">
                <span class="hs__address">{{ h.address }}</span>
                <span class="hs__community"><span class="sr-only">, </span>{{ h.communityName }}</span>
                @if (h.id === current()?.id) { <span class="hs__current"><span class="sr-only">, </span>Current</span> }
                @else if (h.communityStatus === 'Archived') { <span class="pill hs__chip"><span class="sr-only">, </span>No longer managed</span> }
              </button>
            </li>
          }
        </ul>
      </div>
    }
  `,
  styles: [`
    :host { position: relative; display: inline-flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    b { color: var(--ink); font-weight: 600; }
    .hs__btn { background: none; border: 0; padding: 0; font: inherit; color: inherit; cursor: pointer; text-decoration: underline dotted; }
    .hs__chip { font-size: 10.5px; }
    .hs__panel {
      position: absolute; left: 0; top: calc(100% + 6px); z-index: 50; min-width: 260px;
      background: var(--paper); border: 1.5px solid var(--ink); border-radius: 12px; padding: 8px;
    }
    .hs__panel ul { list-style: none; }
    .hs__heading { font-size: 10.5px; font-weight: 600; text-transform: uppercase; color: var(--ink-mute); padding: 4px 8px; }
    .hs__item {
      display: grid; grid-template-columns: 1fr auto; gap: 2px 8px; width: 100%; text-align: left;
      padding: 8px; border: 0; border-radius: 8px; background: none; color: var(--ink); font: inherit; cursor: pointer;
    }
    .hs__item:hover, .hs__item:focus { background: var(--lav); outline: none; }
    .hs__address { font-weight: 600; }
    .hs__community { grid-row: 2; color: var(--ink-soft); font-size: 11.5px; }
    .hs__current { grid-row: 1 / span 2; grid-column: 2; align-self: center; font-size: 11px; color: var(--ink-mute); }
    .hs__item .hs__chip { grid-row: 1 / span 2; grid-column: 2; align-self: center; }
  `],
})
export class HomeSwitcherComponent {
  /** `GET /property`, for backends that don't send the home list yet. */
  @Input() fallback: { communityName: string; address: string } | null = null;
  /** After another home was chosen, so the shell re-reads it. */
  @Output() readonly switched = new EventEmitter<string>();

  @ViewChild('button') private button?: ElementRef<HTMLButtonElement>;

  private auth = inject(AuthService);
  private home = inject(CurrentHomeService);
  private router = inject(Router);
  private toasts = inject(ToastService);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly homes = this.home.homes;
  readonly current = this.home.current;
  readonly archived = this.home.archived;
  readonly open = signal(false);

  readonly communityName = computed(() => this.current()?.communityName ?? this.fallback?.communityName ?? '');
  readonly address = computed(() => this.current()?.address ?? this.fallback?.address ?? '');

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
    if (returnFocus) this.button?.nativeElement.focus();
  }

  async choose(h: UserPropertySummary): Promise<void> {
    if (h.id === this.current()?.id) {
      this.close(true);
      return;
    }
    try {
      await this.auth.switchProperty(h.id);
    } catch (err) {
      const code = refusalCodeOf(err);
      this.toasts.show(code ? refusalMessage(code, { audience: 'resident' }) : "Couldn't switch homes. Check your connection and try again.");
      this.close(true);
      return;
    }
    this.close(false);
    this.switched.emit(h.id);
    // D3: a "No longer managed" home opens on the page it can still show.
    await this.router.navigate([h.communityStatus === 'Archived' ? '/app/payments/statement' : '/app/dashboard']);
  }

  onMenuKey(event: KeyboardEvent): void {
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
    return Array.from(this.host.nativeElement.querySelectorAll<HTMLElement>('.hs__item'));
  }
}
