import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { PaymentsService } from '../../../core/services/payments.service';
import { StripeElementsHostComponent } from '../../../shared/stripe-elements-host/stripe-elements-host.component';
import { PaymentAlertsComponent } from './alerts/alerts.component';
import { MANDATE_TEXT, RecurringWizardStore } from './recurring-wizard.store';

/**
 * Auto-pay screen (015 US6, FR-020 — decomposed from a 329-line god component): presentation in
 * the template/styles, enrollment/mandate state in {@link RecurringWizardStore}, Stripe Elements
 * handling in the shared {@link StripeElementsHostComponent}. This class only wires them together.
 */
@Component({
  selector: 'app-recurring',
  standalone: true,
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, StripeElementsHostComponent, PaymentAlertsComponent],
  templateUrl: './recurring.component.html',
  styleUrl: './recurring.component.scss',
})
export class RecurringComponent implements OnInit {
  readonly wizard = new RecurringWizardStore(inject(PaymentsService));
  readonly mandateText = MANDATE_TEXT;

  @ViewChild(StripeElementsHostComponent) private stripeHost?: StripeElementsHostComponent;

  readonly amountOptions = [
    { id: 'assessment' as const, label: 'Just the assessment',           sub: 'the monthly dues' },
    { id: 'balance'    as const, label: 'Whatever I owe (open balance)', sub: 'variable each month' },
    { id: 'fixed'      as const, label: 'A fixed amount I pick',         sub: 'same every month' },
  ];
  readonly draftDayOptions = [1, 2, 5, 15, 28];

  async ngOnInit() {
    await this.wizard.load();
  }

  ordinal(d: number): string {
    const s = ['th', 'st', 'nd', 'rd'];
    const v = d % 100;
    return d + (s[(v - 20) % 10] ?? s[v] ?? s[0]);
  }

  async save() {
    await this.wizard.save(() =>
      this.stripeHost?.confirmSetup() ?? Promise.resolve({ error: 'Payment form is not ready yet.', status: null }));
  }

  async confirmCancel() {
    if (!confirm('Turn off auto-pay? Your next assessment will not be auto-drafted.')) return;
    await this.wizard.turnOff();
  }
}
