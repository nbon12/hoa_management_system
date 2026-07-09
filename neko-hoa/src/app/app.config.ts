import { APP_INITIALIZER, ApplicationConfig } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideNgxStripe } from 'ngx-stripe';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { initObservability } from './core/observability/otel.bootstrap';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withComponentInputBinding()),
    // errorInterceptor runs after authInterceptor on the response path, normalizing the backend's
    // uniform { code, message } envelope into a typed ApiError (015 US6, FR-020).
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
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
  ],
};
