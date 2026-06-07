import type { Meta, StoryObj } from '@storybook/angular';
import { applicationConfig, moduleMetadata } from '@storybook/angular';
import { provideRouter } from '@angular/router';
import { StatementComponent } from './statement.component';
import { PaymentsService, TransactionSummary } from '../../../core/services/payments.service';
import { LedgerEntry } from '../../../core/models';

const LEDGER: LedgerEntry[] = [
  { id: '1', date: '2026-01-01', description: 'Regular Assessment – January 2026', type: 'Regular Assessment', charge: 250, payment: 0,   balance: 250, docNumber: 'RA202601' },
  { id: '2', date: '2026-01-05', description: 'Online Payment – Thank You',         type: 'Payment',           charge: 0,   payment: 250, balance: 0,   docNumber: 'PMT202601' },
  { id: '3', date: '2026-02-01', description: 'Regular Assessment – February 2026', type: 'Regular Assessment', charge: 250, payment: 0,   balance: 250, docNumber: 'RA202602' },
];

// Masked methods only — the story renders against a stubbed PaymentsService, so no raw instrument
// data exists anywhere in the component (SC-001).
const TRANSACTIONS: TransactionSummary[] = [
  { id: 't1', createdAt: '2026-01-05', grossAmount: 250, feeAmount: 5,    total: 255,    cumulativeRefundedAmount: 0,  status: 'Succeeded', paymentMethod: 'card', maskedMethod: 'Visa •••• 4242', isRecurring: false },
  { id: 't2', createdAt: '2026-02-01', grossAmount: 250, feeAmount: 1.95, total: 251.95, cumulativeRefundedAmount: 50, status: 'Refunded',  paymentMethod: 'card', maskedMethod: 'Visa •••• 4242', isRecurring: true },
];

/** PaymentsService stub: ledger renders the statement; getTransactions feeds the Payments tab. */
class StatementPaymentsService {
  getLedger(): Promise<LedgerEntry[]> { return Promise.resolve(LEDGER); }
  getBalance() { return Promise.resolve({ currentBalance: 250, balanceDueDate: '2026-02-01', monthlyAssessment: 250 }); }
  getDrafts() { return Promise.resolve([]); }
  getTransactions(): Promise<TransactionSummary[]> { return Promise.resolve(TRANSACTIONS); }
}

const meta: Meta<StatementComponent> = {
  title: 'Payments/Statement',
  component: StatementComponent,
  decorators: [
    applicationConfig({ providers: [provideRouter([])] }),
    moduleMetadata({ providers: [{ provide: PaymentsService, useClass: StatementPaymentsService }] }),
  ],
};

export default meta;
type Story = StoryObj<StatementComponent>;

/** Default ledger view: every charge and payment posted to the account. */
export const Statement: Story = {};

/** Read-only Stripe transaction history — opened by clicking the Payments tab. */
export const Payments: Story = {
  play: async ({ canvasElement }) => {
    const tab = canvasElement.querySelector<HTMLButtonElement>('[data-testid="tab-payments"]');
    tab?.click();
  },
};
