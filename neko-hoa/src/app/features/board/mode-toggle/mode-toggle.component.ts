import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { landingTargetFor } from '../../../core/services/landing';
import { BoardNavigationService, RESIDENT_ROLE } from '../../../core/services/board-navigation.service';

// 025 FR-019/FR-020: the Resident ↔ Board control. Lives in the top-bar account-controls
// cluster to the LEFT of alerts/avatar; rendered only for a user holding ≥1 active
// non-resident membership. Switching is a server round-trip (auth.switchMode) — the mode
// is UX state, never an authorization input (FR-014).
@Component({
  selector: 'app-mode-toggle',
  standalone: true,
  template: `
    @if (eligible()) {
      @if (mode() === 'Board') {
        <div class="mode-seg" role="group" aria-label="Interface mode">
          <button type="button" class="mode-seg__btn"
                  [class.mode-seg__btn--on]="false"
                  [attr.aria-pressed]="false"
                  [disabled]="busy()"
                  (click)="switch('Resident')">Resident</button>
          <button type="button" class="mode-seg__btn mode-seg__btn--on"
                  [attr.aria-pressed]="true"
                  [disabled]="busy()">Board</button>
        </div>
      } @else {
        <button type="button" class="mode-enter"
                [disabled]="busy()"
                (click)="switch('Board')">🗝️ Enter board mode</button>
      }
      @if (error()) {
        <span class="mode-err" role="alert">{{ error() }}</span>
      }
    }
  `,
  styles: [`
    :host { display: inline-flex; align-items: center; gap: 8px; }
    .mode-enter {
      font-family: inherit; font-size: 11.5px; font-weight: 600;
      padding: 5px 12px; border-radius: 999px; cursor: pointer;
      /* Dark ink on the violet fill — the binding board contrast pairing (FR-038). */
      background: var(--violet); color: var(--ink); border: 1.5px solid var(--ink);
    }
    .mode-enter:disabled { opacity: .6; cursor: default; }
    .mode-seg {
      display: inline-flex; border: 1.5px solid var(--ink); border-radius: 999px;
      overflow: hidden; background: var(--paper);
    }
    .mode-seg__btn {
      font-family: inherit; font-size: 11.5px; font-weight: 500;
      padding: 5px 12px; border: none; background: transparent; color: var(--ink-soft);
      cursor: pointer;
    }
    .mode-seg__btn--on { background: var(--violet); color: var(--ink); font-weight: 600; cursor: default; }
    .mode-seg__btn:disabled { cursor: default; }
    .mode-err { font-size: 11px; color: var(--warn); }
  `]
})
export class ModeToggleComponent {
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);
  private router = inject(Router);

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly mode = computed(() => this.auth.user()?.lastActiveMode ?? 'Resident');

  /** FR-020: control renders only for a user holding ≥1 active non-resident membership. */
  readonly eligible = computed(() =>
    (this.auth.user()?.memberships ?? []).some(m => m.role !== RESIDENT_ROLE));

  async switch(mode: 'Resident' | 'Board'): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.switchMode(mode);
      // FR-026: the post-switch landing is the SAME decision sign-in makes, so both go
      // through landingTargetFor — switchMode has already refreshed auth.user() with the
      // new lastActiveMode, so the shared rule sees the mode we just moved to.
      const target = landingTargetFor(this.auth.user());
      this.nav.setActiveCommunity(target.activeCommunityId);
      await this.router.navigate(target.commands);
    } catch (e: any) {
      // FR-020: a board-ineligible switch is refused server-side (403 NO_ACTIVE_MEMBERSHIP).
      const code = e?.error?.code;
      this.error.set(code === 'NO_ACTIVE_MEMBERSHIP'
        ? 'Board mode is not available for your account.'
        : 'Could not switch modes. Please try again.');
    } finally {
      this.busy.set(false);
    }
  }
}
