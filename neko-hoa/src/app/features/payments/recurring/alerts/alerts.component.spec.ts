import { render, screen } from '@testing-library/angular';
import { PaymentAlertsComponent } from './alerts.component';
import { PaymentsService, AlertPreferences } from '../../../../core/services/payments.service';

const OFF: AlertPreferences = { smsOptIn: false, emailOptIn: false, alertPhone: null };
const ENROLLED_SMS: AlertPreferences = { smsOptIn: true, emailOptIn: true, alertPhone: '+19195551234' };

function mockPaymentsService(prefs: AlertPreferences = OFF): Partial<PaymentsService> {
  return {
    getAlertPreferences:  jasmine.createSpy('getAlertPreferences').and.resolveTo(prefs),
    saveAlertPreferences: jasmine.createSpy('saveAlertPreferences')
      .and.callFake((p: AlertPreferences) => Promise.resolve(p)),
  };
}

async function renderComponent(prefs: AlertPreferences = OFF) {
  const svc = mockPaymentsService(prefs);
  const view = await render(PaymentAlertsComponent, {
    providers: [{ provide: PaymentsService, useValue: svc }],
  });
  await view.fixture.whenStable();
  view.fixture.detectChanges();
  return { ...view, svc };
}

describe('PaymentAlertsComponent', () => {
  it('loads and reflects the current opt-in matrix (off by default)', async () => {
    const { fixture } = await renderComponent(OFF);
    const comp = fixture.componentInstance;
    expect(comp.smsOptIn).toBeFalse();
    expect(comp.emailOptIn).toBeFalse();
    expect(screen.getByTestId('sms-toggle')).toBeTruthy();
    expect(screen.getByTestId('email-toggle')).toBeTruthy();
  });

  it('reflects an existing enrollment with the phone on file', async () => {
    const { fixture } = await renderComponent(ENROLLED_SMS);
    const comp = fixture.componentInstance;
    expect(comp.smsOptIn).toBeTrue();
    expect(comp.emailOptIn).toBeTrue();
    expect(comp.alertPhone).toBe('+19195551234');
    // Phone field is revealed when SMS is on.
    expect(screen.getByTestId('alert-phone')).toBeTruthy();
  });

  it('blocks SMS opt-in without a phone number (TCPA / FR-013)', async () => {
    const { fixture, svc } = await renderComponent(OFF);
    const comp = fixture.componentInstance;
    comp.smsOptIn = true;
    comp.alertPhone = '';

    await comp.save();

    expect(svc.saveAlertPreferences).not.toHaveBeenCalled();
    expect(comp.error()).toContain('mobile number is required');
  });

  it('rejects a non-E.164 phone number', async () => {
    const { fixture, svc } = await renderComponent(OFF);
    const comp = fixture.componentInstance;
    comp.smsOptIn = true;
    comp.alertPhone = '919-555-1234';

    await comp.save();

    expect(svc.saveAlertPreferences).not.toHaveBeenCalled();
    expect(comp.error()).toContain('international format');
  });

  it('saves the opt-in matrix with a normalized phone payload', async () => {
    const { fixture, svc } = await renderComponent(OFF);
    const comp = fixture.componentInstance;
    comp.smsOptIn = true;
    comp.emailOptIn = true;
    comp.alertPhone = '  +19195551234  ';

    await comp.save();

    expect(svc.saveAlertPreferences).toHaveBeenCalled();
    const sent = (svc.saveAlertPreferences as jasmine.Spy).calls.mostRecent().args[0];
    expect(sent).toEqual({ smsOptIn: true, emailOptIn: true, alertPhone: '+19195551234' });
    expect(comp.saved()).toBeTrue();
  });

  it('opts out by sending an all-false matrix and a null phone', async () => {
    const { fixture, svc } = await renderComponent(ENROLLED_SMS);
    const comp = fixture.componentInstance;
    comp.smsOptIn = false;
    comp.emailOptIn = false;
    comp.alertPhone = '';

    await comp.save();

    const sent = (svc.saveAlertPreferences as jasmine.Spy).calls.mostRecent().args[0];
    expect(sent).toEqual({ smsOptIn: false, emailOptIn: false, alertPhone: null });
  });

  it('surfaces a server error without throwing', async () => {
    const { fixture, svc } = await renderComponent(OFF);
    const comp = fixture.componentInstance;
    (svc.saveAlertPreferences as jasmine.Spy).and.rejectWith({ error: { message: 'Phone already in use.' } });
    comp.emailOptIn = true;

    await comp.save();

    expect(comp.error()).toContain('Phone already in use.');
    expect(comp.saving()).toBeFalse();
  });

  describe('accessibility (T088 / WCAG 2.1 AA)', () => {
    it('associates the SMS phone field with a label and its consent helper text', async () => {
      const { fixture } = await renderComponent(ENROLLED_SMS);
      const el = fixture.nativeElement as HTMLElement;

      const input = el.querySelector('#alert-phone-input') as HTMLInputElement;
      expect(input).toBeTruthy();
      // A <label for> must point at the input's id, and aria-describedby at the consent copy.
      expect(el.querySelector('label[for="alert-phone-input"]')).toBeTruthy();
      expect(input.getAttribute('aria-describedby')).toBe('alert-sms-consent');
      expect(el.querySelector('#alert-sms-consent')?.textContent).toContain('SMS payment alerts');
    });

    it('announces a validation error via role="alert"', async () => {
      const { fixture } = await renderComponent(OFF);
      const comp = fixture.componentInstance;
      comp.smsOptIn = true;
      comp.alertPhone = '';
      await comp.save();
      fixture.detectChanges();

      const alert = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="alerts-error"]');
      expect(alert?.getAttribute('role')).toBe('alert');
    });

    it('announces the saved confirmation via a live status region', async () => {
      const { fixture } = await renderComponent(OFF);
      const comp = fixture.componentInstance;
      comp.emailOptIn = true;
      await comp.save();
      fixture.detectChanges();

      const saved = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="alerts-saved"]');
      expect(saved?.getAttribute('role')).toBe('status');
    });
  });
});
