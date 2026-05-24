import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { StatementComponent } from './statement.component';
import { PaymentsService } from '../../../core/services/payments.service';

describe('StatementComponent', () => {
  let fixture: ComponentFixture<StatementComponent>;
  let comp: StatementComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatementComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(StatementComponent);
    comp = fixture.componentInstance;
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(comp).toBeTruthy());

  it('renders the page title', () => {
    expect(el.textContent).toContain('statement');
  });

  it('renders the ledger table', () => {
    expect(el.querySelector('.data-table')).toBeTruthy();
  });

  it('shows all ledger entries by default', () => {
    const svc = TestBed.inject(PaymentsService);
    const rows = el.querySelectorAll('.data-table tbody tr');
    expect(rows.length).toBe(svc.getLedger().length);
  });

  it('shows balance footer', () => {
    expect(el.textContent).toContain('Balance');
  });

  it('filteredEntries() returns all entries with empty search', () => {
    comp.searchTerm = '';
    comp.typeFilter = '';
    fixture.detectChanges();
    expect(comp.filteredEntries().length).toBeGreaterThan(0);
  });

  it('filteredEntries() filters by search term', () => {
    comp.searchTerm = 'received';
    fixture.detectChanges();
    // All returned entries should contain the search term in description or doc number
    comp.filteredEntries().forEach(e => {
      expect(
        e.description.toLowerCase().includes('received') ||
        e.docNumber.toLowerCase().includes('received')
      ).toBeTrue();
    });
    // And results should be fewer than total
    const svc = TestBed.inject(PaymentsService);
    expect(comp.filteredEntries().length).toBeLessThan(svc.getLedger().length);
  });

  it('filteredEntries() filters by type', () => {
    comp.typeFilter = 'Payment';
    fixture.detectChanges();
    comp.filteredEntries().forEach(e => expect(e.type).toBe('Payment'));
  });

  it('totalCharges is positive', () => {
    expect(comp.totalCharges).toBeGreaterThan(0);
  });

  it('totalPayments is positive', () => {
    expect(comp.totalPayments).toBeGreaterThan(0);
  });

  it('balance = totalCharges - totalPayments', () => {
    expect(comp.balance).toBe(comp.totalCharges - comp.totalPayments);
  });

  it('shows Make payment button', () => {
    expect(el.textContent).toContain('Make payment');
  });
});
