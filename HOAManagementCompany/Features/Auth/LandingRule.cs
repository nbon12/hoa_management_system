using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Features.Auth;

/// <summary>Where the app goes after sign-in (030 research R11). Reported as <c>CurrentUserDto.landing</c>.</summary>
public enum Landing
{
    Board,
    Company,
    Resident,
    NotLive,
    NoHome
}

/// <summary>
/// The sign-in landing order (030 FR-036a, FR-036c), pure so the server and its tests share one
/// rule. A user with a usable home (in an Active community, else an Archived one) keeps the 025
/// mode rule. A user without one lands in board mode when they hold an effective non-resident
/// membership in an Onboarding or Active community, then in the company area as a Company
/// Administrator, then on "isn't live yet" when their only homes are Onboarding, else on
/// "No home linked". The company sign-in page sends a Company Administrator to the company area
/// whatever else they hold, and anyone else to their usual landing.
/// </summary>
public static class LandingRule
{
    public sealed record Input(
        bool HasUsableHome,
        bool HasOnlyOnboardingHomes,
        bool HasBoardMembership,
        bool IsCompanyAdministrator,
        UserMode LastActiveMode,
        bool CompanySignInPage);

    public static Landing Decide(Input input)
    {
        if (input.CompanySignInPage && input.IsCompanyAdministrator)
            return Landing.Company;

        if (input.HasUsableHome)
            return input.LastActiveMode == UserMode.Board && input.HasBoardMembership ? Landing.Board : Landing.Resident;

        if (input.HasBoardMembership)
            return Landing.Board;
        if (input.IsCompanyAdministrator)
            return Landing.Company;
        return input.HasOnlyOnboardingHomes ? Landing.NotLive : Landing.NoHome;
    }
}
