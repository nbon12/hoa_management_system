namespace HOAManagementCompany.Seed;

public record SeedResult(
    string PrimaryUserId,
    string SecondaryUserId,
    Guid PrimaryPropertyId,
    Guid SecondaryPropertyId,
    string CommunityId = "SAKURA");
