namespace HOAManagementCompany.Features.Company;

/// <summary>
/// The community profile rules (030 FR-006, FR-017), shared by adding a community and editing its
/// Profile. Messages are design brief 03's. Field names are the request's camelCase names, so
/// <c>errors[].field</c> names the field at fault.
/// </summary>
public static class CommunityFieldRules
{
    public const int LegalNameMax = 200;
    public const int CommunityNameMin = 2;
    public const int CommunityNameMax = 80;
    public const int CountyMax = 100;
    public const int DescriptionMax = 2000;

    public static CompanyErrors.FieldError? LegalName(string? value, bool required = true) =>
        Text("legalName", value, required, "Enter the legal name.",
            v => v.Length > LegalNameMax ? $"Legal name can be at most {LegalNameMax} characters." : null);

    public static CompanyErrors.FieldError? CommunityName(string? value, bool required = true) =>
        Text("communityName", value, required, "Enter a community name.",
            v => v.Length is < CommunityNameMin or > CommunityNameMax
                ? $"Community name must be {CommunityNameMin} to {CommunityNameMax} characters."
                : null);

    public static CompanyErrors.FieldError? County(string? value, bool required = true) =>
        Text("county", value, required, "Enter the county.",
            v => v.Length > CountyMax ? $"County can be at most {CountyMax} characters." : null);

    public static CompanyErrors.FieldError? FormationDate(DateOnly? value, DateOnly today, bool required = true) =>
        value switch
        {
            null when required => new("formationDate", "Enter the formation date."),
            { } date when date > today => new("formationDate", "Formation date can't be in the future."),
            _ => null,
        };

    public static CompanyErrors.FieldError? ManagementStartDate(DateOnly? value, bool required = true) =>
        value is null && required ? new("managementStartDate", "Enter the management start date.") : null;

    public static CompanyErrors.FieldError? Description(string? value) =>
        value is { Length: > DescriptionMax }
            ? new("description", "Description can be at most 2,000 characters.")
            : null;

    private static CompanyErrors.FieldError? Text(string field, string? value, bool required, string missing,
        Func<string, string?> rule)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return required ? new(field, missing) : null;
        return rule(trimmed) is { } message ? new(field, message) : null;
    }
}
