import { Component, inject, signal, computed, ViewChild, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { injectStripe, StripePaymentElementComponent } from 'ngx-stripe';
import { environment } from '../../../../environments/environment';
import {
  PaymentsService, RecurringInfo, RecurringSaveRequest, DraftRow,
} from '../../../core/services/payments.service';
import { PaymentAlertsComponent } from './alerts/alerts.component';

type AmountType = 'assessment' | 'balance' | 'fixed';

// The mandate the resident must affirmatively accept before any method is vaulted / charged off-session.
// Stored verbatim with the authorization (text + version + IP/UA captured server-side) for FR-009.
const MANDATE_VERSION = '2026-06-v1';
const MANDATE_TEXT =
  'I authorize NekoHOA to electronically debit the payment method on file for my selected auto-pay ' +
  'amount on the chosen draft day each month, plus any applicable processing fee, until I cancel. ' +
  'I may cancel at any time from this page.';

@Component({
  selector: 'app-recurring',
  standalone: true,
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe, StripePaymentElementComponent, PaymentAlertsComponent],
  template: `
    <div class="page-header">
      <h1 class="page-title">Auto-pay</h1>
      <span class="muted" style="margin-left:4px;">set it &amp; forget it</span>
      @if (rec()?.status === 'active') {
        <span class="pill pill--ok" style="margin-left:8px;" data-testid="enrolled-pill"><span aria-hidden="true">✓</span> Enrolled</span>
        <div class="page-header__actions">
          <button class="btn btn--ghost" (click)="confirmCancel()" data-testid="turn-off"><span aria-hidden="true">🗑</span> Turn off</button>
        </div>
      }
    </div>

    @if (error()) {
      <div class="alert alert--error" role="alert" data-testid="error"><span aria-hidden="true">⚠</span> {{ error() }}</div>
    }

    @if (loading()) {
      <div class="card" role="status"><span class="spinner" aria-hidden="true"></span> Loading auto-pay…</div>
    } @else {
      <!-- Current enrollment status -->
      @if (rec(); as r) {
        <div class="card card--lav" data-testid="status-card"
             style="display:grid;grid-template-columns:1fr 1fr 1fr;gap:14px;">
          <div>
            <div class="field-label">Status</div>
            <div style="font-weight:600;" data-testid="status-state">
              {{ r.status === 'active' ? 'Active' : 'Inactive' }}
            </div>
          </div>
          <div>
            <div class="field-label">Next draft</div>
            <div style="font-weight:600;" data-testid="next-draft">
              @if (r.nextDraftDate) {
                {{ r.nextDraftDate | date:'MMM d' }} ·
                <span data-testid="next-draft-amount">{{ r.nextDraftAmount | currency }}</span>
              } @else { — }
            </div>
          </div>
          <div>
            <div class="field-label">Source</div>
            <div style="font-weight:600;" data-testid="masked-method">{{ r.maskedMethod ?? 'Not set' }}</div>
          </div>
        </div>
      }

      <!-- Set up / update -->
      @if (!setupMode()) {
        <div style="margin-top:14px;">
          <button class="btn btn--primary" (click)="beginSetup()" data-testid="setup-toggle">
            {{ rec()?.status === 'active' ? 'Update payment method & settings' : 'Set up auto-pay' }}
          </button>
        </div>
      } @else {
        <div class="grid-2" style="margin-top:14px;">
          <!-- What gets paid -->
          <div class="card">
            <div class="section-title">What gets paid</div>
            <div style="display:flex;flex-direction:column;gap:8px;margin-top:6px;font-size:12px;">
              @for (opt of amountOptions; track opt.id) {
                <div class="card" [attr.data-amount-type]="opt.id"
                     style="padding:10px 12px;cursor:pointer;display:flex;align-items:center;gap:10px;"
                     [style.border-color]="amountType() === opt.id ? 'var(--ink)' : 'var(--line)'"
                     [style.background]="amountType() === opt.id ? 'var(--pink)' : 'var(--paper)'"
                     (click)="amountType.set(opt.id)">
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
                         placeholder="0.00" [(ngModel)]="fixedAmount" data-testid="fixed-amount">
                </div>
              }
            </div>

            <div class="field-label" style="margin-top:14px;">Draft date</div>
            <select class="field field--dashed" [(ngModel)]="draftDay" data-testid="draft-day">
              @for (d of draftDayOptions; track d) {
                <option [value]="d">{{ ordinal(d) }} of each month</option>
              }
            </select>
          </div>

          <!-- Payment method (vaulted via Stripe) -->
          <div class="card">
            <div class="section-title">Payment method</div>
            <p class="muted" style="font-size:11px;line-height:1.6;margin-top:4px;">
              Your card or bank details are entered securely with Stripe and stored on file as a token —
              they never touch NekoHOA's servers.
            </p>
            @if (clientSecret()) {
              <div style="margin-top:10px;">
                <ngx-stripe-payment [stripe]="stripe" [clientSecret]="clientSecret()!" />
              </div>
            } @else {
              <div class="muted" role="status" style="margin-top:12px;display:flex;align-items:center;gap:8px;">
                <span class="spinner" aria-hidden="true"></span> Preparing secure form…
              </div>
            }
          </div>
        </div>

        <!-- Mandate authorization -->
        <div class="card card--dashed">
          <div class="section-title">Authorization</div>
          <p class="muted" style="font-size:11px;line-height:1.6;">{{ mandateText }}</p>
          <label style="display:flex;align-items:center;gap:8px;font-size:12px;margin-top:8px;cursor:pointer;">
            <input type="checkbox" [(ngModel)]="mandateAccepted" data-testid="mandate-checkbox">
            I authorize this recurring payment.
          </label>
        </div>

        <div style="display:flex;gap:8px;align-self:flex-end;">
          <button class="btn btn--ghost" (click)="cancelSetup()">Cancel</button>
          <button class="btn btn--primary" (click)="save()"
                  [disabled]="saving() || !clientSecret() || !mandateAccepted" data-testid="save">
            @if (saving()) { <span class="spinner" aria-hidden="true"></span> } @else { Save auto-pay }
          </button>
        </div>
      }

      @if (saved()) {
        <div class="alert alert--success" role="status" data-testid="saved"><span aria-hidden="true">✓</span> Auto-pay settings saved.</div>
      }

      <!-- Payment alerts opt-in (US3) -->
      <app-payment-alerts />

      <!-- Draft history -->
      <div class="card card--dashed">
        <div style="display:flex;align-items:baseline;">
          <div class="section-title" style="margin:0;">Drafts</div>
          <span class="muted" style="margin-left:8px;font-size:11px;">past &amp; scheduled</span>
        </div>
        <table class="data-table" style="margin-top:10px;" data-testid="drafts-table">
          <caption class="sr-only">Auto-pay draft history: past and scheduled drafts with amount and status</caption>
          <thead>
            <tr><th scope="col">Date</th><th scope="col">Source</th><th scope="col" class="num">Amount</th><th scope="col">Status</th></tr>
          </thead>
          <tbody>
            @for (d of drafts(); track d.id) {
              <tr>
                <td>{{ d.date | date:'MM/dd/yy' }}</td>
                <td>{{ d.source }}</td>
                <td class="num">{{ d.amount | currency }}</td>
                <td><span class="pill" [class.pill--ok]="(d.transactionStatus ?? d.status) === 'Succeeded'">
                  {{ d.transactionStatus ?? d.status }}
                </span></td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="muted">No drafts yet.</td></tr>
            }
          </tbody>
        </table>
      </div>

      <div style="align-self:flex-end;">
        <a routerLink="/app/payments/statement" class="btn btn--ghost">Back to statement</a>
      </div>
    }
  `,
})
export class RecurringComponent implements OnInit {
  private svc = inject(PaymentsService);

  // The publishable key is browser-safe; injectStripe resolves the same Stripe instance the Element mounts on.
  readonly stripe = injectStripe(environment.stripePublishableKey);
  @ViewChild(StripePaymentElementComponent) private paymentElement?: StripePaymentElementComponent;

  readonly mandateText = MANDATE_TEXT;

  loading        = signal(true);
  rec            = signal<RecurringInfo | null>(null);
  drafts         = signal<DraftRow[]>([]);
  setupMode      = signal(false);
  amountType     = signal<AmountType>('assessment');
  clientSecret   = signal<string | null>(null);
  saving         = signal(false);
  saved          = signal(false);
  error          = signal('');

  fixedAmount = '';
  draftDay = 1;
  mandateAccepted = false;
  private setupIntentId: string | null = null;

  readonly amountOptions = [
    { id: 'assessment' as const, label: 'Just the assessment',           sub: 'the monthly dues' },
    { id: 'balance'    as const, label: 'Whatever I owe (open balance)', sub: 'variable each month' },
    { id: 'fixed'      as const, label: 'A fixed amount I pick',         sub: 'same every month' },
  ];
  readonly draftDayOptions = [1, 2, 5, 15, 28];

  async ngOnInit() {
    try {
      const rec = await this.svc.getRecurring();
      this.rec.set(rec);
      if (rec) {
        this.amountType.set(rec.amountType);
        this.fixedAmount = rec.fixedAmount != null ? String(rec.fixedAmount) : '';
        this.draftDay = rec.draftDay;
      }
    } catch {
      this.error.set('Could not load your auto-pay settings. Please try again.');
    }
    try {
      this.drafts.set(await this.svc.getDrafts());
    } catch { /* drafts are non-critical for the page to render */ }
    this.loading.set(false);
  }

  ordinal(d: number): string {
    const s = ['th', 'st', 'nd', 'rd'];
    const v = d % 100;
    return d + (s[(v - 20) % 10] ?? s[v] ?? s[0]);
  }

  /** Reveals the setup form and creates a SetupIntent so the Payment Element can mount. */
  async beginSetup() {
    this.error.set('');
    this.setupMode.set(true);
    this.clientSecret.set(null);
    this.setupIntentId = null;
    try {
      const setup = await this.svc.createSetupIntent();
      this.setupIntentId = setup.setupIntentId;
      this.clientSecret.set(setup.clientSecret);
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'Could not start setup. Please try again.');
      this.setupMode.set(false);
    }
  }

  cancelSetup() {
    this.setupMode.set(false);
    this.clientSecret.set(null);
    this.setupIntentId = null;
    this.mandateAccepted = false;
  }

  /** Vaults the method via Stripe.js confirmSetup, then persists the enrollment + mandate on the backend. */
  async save() {
    this.error.set('');
    const secret = this.clientSecret();
    const element = this.paymentElement?.elements;
    if (!secret || !element || !this.setupIntentId) {
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
      const { error, setupIntent } = await firstValueFrom(
        this.stripe.confirmSetup({ elements: element, clientSecret: secret, redirect: 'if_required' })
      );
      if (error) { this.error.set(error.message ?? 'Your method could not be saved.'); return; }
      if (setupIntent?.status !== 'succeeded') {
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
      this.rec.set(await this.svc.saveRecurring(req));
      this.drafts.set(await this.svc.getDrafts());
      this.setupMode.set(false);
      this.clientSecret.set(null);
      this.saved.set(true);
      setTimeout(() => this.saved.set(false), 3000);
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'Save failed. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }

  async confirmCancel() {
    if (!confirm('Turn off auto-pay? Your next assessment will not be auto-drafted.')) return;
    this.error.set('');
    try {
      await this.svc.cancelRecurring();
      this.rec.set(await this.svc.getRecurring());
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'Could not turn off auto-pay.');
    }
  }
}
