import { context, propagation, trace } from '@opentelemetry/api';
import { initObservability, resetObservabilityForTesting } from './otel.bootstrap';

describe('OpenTelemetry bootstrap', () => {
  afterEach(() => resetObservabilityForTesting());

  it('initializes without throwing on valid config', () => {
    expect(() =>
      initObservability({
        telemetryUrl: '/api/v1/telemetry',
        propagateTraceHeaderCorsUrls: ['http://localhost:5212'],
      }),
    ).not.toThrow();
  });

  it('returns a tracer provider on init', () => {
    const provider = initObservability({ telemetryUrl: '/api/v1/telemetry' });
    expect(provider).toBeDefined();
  });

  it('registers W3C propagation so traceparent is injected on outbound XHRs', () => {
    initObservability({
      telemetryUrl: '/api/v1/telemetry',
      propagateTraceHeaderCorsUrls: ['http://localhost:5212'],
    });

    const span = trace.getTracer('test').startSpan('outbound-xhr');
    const carrier: Record<string, string> = {};
    context.with(trace.setSpan(context.active(), span), () => {
      propagation.inject(context.active(), carrier);
    });
    span.end();

    expect(carrier['traceparent']).toBeDefined();
    expect(carrier['traceparent']).toMatch(/^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$/);
  });
});
