using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Board.Architectural;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Board.Architectural;

/// <summary>T063 — ArcApplicationFactory: numbering, due date, snapshots and the revision guard.</summary>
public class ArcApplicationFactoryTests(TestDatabaseFixture fixture) : ArcTestBase(fixture)
{
    private static ArcNewApplication Input(Guid propertyId, DateOnly received) =>
        new(propertyId, "Praneeth Pattyam", ArcProjectType.Fence, "Fence", "Rear fence", received, null, []);

    [Fact]
    public async Task CreateFromSettings_AllocatesSequentialNumbers_FromSettingsDefaults()
    {
        var s = await CreateScenarioAsync(1);
        using var scope = NewScope();
        var factory = scope.ServiceProvider.GetRequiredService<ArcApplicationFactory>();

        var first = await factory.CreateFromSettingsAsync(Input(s.PropertyId, new DateOnly(2026, 5, 1)), default);
        var second = await factory.CreateFromSettingsAsync(Input(s.PropertyId, new DateOnly(2026, 5, 2)), default);
        await Db(scope).SaveChangesAsync();

        Assert.Equal(1001, first.ApplicationNumber);
        Assert.Equal(1002, second.ApplicationNumber);
        Assert.Equal(new DateOnly(2026, 5, 31), first.DueDate);
        Assert.Equal(ArcDecisionRule.MajorityOfMembers, first.DecisionRule);
        Assert.Equal(ArcLapseRule.FlagOverdueOnly, first.LapseRule);
        Assert.Equal("America/New_York", first.TimeZoneId);
    }

    [Theory]
    [InlineData(ArcApplicationStatus.Open, null)]
    [InlineData(ArcApplicationStatus.DecisionReached, ArcOutcome.Denied)]
    [InlineData(ArcApplicationStatus.Closed, ArcOutcome.Approved)]
    public async Task CreateRevision_IsRefusedUnlessClosedAndDenied(ArcApplicationStatus status, ArcOutcome? outcome)
    {
        var s = await CreateScenarioAsync(1);
        var v1 = await CreateApplicationAsync(s, new AppSpec { Status = status, Outcome = outcome });
        using var scope = NewScope();
        var factory = scope.ServiceProvider.GetRequiredService<ArcApplicationFactory>();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            factory.CreateRevisionAsync(v1, Input(s.PropertyId, Today), [], default));
        Assert.Equal("REVISION_NOT_ALLOWED", ex.Code);
    }

    [Fact]
    public async Task CreateRevision_OfClosedDenial_IncrementsRevisionAndLinks()
    {
        var s = await CreateScenarioAsync(1);
        var v1 = await CreateApplicationAsync(s, new AppSpec
        {
            Number = 2042, Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Denied, Wording = ArcDenialWording.Denied
        });
        using var scope = NewScope();
        var factory = scope.ServiceProvider.GetRequiredService<ArcApplicationFactory>();

        var v2 = await factory.CreateRevisionAsync(v1, Input(s.PropertyId, Today), [], default);
        await Db(scope).SaveChangesAsync();

        Assert.Equal(2042, v2.ApplicationNumber);
        Assert.Equal(2, v2.Revision);
        Assert.Equal(v1, v2.PreviousRevisionId);
        Assert.Equal(ArcApplicationStatus.Open, v2.Status);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            factory.CreateRevisionAsync(v1, Input(s.PropertyId, Today), [], default));
        Assert.Equal("REVISION_NOT_ALLOWED", ex.Code); // a newer revision already exists
    }
}
