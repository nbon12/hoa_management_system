import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { BoardNavigationService } from '../services/board-navigation.service';
import { landingTargetFor } from '../services/landing';

// 025 FR-026: the default landing for the bare `/app` entry. This guard is attached ONLY to
// the empty-path child of `app` (pathMatch: 'full'), so an explicit deep link such as
// `/app/payments/statement` never reaches it and is never rewritten — only the default
// landing is mode-aware. Always returns a UrlTree; the empty-path route renders nothing.
export const landingGuard: CanActivateFn = (): UrlTree => {
  const auth = inject(AuthService);
  const nav = inject(BoardNavigationService);
  const router = inject(Router);

  const target = landingTargetFor(auth.user());
  nav.setActiveCommunity(target.activeCommunityId);
  return router.createUrlTree(target.commands);
};
