using HOAManagementCompany.Infrastructure.Configuration;
using HOAManagementCompany.Infrastructure.Observability;
using Xunit;

namespace HOAManagementCompany.UnitTests.Configuration;

public class ObservabilityOptionsValidatorTests
{
    private static readonly ObservabilityOptionsValidator Validator = new();

    // The defaults on ObservabilityOptions form a valid configuration.
    private static ObservabilityOptions Valid() => new();

    [Fact]
    public void DefaultValidConfig_Passes() => Assert.True(Validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(0.25d)]
    public void TraceSampleRatio_WithinRange_Passes(double ratio)
    {
        var o = Valid();
        o.TraceSampleRatio = ratio;
        Assert.True(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData(-0.01d)]
    [InlineData(1.01d)]
    [InlineData(2d)]
    public void TraceSampleRatio_OutOfRange_Fails(double ratio)
    {
        var o = Valid();
        o.TraceSampleRatio = ratio;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData(-0.01d)]
    [InlineData(1.5d)]
    public void SentryTraceSampleRatio_OutOfRange_Fails(double ratio)
    {
        var o = Valid();
        o.SentryTraceSampleRatio = ratio;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData("grpc")]
    [InlineData("http/json")]
    [InlineData("")]
    public void UnsupportedProtocol_Fails(string protocol)
    {
        var o = Valid();
        o.OtlpProtocol = protocol;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData("http://localhost:4318")]
    [InlineData("https://otlp.vendor.example/v1")]
    public void AbsoluteEndpoint_Passes(string endpoint)
    {
        var o = Valid();
        o.OtlpEndpoint = endpoint;
        Assert.True(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uri")]
    [InlineData("/relative/path")]
    public void NonAbsoluteEndpoint_Fails(string endpoint)
    {
        var o = Valid();
        o.OtlpEndpoint = endpoint;
        Assert.False(Validator.Validate(o).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveMaxBodyBytes_Fails(int bytes)
    {
        var o = Valid();
        o.TelemetryProxyMaxBodyBytes = bytes;
        Assert.False(Validator.Validate(o).IsValid);
    }
}
