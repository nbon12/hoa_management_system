// <!-- REPOWISE:START section=observability -->
// Frontend OpenTelemetry Web SDK bootstrap (FR-001/FR-030). Emits traces only —
// document-load + XHR spans — exported as OTLP/HTTP protobuf to the same-API telemetry
// proxy (vendor credentials never reach the browser). W3C `traceparent` is injected on
// outbound XHRs to the API origin via `propagateTraceHeaderCorsUrls`, linking the
// browser span to the backend server span. Initialization must never throw or block app
// startup; failures are swallowed.
// <!-- REPOWISE:END -->

import {
  BatchSpanProcessor,
  WebTracerProvider,
} from '@opentelemetry/sdk-trace-web';
import { ZoneContextManager } from '@opentelemetry/context-zone';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { DocumentLoadInstrumentation } from '@opentelemetry/instrumentation-document-load';
import { XMLHttpRequestInstrumentation } from '@opentelemetry/instrumentation-xml-http-request';

export interface ObservabilityConfig {
  /** Same-API telemetry proxy URL the browser exports OTLP traces to (FR-016). */
  telemetryUrl: string;
  /** API origins to inject the W3C `traceparent` header onto (cross-origin, FR-001). */
  propagateTraceHeaderCorsUrls?: (string | RegExp)[];
  /** Logical service name reported on browser spans. */
  serviceName?: string;
}

let provider: WebTracerProvider | undefined;

/**
 * Initializes the Web Tracer once. Safe to call during app bootstrap: any failure is
 * logged and swallowed so telemetry can never prevent the app from starting (FR-030).
 */
export function initObservability(config: ObservabilityConfig): WebTracerProvider | undefined {
  if (provider) {
    return provider;
  }

  try {
    const exporter = new OTLPTraceExporter({ url: config.telemetryUrl });

    provider = new WebTracerProvider({
      resource: resourceFromAttributes({
        [ATTR_SERVICE_NAME]: config.serviceName ?? 'neko-hoa-web',
      }),
      spanProcessors: [new BatchSpanProcessor(exporter)],
    });

    // ZoneContextManager keeps the active span across Angular's Zone.js async boundaries;
    // register() also installs the default W3C trace-context propagator.
    provider.register({ contextManager: new ZoneContextManager() });

    registerInstrumentations({
      tracerProvider: provider,
      instrumentations: [
        new DocumentLoadInstrumentation(),
        new XMLHttpRequestInstrumentation({
          propagateTraceHeaderCorsUrls: config.propagateTraceHeaderCorsUrls ?? [],
        }),
      ],
    });

    return provider;
  } catch (err) {
    // Never surface telemetry-init failures to the user (FR-008/FR-030).
    console.warn('[observability] OpenTelemetry init failed; continuing without tracing.', err);
    return undefined;
  }
}

/** Test hook: clears the singleton so initialization can be exercised repeatedly. */
export function resetObservabilityForTesting(): void {
  provider = undefined;
}
