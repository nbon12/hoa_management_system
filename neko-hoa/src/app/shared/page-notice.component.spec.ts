import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { screen, fireEvent } from '@testing-library/angular';
import { PageNoticeComponent } from './page-notice.component';
import { PageNoticeService } from '../core/services/page-notice.service';

// 030 conventions §6.1 / brief 11 §7: the page notice is a polite live region, stays until
// dismissed, and turns a known `?notice=` key into its words.

@Component({ standalone: true, imports: [PageNoticeComponent], template: '<app-page-notice /><h1>Page</h1>' })
class PageComponent {}

describe('PageNoticeComponent (030)', () => {
  async function open(url: string) {
    TestBed.configureTestingModule({ providers: [provideRouter([{ path: '**', component: PageComponent }])] });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    await harness.fixture.whenStable();
    harness.detectChanges();
    return harness;
  }

  afterEach(() => TestBed.inject(PageNoticeService).dismiss());

  it('shows the notice for a known key, then removes the key from the address', async () => {
    await open('/app/board/communities?notice=not-company-admin&tab=1');
    expect(screen.getByText("This account isn't a Company Administrator, so you've been signed in to your usual page.")).toBeTruthy();
    expect(TestBed.inject(Router).url).toBe('/app/board/communities?tab=1');
    expect(screen.getByRole('status').getAttribute('aria-live')).toBe('polite');
  });

  it('ignores an unknown key but still removes it', async () => {
    await open('/app/dashboard?notice=whatever');
    expect(screen.getByRole('status').textContent!.trim()).toBe('');
    expect(TestBed.inject(Router).url).toBe('/app/dashboard');
  });

  it('stays until dismissed', async () => {
    const harness = await open('/app/dashboard');
    TestBed.inject(PageNoticeService).show('You no longer have access to that home.');
    harness.detectChanges();
    await harness.navigateByUrl('/app/payments/statement');
    harness.detectChanges();
    expect(screen.getByText('You no longer have access to that home.')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Dismiss message' }));
    harness.detectChanges();
    expect(screen.queryByText('You no longer have access to that home.')).toBeNull();
  });
});
