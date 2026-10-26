import { context, propagation, trace } from '@opentelemetry/api';
import { BasicTracerProvider, InMemorySpanExporter, SimpleSpanProcessor } from '@opentelemetry/sdk-trace-web';
import {
  FragmentStrippingSpanProcessor, buildSpanProcessors, initObservability, resetObservabilityForTesting,
  stripUrlFragment, stripUrlFragments,
} from './otel.bootstrap';

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

// 030 T057 / R6: an invitation link's token sits in the URL fragment (`/invite#t=…`). No span URL
// attribute may carry it to telemetry.
describe('OpenTelemetry URL fragment stripping (030 R6)', () => {
  const SECRET = 'Qm9iIG9uIGEgc2hhcmVkIGxhcHRvcC1fXy0tLS0tLS0';

  it('stripUrlFragment drops everything from the first #', () => {
    expect(stripUrlFragment(`https://app.nekohoa.com/invite#t=${SECRET}`)).toBe('https://app.nekohoa.com/invite');
    expect(stripUrlFragment('/invite#t=x#y')).toBe('/invite');
    expect(stripUrlFragment('https://api.nekohoa.com/api/v1/invitations/preview')).toBe('https://api.nekohoa.com/api/v1/invitations/preview');
  });

  it('strips every URL attribute, drops url.fragment, and leaves other attributes alone', () => {
    const attrs: Record<string, unknown> = {
      'http.url': `https://app/invite#t=${SECRET}`,
      'url.full': `https://app/invite#t=${SECRET}`,
      'http.target': `/invite#t=${SECRET}`,
      'location.href': `https://app/invite#t=${SECRET}`,
      'url.fragment': `t=${SECRET}`,
      'custom.urls': [`https://a/#t=${SECRET}`, 'https://b/'],
      'http.status_code': 200,
      'note': 'a # that is not a url',
    };
    stripUrlFragments(attrs as never);
    expect(attrs).toEqual({
      'http.url': 'https://app/invite',
      'url.full': 'https://app/invite',
      'http.target': '/invite',
      'location.href': 'https://app/invite',
      'custom.urls': ['https://a/', 'https://b/'],
      'http.status_code': 200,
      'note': 'a # that is not a url',
    });
    expect(() => stripUrlFragments(undefined)).not.toThrow();
  });

  async function exportedSpan(processors: (exporter: InMemorySpanExporter) => ConstructorParameters<typeof BasicTracerProvider>[0]) {
    const exporter = new InMemorySpanExporter();
    const tracerProvider = new BasicTracerProvider(processors(exporter));
    const span = tracerProvider.getTracer('test').startSpan('documentFetch', {
      attributes: { 'http.url': `https://app.nekohoa.com/invite#t=${SECRET}` },
    });
    span.setAttribute('url.full', `https://app.nekohoa.com/invite#t=${SECRET}`);
    span.addEvent('fetchStart', { 'http.url': `https://app.nekohoa.com/invite#t=${SECRET}` });
    span.end();
    await tracerProvider.forceFlush();
    const [finished] = exporter.getFinishedSpans();
    await tracerProvider.shutdown();
    return finished;
  }

  it('the processor removes the token from span and span-event attributes before export', async () => {
    const span = await exportedSpan(exporter => ({
      spanProcessors: [new FragmentStrippingSpanProcessor(), new SimpleSpanProcessor(exporter)],
    }));
    expect(span.attributes['http.url']).toBe('https://app.nekohoa.com/invite');
    expect(span.attributes['url.full']).toBe('https://app.nekohoa.com/invite');
    expect(span.events[0].attributes!['http.url']).toBe('https://app.nekohoa.com/invite');
    expect(JSON.stringify(span.attributes)).not.toContain(SECRET);
  });

  it('the bootstrap processor list strips before the batch exporter sees the span', async () => {
    const processors = buildSpanProcessors(new InMemorySpanExporter());
    expect(processors[0]).toEqual(jasmine.any(FragmentStrippingSpanProcessor));

    const span = await exportedSpan(exporter => ({ spanProcessors: buildSpanProcessors(exporter) }));
    expect(span.attributes['http.url']).toBe('https://app.nekohoa.com/invite');
    expect(JSON.stringify(span.attributes)).not.toContain(SECRET);
    await Promise.all(processors.map(p => p.shutdown()));
  });

  it('the stripping processor has nothing to flush or shut down', async () => {
    const p = new FragmentStrippingSpanProcessor();
    await expectAsync(p.forceFlush()).toBeResolved();
    await expectAsync(p.shutdown()).toBeResolved();
  });
});
