import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { OneTimeComponent } from './one-time.component';
import { PaymentsService } from '../../../core/services/payments.service';

describe('OneTimeComponent', () => {
  let fixture: ComponentFixture<OneTimeComponent>;
  let comp: OneTimeComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OneTimeComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(OneTimeComponent);
    comp = fixture.componentInstance;
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(comp).toBeTruthy());

  it('starts on step 1', () => {
    expect(comp.currentStep()).toBe(1);
  });

  it('shows stepper with 3 steps', () => {
    expect(el.textContent).toContain('1');
    expect(el.textContent).toContain('2');
    expect(el.textContent).toContain('3');
  });

  it('shows amount presets in step 1', () => {
    expect(el.textContent).toContain('Current');
    expect(el.textContent).toContain('Next due');
    expect(el.textContent).toContain('Both');
  });

  it('resolvedAmount uses current preset by default', () => {
    const svc = TestBed.inject(PaymentsService);
    expect(comp.resolvedAmount()).toBe(svc.currentBalance);
  });

  it('resolvedAmount changes when preset changes', () => {
    const svc = TestBed.inject(PaymentsService);
    comp.selectedPreset.set('next');
    expect(comp.resolvedAmount()).toBe(svc.nextAssessment);
  });

  it('resolvedAmount for both preset is currentBalance + nextAssessment', () => {
    const svc = TestBed.inject(PaymentsService);
    comp.selectedPreset.set('both');
    expect(comp.resolvedAmount()).toBe(svc.currentBalance + svc.nextAssessment);
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
    await comp.next(); // to step 2
    await comp.next(); // to step 3
    await comp.next(); // submit
    fixture.detectChanges();
    expect(comp.currentStep()).toBe(4);
    expect(comp.result()).not.toBeNull();
  });

  it('shows confirmation number after payment', async () => {
    await comp.next();
    await comp.next();
    await comp.next();
    fixture.detectChanges();
    expect(el.textContent).toContain('Payment submitted');
  });
});
