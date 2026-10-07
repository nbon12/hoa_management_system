using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using HOAManagementCompany.Features.Payments;

namespace HOAManagementCompany.Infrastructure.Payments.Alerts;

/// <summary>
/// Creates the SESv2 client for <see cref="SesEmailProvider"/>. The seam lets unit tests prove the
/// provider makes no network call when it refuses a send (026 FR-004/FR-005).
/// </summary>
public interface ISesClientFactory
{
    IAmazonSimpleEmailServiceV2 Create(SesOptions options);
}

/// <summary>
/// Thin SDK adapter — excluded from coverage like <c>StripeGateway</c>. Uses the static send-only
/// IAM keys when both are set, otherwise the AWS default credential chain (026 research R3).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SesClientFactory : ISesClientFactory
{
    public IAmazonSimpleEmailServiceV2 Create(SesOptions options)
    {
        var region = RegionEndpoint.GetBySystemName(options.Region);
        return !string.IsNullOrWhiteSpace(options.AccessKeyId) && !string.IsNullOrWhiteSpace(options.SecretAccessKey)
            ? new AmazonSimpleEmailServiceV2Client(
                new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), region)
            : new AmazonSimpleEmailServiceV2Client(region);
    }
}
