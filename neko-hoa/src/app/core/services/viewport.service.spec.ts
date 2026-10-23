import { TestBed } from '@angular/core/testing';
import { NARROW_QUERY, ViewportService } from './viewport.service';

// 030: the 768 px breakpoint as a signal.
describe('ViewportService', () => {
  let listeners: ((e: MediaQueryListEvent) => void)[];
  let removed: number;

  function stubMatchMedia(matches: boolean) {
    listeners = [];
    removed = 0;
    spyOn(window, 'matchMedia').and.callFake((query: string) => ({
      matches, media: query,
      addEventListener: (_: string, fn: (e: MediaQueryListEvent) => void) => listeners.push(fn),
      removeEventListener: () => { removed++; },
    }) as unknown as MediaQueryList);
  }

  it('reads the narrow query at start and follows its changes', () => {
    stubMatchMedia(true);
    TestBed.configureTestingModule({});
    const svc = TestBed.inject(ViewportService);
    expect(window.matchMedia).toHaveBeenCalledWith(NARROW_QUERY);
    expect(svc.isNarrow()).toBeTrue();
    listeners[0]({ matches: false } as MediaQueryListEvent);
    expect(svc.isNarrow()).toBeFalse();
    TestBed.resetTestingModule();
    expect(removed).toBe(1);
  });

  it('is wide when matchMedia is not available', () => {
    const original = window.matchMedia;
    (window as unknown as { matchMedia: unknown }).matchMedia = undefined;
    try {
      TestBed.configureTestingModule({});
      expect(TestBed.inject(ViewportService).isNarrow()).toBeFalse();
    } finally {
      window.matchMedia = original;
    }
  });

  it('is wide when the viewport is wide', () => {
    stubMatchMedia(false);
    TestBed.configureTestingModule({});
    expect(TestBed.inject(ViewportService).isNarrow()).toBeFalse();
  });
});
