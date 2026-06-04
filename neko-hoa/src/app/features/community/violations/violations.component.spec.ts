import { TestBed, ComponentFixture } from '@angular/core/testing';
import { ViolationsComponent } from './violations.component';
import { CommunityService } from '../../../core/services/community.service';
import { Violation } from '../../../core/models';

const MOCK_VIOLATIONS: Violation[] = [
  { id: 'v1', issue: 'Overgrown hedges',    date: '2026-05-01', category: 'Landscape',   status: 'open'   },
  { id: 'v2', issue: 'Unapproved fence',    date: '2026-04-01', category: 'Architectural', status: 'open' },
  { id: 'v3', issue: 'Parking violation',   date: '2026-02-01', category: 'Parking',     status: 'closed' },
  { id: 'v4', issue: 'Noise complaint',     date: '2026-01-01', category: 'Noise',       status: 'closed' },
];

function makeMockCommunityService(): Partial<CommunityService> {
  return {
    getViolations: jasmine.createSpy().and.returnValue(Promise.resolve([...MOCK_VIOLATIONS])),
  } as any;
}

describe('ViolationsComponent', () => {
  let fixture: ComponentFixture<ViolationsComponent>;
  let comp: ViolationsComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports:   [ViolationsComponent],
      providers: [{ provide: CommunityService, useValue: makeMockCommunityService() }],
    }).compileComponents();

    fixture = TestBed.createComponent(ViolationsComponent);
    comp    = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
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

  it('openCount matches mock data', () => {
    expect(comp.openCount).toBe(MOCK_VIOLATIONS.filter(v => v.status === 'open').length);
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

  it('open violations all have status open', () => {
    comp.openViolations().forEach(v => expect(v.status).toBe('open'));
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
