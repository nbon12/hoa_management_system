import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { OneTimeComponent } from './one-time.component';
import { PaymentsService } from '../../../core/services/payments.service';
import { LedgerEntry } from '../../../core/models';

const MOCK_ENTRY: LedgerEntry = {
  id: '1', date: '2026-05-01', description: 'Regular Assessment',
  type: 'Regular Assessment', charge: 250, payment: 0, balance: 500, docNumber: 'RA202605',
};

function makeMockPaymentsService(): Partial<PaymentsService> {
  return {
    getLedger:     jasmine.createSpy().and.returnValue(Promise.resolve([MOCK_ENTRY])),
    submitPayment: jasmine.createSpy().and.returnValue(Promise.resolve({
      confirmationNumber: 'CONF123', amount: 500, date: '2026-05-01',
    })),
  } as any;
}

describe('OneTimeComponent', () => {
  let fixture: ComponentFixture<OneTimeComponent>;
  let comp: OneTimeComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports:   [OneTimeComponent],
      providers: [
        provideRouter([]),
        { provide: PaymentsService, useValue: makeMockPaymentsService() },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(OneTimeComponent);
    comp    = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(comp).toBeTruthy());

  it('starts on step 1', () => {
    expect(comp.currentStep()).toBe(1);
  });

  it('shows stepper with steps', () => {
    expect(el.textContent).toContain('1');
    expect(el.textContent).toContain('2');
    expect(el.textContent).toContain('3');
  });

  it('shows amount presets in step 1', () => {
    expect(el.textContent).toContain('Current');
    expect(el.textContent).toContain('Next due');
    expect(el.textContent).toContain('Both');
  });

  it('balance signal is set from loaded ledger', () => {
    expect(comp.balance()).toBe(MOCK_ENTRY.balance);
  });

  it('resolvedAmount with current preset equals loaded balance', () => {
    comp.selectedPreset.set('current');
    expect(comp.resolvedAmount()).toBe(comp.balance());
  });

  it('resolvedAmount with next preset equals assessment', () => {
    comp.selectedPreset.set('next');
    expect(comp.resolvedAmount()).toBe(comp.assessment());
  });

  it('resolvedAmount for both preset = balance + assessment', () => {
    comp.selectedPreset.set('both');
    expect(comp.resolvedAmount()).toBe(comp.balance() + comp.assessment());
  });

  it('totalAmount adds processing fee for card', () => {
    comp.method.set('card');
    expect(comp.totalAmount()).toBe(comp.resolvedAmount() + 1.95);
  });

  it('totalAmount has no fee for ach', () => {
    comp.method.set('ach');
    expect(comp.totalAmount()).toBe(comp.resolvedAmount());
  });

  it('next() advances to step 2', async () => {
    await comp.next();
    expect(comp.currentStep()).toBe(2);
  });

  it('back() goes back to step 1 from step 2', async () => {
    await comp.next();
    comp.back();
    expect(comp.currentStep()).toBe(1);
  });

  it('shows payment method options on step 2', async () => {
    await comp.next();
    fixture.detectChanges();
    expect(el.textContent).toContain('Credit card');
    expect(el.textContent).toContain('eCheck');
  });

  it('shows review on step 3', async () => {
    await comp.next();
    await comp.next();
    fixture.detectChanges();
    expect(el.textContent).toContain('Review');
    expect(el.textContent).toContain('Total');
  });

  it('submits payment and shows confirmation on step 3 submit', async () => {
    await comp.next();
    await comp.next();
    await comp.next();
    fixture.detectChanges();
    expect(comp.currentStep()).toBe(4);
    expect(comp.result()).not.toBeNull();
  });

  it('shows confirmation after payment', async () => {
    await comp.next();
    await comp.next();
    await comp.next();
    fixture.detectChanges();
    expect(el.textContent).toContain('Payment submitted');
  });
});
