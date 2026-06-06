import { APP_INITIALIZER, ApplicationConfig } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { initObservability } from './core/observability/otel.bootstrap';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
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
