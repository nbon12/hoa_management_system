import { TestBed, ComponentFixture } from '@angular/core/testing';
import { PropertyInfoComponent } from './property-info.component';

describe('PropertyInfoComponent', () => {
  let fixture: ComponentFixture<PropertyInfoComponent>;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PropertyInfoComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(PropertyInfoComponent);
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(fixture.componentInstance).toBeTruthy());

  it('shows account number', () => {
    expect(el.textContent).toContain('R0670853L0541192');
  });

  it('shows assessment rules section', () => {
    expect(el.textContent).toContain('Assessment rules');
  });

  it('shows 3 assessment rule rows', () => {
    const rows = el.querySelectorAll('.data-table tbody tr');
    expect(rows.length).toBe(3);
  });

  it('shows late fee amount', () => {
    expect(el.textContent).toContain('20.00');
  });

  it('shows finance charge rate', () => {
    // Angular drops trailing zeros when interpolating a number; match on 18% prefix
    expect(el.textContent).toMatch(/18(\.00)?%/);
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
