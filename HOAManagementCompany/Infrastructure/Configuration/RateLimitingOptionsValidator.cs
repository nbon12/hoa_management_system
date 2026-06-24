using FluentValidation;

namespace HOAManagementCompany.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="RateLimitingOptions"/> at startup (014-post-deploy-hardening, constitution §8):
/// every permit count must be at least 1, and the trusted-edge secret header name/value must be
/// configured together or not at all (a partial pair is a misconfiguration that would silently fail
/// to trust the edge). The service fails fast on violation rather than deferring to request time.
/// </summary>
public sealed class RateLimitingOptionsValidator : AbstractValidator<RateLimitingOptions>
{
    public RateLimitingOptionsValidator()
    {
        RuleFor(x => x.AuthPermitsPerMinute).GreaterThanOrEqualTo(1)
            .WithMessage("RateLimiting:AuthPermitsPerMinute must be at least 1.");
        RuleFor(x => x.PaymentsPermitsPerMinute).GreaterThanOrEqualTo(1)
            .WithMessage("RateLimiting:PaymentsPermitsPerMinute must be at least 1.");
        RuleFor(x => x.UnknownPermitsPerMinute).GreaterThanOrEqualTo(1)
            .WithMessage("RateLimiting:UnknownPermitsPerMinute must be at least 1.");

        // Both-or-neither: a half-configured trusted edge can never trust a request, which would
        // silently route every client to the shared "unknown" bucket (a global-throttle regression).
        RuleFor(x => x.TrustedEdge).Must(BothOrNeither)
            .WithMessage("RateLimiting:TrustedEdge:SecretHeaderName and SecretHeaderValue must both be set or both be empty.");
    }

    private static bool BothOrNeither(TrustedEdgeOptions edge)
    {
        var hasName = !string.IsNullOrWhiteSpace(edge.SecretHeaderName);
        var hasValue = !string.IsNullOrWhiteSpace(edge.SecretHeaderValue);
        return hasName == hasValue;
    }
}
