import { Component, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { PaymentsService, PaymentResult } from '../../../core/services/payments.service';
import { MockDataService } from '../../../core/services/mock-data.service';

type AmountPreset = 'current' | 'next' | 'both' | 'custom';
type PayMethod = 'card' | 'ach';
type WizardStep = 1 | 2 | 3 | 4;

@Component({
  selector: 'app-one-time',
  standalone: true,
  imports: [FormsModule, RouterLink, CurrencyPipe],
  template: `
    <div class="page-header">
      <h1 class="page-title">Make a <span class="hand">one-time</span> payment</h1>
    </div>

    <!-- Stepper -->
    <div class="stepper">
      @for (s of steps; track s.n) {
        <span class="pill"
              [style.background]="currentStep() >= s.n ? 'var(--rose)' : ''"
              [style.color]="currentStep() >= s.n ? 'var(--paper)' : ''">
          {{ s.n }} {{ s.label }}
        </span>
        @if (!$last) { <span class="stepper__line"></span> }
      }
    </div>

    <!-- Step 1: Amount -->
    @if (currentStep() === 1) {
      <div class="card" style="padding:24px;">
        <div class="section-title">Step 1 — How much?</div>
        <p class="muted" style="margin-bottom:14px;">Pick a preset or type your own.</p>
        <div class="grid-3" style="gap:10px;">
          @for (preset of presets; track preset.id) {
            <div class="card"
                 style="cursor:pointer;text-align:center;padding:14px 10px;"
                 [style.border-color]="selectedPreset() === preset.id ? 'var(--ink)' : 'var(--line)'"
                 [style.border-style]="selectedPreset() === preset.id ? 'solid' : 'dashed'"
                 [style.background]="selectedPreset() === preset.id ? 'var(--pink)' : 'var(--paper)'"
                 (click)="selectedPreset.set(preset.id)">
              <div class="field-label">{{ preset.label }}</div>
              <div class="mono" style="font-size:22px;margin:4px 0;">{{ preset.amount | currency }}</div>
              <div class="muted" style="font-size:11px;">{{ preset.sub }}</div>
            </div>
          }
        </div>
        <div style="margin-top:14px;">
          <div class="field-label">Or custom amount</div>
          <div style="display:flex;align-items:center;gap:6px;max-width:200px;">
            <span style="font-weight:600;">$</span>
            <input class="field field--dashed mono"
                   type="number" min="0.01" step="0.01"
                   placeholder="0.00"
                   [disabled]="selectedPreset() !== 'custom'"
                   [(ngModel)]="customAmount"
                   (focus)="selectedPreset.set('custom')">
          </div>
        </div>
      </div>
    }

    <!-- Step 2: Method -->
    @if (currentStep() === 2) {
      <div class="card" style="padding:24px;">
        <div class="section-title">Step 2 — Payment method</div>
        <div style="display:flex;gap:10px;margin-top:14px;">
          <div class="card" style="flex:1;cursor:pointer;text-align:center;"
               [style.border-color]="method() === 'card' ? 'var(--ink)' : 'var(--line)'"
               [style.background]="method() === 'card' ? 'var(--pink)' : 'var(--paper)'"
               (click)="method.set('card')">
            <div style="font-size:24px;">💳</div>
            <div style="font-weight:600;margin-top:6px;">Credit card</div>
            <div class="muted" style="font-size:11px;">$1.95 service fee</div>
          </div>
          <div class="card" style="flex:1;cursor:pointer;text-align:center;"
               [style.border-color]="method() === 'ach' ? 'var(--ink)' : 'var(--line)'"
               [style.background]="method() === 'ach' ? 'var(--pink)' : 'var(--paper)'"
               (click)="method.set('ach')">
            <div style="font-size:24px;">🏦</div>
            <div style="font-weight:600;margin-top:6px;">eCheck (ACH)</div>
            <div class="muted" style="font-size:11px;">Free</div>
          </div>
        </div>

        @if (method() === 'card') {
          <div style="display:flex;flex-direction:column;gap:10px;margin-top:16px;">
            <div>
              <div class="field-label">Cardholder name</div>
              <input class="field" placeholder="Nicholas Bonilla" [(ngModel)]="cardName">
            </div>
            <div>
              <div class="field-label">Card number</div>
              <input class="field mono" placeholder="4242 4242 4242 4242" [(ngModel)]="cardNumber" maxlength="19">
            </div>
            <div class="grid-3" style="gap:8px;">
              <div>
                <div class="field-label">Expires</div>
                <input class="field mono" placeholder="MM/YY" [(ngModel)]="cardExpiry" maxlength="5">
              </div>
              <div>
                <div class="field-label">CVC</div>
                <input class="field mono" placeholder="•••" type="password" [(ngModel)]="cardCvc" maxlength="4">
              </div>
              <div>
                <div class="field-label">ZIP</div>
                <input class="field mono" placeholder="27560" [(ngModel)]="cardZip" maxlength="5">
              </div>
            </div>
          </div>
        }
        @if (method() === 'ach') {
          <div style="display:flex;flex-direction:column;gap:10px;margin-top:16px;">
            <div>
              <div class="field-label">Bank name</div>
              <input class="field" placeholder="Fidelity Investments" [(ngModel)]="bankName">
            </div>
            <div class="grid-2" style="gap:8px;">
              <div>
                <div class="field-label">Routing number</div>
                <input class="field mono" placeholder="•••••••••" [(ngModel)]="routing">
              </div>
              <div>
                <div class="field-label">Account number</div>
                <input class="field mono" placeholder="•••••••••••" [(ngModel)]="accountNum">
              </div>
            </div>
          </div>
        }
      </div>
    }

    <!-- Step 3: Review -->
    @if (currentStep() === 3) {
      <div class="card" style="padding:24px;max-width:480px;">
        <div class="section-title">Step 3 — Review & submit</div>
        <div style="margin-top:16px;display:flex;flex-direction:column;gap:8px;font-size:13px;">
          <div style="display:flex;justify-content:space-between;">
            <span>Amount</span>
            <b class="mono">{{ resolvedAmount() | currency }}</b>
          </div>
          <div style="display:flex;justify-content:space-between;">
            <span>Processing fee</span>
            <b class="mono">{{ method() === 'card' ? '$1.95' : 'Free' }}</b>
          </div>
          <hr class="divider">
          <div style="display:flex;justify-content:space-between;">
            <b>Total</b>
            <b class="mono" style="font-size:18px;">{{ totalAmount() | currency }}</b>
          </div>
          <div style="display:flex;justify-content:space-between;color:var(--ink-soft);">
            <span>Method</span>
            <span>{{ method() === 'card' ? '💳 Credit card' : '🏦 eCheck' }}</span>
          </div>
        </div>
        <div class="card card--pink" style="margin-top:14px;font-size:11px;display:flex;gap:8px;">
          <span>ⓘ</span> Posts within 1 business day.
        </div>
        @if (error()) {
          <div class="alert alert--error" style="margin-top:10px;"><span>⚠</span> {{ error() }}</div>
        }
      </div>
    }

    <!-- Step 4: Confirmation -->
    @if (currentStep() === 4 && result()) {
      <div class="card card--lav" style="max-width:480px;text-align:center;padding:32px;">
        <div style="font-size:48px;">✅</div>
        <h2 style="margin-top:12px;font-weight:600;">Payment submitted!</h2>
        <p class="muted" style="margin-top:6px;">Confirmation # <b class="mono">{{ result()!.confirmationNumber }}</b></p>
        <p class="muted">{{ result()!.amount | currency }} — {{ result()!.date }}</p>
        <div style="display:flex;gap:8px;justify-content:center;margin-top:20px;">
          <a routerLink="/app/payments/statement" class="btn">View statement</a>
          <a routerLink="/app/dashboard" class="btn btn--primary">Back to dashboard</a>
        </div>
      </div>
    }

    <!-- Navigation buttons -->
    @if (currentStep() < 4) {
      <div style="display:flex;gap:8px;margin-top:auto;">
        @if (currentStep() > 1) {
          <button class="btn btn--ghost" (click)="back()">← Back</button>
        } @else {
          <a routerLink="/app/payments/statement" class="btn btn--ghost">← Cancel</a>
        }
        <button class="btn btn--primary" style="margin-left:auto;"
                (click)="next()" [disabled]="loading()">
          @if (loading()) { <span class="spinner"></span> }
          @else if (currentStep() === 3) { Submit payment }
          @else { Continue → }
        </button>
      </div>
    }

    <!-- Pay by mail note -->
    <div class="card card--dashed" style="max-width:300px;">
      <div class="section-title">Pay by mail</div>
      <p class="muted" style="font-size:11px;line-height:1.6;">
        Payment Processing Center<br>
        C/O NekoHOA · 8508 Park Rd<br>
        PMB #118 · Charlotte, NC 28210
      </p>
    </div>
  `
})
export class OneTimeComponent {
  private paymentsSvc = inject(PaymentsService);
  private mockData    = inject(MockDataService);

  steps = [
    { n: 1 as WizardStep, label: 'amount' },
    { n: 2 as WizardStep, label: 'method' },
    { n: 3 as WizardStep, label: 'review'  },
  ];

  currentStep = signal<WizardStep>(1);
  selectedPreset = signal<AmountPreset>('current');
  method  = signal<PayMethod>('card');
  loading = signal(false);
  error   = signal('');
  result  = signal<PaymentResult | null>(null);

  customAmount = '';
  cardName  = ''; cardNumber = ''; cardExpiry = ''; cardCvc = ''; cardZip = '';
  bankName  = ''; routing = ''; accountNum = '';

  get balance()     { return this.paymentsSvc.currentBalance; }
  get assessment()  { return this.paymentsSvc.nextAssessment; }

  presets = [
    { id: 'current' as AmountPreset, label: 'Current',   amount: this.paymentsSvc.currentBalance, sub: 'as of today' },
    { id: 'next'    as AmountPreset, label: 'Next due',   amount: this.paymentsSvc.nextAssessment,  sub: 'due 6/1' },
    { id: 'both'    as AmountPreset, label: 'Both',       amount: this.paymentsSvc.currentBalance + this.paymentsSvc.nextAssessment, sub: 'paid through July' },
  ];

  resolvedAmount = computed(() => {
    const p = this.selectedPreset();
    if (p === 'custom') return parseFloat(this.customAmount) || 0;
    return this.presets.find(x => x.id === p)?.amount ?? 0;
  });

  totalAmount = computed(() =>
    this.resolvedAmount() + (this.method() === 'card' ? 1.95 : 0)
  );

  async next() {
    this.error.set('');
    if (this.currentStep() === 3) {
      this.loading.set(true);
      try {
        const r = await this.paymentsSvc.submitPayment(this.totalAmount(), this.method());
        this.result.set(r);
        this.currentStep.set(4);
      } catch {
        this.error.set('Payment failed. Please try again.');
      } finally {
        this.loading.set(false);
      }
    } else {
      this.currentStep.set((this.currentStep() + 1) as WizardStep);
    }
  }

  back() { this.currentStep.set((this.currentStep() - 1) as WizardStep); }
}
