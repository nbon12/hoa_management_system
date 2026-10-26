import { Component } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { render, screen, fireEvent } from '@testing-library/angular';
import { until } from '../features/board/architectural/arc-testing';
import { AutosaveFieldDirective } from './autosave-field.directive';

// 030 T063 / R14 / brief 04 §6: profile fields save one at a time when the person leaves them.
// "Saving…" then "✓ Saved"; a failure shows the server's message under the field and keeps what
// was typed; the status is announced politely.

interface Deferred { resolve: (v?: unknown) => void; reject: (e: unknown) => void; }

@Component({
  standalone: true,
  imports: [FormsModule, AutosaveFieldDirective],
  template: `
    <label for="county" #countyLabel>County (required)</label>
    <input id="county" name="county" [(ngModel)]="county"
           [appAutosaveField]="save" autosaveLabel="County" [autosaveSavedMs]="savedMs" [autosaveStatusHost]="countyLabel" />
    <label for="tz">Time zone</label>
    <select id="tz" name="tz" [(ngModel)]="tz" [appAutosaveField]="save" autosaveLabel="Time zone" [autosaveBaseline]="savedTz">
      <option value="America/New_York">America/New_York</option>
      <option value="America/Chicago">America/Chicago</option>
    </select>
  `,
})
class HostComponent {
  county = 'Wake County, NC';
  tz = 'America/New_York';
  savedTz = 'America/New_York';
  savedMs = 3000;
  calls: string[] = [];
  pending: Deferred[] = [];
  save = (value: string) => {
    this.calls.push(value);
    return new Promise((resolve, reject) => this.pending.push({ resolve, reject }));
  };
}

describe('AutosaveFieldDirective (030 T063)', () => {
  async function setup(savedMs = 3000) {
    const r = await render(HostComponent, { imports: [FormsModule], componentProperties: { savedMs } });
    await r.fixture.whenStable();
    const host = r.fixture.componentInstance;
    const input = screen.getByLabelText('County (required)', { selector: 'input' }) as HTMLInputElement;
    return { r, host, input };
  }

  function status(): HTMLElement {
    // Beside the label, not inside it, so the field's accessible name stays "County (required)".
    return document.querySelector<HTMLElement>('label[for="county"] + .autosave-status')!;
  }

  function edit(input: HTMLInputElement, value: string) {
    fireEvent.focus(input);
    fireEvent.input(input, { target: { value } });
    fireEvent.blur(input);
  }

  it('does not save when the field is left unchanged', async () => {
    const { host, input } = await setup();
    fireEvent.focus(input);
    fireEvent.blur(input);
    expect(host.calls).toEqual([]);
  });

  it('saves on blur with the typed value, showing "Saving…" then "✓ Saved" in a polite status', async () => {
    const { host, input } = await setup();
    edit(input, 'Durham County, NC');
    expect(host.calls).toEqual(['Durham County, NC']);
    expect(input.labels![0].textContent!.trim()).toBe('County (required)');
    expect(status().getAttribute('role')).toBe('status');
    expect(status().getAttribute('aria-live')).toBe('polite');
    expect(status().textContent).toContain('Saving…');

    host.pending[0].resolve();
    await until(() => status().textContent!.includes('✓ Saved'));
    // Screen readers hear which field was saved.
    expect(status().textContent).toContain('County saved');

    // Leaving it again without a change saves nothing more.
    fireEvent.focus(input);
    fireEvent.blur(input);
    expect(host.calls.length).toBe(1);
  });

  it('clears "✓ Saved" after a few seconds', async () => {
    const { host, input } = await setup(30);
    edit(input, 'Durham County, NC');
    host.pending[0].resolve();
    await until(() => status().textContent!.includes('✓ Saved'));
    await until(() => status().textContent!.trim() === '');
    expect(status().textContent).not.toContain('Saved');
  });

  it('saves a select on change against the baseline it is given', async () => {
    const { host } = await setup();
    const select = screen.getByLabelText('Time zone') as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'America/New_York' } });
    expect(host.calls).toEqual([]);
    fireEvent.change(select, { target: { value: 'America/Chicago' } });
    expect(host.calls).toEqual(['America/Chicago']);
    host.pending[0].resolve();
  });

  it("shows the server's message under the field, keeps the typed value and marks it invalid", async () => {
    const { r, host, input } = await setup();
    edit(input, '');
    host.pending[0].reject(new HttpErrorResponse({
      status: 422,
      error: { code: 'VALIDATION_ERROR', message: 'Validation failed.', errors: [{ field: 'county', message: "County is required and can't be left empty." }] },
    }));
    await until(() => !!screen.queryByText("County is required and can't be left empty."));
    await r.fixture.whenStable();

    const error = screen.getByText("County is required and can't be left empty.");
    expect(error.getAttribute('role')).toBe('alert');
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(input.getAttribute('aria-describedby')).toContain(error.id);
    expect(input.value).toBe('');
    expect(host.county).toBe('');
    expect(status().textContent).not.toContain('Saved');
  });

  it('uses the server message for a refusal, and offers Try again after a network failure', async () => {
    const { host, input } = await setup();
    edit(input, 'Keystone Park');
    host.pending[0].reject(new HttpErrorResponse({ status: 409, error: { code: 'COMMUNITY_NAME_TAKEN', message: 'That community name is unavailable.' } }));
    await until(() => !!screen.queryByText('That community name is unavailable.'));
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();

    edit(input, 'Keystone Pines');
    host.pending[1].reject(new HttpErrorResponse({ status: 0 }));
    await until(() => !!screen.queryByText("Couldn't save. Check your connection and try again."));
    expect(screen.queryByText('That community name is unavailable.')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(host.calls).toEqual(['Keystone Park', 'Keystone Pines', 'Keystone Pines']);
    host.pending[2].resolve();
    await until(() => !screen.queryByText("Couldn't save. Check your connection and try again."));
    expect(input.hasAttribute('aria-invalid')).toBeFalse();
  });

  it('Esc puts back the saved value and clears the error', async () => {
    const { r, host, input } = await setup();
    edit(input, '');
    host.pending[0].reject(new HttpErrorResponse({ status: 422, error: { code: 'VALIDATION_ERROR', message: "County is required and can't be left empty." } }));
    await until(() => !!screen.queryByText("County is required and can't be left empty."));
    fireEvent.keyDown(input, { key: 'Escape' });
    await r.fixture.whenStable();
    expect(input.value).toBe('Wake County, NC');
    expect(host.county).toBe('Wake County, NC');
    expect(screen.queryByText("County is required and can't be left empty.")).toBeNull();
  });

  it('saves the latest value after a save that was still running', async () => {
    const { host, input } = await setup();
    edit(input, 'One');
    edit(input, 'Two');
    expect(host.calls).toEqual(['One']);
    host.pending[0].resolve();
    await until(() => host.calls.length === 2);
    expect(host.calls).toEqual(['One', 'Two']);
    host.pending[1].resolve();
  });

  it('uses a custom failure message, and leaving the field at its saved value clears the error', async () => {
    const { r, host, input } = await setup();
    const dir = r.fixture.debugElement.query(el => el.nativeElement === input).injector.get(AutosaveFieldDirective);
    dir.autosaveErrorMessage = () => 'Custom words.';
    edit(input, 'Elsewhere');
    host.pending[0].reject(new HttpErrorResponse({ status: 500 }));
    await until(() => !!screen.queryByText(/Custom words\./));
    expect(dir.state()).toBe('error');
    expect(dir.error()).toBe('Custom words.');
    edit(input, 'Wake County, NC');
    expect(host.calls.length).toBe(1);
    expect(screen.queryByText(/Custom words\./)).toBeNull();
    expect(dir.state()).toBe('idle');
  });

  it('commit() works before the field was ever focused, and Esc does nothing then', async () => {
    const { r, host, input } = await setup();
    const dir = r.fixture.debugElement.query(el => el.nativeElement === input).injector.get(AutosaveFieldDirective);
    fireEvent.keyDown(input, { key: 'Escape' });
    expect(input.value).toBe('Wake County, NC');
    await dir.commit();
    expect(host.calls).toEqual([]);
  });

  it('removes what it added when the field goes away', async () => {
    const { r, host, input } = await setup();
    edit(input, 'x');
    host.pending[0].reject(new HttpErrorResponse({ status: 0 }));
    await until(() => !!document.querySelector('.autosave-error'));
    r.fixture.destroy();
    expect(document.querySelector('.autosave-error')).toBeNull();
    expect(document.querySelector('.autosave-status')).toBeNull();
  });
});
