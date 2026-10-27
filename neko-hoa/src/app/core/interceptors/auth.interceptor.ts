import {
  HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse
} from '@angular/common/http';
import { Injector, inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { TokenService } from '../services/token.service';
import { SessionRefreshService } from '../services/session-refresh';
import { PropertyAccessRecoveryService } from '../services/property-access-recovery';
import { ArchivedHomeService } from '../services/archived-home.service';
import { environment } from '../../../environments/environment';

// <!-- REPOWISE:START domain=frontend-session -->
// 020-D FR-D4/FR-D5: the bearer token is attached ONLY to API-origin requests — a request to any
// other origin (third-party scripts' fetches, absolute URLs in content) never carries it. 401s on
// API requests route through SessionRefreshService: single-flight in-tab and cross-tab
// (Web Locks), honoring the backend's strict one-time-use refresh rotation.
// 030 FR-024g: a 403 PROPERTY_ACCESS_DENIED means the selected home is gone; the recovery service
// re-reads /auth/me and moves the person on, once, while the original error still reaches the caller.
// 030 US3-S9: a 409 COMMUNITY_ARCHIVED is reported, so a resident page shows the archived notice.
// <!-- REPOWISE:END -->
export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const tokens = inject(TokenService);
  const session = inject(SessionRefreshService);
  const recovery = inject(PropertyAccessRecoveryService);
  const injector = inject(Injector);

  const isApiRequest = req.url.startsWith(environment.apiBaseUrl);

  const addBearer = (r: HttpRequest<unknown>, token: string) =>
    r.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

  const token = tokens.getAccessToken();
  const outReq = isApiRequest && token ? addBearer(req, token) : req;

  return next(outReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && isApiRequest && !req.url.includes('/auth/')) {
        return from(session.refresh()).pipe(
          switchMap(newToken =>
            newToken ? next(addBearer(req, newToken)) : throwError(() => err))
        );
      }
      if (err.status === 403 && isApiRequest && !req.url.includes('/auth/')
          && (err.error as { code?: unknown } | null)?.code === 'PROPERTY_ACCESS_DENIED') {
        void recovery.recover();
      }
      if (err.status === 409 && isApiRequest
          && (err.error as { code?: unknown } | null)?.code === 'COMMUNITY_ARCHIVED') {
        injector.get(ArchivedHomeService).report(req.method);
      }
      return throwError(() => err);
    })
  );
};
