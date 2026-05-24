import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

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
        <span class="pill" [style.background]="step >= 1 ? 'var(--rose)' : ''"
              [style.color]="step >= 1 ? 'var(--paper)' : ''">
          1 find property
        </span>
        <span class="stepper__line"></span>
        <span class="pill" [style.background]="step >= 2 ? 'var(--rose)' : ''"
              [style.color]="step >= 2 ? 'var(--paper)' : ''">
          2 confirm
        </span>
        <span class="stepper__line"></span>
        <span class="pill" [style.background]="step >= 3 ? 'var(--rose)' : ''"
              [style.color]="step >= 3 ? 'var(--paper)' : ''">
          3 create login
        </span>
      </div>

      <div style="flex:1;display:grid;grid-template-columns:1fr 1fr;gap:24px;padding:12px 36px 32px;align-items:flex-start;max-width:900px;width:100%;margin:0 auto;">

        <!-- Step 1: Find property -->
        @if (step === 1) {
          <div class="flex-col">
            <div>
              <h1 class="page-title">Find your <span class="hand">property</span></h1>
              <p class="muted" style="margin-top:4px;">Enter your address or 16-digit account number to get started.</p>
            </div>
            <div class="card">
              <div class="section-title">Look up by address</div>
              <div class="field-label" style="margin-top:10px;">Street address</div>
              <input class="field" type="text" placeholder="714 Keystone Park Dr" [(ngModel)]="street" name="street" />
              <div class="grid-3" style="margin-top:10px;gap:8px;">
                <div>
                  <div class="field-label">City</div>
                  <input class="field" type="text" placeholder="Morrisville" [(ngModel)]="city" name="city" />
                </div>
                <div>
                  <div class="field-label">State</div>
                  <input class="field" type="text" placeholder="NC" [(ngModel)]="state" name="state" />
                </div>
                <div>
                  <div class="field-label">ZIP</div>
                  <input class="field" type="text" placeholder="27560" [(ngModel)]="zip" name="zip" />
                </div>
              </div>
            </div>

            <div style="text-align:center;color:var(--ink-mute);font-size:11px;display:flex;align-items:center;gap:8px;">
              <hr class="divider" style="flex:1;"> or <hr class="divider" style="flex:1;">
            </div>

            <div class="card card--dashed">
              <div class="section-title">Look up by account number</div>
              <p class="muted" style="font-size:11px;margin-top:2px;">Find it on the bottom of any paper statement.</p>
              <input class="field field--dashed mono" style="margin-top:8px;"
                     placeholder="R _ _ _ _ _ _ _ L _ _ _ _ _ _ _"
                     [(ngModel)]="accountNum" name="accountNum" />
            </div>

            <button class="btn btn--primary btn--block" style="padding:10px 14px;" (click)="findProperty()">
              @if (searching()) { <span class="spinner"></span> } @else { Find my property → }
            </button>
          </div>

          <!-- Property found preview -->
          <div class="flex-col">
            @if (found()) {
              <div class="card card--lav">
                <div style="display:flex;align-items:center;gap:8px;">
                  <span class="pill pill--ok">✓ Property found</span>
                  <span class="muted" style="margin-left:auto;font-size:11px;">1 match</span>
                </div>
                <div style="display:flex;gap:14px;margin-top:14px;align-items:flex-start;">
                  <div class="ph" style="width:80px;height:80px;border-radius:10px;flex-shrink:0;font-size:28px;">🏠</div>
                  <div style="flex:1;">
                    <div style="font-size:16px;font-weight:600;">714 Keystone Park Dr</div>
                    <div class="muted">Morrisville, NC 27560</div>
                    <div style="display:flex;gap:6px;margin-top:8px;flex-wrap:wrap;">
                      <span class="pill">Sakura Heights</span>
                      <span class="pill">Lot 151</span>
                      <span class="pill pill--ok">Active</span>
                    </div>
                  </div>
                </div>
                <hr class="divider" style="margin:14px 0;">
                <div style="font-size:12px;font-weight:500;margin-bottom:6px;">Is this your property?</div>
                <div style="display:flex;gap:8px;">
                  <button class="btn btn--primary" style="flex:1;justify-content:center;" (click)="step = 3">
                    Yes, that's me →
                  </button>
                  <button class="btn btn--ghost" (click)="found.set(false)">Not mine</button>
                </div>
              </div>
            } @else {
              <div class="card card--dashed">
                <div class="section-title">🔒 Owner verification</div>
                <p class="muted" style="font-size:11px;line-height:1.6;">
                  After confirming, we'll match what you enter against the owner on file.
                  If it doesn't match, we can mail a one-time PIN to the property address.
                </p>
              </div>
            }
            <div style="font-size:12px;text-align:center;">
              Already registered? <a class="link" routerLink="/login">Sign in instead</a>
            </div>
          </div>
        }

        <!-- Step 3: Create login -->
        @if (step === 3) {
          <div class="flex-col" style="grid-column:1/-1;max-width:420px;margin:0 auto;width:100%;">
            <h1 class="page-title">Create your <span class="hand">login</span></h1>
            @if (error()) {
              <div class="alert alert--error"><span>⚠</span> {{ error() }}</div>
            }
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
                <div class="field-label">Email</div>
                <input class="field" type="email" placeholder="you@example.com" [(ngModel)]="email" name="email" />
              </div>
              <div style="margin-top:10px;">
                <div class="field-label">Password</div>
                <input class="field" type="password" placeholder="Min. 8 characters"
                       [(ngModel)]="password" name="password" />
              </div>
            </div>
            <button class="btn btn--primary btn--block" style="padding:10px 14px;" (click)="createAccount()">
              @if (loading()) { <span class="spinner"></span> } @else { Create account → }
            </button>
          </div>
        }
      </div>
    </div>
  `
})
export class RegisterComponent {
  private auth   = inject(AuthService);
  private router = inject(Router);

  step       = 1;
  street     = '';
  city       = '';
  state      = '';
  zip        = '';
  accountNum = '';
  firstName  = '';
  lastName   = '';
  email      = '';
  password   = '';

  searching = signal(false);
  found     = signal(false);
  loading   = signal(false);
  error     = signal('');

  findProperty() {
    this.searching.set(true);
    setTimeout(() => { this.searching.set(false); this.found.set(true); }, 700);
  }

  async createAccount() {
    if (!this.email || !this.password || !this.firstName) {
      this.error.set('Please fill in all required fields.');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    await this.auth.register(this.email, this.password, this.firstName, this.lastName);
    this.router.navigate(['/app/dashboard']);
  }
}
