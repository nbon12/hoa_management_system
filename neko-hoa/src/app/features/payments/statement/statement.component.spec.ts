import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { StatementComponent } from './statement.component';
import { PaymentsService } from '../../../core/services/payments.service';
import { LedgerEntry } from '../../../core/models';

const MOCK_LEDGER: LedgerEntry[] = [
  { id: '1', date: '2026-01-01', description: 'Regular Assessment – January 2026', type: 'Regular Assessment', charge: 250, payment: 0,   balance: 250, docNumber: 'RA202601' },
  { id: '2', date: '2026-01-05', description: 'Online Payment – Thank You',         type: 'Payment',           charge: 0,   payment: 250, balance: 0,   docNumber: 'PMT202601' },
  { id: '3', date: '2026-02-01', description: 'Regular Assessment – February 2026', type: 'Regular Assessment', charge: 250, payment: 0,   balance: 250, docNumber: 'RA202602' },
  { id: '4', date: '2026-03-01', description: 'Late Fee',                            type: 'Late Fee',          charge: 50,  payment: 0,   balance: 300, docNumber: 'LF202603' },
];

function makeMockPaymentsService(): Partial<PaymentsService> {
  return {
    getLedger:       jasmine.createSpy().and.returnValue(Promise.resolve(MOCK_LEDGER)),
    getBalance:      jasmine.createSpy().and.returnValue(Promise.resolve({ currentBalance: 300, balanceDueDate: '2026-03-01', monthlyAssessment: 250 })),
    getDrafts:       jasmine.createSpy().and.returnValue(Promise.resolve([])),
    loadRecurring:   jasmine.createSpy().and.returnValue(Promise.resolve()),
    submitPayment:   jasmine.createSpy().and.returnValue(Promise.resolve({ confirmationNumber: 'CONF001', amount: 250, date: '2026-03-01' })),
  } as any;
}

describe('StatementComponent', () => {
  let fixture: ComponentFixture<StatementComponent>;
  let comp: StatementComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports:   [StatementComponent],
      providers: [
        provideRouter([]),
        { provide: PaymentsService, useValue: makeMockPaymentsService() },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(StatementComponent);
    comp    = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(comp).toBeTruthy());

  it('renders the page title', () => {
    expect(el.textContent?.toLowerCase()).toContain('statement');
  });

  it('renders the ledger table', () => {
    expect(el.querySelector('.data-table')).toBeTruthy();
  });

  it('shows all mock ledger entries by default', () => {
    expect(comp.filteredEntries().length).toBe(MOCK_LEDGER.length);
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
    comp.searchTerm = 'Late Fee';
    fixture.detectChanges();
    const results = comp.filteredEntries();
    results.forEach(e => {
      expect(
        e.description.toLowerCase().includes('late fee') ||
        e.docNumber.toLowerCase().includes('late fee')
      ).toBeTrue();
    });
    expect(results.length).toBeLessThan(MOCK_LEDGER.length);
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
