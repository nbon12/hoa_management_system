using System.Net;
using System.Net.Sockets;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Alerts;

/// <summary>
/// 026 research R5: SES failures map to a stable "unavailable" (transient) or "rejected" (permanent)
/// reason so the sandbox suite can Skip outages and Fail regressions, and reasons never echo the
/// SDK message (which can carry the recipient or access key ID — FR-009).
/// </summary>
public class SesErrorMapperTests
{
    public static TheoryData<Exception> Transient => new()
    {
        new TooManyRequestsException("throttled"),
        new LimitExceededException("limit"),
        new AmazonSimpleEmailServiceV2Exception("down") { StatusCode = HttpStatusCode.ServiceUnavailable },
        new HttpRequestException("dns"),
        new SocketException(),
        new TimeoutException(),
        new TaskCanceledException(),
        new OperationCanceledException(),
    };

    public static TheoryData<Exception> Permanent => new()
    {
        new MessageRejectedException("rejected"),
        new MailFromDomainNotVerifiedException("unverified"),
        new BadRequestException("bad"),
        new NotFoundException("missing"),
        new AccountSuspendedException("suspended"),
        new SendingPausedException("paused"),
        new AmazonSimpleEmailServiceV2Exception("forbidden") { StatusCode = HttpStatusCode.Forbidden },
        new InvalidOperationException("other"),
    };

    [Theory]
    [MemberData(nameof(Transient))]
    public void Transient_failures_map_to_unavailable(Exception ex) =>
        Assert.StartsWith(SesErrorMapper.UnavailablePrefix, SesErrorMapper.Map(ex));

    [Theory]
    [MemberData(nameof(Permanent))]
    public void Permanent_failures_map_to_rejected(Exception ex) =>
        Assert.StartsWith(SesErrorMapper.RejectedPrefix, SesErrorMapper.Map(ex));

    [Fact]
    public void Reason_includes_http_status_for_service_errors()
    {
        var reason = SesErrorMapper.Map(new MessageRejectedException("x") { StatusCode = HttpStatusCode.BadRequest });

        Assert.Equal("SES rejected: MessageRejectedException (HTTP 400)", reason);
    }

    [Fact]
    public void Reason_never_echoes_the_exception_message()
    {
        var reason = SesErrorMapper.Map(
            new MessageRejectedException("denied for resident@nekohoa.dev with key AKIASECRETVALUE1"));

        Assert.DoesNotContain("resident@nekohoa.dev", reason);
        Assert.DoesNotContain("AKIASECRETVALUE1", reason);
        Assert.Contains(nameof(MessageRejectedException), reason);
    }
}
