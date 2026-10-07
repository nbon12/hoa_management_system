using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using HOAManagementCompany.Features.Payments;
using HOAManagementCompany.Infrastructure.Payments.Alerts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Alerts;

/// <summary>
/// Unit tests for the SES email adapter (026). A fake SDK client records requests so the decision
/// logic — configured check, no-deliver guard, request shape, error mapping — is proven without a
/// network call. The real SES path is covered by the Stage-2 <c>SesSandboxTests</c>.
/// </summary>
public class SesEmailProviderTests
{
    private const string From = "no-reply@mail.nekohoa.com";

    private static SesOptions Configured(bool simulatorOnly = false) => new()
    {
        Region = "us-east-1",
        FromEmail = From,
        SimulatorOnly = simulatorOnly,
    };

    private static (SesEmailProvider Provider, FakeSesClientFactory Factory) Create(
        SesOptions options, FakeSesClient? client = null)
    {
        var factory = new FakeSesClientFactory(client ?? new FakeSesClient());
        return (new SesEmailProvider(Options.Create(options), factory, NullLogger<SesEmailProvider>.Instance), factory);
    }

    private static AlertMessage Message(string target = "resident@nekohoa.dev", string? subject = "Receipt") =>
        new(target, subject, "Your autopay of $25.00 was received.");

    [Fact]
    public void Channel_is_email() =>
        Assert.Equal("email", Create(Configured()).Provider.Channel);

    [Fact]
    public async Task Not_configured_fails_without_creating_a_client()
    {
        var (provider, factory) = Create(new SesOptions { FromEmail = From }); // no region

        var result = await provider.SendAsync(Message());

        Assert.False(provider.IsConfigured);
        Assert.False(result.Success);
        Assert.Equal("SES is not configured.", result.Error);
        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task Production_config_without_the_guard_setting_defaults_off_and_delivers()
    {
        // 026 US2-4: bind the section the way the app does, with SimulatorOnly not set at all.
        var options = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ses:Region"] = "us-east-1",
                ["Ses:FromEmail"] = From,
            })
            .Build()
            .GetSection(SesOptions.SectionName)
            .Get<SesOptions>()!;
        var client = new FakeSesClient();
        var (provider, _) = Create(options, client);

        var result = await provider.SendAsync(
            new AlertMessage("resident@nekohoa.dev", "NekoHOA payment receipt", "Your autopay of $25.00 was received."));

        Assert.False(options.SimulatorOnly);
        Assert.True(result.Success);
        Assert.Equal(["resident@nekohoa.dev"], Assert.Single(client.Requests).Destination.ToAddresses);
    }

    [Fact]
    public async Task Guard_off_delivers_with_the_expected_request()
    {
        var client = new FakeSesClient();
        var (provider, factory) = Create(Configured(), client);

        var result = await provider.SendAsync(Message());

        Assert.True(result.Success);
        Assert.Equal("msg-123", result.ProviderMessageId);
        Assert.Equal(1, factory.CreateCount);
        var request = Assert.Single(client.Requests);
        Assert.Equal($"\"NekoHOA\" <{From}>", request.FromEmailAddress);
        Assert.Equal(["resident@nekohoa.dev"], request.Destination.ToAddresses);
        Assert.Equal("Receipt", request.Content.Simple.Subject.Data);
        Assert.Equal("Your autopay of $25.00 was received.", request.Content.Simple.Body.Text.Data);
        Assert.Null(request.Content.Simple.Body.Html);
    }

    [Fact]
    public async Task Guard_refuses_a_real_recipient_without_creating_a_client()
    {
        var (provider, factory) = Create(Configured(simulatorOnly: true));

        var result = await provider.SendAsync(Message("resident@nekohoa.dev"));

        Assert.False(result.Success);
        Assert.Contains("SimulatorOnly", result.Error);
        Assert.DoesNotContain("resident@nekohoa.dev", result.Error);
        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task Guard_allows_a_simulator_recipient()
    {
        var client = new FakeSesClient();
        var (provider, factory) = Create(Configured(simulatorOnly: true), client);

        var result = await provider.SendAsync(Message("success@simulator.amazonses.com"));

        Assert.True(result.Success);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(["success@simulator.amazonses.com"], Assert.Single(client.Requests).Destination.ToAddresses);
    }

    [Fact]
    public async Task Missing_subject_uses_the_default_subject()
    {
        var client = new FakeSesClient();
        var (provider, _) = Create(Configured(), client);

        await provider.SendAsync(Message(subject: null));

        Assert.Equal("NekoHOA payment alert", Assert.Single(client.Requests).Content.Simple.Subject.Data);
    }

    [Fact]
    public async Task Rejection_is_a_handled_failure_not_an_exception()
    {
        var (provider, _) = Create(Configured(), new FakeSesClient { Throw = new MessageRejectedException("no") });

        var result = await provider.SendAsync(Message());

        Assert.False(result.Success);
        Assert.StartsWith(SesErrorMapper.RejectedPrefix, result.Error);
    }

    [Fact]
    public async Task Throttling_is_reported_as_unavailable()
    {
        var (provider, _) = Create(Configured(), new FakeSesClient { Throw = new TooManyRequestsException("slow") });

        var result = await provider.SendAsync(Message());

        Assert.False(result.Success);
        Assert.StartsWith(SesErrorMapper.UnavailablePrefix, result.Error);
    }

    [Fact]
    public async Task Non_success_status_without_exception_is_a_rejection()
    {
        var (provider, _) = Create(Configured(), new FakeSesClient { Status = HttpStatusCode.BadRequest });

        var result = await provider.SendAsync(Message());

        Assert.False(result.Success);
        Assert.Equal("SES rejected: HTTP 400", result.Error);
    }

    [Fact]
    public async Task Client_is_created_once_and_reused()
    {
        var (provider, factory) = Create(Configured());

        await provider.SendAsync(Message());
        await provider.SendAsync(Message());

        Assert.Equal(1, factory.CreateCount);
    }

    private sealed class FakeSesClientFactory(FakeSesClient client) : ISesClientFactory
    {
        public int CreateCount { get; private set; }

        public IAmazonSimpleEmailServiceV2 Create(SesOptions options)
        {
            CreateCount++;
            return client;
        }
    }

    internal sealed class FakeSesClient()
        : AmazonSimpleEmailServiceV2Client(new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1)
    {
        public List<SendEmailRequest> Requests { get; } = [];
        public Exception? Throw { get; init; }
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        public override Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Throw is not null) throw Throw;
            return Task.FromResult(new SendEmailResponse { HttpStatusCode = Status, MessageId = "msg-123" });
        }
    }
}
