import { computed, inject } from '@angular/core';
import { AuthService } from '../../../core/services/auth.service';
import { BoardNavigationService } from '../../../core/services/board-navigation.service';

// 027: the community the board pages act within (same rule as 025's board pages) and the caller's
// roles there. Roles drive UX only; the server re-checks every capability.
export function arcContext() {
  const auth = inject(AuthService);
  const nav = inject(BoardNavigationService);
  const communityId = computed(() => {
    const memberships = auth.user()?.memberships ?? [];
    return nav.activeCommunityId() ?? (memberships.length ? memberships[0].communityId : null);
  });
  const roles = computed(() => {
    const cid = communityId();
    return new Set((auth.user()?.memberships ?? []).filter(m => m.communityId === cid).map(m => m.role));
  });
  return {
    communityId,
    isManager: computed(() => roles().has('CommunityManager')),
  };
}
