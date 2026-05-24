import { Injectable, signal } from '@angular/core';
import { MockDataService } from './mock-data.service';
import { RecurringPayment } from '../models';

export interface PaymentResult { confirmationNumber: string; amount: number; date: string; }

@Injectable({ providedIn: 'root' })
export class PaymentsService {
  private _recurring = signal<RecurringPayment>(this.mock.recurringPayment);
  readonly recurring = this._recurring.asReadonly();

  constructor(private mock: MockDataService) {}

  getLedger(startDate?: string, endDate?: string) {
    let entries = [...this.mock.ledger];
    if (startDate) entries = entries.filter(e => e.date >= startDate);
    if (endDate)   entries = entries.filter(e => e.date <= endDate);
    return entries;
  }

  getDrafts() { return [...this.mock.drafts]; }

  get currentBalance() { return this.mock.currentBalance; }
  get nextAssessment()  { return this.mock.property.monthlyAssessment; }
  get processingFee()   { return this.recurring().processingFee; }

  submitPayment(amount: number, method: 'card' | 'ach'): Promise<PaymentResult> {
    return new Promise(resolve =>
      setTimeout(() => resolve({
        confirmationNumber: Math.random().toString(36).slice(2, 10).toUpperCase(),
        amount,
        date: new Date().toISOString().split('T')[0],
      }), 900)
    );
  }

  saveRecurring(patch: Partial<RecurringPayment>): Promise<void> {
    return new Promise(resolve =>
      setTimeout(() => {
        this._recurring.set({ ...this._recurring(), ...patch });
        resolve();
      }, 600)
    );
  }

  cancelRecurring(): Promise<void> {
    return new Promise(resolve =>
      setTimeout(() => {
        this._recurring.set({ ...this._recurring(), status: 'inactive' });
        resolve();
      }, 400)
    );
  }
}
