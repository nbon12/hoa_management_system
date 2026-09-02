import { TestBed, ComponentFixture } from '@angular/core/testing';
import { GlossaryPanelComponent } from './glossary-panel.component';
import { MetricDescriptor } from '../../../core/services/board.service';

const DESCRIPTORS: MetricDescriptor[] = [
  { id: 'over30', label: 'Over 30-Days Delinquent', definitionText: 'Share unpaid > 30 days.', value: '10%', status: 'Watch', emphasis: 'Highlight' },
  { id: 'over60', label: 'Over 60-Days Delinquent', definitionText: 'Unpaid > 60 days.',       value: '8%',  status: 'Watch', emphasis: 'Highlight' },
  { id: 'ach',    label: 'Registered ACH Owners',   definitionText: 'Owners paying by draft.', value: '60%', status: 'Ok',    emphasis: 'Normal' },
];

describe('GlossaryPanelComponent', () => {
  let fixture: ComponentFixture<GlossaryPanelComponent>;
  let comp: GlossaryPanelComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [GlossaryPanelComponent] }).compileComponents();
    fixture = TestBed.createComponent(GlossaryPanelComponent);
    comp = fixture.componentInstance;
    el = fixture.nativeElement;
    // Attach to the document so focus() can move the active element.
    document.body.appendChild(el);
    comp.descriptors = DESCRIPTORS;
  });

  afterEach(() => {
    if (el.parentNode) el.parentNode.removeChild(el);
  });

  it('derives one entry per descriptor from the same list as the rows (FR-034)', () => {
    fixture.detectChanges();
    expect(el.querySelectorAll('.glossary__item').length).toBe(3);
    expect(el.textContent).toContain('Unpaid > 60 days.');
  });

  it('visually distinguishes the targeted entry (FR-033)', () => {
    comp.target = 'over60';
    fixture.detectChanges();
    const target = el.querySelector('[data-glossary-id="over60"]')!;
    expect(target.classList).toContain('glossary__item--target');
    expect(target.textContent).toContain('jumped here');
    // Non-targets are not distinguished.
    expect(el.querySelector('[data-glossary-id="ach"]')!.classList).not.toContain('glossary__item--target');
  });

  it('moves focus to the targeted definition on open (Accessibility)', () => {
    comp.target = 'over60';
    fixture.detectChanges();
    const target = el.querySelector('[data-glossary-id="over60"]') as HTMLElement;
    expect(document.activeElement).toBe(target);
  });

  it('emits close when the close button is activated', () => {
    comp.target = 'over60';
    fixture.detectChanges();
    const closed = jasmine.createSpy('close');
    comp.close.subscribe(closed);
    (el.querySelector('.glossary__close') as HTMLButtonElement).click();
    expect(closed).toHaveBeenCalled();
  });
});
