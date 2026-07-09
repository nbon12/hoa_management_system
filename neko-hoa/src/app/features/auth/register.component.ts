import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

// 020-D FR-D9/FR-D10 (017-A register contract): registration is a verified, entitled flow —
// (1) prove control of the email (one-time code), (2) create the login, presenting the claim
// code the HOA delivered to the owner on file. There is no account-number lookup: property
// claiming is by claim code only, and every failure renders the same generic message so the UI
// is not an enumeration oracle.
@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div style="min-height:100vh;display:flex;flex-direction:column;background:var(--paper);">
      <header style="padding:18px 28px;border-bottom:1.5px dashed var(--line);display:flex;align-items:center;gap:10px;">
        <span class="logo-mark"></span>
        <span class="hand" style="font-size:24px;font-weight:700;">NekoHOA</span>
        <span style="margin-left:auto;font-size:12px;color:var(--ink-soft);">Resident registration</span>
      </header>

      <!-- Stepper -->
      <div class="stepper" style="padding:16px 36px 8px;">
        <span class="pill" [style.background]="step() >= 1 ? 'var(--rose)' : ''"
              [style.color]="step() >= 1 ? 'var(--paper)' : ''">
          1 verify email
        </span>
        <span class="stepper__line"></span>
        <span class="pill" [style.background]="step() >= 2 ? 'var(--rose)' : ''"
              [style.color]="step() >= 2 ? 'var(--paper)' : ''">
          2 enter code
        </span>
        <span class="stepper__line"></span>
        <span class="pill" [style.background]="step() >= 3 ? 'var(--rose)' : ''"
              [style.color]="step() >= 3 ? 'var(--paper)' : ''">
          3 create login
        </span>
      </div>

      <div style="flex:1;display:flex;flex-direction:column;gap:16px;padding:12px 36px 32px;max-width:440px;width:100%;margin:0 auto;">

        @if (error()) {
          <div class="alert alert--error"><span>⚠</span> {{ error() }}</div>
        }

        <!-- Step 1: request a verification code -->
        @if (step() === 1) {
          <h1 class="page-title">Verify your <span class="hand">email</span></h1>
          <p class="muted">We'll send a one-time code to confirm it's really you.</p>
          <div class="card">
            <div class="field-label">Email</div>
            <input class="field" type="email" placeholder="you@example.com"
                   [(ngModel)]="email" name="email" />
          </div>
          <button class="btn btn--primary btn--block" style="padding:10px 14px;"
                  (click)="sendCode()" [disabled]="busy()">
            @if (busy()) { <span class="spinner"></span> } @else { Send code → }
          </button>
        }

        <!-- Step 2: confirm the code -->
        @if (step() === 2) {
          <h1 class="page-title">Enter the <span class="hand">code</span></h1>
          <p class="muted">We sent a 6-digit code to <strong>{{ email }}</strong>. It expires in 30 minutes.</p>
          <div class="card">
            <div class="field-label">Verification code</div>
            <input class="field mono" inputmode="numeric" maxlength="6" placeholder="______"
                   [(ngModel)]="code" name="code" />
          </div>
          <button class="btn btn--primary btn--block" style="padding:10px 14px;"
                  (click)="confirmCode()" [disabled]="busy()">
            @if (busy()) { <span class="spinner"></span> } @else { Verify → }
          </button>
          <button class="btn btn--ghost btn--block" type="button" (click)="step.set(1)">
            Use a different email
          </button>
        }

        <!-- Step 3: create the login with the claim code -->
        @if (step() === 3) {
          <h1 class="page-title">Create your <span class="hand">login</span></h1>
          <div class="card">
            <div class="grid-2" style="gap:10px;">
              <div>
                <div class="field-label">First name</div>
                <input class="field" type="text" placeholder="Nicholas" [(ngModel)]="firstName" name="firstName" />
              </div>
              <div>
                <div class="field-label">Last name</div>
                <input class="field" type="text" placeholder="Bonilla" [(ngModel)]="lastName" name="lastName" />
              </div>
            </div>
            <div style="margin-top:10px;">
              <div class="field-label">Password</div>
              <input class="field" type="password" placeholder="Min. 8 characters"
                     [(ngModel)]="password" name="password" />
            </div>
            <div style="margin-top:10px;">
              <div class="field-label">Property claim code</div>
              <input class="field field--dashed mono" placeholder="From your HOA welcome letter"
                     [(ngModel)]="claimCode" name="claimCode" />
              <p class="muted" style="font-size:11px;margin-top:4px;">
                Your HOA sent this single-use code to the owner contact on file. Don't have it?
                Contact the HOA office.
              </p>
            </div>
          </div>
          <button class="btn btn--primary btn--block" style="padding:10px 14px;"
                  (click)="createAccount()" [disabled]="busy()">
            @if (busy()) { <span class="spinner"></span> } @else { Create account → }
          </button>
        }

        <div style="font-size:12px;text-align:center;">
          Already registered? <a class="link" routerLink="/login">Sign in instead</a>
        </div>
      </div>
    </div>
  `
})
export class RegisterComponent {
  private auth   = inject(AuthService);
  private router = inject(Router);

  // FR-D10: one generic message for every failure — never hint at which element was wrong.
  private static readonly GENERIC_ERROR =
    'Registration could not be completed. Please check your details and try again.';

  step = signal(1);

  email     = '';
  code      = '';
  firstName = '';
  lastName  = '';
  password  = '';
  claimCode = '';

  private verificationToken = '';

  busy  = signal(false);
  error = signal('');

  async sendCode() {
    if (!this.email) { this.error.set('Please enter your email address.'); return; }
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.requestEmailVerification(this.email);
      this.step.set(2);
    } catch {
      this.error.set(RegisterComponent.GENERIC_ERROR);
    } finally {
      this.busy.set(false);
    }
  }

  async confirmCode() {
    if (!this.code) { this.error.set('Please enter the 6-digit code.'); return; }
    this.busy.set(true);
    this.error.set('');
    const proof = await this.auth.confirmEmailVerification(this.email, this.code);
    this.busy.set(false);
    if (proof) {
      this.verificationToken = proof;
      this.step.set(3);
    } else {
      this.error.set(RegisterComponent.GENERIC_ERROR);
    }
  }

  async createAccount() {
    if (!this.password || !this.firstName || !this.claimCode) {
      this.error.set('Please fill in all required fields including the claim code.');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.register(this.verificationToken, this.password, this.firstName, this.lastName, this.claimCode);
      this.router.navigate(['/app/dashboard']);
    } catch {
      this.error.set(RegisterComponent.GENERIC_ERROR);
    } finally {
      this.busy.set(false);
    }
  }
}
