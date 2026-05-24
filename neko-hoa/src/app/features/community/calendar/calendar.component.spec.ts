import { TestBed, ComponentFixture } from '@angular/core/testing';
import { CalendarComponent } from './calendar.component';

describe('CalendarComponent', () => {
  let fixture: ComponentFixture<CalendarComponent>;
  let comp: CalendarComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CalendarComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(CalendarComponent);
    comp = fixture.componentInstance;
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(comp).toBeTruthy());

  it('starts in month view', () => {
    expect(comp.view()).toBe('month');
  });

  it('shows month label', () => {
    expect(el.textContent).toContain('2026');
  });

  it('renders 42 calendar day cells', () => {
    const days = comp.calendarDays();
    expect(days.length).toBe(42);
  });

  it('calendarDays has 7 days per week', () => {
    expect(comp.calendarDays().length % 7).toBe(0);
  });

  it('shows day headers', () => {
    expect(el.textContent).toContain('Sun');
    expect(el.textContent).toContain('Sat');
  });

  it('nextMonth() advances the month', () => {
    const before = comp.currentMonth();
    comp.nextMonth();
    expect(comp.currentMonth()).toBe((before + 1) % 12);
  });

  it('prevMonth() goes back one month', () => {
    comp.currentMonth.set(5); // June
    comp.prevMonth();
    expect(comp.currentMonth()).toBe(4); // May
  });

  it('prevMonth() wraps around year boundary', () => {
    comp.currentMonth.set(0); // January
    comp.currentYear.set(2026);
    comp.prevMonth();
    expect(comp.currentMonth()).toBe(11); // December
    expect(comp.currentYear()).toBe(2025);
  });

  it('nextMonth() wraps around year boundary', () => {
    comp.currentMonth.set(11);
    comp.currentYear.set(2026);
    comp.nextMonth();
    expect(comp.currentMonth()).toBe(0);
    expect(comp.currentYear()).toBe(2027);
  });

  it('toggleCategory removes and re-adds a category', () => {
    const cat = 'Board' as const;
    expect(comp.activeCategories().has(cat)).toBeTrue();
    comp.toggleCategory(cat);
    expect(comp.activeCategories().has(cat)).toBeFalse();
    comp.toggleCategory(cat);
    expect(comp.activeCategories().has(cat)).toBeTrue();
  });

  it('switching to timeline view shows timeline content', () => {
    comp.view.set('timeline');
    fixture.detectChanges();
    expect(el.textContent).toContain('May');
  });

  it('upcomingEvents returns events in chronological order', () => {
    const events = comp.upcomingEvents();
    for (let i = 1; i < events.length; i++) {
      expect(events[i].date >= events[i - 1].date).toBeTrue();
    }
  });

  it('subscribe card is visible', () => {
    expect(el.textContent).toContain('Subscribe');
  });
});
