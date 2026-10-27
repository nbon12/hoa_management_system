import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { BoardNavigationService } from '../services/board-navigation.service';
import { ToastService } from '../services/toast.service';
import { landingTargetFor } from '../services/landing';
import { refusalMessage } from '../../shared/refusal-messages';

// 030 FR-035 / FR-036: company-area routes are for Company Administrators only. Anyone else is sent
// to their landing page with "You don't have access to that page." (brief 01 8.01.q, spec 025
// FR-028). UX only: every company endpoint is still authorized by the server.
export const companyAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const nav = inject(BoardNavigationService);
  const router = inject(Router);
  const toasts = inject(ToastService);

  const user = auth.user();
  if (user?.isCompanyAdministrator === true) return true;
  if (!user) return router.createUrlTree(['/login']);

  toasts.show(refusalMessage('FORBIDDEN', { surface: 'page' }));
  const target = landingTargetFor(user);
  // A stale `landing: Company` without the grant would bounce straight back here.
  if (target.commands[0].startsWith('/app/company')) return router.createUrlTree(['/app/dashboard']);
  nav.setActiveCommunity(target.activeCommunityId);
  return router.createUrlTree(target.commands);
};
