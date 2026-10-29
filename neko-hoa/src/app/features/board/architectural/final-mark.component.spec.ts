import { render, screen } from '@testing-library/angular';
import { FinalMarkComponent } from './final-mark.component';

describe('FinalMarkComponent (031 item 8)', () => {
  // 031 US1-S6: "Given finalized decisions, Then the Final decision column shows ✓ for approved, ✕ for denied and a
  // yellow ↻ for revisions requested, each with a text label."
  it('renders ✓ / ✕ / ↻ with labels', async () => {
    const r = await render(FinalMarkComponent, { componentInputs: { decision: { outcome: 'Approved', wording: null, source: 'Votes' } } });
    expect(screen.getByRole('img', { name: 'Final decision: Application Approved' }).textContent!.trim()).toBe('✓');

    r.fixture.componentRef.setInput('decision', { outcome: 'Denied', wording: 'Denied', source: 'Votes' });
    r.fixture.detectChanges();
    expect(screen.getByRole('img', { name: 'Final decision: Denied' }).textContent!.trim()).toBe('✕');

    r.fixture.componentRef.setInput('decision', { outcome: 'Denied', wording: 'RevisionsRequested', source: 'Votes' });
    r.fixture.detectChanges();
    const mark = screen.getByRole('img', { name: /Revisions Requested/ });
    expect(mark.textContent!.trim()).toBe('↻');
    expect(mark.classList).toContain('final-mark--revisions');
  });

  it('renders nothing without a decision', async () => {
    await render(FinalMarkComponent, { componentInputs: { decision: null } });
    expect(screen.queryByRole('img')).toBeNull();
  });
});
