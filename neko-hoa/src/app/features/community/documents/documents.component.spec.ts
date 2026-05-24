import { TestBed, ComponentFixture } from '@angular/core/testing';
import { DocumentsComponent } from './documents.component';
import { CommunityService } from '../../../core/services/community.service';

describe('DocumentsComponent', () => {
  let fixture: ComponentFixture<DocumentsComponent>;
  let comp: DocumentsComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentsComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(DocumentsComponent);
    comp = fixture.componentInstance;
    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  it('should create', () => expect(comp).toBeTruthy());

  it('starts with All category active', () => {
    expect(comp.activeCategory()).toBe('All');
  });

  it('shows document table', () => {
    expect(el.querySelector('.data-table')).toBeTruthy();
  });

  it('shows all documents by default', () => {
    const svc = TestBed.inject(CommunityService);
    expect(comp.visibleDocs().length).toBe(svc.getDocuments().length);
  });

  it('filtering by category shows only that category', () => {
    comp.activeCategory.set('Forms');
    fixture.detectChanges();
    comp.visibleDocs().forEach(d => expect(d.category).toBe('Forms'));
  });

  it('search filters documents', () => {
    comp.searchTerm = 'budget';
    comp.onSearch();
    fixture.detectChanges();
    comp.visibleDocs().forEach(d =>
      expect(d.name.toLowerCase()).toContain('budget')
    );
  });

  it('search resets category to All', () => {
    comp.activeCategory.set('Forms');
    comp.searchTerm = 'ACC';
    comp.onSearch();
    expect(comp.activeCategory()).toBe('All');
  });

  it('empty search shows all documents', () => {
    comp.searchTerm = '';
    comp.onSearch();
    fixture.detectChanges();
    const svc = TestBed.inject(CommunityService);
    expect(comp.visibleDocs().length).toBe(svc.getDocuments().length);
  });

  it('shows category tabs', () => {
    expect(el.textContent).toContain('All');
    expect(el.textContent).toContain('Forms');
    expect(el.textContent).toContain('Governing');
  });

  it('totalCount returns all documents', () => {
    const svc = TestBed.inject(CommunityService);
    expect(comp.totalCount).toBe(svc.getDocuments().length);
  });

  it('shows pinned groups in Pinned category', () => {
    comp.activeCategory.set('Pinned');
    fixture.detectChanges();
    expect(el.textContent).toContain('Pinned');
  });
});
