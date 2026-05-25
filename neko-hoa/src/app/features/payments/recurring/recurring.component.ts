import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { PaymentsService } from '../../../core/services/payments.service';
import { RecurringPayment, DraftEntry } from '../../../core/models';

@Component({
  selector: 'app-recurring',
  standalone: true,
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe],
  template: `
    <!-- Header -->
    <div class="page-header">
      <h1 class="page-title">Auto-pay</h1>
      <span class="muted" style="margin-left:4px;">set it & forget it</span>
      <span class="pill pill--ok" style="margin-left:8px;">✓ Enrolled</span>
      <div class="page-header__actions">
        <button class="btn btn--ghost" (click)="confirmCancel()">🗑 Turn off</button>
      </div>
      <div class="toggle" [class.toggle--on]="active()" (click)="toggleActive()"></div>
    </div>

    <!-- Status bar -->
    <div class="card card--lav" style="display:grid;grid-template-columns:1fr 1fr 1fr;gap:14px;">
      <div>
        <div class="field-label">Status</div>
        <div style="font-weight:600;">{{ active() ? 'Active' : 'Inactive' }}</div>
      </div>
      <div>
        <div class="field-label">Next draft</div>
        <div style="font-weight:600;">Jun 2 · {{ (nextAmount() | currency) }}</div>
      </div>
      <div>
        <div class="field-label">Source</div>
        <div style="font-weight:600;">{{ sourceLabel() }}</div>
      </div>
    </div>

    <div class="grid-2">
      <!-- What gets paid -->
      <div class="card">
        <div class="section-title">What gets paid</div>
        <div style="display:flex;flex-direction:column;gap:8px;margin-top:6px;font-size:12px;">
          @for (opt of amountOptions; track opt.id) {
            <div style="padding:10px 12px;border-radius:10px;border:1.5px solid;cursor:pointer;"
                 [style.border-color]="amountType() === opt.id ? 'var(--ink)' : 'var(--line)'"
                 [style.background]="amountType() === opt.id ? 'var(--pink)' : 'var(--paper)'"
                 (click)="amountType.set(opt.id)"
                 style="display:flex;align-items:center;gap:10px;">
              <span class="radio" [class.radio--on]="amountType() === opt.id"></span>
              <div style="flex:1;">
                <div [style.font-weight]="amountType() === opt.id ? 600 : 400">{{ opt.label }}</div>
                <div class="muted" style="font-size:11px;">{{ opt.sub }}</div>
              </div>
            </div>
          }
          @if (amountType() === 'fixed') {
            <div style="margin-top:4px;">
              <div class="field-label">Fixed amount</div>
              <input class="field mono" type="number" min="1" step="0.01"
                     placeholder="0.00" [(ngModel)]="fixedAmount">
            </div>
          }
        </div>
      </div>

      <!-- When & where -->
      <div class="card">
        <div class="section-title">When &amp; where</div>

        <!-- Method toggle -->
        <div class="field-label" style="margin-top:10px;">How to pay</div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:6px;padding:3px;background:var(--lav);border-radius:10px;border:1.5px solid var(--line);margin-bottom:14px;">
          <button class="btn" style="border:none;border-radius:7px;justify-content:center;"
                  [style.background]="method() === 'ach' ? 'var(--paper)' : 'transparent'"
                  [style.font-weight]="method() === 'ach' ? 600 : 400"
                  (click)="method.set('ach')">
            🏦 Bank (ACH)
          </button>
          <button class="btn" style="border:none;border-radius:7px;justify-content:center;"
                  [style.background]="method() === 'card' ? 'var(--paper)' : 'transparent'"
                  [style.font-weight]="method() === 'card' ? 600 : 400"
                  (click)="method.set('card')">
            💳 Credit card
          </button>
        </div>

        <div class="field-label">Draft date</div>
        <select class="field field--dashed" [(ngModel)]="draftDay">
          <option [value]="1">1st of each month</option>
          <option [value]="2">2nd of each month</option>
          <option [value]="5">5th of each month</option>
          <option [value]="15">15th of each month</option>
        </select>

        <!-- ACH fields -->
        @if (method() === 'ach') {
          <div style="display:flex;flex-direction:column;gap:10px;margin-top:12px;">
            <div>
              <div class="field-label">Bank name</div>
              <input class="field" placeholder="Fidelity Investments" [(ngModel)]="bankName">
            </div>
            <div class="grid-2" style="gap:8px;">
              <div>
                <div class="field-label">Routing #</div>
                <input class="field mono" placeholder="•••••681" [(ngModel)]="routing">
              </div>
              <div>
                <div class="field-label">Account #</div>
                <input class="field mono" placeholder="•••••747" [(ngModel)]="accountNum">
              </div>
            </div>
            <div>
              <div class="field-label">Account type</div>
              <div style="display:flex;gap:14px;font-size:12px;margin-top:4px;">
                <label style="display:flex;align-items:center;gap:6px;cursor:pointer;">
                  <span class="radio" [class.radio--on]="accountType === 'checking'" (click)="accountType='checking'"></span>
                  Checking
                </label>
                <label style="display:flex;align-items:center;gap:6px;cursor:pointer;">
                  <span class="radio" [class.radio--on]="accountType === 'savings'" (click)="accountType='savings'"></span>
                  Savings
                </label>
              </div>
            </div>
            <div class="card card--pink" style="padding:8px 10px;font-size:11px;">
              $1.95 processing fee per draft.
            </div>
          </div>
        }

        <!-- Card fields -->
        @if (method() === 'card') {
          <div style="display:flex;flex-direction:column;gap:10px;margin-top:12px;">
            <div>
              <div class="field-label">Cardholder name</div>
              <input class="field" placeholder="Nicholas Bonilla" [(ngModel)]="cardName">
            </div>
            <div>
              <div class="field-label">Card number</div>
              <input class="field mono" placeholder="4242 4242 4242 ____" [(ngModel)]="cardNumber" maxlength="19">
            </div>
            <div class="grid-3" style="gap:8px;">
              <div>
                <div class="field-label">Expires</div>
                <input class="field mono" placeholder="MM/YY" [(ngModel)]="cardExpiry" maxlength="5">
              </div>
              <div>
                <div class="field-label">CVC</div>
                <input class="field mono" placeholder="•••" type="password" [(ngModel)]="cardCvc" maxlength="4">
              </div>
              <div>
                <div class="field-label">ZIP</div>
                <input class="field mono" placeholder="27560" [(ngModel)]="cardZip" maxlength="5">
              </div>
            </div>
            <div class="card card--pink" style="padding:8px 10px;font-size:11px;">
              3% processing fee per draft · charged at time of payment.
            </div>
          </div>
        }
      </div>
    </div>

    <!-- Draft history -->
    <div class="card card--dashed">
      <div style="display:flex;align-items:baseline;">
        <div class="section-title" style="margin:0;">Drafts</div>
        <span class="muted" style="margin-left:8px;font-size:11px;">past &amp; scheduled</span>
      </div>
      <table class="data-table" style="margin-top:10px;">
        <thead>
          <tr><th>Date</th><th>Source</th><th class="num">Amount</th><th>Status</th></tr>
        </thead>
        <tbody>
          @for (d of drafts(); track d.date) {
            <tr>
              <td>{{ d.date | date:'MM/dd/yy' }}</td>
              <td>{{ d.source }}</td>
              <td class="num">{{ d.amount | currency }}</td>
              <td>
                <span class="pill" [class.pill--ok]="d.status === 'paid'">{{ d.status }}</span>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>

    <!-- ACH authorization -->
    <div class="card card--dashed">
      <div class="section-title">Authorization</div>
      <p class="muted" style="font-size:11px;line-height:1.6;">
        By enrolling, you authorize NekoHOA to withdraw the amount above from the bank on file
        each month, until cancelled. You may cancel at any time from this page.
      </p>
      <label style="display:flex;align-items:center;gap:8px;font-size:12px;margin-top:8px;cursor:pointer;">
        <span class="check" [class.check--on]="agreed" (click)="agreed = !agreed"></span>
        I agree to the ACH agreement
      </label>
    </div>

    <div style="display:flex;gap:8px;align-self:flex-end;">
      <a routerLink="/app/payments/statement" class="btn btn--ghost">Cancel</a>
      <button class="btn btn--primary" (click)="save()" [disabled]="saving()">
        @if (saving()) { <span class="spinner"></span> } @else { Save changes }
      </button>
    </div>

    @if (saved()) {
      <div class="alert alert--success"><span>✓</span> Auto-pay settings saved.</div>
    }
  `
})
export class RecurringComponent implements OnInit {
  private svc = inject(PaymentsService);

  active      = signal(false);
  amountType  = signal<'assessment' | 'balance' | 'fixed'>('assessment');
  method      = signal<'ach' | 'card'>('ach');
  fixedAmount = '';
  draftDay    = 1;
  accountType = 'checking';
  bankName    = '';
  routing     = '';
  accountNum  = '';
  cardName    = '';
  cardNumber  = '';
  cardExpiry  = '';
  cardCvc     = '';
  cardZip     = '';
  agreed      = true;
  saving      = signal(false);
  saved       = signal(false);
  drafts      = signal<DraftEntry[]>([]);

  private _rec: RecurringPayment | null = null;

  amountOptions = [
    { id: 'assessment' as const, label: 'Just the assessment',           sub: '$250/mo' },
    { id: 'balance'    as const, label: 'Whatever I owe (open balance)', sub: 'variable' },
    { id: 'fixed'      as const, label: 'A fixed amount I pick',         sub: '$ ____' },
  ];

  async ngOnInit() {
    await this.svc.loadRecurring();
    this._rec = this.svc.recurring();
    if (this._rec) {
      this.active.set(this._rec.status === 'active');
      this.amountType.set(this._rec.amountType as any);
      this.method.set(this._rec.method as any);
      this.draftDay    = this._rec.draftDay;
      this.accountType = this._rec.accountType ?? 'checking';
    }
    try {
      this.drafts.set(await this.svc.getDrafts());
    } catch { /* ignore */ }
  }

  sourceLabel() {
    const r = this._rec;
    if (!r) return 'Not set';
    if (r.method === 'ach') return `Bank ••${r.accountLast4 ?? '??'}`;
    return r.cardLast4 ? `Card ••${r.cardLast4}` : 'Not set';
  }

  nextAmount() { return (this._rec?.processingFee ?? 0) + 250; }

  toggleActive() { this.active.set(!this.active()); }

  confirmCancel() {
    if (confirm('Turn off auto-pay? Your next assessment will not be auto-drafted.')) {
      this.active.set(false);
      this.svc.cancelRecurring();
    }
  }

  async save() {
    this.saving.set(true);
    try {
      await this.svc.saveRecurring({
        status:     this.active() ? 'active' : 'inactive',
        amountType: this.amountType(),
        method:     this.method(),
        draftDay:   this.draftDay,
      });
      this.saved.set(true);
      setTimeout(() => this.saved.set(false), 3000);
    } catch (e: any) {
      alert(e?.error?.message ?? 'Save failed.');
    } finally {
      this.saving.set(false);
    }
  }
}
