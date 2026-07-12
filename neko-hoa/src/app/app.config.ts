import { APP_INITIALIZER, ApplicationConfig } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideNgxStripe } from 'ngx-stripe';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { initObservability } from './core/observability/otel.bootstrap';
import { SessionRefreshService, sessionRefreshInitializer } from './core/services/session-refresh';
import { TokenService } from './core/services/token.service';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    // Stripe.js loader. The publishable key (pk_…) is browser-safe and supplied per environment
    // (set at deploy time for prod); components obtain a Stripe instance via injectStripe(key).
    provideNgxStripe(),
    // Initialize OpenTelemetry during startup. The factory is synchronous and guarded,
    // so it can never throw or block app bootstrap (FR-030).
    {
      provide: APP_INITIALIZER,
      multi: true,
      useFactory: () => () => {
        initObservability({
          telemetryUrl: environment.telemetryUrl,
          propagateTraceHeaderCorsUrls: environment.propagateTraceHeaderCorsUrls,
        });
      },
    },
    // 020-D FR-D1 (research D-R2): hint-gated silent refresh — returning users re-hydrate the
    // session from the HttpOnly cookie before protected routes render; anonymous visits skip
    // the call entirely.
    {
      provide: APP_INITIALIZER,
      multi: true,
      useFactory: sessionRefreshInitializer,
      deps: [TokenService, SessionRefreshService],
    },
  ],
};
