import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { PageNoticeComponent } from '../../shared/page-notice.component';

/**
 * 030 brief 11 D1: the frame for the landings of a person with no usable home. A top bar with the
 * logo and avatar only (no tabs, side nav, strip, alerts or mode control), the page notice, and
 * one centred card projected in.
 */
@Component({
  selector: 'app-landing-frame',
  standalone: true,
  imports: [PageNoticeComponent],
  template: `
    <div class="lf">
      <header class="lf__top">
        <span class="logo-mark"></span>
        <span class="hand lf__logo">NekoHOA</span>
        <span class="avatar lf__avatar" aria-hidden="true">{{ user()?.initials }}</span>
      </header>
      <main class="lf__main">
        <app-page-notice />
        <ng-content />
      </main>
    </div>
  `,
  styles: [`
    .lf { min-height: 100vh; display: flex; flex-direction: column; background: var(--paper); }
    .lf__top { display: flex; align-items: center; gap: 8px; padding: 14px 16px; border-bottom: 1.5px dashed var(--line); }
    .lf__logo { font-size: 22px; font-weight: 700; }
    .lf__avatar { margin-left: auto; }
    .lf__main { width: 100%; max-width: 560px; margin: 40px auto; padding: 0 16px; display: flex; flex-direction: column; gap: 14px; }
  `],
})
export class LandingFrameComponent {
  readonly user = inject(AuthService).user;
}
