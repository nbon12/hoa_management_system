import { TestBed, ComponentFixture } from '@angular/core/testing';
import { DocumentsComponent } from './documents.component';
import { CommunityService } from '../../../core/services/community.service';
import { HOADocument } from '../../../core/models';

const MOCK_DOCS: HOADocument[] = [
  { id: 'd1', name: '2026 HOA Budget',         category: 'Budgets',   effectiveDate: '2026-01-01', fileSizeLabel: '1.2 MB', pinned: true,  url: null },
  { id: 'd2', name: 'Community Rules',          category: 'Rules',     effectiveDate: '2024-03-15', fileSizeLabel: '3.0 MB', pinned: true,  url: null },
  { id: 'd3', name: 'ARC Form',                 category: 'Forms',     effectiveDate: '2025-01-01', fileSizeLabel: '512 KB', pinned: false, url: null },
  { id: 'd4', name: 'Pet Registration Form',    category: 'Forms',     effectiveDate: '2025-01-01', fileSizeLabel: '200 KB', pinned: false, url: null },
  { id: 'd5', name: 'Master Insurance Policy',  category: 'Insurance', effectiveDate: '2026-01-01', fileSizeLabel: '1.5 MB', pinned: false, url: null },
];

function makeMockCommunityService(): Partial<CommunityService> {
  return {
    getDocuments:    jasmine.createSpy().and.returnValue(Promise.resolve([...MOCK_DOCS])),
    searchDocuments: jasmine.createSpy().and.callFake((q: string) =>
      Promise.resolve(MOCK_DOCS.filter(d => d.name.toLowerCase().includes(q.toLowerCase())))
    ),
    getDocumentDownloadUrl: jasmine.createSpy().and.returnValue(
      Promise.resolve({ url: 'http://minio.test/hoa-documents/test.pdf', expiresAt: '2026-05-25T12:00:00Z' }),
    ),
  } as any;
}

describe('DocumentsComponent', () => {
  let fixture: ComponentFixture<DocumentsComponent>;
  let comp: DocumentsComponent;
  let el: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports:   [DocumentsComponent],
      providers: [{ provide: CommunityService, useValue: makeMockCommunityService() }],
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentsComponent);
    comp    = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
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
    expect(comp.visibleDocs().length).toBe(MOCK_DOCS.length);
  });

  it('shows category tabs', () => {
    expect(el.textContent).toContain('All');
    expect(el.textContent).toContain('Forms');
    expect(el.textContent).toContain('Governing');
  });

  it('download() requests presigned url and opens it', async () => {
    const svc = TestBed.inject(CommunityService) as jasmine.SpyObj<CommunityService>;
    const openSpy = spyOn(window, 'open');

    await comp.download(MOCK_DOCS[0]);

    expect(svc.getDocumentDownloadUrl).toHaveBeenCalledWith('d1');
    expect(openSpy).toHaveBeenCalledWith(
      'http://minio.test/hoa-documents/test.pdf',
      '_blank',
      'noopener,noreferrer',
    );
  });
});
