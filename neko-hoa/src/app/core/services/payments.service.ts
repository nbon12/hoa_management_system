import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LedgerEntry, RecurringPayment, DraftEntry } from '../models';

export interface PaymentResult { confirmationNumber: string; amount: number; date: string; }

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

  // ── One-time payment ──────────────────────────────────────────────────────

  async submitPayment(amount: number, method: 'card' | 'ach', details?: Record<string, string>): Promise<PaymentResult> {
    const body = { amount, method, ...details };
    const res = await firstValueFrom(
      this.http.post<{ confirmationNumber: string; amount: number; processedAt: string }>(
        `${this.base}/payments/one-time`, body
      )
    );
    return {
      confirmationNumber: res.confirmationNumber,
      amount:             res.amount,
      date:               res.processedAt?.split('T')[0] ?? new Date().toISOString().split('T')[0],
    };
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
