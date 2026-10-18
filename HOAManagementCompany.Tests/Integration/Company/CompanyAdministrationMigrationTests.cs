using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Board;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Company;

/// <summary>
/// 030 T015 — the AddCompanyAdministration migration (data-model.md "Migration outline"; spec
/// Database/runtime). Unlike the 025 backfill test, this one replays the real upgrade: a fresh
/// database is migrated to the migration before this one, seeded with a legacy dataset through raw
/// SQL (the EF model only knows the final schema), then migrated to the latest.
/// </summary>
public class CompanyAdministrationMigrationTests(TestDatabaseFixture fixture) : BoardTestBase(fixture)
{
    private const string PreviousMigration = "20261008222445_AddResidentArcSubmission";
    private const string ThisMigration = "20261018051053_AddCompanyAdministration";

    [Fact]
    public async Task Upgrade_BackfillsCompany_Communities_ArcSettings_AndCancelsArchivedAutopay()
    {
        var cs = await Fixture.CreateFreshDatabaseAsync(targetMigration: PreviousMigration);
        var activeId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        var activeCreated = new DateTimeOffset(2025, 3, 1, 12, 0, 0, TimeSpan.Zero);
        Guid recurringId = Guid.NewGuid(), authorizationId = Guid.NewGuid();

        await using (var conn = await OpenAsync(cs))
        {
            await InsertAsync(conn, "Communities", new()
            {
                ["Id"] = activeId, ["LegalName"] = "Keystone Park HOA, Inc.", ["CommunityName"] = "Keystone Park",
                ["Status"] = "Active", ["CreatedAt"] = activeCreated,
            });
            await InsertAsync(conn, "Communities", new()
            {
                ["Id"] = inactiveId, ["LegalName"] = "Old Mill Commons", ["CommunityName"] = "Old Mill Commons",
                ["Status"] = "Inactive", ["CreatedAt"] = activeCreated,
            });
            var propertyId = Guid.NewGuid();
            await InsertAsync(conn, "Properties", new()
            {
                ["Id"] = propertyId, ["CommunityId"] = inactiveId, ["AccountNumber"] = $"MIG-{propertyId:N}",
                ["Address"] = "1 Mill Rd", ["Status"] = "active",
            });
            await InsertAsync(conn, "RecurringPayments", new()
            {
                ["Id"] = recurringId, ["PropertyId"] = propertyId, ["Status"] = "active",
                ["AmountType"] = "Balance", ["Method"] = "Card", ["DraftDay"] = 1,
            });
            await InsertAsync(conn, "PaymentAuthorizations", new()
            {
                ["Id"] = authorizationId, ["RecurringPaymentId"] = recurringId,
            });
            await ExecAsync(conn, $"""UPDATE "RecurringPayments" SET "CurrentAuthorizationId" = '{authorizationId}' WHERE "Id" = '{recurringId}'""");
        }

        await using (var ctx = Context(cs))
            await ctx.GetService<IMigrator>().MigrateAsync();

        await using var db = Context(cs);

        // Steps 1–2: one company, with the 027 default values.
        var company = Assert.Single(await db.ManagementCompanies.Include(c => c.DefaultSettings).ToListAsync());
        Assert.Equal(ManagementCompany.DefaultName, company.Name);
        var defaults = company.DefaultSettings!;
        Assert.Equal(30, defaults.ReviewPeriodDays);
        Assert.Equal("FlagOverdueOnly", defaults.LapseRule.ToString());
        Assert.Equal("MajorityOfMembers", defaults.DecisionRule.ToString());
        Assert.Equal(7, defaults.ReminderDays);
        Assert.Equal("America/New_York", defaults.TimeZoneId);
        Assert.Equal(CommunityArcSettings.DefaultFormalDisapprovalStatement, defaults.FormalDisapprovalStatement);

        // Step 3: both communities linked; Active stays Active and counts as activated; Inactive → Archived.
        var active = await db.Communities.SingleAsync(c => c.Id == activeId);
        var archived = await db.Communities.SingleAsync(c => c.Id == inactiveId);
        Assert.All(new[] { active, archived }, c => Assert.Equal(company.Id, c.ManagementCompanyId));
        Assert.Equal("keystone park", active.NormalizedName);
        Assert.Equal("Active", active.Status.ToString());
        Assert.Equal(activeCreated, active.ActivatedAt);
        Assert.Equal("Archived", archived.Status.ToString());
        Assert.NotNull(archived.ArchivedAt);
        Assert.Null(archived.ActivatedAt);

        // Step 7: autopay in the archived community is cancelled, its authorization terminated, no email.
        var recurring = await db.RecurringPayments.SingleAsync(r => r.Id == recurringId);
        Assert.Equal("inactive", recurring.Status);
        Assert.Equal(RecurringCancellationReasons.CommunityArchived, recurring.CancellationReason);
        Assert.NotNull((await db.PaymentAuthorizations.SingleAsync(a => a.Id == authorizationId)).TerminatedAt);
        Assert.Equal(0, await db.OutboxMessages.CountAsync());

        // Step 5: every community has its own ARC settings with the 027 constants.
        var arc = await db.CommunityArcSettings.Where(s => s.CommunityId == activeId || s.CommunityId == inactiveId).ToListAsync();
        Assert.Equal(2, arc.Count);
        Assert.All(arc, s =>
        {
            Assert.Equal(30, s.ReviewPeriodDays);
            Assert.Equal(7, s.ReminderDays);
            Assert.Equal(CommunityArcSettings.FirstApplicationNumber, s.NextApplicationNumber);
        });
    }

    [Fact]
    public async Task Upgrade_RefusesNamesThatCollideWithoutCaseOrSpaces_AndAppliesNothing()
    {
        var cs = await Fixture.CreateFreshDatabaseAsync(targetMigration: PreviousMigration);
        await using (var conn = await OpenAsync(cs))
        {
            // Allowed by 025's case-sensitive global index; a collision under 030's rule.
            await InsertAsync(conn, "Communities", new() { ["Id"] = Guid.NewGuid(), ["CommunityName"] = "Keystone Park", ["Status"] = "Active" });
            await InsertAsync(conn, "Communities", new() { ["Id"] = Guid.NewGuid(), ["CommunityName"] = "keystone park ", ["Status"] = "Active" });
        }

        await using (var ctx = Context(cs))
        {
            var ex = await Assert.ThrowsAnyAsync<PostgresException>(() => ctx.GetService<IMigrator>().MigrateAsync());
            Assert.Contains("Community names collide", ex.MessageText);
        }

        await using var check = await OpenAsync(cs);
        Assert.Equal(0L, await ScalarAsync(check,
            $"""SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" = '{ThisMigration}'"""));
        Assert.Equal(0L, await ScalarAsync(check,
            """SELECT count(*) FROM information_schema.tables WHERE table_name = 'ManagementCompanies'"""));
    }

    [Fact]
    public async Task NaturalSortCollation_OrdersHouseNumbersNumerically()
    {
        await using var conn = await OpenAsync(Fixture.ConnectionString);
        await using var cmd = new NpgsqlCommand(
            """SELECT a FROM unnest(ARRAY['711 Keystone Park Dr #10', '711 Keystone Park Dr #9', '711 Keystone Park Dr #29']) a ORDER BY a COLLATE natural_sort""", conn);
        var ordered = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync()) ordered.Add(reader.GetString(0));

        Assert.Equal(new[] { "711 Keystone Park Dr #9", "711 Keystone Park Dr #10", "711 Keystone Park Dr #29" }, ordered);
    }

    [Theory]
    [InlineData("Membership", false, true, false, "CK_Invitations_KindShape")]      // no Role
    [InlineData("Residency", false, false, false, "CK_Invitations_KindShape")]     // no PropertyId
    [InlineData("CompanyAdministrator", true, false, false, "CK_Invitations_KindShape")] // CommunityId set
    [InlineData("Membership", true, true, true, "CK_Invitations_KindShape")]       // Role and no sender
    public async Task InvitationShape_IsEnforcedByTheCheckConstraint(
        string kind, bool withCommunity, bool withRole, bool withoutSender, string expected)
    {
        await using var conn = await OpenAsync(Fixture.ConnectionString);
        var companyId = await ScalarAsync(conn, """SELECT "Id" FROM "ManagementCompanies" ORDER BY "CreatedAt" LIMIT 1""");
        var community = withCommunity ? $"'{TestDataSeeder.SakuraCommunityId}'" : "NULL";
        var role = withRole ? "'BoardMember'" : "NULL";
        var sender = withoutSender ? "NULL" : "'test-user-primary-id'";

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync(conn, $"""
            INSERT INTO "Invitations" ("Id", "Kind", "CompanyId", "CommunityId", "PropertyId", "Role", "Email", "NormalizedEmail",
                "State", "SenderUserId", "CreatedAt", "ResendCount")
            VALUES ('{Guid.NewGuid()}', '{kind}', '{companyId}', {community}, NULL, {role}, 'x@example.com', 'x@example.com',
                'Pending', {sender}, now(), 0)
            """));
        Assert.Equal(expected, ex.ConstraintName);
    }

    [Fact]
    public async Task SecondOpenInvitation_ForTheSameTarget_IsRejected()
    {
        await using var conn = await OpenAsync(Fixture.ConnectionString);
        var companyId = await ScalarAsync(conn, """SELECT "Id" FROM "ManagementCompanies" ORDER BY "CreatedAt" LIMIT 1""");
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        string Insert(string state) => $"""
            INSERT INTO "Invitations" ("Id", "Kind", "CompanyId", "CommunityId", "Role", "Email", "NormalizedEmail",
                "State", "SenderUserId", "CreatedAt", "ResendCount")
            VALUES ('{Guid.NewGuid()}', 'Membership', '{companyId}', '{TestDataSeeder.SakuraCommunityId}', 'BoardMember',
                '{email}', '{email}', '{state}', 'test-user-primary-id', now(), 0)
            """;

        await ExecAsync(conn, Insert("Pending"));
        var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecAsync(conn, Insert("Scheduled")));
        Assert.Equal("IX_Invitations_OpenMembership", ex.ConstraintName);

        // A closed invitation doesn't block a new one.
        await ExecAsync(conn, $"""UPDATE "Invitations" SET "State" = 'Revoked' WHERE "NormalizedEmail" = '{email}'""");
        await ExecAsync(conn, Insert("Pending"));
    }

    private static ApplicationDbContext Context(string cs) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(cs).Options);

    private static async Task<NpgsqlConnection> OpenAsync(string cs)
    {
        var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        return conn;
    }

    private static async Task ExecAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Inserts a row into a legacy-schema table. Columns not given that are NOT NULL and have no
    /// default get a type-appropriate filler, so the test names only the columns it cares about.
    /// </summary>
    private static async Task InsertAsync(NpgsqlConnection conn, string table, Dictionary<string, object?> values)
    {
        var columns = new List<(string Name, string Type, int? MaxLength, bool Required)>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT column_name, data_type, character_maximum_length, is_nullable = 'NO' AND column_default IS NULL
            FROM information_schema.columns WHERE table_name = @t
            """, conn))
        {
            cmd.Parameters.AddWithValue("t", table);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.GetBoolean(3)));
        }

        foreach (var (name, type, maxLength, _) in columns.Where(c => c.Required && !values.ContainsKey(c.Name)))
            values[name] = type switch
            {
                "uuid" => Guid.NewGuid(),
                "integer" or "smallint" => 0,
                "bigint" => 0L,
                "numeric" => 0m,
                "boolean" => false,
                "timestamp with time zone" => DateTimeOffset.UtcNow,
                "date" => DateOnly.FromDateTime(DateTime.UtcNow),
                "jsonb" => "{}",
                _ => Guid.NewGuid().ToString("N")[..Math.Min(maxLength ?? 32, 32)],
            };

        var names = values.Keys.ToList();
        await using var insert = new NpgsqlCommand(
            $"INSERT INTO \"{table}\" ({string.Join(", ", names.Select(k => $"\"{k}\""))}) VALUES ({string.Join(", ", names.Select((_, i) => $"@p{i}"))})",
            conn);
        for (var i = 0; i < names.Count; i++)
        {
            var parameter = insert.Parameters.AddWithValue($"p{i}", values[names[i]] ?? DBNull.Value);
            if (columns.Any(c => c.Name == names[i] && c.Type == "jsonb"))
                parameter.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb;
        }
        await insert.ExecuteNonQueryAsync();
    }
}
