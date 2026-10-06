using System.Net;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using HOAManagementCompany.Tests.Fixtures;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Alerts;

/// <summary>
/// 026 FR-012 (US1-4 and the outage edge case): <see cref="SandboxResult.SkipIfUnavailable"/> turns an
/// SES outage into Skipped, while a rejection such as invalid credentials is left alone so the sandbox
/// assertion Fails and blocks the release.
/// </summary>
public class SesSandboxClassificationTests
{
    [Fact]
    public void Outage_result_is_skipped() =>
        Assert.Throws<SkipException>(() => SandboxResult.SkipIfUnavailable(
            AlertSendResult.Fail(SesErrorMapper.Map(new TooManyRequestsException("throttled")))));

    [Fact]
    public void Invalid_credentials_result_is_not_skipped()
    {
        var result = AlertSendResult.Fail(SesErrorMapper.Map(
            new AmazonSimpleEmailServiceV2Exception("The security token included in the request is invalid.")
            {
                StatusCode = HttpStatusCode.Forbidden,
            }));

        Assert.Null(Record.Exception(() => SandboxResult.SkipIfUnavailable(result)));
        Assert.StartsWith(SesErrorMapper.RejectedPrefix, result.Error);
    }

    [Fact]
    public void Success_result_is_not_skipped() =>
        Assert.Null(Record.Exception(() => SandboxResult.SkipIfUnavailable(AlertSendResult.Ok("msg-123"))));
}
