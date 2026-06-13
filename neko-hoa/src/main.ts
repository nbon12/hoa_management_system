import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { environment } from './environments/environment';
import { findMissingRequiredConfig } from './app/core/config/runtime-config.validator';
import { renderConfigError } from './app/core/config/config-error.render';

// Boot-time configuration guard (008-config-validation, FR-017). In production builds, a missing
// required value (API base URL, Stripe publishable key) halts bootstrap and renders a full-page
// error instead of failing later in the browser. Non-production builds are never blocked.
const missingConfig = findMissingRequiredConfig(environment);
if (missingConfig.length > 0) {
  renderConfigError(missingConfig);
} else {
  bootstrapApplication(AppComponent, appConfig)
    .catch((err) => console.error(err));
}
