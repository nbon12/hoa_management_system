import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PaymentsService, AlertPreferences } from '../../../../core/services/payments.service';

// E.164 mirror of the backend validator (UpdateAlertPreferencesValidator): leading '+', country digit
// 1–9, up to 15 digits total. We guard client-side so the common case never round-trips a 422.
const E164 = /^\+[1-9]\d{1,14}$/;

const SMS_CONSENT =
  'I agree to receive SMS payment alerts from NekoHOA. Msg & data rates may apply. Reply STOP to opt out.';

/**
 * "Payment alerts" opt-in section (US3 / T080). Residents opt in to SMS/email notifications for
 * failed auto-pay drafts and ACH returns. Alerts default OFF (TCPA-safe); enabling SMS requires a
 * phone number in E.164 form, and every change is recorded as an immutable consent row server-side.
 */
@Component({
  selector: 'app-payment-alerts',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="card card--dashed" data-testid="alerts-section">
      <div class="section-title">Payment alerts</div>
      <p class="muted" style="font-size:11px;line-height:1.6;margin-top:4px;">
        Get notified if an auto-pay draft fails or a bank payment is returned. Off by default — choose
        the channels you want.
      </p>

      @if (loading()) {
        <div class="muted" role="status" style="margin-top:12px;display:flex;align-items:center;gap:8px;">
          <span class="spinner" aria-hidden="true"></span> Loading alert settings…
        </div>
      } @else {
        @if (error()) {
          <div class="alert alert--error" role="alert" data-testid="alerts-error"><span aria-hidden="true">⚠</span> {{ error() }}</div>
        }

        <div style="display:flex;flex-direction:column;gap:10px;margin-top:10px;">
          <label style="display:flex;align-items:center;gap:8px;font-size:12px;cursor:pointer;">
            <input type="checkbox" [(ngModel)]="smsOptIn" data-testid="sms-toggle">
            Text me (SMS)
          </label>

          @if (smsOptIn) {
            <div style="margin-left:24px;">
              <label class="field-label" for="alert-phone-input">Mobile number</label>
              <input id="alert-phone-input" class="field mono" type="tel" placeholder="+19195551234"
                     aria-describedby="alert-sms-consent"
                     [(ngModel)]="alertPhone" data-testid="alert-phone">
              <div id="alert-sms-consent" class="muted" style="font-size:10px;line-height:1.5;margin-top:6px;">{{ smsConsent }}</div>
            </div>
          }

          <label style="display:flex;align-items:center;gap:8px;font-size:12px;cursor:pointer;">
            <input type="checkbox" [(ngModel)]="emailOptIn" data-testid="email-toggle">
            Email me
          </label>
        </div>

        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:12px;">
          <button class="btn btn--primary" (click)="save()" [disabled]="saving()" data-testid="alerts-save">
            @if (saving()) { <span class="spinner" aria-hidden="true"></span> } @else { Save alert settings }
          </button>
        </div>

        @if (saved()) {
          <div class="alert alert--success" role="status" data-testid="alerts-saved"><span aria-hidden="true">✓</span> Alert settings saved.</div>
        }
      }
    </div>
  `,
})
export class PaymentAlertsComponent implements OnInit {
  private svc = inject(PaymentsService);

  readonly smsConsent = SMS_CONSENT;

  loading = signal(true);
  saving  = signal(false);
  saved   = signal(false);
  error   = signal('');

  smsOptIn = false;
  emailOptIn = false;
  alertPhone = '';

  async ngOnInit() {
    try {
      const prefs = await this.svc.getAlertPreferences();
      this.smsOptIn = prefs.smsOptIn;
      this.emailOptIn = prefs.emailOptIn;
      this.alertPhone = prefs.alertPhone ?? '';
    } catch {
      this.error.set('Could not load your alert settings. Please try again.');
    }
    this.loading.set(false);
  }

  async save() {
    this.error.set('');
    const phone = this.alertPhone.trim();
    if (this.smsOptIn && !phone) {
      this.error.set('A mobile number is required to enable SMS alerts.'); return;
    }
    if (this.smsOptIn && !E164.test(phone)) {
      this.error.set('Enter your number in international format, e.g. +19195551234.'); return;
    }

    this.saving.set(true);
    try {
      const prefs: AlertPreferences = {
        smsOptIn:   this.smsOptIn,
        emailOptIn: this.emailOptIn,
        // The phone is only relevant while SMS is on; drop it on opt-out so we don't resend a stale value.
        alertPhone: this.smsOptIn ? (phone || null) : null,
      };
      const saved = await this.svc.saveAlertPreferences(prefs);
      this.smsOptIn = saved.smsOptIn;
      this.emailOptIn = saved.emailOptIn;
      this.alertPhone = saved.alertPhone ?? '';
      this.saved.set(true);
      setTimeout(() => this.saved.set(false), 3000);
    } catch (e: any) {
      this.error.set(e?.error?.message ?? 'Could not save your alert settings. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }
}
