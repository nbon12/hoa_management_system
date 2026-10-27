import {
  Directive, ElementRef, EventEmitter, HostListener, Input, NgZone, OnChanges, OnDestroy, Output, Renderer2,
  SimpleChanges, inject, signal,
} from '@angular/core';
import { SAVE_FAILED_MESSAGE, refusalCodeOf, refusalMessage, serverMessageOf } from './refusal-messages';

type FieldElement = HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement;
export type AutosaveState = 'idle' | 'saving' | 'saved' | 'error';

let nextId = 1;

/**
 * 030 R14 / brief 04 §6 (owner decision 2026-10-14): a field that saves on its own when the person
 * finishes it — on blur for text, on change for selects, dates and checkboxes — and only when the
 * value differs from the last saved one.
 *
 * - While saving: "Saving…"; then "✓ Saved" for a few seconds (screen readers hear "County saved"),
 *   in a polite live region placed after `autosaveStatusHost` (the field's label) or after the field.
 * - On failure: the server's message under the field (`role="alert"`, tied by `aria-describedby`,
 *   `aria-invalid`), and the typed value is kept. A network failure adds "Try again".
 * - Esc puts back the saved value.
 *
 * Usage: `<input [(ngModel)]="draft.county" [appAutosaveField]="saveCounty"
 *          [autosaveBaseline]="profile().county" autosaveLabel="County" [autosaveStatusHost]="countyLabel" />`
 */
@Directive({
  selector: '[appAutosaveField]',
  standalone: true,
  exportAs: 'autosaveField',
})
export class AutosaveFieldDirective implements OnChanges, OnDestroy {
  /** Saves one value; resolves on success, rejects with the HTTP error otherwise. */
  @Input({ alias: 'appAutosaveField', required: true }) save!: (value: string) => Promise<unknown>;
  /** The field's name for announcements: "County" → "County saved". */
  @Input() autosaveLabel = '';
  /** The saved value. When not bound, the value at first focus is used. */
  @Input() autosaveBaseline: string | null | undefined;
  /** When to save. Defaults to `change` for selects, dates and checkboxes, `blur` otherwise. */
  @Input() autosaveOn: 'blur' | 'change' | undefined;
  /** The status goes right after this element (usually the field's label); default: after the field. */
  @Input() autosaveStatusHost: HTMLElement | null | undefined;
  /** How long "✓ Saved" stays. */
  @Input() autosaveSavedMs = 3000;
  /** Overrides the words shown for a failure. */
  @Input() autosaveErrorMessage: ((err: unknown) => string) | undefined;

  @Output() readonly autosaveSaved = new EventEmitter<unknown>();
  @Output() readonly autosaveFailed = new EventEmitter<unknown>();

  readonly state = signal<AutosaveState>('idle');
  readonly error = signal<string | null>(null);

  private host = inject<ElementRef<FieldElement>>(ElementRef);
  private renderer = inject(Renderer2);
  private zone = inject(NgZone);

  private readonly errorId = `autosave-error-${nextId++}`;
  private baseline: string | null = null;
  private hasBaseline = false;
  private saving = false;
  private queued = false;
  private statusEl: HTMLElement | null = null;
  private errorEl: HTMLElement | null = null;
  private savedTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['autosaveBaseline'] && this.autosaveBaseline !== undefined) {
      this.baseline = this.autosaveBaseline ?? '';
      this.hasBaseline = true;
    }
  }

  ngOnDestroy(): void {
    if (this.savedTimer) clearTimeout(this.savedTimer);
    this.statusEl?.remove();
    this.errorEl?.remove();
  }

  @HostListener('focus')
  onFocus(): void {
    if (!this.hasBaseline) {
      this.baseline = this.value();
      this.hasBaseline = true;
    }
  }

  @HostListener('blur')
  onBlur(): void {
    if (this.trigger() === 'blur') void this.commit();
  }

  @HostListener('change')
  onChange(): void {
    if (this.trigger() === 'change') void this.commit();
  }

  @HostListener('keydown.escape')
  onEscape(): void {
    if (!this.hasBaseline) return;
    const el = this.host.nativeElement;
    el.value = this.baseline ?? '';
    // Tell value accessors (ngModel) about the restored value.
    el.dispatchEvent(new Event(el instanceof HTMLSelectElement ? 'change' : 'input', { bubbles: true }));
    this.clearError();
    this.setStatus('idle');
  }

  /** Saves the current value now if it changed. */
  async commit(): Promise<void> {
    if (!this.hasBaseline) this.onFocus();
    const value = this.value();
    if (value === this.baseline && this.state() !== 'error') return;
    if (value === this.baseline) {
      // Back to the saved value after a failed save: nothing to send.
      this.clearError();
      this.setStatus('idle');
      return;
    }
    if (this.saving) {
      this.queued = true;
      return;
    }
    this.saving = true;
    this.setStatus('saving');
    try {
      const result = await this.save(value);
      this.baseline = value;
      this.clearError();
      this.setStatus('saved');
      this.autosaveSaved.emit(result);
    } catch (err) {
      this.showError(err);
      this.autosaveFailed.emit(err);
    } finally {
      this.saving = false;
    }
    if (this.queued) {
      this.queued = false;
      await this.commit();
    }
  }

  private trigger(): 'blur' | 'change' {
    if (this.autosaveOn) return this.autosaveOn;
    const el = this.host.nativeElement;
    if (el instanceof HTMLSelectElement) return 'change';
    if (el instanceof HTMLInputElement && ['date', 'checkbox', 'radio'].includes(el.type)) return 'change';
    return 'blur';
  }

  private value(): string {
    const el = this.host.nativeElement;
    return el instanceof HTMLInputElement && el.type === 'checkbox' ? String(el.checked) : el.value;
  }

  private messageFor(err: unknown): string {
    if (this.autosaveErrorMessage) return this.autosaveErrorMessage(err);
    const fieldErrors = (err as { error?: { errors?: { message?: string }[] } })?.error?.errors;
    const first = Array.isArray(fieldErrors) ? fieldErrors.find(e => !!e?.message)?.message : undefined;
    if (first) return first;
    const code = refusalCodeOf(err);
    if (!code) return SAVE_FAILED_MESSAGE;
    return serverMessageOf(err) ?? refusalMessage(code);
  }

  private showError(err: unknown): void {
    const message = this.messageFor(err);
    const retryable = refusalCodeOf(err) === null;
    this.error.set(message);
    this.setStatus('error');

    const el = this.host.nativeElement;
    if (!this.errorEl) {
      const p = this.renderer.createElement('p') as HTMLElement;
      this.renderer.setAttribute(p, 'id', this.errorId);
      this.renderer.setAttribute(p, 'role', 'alert');
      this.renderer.addClass(p, 'autosave-error');
      this.insertAfter(this.statusEl && !this.autosaveStatusHost ? this.statusEl : el, p);
      this.errorEl = p;
    }
    this.errorEl.textContent = message;
    if (retryable) {
      const retry = this.renderer.createElement('button') as HTMLButtonElement;
      retry.type = 'button';
      retry.className = 'link autosave-retry';
      retry.textContent = 'Try again';
      // Saves what is in the field now: the failed value, unless the person has typed since.
      this.renderer.listen(retry, 'click', () => void this.commit());
      this.errorEl.append(' ', retry);
    }
    this.renderer.setAttribute(el, 'aria-invalid', 'true');
    this.setDescribedBy(true);
  }

  private clearError(): void {
    this.error.set(null);
    this.errorEl?.remove();
    this.errorEl = null;
    this.renderer.removeAttribute(this.host.nativeElement, 'aria-invalid');
    this.setDescribedBy(false);
  }

  private setDescribedBy(add: boolean): void {
    const el = this.host.nativeElement;
    const ids = (el.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(id => id && id !== this.errorId);
    if (add) ids.push(this.errorId);
    if (ids.length) this.renderer.setAttribute(el, 'aria-describedby', ids.join(' '));
    else this.renderer.removeAttribute(el, 'aria-describedby');
  }

  private setStatus(state: AutosaveState): void {
    this.state.set(state);
    if (this.savedTimer) {
      clearTimeout(this.savedTimer);
      this.savedTimer = null;
    }
    const status = this.ensureStatus();
    status.textContent = '';
    if (state === 'saving') {
      status.textContent = 'Saving…';
    } else if (state === 'saved') {
      const visible = this.renderer.createElement('span') as HTMLElement;
      this.renderer.setAttribute(visible, 'aria-hidden', 'true');
      visible.textContent = '✓ Saved';
      const spoken = this.renderer.createElement('span') as HTMLElement;
      this.renderer.addClass(spoken, 'sr-only');
      spoken.textContent = this.autosaveLabel ? `${this.autosaveLabel} saved` : 'Saved';
      status.append(visible, spoken);
      // Outside the zone: a waiting "✓ Saved" never holds the app unstable.
      this.zone.runOutsideAngular(() => {
        this.savedTimer = setTimeout(() => {
          this.savedTimer = null;
          if (this.state() === 'saved') this.zone.run(() => this.setStatus('idle'));
        }, this.autosaveSavedMs);
      });
    }
  }

  private ensureStatus(): HTMLElement {
    if (!this.statusEl) {
      const span = this.renderer.createElement('span') as HTMLElement;
      this.renderer.addClass(span, 'autosave-status');
      this.renderer.setAttribute(span, 'role', 'status');
      this.renderer.setAttribute(span, 'aria-live', 'polite');
      this.insertAfter(this.autosaveStatusHost ?? this.host.nativeElement, span);
      this.statusEl = span;
    }
    return this.statusEl;
  }

  private insertAfter(anchor: Element, node: Element): void {
    const parent = this.renderer.parentNode(anchor);
    if (parent) this.renderer.insertBefore(parent, node, this.renderer.nextSibling(anchor));
  }
}
