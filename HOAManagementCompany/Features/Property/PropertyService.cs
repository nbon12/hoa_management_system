using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Features.Property.Models;
using HOAManagementCompany.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Features.Property;

// <!-- REPOWISE:START domain=property -->
// Property, owner, directory fields, address history; scoped by JWT propertyId.
// <!-- REPOWISE:END -->

public class PropertyService(ApplicationDbContext db)
{
    public async Task<PropertyDto> GetPropertyAsync(Guid propertyId, CancellationToken ct = default)
    {
        var row = await FetchPropertyAsync(propertyId, ct);
        return MapProperty(row.Property, row.CommunityName);
    }

    public async Task<OwnerDto> GetOwnerAsync(Guid propertyId, CancellationToken ct = default)
    {
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.PropertyId == propertyId, ct)
            ?? throw new DomainException("NOT_FOUND", "Owner not found.", 404);
        var row = await FetchPropertyAsync(propertyId, ct);
        return MapOwner(owner, row.Property, row.CommunityName);
    }

    public async Task<OwnerDto> PatchOwnerAsync(Guid propertyId, OwnerPatchRequest req, CancellationToken ct = default)
    {
        var owner = await db.Owners.FirstOrDefaultAsync(o => o.PropertyId == propertyId, ct)
            ?? throw new DomainException("NOT_FOUND", "Owner not found.", 404);
        var row = await FetchPropertyAsync(propertyId, ct);

        var mailingChanged = req.MailingAddress is not null && req.MailingAddress != owner.MailingAddress;

        if (req.FirstName is not null) owner.FirstName = req.FirstName;
        if (req.LastName is not null) owner.LastName = req.LastName;
        if (req.OwnerName2 is not null) owner.OwnerName2 = req.OwnerName2;
        if (req.Email is not null) owner.Email = req.Email;
        if (req.Phone is not null) owner.Phone = req.Phone;
        if (req.MailingToProperty.HasValue) owner.MailingToProperty = req.MailingToProperty.Value;
        if (req.MailingAddress is not null) owner.MailingAddress = req.MailingAddress;
        if (req.PaperlessStatements.HasValue) owner.PaperlessStatements = req.PaperlessStatements.Value;
        if (req.SmsReminders.HasValue) owner.SmsReminders = req.SmsReminders.Value;
        owner.UpdatedAt = DateTimeOffset.UtcNow;

        if (mailingChanged)
            db.AddressHistories.Add(new AddressHistory
            {
                PropertyId = propertyId,
                EventType = "change",
                Address = req.MailingAddress!,
                EffectiveDate = DateOnly.FromDateTime(DateTime.Today)
            });

        await db.SaveChangesAsync(ct);
        return MapOwner(owner, row.Property, row.CommunityName);
    }

    public async Task<IEnumerable<AddressHistoryDto>> GetAddressHistoryAsync(Guid propertyId, CancellationToken ct = default)
    {
        return await db.AddressHistories
            .Where(h => h.PropertyId == propertyId)
            .OrderByDescending(h => h.EffectiveDate)
            .Select(h => new AddressHistoryDto(h.Id, h.EventType, h.Address, h.EffectiveDate))
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<DirectoryFieldDto>> GetDirectoryFieldsAsync(Guid propertyId, CancellationToken ct = default)
    {
        return await db.DirectoryFields
            .Where(d => d.PropertyId == propertyId)
            .Select(d => new DirectoryFieldDto(d.Id, d.FieldKey, d.Label, d.Shared))
            .ToListAsync(ct);
    }

    public async Task<DirectoryFieldDto> PatchDirectoryFieldAsync(Guid propertyId, string key, DirectoryFieldPatchRequest req, CancellationToken ct = default)
    {
        var field = await db.DirectoryFields.FirstOrDefaultAsync(d => d.PropertyId == propertyId && d.FieldKey == key, ct)
            ?? throw new DomainException("NOT_FOUND", $"Directory field '{key}' not found.", 404);

        field.Shared = req.Shared;
        await db.SaveChangesAsync(ct);
        return new DirectoryFieldDto(field.Id, field.FieldKey, field.Label, field.Shared);
    }

    // The community name is the only field either mapper reads off the Community, so it is
    // fetched as a single joined column instead of materializing the whole related entity.
    private async Task<(Domain.Entities.Property Property, string CommunityName)> FetchPropertyAsync(
        Guid propertyId, CancellationToken ct)
    {
        var row = await db.Properties
            .Where(x => x.Id == propertyId)
            .Select(x => new { Property = x, x.Community.CommunityName })
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException("NOT_FOUND", "Property not found.", 404);
        return (row.Property, row.CommunityName);
    }

    private static PropertyDto MapProperty(Domain.Entities.Property p, string communityName) => new(p.Id, p.AccountNumber, p.CommunityId.ToString(), communityName, p.Address, p.City, p.State, p.Zip, p.Lot, p.Phase, p.Section, p.Block, p.FiscalYear, p.YearBuilt, p.Status, p.MonthlyAssessment, p.AnnualAssessment, p.AssessmentDueDay, p.LateFeeAmount, p.LateFeeGraceDays, p.FinanceChargeRate);

    private static OwnerDto MapOwner(Owner o, Domain.Entities.Property p, string communityName) => new(
        o.Id, o.FirstName, o.LastName, o.OwnerName2, o.Email, o.Phone,
        o.MailingToProperty, o.MailingAddress, o.PaperlessStatements, o.SmsReminders, o.VotingRights,
        p.AccountNumber, communityName, $"{p.Address}, {p.City}, {p.State} {p.Zip}",
        p.CreatedAt.ToString("yyyy-MM-dd"));
}
