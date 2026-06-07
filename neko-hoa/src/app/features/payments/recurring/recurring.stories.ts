import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig, moduleMetadata } from '@storybook/angular';
import { provideRouter } from '@angular/router';
import { provideNgxStripe } from 'ngx-stripe';
import { RecurringComponent } from './recurring.component';
import {
  PaymentsService,
  RecurringInfo,
  SetupIntentResult,
  DraftRow,
} from '../../../core/services/payments.service';

// Masked figures only — the story renders against a stubbed PaymentsService so it never hits the
// network and no raw instrument data exists anywhere in the component (SC-001).
const ENROLLED: RecurringInfo = {
  id: 'rec_story',
  amountType: 'assessment',
  fixedAmount: null,
  method: 'card',
  draftDay: 1,
  status: 'active',
  processingFee: 1.95,
  maskedMethod: 'Visa •••• 4242',
  nextDraftDate: '2026-07-01',
  nextDraftAmount: 251.95,
  mandateAcceptedAt: '2026-06-07T00:00:00Z',
};

const SETUP: SetupIntentResult = {
  setupIntentId: 'seti_story_1',
  clientSecret: 'seti_story_1_secret',
  publishableKey: 'pk_test_storybook',
};

const DRAFTS: DraftRow[] = [
  { id: 'd1', date: '2026-06-01', source: 'Visa •••• 4242', amount: 251.95, status: 'Paid', transactionStatus: 'Succeeded' },
  { id: 'd2', date: '2026-07-01', source: 'Visa •••• 4242', amount: 251.95, status: 'Scheduled', transactionStatus: null },
];

/** PaymentsService stub for an enrolled resident: status card + draft history render with no HttpClient. */
class EnrolledPaymentsService {
  getRecurring(): Promise<RecurringInfo | null> {
    return Promise.resolve(ENROLLED);
  }
  getDrafts(): Promise<DraftRow[]> {
    return Promise.resolve(DRAFTS);
  }
  createSetupIntent(): Promise<SetupIntentResult> {
    return Promise.resolve(SETUP);
  }
  saveRecurring(): Promise<RecurringInfo> {
    return Promise.resolve(ENROLLED);
  }
  cancelRecurring(): Promise<void> {
    return Promise.resolve();
  }
  getAlertPreferences() {
    return Promise.resolve({ smsOptIn: false, emailOptIn: false, alertPhone: null });
  }
  saveAlertPreferences(prefs: unknown) {
    return Promise.resolve(prefs);
  }
}

/** Stub for a resident with no enrollment yet — the page shows the "Set up auto-pay" CTA. */
class NotEnrolledPaymentsService extends EnrolledPaymentsService {
  override getRecurring(): Promise<RecurringInfo | null> {
    return Promise.resolve(null);
  }
}

const meta: Meta<RecurringComponent> = {
  title: 'Payments/Recurring',
  component: RecurringComponent,
  decorators: [
    applicationConfig({
      providers: [provideRouter([]), provideNgxStripe('pk_test_storybook')],
    }),
  ],
};

export default meta;
type Story = StoryObj<RecurringComponent>;

/** Active auto-pay: status card with masked method + next draft, plus the drafts history. */
export const Enrolled: Story = {
  decorators: [
    moduleMetadata({
      providers: [{ provide: PaymentsService, useClass: EnrolledPaymentsService }],
    }),
  ],
};

/** No enrollment yet: the "Set up auto-pay" call to action. */
export const NotEnrolled: Story = {
  decorators: [
    moduleMetadata({
      providers: [{ provide: PaymentsService, useClass: NotEnrolledPaymentsService }],
    }),
  ],
};
