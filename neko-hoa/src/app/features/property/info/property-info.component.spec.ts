import { TestBed, ComponentFixture } from '@angular/core/testing';
import { PropertyInfoComponent } from './property-info.component';
import { PropertyService } from '../../../core/services/property.service';
import { Property } from '../../../core/models';

const MOCK_PROPERTY: Property = {
  accountNumber: 'SAKURA-001',
  communityId: 'SAKURA', communityName: 'Sakura Heights HOA',
  address: '1 Sakura Drive', city: 'San Jose', state: 'CA', zip: '95101',
  lot: 'A1', phase: null, section: '1', block: null,
  fiscalYear: 2026, yearBuilt: 2005, status: 'active',
  monthlyAssessment: 250, annualAssessment: 3000,
  assessmentDueDay: 1, lateFeeAmount: 50, lateFeeGraceDays: 15,
  financeChargeRate: 0.015,
};

function makeMockPropertyService(): Partial<PropertyService> {
  return {
    getProperty: jasmine.createSpy().and.returnValue(Promise.resolve(MOCK_PROPERTY)),
  } as any;
}

describe('PropertyInfoComponent', () => {
  let fixture: ComponentFixture<PropertyInfoComponent>;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports:   [PropertyInfoComponent],
      providers: [{ provide: PropertyService, useValue: makeMockPropertyService() }],
    }).compileComponents();
    fixture = TestBed.createComponent(PropertyInfoComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(fixture.componentInstance).toBeTruthy());

  it('shows account number from API data', () => {
    expect(el.textContent).toContain('SAKURA-001');
  });

  it('shows assessment rules section', () => {
    expect(el.textContent).toContain('Assessment rules');
  });

  it('shows 3 assessment rule rows', () => {
    const rows = el.querySelectorAll('.data-table tbody tr');
    expect(rows.length).toBe(3);
  });

  it('shows late fee amount', () => {
    expect(el.textContent).toContain('50.00');
  });

  it('shows finance charge rate', () => {
    expect(el.textContent).toMatch(/0\.015%/);
  });

  it('shows property details grid', () => {
    expect(el.textContent).toContain('Property details');
  });

  it('has 12 month markers in timeline', () => {
    const comp = fixture.componentInstance;
    expect(comp.months.length).toBe(12);
  });

  it('shows timeline section', () => {
    expect(el.textContent).toContain('Late fee timeline');
  });
});
