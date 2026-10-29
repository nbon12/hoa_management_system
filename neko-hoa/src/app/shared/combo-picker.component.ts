import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';

export interface PickerOption {
  id: string;
  label: string;
  /** A second line, for example "Board Member". */
  detail?: string | null;
  /** A text chip after the label, for example "Onboarding". */
  chip?: string | null;
  /** More words that match when typing but aren't shown, for example "New York Eastern Time". */
  keywords?: string | null;
}

interface PickerRow extends PickerOption {
  key: string;
  none: boolean;
}

let nextId = 1;

/**
 * 030 brief 03 §4.1 / brief 05 §7: a combobox picker. Typing filters the options by label
 * (case-insensitive, part of a word) and emits `queryChange` for server-side search; Up/Down move,
 * Enter chooses, Esc closes without changing the value; the match count is announced politely;
 * the chosen option reads "✓ Selected". With `noneLabel` a "none" row comes first.
 * With `dropdown`, clicking the field or its ▾ button lists every option, so it works as a
 * searchable dropdown.
 * Used for the parent association (brief 03, 04), the people pickers (brief 05) and the time zone.
 */
@Component({
  selector: 'app-combo-picker',
  standalone: true,
  template: `
    <div class="cbp">
      <label class="field-label" [for]="inputId">{{ label }}</label>
      @if (helper && helperFirst) { <p class="field-help" [id]="helpId">{{ helper }}</p> }
      <div class="cbp__control" [class.cbp__control--dropdown]="dropdown">
        <input #input class="field" [id]="inputId" role="combobox" aria-autocomplete="list" autocomplete="off" [placeholder]="placeholder"
               [attr.data-setting-id]="settingId" [attr.aria-expanded]="open()" [attr.aria-controls]="listId"
               [attr.aria-activedescendant]="activeOptionId()" [attr.aria-describedby]="describedBy()"
               [attr.aria-invalid]="error ? 'true' : null" [disabled]="disabled" [value]="text()"
               (input)="onInput($any($event.target).value)" (keydown)="onKey($event)" (click)="onClick(input)" (blur)="close()" />
        @if (dropdown) {
          <button type="button" class="cbp__toggle" tabindex="-1" [attr.aria-label]="'Show all ' + noun[1]"
                  [attr.aria-expanded]="open()" [attr.aria-controls]="listId" [disabled]="disabled"
                  (mousedown)="$event.preventDefault()" (click)="toggle(input)"><span aria-hidden="true">▾</span></button>
        }
      </div>
      <p class="sr-only" role="status" aria-live="polite">{{ announcement() }}</p>
      @if (open()) {
        <ul class="cbp__list" role="listbox" [id]="listId" [attr.aria-label]="label">
          @for (row of rows(); track row.key; let i = $index) {
            <li role="option" class="cbp__option" [id]="optionId(i)" [class.cbp__option--active]="i === active()"
                [attr.aria-selected]="isSelected(row)" (mousedown)="$event.preventDefault()" (click)="choose(row)">
              <span class="cbp__label">{{ row.label }}@if (row.detail) {<span class="cbp__detail">{{ row.detail }}</span>}</span>
              @if (row.chip) { <span class="sr-only">, </span><span class="pill">{{ row.chip }}</span> }
              @if (isSelected(row)) { <span class="sr-only">, </span><span class="cbp__selected">✓ Selected</span> }
            </li>
          }
        </ul>
        @if (query().trim() && matchCount() === 0) { <p class="muted cbp__empty">{{ noMatch() }}</p> }
      }
      @if (helper && !helperFirst) { <p class="field-help" [id]="helpId">{{ helper }}</p> }
      @if (error) { <p class="field-error" [id]="errorId">{{ error }}</p> }
    </div>
  `,
  styles: [`
    .cbp__control { position: relative; }
    .cbp__control--dropdown .field { padding-right: 36px; }
    .cbp__toggle {
      position: absolute; top: 50%; right: 4px; transform: translateY(-50%); width: 30px; height: 30px;
      border: 0; border-radius: 8px; background: transparent; color: var(--ink-soft); cursor: pointer;
    }
    .cbp__toggle:hover:not(:disabled) { background: var(--lav); color: var(--ink); }
    .cbp__toggle:disabled { cursor: default; opacity: 0.5; }
    .cbp__list {
      list-style: none; margin-top: 4px; max-height: 240px; overflow-y: auto;
      border: 1.5px solid var(--ink); border-radius: 10px; background: var(--paper); padding: 4px;
    }
    .cbp__option { display: flex; align-items: center; gap: 6px; padding: 8px 10px; border-radius: 8px; cursor: pointer; }
    .cbp__option--active, .cbp__option:hover { background: var(--lav); outline: 2px solid var(--violet); }
    .cbp__label { display: flex; flex-direction: column; }
    .cbp__detail { font-size: 11px; color: var(--ink-soft); }
    .cbp__selected { margin-left: auto; font-size: 11px; font-weight: 600; }
    .cbp__empty { padding: 4px 2px; }
  `],
})
export class ComboPickerComponent {
  @Input({ required: true }) label = '';
  @Input() helper = '';
  @Input() error: string | null = null;
  @Input() placeholder = '';
  @Input() disabled = false;
  /** A first row meaning "none", for example "None — not a sub-association". */
  @Input() noneLabel: string | null = null;
  /** Words for the count: [singular, plural]. */
  @Input() noun: [string, string] = ['option', 'options'];
  @Input() noMatchMessage: (query: string) => string = q => `Nothing matches “${q}”.`;
  /** A searchable dropdown: clicking the field, or the ▾ button after it, lists every option. */
  @Input() dropdown = false;
  /** The helper goes between the label and the field, as in the architectural review form. */
  @Input() helperFirst = false;
  /** The settings registry id, for settings search (`data-setting-id` on the field). */
  @Input() settingId: string | null = null;
  @Input() set options(v: PickerOption[]) {
    this.all.set([...(v ?? [])]);
    this.text.set(this.labelOf(this.selected()));
  }
  @Input() set value(v: string | null) {
    this.selected.set(v ?? null);
    this.text.set(this.labelOf(v ?? null));
  }
  get value(): string | null { return this.selected(); }

  @Output() readonly valueChange = new EventEmitter<string | null>();
  @Output() readonly queryChange = new EventEmitter<string>();

  private readonly uid = nextId++;
  /** The field's id. Set it when something else links to the field, such as an error summary. */
  @Input() inputId = `cbp-input-${this.uid}`;
  readonly listId = `cbp-list-${this.uid}`;
  readonly helpId = `cbp-help-${this.uid}`;
  readonly errorId = `cbp-error-${this.uid}`;

  private readonly all = signal<PickerOption[]>([]);
  private readonly selected = signal<string | null>(null);
  readonly open = signal(false);
  readonly query = signal('');
  readonly text = signal('');
  readonly active = signal(-1);

  readonly rows = computed<PickerRow[]>(() => {
    const q = this.query().trim().toLowerCase();
    const options = this.all()
      .filter(o => !q || o.label.toLowerCase().includes(q) || !!o.keywords?.toLowerCase().includes(q))
      .map(o => ({ ...o, key: o.id, none: false }));
    return this.noneLabel ? [{ key: '', id: '', label: this.noneLabel, none: true }, ...options] : options;
  });

  readonly matchCount = computed(() => this.rows().filter(r => !r.none).length);

  readonly announcement = computed(() => {
    if (!this.open() || !this.query().trim()) return '';
    const n = this.matchCount();
    if (n === 0) return this.noMatch();
    return n === 1 ? `1 ${this.noun[0]} matches.` : `${n} ${this.noun[1]} match.`;
  });

  readonly activeOptionId = computed(() => (this.open() && this.active() >= 0 ? this.optionId(this.active()) : null));

  describedBy(): string | null {
    const ids = [this.helper ? this.helpId : null, this.error ? this.errorId : null].filter(Boolean);
    return ids.length ? ids.join(' ') : null;
  }

  noMatch(): string {
    return this.noMatchMessage(this.query().trim());
  }

  optionId(index: number): string {
    return `cbp-option-${this.uid}-${index}`;
  }

  isSelected(row: PickerRow): boolean {
    return row.none ? this.selected() === null : row.id === this.selected();
  }

  onInput(value: string): void {
    this.text.set(value);
    this.query.set(value);
    this.open.set(true);
    this.active.set(-1);
    this.queryChange.emit(value.trim());
  }

  onKey(event: KeyboardEvent): void {
    const count = this.rows().length;
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (!this.open()) return this.openList();
        this.setActive(Math.min(count - 1, this.active() + 1));
        return;
      case 'ArrowUp':
        event.preventDefault();
        if (this.open()) this.setActive(Math.max(0, this.active() - 1));
        return;
      case 'Enter':
        if (this.open()) {
          event.preventDefault();
          const row = this.rows()[this.active()];
          if (row) this.choose(row);
        }
        return;
      case 'Escape':
        if (this.open()) {
          event.preventDefault();
          event.stopPropagation();
          this.close();
        }
        return;
    }
  }

  choose(row: PickerRow): void {
    const id = row.none ? null : row.id;
    this.selected.set(id);
    this.text.set(id ? row.label : '');
    this.query.set('');
    this.open.set(false);
    this.valueChange.emit(id);
  }

  close(): void {
    this.open.set(false);
    this.query.set('');
    this.text.set(this.labelOf(this.selected()));
  }

  /** `dropdown`: a click lists every option and selects the text, so typing starts a new search. */
  onClick(input: HTMLInputElement): void {
    if (!this.dropdown || this.disabled || this.open()) return;
    this.openList();
    input.select();
  }

  /** The ▾ button: opens or closes the list, keeping focus in the field. */
  toggle(input: HTMLInputElement): void {
    if (this.disabled) return;
    if (this.open()) return this.close();
    input.focus();
    this.openList();
    input.select();
  }

  private openList(): void {
    this.query.set('');
    this.open.set(true);
    this.setActive(Math.max(0, this.rows().findIndex(r => this.isSelected(r))));
  }

  /** Moves the active option, scrolling it into view once the list has rendered. */
  private setActive(index: number): void {
    this.active.set(index);
    setTimeout(() => document.getElementById(this.optionId(index))?.scrollIntoView?.({ block: 'nearest' }));
  }

  private labelOf(id: string | null): string {
    return id ? this.all().find(o => o.id === id)?.label ?? this.text() : '';
  }
}
