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
    spyOn(window, 'open').and.returnValue(null);
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

  it('openDocument() requests presigned url and opens it in a new tab', async () => {
    const svc = TestBed.inject(CommunityService) as jasmine.SpyObj<CommunityService>;
    const openSpy = window.open as jasmine.Spy;
    const mockTab = { location: { href: '' }, close: jasmine.createSpy('close') };
    openSpy.and.returnValue(mockTab as any);

    await comp.openDocument(MOCK_DOCS[0]);

    expect(openSpy).toHaveBeenCalledWith('about:blank', '_blank');
    expect(svc.getDocumentDownloadUrl).toHaveBeenCalledWith('d1');
    expect(mockTab.location.href).toBe('http://minio.test/hoa-documents/test.pdf');
  });

  // 020-D FR-D6 (T020): the pre-opened tab's opener must be severed BEFORE navigation — the
  // reverse order leaves a window where the target page can script window.opener.
  it('nulls the opener before assigning the target URL', async () => {
    const openSpy = window.open as jasmine.Spy;
    const order: string[] = [];
    const fakeTab: any = { close: jasmine.createSpy('close'), location: {} };
    Object.defineProperty(fakeTab.location, 'href', { set: () => order.push('href') });
    Object.defineProperty(fakeTab, 'opener', { set: () => order.push('opener') });
    openSpy.and.returnValue(fakeTab);

    await comp.openDocument(MOCK_DOCS[0]);

    expect(order).toEqual(['opener', 'href']);
  });

  it('falls back to noopener,noreferrer when the tab could not be pre-opened', async () => {
    const openSpy = window.open as jasmine.Spy;
    openSpy.and.returnValue(null);

    await comp.openDocument(MOCK_DOCS[0]);

    expect(openSpy.calls.mostRecent().args).toEqual(
      ['http://minio.test/hoa-documents/test.pdf', '_blank', 'noopener,noreferrer']);
  });

  it('clicking document name opens PDF in a new tab', async () => {
    const svc = TestBed.inject(CommunityService) as jasmine.SpyObj<CommunityService>;
    const openSpy = window.open as jasmine.Spy;
    const mockTab = { location: { href: '' }, close: jasmine.createSpy('close') };
    openSpy.and.returnValue(mockTab as any);

    const nameButton = el.querySelector('.data-table tbody tr .doc-name-link') as HTMLButtonElement;
    expect(nameButton?.textContent?.trim()).toBe('2026 HOA Budget');
    nameButton.click();
    await fixture.whenStable();

    expect(openSpy).toHaveBeenCalledWith('about:blank', '_blank');
    expect(svc.getDocumentDownloadUrl).toHaveBeenCalledWith('d1');
    expect(mockTab.location.href).toBe('http://minio.test/hoa-documents/test.pdf');
  });
});
