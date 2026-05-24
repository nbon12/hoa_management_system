namespace HOAManagementCompany.Features.Property.Models;

public record PropertyDto(Guid Id, string AccountNumber, string CommunityId, string CommunityName, string Address, string City, string State, string Zip, string Lot, string? Phase, string Section, string? Block, int FiscalYear, int YearBuilt, string Status, decimal MonthlyAssessment, decimal AnnualAssessment, int AssessmentDueDay, decimal LateFeeAmount, int LateFeeGraceDays, decimal FinanceChargeRate);

public record OwnerDto(Guid Id, string FirstName, string LastName, string? OwnerName2, string Email, string? Phone, bool MailingToProperty, string? MailingAddress, bool PaperlessStatements, bool SmsReminders, bool VotingRights);

public record OwnerPatchRequest(string? FirstName = null, string? LastName = null, string? OwnerName2 = null, string? Email = null, string? Phone = null, bool? MailingToProperty = null, string? MailingAddress = null, bool? PaperlessStatements = null, bool? SmsReminders = null);

public record AddressHistoryDto(Guid Id, string EventType, string Address, DateOnly EffectiveDate);

public record DirectoryFieldDto(Guid Id, string FieldKey, string Label, bool Shared);

public record DirectoryFieldPatchRequest(bool Shared);
