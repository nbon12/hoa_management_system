import { Injectable, computed, inject } from '@angular/core';
import { AuthService } from './auth.service';
import { TokenService } from './token.service';
import { UserPropertySummary } from '../models';

/**
 * 030 FR-012a / brief 11 §4.3: the signed-in person's home list and the selected home.
 *
 * - `homes`: homes in Active communities first, then homes in Archived ones ("No longer managed").
 *   A home in an Onboarding community is never listed, even if an older backend sends it.
 * - `current`: the home the access token is for (its `propertyId` claim).
 */
@Injectable({ providedIn: 'root' })
export class CurrentHomeService {
  private auth = inject(AuthService);
  private tokens = inject(TokenService);

  readonly homes = computed<UserPropertySummary[]>(() => {
    const all = (this.auth.user()?.properties ?? []).filter(p => p.communityStatus !== 'Onboarding');
    return [...all.filter(p => p.communityStatus !== 'Archived'), ...all.filter(p => p.communityStatus === 'Archived')];
  });

  readonly currentId = computed(() => this.tokens.claim(this.tokens.getAccessToken(), 'propertyId'));

  readonly current = computed(() => this.homes().find(h => h.id === this.currentId()) ?? null);

  /** The selected home's community is Archived: the home reads "No longer managed". */
  readonly archived = computed(() => this.current()?.communityStatus === 'Archived');
}
