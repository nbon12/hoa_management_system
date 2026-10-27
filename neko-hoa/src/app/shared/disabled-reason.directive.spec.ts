import { Component } from '@angular/core';
import { render, screen, fireEvent } from '@testing-library/angular';
import { until } from '../features/board/architectural/arc-testing';
import { DISABLED_REASON_HIDE_DELAY_MS, DisabledReasonDirective } from './disabled-reason.directive';

const pause = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

// 030 T063 / R20 / conventions §7.10: a control disabled with a reason stays visible and reachable
// by Tab, is announced as unavailable, and shows its reason on hover AND on keyboard focus.

const REASON = "Keystone Park can't be marked Active yet. It needs a Community Manager with no end date. A pending invitation doesn't count until it's accepted.";

@Component({
  standalone: true,
  imports: [DisabledReasonDirective],
  template: `
    <button type="button" class="btn" aria-describedby="extra-help" [appDisabledReason]="reason" (click)="clicks = clicks + 1">Mark Active</button>
    <p id="extra-help">Marks the community live.</p>
  `,
})
class HostComponent {
  reason: string | null = REASON;
  clicks = 0;
}

describe('DisabledReasonDirective (030 T063)', () => {
  async function setup(reason: string | null = REASON) {
    const r = await render(HostComponent, { componentProperties: { reason } });
    const button = screen.getByRole('button', { name: 'Mark Active' });
    return { r, button, host: r.fixture.componentInstance };
  }

  function tooltip(): HTMLElement {
    return document.querySelector<HTMLElement>('[role="tooltip"]')!;
  }

  it('marks the control aria-disabled, keeps it focusable and enabled for the keyboard', async () => {
    const { button } = await setup();
    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect((button as HTMLButtonElement).disabled).toBeFalse();
    button.focus();
    expect(document.activeElement).toBe(button);
  });

  it('ties the reason to the control with aria-describedby, keeping existing descriptions', async () => {
    const { button } = await setup();
    const tip = tooltip();
    expect(tip.textContent!.trim()).toBe(REASON);
    const ids = button.getAttribute('aria-describedby')!.split(' ');
    expect(ids).toContain('extra-help');
    expect(ids).toContain(tip.id);
  });

  it('shows the reason on hover and hides it when the pointer leaves', async () => {
    const { button } = await setup();
    expect(tooltip().hidden).toBeTrue();
    fireEvent.mouseEnter(button);
    expect(tooltip().hidden).toBeFalse();
    fireEvent.mouseLeave(button);
    await until(() => tooltip().hidden);
  });

  it('keeps the reason open while the pointer moves onto the tooltip', async () => {
    const { button } = await setup();
    fireEvent.mouseEnter(button);
    fireEvent.mouseLeave(button);
    fireEvent.mouseEnter(tooltip());
    await pause(DISABLED_REASON_HIDE_DELAY_MS + 50);
    expect(tooltip().hidden).toBeFalse();
    fireEvent.mouseLeave(tooltip());
    await until(() => tooltip().hidden);
  });

  it('shows the reason on keyboard focus and hides it on blur', async () => {
    const { button } = await setup();
    fireEvent.focus(button);
    expect(tooltip().hidden).toBeFalse();
    fireEvent.blur(button);
    expect(tooltip().hidden).toBeTrue();
  });

  it('Esc hides the reason and leaves focus on the control', async () => {
    const { button } = await setup();
    button.focus();
    fireEvent.focus(button);
    fireEvent.keyDown(button, { key: 'Escape' });
    expect(tooltip().hidden).toBeTrue();
    expect(document.activeElement).toBe(button);
  });

  it('does not run the action while disabled (Enter and Space reach a button as a click)', async () => {
    const { button, host } = await setup();
    fireEvent.click(button);
    fireEvent.click(button, { detail: 0 }); // a keyboard-activated click
    expect(host.clicks).toBe(0);
  });

  it('becomes a normal control again when the reason is cleared', async () => {
    const { r, button, host } = await setup();
    host.reason = null;
    r.fixture.detectChanges();
    expect(button.hasAttribute('aria-disabled')).toBeFalse();
    expect(button.getAttribute('aria-describedby')).toBe('extra-help');
    expect(document.querySelector('[role="tooltip"]')).toBeNull();
    fireEvent.click(button);
    expect(host.clicks).toBe(1);
    fireEvent.mouseEnter(button);
    expect(document.querySelector('[role="tooltip"]')).toBeNull();
  });

  it('adds the reason when one appears later, and updates its text', async () => {
    const { r, button, host } = await setup(null);
    expect(button.hasAttribute('aria-disabled')).toBeFalse();
    host.reason = 'First reason.';
    r.fixture.detectChanges();
    expect(tooltip().textContent!.trim()).toBe('First reason.');
    host.reason = 'Second reason.';
    r.fixture.detectChanges();
    expect(tooltip().textContent!.trim()).toBe('Second reason.');
    expect(document.querySelectorAll('[role="tooltip"]').length).toBe(1);
  });

  it('removes its tooltip when the control goes away', async () => {
    const { r } = await setup();
    r.fixture.destroy();
    expect(document.querySelector('[role="tooltip"]')).toBeNull();
  });
});
