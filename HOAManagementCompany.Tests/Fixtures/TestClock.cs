namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// Hand-written controllable clock (no Microsoft.Extensions.TimeProvider.Testing package — 027 plan
/// "no new packages"). Registered as the app's <see cref="TimeProvider"/> by the ARC and company
/// test bases (030 T017 moved it here so both share it).
/// </summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
