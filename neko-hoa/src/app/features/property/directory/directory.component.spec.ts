import { TestBed, ComponentFixture } from '@angular/core/testing';
import { DirectoryComponent } from './directory.component';
import { PropertyService } from '../../../core/services/property.service';
import { CommunityService } from '../../../core/services/community.service';
import { DirectoryField } from '../../../core/models';

const MOCK_FIELDS: DirectoryField[] = [
  { key: 'name',    label: 'Full Name', shared: true,  value: 'Jane Resident' },
  { key: 'email',   label: 'Email',     shared: false, value: 'jane@example.com' },
  { key: 'phone',   label: 'Phone',     shared: false, value: '408-555-0101' },
  { key: 'address', label: 'Address',   shared: true,  value: '1 Sakura Drive' },
];

const MOCK_DIRECTORY = { neighbors: [], totalSharing: 0, totalHouseholds: 248 };

function makeMockPropertyService(): Partial<PropertyService> {
  return {
    getDirectoryFields:   jasmine.createSpy().and.returnValue(Promise.resolve([...MOCK_FIELDS])),
    toggleDirectoryField: jasmine.createSpy().and.returnValue(Promise.resolve()),
  } as any;
}

function makeMockCommunityService(): Partial<CommunityService> {
  return {
    getCommunityDirectory: jasmine.createSpy().and.returnValue(Promise.resolve(MOCK_DIRECTORY)),
  } as any;
}

describe('DirectoryComponent', () => {
  let fixture: ComponentFixture<DirectoryComponent>;
  let comp: DirectoryComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports:   [DirectoryComponent],
      providers: [
        { provide: PropertyService,  useValue: makeMockPropertyService() },
        { provide: CommunityService, useValue: makeMockCommunityService() },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DirectoryComponent);
    comp    = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(comp).toBeTruthy());

  it('shows directory heading', () => {
    expect(el.textContent).toContain('Directory');
  });

  it('shows all directory fields in table', () => {
    const rows = el.querySelectorAll('.data-table tbody tr');
    expect(rows.length).toBe(MOCK_FIELDS.length);
  });

  it('sharedFields() returns only shared fields', () => {
    comp.sharedFields().forEach(f => expect(f.shared).toBeTrue());
  });

  it('sharedFields() initially returns 2 shared fields', () => {
    expect(comp.sharedFields().length).toBe(2);
  });

  it('shows privacy promise', () => {
    expect(el.textContent).toContain('Privacy promise');
  });

  it('shows info banner about sharing defaults', () => {
    expect(el.textContent).toContain('Nothing is shared by default');
  });
});
