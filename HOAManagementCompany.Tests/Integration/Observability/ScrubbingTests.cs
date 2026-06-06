using System.Diagnostics;
using HOAManagementCompany.Infrastructure.Observability;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Observability;

/// <summary>
/// FR-009: PII / financial / credential values are redacted at the source — in span
/// attributes (the OTel processor) and in structured-log fields (the Serilog enricher) —
/// before anything is exported. Non-sensitive keys are preserved.
/// </summary>
public class ScrubbingTests
{
    private static readonly string[] DefaultKeys =
    {
        "password", "token", "cardNumber", "cardCvv",
        "routingNumber", "accountNumber", "email", "fullName"
    };

    private static readonly ScrubbingPolicy Policy = new(DefaultKeys);

    [Theory]
    [InlineData("password", "hunter2")]
    [InlineData("token", "eyJhbGciOi...")]
    [InlineData("cardNumber", "4111111111111111")]
    [InlineData("cardCvv", "123")]
    [InlineData("routingNumber", "021000021")]
    [InlineData("accountNumber", "000123456789")]
    [InlineData("email", "resident@nekohoa.dev")]
    [InlineData("fullName", "Jane Q. Resident")]
    [InlineData("user.email", "resident@nekohoa.dev")]   // key-substring match
    [InlineData("card_number", "4111111111111111")]      // separator-insensitive match
    public void Processor_RedactsSensitiveSpanAttributes(string key, string value)
    {
        using var processor = new TelemetryScrubbingProcessor(Policy, captureSqlText: true);
        using var listener = AllDataListener();
        using var source = new ActivitySource("scrub-test-spans");

        using var activity = source.StartActivity("op", ActivityKind.Internal)!;
        activity.SetTag(key, value);
        activity.SetTag("http.request.method", "GET"); // non-sensitive control
        activity.Stop();

        processor.OnEnd(activity);

        Assert.Equal(ScrubbingPolicy.Redacted, activity.GetTagItem(key));
        Assert.Equal("GET", activity.GetTagItem("http.request.method"));
    }

    [Theory]
    [InlineData("password", "hunter2")]
    [InlineData("token", "eyJhbGciOi...")]
    [InlineData("cardNumber", "4111111111111111")]
    [InlineData("routingNumber", "021000021")]
    [InlineData("accountNumber", "000123456789")]
    [InlineData("email", "resident@nekohoa.dev")]
    [InlineData("fullName", "Jane Q. Resident")]
    public void Enricher_RedactsSensitiveLogFields(string key, string value)
    {
        var enricher = new TelemetryScrubbingEnricher(Policy);
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplate("msg", Array.Empty<MessageTemplateToken>()),
            new[]
            {
                new LogEventProperty(key, new ScalarValue(value)),
                new LogEventProperty("RequestPath", new ScalarValue("/api/v1/auth/me")),
            });

        enricher.Enrich(logEvent, propertyFactory: null!);

        Assert.Equal(ScrubbingPolicy.Redacted, ((ScalarValue)logEvent.Properties[key]).Value);
        // Non-sensitive fields untouched.
        Assert.Equal("/api/v1/auth/me", ((ScalarValue)logEvent.Properties["RequestPath"]).Value);
    }

    private static ActivityListener AllDataListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
