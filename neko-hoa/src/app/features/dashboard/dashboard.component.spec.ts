import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DashboardComponent } from './dashboard.component';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import { signal } from '@angular/core';
import { CurrentUser } from '../../core/models';

function makeMockAuthService(firstName = 'Nicholas'): Partial<AuthService> {
  const u: CurrentUser = { id: '1', firstName, lastName: 'Bonilla', email: 'n@b.com', initials: 'NB' };
  return { user: signal(u) };
}

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: makeMockAuthService() },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('displays first name in greeting', () => {
    expect(el.textContent).toContain('Nicholas');
  });

  it('renders 4 stat cards', () => {
    const cards = el.querySelectorAll('.grid-4 .card');
    expect(cards.length).toBe(4);
  });

  it('shows balance stat card', () => {
    expect(el.textContent).toContain('Balance');
  });

  it('shows violations stat card', () => {
    expect(el.textContent).toContain('Violations');
  });

  it('shows recent activity table', () => {
    expect(el.textContent).toContain('Recent activity');
  });

  it('shows the pinned announcement', () => {
    expect(el.textContent).toContain('Pinned announcement');
  });

  it('shows This week section', () => {
    expect(el.textContent).toContain('This week');
  });

  it('shows community expenses section', () => {
    expect(el.textContent).toContain('My community');
  });

  it('shows quick links', () => {
    expect(el.textContent).toContain('Quick links');
  });

  it('shows payment due card when balance > 0', () => {
    const svc = TestBed.inject(DashboardService);
    const summary = svc.getSummary();
    if (summary.currentBalance > 0) {
      expect(el.textContent).toContain('Payment due');
    }
  });

  describe('categoryColor()', () => {
    it('returns lav-2 for Board', () => {
      const comp = fixture.componentInstance;
      expect(comp.categoryColor('Board')).toBe('var(--lav-2)');
    });
    it('returns pink-2 for Amenity', () => {
      const comp = fixture.componentInstance;
      expect(comp.categoryColor('Amenity')).toBe('var(--pink-2)');
    });
    it('returns default for unknown category', () => {
      const comp = fixture.componentInstance;
      expect(comp.categoryColor('Unknown')).toBe('var(--lav-2)');
    });
  });
});
