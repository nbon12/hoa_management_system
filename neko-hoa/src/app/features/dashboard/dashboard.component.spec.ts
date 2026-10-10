import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DashboardComponent } from './dashboard.component';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import { signal } from '@angular/core';
import { CurrentUser, DashboardSummary } from '../../core/models';

const MOCK_USER: CurrentUser = { id: '1', firstName: 'Nicholas', lastName: 'Bonilla', email: 'n@b.com', initials: 'NB', lastActiveMode: 'Resident', memberships: [] };

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
  architecturalInfoRequested: { count: 0, applicationId: null },
};

function makeMockAuthService(): Partial<AuthService> {
  return { user: signal(MOCK_USER) };
}

function makeMockDashboardService(summary: DashboardSummary = MOCK_SUMMARY): Partial<DashboardService> {
  return {
    getSummary: jasmine.createSpy().and.returnValue(Promise.resolve(summary)),
  } as any;
}

function violationsCard(root: HTMLElement): HTMLElement | null {
  const labels = root.querySelectorAll('.grid-4 .card .field-label');
  for (const label of Array.from(labels)) {
    if (label.textContent?.trim() === 'Violations') {
      return label.closest('.card');
    }
  }
  return null;
}

async function createDashboardFixture(summary: DashboardSummary = MOCK_SUMMARY): Promise<{
  fixture: ComponentFixture<DashboardComponent>;
  el: HTMLElement;
}> {
  await TestBed.configureTestingModule({
    imports:   [DashboardComponent],
    providers: [
      provideRouter([]),
      { provide: AuthService,       useValue: makeMockAuthService() },
      { provide: DashboardService,  useValue: makeMockDashboardService(summary) },
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(DashboardComponent);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, el: fixture.nativeElement };
}

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let el: HTMLElement;

  beforeEach(async () => {
    ({ fixture, el } = await createDashboardFixture());
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

  it('shows "needs attention" pill when openViolations > 0', () => {
    const card = violationsCard(el);
    expect(card?.textContent).toContain('needs attention');
    expect(card?.querySelector('.pill--warn')).toBeTruthy();
    expect(card?.querySelector('.pill--ok')).toBeFalsy();
  });

  it('shows "compliant" pill when openViolations is 0', async () => {
    TestBed.resetTestingModule();
    const { el: zeroEl } = await createDashboardFixture({ ...MOCK_SUMMARY, openViolations: 0 });
    const card = violationsCard(zeroEl);
    expect(card?.textContent).toContain('compliant');
    expect(card?.querySelector('.pill--ok')).toBeTruthy();
    expect(card?.querySelector('.pill--warn')).toBeFalsy();
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

  // 029 US3 AS1 / FR-017: an open architectural request with an unanswered board question shows a dashboard
  // alert that links to it; with none, there is no alert.
  describe('architectural info-requested alert (029)', () => {
    it('shows the alert with a link to the request when count > 0', async () => {
      TestBed.resetTestingModule();
      const { el: alertEl } = await createDashboardFixture({
        ...MOCK_SUMMARY, architecturalInfoRequested: { count: 1, applicationId: 'app-42' },
      });
      const alert = alertEl.querySelector('[role="status"]');
      expect(alert?.textContent).toContain('The board needs more information about your architectural request.');
      expect(alert?.querySelector('a')?.getAttribute('href')).toBe('/app/property/architectural/app-42');
    });

    it('shows no alert when count is 0', () => {
      expect(el.textContent).not.toContain('The board needs more information');
    });
  });
});
