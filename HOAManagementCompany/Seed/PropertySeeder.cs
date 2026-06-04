using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;

namespace HOAManagementCompany.Seed;

public class PropertySeeder(ApplicationDbContext db, SeedResult result, ILogger logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // ── Primary resident (Jane Resident – SAKURA-001) ─────────────────
        var primaryOwner = new Owner
        {
            PropertyId          = result.PrimaryPropertyId,
            FirstName           = "Jane",
            LastName            = "Resident",
            Email               = "resident@nekohoa.dev",
            Phone               = "408-555-0101",
            MailingToProperty   = true,
            PaperlessStatements = true,
            SmsReminders        = true,
            VotingRights        = true
        };
        db.Owners.Add(primaryOwner);

        db.AddressHistories.AddRange(
            new AddressHistory
            {
                PropertyId    = result.PrimaryPropertyId,
                EventType     = "created",
                Address       = "1 Sakura Drive, San Jose CA 95101",
                EffectiveDate = new DateOnly(2005, 6, 1)
            },
            new AddressHistory
            {
                PropertyId    = result.PrimaryPropertyId,
                EventType     = "change",
                Address       = "1 Sakura Drive Apt 2, San Jose CA 95101",
                EffectiveDate = new DateOnly(2018, 8, 20)
            },
            new AddressHistory
            {
                PropertyId    = result.PrimaryPropertyId,
                EventType     = "change",
                Address       = "1 Sakura Drive, San Jose CA 95101",
                EffectiveDate = new DateOnly(2022, 3, 15)
            });

        db.DirectoryFields.AddRange(
            new DirectoryField { PropertyId = result.PrimaryPropertyId, FieldKey = "name",    Label = "Full Name", Shared = true },
            new DirectoryField { PropertyId = result.PrimaryPropertyId, FieldKey = "email",   Label = "Email",     Shared = false },
            new DirectoryField { PropertyId = result.PrimaryPropertyId, FieldKey = "phone",   Label = "Phone",     Shared = false },
            new DirectoryField { PropertyId = result.PrimaryPropertyId, FieldKey = "address", Label = "Address",   Shared = true });

        // ── Secondary resident (John Resident – SAKURA-002) ───────────────
        var secondaryOwner = new Owner
        {
            PropertyId = result.SecondaryPropertyId,
            FirstName  = "John",
            LastName   = "Resident",
            Email      = "resident2@nekohoa.dev",
            Phone      = "408-555-0202",
            MailingToProperty = true,
            VotingRights      = true
        };
        db.Owners.Add(secondaryOwner);

        db.DirectoryFields.AddRange(
            new DirectoryField { PropertyId = result.SecondaryPropertyId, FieldKey = "name",    Label = "Full Name", Shared = true },
            new DirectoryField { PropertyId = result.SecondaryPropertyId, FieldKey = "email",   Label = "Email",     Shared = true },
            new DirectoryField { PropertyId = result.SecondaryPropertyId, FieldKey = "phone",   Label = "Phone",     Shared = false },
            new DirectoryField { PropertyId = result.SecondaryPropertyId, FieldKey = "address", Label = "Address",   Shared = true });

        // ── Neighbor properties ───────────────────────────────────────────
        var neighbors = new[]
        {
            (Acct: "SAKURA-003", Addr: "3 Sakura Drive",       City: "San Jose", First: "Maria",   Last: "Santos",    Email: "maria.santos@example.com",    Phone: "408-555-0303", ShareName: true,  ShareEmail: true,  SharePhone: true,  ShareAddress: true),
            (Acct: "SAKURA-004", Addr: "4 Sakura Drive",       City: "San Jose", First: "David",   Last: "Chen",      Email: "d.chen@example.com",          Phone: "408-555-0404", ShareName: true,  ShareEmail: false, SharePhone: true,  ShareAddress: true),
            (Acct: "SAKURA-005", Addr: "5 Sakura Drive",       City: "San Jose", First: "Priya",   Last: "Sharma",    Email: "priya.sharma@example.com",    Phone: "408-555-0505", ShareName: true,  ShareEmail: true,  SharePhone: false, ShareAddress: true),
            (Acct: "SAKURA-006", Addr: "6 Sakura Drive",       City: "San Jose", First: "Carlos",  Last: "Reyes",     Email: "creyes@example.com",          Phone: "408-555-0606", ShareName: true,  ShareEmail: false, SharePhone: false, ShareAddress: true),
            (Acct: "SAKURA-007", Addr: "7 Sakura Drive",       City: "San Jose", First: "Angela",  Last: "Park",      Email: "angela.park@example.com",     Phone: "408-555-0707", ShareName: true,  ShareEmail: true,  SharePhone: true,  ShareAddress: true),
            (Acct: "SAKURA-008", Addr: "8 Sakura Drive",       City: "San Jose", First: "Thomas",  Last: "Nguyen",    Email: "tnguyen@example.com",         Phone: "408-555-0808", ShareName: false, ShareEmail: false, SharePhone: false, ShareAddress: false),
            (Acct: "SAKURA-009", Addr: "9 Sakura Drive",       City: "San Jose", First: "Fatima",  Last: "Al-Hassan", Email: "fatima.ah@example.com",       Phone: "408-555-0909", ShareName: true,  ShareEmail: true,  SharePhone: false, ShareAddress: true),
            (Acct: "SAKURA-010", Addr: "10 Sakura Drive",      City: "San Jose", First: "Robert",  Last: "Kim",       Email: "r.kim@example.com",           Phone: "408-555-1010", ShareName: true,  ShareEmail: false, SharePhone: true,  ShareAddress: true),
            (Acct: "SAKURA-011", Addr: "11 Sakura Drive",      City: "San Jose", First: "Lindiwe", Last: "Dube",      Email: "l.dube@example.com",          Phone: "408-555-1111", ShareName: true,  ShareEmail: true,  SharePhone: true,  ShareAddress: false),
            (Acct: "SAKURA-012", Addr: "12 Sakura Drive",      City: "San Jose", First: "Marco",   Last: "Ferrari",   Email: "mferrari@example.com",        Phone: "408-555-1212", ShareName: true,  ShareEmail: false, SharePhone: false, ShareAddress: true),
        };

        foreach (var n in neighbors)
        {
            var prop = new Property
            {
                Id               = Guid.NewGuid(),
                AccountNumber    = n.Acct,
                CommunityId      = result.CommunityId,
                CommunityName    = "Sakura Heights HOA",
                Address          = n.Addr,
                City             = n.City,
                State            = "CA",
                Zip              = "95101",
                Lot              = n.Acct.Split('-')[1],
                Section          = "1",
                FiscalYear       = 2026,
                YearBuilt        = 2005,
                Status           = "active",
                MonthlyAssessment  = 250m,
                AnnualAssessment   = 3000m,
                AssessmentDueDay   = 1,
                LateFeeAmount      = 50m,
                LateFeeGraceDays   = 15,
                FinanceChargeRate  = 0.015m
            };
            db.Properties.Add(prop);

            db.Owners.Add(new Owner
            {
                PropertyId        = prop.Id,
                FirstName         = n.First,
                LastName          = n.Last,
                Email             = n.Email,
                Phone             = n.Phone,
                MailingToProperty = true,
                VotingRights      = true
            });

            db.DirectoryFields.AddRange(
                new DirectoryField { PropertyId = prop.Id, FieldKey = "name",    Label = "Full Name", Shared = n.ShareName    },
                new DirectoryField { PropertyId = prop.Id, FieldKey = "email",   Label = "Email",     Shared = n.ShareEmail   },
                new DirectoryField { PropertyId = prop.Id, FieldKey = "phone",   Label = "Phone",     Shared = n.SharePhone   },
                new DirectoryField { PropertyId = prop.Id, FieldKey = "address", Label = "Address",   Shared = n.ShareAddress });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("PropertySeeder complete.");
    }
}
