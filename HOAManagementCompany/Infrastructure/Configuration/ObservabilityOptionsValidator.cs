using System;
using FluentValidation;
using HOAManagementCompany.Infrastructure.Observability;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="ObservabilityOptions"/> at startup: sampling ratios must be in the
/// inclusive range [0, 1], the OTLP transport must be the single supported protocol, and the
/// endpoint must be a well-formed absolute URI (008 FR-009/FR-010).
/// </summary>
public sealed class ObservabilityOptionsValidator : AbstractValidator<ObservabilityOptions>
{
    public ObservabilityOptionsValidator()
    {
        RuleFor(x => x.TraceSampleRatio).InclusiveBetween(0d, 1d)
            .WithMessage("Observability:TraceSampleRatio must be between 0 and 1 (inclusive).");
        RuleFor(x => x.SentryTraceSampleRatio).InclusiveBetween(0d, 1d)
            .WithMessage("Observability:SentryTraceSampleRatio must be between 0 and 1 (inclusive).");

        RuleFor(x => x.OtlpProtocol).Equal("http/protobuf")
            .WithMessage("Observability:OtlpProtocol must be 'http/protobuf'.");

        RuleFor(x => x.OtlpEndpoint)
            .Must(BeHttpUri)
            .WithMessage("Observability:OtlpEndpoint must be a valid absolute http or https URI.");

        RuleFor(x => x.TelemetryProxyMaxBodyBytes).GreaterThan(0)
            .WithMessage("Observability:TelemetryProxyMaxBodyBytes must be greater than 0.");
    }

    // An OTLP endpoint is an http(s) URL. Require the scheme explicitly: on Unix, Uri.TryCreate
    // treats bare paths ("/foo") and "host:port" as absolute URIs with file/host schemes, which
    // we must reject.
    private static bool BeHttpUri(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
