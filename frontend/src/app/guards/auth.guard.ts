import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { map, catchError, of } from 'rxjs';
import { API_BASE_URL } from '../app.config';

/**
 * Guard that allows access only when the user is authenticated (API returns 2xx).
 * On 401, redirects to the backend login page.
 */
export const authGuard: CanActivateFn = () => {
  const http = inject(HttpClient);
  const router = inject(Router);
  const apiBaseUrl = inject(API_BASE_URL);

  return http
    .get(`${apiBaseUrl}/dashboard/summary`, { withCredentials: true, observe: 'response' })
    .pipe(
      map(() => true),
      catchError((err) => {
        if (err.status === 401) {
          // Redirect to backend login; in dev, backend may be on different port
          const loginUrl = apiBaseUrl.replace('/api', '') + '/Identity/Account/Login';
          window.location.href = loginUrl + '?returnUrl=' + encodeURIComponent(window.location.href);
          return of(false);
        }
        return of(true); // Allow route on other errors (e.g. 500); page can show error
      })
    );
};
