import { computed, signal } from '@angular/core';
import { apiErrorMessage } from '../../../core/api/api-error';
import {
  PaymentsService, PaymentOptions, PaymentIntentResult, ConfirmResult,
} from '../../../core/services/payments.service';

export type AmountPreset = 'current' | 'next' | 'both' | 'custom';
export type PayMethod = 'card' | 'ach';
export type WizardStep = 1 | 2 | 3 | 4;

/**
 * Signal-based state machine for the one-time payment wizard (015 US6, FR-020): amount selection,
 * intent lifecycle, step guards, and error state — extracted from the god component so the flow is
 * testable without Stripe Elements or a rendered template. Presentation stays in the component;
 * the in-browser Stripe confirmation is injected into {@link submit} by the caller.
 */
export class OneTimeWizardStore {
  constructor(private payments: PaymentsService) {}

  readonly currentStep    = signal<WizardStep>(1);
  readonly selectedPreset = signal<AmountPreset>('current');
  readonly method         = signal<PayMethod>('card');
  readonly loading        = signal(false);
  readonly error          = signal('');
  readonly result         = signal<ConfirmResult | null>(null);
  readonly options        = signal<PaymentOptions | null>(null);
  readonly intent         = signal<PaymentIntentResult | null>(null);
  readonly clientSecret   = signal<string | null>(null);

  customAmount = '';

  readonly presets = computed(() => {
    const o = this.options();
    const balance = o?.currentBalance ?? 0;
    const assessment = o?.nextAssessment ?? 0;
    return [
      { id: 'current' as AmountPreset, label: 'Current',  amount: balance,              sub: 'as of today' },
      { id: 'next'    as AmountPreset, label: 'Next due', amount: assessment,           sub: 'due next 1st' },
      { id: 'both'    as AmountPreset, label: 'Both',     amount: balance + assessment, sub: 'current + next' },
    ];
  });

  readonly resolvedAmount = computed(() => {
    const p = this.selectedPreset();
    if (p === 'custom') return parseFloat(this.customAmount) || 0;
    return this.presets().find(x => x.id === p)?.amount ?? 0;
  });

  // Fee labels mirror the backend FeeCalculator policy from /payments/options — never recomputed here.
  readonly cardFeeLabel = computed(() => {
    const o = this.options();
    if (!o || !o.surchargingEnabled) return 'No fee';
    return o.cardFeeType === 'Percentage'
      ? `${(o.cardFeeValue * 100).toFixed(2)}% surcharge`
      : `$${o.cardFeeValue.toFixed(2)} service fee`;
  });

  readonly achFeeLabel = computed(() => {
    const v = this.options()?.achFeeValue ?? 0;
    return v > 0 ? `$${v.toFixed(2)} fee` : 'Free';
  });

  // The authoritative fee is whatever the server returned on the PaymentIntent.
  readonly feeDisplay = computed(() => {
    const fee = this.intent()?.fee ?? 0;
    return fee > 0 ? `$${fee.toFixed(2)}` : 'Free';
  });

  async load(): Promise<void> {
    try {
      this.options.set(await this.payments.getPaymentOptions());
    } catch {
      this.error.set('Could not load your account balance. Please try again.');
    }
  }

  async selectMethod(m: PayMethod): Promise<void> {
    if (this.method() === m && this.clientSecret()) return;
    this.method.set(m);
    await this.prepareIntent();
  }

  /** Creates (or recreates) the PaymentIntent for the chosen amount + method. */
  async prepareIntent(): Promise<void> {
    this.error.set('');
    this.clientSecret.set(null);
    this.intent.set(null);
    this.loading.set(true);
    try {
      const intent = await this.payments.createIntent(this.resolvedAmount(), this.method());
      this.intent.set(intent);
      this.clientSecret.set(intent.clientSecret);
    } catch (e) {
      this.error.set(apiErrorMessage(e, 'Could not start the payment. Please try again.'));
    } finally {
      this.loading.set(false);
    }
  }

  /** Advances the wizard, enforcing the per-step guards. Returns true when a step changed. */
  async next(confirmInBrowser: () => Promise<{ error: string | null; status: string | null }>): Promise<void> {
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
      await this.submit(confirmInBrowser);
    }
  }

  back(): void {
    const step = this.currentStep();
    if (step > 1) this.currentStep.set((step - 1) as WizardStep);
  }

  /** Confirms in-browser (injected), then records the payment on the backend → receipt. */
  private async submit(confirmInBrowser: () => Promise<{ error: string | null; status: string | null }>): Promise<void> {
    if (!this.clientSecret()) { this.error.set('Payment form is not ready yet.'); return; }

    this.loading.set(true);
    try {
      const outcome = await confirmInBrowser();
      if (outcome.error) { this.error.set(outcome.error); return; }
      if (outcome.status !== 'succeeded' && outcome.status !== 'processing') {
        this.error.set('Your payment could not be processed.');
        return;
      }

      const confirmed = await this.payments.confirmPayment(this.intent()!.paymentIntentId);
      this.result.set(confirmed);
      this.currentStep.set(4);
    } catch (e) {
      this.error.set(apiErrorMessage(e, 'Payment failed. Please try again.'));
    } finally {
      this.loading.set(false);
    }
  }
}
