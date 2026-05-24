using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;

namespace HOAManagementCompany.Seed;

public class PropertySeeder(ApplicationDbContext db, SeedResult result, ILogger logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var owner = new Owner
        {
            PropertyId = result.PrimaryPropertyId,
            FirstName = "Jane",
            LastName = "Resident",
            Email = "resident@nekohoa.dev",
            Phone = "408-555-0101",
            MailingToProperty = true,
            VotingRights = true
        };
        db.Owners.Add(owner);

        db.AddressHistories.AddRange(
            new AddressHistory
            {
                PropertyId = result.PrimaryPropertyId,
                EventType = "created",
                Address = "1 Sakura Drive, San Jose CA 95101",
                EffectiveDate = new DateOnly(2005, 6, 1)
            },
            new AddressHistory
            {
                PropertyId = result.PrimaryPropertyId,
                EventType = "change",
                Address = "1 Sakura Drive Apt 2, San Jose CA 95101",
                EffectiveDate = new DateOnly(2022, 3, 15)
            });

        db.DirectoryFields.AddRange(
            new DirectoryField { PropertyId = result.PrimaryPropertyId, FieldKey = "name", Label = "Full Name", Shared = true },
            new DirectoryField { PropertyId = result.PrimaryPropertyId, FieldKey = "email", Label = "Email", Shared = false },
            new DirectoryField { PropertyId = result.PrimaryPropertyId, FieldKey = "phone", Label = "Phone", Shared = false },
            new DirectoryField { PropertyId = result.PrimaryPropertyId, FieldKey = "address", Label = "Address", Shared = true });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("PropertySeeder complete.");
    }
}
