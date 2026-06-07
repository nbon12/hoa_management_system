import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LedgerEntry } from '../models';

/** Fee policy + balances for the one-time payment screen (mirrors backend PaymentOptionsResponse, FR-007). */
export interface PaymentOptions {
  currentBalance: number;
  creditBalance: number;
  nextAssessment: number;
  nextAssessmentDueDate: string | null;
  cardFeeType: string;
  cardFeeValue: number;
  cardScope: string;
  surchargingEnabled: boolean;
  achFeeValue: number;
}

/** Server-authoritative PaymentIntent: the fee/total are computed by the backend FeeCalculator, never client-side. */
export interface PaymentIntentResult {
  paymentIntentId: string;
  clientSecret: string;
  amount: number;
  fee: number;
  total: number;
}

/** Result of confirming a one-time payment against the backend (post Stripe.js confirmPayment). */
export interface ConfirmResult {
  transactionId: string;
  status: string;
  grossAmount: number;
  feeAmount: number;
  total: number;
  maskedMethod: string;
  confirmationNumber: string | null;
  receiptId: string | null;
}

export interface TransactionSummary {
  id: string;
  createdAt: string;
  grossAmount: number;
  feeAmount: number;
  total: number;
  cumulativeRefundedAmount: number;
  status: string;
  paymentMethod: string;
  maskedMethod: string;
  isRecurring: boolean;
}

export interface Receipt {
  id: string;
  transactionId: string;
  confirmationNumber: string;
  maskedMethod: string;
  grossAmount: number;
  feeAmount: number;
  total: number;
  issuedAt: string;
}

/** SetupIntent for vaulting a payment method on file (US2, FR-009); mirrors backend SetupIntentResponse. */
export interface SetupIntentResult {
  setupIntentId: string;
  clientSecret: string;
  publishableKey: string;
}

/** Current auto-pay enrollment (mirrors backend RecurringPaymentDto). No raw instrument data — masked only. */
export interface RecurringInfo {
  id: string;
  amountType: 'assessment' | 'balance' | 'fixed';
  fixedAmount: number | null;
  method: string;
  draftDay: number;
  status: string;
  processingFee: number;
  maskedMethod: string | null;
  nextDraftDate: string | null;
  nextDraftAmount: number | null;
  mandateAcceptedAt: string | null;
}

/**
 * Auto-pay upsert payload (mirrors backend RecurringPaymentRequest). The browser vaults the method via a
 * SetupIntent and submits only its id + an explicit mandate acceptance — no raw card/bank data (SC-001).
 */
export interface RecurringSaveRequest {
  amountType: 'assessment' | 'balance' | 'fixed';
  fixedAmount: number | null;
  draftDay: number;
  setupIntentId: string;
  mandateAccepted: boolean;
  mandateText?: string;
  mandateVersion?: string;
}

/** One scheduled/historical auto-pay draft row (mirrors backend DraftEntryDto). */
export interface DraftRow {
  id: string;
  date: string;
  source: string;
  amount: number;
  status: string;
  transactionStatus: string | null;
}

interface ApiLedgerPage {
  items: ApiLedgerEntry[];
  total: number;
  page: number;
  pageSize: number;
}

interface ApiLedgerEntry {
  id: string;
  entryDate: string;
  description: string;
  chargeAmount: number;
  paymentAmount: number;
  runningBalance: number;
  entryType: string;
}

interface ApiDraftEntry {
  id: string;
  draftDate: string;
  sourceLabel: string;
  amount: number;
  status: string;
  transactionStatus: string | null;
}

interface ApiBalance {
  currentBalance: number;
  balanceDueDate: string;
  monthlyAssessment: number;
}

@Injectable({ providedIn: 'root' })
export class PaymentsService {
  private readonly base = environment.apiBaseUrl;

  constructor(private http: HttpClient) {}

  // ── Ledger ────────────────────────────────────────────────────────────────

  async getLedger(page = 1, pageSize = 100): Promise<LedgerEntry[]> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const res = await firstValueFrom(
      this.http.get<ApiLedgerPage>(`${this.base}/payments/ledger`, { params })
    );
    return res.items.map(e => this._mapLedger(e));
  }

  // ── Balance ───────────────────────────────────────────────────────────────

  async getBalance(): Promise<ApiBalance> {
    const page = await firstValueFrom(
      this.http.get<ApiLedgerPage>(`${this.base}/payments/ledger?page=1&pageSize=1`)
    );
    const latest = page.items[0];
    return {
      currentBalance:    latest?.runningBalance ?? 0,
      balanceDueDate:    '',
      monthlyAssessment: 250,
    };
  }

  // ── Drafts ────────────────────────────────────────────────────────────────

  async getDrafts(limit = 50, offset = 0): Promise<DraftRow[]> {
    const params = new HttpParams().set('limit', limit).set('offset', offset);
    const res = await firstValueFrom(
      this.http.get<{ items: ApiDraftEntry[] }>(`${this.base}/payments/drafts`, { params })
    );
    return res.items.map(d => ({
      id:                d.id,
      date:              d.draftDate,
      source:            d.sourceLabel,
      amount:            d.amount,
      status:            d.status,
      transactionStatus: d.transactionStatus,
    }));
  }

  // ── One-time payment (Stripe Payment Element) ───────────────────────────────

  /** Balance presets + fee policy for the one-time screen. */
  async getPaymentOptions(): Promise<PaymentOptions> {
    return firstValueFrom(this.http.get<PaymentOptions>(`${this.base}/payments/options`));
  }

  /**
   * Creates a Stripe PaymentIntent server-side. The backend computes the fee/total authoritatively
   * and returns the clientSecret the Payment Element mounts against. No raw instrument data is sent.
   */
  async createIntent(amount: number, method: 'card' | 'ach'): Promise<PaymentIntentResult> {
    return firstValueFrom(
      this.http.post<PaymentIntentResult>(`${this.base}/payments/intent`, { amount, method })
    );
  }

  /** Records the payment after Stripe.js has confirmed the intent in-browser; returns the receipt detail. */
  async confirmPayment(paymentIntentId: string): Promise<ConfirmResult> {
    return firstValueFrom(
      this.http.post<ConfirmResult>(`${this.base}/payments/one-time/confirm`, { paymentIntentId })
    );
  }

  async getTransactions(limit = 20, offset = 0): Promise<TransactionSummary[]> {
    const params = new HttpParams().set('limit', limit).set('offset', offset);
    const res = await firstValueFrom(
      this.http.get<{ items: TransactionSummary[] }>(`${this.base}/payments/transactions`, { params })
    );
    return res.items;
  }

  async getReceipt(id: string): Promise<Receipt> {
    return firstValueFrom(this.http.get<Receipt>(`${this.base}/payments/receipts/${id}`));
  }

  // ── Recurring payment (auto-pay via vaulted method) ─────────────────────────

  /**
   * Creates (or reuses) the Stripe customer and a SetupIntent so the Payment Element can vault a
   * method on file. Returns the clientSecret the Element mounts against — no raw instrument data is sent.
   */
  async createSetupIntent(): Promise<SetupIntentResult> {
    return firstValueFrom(
      this.http.post<SetupIntentResult>(`${this.base}/payments/recurring/setup-intent`, {})
    );
  }

  /** Current auto-pay enrollment, or null when the resident has none (backend returns 204). */
  async getRecurring(): Promise<RecurringInfo | null> {
    const r = await firstValueFrom(
      this.http.get<RecurringInfo>(`${this.base}/payments/recurring`, { observe: 'response' })
    );
    return r.status === 204 ? null : r.body;
  }

  /** Enrolls/updates auto-pay against a vaulted method (setupIntentId) with an explicit mandate. */
  async saveRecurring(req: RecurringSaveRequest): Promise<RecurringInfo> {
    return firstValueFrom(
      this.http.put<RecurringInfo>(`${this.base}/payments/recurring`, req)
    );
  }

  /** Disables auto-pay and terminates the stored mandate. */
  async cancelRecurring(): Promise<void> {
    await firstValueFrom(this.http.delete(`${this.base}/payments/recurring`));
  }

  // ── Mappers ───────────────────────────────────────────────────────────────

  private _mapLedger(e: ApiLedgerEntry): LedgerEntry {
    const typeMap: Record<string, string> = {
      RegularAssessment: 'Regular Assessment',
      Payment:           'Payment',
      LateFee:           'Late Fee',
      FinanceCharge:     'Finance Charge',
    };
    return {
      id:          e.id,
      type:        (typeMap[e.entryType] ?? e.entryType) as any,
      date:        e.entryDate,
      docNumber:   '',
      description: e.description,
      charge:      e.chargeAmount || null,
      payment:     e.paymentAmount || null,
      balance:     e.runningBalance,
    };
  }

}
