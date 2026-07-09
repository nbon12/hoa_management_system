import { render, screen, fireEvent } from '@testing-library/angular';
import { provideRouter } from '@angular/router';
import { provideNgxStripe } from 'ngx-stripe';
import { RecurringComponent } from './recurring.component';
import {
  PaymentsService, RecurringInfo, SetupIntentResult, DraftRow,
} from '../../../core/services/payments.service';

const ENROLLED: RecurringInfo = {
  id: 'rec1', amountType: 'assessment', fixedAmount: null, method: 'card', draftDay: 1,
  status: 'active', processingFee: 1.95, maskedMethod: 'Visa •••• 4242',
  nextDraftDate: '2026-07-01', nextDraftAmount: 251.95, mandateAcceptedAt: '2026-06-07T00:00:00Z',
};

const SETUP: SetupIntentResult = {
  setupIntentId: 'seti_1', clientSecret: 'seti_1_secret', publishableKey: 'pk_test_x',
};

const DRAFTS: DraftRow[] = [
  { id: 'd1', date: '2026-06-01', source: 'Visa •••• 4242', amount: 251.95, status: 'Paid', transactionStatus: 'Succeeded' },
  { id: 'd2', date: '2026-07-01', source: 'Visa •••• 4242', amount: 251.95, status: 'Scheduled', transactionStatus: null },
];

/** A PaymentsService double; `recurring` defaults to null (not enrolled) unless overridden. */
function mockPaymentsService(recurring: RecurringInfo | null = null): Partial<PaymentsService> {
  return {
    getRecurring:      jasmine.createSpy('getRecurring').and.resolveTo(recurring),
    getDrafts:         jasmine.createSpy('getDrafts').and.resolveTo(DRAFTS),
    createSetupIntent: jasmine.createSpy('createSetupIntent').and.resolveTo(SETUP),
    saveRecurring:     jasmine.createSpy('saveRecurring').and.resolveTo(ENROLLED),
    cancelRecurring:   jasmine.createSpy('cancelRecurring').and.resolveTo(undefined),
    // The embedded payment-alerts section loads its own prefs on init.
    getAlertPreferences: jasmine.createSpy('getAlertPreferences')
      .and.resolveTo({ smsOptIn: false, emailOptIn: false, alertPhone: null }),
    saveAlertPreferences: jasmine.createSpy('saveAlertPreferences')
      .and.resolveTo({ smsOptIn: false, emailOptIn: false, alertPhone: null }),
  };
}

async function renderComponent(recurring: RecurringInfo | null = null) {
  const svc = mockPaymentsService(recurring);
  const view = await render(RecurringComponent, {
    providers: [
      provideRouter([]),
      // Component injects with environment.stripePublishableKey (empty in test/prod env);
      // a global test key lets injectStripe resolve a (spyable) Stripe instance.
      provideNgxStripe('pk_test_karma'),
      { provide: PaymentsService, useValue: svc },
    ],
  });
  await view.fixture.whenStable();
  view.fixture.detectChanges();
  return { ...view, svc };
}

describe('RecurringComponent', () => {
  it('loads and renders the drafts history once ready', async () => {
    await renderComponent();
    expect(screen.getByTestId('drafts-table')).toBeTruthy();
    expect(screen.queryByText('No drafts yet.')).toBeNull();
    // Both draft rows are shown.
    expect(screen.getAllByText('Visa •••• 4242').length).toBeGreaterThan(0);
  });

  it('shows the enrolled status card with masked method and next draft (no raw data)', async () => {
    await renderComponent(ENROLLED);
    expect(screen.getByTestId('enrolled-pill')).toBeTruthy();
    expect(screen.getByTestId('status-state').textContent).toContain('Active');
    expect(screen.getByTestId('masked-method').textContent).toContain('Visa •••• 4242');
    expect(screen.getByTestId('next-draft-amount').textContent).toContain('251.95');
  });

  it('offers "Set up auto-pay" when the resident is not enrolled', async () => {
    await renderComponent(null);
    expect(screen.queryByTestId('enrolled-pill')).toBeNull();
    expect(screen.getByTestId('setup-toggle').textContent).toContain('Set up auto-pay');
  });

  it('has NO raw card or bank inputs (SC-001 regression guard)', async () => {
    const { fixture } = await renderComponent(null);
    // Reveal the full setup form so any legacy raw-instrument fields would be in the DOM.
    fixture.componentInstance.wizard.setupMode.set(true);
    fixture.componentInstance.wizard.clientSecret.set(SETUP.clientSecret);
    fixture.detectChanges();

    expect(screen.queryByText('Card number')).toBeNull();
    expect(screen.queryByText('CVC')).toBeNull();
    expect(screen.queryByText('Routing number')).toBeNull();
    expect(screen.queryByText('Account number')).toBeNull();
    expect(screen.queryByPlaceholderText(/4242 4242/)).toBeNull();
    // The vaulting happens inside Stripe's hosted Payment Element, not our inputs.
    expect(document.querySelector('ngx-stripe-payment')).toBeTruthy();
  });

  it('creates a SetupIntent when setup begins so the Element can mount', async () => {
    const { fixture, svc } = await renderComponent(null);
    const comp = fixture.componentInstance;

    await comp.wizard.beginSetup();
    fixture.detectChanges();

    expect(svc.createSetupIntent).toHaveBeenCalled();
    expect(comp.wizard.clientSecret()).toBe(SETUP.clientSecret);
    expect(comp.wizard.setupMode()).toBeTrue();
  });

  it('blocks save until the mandate is accepted (FR-009)', async () => {
    const { fixture } = await renderComponent(null);
    const comp = fixture.componentInstance;

    await comp.wizard.beginSetup();
    const confirmSetup = jasmine.createSpy('confirmSetup');
    (comp as any).stripeHost = { confirmSetup };
    comp.wizard.mandateAccepted = false;

    await comp.save();

    expect(confirmSetup).not.toHaveBeenCalled();
    expect(comp.wizard.error()).toContain('authorization');
  });

  it('vaults via confirmSetup then persists the enrollment + mandate', async () => {
    const { fixture, svc } = await renderComponent(null);
    const comp = fixture.componentInstance;

    await comp.wizard.beginSetup();
    const confirmSetup = jasmine.createSpy('confirmSetup')
      .and.resolveTo({ error: null, status: 'succeeded' });
    (comp as any).stripeHost = { confirmSetup };
    comp.wizard.amountType.set('fixed');
    comp.wizard.fixedAmount = '150';
    comp.wizard.draftDay = 5;
    comp.wizard.mandateAccepted = true;

    await comp.save();

    expect(confirmSetup).toHaveBeenCalled();
    expect(svc.saveRecurring).toHaveBeenCalled();
    const req = (svc.saveRecurring as jasmine.Spy).calls.mostRecent().args[0];
    expect(req.amountType).toBe('fixed');
    expect(req.fixedAmount).toBe(150);
    expect(req.draftDay).toBe(5);
    expect(req.setupIntentId).toBe('seti_1');
    expect(req.mandateAccepted).toBeTrue();
    expect(req.mandateVersion).toBeTruthy();
    expect(comp.wizard.saved()).toBeTrue();
  });

  it('rejects a fixed amount of zero or less', async () => {
    const { fixture, svc } = await renderComponent(null);
    const comp = fixture.componentInstance;

    await comp.wizard.beginSetup();
    const confirmSetup = jasmine.createSpy('confirmSetup');
    (comp as any).stripeHost = { confirmSetup };
    comp.wizard.amountType.set('fixed');
    comp.wizard.fixedAmount = '0';
    comp.wizard.mandateAccepted = true;

    await comp.save();

    expect(confirmSetup).not.toHaveBeenCalled();
    expect(svc.saveRecurring).not.toHaveBeenCalled();
    expect(comp.wizard.error()).toContain('greater than zero');
  });

  it('surfaces a Stripe confirmSetup error without persisting', async () => {
    const { fixture, svc } = await renderComponent(null);
    const comp = fixture.componentInstance;

    await comp.wizard.beginSetup();
    (comp as any).stripeHost = {
      confirmSetup: jasmine.createSpy('confirmSetup')
        .and.resolveTo({ error: 'Your card was declined.', status: null }),
    };
    comp.wizard.mandateAccepted = true;

    await comp.save();

    expect(svc.saveRecurring).not.toHaveBeenCalled();
    expect(comp.wizard.error()).toContain('declined');
  });

  it('cancels auto-pay after confirmation and reloads the (now empty) enrollment', async () => {
    const { fixture, svc } = await renderComponent(ENROLLED);
    const comp = fixture.componentInstance;
    spyOn(window, 'confirm').and.returnValue(true);
    (svc.getRecurring as jasmine.Spy).and.resolveTo(null);

    await comp.confirmCancel();

    expect(svc.cancelRecurring).toHaveBeenCalled();
    expect(comp.wizard.rec()).toBeNull();
  });

  it('does not cancel when the resident dismisses the confirm dialog', async () => {
    const { fixture, svc } = await renderComponent(ENROLLED);
    const comp = fixture.componentInstance;
    spyOn(window, 'confirm').and.returnValue(false);

    await comp.confirmCancel();

    expect(svc.cancelRecurring).not.toHaveBeenCalled();
  });
});
