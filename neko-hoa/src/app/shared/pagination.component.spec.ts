import { render, screen, fireEvent, within } from '@testing-library/angular';
import { PaginationComponent } from './pagination.component';

// 030 conventions §7.6: "Showing 1–25 of 120", Previous, page numbers, Next; 25 rows by default.
describe('PaginationComponent (030 conventions §7.6)', () => {
  async function setup(totalItems: number, offset = 0, limit = 25) {
    const r = await render(PaginationComponent, { componentInputs: { totalItems, limit, offset, noun: 'communities' } });
    const changes: number[] = [];
    r.fixture.componentInstance.pageChange.subscribe(o => changes.push(o));
    return { r, changes };
  }

  it('shows the range and announces it politely', async () => {
    await setup(31);
    const summary = screen.getByRole('status');
    expect(summary.textContent!.replace(/\s+/g, ' ').trim()).toBe('Showing 1–25 of 31 communities.');
    expect(summary.getAttribute('aria-live')).toBe('polite');
  });

  it('has Previous (disabled on page 1), the page numbers and Next, in a "Pages" landmark', async () => {
    const { changes } = await setup(31);
    const nav = screen.getByRole('navigation', { name: 'Pages' });
    expect((within(nav).getByRole('button', { name: 'Previous page' }) as HTMLButtonElement).disabled).toBeTrue();
    expect(within(nav).getByRole('button', { name: 'Page 1' }).getAttribute('aria-current')).toBe('page');
    fireEvent.click(within(nav).getByRole('button', { name: 'Next page' }));
    fireEvent.click(within(nav).getByRole('button', { name: 'Page 2' }));
    fireEvent.click(within(nav).getByRole('button', { name: 'Page 1' }));
    expect(changes).toEqual([25, 25]);
  });

  it('shows the last page range and disables Next there', async () => {
    const { changes } = await setup(31, 25);
    expect(screen.getByRole('status').textContent).toContain('Showing 26–31 of 31');
    expect((screen.getByRole('button', { name: 'Next page' }) as HTMLButtonElement).disabled).toBeTrue();
    fireEvent.click(screen.getByRole('button', { name: 'Previous page' }));
    expect(changes).toEqual([0]);
  });

  it('shows no page buttons for a single page, and nothing for an empty list', async () => {
    const { r } = await setup(5);
    expect(screen.getByRole('status').textContent).toContain('Showing 1–5 of 5');
    expect(screen.queryByRole('navigation')).toBeNull();
    r.fixture.componentRef.setInput('totalItems', 0);
    r.fixture.detectChanges();
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('shortens a long run of pages around the current one', async () => {
    await setup(500, 250);
    const labels = within(screen.getByRole('navigation')).getAllByRole('button').map(b => b.textContent!.trim());
    expect(labels).toEqual(['Previous', '1', '9', '10', '11', '12', '13', '20', 'Next']);
    expect(screen.getAllByText('…').length).toBe(2);
  });
});
