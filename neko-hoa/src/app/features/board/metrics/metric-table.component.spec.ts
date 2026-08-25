import { TestBed, ComponentFixture } from '@angular/core/testing';
import { MetricTableComponent } from './metric-table.component';
import { MetricDescriptor } from '../../../core/services/board.service';

const ROWS: MetricDescriptor[] = [
  { id: 'over30', label: 'Over 30-Days Delinquent', definitionText: 'def a', value: '10%', detail: '37 homeowners', status: 'warn', emphasis: 'warn' },
  { id: 'ach',    label: 'Registered ACH Owners',   definitionText: 'def b', value: '60%', detail: null, status: 'ok', emphasis: 'ok' },
];

describe('MetricTableComponent', () => {
  let fixture: ComponentFixture<MetricTableComponent>;
  let comp: MetricTableComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [MetricTableComponent] }).compileComponents();
    fixture = TestBed.createComponent(MetricTableComponent);
    comp = fixture.componentInstance;
    el = fixture.nativeElement;
  });

  it('renders one row per descriptor', () => {
    comp.rows = ROWS;
    fixture.detectChanges();
    expect(el.querySelectorAll('tbody tr').length).toBe(2);
  });

  it('makes the help affordance the right-most column of every row (FR-032)', () => {
    comp.rows = ROWS;
    fixture.detectChanges();
    // Header: last column is "Help".
    const headers = Array.from(el.querySelectorAll('thead th'));
    expect(headers[headers.length - 1].textContent?.trim()).toBe('Help');
    // Each row: the last cell holds the help button.
    el.querySelectorAll('tbody tr').forEach(tr => {
      const cells = tr.querySelectorAll('td');
      const last = cells[cells.length - 1];
      expect(last.querySelector('button.metric-help')).toBeTruthy();
    });
  });

  it('emits the descriptor id when a help affordance is activated', () => {
    comp.rows = ROWS;
    fixture.detectChanges();
    const emitted: string[] = [];
    comp.help.subscribe(id => emitted.push(id));
    const firstHelp = el.querySelector('tbody tr .metric-help') as HTMLButtonElement;
    firstHelp.click();
    expect(emitted).toEqual(['over30']);
  });

  it('renders an explicit empty state when there are no descriptors (Edge case)', () => {
    comp.rows = [];
    fixture.detectChanges();
    expect(el.querySelector('[data-testid="metric-empty"]')).toBeTruthy();
    expect(el.querySelector('table')).toBeNull();
  });

  it('adding/removing a descriptor changes only the data (no template edit)', () => {
    comp.rows = ROWS;
    fixture.detectChanges();
    expect(el.querySelectorAll('tbody tr').length).toBe(2);
    // Remove one — same component, only the input changed.
    comp.rows = [ROWS[0]];
    fixture.detectChanges();
    expect(el.querySelectorAll('tbody tr').length).toBe(1);
    // Add a new one — again only data.
    comp.rows = [...ROWS, { id: 'x', label: 'New Metric', definitionText: 'd', value: 1, status: 'ok', emphasis: 'none' }];
    fixture.detectChanges();
    expect(el.querySelectorAll('tbody tr').length).toBe(3);
    expect(el.textContent).toContain('New Metric');
  });

  it('renders an explicit per-row unavailable state without blanking siblings (FR-036)', () => {
    comp.rows = [
      { id: 'broken', label: 'Broken Metric', definitionText: 'd', value: null, status: 'Unavailable', emphasis: 'none' },
      ROWS[1],
    ];
    fixture.detectChanges();
    const rows = el.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].classList).toContain('metric-row--unavailable');
    expect(rows[0].textContent).toContain('unavailable');
    // The sibling still renders its real value.
    expect(rows[1].textContent).toContain('60%');
  });
});
