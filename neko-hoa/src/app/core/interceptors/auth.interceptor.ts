import {
  HttpInterceptorFn, HttpRequest, HttpHandlerFn,
  HttpErrorResponse, HttpClient
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { TokenService } from '../services/token.service';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const tokens = inject(TokenService);
  const http   = inject(HttpClient);

  const addBearer = (r: HttpRequest<unknown>, token: string) =>
    r.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

  const token = tokens.getAccessToken();
  const outReq = token ? addBearer(req, token) : req;

  return next(outReq).pipe(
    catchError((err: HttpErrorResponse) => {
      // Attempt silent refresh only on 401 from non-auth endpoints
      if (err.status === 401 && !req.url.includes('/auth/')) {
        const refresh = tokens.getRefreshToken();
        if (refresh) {
          return http.post<{ token: string; refreshToken: string }>(
            `${environment.apiBaseUrl}/auth/refresh`,
            { refreshToken: refresh }
          ).pipe(
            switchMap(res => {
              tokens.setTokens(res.token, res.refreshToken);
              return next(addBearer(req, res.token));
            }),
            catchError(refreshErr => {
              tokens.clearTokens();
              return throwError(() => refreshErr);
            })
          );
        }
        tokens.clearTokens();
      }
      return throwError(() => err);
    })
  );
};
