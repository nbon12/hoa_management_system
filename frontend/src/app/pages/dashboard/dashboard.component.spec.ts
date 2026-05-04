import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { DashboardService } from '../../services/dashboard.service';
import { DashboardSummary } from '../../models/dashboard-summary.model';

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let mockDashboardService: jasmine.SpyObj<DashboardService>;
  let mockRouter: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    mockDashboardService = jasmine.createSpyObj('DashboardService', ['getSummary']);
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: DashboardService, useValue: mockDashboardService },
        { provide: Router, useValue: mockRouter },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should show loading then summary with openViolationCount', () => {
    const summary: DashboardSummary = {
      openViolationCount: 3,
      currentBalance: null,
      workOrdersCount: null,
      architectureRequestsCount: null,
    };
    mockDashboardService.getSummary.and.returnValue(of(summary));
    fixture.detectChanges();
    expect(component.loading).toBe(false);
    expect(component.summary).toEqual(summary);
    expect(component.summary?.openViolationCount).toBe(3);
  });

  it('should show error when API returns error', () => {
    mockDashboardService.getSummary.and.returnValue(of({ error: 'Failed to load violation count' }));
    fixture.detectChanges();
    expect(component.loading).toBe(false);
    expect(component.violationError).toBe('Failed to load violation count');
  });

  it('should navigate to my-violations when count is clicked', () => {
    const summary: DashboardSummary = {
      openViolationCount: 2,
      currentBalance: null,
      workOrdersCount: null,
      architectureRequestsCount: null,
    };
    mockDashboardService.getSummary.and.returnValue(of(summary));
    fixture.detectChanges();
    component.goToMyViolations();
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/my-violations']);
  });
});
