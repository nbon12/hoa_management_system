import { ChangeDetectionStrategy, Component, ViewChild, input } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { injectStripe, StripePaymentElementComponent } from 'ngx-stripe';
import { environment } from '../../../environments/environment';

/** Outcome of an in-browser Stripe confirmation, normalized for wizard flows. */
export interface StripeConfirmOutcome {
  /** User-presentable error, or null on success. */
  error: string | null;
  /** Stripe object status (`succeeded`, `processing`, …) when confirmation went through. */
  status: string | null;
}

/**
 * Shared Stripe Payment Element host (015 US6, FR-020): owns the Stripe.js instance, the mounted
 * `<ngx-stripe-payment>` element, and the confirm calls — previously duplicated across the
 * one-time and recurring god components. Card/bank details live only in the Stripe-hosted iframe;
 * no PAN/account data ever touches Angular state or our backend (SC-001).
 *
 * Keep this component mounted for the element's whole lifecycle (method → review steps):
 * `confirmPayment`/`confirmSetup` need the same elements instance that rendered the form.
 */
@Component({
  selector: 'app-stripe-elements-host',
  standalone: true,
  imports: [StripePaymentElementComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ngx-stripe-payment [stripe]="stripe" [clientSecret]="clientSecret()" />`,
})
export class StripeElementsHostComponent {
  /** The PaymentIntent/SetupIntent client secret the element mounts against. */
  clientSecret = input.required<string>();

  // A Stripe instance for this host; the publishable key is browser-safe environment config.
  readonly stripe = injectStripe(environment.stripePublishableKey);

  @ViewChild(StripePaymentElementComponent) private paymentElement?: StripePaymentElementComponent;

  /** Confirms a PaymentIntent in-browser (one-time flow). */
  async confirmPayment(): Promise<StripeConfirmOutcome> {
    const elements = this.paymentElement?.elements;
    if (!elements) return { error: 'Payment form is not ready yet.', status: null };

    const { error, paymentIntent } = await firstValueFrom(
      this.stripe.confirmPayment({
        elements,
        clientSecret: this.clientSecret(),
        redirect: 'if_required',
      }),
    );
    if (error) return { error: error.message ?? 'Your payment could not be processed.', status: null };
    return { error: null, status: paymentIntent?.status ?? null };
  }

  /** Confirms a SetupIntent in-browser (vaulting flow for auto-pay). */
  async confirmSetup(): Promise<StripeConfirmOutcome> {
    const elements = this.paymentElement?.elements;
    if (!elements) return { error: 'Payment form is not ready yet.', status: null };

    const { error, setupIntent } = await firstValueFrom(
      this.stripe.confirmSetup({
        elements,
        clientSecret: this.clientSecret(),
        redirect: 'if_required',
      }),
    );
    if (error) return { error: error.message ?? 'Your payment method could not be saved.', status: null };
    return { error: null, status: setupIntent?.status ?? null };
  }
}
