using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityAndMembership : Migration
    {
        // The eight community-scoped tables whose loose string CommunityId handle
        // migrates to a Community GUID foreign key (spec FR-006). Order does not
        // matter for the per-table column swap; Properties is backfilled first
        // because it carries the handle↔name mapping the others join through.
        private static readonly string[] ScopedTables =
        {
            "Properties", "Violations", "Announcements", "Polls",
            "HoaDocuments", "CalendarEvents", "CommunityExpenses", "HoaPaymentConfigs"
        };

        // Original indexes on the old string CommunityId column, recreated on the new
        // uuid column after the swap.
        private static readonly (string Table, string Name, string Columns, bool Unique)[] ScopedIndexes =
        {
            ("Properties", "IX_Properties_CommunityId", "\"CommunityId\"", false),
            ("Violations", "IX_Violations_CommunityId", "\"CommunityId\"", false),
            ("Announcements", "IX_Announcements_CommunityId", "\"CommunityId\"", false),
            ("Polls", "IX_Polls_CommunityId", "\"CommunityId\"", false),
            ("HoaDocuments", "IX_HoaDocuments_CommunityId", "\"CommunityId\"", false),
            ("CalendarEvents", "IX_CalendarEvents_CommunityId", "\"CommunityId\"", false),
            ("CommunityExpenses", "IX_CommunityExpenses_CommunityId_FiscalYear", "\"CommunityId\", \"FiscalYear\"", false),
            ("HoaPaymentConfigs", "IX_HoaPaymentConfigs_CommunityId", "\"CommunityId\"", true),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. New enum column on the identity user (FR-022) ─────────────────
            migrationBuilder.AddColumn<int>(
                name: "LastActiveMode",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // ── 2. New tables (independent of the eight scoped tables) ───────────
            migrationBuilder.CreateTable(
                name: "Communities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalName = table.Column<string>(type: "text", nullable: false),
                    CommunityName = table.Column<string>(type: "text", nullable: false),
                    County = table.Column<string>(type: "text", nullable: true),
                    FormationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ManagementStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ParentCommunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Communities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Communities_Communities_ParentCommunityId",
                        column: x => x.ParentCommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityMemberships_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Communities_CommunityName", table: "Communities",
                column: "CommunityName", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Communities_ParentCommunityId", table: "Communities",
                column: "ParentCommunityId");
            migrationBuilder.CreateIndex(
                name: "IX_CommunityMemberships_CommunityId", table: "CommunityMemberships",
                column: "CommunityId");
            migrationBuilder.CreateIndex(
                name: "IX_CommunityMemberships_UserId", table: "CommunityMemberships",
                column: "UserId");
            migrationBuilder.CreateIndex(
                name: "IX_CommunityMemberships_UserId_CommunityId_Role", table: "CommunityMemberships",
                columns: new[] { "UserId", "CommunityId", "Role" }, unique: true);

            // ── 3. Rename each old string column aside and add the new uuid column ─
            // (nullable for now; backfilled below, then made NOT NULL). Postgres drops
            // the old index with the renamed column when it is dropped in step 6.
            foreach (var t in ScopedTables)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE \"{t}\" RENAME COLUMN \"CommunityId\" TO \"OldCommunityId\";");
                migrationBuilder.AddColumn<Guid>(
                    name: "CommunityId", table: t, type: "uuid", nullable: true);
            }

            // ── 4. Backfill Communities from the distinct (handle, name) pairs on
            // Properties. Idempotent — insert only when no Community with that name
            // exists (FR-005). Skips blank names. Requires pgcrypto's gen_random_uuid,
            // available by default on Neon/PostgreSQL 13+.
            migrationBuilder.Sql(@"
                INSERT INTO ""Communities"" (""Id"", ""LegalName"", ""CommunityName"", ""Status"", ""CreatedAt"")
                SELECT gen_random_uuid(), COALESCE(src.name, ''), src.name, 'Active', now()
                FROM (SELECT DISTINCT ""CommunityName"" AS name FROM ""Properties""
                      WHERE ""CommunityName"" IS NOT NULL AND ""CommunityName"" <> '') src
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""Communities"" c WHERE c.""CommunityName"" = src.name);");

            // ── 5. Backfill Properties (joins Community by name directly). ────────
            migrationBuilder.Sql(@"
                UPDATE ""Properties"" p
                SET ""CommunityId"" = c.""Id""
                FROM ""Communities"" c
                WHERE c.""CommunityName"" = p.""CommunityName"";");

            // ── 6. Backfill the other seven tables through the handle→name map that
            // Properties provides (their OldCommunityId is the string handle).
            foreach (var t in ScopedTables)
            {
                if (t == "Properties") continue;
                migrationBuilder.Sql($@"
                    UPDATE ""{t}"" x
                    SET ""CommunityId"" = c.""Id""
                    FROM (SELECT DISTINCT ""OldCommunityId"" AS handle, ""CommunityName"" AS name
                          FROM ""Properties"") m
                    JOIN ""Communities"" c ON c.""CommunityName"" = m.name
                    WHERE x.""OldCommunityId"" = m.handle;");
            }

            // ── 7. Drop the now-redundant string columns; enforce NOT NULL. ───────
            migrationBuilder.DropColumn(name: "CommunityName", table: "Properties");
            foreach (var t in ScopedTables)
            {
                migrationBuilder.DropColumn(name: "OldCommunityId", table: t);
                migrationBuilder.AlterColumn<Guid>(
                    name: "CommunityId", table: t, type: "uuid", nullable: false,
                    oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            }

            // ── 8. Recreate the per-table indexes on the new uuid column and add the
            // Community foreign keys. ─────────────────────────────────────────────
            foreach (var (table, name, columns, unique) in ScopedIndexes)
            {
                migrationBuilder.Sql(
                    $"CREATE {(unique ? "UNIQUE " : "")}INDEX \"{name}\" ON \"{table}\" ({columns});");
            }

            foreach (var t in ScopedTables)
            {
                migrationBuilder.AddForeignKey(
                    name: $"FK_{t}_Communities_CommunityId",
                    table: t, column: "CommunityId",
                    principalTable: "Communities", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: re-add the string columns and re-derive each from the referenced
            // Community's CommunityName (FR-005). The original short handle is not
            // recovered — the reversible contract is "derive from CommunityName".
            foreach (var t in ScopedTables)
                migrationBuilder.DropForeignKey(name: $"FK_{t}_Communities_CommunityId", table: t);

            foreach (var (table, name, _, _) in ScopedIndexes)
                migrationBuilder.Sql($"DROP INDEX IF EXISTS \"{name}\";");

            // Add nullable string column, backfill from Community, then finalize.
            foreach (var t in ScopedTables)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE \"{t}\" RENAME COLUMN \"CommunityId\" TO \"NewCommunityId\";");
                migrationBuilder.AddColumn<string>(
                    name: "CommunityId", table: t, type: "text", nullable: true);
                migrationBuilder.Sql($@"
                    UPDATE ""{t}"" x SET ""CommunityId"" = c.""CommunityName""
                    FROM ""Communities"" c WHERE c.""Id"" = x.""NewCommunityId"";");
            }

            migrationBuilder.AddColumn<string>(
                name: "CommunityName", table: "Properties", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.Sql(@"
                UPDATE ""Properties"" p SET ""CommunityName"" = c.""CommunityName""
                FROM ""Communities"" c WHERE c.""Id"" = p.""NewCommunityId"";");

            foreach (var t in ScopedTables)
            {
                migrationBuilder.DropColumn(name: "NewCommunityId", table: t);
                migrationBuilder.Sql(
                    $"ALTER TABLE \"{t}\" ALTER COLUMN \"CommunityId\" SET NOT NULL;");
            }

            // Recreate the original string-column indexes.
            foreach (var (table, name, columns, unique) in ScopedIndexes)
                migrationBuilder.Sql(
                    $"CREATE {(unique ? "UNIQUE " : "")}INDEX \"{name}\" ON \"{table}\" ({columns});");

            // Restore the old string-column length caps that EF had configured.
            migrationBuilder.AlterColumn<string>(
                name: "CommunityId", table: "Properties", type: "character varying(20)",
                maxLength: 20, nullable: false, oldClrType: typeof(string), oldType: "text");
            migrationBuilder.AlterColumn<string>(
                name: "CommunityId", table: "HoaPaymentConfigs", type: "character varying(20)",
                maxLength: 20, nullable: false, oldClrType: typeof(string), oldType: "text");

            migrationBuilder.DropTable(name: "CommunityMemberships");
            migrationBuilder.DropTable(name: "Communities");
            migrationBuilder.DropColumn(name: "LastActiveMode", table: "AspNetUsers");
        }
    }
}
