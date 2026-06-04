import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { PaymentsService } from '../../../core/services/payments.service';
import { LedgerEntry } from '../../../core/models';

@Component({
  selector: 'app-statement',
  standalone: true,
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe],
  template: `
    <!-- Header -->
    <div class="page-header">
      <div>
        <h1 class="page-title">Account <span class="hand">statement</span></h1>
        <p class="muted">Every charge and payment posted to your account.</p>
      </div>
      <div class="page-header__actions">
        <button class="btn btn--ghost">⎙ Print</button>
        <a routerLink="/app/payments/recurring" class="btn">Set up recurring</a>
        <a routerLink="/app/payments/one-time" class="btn btn--primary">Make payment</a>
      </div>
    </div>

    <!-- Filter bar -->
    <div class="card card--lav" style="display:flex;align-items:center;gap:12px;flex-wrap:wrap;">
      <div style="display:flex;gap:6px;padding:3px;background:var(--paper);border-radius:999px;border:1.5px solid var(--ink);">
        <button class="tab" [class.tab--active]="activeTab==='statement'" (click)="activeTab='statement'">Statement</button>
        <button class="tab" [class.tab--active]="activeTab==='balance'" (click)="activeTab='balance'">Open balance</button>
      </div>
      <div class="field-label" style="margin:0;">From</div>
      <input class="field field--dashed" type="date" style="width:150px;" [(ngModel)]="startDate">
      <span class="muted">to</span>
      <input class="field field--dashed" type="date" style="width:150px;" [(ngModel)]="endDate">
      <button class="btn" (click)="refresh()">↻ Refresh</button>
      <div style="margin-left:auto;text-align:right;">
        <div class="field-label" style="margin:0;">Balance</div>
        <div class="mono" style="font-size:18px;font-weight:600;color:var(--rose);">{{ balance | currency }}</div>
      </div>
    </div>

    <!-- Summary cards (only in statement view) -->
    @if (activeTab === 'balance') {
      <div class="grid-3">
        <div class="card card--pink">
          <div class="field-label">Total charges</div>
          <div class="mono" style="font-size:22px;">{{ totalCharges | currency }}</div>
        </div>
        <div class="card card--lav">
          <div class="field-label">Total payments</div>
          <div class="mono" style="font-size:22px;">{{ totalPayments | currency }}</div>
        </div>
        <div class="card card--rose">
          <div class="field-label">Outstanding</div>
          <div class="mono" style="font-size:22px;color:var(--rose);">{{ balance | currency }}</div>
        </div>
      </div>
    }

    <!-- Search + filters -->
    <div class="card" style="padding:0;overflow:hidden;">
      <div style="padding:10px 14px;display:flex;gap:8px;align-items:center;border-bottom:1.5px dashed var(--line);">
        <input class="field field--dashed" placeholder="🔍 Search ledger…" style="max-width:220px;" [(ngModel)]="searchTerm">
        <select class="field field--dashed" style="width:120px;" [(ngModel)]="typeFilter">
          <option value="">Type: All</option>
          <option value="Regular Assessment">Assessment</option>
          <option value="Payment">Payment</option>
          <option value="Late Fee">Late Fee</option>
        </select>
      </div>

      <!-- Table -->
      <div class="scroll-area" style="max-height:420px;">
        <table class="data-table">
          <thead>
            <tr>
              <th>Type</th><th>Date</th><th>Doc #</th><th>Description</th>
              <th class="num">Charge</th><th class="num">Payment</th><th class="num">Balance</th>
            </tr>
          </thead>
          <tbody>
            @for (row of filteredEntries(); track row.id) {
              <tr [style.background]="row.balance > 0 && !row.payment ? 'var(--pink)' : ''">
                <td>
                  <span class="pill"
                        [style.background]="row.type === 'Payment' ? 'var(--lav-2)' : 'var(--pink-2)'">
                    {{ row.type === 'Payment' ? 'payment' : 'charge' }}
                  </span>
                </td>
                <td>{{ row.date | date:'MM/dd/yy' }}</td>
                <td class="mono link" style="font-size:11px;">{{ row.docNumber }}</td>
                <td>{{ row.description }}</td>
                <td class="num">{{ row.charge ? (row.charge | currency) : '—' }}</td>
                <td class="num">{{ row.payment ? (row.payment | currency) : '—' }}</td>
                <td class="num" [style.color]="row.balance > 0 ? 'var(--rose)' : ''">
                  {{ row.balance | currency }}
                </td>
              </tr>
            }
            @empty {
              <tr><td colspan="7" style="text-align:center;padding:32px;color:var(--ink-mute);">No entries found.</td></tr>
            }
          </tbody>
        </table>
      </div>

      <!-- Totals footer -->
      <div style="padding:10px 14px;display:flex;gap:14px;justify-content:flex-end;background:var(--lav);border-top:1.5px solid var(--line);">
        <span class="muted" style="font-size:11px;">Total charges</span>
        <b class="mono">{{ totalCharges | currency }}</b>
        <span class="muted" style="font-size:11px;">Total payments</span>
        <b class="mono">{{ totalPayments | currency }}</b>
        <span class="muted" style="font-size:11px;">Balance</span>
        <b class="mono" style="color:var(--rose);">{{ balance | currency }}</b>
      </div>
    </div>
  `
})
export class StatementComponent implements OnInit {
  private svc = inject(PaymentsService);

  activeTab  = 'statement';
  startDate  = '2025-05-04';
  endDate    = '2026-06-01';

  private _entries    = signal<LedgerEntry[]>([]);
  private _balance    = signal(0);
  private _searchTerm = signal('');
  private _typeFilter = signal('');

  get searchTerm()          { return this._searchTerm(); }
  set searchTerm(v: string) { this._searchTerm.set(v); }
  get typeFilter()          { return this._typeFilter(); }
  set typeFilter(v: string) { this._typeFilter.set(v); }

  filteredEntries = computed(() => {
    const term = this._searchTerm();
    const type = this._typeFilter();
    let entries = this._entries();
    if (term)
      entries = entries.filter(e =>
        e.description.toLowerCase().includes(term.toLowerCase()) ||
        e.docNumber.toLowerCase().includes(term.toLowerCase())
      );
    if (type)
      entries = entries.filter(e => e.type === type);
    return entries;
  });

  get balance(): number      { return this._balance(); }
  get totalCharges(): number { return this._entries().reduce((s, e) => s + (e.charge ?? 0), 0); }
  get totalPayments(): number{ return this._entries().reduce((s, e) => s + (e.payment ?? 0), 0); }

  async ngOnInit() {
    const entries = await this.svc.getLedger();
    this._entries.set(entries);
    this._balance.set(entries.at(-1)?.balance ?? 0);
  }

  async refresh() {
    const entries = await this.svc.getLedger();
    this._entries.set(entries);
    this._balance.set(entries.at(-1)?.balance ?? 0);
  }
}
