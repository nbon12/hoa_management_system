import { Component, inject, signal, computed, ViewChild, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { injectStripe, StripePaymentElementComponent } from 'ngx-stripe';
import { environment } from '../../../../environments/environment';
import {
  PaymentsService, PaymentOptions, PaymentIntentResult, ConfirmResult,
} from '../../../core/services/payments.service';

type AmountPreset = 'current' | 'next' | 'both' | 'custom';
type PayMethod = 'card' | 'ach';
type WizardStep = 1 | 2 | 3 | 4;

@Component({
  selector: 'app-one-time',
  standalone: true,
  imports: [FormsModule, RouterLink, CurrencyPipe, StripePaymentElementComponent],
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
          @for (preset of presets(); track preset.id) {
            <div class="card"
                 style="cursor:pointer;text-align:center;padding:14px 10px;"
                 [attr.data-preset]="preset.id"
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

    <!-- Step 2: Method + Stripe Payment Element -->
    @if (currentStep() === 2) {
      <div class="card" style="padding:24px;">
        <div class="section-title">Step 2 — Payment method</div>
        <div style="display:flex;gap:10px;margin-top:14px;">
          <div class="card" style="flex:1;cursor:pointer;text-align:center;"
               [style.border-color]="method() === 'card' ? 'var(--ink)' : 'var(--line)'"
               [style.background]="method() === 'card' ? 'var(--pink)' : 'var(--paper)'"
               (click)="selectMethod('card')">
            <div style="font-size:24px;">💳</div>
            <div style="font-weight:600;margin-top:6px;">Credit card</div>
            <div class="muted" style="font-size:11px;">{{ cardFeeLabel() }}</div>
          </div>
          <div class="card" style="flex:1;cursor:pointer;text-align:center;"
               [style.border-color]="method() === 'ach' ? 'var(--ink)' : 'var(--line)'"
               [style.background]="method() === 'ach' ? 'var(--pink)' : 'var(--paper)'"
               (click)="selectMethod('ach')">
            <div style="font-size:24px;">🏦</div>
            <div style="font-weight:600;margin-top:6px;">eCheck (ACH)</div>
            <div class="muted" style="font-size:11px;">{{ achFeeLabel() }}</div>
          </div>
        </div>

        @if (!clientSecret() && loading()) {
          <div class="muted" style="margin-top:16px;display:flex;align-items:center;gap:8px;">
            <span class="spinner"></span> Preparing secure payment form…
          </div>
        }

        @if (error()) {
          <div class="alert alert--error" style="margin-top:10px;"><span>⚠</span> {{ error() }}</div>
        }
      </div>
    }

    <!-- Stripe Payment Element. Kept mounted across method (2) and review (3): confirmPayment needs
         the same elements instance that rendered the form, so it must outlive the step-2 card. Card
         and bank details live only in the Stripe-hosted iframe — no PAN/account touches Angular
         state or our backend (SC-001). -->
    @if (clientSecret() && (currentStep() === 2 || currentStep() === 3)) {
      <div class="card" style="padding:24px;">
        <div class="section-title">{{ currentStep() === 2 ? 'Card or bank details' : 'Paying with' }}</div>
        <div style="margin-top:14px;">
          <ngx-stripe-payment [stripe]="stripe" [clientSecret]="clientSecret()!" />
        </div>
      </div>
    }

    <!-- Step 3: Review -->
    @if (currentStep() === 3) {
      <div class="card" style="padding:24px;max-width:480px;">
        <div class="section-title">Step 3 — Review & submit</div>
        <div style="margin-top:16px;display:flex;flex-direction:column;gap:8px;font-size:13px;">
          <div style="display:flex;justify-content:space-between;">
            <span>Amount</span>
            <b class="mono" data-testid="summary-amount">{{ intent()?.amount | currency }}</b>
          </div>
          <div style="display:flex;justify-content:space-between;">
            <span>Processing fee</span>
            <b class="mono" data-testid="summary-fee">{{ feeDisplay() }}</b>
          </div>
          <hr class="divider">
          <div style="display:flex;justify-content:space-between;">
            <b>Total</b>
            <b class="mono" style="font-size:18px;" data-testid="summary-total">{{ intent()?.total | currency }}</b>
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
      <div class="card card--lav" style="max-width:480px;text-align:center;padding:32px;" data-testid="receipt">
        <div style="font-size:48px;">✅</div>
        <h2 style="margin-top:12px;font-weight:600;">Payment submitted!</h2>
        <p class="muted" style="margin-top:6px;">Confirmation # <b class="mono" data-testid="confirmation-number">{{ result()!.confirmationNumber }}</b></p>
        <p class="muted">{{ result()!.total | currency }} — {{ result()!.maskedMethod }}</p>
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
                (click)="next()" [disabled]="loading() || (currentStep() === 2 && !clientSecret())">
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
  `,
})
export class OneTimeComponent implements OnInit {
  private paymentsSvc = inject(PaymentsService);

  // A Stripe instance for this component; the publishable key is browser-safe environment config.
  readonly stripe = injectStripe(environment.stripePublishableKey);

  @ViewChild(StripePaymentElementComponent) private paymentElement?: StripePaymentElementComponent;

  steps = [
    { n: 1 as WizardStep, label: 'amount' },
    { n: 2 as WizardStep, label: 'method' },
    { n: 3 as WizardStep, label: 'review'  },
  ];

  currentStep    = signal<WizardStep>(1);
  selectedPreset = signal<AmountPreset>('current');
  method         = signal<PayMethod>('card');
  loading        = signal(false);
  error          = signal('');
  result         = signal<ConfirmResult | null>(null);
  options        = signal<PaymentOptions | null>(null);
  intent         = signal<PaymentIntentResult | null>(null);
  clientSecret   = signal<string | null>(null);

  customAmount = '';

  presets = computed(() => {
    const o = this.options();
    const balance = o?.currentBalance ?? 0;
    const assessment = o?.nextAssessment ?? 0;
    return [
      { id: 'current' as AmountPreset, label: 'Current',  amount: balance,              sub: 'as of today' },
      { id: 'next'    as AmountPreset, label: 'Next due', amount: assessment,           sub: 'due next 1st' },
      { id: 'both'    as AmountPreset, label: 'Both',     amount: balance + assessment, sub: 'current + next' },
    ];
  });

  resolvedAmount = computed(() => {
    const p = this.selectedPreset();
    if (p === 'custom') return parseFloat(this.customAmount) || 0;
    return this.presets().find(x => x.id === p)?.amount ?? 0;
  });

  // Fee labels mirror the backend FeeCalculator policy from /payments/options — never recomputed here.
  cardFeeLabel = computed(() => {
    const o = this.options();
    if (!o || !o.surchargingEnabled) return 'No fee';
    return o.cardFeeType === 'Percentage'
      ? `${(o.cardFeeValue * 100).toFixed(2)}% surcharge`
      : `$${o.cardFeeValue.toFixed(2)} service fee`;
  });
  achFeeLabel = computed(() => {
    const v = this.options()?.achFeeValue ?? 0;
    return v > 0 ? `$${v.toFixed(2)} fee` : 'Free';
  });

  // The authoritative fee is whatever the server returned on the PaymentIntent.
  feeDisplay = computed(() => {
    const fee = this.intent()?.fee ?? 0;
    return fee > 0 ? `$${fee.toFixed(2)}` : 'Free';
  });

  async ngOnInit() {
    try {
      this.options.set(await this.paymentsSvc.getPaymentOptions());
    } catch {
      this.error.set('Could not load your account balance. Please try again.');
    }
  }

  selectMethod(m: PayMethod) {
    if (this.method() === m && this.clientSecret()) return;
    this.method.set(m);
    void this.prepareIntent();
  }

  /** Creates (or recreates) the PaymentIntent for the chosen amount + method and mounts the Element. */
  private async prepareIntent() {
    this.error.set('');
    this.clientSecret.set(null);
    this.intent.set(null);
    this.loading.set(true);
    try {
      const intent = await this.paymentsSvc.createIntent(this.resolvedAmount(), this.method());
      this.intent.set(intent);
      this.clientSecret.set(intent.clientSecret);
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'Could not start the payment. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  async next() {
    this.error.set('');
    if (this.currentStep() === 1) {
      if (this.resolvedAmount() <= 0) { this.error.set('Enter an amount greater than zero.'); return; }
      this.currentStep.set(2);
      await this.prepareIntent();
      return;
    }
    if (this.currentStep() === 2) {
      if (!this.clientSecret()) return;
      this.currentStep.set(3);
      return;
    }
    if (this.currentStep() === 3) {
      await this.submit();
    }
  }

  /** Confirms the intent in-browser via Stripe.js, then records it on the backend → receipt. */
  private async submit() {
    const secret = this.clientSecret();
    const element = this.paymentElement?.elements;
    if (!secret || !element) { this.error.set('Payment form is not ready yet.'); return; }

    this.loading.set(true);
    try {
      const { error, paymentIntent } = await firstValueFrom(
        this.stripe.confirmPayment({
          elements: element,
          clientSecret: secret,
          redirect: 'if_required',
        }),
      );

      if (error) { this.error.set(error.message ?? 'Your payment could not be processed.'); return; }
      if (paymentIntent?.status !== 'succeeded' && paymentIntent?.status !== 'processing') {
        this.error.set('Your payment could not be processed.');
        return;
      }

      const confirmed = await this.paymentsSvc.confirmPayment(this.intent()!.paymentIntentId);
      this.result.set(confirmed);
      this.currentStep.set(4);
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'Payment failed. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  back() {
    const step = this.currentStep();
    if (step > 1) this.currentStep.set((step - 1) as WizardStep);
  }
}
