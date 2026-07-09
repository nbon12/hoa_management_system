import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiError } from '../api/api-error';

/**
 * Central error normalization (015 US6, FR-020): converts every failed API response into a typed
 * `ApiError` carrying the backend's uniform `{ code, message }` envelope (015 US2). Components
 * catch `ApiError` and present `err.message` — no per-component `HttpErrorResponse` parsing.
 * 401s are left to the auth interceptor's refresh flow (it runs closer to the backend).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        const envelope = err.error as { code?: string; message?: string } | null;
        return throwError(() => new ApiError(
          envelope?.code ?? `HTTP_${err.status}`,
          envelope?.message ?? 'Something went wrong. Please try again.',
          err.status,
        ));
      }
      return throwError(() => err);
    }),
  );
