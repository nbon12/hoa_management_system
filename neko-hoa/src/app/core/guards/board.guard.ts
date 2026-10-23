import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { BoardNavigationService } from '../services/board-navigation.service';
import { ToastService } from '../services/toast.service';
import { boardMemberships, landingTargetFor } from '../services/landing';
import { refusalMessage } from '../../shared/refusal-messages';

// 025 FR-028: protects board routes. A user whose role set cannot use the route is refused
// and redirected to a permitted board page — never shown an empty screen. Authorization is
// UX-only here (FR-014 keeps the server as the real gate); this just avoids offering a
// route the user's memberships can't back.
//
// 030 FR-036b: memberships in Onboarding communities count for board eligibility; only Archived
// communities are excluded. A refused user who isn't board-eligible goes to their FR-036a landing
// (for example the company area, or "No home linked"), with the page-refusal toast (brief 01 §6).
export const boardGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const nav = inject(BoardNavigationService);
  const router = inject(Router);
  const toasts = inject(ToastService);

  const user = auth.user();
  const board = boardMemberships(user);

  const refuse = (commands: string[]): UrlTree => {
    toasts.show(refusalMessage('FORBIDDEN', { surface: 'page' }));
    return router.createUrlTree(commands);
  };

  // Not board-eligible at all → the user's landing page. A landing that is itself a board page
  // (a stale `landing: Board`) would bounce straight back here, so it falls back to the dashboard.
  if (board.length === 0) {
    if (!user) return router.createUrlTree(['/app/dashboard']);
    const target = landingTargetFor(user);
    const commands = target.commands[0].startsWith('/app/board') ? ['/app/dashboard'] : target.commands;
    return refuse(commands);
  }

  const requiredRoles = route.data?.['requiredRoles'] as string[] | undefined;
  if (requiredRoles && requiredRoles.length) {
    // 030: `roleScope: 'any'` routes (Add community) need the role in any community, not the active one.
    const anyCommunity = route.data?.['roleScope'] === 'any';
    const active = nav.activeCommunityId();
    const relevant = active && !anyCommunity ? board.filter(m => m.communityId === active) : board;
    const roleSet = new Set(relevant.map(m => m.role));
    const allowed = requiredRoles.some(r => roleSet.has(r));
    if (!allowed) {
      // Refused → redirect to a permitted board page rather than an empty screen (FR-028).
      return refuse(['/app/board/home']);
    }
  }

  return true;
};
