import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LedgerEntry, RecurringPayment, DraftEntry } from '../models';

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

interface ApiRecurring {
  status: string;
  amountType: string;
  fixedAmount: number | null;
  method: string;
  draftDay: number;
  routingNumberMasked: string | null;
  accountNumberMasked: string | null;
  accountType: string | null;
}

interface ApiDraft {
  draftDate: string;
  amount: number;
  source: string;
  status: string;
}

interface ApiBalance {
  currentBalance: number;
  balanceDueDate: string;
  monthlyAssessment: number;
}

@Injectable({ providedIn: 'root' })
export class PaymentsService {
  private readonly base = environment.apiBaseUrl;
  private _recurring = signal<RecurringPayment | null>(null);
  readonly recurring = this._recurring.asReadonly();

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

  async getDrafts(): Promise<DraftEntry[]> {
    const items = await firstValueFrom(
      this.http.get<ApiDraft[]>(`${this.base}/payments/drafts`)
    );
    return items.map(d => ({
      date:   d.draftDate.split('T')[0],
      source: d.source,
      amount: d.amount,
      status: d.status as any,
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

  // ── Recurring payment ─────────────────────────────────────────────────────

  async loadRecurring(): Promise<void> {
    try {
      const r = await firstValueFrom(
        this.http.get<ApiRecurring>(`${this.base}/payments/recurring`)
      );
      this._recurring.set(this._mapRecurring(r));
    } catch {
      this._recurring.set(null);
    }
  }

  async saveRecurring(patch: Partial<RecurringPayment>): Promise<void> {
    const body = this._toApiRecurring(patch);
    const r = await firstValueFrom(
      this.http.put<ApiRecurring>(`${this.base}/payments/recurring`, body)
    );
    this._recurring.set(this._mapRecurring(r));
  }

  async cancelRecurring(): Promise<void> {
    await firstValueFrom(this.http.delete(`${this.base}/payments/recurring`));
    if (this._recurring()) {
      this._recurring.set({ ...this._recurring()!, status: 'inactive' });
    }
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

  private _mapRecurring(r: ApiRecurring): RecurringPayment {
    return {
      status:          r.status as any,
      amountType:      r.amountType.toLowerCase() as any,
      fixedAmount:     r.fixedAmount,
      method:          r.method.toLowerCase() as any,
      draftDay:        r.draftDay,
      bankName:        null,
      routingLast4:    r.routingNumberMasked?.slice(-4) ?? null,
      accountLast4:    r.accountNumberMasked?.slice(-4) ?? null,
      accountType:     r.accountType as any,
      cardholderName:  null,
      cardLast4:       null,
      cardExpiry:      null,
      cardZip:         null,
      processingFee:   r.method === 'card' ? 1.95 : 0,
    };
  }

  private _toApiRecurring(p: Partial<RecurringPayment>): Record<string, unknown> {
    return {
      amountType:     p.amountType,
      fixedAmount:    p.fixedAmount,
      method:         p.method,
      draftDay:       p.draftDay,
      routingNumber:  undefined,
      accountNumber:  undefined,
      accountType:    p.accountType,
    };
  }
}
