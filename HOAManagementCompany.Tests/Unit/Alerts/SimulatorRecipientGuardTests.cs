using HOAManagementCompany.Infrastructure.Payments.Alerts;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Alerts;

/// <summary>
/// 026 FR-005: with <c>Ses:SimulatorOnly</c> on, only the SES mailbox simulator domain may receive
/// mail. Exact (case-insensitive) domain match after trimming — look-alikes are refused.
/// </summary>
public class SimulatorRecipientGuardTests
{
    [Theory]
    [InlineData("success@simulator.amazonses.com")]
    [InlineData("  SUCCESS@Simulator.AmazonSES.com ")]
    [InlineData("bounce@simulator.amazonses.com")]
    public void Simulator_addresses_are_allowed(string target) =>
        Assert.True(SimulatorRecipientGuard.IsAllowed(target));

    [Theory]
    [InlineData("resident@nekohoa.dev")]
    [InlineData("x@simulator.amazonses.com.evil.test")]
    [InlineData("x@evilsimulator.amazonses.com")]
    [InlineData("a@b@simulator.amazonses.com.evil")]
    [InlineData("simulator.amazonses.com")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Everything_else_is_refused(string? target) =>
        Assert.False(SimulatorRecipientGuard.IsAllowed(target));
}
