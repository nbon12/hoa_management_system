import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { PaymentsService } from '../../../core/services/payments.service';
import { ErrorBannerComponent } from '../../../shared/error-banner/error-banner.component';
import { StripeElementsHostComponent } from '../../../shared/stripe-elements-host/stripe-elements-host.component';
import { OneTimeWizardStore, WizardStep } from './one-time-wizard.store';

/**
 * One-time payment screen (015 US6, FR-020 — decomposed from a 341-line god component):
 * presentation lives in the template/styles, wizard/form state in {@link OneTimeWizardStore},
 * Stripe Elements handling in the shared {@link StripeElementsHostComponent}, and API
 * orchestration in the store via PaymentsService. This class only wires the three together.
 */
@Component({
  selector: 'app-one-time',
  standalone: true,
  imports: [FormsModule, RouterLink, CurrencyPipe, ErrorBannerComponent, StripeElementsHostComponent],
  templateUrl: './one-time.component.html',
  styleUrl: './one-time.component.scss',
})
export class OneTimeComponent implements OnInit {
  readonly wizard = new OneTimeWizardStore(inject(PaymentsService));

  @ViewChild(StripeElementsHostComponent) private stripeHost?: StripeElementsHostComponent;

  steps = [
    { n: 1 as WizardStep, label: 'amount' },
    { n: 2 as WizardStep, label: 'method' },
    { n: 3 as WizardStep, label: 'review'  },
  ];

  async ngOnInit() {
    await this.wizard.load();
  }

  async next() {
    await this.wizard.next(() =>
      this.stripeHost?.confirmPayment() ?? Promise.resolve({ error: 'Payment form is not ready yet.', status: null }));
  }
}
