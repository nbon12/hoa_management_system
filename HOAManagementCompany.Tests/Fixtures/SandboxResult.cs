using System.Net.Sockets;
using Stripe;
using Twilio.Exceptions;
using Xunit;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// Wraps a Stage-2 (007) sandbox provider probe with bounded retry and outage-vs-regression
/// classification (FR-005, SC-005).
///
/// <para>
/// A <b>transport / availability</b> failure (timeout, connection reset, provider 5xx) that
/// survives the retries throws <see cref="SkipException"/>, so the test reports <i>Skipped
/// (provider unavailable)</i> rather than Failed — a Stripe/Twilio blip never blocks the release.
/// A <b>domain / assertion</b> failure (4xx, wrong status, failed assert) rethrows unchanged, so a
/// real regression Fails (red) and blocks <c>docker-push</c>.
/// </para>
///
/// <para>
/// The SendGrid and Twilio adapters swallow exceptions into <c>AlertSendResult.Fail</c>, so an
/// outage there surfaces as a failed result, not a thrown exception; this classifier is the concrete
/// home for the providers that throw (notably <see cref="StripeGateway"/>) and for any SDK exception
/// that escapes an adapter. Failure messages never include secret values (FR-010).
/// </para>
/// </summary>
public static class SandboxResult
{
    /// <summary>Runs a probe with no return value under retry + outage classification.</summary>
    public static Task RunAsync(Func<Task> probe, int retries = 3) =>
        RunAsync(async () => { await probe(); return true; }, retries);

    /// <summary>Runs a probe and returns its value under retry + outage classification.</summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> probe, int retries = 3)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                return await probe();
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                last = ex;
                if (attempt < retries)
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)));
            }
            // Domain/assertion exceptions are NOT caught here → they propagate → test Fails.
        }

        // Retries exhausted on a transport/availability error → Skip, do not Fail (SC-005).
        throw new SkipException($"provider unavailable after {retries} attempts: {Describe(last!)}");
    }

    private static bool IsTransient(Exception ex) => ex switch
    {
        TaskCanceledException => true,                 // request timeout
        TimeoutException => true,
        HttpRequestException => true,                  // DNS / socket / TLS at the HTTP layer
        SocketException => true,
        ApiConnectionException => true,                // Twilio could not reach the API
        ApiException tw => tw.Status >= 500,           // Twilio server-side error
        StripeException se => (int)se.HttpStatusCode is 0 or >= 500, // Stripe transport / 5xx
        _ => false,
    };

    private static string Describe(Exception ex) => ex switch
    {
        StripeException se => $"Stripe HTTP {(int)se.HttpStatusCode}",
        ApiException tw => $"Twilio HTTP {tw.Status}",
        ApiConnectionException => "Twilio connection error",
        _ => ex.GetType().Name,
    };
}
