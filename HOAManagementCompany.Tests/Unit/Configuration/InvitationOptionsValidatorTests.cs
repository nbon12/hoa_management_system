using HOAManagementCompany.Infrastructure.Configuration;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Configuration;

/// <summary>
/// 030 T003 — invitation limits (FR-022, FR-041c; research R12) fail the host fast when
/// misconfigured. The 72-hour expiry is fixed by the spec, so configuration can't drift from it.
/// </summary>
public class InvitationOptionsValidatorTests
{
    private static readonly InvitationOptionsValidator Validator = new();

    [Fact]
    public void Defaults_AreTheSpecifiedLimits_AndValid()
    {
        var options = new InvitationOptions();

        Assert.Equal(72, options.ExpiryHours);
        Assert.Equal(5, options.MaxEmailsPerRecipientPer24h);
        Assert.Equal(60, options.ResendCooldownSeconds);
        Assert.True(Validator.Validate(options).IsValid);
    }

    [Theory]
    [InlineData(71, 5, 60, "ExpiryHours")]
    [InlineData(48, 5, 60, "ExpiryHours")]
    [InlineData(72, 0, 60, "MaxEmailsPerRecipientPer24h")]
    [InlineData(72, 51, 60, "MaxEmailsPerRecipientPer24h")]
    [InlineData(72, 5, -1, "ResendCooldownSeconds")]
    [InlineData(72, 5, 3601, "ResendCooldownSeconds")]
    public void InvalidValues_AreRejected_NamingTheSetting(int expiry, int cap, int cooldown, string setting)
    {
        var result = Validator.Validate(new InvitationOptions
        {
            ExpiryHours = expiry, MaxEmailsPerRecipientPer24h = cap, ResendCooldownSeconds = cooldown
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains(setting));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(50, 3600)]
    public void BoundaryValues_AreValid(int cap, int cooldown)
    {
        Assert.True(Validator.Validate(new InvitationOptions
        {
            MaxEmailsPerRecipientPer24h = cap, ResendCooldownSeconds = cooldown
        }).IsValid);
    }
}
