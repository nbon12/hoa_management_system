import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DashboardComponent } from './dashboard.component';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import { signal } from '@angular/core';
import { CurrentUser, DashboardSummary } from '../../core/models';

const MOCK_USER: CurrentUser = { id: '1', firstName: 'Nicholas', lastName: 'Bonilla', email: 'n@b.com', initials: 'NB' };

const MOCK_SUMMARY: DashboardSummary = {
  currentBalance:        500,
  balanceDueDate:        '2026-06-01',
  openViolations:        2,
  documentCount:         18,
  newDocumentsThisMonth: 3,
  pinnedAnnouncement: {
    id: 'a1', title: 'Board Meeting', body: 'Meeting June 10', date: '2026-05-19',
    category: 'Board', pinned: true, commentCount: 2, likeCount: 5,
    imageUrl: null, authorInitials: 'DC', authorLabel: 'David Chen',
  },
  thisWeekEvents: [],
  nextEvent: null,
  recentActivity: [],
  communityExpenses: [],
};

function makeMockAuthService(): Partial<AuthService> {
  return { user: signal(MOCK_USER) };
}

function makeMockDashboardService(): Partial<DashboardService> {
  return {
    getSummary: jasmine.createSpy().and.returnValue(Promise.resolve(MOCK_SUMMARY)),
  } as any;
}

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports:   [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService,       useValue: makeMockAuthService() },
        { provide: DashboardService,  useValue: makeMockDashboardService() },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    await fixture.whenStable();
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
    expect(el.textContent).toContain('Payment due');
  });

  describe('categoryColor()', () => {
    it('returns lav-2 for Board', () => {
      expect(fixture.componentInstance.categoryColor('Board')).toBe('var(--lav-2)');
    });
    it('returns pink-2 for Amenity', () => {
      expect(fixture.componentInstance.categoryColor('Amenity')).toBe('var(--pink-2)');
    });
    it('returns default for unknown category', () => {
      expect(fixture.componentInstance.categoryColor('Unknown')).toBe('var(--lav-2)');
    });
  });
});
