using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.SimpleEmailV2.Model;

namespace HOAManagementCompany.Infrastructure.Payments.Alerts;

/// <summary>
/// Maps an SES/SDK failure to a stable, secret-free reason (026 research R5). The prefix tells a
/// transient outage (<see cref="UnavailablePrefix"/>) from a permanent rejection
/// (<see cref="RejectedPrefix"/>); the Stage-2 sandbox suite Skips the former and Fails the latter.
/// The exception message is never used — the SDK can echo the recipient or access key ID in it.
/// </summary>
public static class SesErrorMapper
{
    public const string UnavailablePrefix = "SES unavailable:";
    public const string RejectedPrefix = "SES rejected:";

    public static string Map(Exception ex)
    {
        var prefix = IsTransient(ex) ? UnavailablePrefix : RejectedPrefix;
        return ex is AmazonServiceException svc
            ? $"{prefix} {ex.GetType().Name} (HTTP {(int)svc.StatusCode})"
            : $"{prefix} {ex.GetType().Name}";
    }

    private static bool IsTransient(Exception ex) => ex switch
    {
        TooManyRequestsException or LimitExceededException => true,
        AmazonServiceException svc => (int)svc.StatusCode >= 500,
        HttpRequestException or SocketException or TimeoutException or OperationCanceledException => true,
        _ => false,
    };
}
