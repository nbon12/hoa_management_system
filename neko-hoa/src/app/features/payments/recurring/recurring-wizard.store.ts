import { signal } from '@angular/core';
import { apiErrorMessage } from '../../../core/api/api-error';
import {
  PaymentsService, RecurringInfo, RecurringSaveRequest, DraftRow,
} from '../../../core/services/payments.service';

export type AmountType = 'assessment' | 'balance' | 'fixed';

// The mandate the resident must affirmatively accept before any method is vaulted / charged off-session.
// Stored verbatim with the authorization (text + version + IP/UA captured server-side) for FR-009.
export const MANDATE_VERSION = '2026-06-v1';
export const MANDATE_TEXT =
  'I authorize NekoHOA to electronically debit the payment method on file for my selected auto-pay ' +
  'amount on the chosen draft day each month, plus any applicable processing fee, until I cancel. ' +
  'I may cancel at any time from this page.';

/**
 * Signal-based state for the auto-pay enrollment flow (015 US6, FR-020): enrollment/draft data,
 * setup-mode lifecycle, mandate/fixed-amount validation, and error state — extracted from the god
 * component so the flow is testable without Stripe Elements or a rendered template. The in-browser
 * Stripe confirmation is injected into {@link save} by the caller.
 */
export class RecurringWizardStore {
  constructor(private payments: PaymentsService) {}

  readonly loading      = signal(true);
  readonly rec          = signal<RecurringInfo | null>(null);
  readonly drafts       = signal<DraftRow[]>([]);
  readonly setupMode    = signal(false);
  readonly amountType   = signal<AmountType>('assessment');
  readonly clientSecret = signal<string | null>(null);
  readonly saving       = signal(false);
  readonly saved        = signal(false);
  readonly error        = signal('');

  fixedAmount = '';
  draftDay = 1;
  mandateAccepted = false;
  private setupIntentId: string | null = null;

  async load(): Promise<void> {
    try {
      const rec = await this.payments.getRecurring();
      this.rec.set(rec);
      if (rec) {
        // Contract type is `string` (generated); the backend only ever emits these three values.
        this.amountType.set(rec.amountType as AmountType);
        this.fixedAmount = rec.fixedAmount != null ? String(rec.fixedAmount) : '';
        this.draftDay = rec.draftDay;
      }
    } catch {
      this.error.set('Could not load your auto-pay settings. Please try again.');
    }
    try {
      this.drafts.set(await this.payments.getDrafts());
    } catch { /* drafts are non-critical for the page to render */ }
    this.loading.set(false);
  }

  /** Reveals the setup form and creates a SetupIntent so the Payment Element can mount. */
  async beginSetup(): Promise<void> {
    this.error.set('');
    this.setupMode.set(true);
    this.clientSecret.set(null);
    this.setupIntentId = null;
    try {
      const setup = await this.payments.createSetupIntent();
      this.setupIntentId = setup.setupIntentId;
      this.clientSecret.set(setup.clientSecret);
    } catch (e) {
      this.error.set(apiErrorMessage(e, 'Could not start setup. Please try again.'));
      this.setupMode.set(false);
    }
  }

  cancelSetup(): void {
    this.setupMode.set(false);
    this.clientSecret.set(null);
    this.setupIntentId = null;
    this.mandateAccepted = false;
  }

  /** Vaults the method (injected in-browser confirm), then persists the enrollment + mandate. */
  async save(confirmInBrowser: () => Promise<{ error: string | null; status: string | null }>): Promise<void> {
    this.error.set('');
    if (!this.clientSecret() || !this.setupIntentId) {
      this.error.set('Payment form is not ready yet.'); return;
    }
    if (!this.mandateAccepted) {
      this.error.set('Please accept the authorization to continue.'); return;
    }
    if (this.amountType() === 'fixed' && !(parseFloat(this.fixedAmount) > 0)) {
      this.error.set('Enter a fixed amount greater than zero.'); return;
    }

    this.saving.set(true);
    try {
      const outcome = await confirmInBrowser();
      if (outcome.error) { this.error.set(outcome.error); return; }
      if (outcome.status !== 'succeeded') {
        this.error.set('Your method could not be saved. Please try again.'); return;
      }

      const req: RecurringSaveRequest = {
        amountType:      this.amountType(),
        fixedAmount:     this.amountType() === 'fixed' ? parseFloat(this.fixedAmount) : null,
        draftDay:        Number(this.draftDay),
        setupIntentId:   this.setupIntentId,
        mandateAccepted: true,
        mandateText:     MANDATE_TEXT,
        mandateVersion:  MANDATE_VERSION,
      };
      this.rec.set(await this.payments.saveRecurring(req));
      this.drafts.set(await this.payments.getDrafts());
      this.setupMode.set(false);
      this.clientSecret.set(null);
      this.saved.set(true);
      setTimeout(() => this.saved.set(false), 3000);
    } catch (e) {
      this.error.set(apiErrorMessage(e, 'Save failed. Please try again.'));
    } finally {
      this.saving.set(false);
    }
  }

  /** Turns auto-pay off (caller confirms with the user first). */
  async turnOff(): Promise<void> {
    this.error.set('');
    try {
      await this.payments.cancelRecurring();
      this.rec.set(await this.payments.getRecurring());
    } catch (e) {
      this.error.set(apiErrorMessage(e, 'Could not turn off auto-pay.'));
    }
  }
}
