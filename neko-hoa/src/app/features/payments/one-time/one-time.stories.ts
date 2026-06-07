import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig, moduleMetadata } from '@storybook/angular';
import { provideRouter } from '@angular/router';
import { provideNgxStripe } from 'ngx-stripe';
import { OneTimeComponent } from './one-time.component';
import {
  PaymentsService,
  PaymentOptions,
  PaymentIntentResult,
  ConfirmResult,
} from '../../../core/services/payments.service';

// Server-authoritative figures (fee/total come straight from the backend FeeCalculator, never recomputed
// client-side). The story renders the wizard against a stubbed PaymentsService so it never hits the network.
const OPTIONS: PaymentOptions = {
  currentBalance: 300,
  creditBalance: 0,
  nextAssessment: 250,
  nextAssessmentDueDate: '2026-07-01',
  cardFeeType: 'Flat',
  cardFeeValue: 1.95,
  cardScope: 'All',
  surchargingEnabled: true,
  achFeeValue: 0,
};

const INTENT: PaymentIntentResult = {
  paymentIntentId: 'pi_story_123',
  clientSecret: 'pi_story_123_secret',
  amount: 300,
  fee: 1.95,
  total: 301.95,
};

const CONFIRMED: ConfirmResult = {
  transactionId: 't_story',
  status: 'Succeeded',
  grossAmount: 300,
  feeAmount: 1.95,
  total: 301.95,
  maskedMethod: 'Visa •••• 4242',
  confirmationNumber: 'NEKO-STORY1',
  receiptId: 'r_story',
};

/** PaymentsService stub: resolves the one-time flow with fixed figures, no HttpClient required. */
class MockPaymentsService {
  getPaymentOptions(): Promise<PaymentOptions> {
    return Promise.resolve(OPTIONS);
  }
  createIntent(): Promise<PaymentIntentResult> {
    return Promise.resolve(INTENT);
  }
  confirmPayment(): Promise<ConfirmResult> {
    return Promise.resolve(CONFIRMED);
  }
}

const meta: Meta<OneTimeComponent> = {
  title: 'Payments/OneTime',
  component: OneTimeComponent,
  decorators: [
    moduleMetadata({
      providers: [{ provide: PaymentsService, useClass: MockPaymentsService }],
    }),
    applicationConfig({
      providers: [provideRouter([]), provideNgxStripe('pk_test_storybook')],
    }),
  ],
};

export default meta;
type Story = StoryObj<OneTimeComponent>;

/** Step 1 of the wizard: amount presets sourced from the mocked payment options. */
export const AmountStep: Story = {};
