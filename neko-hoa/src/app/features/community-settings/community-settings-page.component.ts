import {
  AfterViewChecked, Component, ElementRef, ViewChild, computed, effect, inject, signal, untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommunitySettings, CommunitySettingsService } from '../../core/services/community-settings.service';
import { LifecycleAction } from '../../core/services/company.service';
import { AuthService } from '../../core/services/auth.service';
import { BoardNavigationService } from '../../core/services/board-navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { boardMemberships, landingTargetFor } from '../../core/services/landing';
import { DisabledReasonDirective } from '../../shared/disabled-reason.directive';
import { refusalCodeOf, refusalMessage } from '../../shared/refusal-messages';
import { CommunitySettingsContext, SettingsMode } from './community-settings-context';
import { SettingsSideNavComponent, SettingChoice } from './settings-side-nav.component';
import { SettingsSectionPlaceholderComponent } from './settings-section-placeholder.component';
import { ManagersMembershipsSectionComponent } from './managers-memberships.component';
import { ProfileSectionComponent } from './profile-section.component';
import { ResidentsSectionComponent } from './residents/residents-section.component';
import { LifecycleDialogsComponent } from './lifecycle-dialogs.component';
import { sectionsFor } from './settings-registry';

type LoadState = 'loading' | 'ready' | 'error';

interface StatusAction {
  action: LifecycleAction;
  label: string;
  /** The accessible name: "Mark Keystone Ridge Active", "Archive Keystone Ridge". */
  name: string;
  primary: boolean;
  /** Shown disabled with this reason (Mark Active with no open-ended manager, FR-010). */
  reason: string | null;
}

/**
 * 030 US5 / FR-029 / brief 04: one community's Community settings page. It is addressed by the
 * community, not by board mode's active community, so it opens for Onboarding and Archived
 * communities in nobody's switcher.
 *
 * - `/app/company/communities/:communityId/settings/:section` (Company Administrators, company area);
 * - `/app/board/settings/:section` (the active community's Community Manager, board mode).
 *
 * It renders the header (name, status in words, legal name, parent, status actions), the searchable
 * side list and the open section, all from `GET /communities/{id}/settings`. `can.*` only hides or
 * disables controls; the server re-checks every action. A 403 sends the person to their landing.
 */
@Component({
  selector: 'app-community-settings-page',
  standalone: true,
  imports: [RouterLink, DisabledReasonDirective, SettingsSideNavComponent, SettingsSectionPlaceholderComponent, ManagersMembershipsSectionComponent,
    ProfileSectionComponent, LifecycleDialogsComponent, ResidentsSectionComponent],
  providers: [CommunitySettingsContext],
  template: `
    @switch (state()) {
      @case ('loading') {
        <p class="sr-only" role="status">Loading Community settings…</p>
        <div class="ph csp__ph" aria-hidden="true">Community settings</div>
      }
      @case ('error') {
        <div class="card csp__error" role="alert">
          <p>{{ loadError() }}</p>
          @if (canRetry()) { <button type="button" class="btn" (click)="reload()">Try again</button> }
        </div>
      }
      @default {
        @if (settings(); as s) {
          <header class="csp__header">
            @if (mode === 'company') {
              <nav aria-label="Breadcrumb">
                <ol class="csp__crumbs">
                  <li><a class="link" routerLink="/app/company/portfolio">Portfolio</a></li>
                  <li><span aria-hidden="true">›</span> <span aria-current="page">{{ s.communityName }}</span></li>
                </ol>
              </nav>
            }
            <div class="csp__title-row">
              <h1 class="page-title csp__title" id="csp-title" tabindex="-1">{{ s.communityName }}<span class="sr-only">, </span>
                <span class="pill csp__chip">{{ s.status }}</span>
                @if (s.noManager === true) {<span class="sr-only">, </span>
                  <span class="pill pill--warn csp__chip">No manager</span>}
              </h1>
              @if (statusActions().length) {
                <div class="csp__actions">
                  @for (a of statusActions(); track a.action) {
                    <button type="button" class="btn" [class.btn--primary]="a.primary" [attr.aria-label]="a.name"
                            [appDisabledReason]="a.reason" (click)="requestLifecycle(a.action)">{{ a.label }}</button>
                  }
                </div>
              }
            </div>
            <p class="muted">{{ s.legalName || 'Legal name: Missing — required' }}</p>
            @if (s.parent) { <p class="muted">Sub-association of {{ s.parent.communityName }}</p> }
          </header>

          <div class="csp__body">
            <app-settings-side-nav class="csp__side" [sections]="sections()" [current]="current()?.id ?? ''" [basePath]="basePath()"
                                   (settingChosen)="focusSetting($event)" (sectionChosen)="focusSectionHeading()" />
            <div class="csp__section" #outlet>
              @if (s.status === 'Archived') {
                <div class="card card--lav csp__banner">{{ archivedBanner() }}</div>
              }
              @if (onboardingNotice()) {
                <div class="card card--lav csp__banner">{{ onboardingNotice() }}</div>
              }
              @if (current(); as section) {
                @switch (section.id) {
                  @case ('profile') { <app-profile-section /> }
                  @case ('managers-and-memberships') {
                    <app-managers-memberships-section><app-residents-section residents /></app-managers-memberships-section>
                  }
                  @default {
                    <!-- A placeholder until T132 builds the section. -->
                    <app-settings-section-placeholder [section]="section" />
                  }
                }
              }
            </div>
          </div>
          <!-- The header's status buttons open these (T109). -->
          <app-lifecycle-dialogs />
        }
      }
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: 16px; }
    .csp__ph { height: 120px; }
    .csp__error { display: flex; flex-direction: column; gap: 10px; align-items: flex-start; }
    .csp__header { display: flex; flex-direction: column; gap: 4px; }
    .csp__crumbs { list-style: none; display: flex; gap: 6px; font-size: 12px; color: var(--ink-soft); }
    .csp__title-row { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
    .csp__title { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .csp__actions { margin-left: auto; display: flex; flex-wrap: wrap; gap: 8px; }
    .csp__actions .btn { min-height: 36px; }
    .csp__body { display: grid; grid-template-columns: 220px minmax(0, 1fr); gap: 20px; align-items: start; }
    .csp__section { display: flex; flex-direction: column; gap: 12px; min-width: 0; }
    @media (max-width: 767.98px) { .csp__body { grid-template-columns: minmax(0, 1fr); } }
  `],
})
export class CommunitySettingsPageComponent implements AfterViewChecked {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private auth = inject(AuthService);
  private nav = inject(BoardNavigationService);
  private toasts = inject(ToastService);
  private settingsService = inject(CommunitySettingsService);
  readonly ctx = inject(CommunitySettingsContext);

  @ViewChild('outlet') private outlet?: ElementRef<HTMLElement>;

  readonly mode: SettingsMode = this.route.snapshot.data['settingsMode'] === 'board' ? 'board' : 'company';

  private readonly params = toSignal(this.route.paramMap, { initialValue: this.route.snapshot.paramMap });
  private readonly sectionParam = computed(() => this.params().get('section') ?? '');

  /** Company area: the address names it. Board mode: the active community, or the only one this person manages. */
  readonly communityId = computed<string | null>(() => {
    if (this.mode === 'company') return this.params().get('communityId');
    const active = this.nav.activeCommunityId();
    if (active) return active;
    const managed = Array.from(new Set(boardMemberships(this.auth.user())
      .filter(m => m.role === 'CommunityManager').map(m => m.communityId)));
    return managed.length === 1 ? managed[0] : null;
  });

  readonly state = signal<LoadState>('loading');
  readonly loadError = signal<string | null>(null);
  readonly canRetry = signal(true);
  readonly settings = this.ctx.settings;

  readonly sections = computed(() => sectionsFor(this.settings()?.sections ?? []));
  readonly current = computed(() => this.sections().find(s => s.id === this.sectionParam()) ?? null);

  readonly basePath = computed(() => this.mode === 'company'
    ? ['/app/company/communities', this.communityId() ?? '', 'settings']
    : ['/app/board/settings']);

  readonly statusActions = computed<StatusAction[]>(() => {
    const s = this.settings();
    if (!s) return [];
    const name = s.communityName;
    const shown = (c: { allowed: boolean; reasonCode: string | null }) => c.allowed || !!c.reasonCode;
    const reason = (c: { allowed: boolean; reasonCode: string | null }) =>
      c.allowed ? null : refusalMessage(c.reasonCode, { communityName: name, audience: this.ctx.audience() });
    const actions: StatusAction[] = [];
    // brief 04 §4.1: Onboarding → Mark Active, Archive; Active → Archive; Archived → Restore.
    if (s.status === 'Onboarding' && shown(s.can.activate)) {
      actions.push({ action: 'activate', label: 'Mark Active', name: `Mark ${name} Active`, primary: true, reason: reason(s.can.activate) });
    }
    if (s.status !== 'Archived' && shown(s.can.archive)) {
      actions.push({ action: 'archive', label: 'Archive', name: `Archive ${name}`, primary: false, reason: reason(s.can.archive) });
    }
    if (s.status === 'Archived' && shown(s.can.restore)) {
      actions.push({ action: 'restore', label: 'Restore', name: `Restore ${name}`, primary: true, reason: reason(s.can.restore) });
    }
    return actions;
  });

  readonly archivedBanner = computed(() => {
    const s = this.settings();
    return s ? `${s.communityName} is archived. The management company no longer manages it. Everything here is read-only. Nothing has been deleted.` : '';
  });

  /** FR-029: a Community Manager of an Onboarding community sees this at the top of Profile. */
  readonly onboardingNotice = computed(() => {
    const s = this.settings();
    if (!s || this.mode !== 'board' || s.status !== 'Onboarding' || this.current()?.id !== 'profile') return null;
    return `${s.communityName} is being set up. A Company Administrator must mark it Active. Resident invitations are sent then.`;
  });

  private pendingFocus: string | null = null;
  private loadSeq = 0;

  constructor() {
    this.ctx.mode.set(this.mode);
    this.ctx.setReloader(() => this.reload());

    effect(() => {
      const id = this.communityId();
      untracked(() => {
        if (id) void this.load(id);
        // Board mode with several managed communities and none chosen: choose one first.
        else if (this.mode === 'board') void this.router.navigate(['/app/board/communities']);
      });
    }, { allowSignalWrites: true });

    // An unknown or hidden section opens the first one the server lists.
    effect(() => {
      const sections = this.sections();
      const wanted = this.sectionParam();
      if (sections.length && !sections.some(s => s.id === wanted)) {
        untracked(() => void this.router.navigate([...this.basePath(), sections[0].id], { replaceUrl: true }));
      }
    });
  }

  ngAfterViewChecked(): void {
    if (!this.pendingFocus || !this.outlet) return;
    const target = this.outlet.nativeElement.querySelector<HTMLElement>(this.pendingFocus);
    if (target) {
      this.pendingFocus = null;
      setTimeout(() => target.focus());
    }
  }

  reload(): Promise<void> {
    const id = this.communityId();
    return id ? this.load(id) : Promise.resolve();
  }

  requestLifecycle(action: LifecycleAction): void {
    this.ctx.requestLifecycle(action);
  }

  focusSetting(choice: SettingChoice): void {
    this.pendingFocus = `[data-setting-id="${choice.settingId}"]`;
  }

  focusSectionHeading(): void {
    this.pendingFocus = '[data-section-heading]';
  }

  private async load(communityId: string): Promise<void> {
    const seq = ++this.loadSeq;
    if (!this.settings()) this.state.set('loading');
    try {
      const result: CommunitySettings = await this.settingsService.getSettings(communityId);
      if (seq !== this.loadSeq) return;
      this.settings.set(result);
      this.state.set('ready');
    } catch (err) {
      if (seq !== this.loadSeq) return;
      this.onLoadError(err);
    }
  }

  private onLoadError(err: unknown): void {
    const code = refusalCodeOf(err);
    if (code === 'FORBIDDEN') {
      // US5-S3 / FR-032: the same for a community that doesn't exist; nothing of it is shown.
      this.settings.set(null);
      this.toasts.show(refusalMessage('FORBIDDEN', { surface: 'page' }));
      const target = landingTargetFor(this.auth.user());
      this.nav.setActiveCommunity(target.activeCommunityId);
      void this.router.navigate(target.commands);
      return;
    }
    this.settings.set(null);
    this.state.set('error');
    if (code === 'COMMUNITY_ARCHIVED' || code === 'COMMUNITY_NOT_ACTIVE') {
      this.canRetry.set(false);
      this.loadError.set(refusalMessage(code, { audience: this.ctx.audience() }));
    } else {
      this.canRetry.set(true);
      this.loadError.set("Couldn't load Community settings.");
    }
  }
}
