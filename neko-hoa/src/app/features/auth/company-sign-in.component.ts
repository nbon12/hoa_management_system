import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { landingTargetFor } from '../../core/services/landing';
import { RATE_LIMITED_MESSAGE, refusalCodeOf } from '../../shared/refusal-messages';

/**
 * 030 FR-036c / brief 01 8.01.a–c: the company sign-in page, at a stable address Company
 * Administrators can bookmark (`/company/sign-in`). It runs the usual `POST /auth/login` (same
 * accounts, lockout and rate limits) and grants nothing by itself:
 * - a Company Administrator lands on the portfolio;
 * - anyone else lands on their usual FR-036a landing with `?notice=not-company-admin`.
 * Sign-in failures stay as generic as on the usual page (spec 017). No company name is shown
 * (T123); T160 adds it if the design handoff keeps it (brief 01 D4).
 */
@Component({
  selector: 'app-company-sign-in',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="auth-page csi">
      <main class="card csi__card">
        <div class="csi__brand"><span class="logo-mark"></span><span class="hand csi__logo">NekoHOA</span></div>
        <h1 class="page-title">Company sign-in</h1>
        <p class="muted">For company staff. Bookmark this page.</p>

        @if (error()) {
          <div class="alert alert--error" role="alert">{{ error() }}</div>
        }

        <form class="csi__form" (ngSubmit)="submit()" novalidate>
          <div>
            <label class="field-label" for="csi-email">Email</label>
            <input class="field" id="csi-email" name="email" type="email" inputmode="email" autocomplete="email" [(ngModel)]="email" />
          </div>
          <div>
            <label class="field-label" for="csi-password">Password</label>
            <input class="field" id="csi-password" name="password" type="password" autocomplete="current-password" [(ngModel)]="password" />
          </div>
          <button type="submit" class="btn btn--primary btn--block csi__submit" [disabled]="busy()">
            {{ busy() ? 'Signing in…' : 'Sign in' }}
          </button>
          <span class="muted">Forgot password?</span>
        </form>
      </main>
      <p class="csi__usual">Not company staff? <a class="link" routerLink="/login">Use the usual sign-in page.</a></p>
    </div>
  `,
  styles: [`
    .csi { align-items: center; justify-content: center; padding: 24px 16px; gap: 14px; }
    .csi__card { width: 100%; max-width: 420px; display: flex; flex-direction: column; gap: 12px; padding: 28px 24px; }
    .csi__brand { display: flex; align-items: center; gap: 8px; }
    .csi__logo { font-size: 22px; font-weight: 700; }
    .csi__form { display: flex; flex-direction: column; gap: 12px; }
    .csi__submit { min-height: 44px; }
    .csi__usual { font-size: 12px; }
  `],
})
export class CompanySignInComponent implements OnInit {
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);
  private router = inject(Router);

  email = '';
  password = '';
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    // Brief 01 §6: opening the page while signed in as a Company Administrator goes to the portfolio.
    if (this.auth.user()?.isCompanyAdministrator) void this.router.navigate(['/app/company/portfolio']);
  }

  async submit(): Promise<void> {
    if (this.busy()) return;
    if (!this.email.trim() || !this.password) {
      this.error.set('Please enter your email and password.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.login(this.email.trim(), this.password);
    } catch (err) {
      this.error.set(refusalCodeOf(err) === 'RATE_LIMITED' ? RATE_LIMITED_MESSAGE : 'Invalid email or password.');
      return;
    } finally {
      this.busy.set(false);
      this.password = '';
    }
    const user = this.auth.user();
    if (user?.isCompanyAdministrator) {
      this.nav.setActiveCommunity(null);
      await this.router.navigate(['/app/company/portfolio']);
      return;
    }
    const target = landingTargetFor(user);
    this.nav.setActiveCommunity(target.activeCommunityId);
    await this.router.navigate(target.commands, { queryParams: { notice: 'not-company-admin' } });
  }
}
