import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { render, screen, within } from '@testing-library/angular';
import { MyRequestsPageComponent } from './my-requests-page.component';
import { ResidentArchitecturalService } from '../../../core/services/resident-architectural.service';
import { LIST_ITEMS } from './resident-arc-fixtures';
import { until } from '../../board/architectural/arc-testing';

describe('MyRequestsPageComponent (029 US2)', () => {
  async function setup(result: Promise<unknown>) {
    const api = jasmine.createSpyObj<ResidentArchitecturalService>('ResidentArchitecturalService', ['list']);
    api.list.and.returnValue(result as never);
    await render(MyRequestsPageComponent, {
      providers: [provideRouter([]), { provide: ResidentArchitecturalService, useValue: api }],
    });
    await until(() => api.list.calls.count() > 0);
    return api;
  }

  // US2 AS1: each request shows with its status label (text, not just color).
  it('lists drafts and requests with their status labels and links', async () => {
    await setup(Promise.resolve({ items: LIST_ITEMS, total: 3, limit: 25, offset: 0 }));
    await until(() => !!screen.queryByText('Fence replacement — 6ft cedar'));

    const list = screen.getByRole('list', { name: 'Architectural requests' });
    const rows = within(list).getAllByRole('link');
    expect(rows.length).toBe(3);
    expect(rows[0].getAttribute('href')).toBe('/app/property/architectural/drafts/d1');
    expect(rows[0].textContent).toContain('Draft');
    expect(rows[0].textContent).toContain('Not submitted yet');
    expect(rows[1].getAttribute('href')).toBe('/app/property/architectural/a1');
    expect(rows[1].textContent).toContain('ARC-1042');
    expect(rows[1].textContent).toContain('More info requested');
    expect(rows[1].textContent).toContain('Decision due 06/27/26');
    expect(rows[2].textContent).toContain('v2');
    expect(rows[2].textContent).toContain('Approved');
  });

  it('shows an empty state with a way to start', async () => {
    await setup(Promise.resolve({ items: [], total: 0, limit: 25, offset: 0 }));
    await until(() => !!screen.queryByText("You haven't filed any architectural requests yet."));
    expect(screen.getByRole('link', { name: 'New request' }).getAttribute('href')).toBe('/app/property/architectural/new');
  });

  it('shows an error when the list fails', async () => {
    await setup(Promise.reject(new HttpErrorResponse({ status: 500 })));
    await until(() => !!screen.queryByRole('alert'));
    expect(screen.getByRole('alert').textContent).toContain('could not be loaded');
  });
});
