import { TestBed, ComponentFixture } from '@angular/core/testing';
import { DirectoryComponent } from './directory.component';
import { PropertyService } from '../../../core/services/property.service';

describe('DirectoryComponent', () => {
  let fixture: ComponentFixture<DirectoryComponent>;
  let comp: DirectoryComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DirectoryComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(DirectoryComponent);
    comp = fixture.componentInstance;
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(comp).toBeTruthy());

  it('shows directory heading', () => {
    expect(el.textContent).toContain('Directory');
  });

  it('shows all directory fields in table', () => {
    const svc = TestBed.inject(PropertyService);
    const rows = el.querySelectorAll('.data-table tbody tr');
    expect(rows.length).toBe(svc.getDirectoryFields().length);
  });

  it('toggle() flips shared status', () => {
    const initial = comp.fields()[0].shared;
    comp.toggle(comp.fields()[0].key);
    expect(comp.fields()[0].shared).toBe(!initial);
  });

  it('sharedFields() returns only shared fields', () => {
    comp.sharedFields().forEach(f => expect(f.shared).toBeTrue());
  });

  it('directory preview updates when field is toggled on', () => {
    // Turn off all fields first
    comp.fields().forEach(f => {
      if (f.shared) comp.toggle(f.key);
    });
    expect(comp.sharedFields().length).toBe(0);

    // Toggle the first field on
    comp.toggle(comp.fields()[0].key);
    expect(comp.sharedFields().length).toBe(1);
  });

  it('shows privacy promise', () => {
    expect(el.textContent).toContain('Privacy promise');
  });

  it('shows info banner about sharing defaults', () => {
    expect(el.textContent).toContain('Nothing is shared by default');
  });
});
