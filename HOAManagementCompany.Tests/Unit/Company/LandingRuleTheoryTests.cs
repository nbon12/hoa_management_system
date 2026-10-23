using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Company;

/// <summary>
/// 030 T030 — where a user lands after sign-in (FR-036a, FR-036c; research R11). With a usable home
/// (Active, else Archived) the 025 mode rule applies. Without one: board mode if they hold an
/// effective non-resident membership in an Onboarding or Active community; otherwise the company
/// area for a Company Administrator; otherwise "isn't live yet" when their only homes are
/// Onboarding; otherwise "No home linked". The company sign-in page sends a Company Administrator
/// to the company area whatever else they hold.
/// </summary>
public class LandingRuleTheoryTests
{
    public static IEnumerable<object[]> Cases() =>
    [
        // label, usable home, onboarding-only homes, board membership, company admin, last mode, company page, expected
        // US9-S1: a Company Administrator with no home and no membership.
        ["US9-S1", false, false, false, true, UserMode.Resident, false, Landing.Company],
        // US9-S2: an effective manager with no home lands in board mode.
        ["US9-S2", false, false, true, false, UserMode.Resident, false, Landing.Board],
        ["no home · board beats company", false, false, true, true, UserMode.Resident, false, Landing.Board],
        // US9-S3: nothing at all.
        ["US9-S3", false, false, false, false, UserMode.Resident, false, Landing.NoHome],
        // US9-S5: only Onboarding homes.
        ["US9-S5", false, true, false, false, UserMode.Resident, false, Landing.NotLive],
        ["onboarding homes · company admin first", false, true, false, true, UserMode.Resident, false, Landing.Company],
        ["onboarding homes · board first", false, true, true, false, UserMode.Resident, false, Landing.Board],
        // With a usable home, the 025 mode rule applies.
        ["home · resident mode", true, false, false, false, UserMode.Resident, false, Landing.Resident],
        ["home · board mode with membership", true, false, true, false, UserMode.Board, false, Landing.Board],
        ["home · board mode without membership", true, false, false, false, UserMode.Board, false, Landing.Resident],
        ["home · resident mode with membership", true, false, true, false, UserMode.Resident, false, Landing.Resident],
        ["home · company admin on the usual page", true, false, false, true, UserMode.Resident, false, Landing.Resident],
        // US9-S7: the company sign-in page, for a Company Administrator, whatever else they hold.
        ["US9-S7 · home and board", true, false, true, true, UserMode.Board, true, Landing.Company],
        ["US9-S7 · no home", false, false, false, true, UserMode.Resident, true, Landing.Company],
        // US9-S8: the company page for anyone else gives the usual landing.
        ["US9-S8 · home", true, false, false, false, UserMode.Resident, true, Landing.Resident],
        ["US9-S8 · board, no home", false, false, true, false, UserMode.Resident, true, Landing.Board],
        ["US9-S8 · nothing", false, false, false, false, UserMode.Resident, true, Landing.NoHome],
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Decide_FollowsTheFR036aOrder(
        string label, bool usableHome, bool onboardingHomesOnly, bool boardMembership, bool companyAdministrator,
        UserMode lastMode, bool companySignInPage, Landing expected)
    {
        var actual = LandingRule.Decide(new LandingRule.Input(
            usableHome, onboardingHomesOnly, boardMembership, companyAdministrator, lastMode, companySignInPage));
        Assert.True(expected == actual, $"{label}: expected {expected}, got {actual}");
    }
}
