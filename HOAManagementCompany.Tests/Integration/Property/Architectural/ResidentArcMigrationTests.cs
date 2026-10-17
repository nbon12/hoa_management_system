using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Property.Architectural;

/// <summary>
/// 029 T011 — the AddResidentArcSubmission migration on the shared Testcontainers database. It adds the
/// draft tables and nullable columns only, so 027-shaped rows still round-trip unchanged.
/// </summary>
public class ResidentArcMigrationTests(TestDatabaseFixture fixture) : ResidentArcTestBase(fixture)
{
    [Theory]
    [InlineData("ArchitecturalApplicationDrafts", "Id")]
    [InlineData("ArchitecturalDraftAttachments", "StorageKey")]
    [InlineData("ArchitecturalApplications", "PlannedStartDate")]
    [InlineData("ArchitecturalApplications", "WithdrawnByUserId")]
    [InlineData("ArchitecturalInfoRequests", "ResponseMessage")]
    [InlineData("ArchitecturalAttachments", "InfoRequestId")]
    public async Task Migration_AddsTablesAndColumns(string table, string column)
    {
        var exists = await WithDbAsync(db => db.Database
            .SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = {table} AND column_name = {column}) AS \"Value\"")
            .SingleAsync());
        Assert.True(exists);
    }

    [Theory]
    [InlineData("ReceivedDate")]
    [InlineData("DueDate")]
    [InlineData("ApplicationNumber")]
    public async Task Migration_LeavesExisting027ColumnsNonNullable(string column)
    {
        var nullable = await WithDbAsync(db => db.Database
            .SqlQuery<string>($"SELECT is_nullable AS \"Value\" FROM information_schema.columns WHERE table_name = 'ArchitecturalApplications' AND column_name = {column}")
            .SingleAsync());
        Assert.Equal("NO", nullable);
    }

    [Fact]
    public async Task A027ShapedApplication_RoundTripsWithNullResidentFields()
    {
        var r = await CreateResidentAsync();
        var id = await SeedApplicationAsync(r, new AppSpec());

        var row = await WithDbAsync(db => db.ArchitecturalApplications.SingleAsync(a => a.Id == id));

        Assert.Equal(ArcApplicationStatus.Open, row.Status);
        Assert.Null(row.PlannedStartDate);
        Assert.Null(row.ContractorName);
        Assert.Null(row.AcknowledgedAt);
        Assert.Null(row.WithdrawnAt);
    }

    [Fact]
    public async Task DraftCheckConstraint_RejectsCompletionBeforeStart()
    {
        var r = await CreateResidentAsync();

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => WithDbAsync(async db =>
        {
            db.ArchitecturalApplicationDrafts.Add(new ArchitecturalApplicationDraft
            {
                CommunityId = r.CommunityId, PropertyId = r.PropertyId, ProjectType = ArcProjectType.Fence,
                PlannedStartDate = Finish, PlannedCompletionDate = Start
            });
            return await db.SaveChangesAsync();
        }));
        Assert.Equal(PostgresErrorCodes.CheckViolation, (ex.InnerException as PostgresException)?.SqlState);
    }

    [Fact]
    public async Task UniquePreviousRevisionIndex_RejectsASecondRevisionDraft()
    {
        var r = await CreateResidentAsync();
        var v1 = await SeedApplicationAsync(r, new AppSpec { Status = ArcApplicationStatus.Closed, Outcome = ArcOutcome.Denied });
        ArchitecturalApplicationDraft Draft() => new()
        {
            CommunityId = r.CommunityId, PropertyId = r.PropertyId, ProjectType = ArcProjectType.Fence, PreviousRevisionId = v1
        };
        await WithDbAsync(async db => { db.ArchitecturalApplicationDrafts.Add(Draft()); return await db.SaveChangesAsync(); });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            WithDbAsync(async db => { db.ArchitecturalApplicationDrafts.Add(Draft()); return await db.SaveChangesAsync(); }));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, (ex.InnerException as PostgresException)?.SqlState);
    }
}
