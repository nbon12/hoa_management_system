import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="auth-page" style="background:linear-gradient(135deg,var(--pink) 0%,var(--lav) 100%);min-height:100vh;display:flex;flex-direction:column;">
      <div style="flex:1;display:grid;grid-template-columns:1fr 1fr;max-width:900px;width:100%;margin:auto;background:var(--paper);border-radius:20px;box-shadow:0 4px 32px rgba(58,47,63,.1);overflow:hidden;">

        <!-- Left panel -->
        <div style="background:var(--lav);padding:48px 40px;display:flex;flex-direction:column;">
          <div style="display:flex;align-items:center;gap:8px;">
            <span class="logo-mark"></span>
            <span class="hand" style="font-size:24px;font-weight:700;">NekoHOA</span>
          </div>
          <div style="flex:1;display:flex;flex-direction:column;justify-content:center;max-width:320px;">
            <h1 style="font-size:36px;line-height:1.05;margin:8px 0 20px;letter-spacing:-.02em;font-weight:600;">
              Sign in to your <span class="hand" style="color:var(--violet);">community.</span>
            </h1>
            <p class="muted" style="font-size:13px;margin-bottom:24px;">
              The Sakura Heights resident portal — bills, violations, neighbors, all in one place.
            </p>
            <div class="ph" style="height:100px;max-width:280px;border-radius:12px;">
              🏘 neighborhood · sakura heights
            </div>
          </div>
          <div style="font-size:11px;color:var(--ink-mute);">powered by NekoHOA · v1.0</div>
        </div>

        <!-- Right panel -->
        <div style="padding:48px 40px;display:flex;flex-direction:column;justify-content:center;">
          <div class="field-label" style="font-size:13px;color:var(--ink-soft);text-transform:uppercase;letter-spacing:.1em;">Sign in</div>

          @if (error()) {
            <div class="alert alert--error" style="margin-top:14px;">
              <span>⚠</span> {{ error() }}
            </div>
          }

          <div style="display:flex;flex-direction:column;gap:14px;margin-top:14px;">
            <button class="btn btn--block" style="justify-content:center;padding:10px 14px;" type="button">
              ◯&nbsp; Continue with Google
            </button>

            <div style="display:flex;align-items:center;gap:8px;color:var(--ink-mute);font-size:11px;">
              <hr class="divider" style="flex:1;">
              <span>or with email</span>
              <hr class="divider" style="flex:1;">
            </div>

            <div>
              <div class="field-label">Email</div>
              <input class="field" type="email" placeholder="you@example.com"
                     [(ngModel)]="email" name="email" />
            </div>
            <div>
              <div class="field-label">Password</div>
              <input class="field" type="password" placeholder="••••••••"
                     [(ngModel)]="password" name="password"
                     (keydown.enter)="submit()" />
            </div>

            <button class="btn btn--primary btn--block" style="padding:10px 14px;"
                    (click)="submit()" [disabled]="loading()">
              @if (loading()) { <span class="spinner"></span> } @else { Sign in → }
            </button>

            <div style="display:flex;justify-content:space-between;font-size:12px;">
              <span class="link">Forgot password</span>
              <a class="link" routerLink="/login/quick-pay">Quick pay (no login)</a>
            </div>

            <div style="text-align:center;font-size:12px;">
              No account? <a class="link" routerLink="/register">Register</a>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class LoginComponent {
  private auth   = inject(AuthService);
  private router = inject(Router);

  email    = '';
  password = '';
  loading  = signal(false);
  error    = signal('');

  async submit() {
    if (!this.email || !this.password) {
      this.error.set('Please enter your email and password.');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    try {
      await this.auth.login(this.email, this.password);
      this.router.navigate(['/app/dashboard']);
    } catch {
      this.error.set('Invalid email or password. Try anything@example.com / password123');
    } finally {
      this.loading.set(false);
    }
  }
}
