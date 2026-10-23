// <!-- REPOWISE:START section=observability -->
// Frontend OpenTelemetry Web SDK bootstrap (FR-001/FR-030). Emits traces only —
// document-load + XHR spans — exported as OTLP/HTTP protobuf to the same-API telemetry
// proxy (vendor credentials never reach the browser). W3C `traceparent` is injected on
// outbound XHRs to the API origin via `propagateTraceHeaderCorsUrls`, linking the
// browser span to the backend server span. Initialization must never throw or block app
// startup; failures are swallowed.
//
// 030 R6: invitation links carry their token in the URL fragment (`/invite#t=…`). Every span URL
// attribute has its fragment stripped before export, so the token never reaches telemetry.
// <!-- REPOWISE:END -->

import type { Attributes, AttributeValue, Context } from '@opentelemetry/api';
import {
  BatchSpanProcessor,
  WebTracerProvider,
} from '@opentelemetry/sdk-trace-web';
import type { ReadableSpan, Span, SpanExporter, SpanProcessor } from '@opentelemetry/sdk-trace-web';
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

/** Attribute keys that hold a URL or part of one (`http.url`, `url.full`, `http.target`, `url.path`, …). */
const URL_ATTRIBUTE_KEY = /url|uri|href|target|path|referr?er|location/i;
/** OTel semconv's dedicated fragment attribute: dropped outright. */
const FRAGMENT_ATTRIBUTE = 'url.fragment';

/** `https://app/invite#t=abc` → `https://app/invite`. Values without a `#` are returned unchanged. */
export function stripUrlFragment(value: string): string {
  const hash = value.indexOf('#');
  return hash === -1 ? value : value.slice(0, hash);
}

function stripValue(value: AttributeValue | undefined): AttributeValue | undefined {
  if (typeof value === 'string') return stripUrlFragment(value);
  if (Array.isArray(value)) {
    return value.map(v => (typeof v === 'string' ? stripUrlFragment(v) : v)) as AttributeValue;
  }
  return value;
}

/** Removes URL fragments from every URL attribute, in place. */
export function stripUrlFragments(attributes: Attributes | undefined): void {
  if (!attributes) return;
  for (const key of Object.keys(attributes)) {
    if (key === FRAGMENT_ATTRIBUTE) {
      delete attributes[key];
    } else if (URL_ATTRIBUTE_KEY.test(key)) {
      attributes[key] = stripValue(attributes[key]);
    }
  }
}

/**
 * Strips URL fragments from span (and span event) URL attributes when a span ends, before any
 * exporter sees it. It runs first in the processor list; attributes are set throughout a span's
 * life (document-load sets `http.url` from `location.href`), so `onEnd` is the place to clean them.
 */
export class FragmentStrippingSpanProcessor implements SpanProcessor {
  onStart(_span: Span, _parentContext: Context): void { /* attributes are cleaned when the span ends */ }

  onEnd(span: ReadableSpan): void {
    stripUrlFragments(span.attributes);
    for (const event of span.events) stripUrlFragments(event.attributes);
  }

  forceFlush(): Promise<void> { return Promise.resolve(); }

  shutdown(): Promise<void> { return Promise.resolve(); }
}

/** The processors every provider uses: fragment stripping first, then batched export. */
export function buildSpanProcessors(exporter: SpanExporter): SpanProcessor[] {
  return [new FragmentStrippingSpanProcessor(), new BatchSpanProcessor(exporter)];
}

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
      spanProcessors: buildSpanProcessors(exporter),
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
