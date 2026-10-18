using System.Text.Json;
using HOAManagementCompany.Features.Company;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Company;

/// <summary>
/// 030 T023 — every new error code carries the contract's HTTP status, and the codes that carry
/// detail (OPEN_WORK, INVITATION_PENDING, VALIDATION_ERROR) put it beside code and message.
/// </summary>
public class CompanyErrorsTests
{
    public static IEnumerable<object[]> Codes() =>
    [
        [CompanyErrors.Forbidden(), "FORBIDDEN", 403],
        [CompanyErrors.CommunityNameTaken(null, false), "COMMUNITY_NAME_TAKEN", 409],
        [CompanyErrors.NoActiveManager("Keystone Park"), "NO_ACTIVE_MANAGER", 409],
        [CompanyErrors.OpenWork("Keystone Park", []), "OPEN_WORK", 409],
        [CompanyErrors.InvalidTransition("x"), "INVALID_TRANSITION", 409],
        [CompanyErrors.CommunityArchived("Old Mill Commons"), "COMMUNITY_ARCHIVED", 409],
        [CompanyErrors.CommunityNotActive(), "COMMUNITY_NOT_ACTIVE", 409],
        [CompanyErrors.ParentArchived("Old Mill Commons"), "PARENT_ARCHIVED", 409],
        [CompanyErrors.ParentCycle("A", "B"), "PARENT_CYCLE", 422],
        [CompanyErrors.InvitationInvalid(), "INVITATION_INVALID", 410],
        [CompanyErrors.InvitationExpired(), "INVITATION_EXPIRED", 410],
        [CompanyErrors.InvitationEmailMismatch(), "INVITATION_EMAIL_MISMATCH", 403],
        [CompanyErrors.InvitationPending(Guid.NewGuid(), "x"), "INVITATION_PENDING", 409],
        [CompanyErrors.AlreadyMember("x"), "ALREADY_MEMBER", 409],
        [CompanyErrors.AlreadyResident("x"), "ALREADY_RESIDENT", 409],
        [CompanyErrors.SelfGrantNotAllowed(), "SELF_GRANT_NOT_ALLOWED", 422],
        [CompanyErrors.LastManager("x"), "LAST_MANAGER", 422],
        [CompanyErrors.LastCompanyAdmin(), "LAST_COMPANY_ADMIN", 422],
        [CompanyErrors.PropertyAccessDenied(), "PROPERTY_ACCESS_DENIED", 403],
        [CompanyErrors.RegistrationFailed(), "REGISTRATION_FAILED", 422],
        [CompanyErrors.RateLimited(), "RATE_LIMITED", 429],
        [CompanyErrors.Validation(new CompanyErrors.FieldError("county", "Enter the county.")), "VALIDATION_ERROR", 422],
    ];

    [Theory]
    [MemberData(nameof(Codes))]
    public void EachCode_HasTheContractStatus(Features.Auth.DomainException error, string code, int status)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(status, error.StatusCode);
        var body = error.ToBody();
        Assert.Equal(code, body["code"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)body["message"]));
    }

    [Fact]
    public void OpenWork_ListsEachItem_InTheMessageAndTheBody()
    {
        var error = CompanyErrors.OpenWork("Keystone Crossing Master",
        [
            new("ArcApplication", "ARC-1041", "Open"),
            new("SubAssociation", "Keystone Crossing SF", "Active"),
        ]);

        Assert.Contains("ARC-1041 · Open", error.Message);
        Assert.Contains("Keystone Crossing SF · Active", error.Message);
        var json = JsonSerializer.Serialize(error.ToBody(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"items\":[{\"kind\":\"ArcApplication\",\"label\":\"ARC-1041\",\"status\":\"Open\"}", json);
    }

    [Fact]
    public void InvitationPending_CarriesTheInvitationId()
    {
        var id = Guid.NewGuid();
        Assert.Equal(id, CompanyErrors.InvitationPending(id, "x").ToBody()["invitationId"]);
    }

    [Fact]
    public void Validation_NamesEachField()
    {
        var json = JsonSerializer.Serialize(CompanyErrors.Validation(
            new("legalName", "Enter the legal name."), new("county", "Enter the county.")).ToBody());
        Assert.Contains("\"field\":\"legalName\"", json);
        Assert.Contains("\"field\":\"county\"", json);
    }

    [Fact]
    public void NameTaken_NamesTheOtherCommunity_OnlyWhenGivenOne()
    {
        Assert.Equal("That community name is unavailable.", CompanyErrors.CommunityNameTaken(null, false).Message);
        Assert.StartsWith("Keystone Park is already", CompanyErrors.CommunityNameTaken("Keystone Park", false).Message);
        Assert.Contains("archived community", CompanyErrors.CommunityNameTaken("Old Mill Commons", true).Message);
    }

    [Fact]
    public void EmailMismatch_NeverContainsAnEmailAddress()
    {
        Assert.DoesNotContain("@", CompanyErrors.InvitationEmailMismatch().Message);
    }
}
