import { render, screen } from '@testing-library/angular';
import { provideRouter } from '@angular/router';
import { provideNgxStripe } from 'ngx-stripe';
import { of } from 'rxjs';
import { OneTimeComponent } from './one-time.component';
import {
  PaymentsService, PaymentOptions, PaymentIntentResult, ConfirmResult,
} from '../../../core/services/payments.service';

const OPTIONS: PaymentOptions = {
  currentBalance: 300, creditBalance: 0, nextAssessment: 250, nextAssessmentDueDate: '2026-07-01',
  cardFeeType: 'Flat', cardFeeValue: 1.95, cardScope: 'All', surchargingEnabled: true, achFeeValue: 0,
};

const INTENT: PaymentIntentResult = {
  paymentIntentId: 'pi_test_123', clientSecret: 'pi_test_123_secret', amount: 300, fee: 1.95, total: 301.95,
};

const CONFIRMED: ConfirmResult = {
  transactionId: 't1', status: 'Succeeded', grossAmount: 300, feeAmount: 1.95, total: 301.95,
  maskedMethod: 'Visa •••• 4242', confirmationNumber: 'NEKO-ABC123', receiptId: 'r1',
};

function mockPaymentsService(): Partial<PaymentsService> {
  return {
    getPaymentOptions: jasmine.createSpy().and.resolveTo(OPTIONS),
    createIntent:      jasmine.createSpy().and.resolveTo(INTENT),
    confirmPayment:    jasmine.createSpy().and.resolveTo(CONFIRMED),
  };
}

async function renderComponent() {
  const view = await render(OneTimeComponent, {
    providers: [
      provideRouter([]),
      // The component injects with environment.stripePublishableKey (empty in the test/prod
      // environment); a global test key lets injectStripe resolve to the (spied) StripeService.
      provideNgxStripe('pk_test_karma'),
      { provide: PaymentsService, useValue: mockPaymentsService() },
    ],
  });
  await view.fixture.whenStable();
  view.fixture.detectChanges();
  return view;
}

describe('OneTimeComponent', () => {
  it('renders and starts on step 1', async () => {
    const { fixture } = await renderComponent();
    expect(fixture.componentInstance.currentStep()).toBe(1);
    expect(screen.getByText('Step 1 — How much?')).toBeTruthy();
  });

  it('builds amount presets from the backend options (FR-007)', async () => {
    const { fixture } = await renderComponent();
    const comp = fixture.componentInstance;
    const presets = comp.presets();
    expect(presets.find(p => p.id === 'current')!.amount).toBe(300);
    expect(presets.find(p => p.id === 'next')!.amount).toBe(250);
    expect(presets.find(p => p.id === 'both')!.amount).toBe(550);
    expect(screen.getByText('Current')).toBeTruthy();
    expect(screen.getByText('Next due')).toBeTruthy();
  });

  it('has NO raw card or bank inputs (SC-001 regression guard)', async () => {
    await renderComponent();
    // The legacy mock collected PAN/CVC/routing/account in plain inputs — those must be gone.
    expect(screen.queryByText('Card number')).toBeNull();
    expect(screen.queryByText('CVC')).toBeNull();
    expect(screen.queryByText('Routing number')).toBeNull();
    expect(screen.queryByText('Account number')).toBeNull();
    expect(screen.queryByPlaceholderText(/4242 4242/)).toBeNull();
  });

  it('shows the server-authoritative fee and total on review (not recomputed)', async () => {
    const { fixture } = await renderComponent();
    const comp = fixture.componentInstance;
    // Jump to review with the intent the server returned.
    comp.intent.set(INTENT);
    comp.clientSecret.set(INTENT.clientSecret);
    comp.currentStep.set(3);
    fixture.detectChanges();

    expect(screen.getByTestId('summary-fee').textContent).toContain('1.95');
    expect(screen.getByTestId('summary-total').textContent).toContain('301.95');
  });

  it('selecting a method creates a PaymentIntent for the chosen amount', async () => {
    const { fixture } = await renderComponent();
    const comp = fixture.componentInstance;
    const svc = fixture.debugElement.injector.get(PaymentsService);

    comp.selectedPreset.set('current');
    comp.selectMethod('ach');
    await fixture.whenStable();

    expect(svc.createIntent).toHaveBeenCalledWith(300, 'ach');
    expect(comp.clientSecret()).toBe(INTENT.clientSecret);
  });

  it('confirms via Stripe.js then records on the backend and shows the receipt', async () => {
    const { fixture } = await renderComponent();
    const comp = fixture.componentInstance;
    const svc = fixture.debugElement.injector.get(PaymentsService);

    // Arrange a ready-to-confirm review state without mounting the real Stripe iframe.
    comp.intent.set(INTENT);
    comp.clientSecret.set(INTENT.clientSecret);
    comp.currentStep.set(3);
    (comp as any).paymentElement = { elements: {} };
    spyOn(comp.stripe, 'confirmPayment').and.returnValue(
      of({ paymentIntent: { status: 'succeeded' } }) as any);

    await comp.next(); // step 3 → submit

    expect(comp.stripe.confirmPayment).toHaveBeenCalled();
    expect(svc.confirmPayment).toHaveBeenCalledWith('pi_test_123');
    expect(comp.currentStep()).toBe(4);
    expect(comp.result()!.confirmationNumber).toBe('NEKO-ABC123');

    fixture.detectChanges();
    expect(screen.getByTestId('confirmation-number').textContent).toContain('NEKO-ABC123');
  });

  it('surfaces a Stripe confirmation error and stays on review', async () => {
    const { fixture } = await renderComponent();
    const comp = fixture.componentInstance;

    comp.intent.set(INTENT);
    comp.clientSecret.set(INTENT.clientSecret);
    comp.currentStep.set(3);
    (comp as any).paymentElement = { elements: {} };
    spyOn(comp.stripe, 'confirmPayment').and.returnValue(
      of({ error: { message: 'Your card was declined.' } }) as any);

    await comp.next();

    expect(comp.currentStep()).toBe(3);
    expect(comp.error()).toContain('declined');
  });
});
