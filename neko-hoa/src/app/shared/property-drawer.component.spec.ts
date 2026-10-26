import { Component, signal } from '@angular/core';
import { render, screen, fireEvent } from '@testing-library/angular';
import { PropertyDrawerComponent } from './property-drawer.component';
import { ViewportService } from '../core/services/viewport.service';

// 030 T063 / brief 06 / conventions §8 "Side panels": the property drawer follows spec 025's
// glossary panel. It opens beside the list (full width below 768 px), focus moves in, and Esc
// closes it and returns focus to the row that opened it.

@Component({
  standalone: true,
  imports: [PropertyDrawerComponent],
  template: `
    <div style="display:flex;width:900px;">
      <ul class="list" style="flex:1;margin:0;padding:0;">
        @for (row of rows; track row) {
          <li><button type="button" (click)="open = row">{{ row }}</button></li>
        }
      </ul>
      @if (open) {
        <app-property-drawer [heading]="open" (close)="open = null">
          <p>Residents of {{ open }}</p>
        </app-property-drawer>
      }
    </div>
  `,
})
class HostComponent {
  rows = ['711 Keystone Park Dr #29', '713 Keystone Park Dr #31'];
  open: string | null = null;
}

describe('PropertyDrawerComponent (030 T063)', () => {
  const narrow = signal(false);

  async function setup(isNarrow = false) {
    narrow.set(isNarrow);
    const r = await render(HostComponent, {
      providers: [{ provide: ViewportService, useValue: { isNarrow: narrow.asReadonly() } }],
    });
    return r;
  }

  async function openRow(r: Awaited<ReturnType<typeof setup>>, name: string) {
    const row = screen.getByRole('button', { name });
    row.focus();
    fireEvent.click(row);
    r.fixture.detectChanges();
    await r.fixture.whenStable();
    return row;
  }

  it('opens as a labelled panel beside the list and moves focus in', async () => {
    const r = await setup();
    await openRow(r, '711 Keystone Park Dr #29');
    const drawer = screen.getByRole('dialog', { name: '711 Keystone Park Dr #29' });
    expect(drawer.getAttribute('aria-modal')).toBe('false');
    expect(screen.getByText('Residents of 711 Keystone Park Dr #29')).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByRole('heading', { name: '711 Keystone Park Dr #29' }));

    const list = document.querySelector('.list')!.getBoundingClientRect();
    const panel = drawer.getBoundingClientRect();
    expect(getComputedStyle(drawer).position).not.toBe('fixed');
    expect(panel.left).toBeGreaterThanOrEqual(list.right - 1);
    expect(panel.width).toBeLessThan(900);
  });

  it('opens full width below 768 px', async () => {
    const r = await setup(true);
    await openRow(r, '711 Keystone Park Dr #29');
    const drawer = screen.getByRole('dialog', { name: '711 Keystone Park Dr #29' });
    expect(drawer.classList).toContain('drawer--full');
    expect(getComputedStyle(drawer).position).toBe('fixed');
    expect(Math.round(drawer.getBoundingClientRect().width)).toBe(document.documentElement.clientWidth);
  });

  it('Esc closes it and returns focus to the row', async () => {
    const r = await setup();
    const row = await openRow(r, '713 Keystone Park Dr #31');
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
    r.fixture.detectChanges();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(document.activeElement).toBe(row);
  });

  it('the close button closes it and returns focus to the row', async () => {
    const r = await setup();
    const row = await openRow(r, '711 Keystone Park Dr #29');
    fireEvent.click(screen.getByRole('button', { name: 'Close 711 Keystone Park Dr #29' }));
    r.fixture.detectChanges();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(document.activeElement).toBe(row);
  });
});
