import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { StatementComponent } from './statement.component';
import { PaymentsService, TransactionSummary } from '../../../core/services/payments.service';
import { LedgerEntry } from '../../../core/models';

const MOCK_LEDGER: LedgerEntry[] = [
  { id: '1', date: '2026-01-01', description: 'Regular Assessment – January 2026', type: 'Regular Assessment', charge: 250, payment: 0,   balance: 250, docNumber: 'RA202601' },
  { id: '2', date: '2026-01-05', description: 'Online Payment – Thank You',         type: 'Payment',           charge: 0,   payment: 250, balance: 0,   docNumber: 'PMT202601' },
  { id: '3', date: '2026-02-01', description: 'Regular Assessment – February 2026', type: 'Regular Assessment', charge: 250, payment: 0,   balance: 250, docNumber: 'RA202602' },
  { id: '4', date: '2026-03-01', description: 'Late Fee',                            type: 'Late Fee',          charge: 50,  payment: 0,   balance: 300, docNumber: 'LF202603' },
];

// Masked methods only — TransactionDto never carries raw card/bank data (SC-001).
const MOCK_TRANSACTIONS: TransactionSummary[] = [
  { id: 't1', createdAt: '2026-01-05', grossAmount: 250, feeAmount: 5,    total: 255, cumulativeRefundedAmount: 0,  status: 'Succeeded', paymentMethod: 'card', maskedMethod: 'Visa •••• 4242', isRecurring: false },
  { id: 't2', createdAt: '2026-02-01', grossAmount: 250, feeAmount: 1.95, total: 251.95, cumulativeRefundedAmount: 50, status: 'Refunded',  paymentMethod: 'card', maskedMethod: 'Visa •••• 4242', isRecurring: true },
];

function makeMockPaymentsService(): Partial<PaymentsService> {
  return {
    getLedger:       jasmine.createSpy().and.returnValue(Promise.resolve(MOCK_LEDGER)),
    getBalance:      jasmine.createSpy().and.returnValue(Promise.resolve({ currentBalance: 300, balanceDueDate: '2026-03-01', monthlyAssessment: 250 })),
    getDrafts:       jasmine.createSpy().and.returnValue(Promise.resolve([])),
    getTransactions: jasmine.createSpy('getTransactions').and.returnValue(Promise.resolve(MOCK_TRANSACTIONS)),
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

  describe('accessibility (T088 / WCAG 2.1 AA)', () => {
    it('exposes the view switcher as an ARIA tablist with aria-selected on the active tab', () => {
      const tablist = el.querySelector('[role="tablist"]');
      expect(tablist).toBeTruthy();
      expect(tablist?.getAttribute('aria-label')).toBeTruthy();

      const activeTab = el.querySelector('[data-testid="tab-statement"]');
      expect(activeTab?.getAttribute('role')).toBe('tab');
      expect(activeTab?.getAttribute('aria-selected')).toBe('true');
      expect(el.querySelector('[data-testid="tab-payments"]')?.getAttribute('aria-selected')).toBe('false');
    });

    it('gives the ledger table a caption and column-scoped headers', () => {
      const table = el.querySelector('.data-table');
      expect(table?.querySelector('caption')?.textContent?.toLowerCase()).toContain('ledger');
      const ths = Array.from(table?.querySelectorAll('thead th') ?? []);
      expect(ths.length).toBeGreaterThan(0);
      ths.forEach(th => expect(th.getAttribute('scope')).toBe('col'));
    });

    it('marks the transactions loading state as a live status region', async () => {
      comp.switchTab('payments');
      // Before whenStable the load is in flight, so the status spinner is showing.
      fixture.detectChanges();
      expect(el.querySelector('[data-testid="transactions-card"] [role="status"]')).toBeTruthy();
      await fixture.whenStable();
      fixture.detectChanges();

      const txnTable = el.querySelector('[data-testid="transactions-table"]');
      expect(txnTable?.querySelector('caption')).toBeTruthy();
      Array.from(txnTable?.querySelectorAll('thead th') ?? [])
        .forEach(th => expect(th.getAttribute('scope')).toBe('col'));
    });
  });

  describe('Payments tab (transaction history)', () => {
    function svc() {
      return TestBed.inject(PaymentsService) as unknown as { getTransactions: jasmine.Spy };
    }

    it('does not load transactions until the Payments tab is opened', () => {
      expect(svc().getTransactions).not.toHaveBeenCalled();
    });

    it('lazy-loads transactions the first time the Payments tab is opened, and only once', async () => {
      comp.switchTab('payments');
      await fixture.whenStable();
      fixture.detectChanges();

      expect(svc().getTransactions).toHaveBeenCalledTimes(1);
      expect(comp.transactions().length).toBe(MOCK_TRANSACTIONS.length);

      // Switching away and back must not refetch.
      comp.switchTab('statement');
      comp.switchTab('payments');
      await fixture.whenStable();
      expect(svc().getTransactions).toHaveBeenCalledTimes(1);
    });

    it('renders a row per transaction with masked method and refund column', async () => {
      comp.switchTab('payments');
      await fixture.whenStable();
      fixture.detectChanges();

      const rows = el.querySelectorAll('[data-testid="txn-row"]');
      expect(rows.length).toBe(MOCK_TRANSACTIONS.length);
      const table = el.querySelector('[data-testid="transactions-table"]') as HTMLElement;
      expect(table.textContent).toContain('Visa •••• 4242');
      expect(table.textContent).toContain('auto-pay');
      expect(table.textContent).toContain('one-time');
    });

    it('shows an empty state when there are no transactions', async () => {
      svc().getTransactions.and.returnValue(Promise.resolve([]));
      comp.switchTab('payments');
      await fixture.whenStable();
      fixture.detectChanges();

      expect(el.querySelector('[data-testid="txn-empty"]')).toBeTruthy();
      expect(el.querySelectorAll('[data-testid="txn-row"]').length).toBe(0);
    });

    it('hides the ledger search/filter card on the Payments tab (SC-001: no raw inputs anywhere)', async () => {
      comp.switchTab('payments');
      await fixture.whenStable();
      fixture.detectChanges();

      // The ledger card is gone, and there is certainly no card/account entry field.
      expect((el.textContent ?? '').toLowerCase()).not.toContain('search ledger');
      expect(el.querySelector('input[placeholder*="4242"]')).toBeNull();
    });
  });
});
