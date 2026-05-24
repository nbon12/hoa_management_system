import { TestBed, ComponentFixture } from '@angular/core/testing';
import { ViolationsComponent } from './violations.component';

describe('ViolationsComponent', () => {
  let fixture: ComponentFixture<ViolationsComponent>;
  let comp: ViolationsComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ViolationsComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(ViolationsComponent);
    comp = fixture.componentInstance;
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(comp).toBeTruthy());

  it('starts on open tab', () => {
    expect(comp.activeTab()).toBe('open');
  });

  it('shows 3 summary cards', () => {
    const cards = el.querySelectorAll('.grid-3 .card');
    expect(cards.length).toBe(3);
  });

  it('openCount is 0', () => {
    expect(comp.openCount).toBe(0);
  });

  it('shows compliance empty state', () => {
    expect(el.textContent).toContain('compliance');
  });

  it('switching to closed tab shows violation history', () => {
    comp.activeTab.set('closed');
    fixture.detectChanges();
    expect(el.querySelector('.data-table tbody')).toBeTruthy();
    const rows = el.querySelectorAll('.data-table tbody tr');
    expect(rows.length).toBe(comp.closedViolations().length);
  });

  it('closed violations all have status closed', () => {
    comp.closedViolations().forEach(v => expect(v.status).toBe('closed'));
  });

  it('mostCommon returns a non-empty string', () => {
    expect(comp.mostCommon.length).toBeGreaterThan(0);
  });

  it('shows rules tab content', () => {
    comp.activeTab.set('rules');
    fixture.detectChanges();
    expect(el.textContent).toContain('rules');
  });
});
