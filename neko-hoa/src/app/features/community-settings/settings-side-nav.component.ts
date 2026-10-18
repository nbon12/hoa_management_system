import { Component, ElementRef, EventEmitter, Input, Output, ViewChild, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ViewportService } from '../../core/services/viewport.service';
import { SectionDef, SettingsSearchResult, searchSettings } from './settings-registry';

let nextId = 1;

export interface SettingChoice {
  sectionId: string;
  settingId: string;
}

/**
 * 030 FR-029 / brief 04 §4.1 (owner decision 2026-10-14): the Community settings side list, with a
 * search box at its top. Each section is a link (each has its own address). Searching filters to
 * matching sections and setting labels from the static registry; a result opens its section and
 * emits `settingChosen` so the page can focus that field. Below 768 px it collapses into a menu
 * button that opens the same panel, search included (constitution §6).
 */
@Component({
  selector: 'app-settings-side-nav',
  standalone: true,
  imports: [RouterLink],
  template: `
    <nav class="ssn" aria-label="Community settings sections">
      @if (narrow()) {
        <button type="button" class="btn ssn__toggle" #toggle [attr.aria-expanded]="menuOpen()" [attr.aria-controls]="panelId"
                (click)="menuOpen.set(!menuOpen())">Sections: {{ currentLabel() }}</button>
      }
      <div class="ssn__panel" [id]="panelId" [hidden]="narrow() && !menuOpen()" (keydown.escape)="closeMenu()">
        <label class="sr-only" [for]="searchId">Search settings</label>
        <input class="field" type="search" [id]="searchId" placeholder="Search settings" autocomplete="off"
               [value]="query()" (input)="query.set($any($event.target).value)" />
        <p class="sr-only" role="status" aria-live="polite">{{ announcement() }}</p>
        @if (query().trim()) {
          @if (results().length) {
            <ul class="ssn__list">
              @for (r of results(); track r.key) {
                <li><a class="ssn__item" [routerLink]="linkFor(r.sectionId)" (click)="chooseResult(r)">{{ r.label }}</a></li>
              }
            </ul>
          } @else {
            <p class="muted ssn__empty">No settings match “{{ query().trim() }}”.</p>
          }
        } @else {
          <ul class="ssn__list">
            @for (s of sections; track s.id) {
              <li>
                <a class="ssn__item" [class.ssn__item--current]="s.id === current" [routerLink]="linkFor(s.id)"
                   [attr.aria-current]="s.id === current ? 'page' : null" (click)="chooseSection(s.id)">{{ s.label }}</a>
              </li>
            }
          </ul>
        }
      </div>
    </nav>
  `,
  styles: [`
    .ssn { display: flex; flex-direction: column; gap: 8px; }
    .ssn__panel { display: flex; flex-direction: column; gap: 8px; }
    .ssn__panel[hidden] { display: none; }
    .ssn__list { list-style: none; display: flex; flex-direction: column; gap: 2px; }
    .ssn__item {
      display: block; padding: 8px 10px; border-radius: 10px; min-height: 36px;
      color: var(--ink-soft); text-decoration: none; border: 1.5px solid transparent;
    }
    .ssn__item:hover { background: var(--lav-2); }
    .ssn__item:focus-visible { outline: 2px solid var(--violet); outline-offset: 1px; }
    /* Current: bold and a bar, not colour alone (brief 04 §4.1). */
    .ssn__item--current { color: var(--ink); font-weight: 600; border-left: 4px solid var(--ink); background: var(--paper); }
    .ssn__toggle { justify-content: space-between; width: 100%; min-height: 44px; }
    .ssn__empty { padding: 4px 2px; }
  `],
})
export class SettingsSideNavComponent {
  /** The sections the server lists for this person, in order. */
  @Input({ required: true }) set sections(value: SectionDef[]) { this.sectionList.set(value ?? []); }
  get sections(): SectionDef[] { return this.sectionList(); }
  /** The open section's id. */
  @Input() current = '';

  private readonly sectionList = signal<SectionDef[]>([]);
  /** Router commands up to and including `settings`; each section's id is appended. */
  @Input({ required: true }) basePath: string[] = [];

  @Output() readonly settingChosen = new EventEmitter<SettingChoice>();
  @Output() readonly sectionChosen = new EventEmitter<string>();

  @ViewChild('toggle') private toggle?: ElementRef<HTMLButtonElement>;

  readonly narrow = inject(ViewportService).isNarrow;
  readonly menuOpen = signal(false);
  readonly query = signal('');

  readonly panelId = `ssn-panel-${nextId}`;
  readonly searchId = `ssn-search-${nextId++}`;

  readonly results = computed<SettingsSearchResult[]>(() => searchSettings(this.query(), this.sectionList()));

  readonly announcement = computed(() => {
    const q = this.query().trim();
    if (!q) return '';
    const n = this.results().length;
    if (n === 0) return `No settings match “${q}”.`;
    return n === 1 ? '1 setting matches' : `${n} settings match`;
  });

  currentLabel(): string {
    return this.sections.find(s => s.id === this.current)?.label ?? this.sections[0]?.label ?? '';
  }

  linkFor(sectionId: string): string[] {
    return [...this.basePath, sectionId];
  }

  chooseSection(sectionId: string): void {
    this.menuOpen.set(false);
    this.sectionChosen.emit(sectionId);
  }

  chooseResult(result: SettingsSearchResult): void {
    this.query.set('');
    this.menuOpen.set(false);
    if (result.settingId) this.settingChosen.emit({ sectionId: result.sectionId, settingId: result.settingId });
    else this.sectionChosen.emit(result.sectionId);
  }

  closeMenu(): void {
    if (!this.narrow() || !this.menuOpen()) return;
    this.menuOpen.set(false);
    this.toggle?.nativeElement.focus();
  }
}
