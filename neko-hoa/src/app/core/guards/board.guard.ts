import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { BoardNavigationService, RESIDENT_ROLE } from '../services/board-navigation.service';

// 025 FR-028: protects board routes. A user whose role set cannot use the route is refused
// and redirected to a permitted board page — never shown an empty screen. Authorization is
// UX-only here (FR-014 keeps the server as the real gate); this just avoids offering a
// route the user's memberships can't back.
export const boardGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const nav = inject(BoardNavigationService);
  const router = inject(Router);

  const memberships = auth.user()?.memberships ?? [];
  const nonResident = memberships.filter(m => m.role !== RESIDENT_ROLE);

  // Not board-eligible at all → back to the resident dashboard.
  if (nonResident.length === 0) {
    return router.createUrlTree(['/app/dashboard']);
  }

  const requiredRoles = route.data?.['requiredRoles'] as string[] | undefined;
  if (requiredRoles && requiredRoles.length) {
    const active = nav.activeCommunityId();
    const relevant = active ? memberships.filter(m => m.communityId === active) : memberships;
    const roleSet = new Set(relevant.map(m => m.role));
    const allowed = requiredRoles.some(r => roleSet.has(r));
    if (!allowed) {
      // Refused → redirect to a permitted board page rather than an empty screen (FR-028).
      return router.createUrlTree(['/app/board/home']);
    }
  }

  return true;
};
