import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { MyViolationsComponent } from './my-violations.component';
import { MyViolationsService } from '../../services/my-violations.service';
import { MyViolationsResponse } from '../../models/my-violations.model';

describe('MyViolationsComponent', () => {
  let component: MyViolationsComponent;
  let fixture: ComponentFixture<MyViolationsComponent>;
  let mockMyViolationsService: jasmine.SpyObj<MyViolationsService>;

  beforeEach(async () => {
    mockMyViolationsService = jasmine.createSpyObj('MyViolationsService', ['getMine']);

    await TestBed.configureTestingModule({
      imports: [MyViolationsComponent],
      providers: [
        provideRouter([]),
        { provide: MyViolationsService, useValue: mockMyViolationsService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(MyViolationsComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load and display violations', () => {
    const response: MyViolationsResponse = {
      items: [
        {
          id: 'id1',
          description: 'Lawn overgrown',
          occurrenceDate: '2025-01-15T00:00:00Z',
          violationTypeName: 'GRASS',
          propertyDisplayName: '123 Main St',
        },
      ],
      totalCount: 1,
    };
    mockMyViolationsService.getMine.and.returnValue(of(response));
    fixture.detectChanges();
    expect(component.loading).toBe(false);
    expect(component.items.length).toBe(1);
    expect(component.items[0].description).toBe('Lawn overgrown');
    expect(component.totalCount).toBe(1);
  });

  it('should show error when API fails', () => {
    mockMyViolationsService.getMine.and.returnValue(of({ error: 'Failed to load violations' }));
    fixture.detectChanges();
    expect(component.loading).toBe(false);
    expect(component.error).toBe('Failed to load violations');
  });

  it('formatDate should format ISO string', () => {
    expect(component.formatDate('2025-03-14T12:00:00Z')).toMatch(/\d/);
  });
});
