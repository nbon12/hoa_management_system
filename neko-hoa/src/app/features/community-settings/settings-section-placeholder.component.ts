import { Component, Input } from '@angular/core';
import { SectionDef } from './settings-registry';

/**
 * 030 T068: a stand-in for a Community settings section until its own task builds it (Profile:
 * T109; Managers and memberships: T090, with Residents T120; Architectural review: T132). It keeps
 * the page contract the real sections must keep: an `h2` marked `data-section-heading`, and one
 * focus target per registry setting marked `data-setting-id`.
 */
@Component({
  selector: 'app-settings-section-placeholder',
  standalone: true,
  template: `
    <section class="card" [attr.aria-labelledby]="'settings-section-' + section.id">
      <h2 class="section-title" [id]="'settings-section-' + section.id" tabindex="-1" data-section-heading>{{ section.label }}</h2>
      <p class="muted">This section is being built.</p>
      <ul class="ssp__list">
        @for (s of section.settings; track s.id) {
          <li tabindex="-1" [attr.data-setting-id]="s.id">{{ s.label }}</li>
        }
      </ul>
    </section>
  `,
  styles: [`.ssp__list { margin: 10px 0 0 18px; color: var(--ink-soft); font-size: 12px; }`],
})
export class SettingsSectionPlaceholderComponent {
  @Input({ required: true }) section!: SectionDef;
}
