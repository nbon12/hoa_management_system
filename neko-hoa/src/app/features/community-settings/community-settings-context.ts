import { Injectable, computed, signal } from '@angular/core';
import { CommunitySettings } from '../../core/services/community-settings.service';
import { LifecycleAction } from '../../core/services/company.service';
import { RefusalAudience } from '../../shared/refusal-messages';

/** Which route opened the page: the company area (Company Administrator) or board mode (Community Manager). */
export type SettingsMode = 'company' | 'board';

export interface LifecycleRequest {
  action: LifecycleAction;
  /** Increments on every request, so pressing the same button twice is two requests. */
  seq: number;
}

/**
 * 030 R20: state shared by the Community settings page and its sections, provided by
 * `CommunitySettingsPageComponent` (one per page). Sections read `settings()` for the community,
 * its status and `can.*`, and call `reload()` after a change that alters them. The header's status
 * buttons put a `lifecycleRequest` here for the lifecycle dialogs (T109) to answer.
 */
@Injectable()
export class CommunitySettingsContext {
  readonly settings = signal<CommunitySettings | null>(null);
  readonly mode = signal<SettingsMode>('company');
  readonly lifecycleRequest = signal<LifecycleRequest | null>(null);

  readonly communityId = computed(() => this.settings()?.id ?? null);
  /** For `refusalMessage`: Company Administrators see names the others don't (FR-007). */
  readonly audience = computed<RefusalAudience>(() => (this.mode() === 'company' ? 'companyAdmin' : 'staff'));
  readonly readOnly = computed(() => this.settings()?.status === 'Archived');

  private reloader: (() => Promise<void>) | null = null;
  private seq = 0;

  /** Set by the page. */
  setReloader(fn: () => Promise<void>): void {
    this.reloader = fn;
  }

  /** Re-reads the page shell (`GET /communities/{id}/settings`). */
  reload(): Promise<void> {
    return this.reloader ? this.reloader() : Promise.resolve();
  }

  requestLifecycle(action: LifecycleAction): void {
    this.lifecycleRequest.set({ action, seq: ++this.seq });
  }
}
