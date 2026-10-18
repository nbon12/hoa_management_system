import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { render, screen, fireEvent } from '@testing-library/angular';
import { ToastHostComponent } from './toast-host.component';
import { DEFAULT_TOAST_MS, ToastService } from '../core/services/toast.service';

// 030 T063 / conventions §7.8, §8: toasts sit in one polite live region, never take focus, need no
// action, and go away by themselves.
describe('ToastHostComponent (030 T063)', () => {
  it('renders a polite live region before any toast arrives', async () => {
    await render(ToastHostComponent);
    const region = screen.getByRole('status');
    expect(region.getAttribute('aria-live')).toBe('polite');
    expect(region.textContent!.trim()).toBe('');
  });

  it('shows a toast inside the live region without taking focus', async () => {
    const r = await render(ToastHostComponent);
    const before = document.activeElement;
    TestBed.inject(ToastService).show('Invitation sent to kim@example.com.');
    r.fixture.detectChanges();
    expect(screen.getByRole('status').textContent).toContain('Invitation sent to kim@example.com.');
    expect(document.activeElement).toBe(before);
    TestBed.inject(ToastService).clear();
  });

  it('auto-dismisses after its duration', fakeAsync(() => {
    const fixture = TestBed.configureTestingModule({ imports: [ToastHostComponent] }).createComponent(ToastHostComponent);
    const toasts = TestBed.inject(ToastService);
    toasts.show('Restore complete.');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Restore complete.');
    tick(DEFAULT_TOAST_MS - 1);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Restore complete.');
    tick(1);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Restore complete.');
  }));

  it('can be dismissed early', async () => {
    const r = await render(ToastHostComponent);
    TestBed.inject(ToastService).show("You're signed out.");
    r.fixture.detectChanges();
    fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));
    r.fixture.detectChanges();
    expect(screen.getByRole('status').textContent).not.toContain("You're signed out.");
  });
});

describe('ToastService', () => {
  beforeEach(() => TestBed.configureTestingModule({}));

  it('lists toasts in order and dismisses by id', () => {
    const svc = TestBed.inject(ToastService);
    const a = svc.show('One');
    svc.show('Two', { durationMs: 10_000 });
    expect(svc.toasts().map(t => t.message)).toEqual(['One', 'Two']);
    svc.dismiss(a);
    expect(svc.toasts().map(t => t.message)).toEqual(['Two']);
    svc.dismiss(999);
    svc.clear();
    expect(svc.toasts()).toEqual([]);
  });

  it('honours a custom duration', fakeAsync(() => {
    const svc = TestBed.inject(ToastService);
    svc.show('Quick', { durationMs: 100 });
    tick(100);
    expect(svc.toasts()).toEqual([]);
  }));
});
