import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LedgerEntry } from '../models';
import type { components } from '../api/generated-types';

// ─── Contract types (015 US4, FR-011/FR-012) ─────────────────────────────────
// Request/response shapes are the GENERATED contract types — the single client-side source of
// truth, regenerated with `npm run generate:api-types` (see core/api/README.md). Only app-internal
// view-model shapes are declared locally.
type Schemas = components['schemas'];

/** Fee policy + balances for the one-time payment screen (backend PaymentOptionsResponse, FR-007). */
export type PaymentOptions = Schemas['PaymentOptionsResponse'];

/** Server-authoritative PaymentIntent: the fee/total are computed by the backend FeeCalculator, never client-side. */
export type PaymentIntentResult = Schemas['CreateIntentResponse'];

/** Result of confirming a one-time payment against the backend (post Stripe.js confirmPayment). */
export type ConfirmResult = Schemas['ConfirmPaymentResponse'];

export type TransactionSummary = Schemas['TransactionDto'];

export type Receipt = Schemas['ReceiptResponse'];

/** SetupIntent for vaulting a payment method on file (US2, FR-009); backend SetupIntentResponse. */
export type SetupIntentResult = Schemas['SetupIntentResponse'];

/** Current auto-pay enrollment (backend RecurringPaymentDto). No raw instrument data — masked only. */
export type RecurringInfo = Schemas['RecurringPaymentDto'];

/**
 * Auto-pay upsert payload (backend RecurringPaymentRequest). The browser vaults the method via a
 * SetupIntent and submits only its id + an explicit mandate acceptance — no raw card/bank data (SC-001).
 */
export type RecurringSaveRequest = Schemas['RecurringPaymentRequest'];

/**
 * Payment-alert opt-in matrix for the signed-in owner (backend AlertPreferencesDto, FR-013/FR-031).
 * Alerts default OFF (TCPA-safe). SMS requires a phone number on file in E.164 form.
 */
export type AlertPreferences = Schemas['AlertPreferencesDto'];

/** One scheduled/historical auto-pay draft row — app view-model mapped from DraftEntryDto. */
export interface DraftRow {
  id: string;
  date: string;
  source: string;
  amount: number;
  status: string;
  transactionStatus: string | null;
}

/** App-internal derivation (latest running balance), not a backend contract shape. */
export interface BalanceSummary {
  currentBalance: number;
  balanceDueDate: string;
  monthlyAssessment: number;
}

type ApiLedgerPage = Schemas['LedgerResponse'];
type ApiLedgerEntry = Schemas['LedgerItemDto'];
type ApiDraftEntry = Schemas['DraftEntryDto'];

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

  async getBalance(): Promise<BalanceSummary> {
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
      transactionStatus: d.transactionStatus ?? null,
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

  // ── Payment alerts (opt-in) ─────────────────────────────────────────────────

  /** Current SMS/email opt-in flags + alert phone for the signed-in owner. */
  async getAlertPreferences(): Promise<AlertPreferences> {
    return firstValueFrom(
      this.http.get<AlertPreferences>(`${this.base}/payments/alert-preferences`)
    );
  }

  /** Updates the opt-in matrix; the backend appends an immutable TCPA consent row per changed channel. */
  async saveAlertPreferences(prefs: AlertPreferences): Promise<AlertPreferences> {
    return firstValueFrom(
      this.http.put<AlertPreferences>(`${this.base}/payments/alert-preferences`, prefs)
    );
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
