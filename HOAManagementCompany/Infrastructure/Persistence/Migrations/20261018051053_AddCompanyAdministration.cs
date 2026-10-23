using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "RecurringPayments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnrolledByUserId",
                table: "RecurringPayments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAt",
                table: "Communities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivatedByUserId",
                table: "Communities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "Communities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedByUserId",
                table: "Communities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Communities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagementCompanyId",
                table: "Communities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Communities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RestoredAt",
                table: "Communities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestoredByUserId",
                table: "Communities",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ManagementCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementCompanies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyDefaultSettings",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewPeriodDays = table.Column<int>(type: "integer", nullable: false),
                    LapseRule = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DecisionRule = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReminderDays = table.Column<int>(type: "integer", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FormalDisapprovalStatement = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDefaultSettings", x => x.CompanyId);
                    table.ForeignKey(
                        name: "FK_CompanyDefaultSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CompanyDefaultSettings_ManagementCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ManagementCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── 030 data steps (data-model.md "Migration outline") ─────────────────────────────
            // 1–2. The deployment's one management company and its default settings (027 values).
            migrationBuilder.Sql("""
                INSERT INTO "ManagementCompanies" ("Id", "Name", "CreatedAt")
                SELECT gen_random_uuid(), 'Management company', now()
                WHERE NOT EXISTS (SELECT 1 FROM "ManagementCompanies");

                INSERT INTO "CompanyDefaultSettings"
                    ("CompanyId", "ReviewPeriodDays", "LapseRule", "DecisionRule", "ReminderDays",
                     "TimeZoneId", "FormalDisapprovalStatement", "UpdatedAt", "UpdatedByUserId")
                SELECT mc."Id", 30, 'FlagOverdueOnly', 'MajorityOfMembers', 7, 'America/New_York',
                       'This is a formal disapproval of the plans as submitted under the community''s governing documents. You may revise and resubmit.', now(), NULL
                FROM "ManagementCompanies" mc
                WHERE NOT EXISTS (SELECT 1 FROM "CompanyDefaultSettings" d WHERE d."CompanyId" = mc."Id");
                """);

            // 3. Link every community to the company, fill the normalized name, record existing
            //    Active communities as activated, and move Inactive to Archived (FR-010).
            migrationBuilder.Sql("""
                UPDATE "Communities"
                SET "ManagementCompanyId" = (SELECT "Id" FROM "ManagementCompanies" ORDER BY "CreatedAt" LIMIT 1)
                WHERE "ManagementCompanyId" = '00000000-0000-0000-0000-000000000000';

                UPDATE "Communities" SET "NormalizedName" = lower(btrim("CommunityName"));

                UPDATE "Communities" SET "ActivatedAt" = "CreatedAt"
                WHERE "Status" = 'Active' AND "ActivatedAt" IS NULL;

                UPDATE "Communities" SET "Status" = 'Archived', "ArchivedAt" = now()
                WHERE "Status" = 'Inactive';
                """);

            // 4. R15: refuse to continue when names collide under the new rule, before swapping
            //    the global case-sensitive index for the per-company normalized one.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "Communities"
                             GROUP BY "ManagementCompanyId", "NormalizedName" HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'Community names collide when compared without case and surrounding spaces; rename them before applying 030.';
                  END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Communities_CommunityName",
                table: "Communities");

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TermEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SenderUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResendCount = table.Column<int>(type: "integer", nullable: false),
                    LastSendError = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserId = table.Column<string>(type: "text", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "text", nullable: true),
                    RevokeReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.CheckConstraint("CK_Invitations_KindShape", "((\"Kind\" = 'Membership' AND \"CommunityId\" IS NOT NULL AND \"Role\" IS NOT NULL AND \"PropertyId\" IS NULL) OR (\"Kind\" = 'Residency' AND \"CommunityId\" IS NOT NULL AND \"PropertyId\" IS NOT NULL AND \"Role\" IS NULL) OR (\"Kind\" = 'CompanyAdministrator' AND \"CommunityId\" IS NULL AND \"PropertyId\" IS NULL AND \"Role\" IS NULL)) AND (\"SenderUserId\" IS NOT NULL OR \"Kind\" = 'CompanyAdministrator')");
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_ManagementCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ManagementCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyAdministratorGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GrantedByUserId = table.Column<string>(type: "text", nullable: true),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyAdministratorGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyAdministratorGrants_AspNetUsers_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CompanyAdministratorGrants_AspNetUsers_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CompanyAdministratorGrants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyAdministratorGrants_Invitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "Invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CompanyAdministratorGrants_ManagementCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ManagementCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvitationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    BypassesRecipientCap = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvitationDeliveries_Invitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "Invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringPayments_EnrolledByUserId",
                table: "RecurringPayments",
                column: "EnrolledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Communities_ActivatedByUserId",
                table: "Communities",
                column: "ActivatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Communities_ArchivedByUserId",
                table: "Communities",
                column: "ArchivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Communities_CreatedByUserId",
                table: "Communities",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Communities_ManagementCompanyId_NormalizedName",
                table: "Communities",
                columns: new[] { "ManagementCompanyId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Communities_ManagementCompanyId_Status",
                table: "Communities",
                columns: new[] { "ManagementCompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Communities_RestoredByUserId",
                table: "Communities",
                column: "RestoredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAdministratorGrants_CompanyId_UserId",
                table: "CompanyAdministratorGrants",
                columns: new[] { "CompanyId", "UserId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAdministratorGrants_GrantedByUserId",
                table: "CompanyAdministratorGrants",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAdministratorGrants_InvitationId",
                table: "CompanyAdministratorGrants",
                column: "InvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAdministratorGrants_RevokedByUserId",
                table: "CompanyAdministratorGrants",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAdministratorGrants_UserId",
                table: "CompanyAdministratorGrants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDefaultSettings_UpdatedByUserId",
                table: "CompanyDefaultSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationDeliveries_InvitationId",
                table: "InvitationDeliveries",
                column: "InvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationDeliveries_NormalizedEmail_AttemptedAt",
                table: "InvitationDeliveries",
                columns: new[] { "NormalizedEmail", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_AcceptedByUserId",
                table: "Invitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_CommunityId_State",
                table: "Invitations",
                columns: new[] { "CommunityId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OpenCompanyAdministrator",
                table: "Invitations",
                columns: new[] { "CompanyId", "NormalizedEmail" },
                unique: true,
                filter: "\"Kind\" = 'CompanyAdministrator' AND \"State\" IN ('Scheduled','Pending')");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OpenMembership",
                table: "Invitations",
                columns: new[] { "CommunityId", "NormalizedEmail", "Role" },
                unique: true,
                filter: "\"Kind\" = 'Membership' AND \"State\" IN ('Scheduled','Pending')");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OpenResidency",
                table: "Invitations",
                columns: new[] { "PropertyId", "NormalizedEmail" },
                unique: true,
                filter: "\"Kind\" = 'Residency' AND \"State\" IN ('Scheduled','Pending')");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_PropertyId",
                table: "Invitations",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_RevokedByUserId",
                table: "Invitations",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_SenderUserId_State",
                table: "Invitations",
                columns: new[] { "SenderUserId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TokenHash",
                table: "Invitations",
                column: "TokenHash",
                unique: true,
                filter: "\"TokenHash\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Communities_AspNetUsers_ActivatedByUserId",
                table: "Communities",
                column: "ActivatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Communities_AspNetUsers_ArchivedByUserId",
                table: "Communities",
                column: "ArchivedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Communities_AspNetUsers_CreatedByUserId",
                table: "Communities",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Communities_AspNetUsers_RestoredByUserId",
                table: "Communities",
                column: "RestoredByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Communities_ManagementCompanies_ManagementCompanyId",
                table: "Communities",
                column: "ManagementCompanyId",
                principalTable: "ManagementCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringPayments_AspNetUsers_EnrolledByUserId",
                table: "RecurringPayments",
                column: "EnrolledByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // 5. R13: every community gets its own ARC settings row (027 constants), so a later
            //    company default change can never reach it (FR-026).
            migrationBuilder.Sql("""
                INSERT INTO "CommunityArcSettings"
                    ("CommunityId", "ReviewPeriodDays", "LapseRule", "DecisionRule", "ReminderDays",
                     "TimeZoneId", "FormalDisapprovalStatement", "NextApplicationNumber", "UpdatedAt", "UpdatedByUserId")
                SELECT c."Id", 30, 'FlagOverdueOnly', 'MajorityOfMembers', 7, 'America/New_York',
                       'This is a formal disapproval of the plans as submitted under the community''s governing documents. You may revise and resubmit.', 1001, now(), NULL
                FROM "Communities" c
                WHERE NOT EXISTS (SELECT 1 FROM "CommunityArcSettings" s WHERE s."CommunityId" = c."Id");
                """);

            // 7. FR-013 for communities migrated to Archived: cancel active autopay and terminate the
            //    current authorization. No email is sent (spec Database/runtime).
            migrationBuilder.Sql("""
                UPDATE "PaymentAuthorizations" pa SET "TerminatedAt" = now()
                FROM "RecurringPayments" rp
                JOIN "Properties" p ON p."Id" = rp."PropertyId"
                JOIN "Communities" c ON c."Id" = p."CommunityId"
                WHERE pa."Id" = rp."CurrentAuthorizationId"
                  AND c."Status" = 'Archived' AND rp."Status" = 'active' AND pa."TerminatedAt" IS NULL;

                UPDATE "RecurringPayments" rp
                SET "Status" = 'inactive', "CancellationReason" = 'community_archived', "UpdatedAt" = now()
                FROM "Properties" p
                JOIN "Communities" c ON c."Id" = p."CommunityId"
                WHERE rp."PropertyId" = p."Id" AND c."Status" = 'Archived' AND rp."Status" = 'active';
                """);

            // 8. R16: natural ordering for addresses (#9 before #10) in the Residents area.
            migrationBuilder.Sql("""
                CREATE COLLATION IF NOT EXISTS natural_sort (provider = icu, locale = 'und-u-kn-true', deterministic = true);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP COLLATION IF EXISTS natural_sort;");

            migrationBuilder.DropForeignKey(
                name: "FK_Communities_AspNetUsers_ActivatedByUserId",
                table: "Communities");

            migrationBuilder.DropForeignKey(
                name: "FK_Communities_AspNetUsers_ArchivedByUserId",
                table: "Communities");

            migrationBuilder.DropForeignKey(
                name: "FK_Communities_AspNetUsers_CreatedByUserId",
                table: "Communities");

            migrationBuilder.DropForeignKey(
                name: "FK_Communities_AspNetUsers_RestoredByUserId",
                table: "Communities");

            migrationBuilder.DropForeignKey(
                name: "FK_Communities_ManagementCompanies_ManagementCompanyId",
                table: "Communities");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringPayments_AspNetUsers_EnrolledByUserId",
                table: "RecurringPayments");

            migrationBuilder.DropTable(
                name: "CompanyAdministratorGrants");

            migrationBuilder.DropTable(
                name: "CompanyDefaultSettings");

            migrationBuilder.DropTable(
                name: "InvitationDeliveries");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "ManagementCompanies");

            migrationBuilder.DropIndex(
                name: "IX_RecurringPayments_EnrolledByUserId",
                table: "RecurringPayments");

            migrationBuilder.DropIndex(
                name: "IX_Communities_ActivatedByUserId",
                table: "Communities");

            migrationBuilder.DropIndex(
                name: "IX_Communities_ArchivedByUserId",
                table: "Communities");

            migrationBuilder.DropIndex(
                name: "IX_Communities_CreatedByUserId",
                table: "Communities");

            migrationBuilder.DropIndex(
                name: "IX_Communities_ManagementCompanyId_NormalizedName",
                table: "Communities");

            migrationBuilder.DropIndex(
                name: "IX_Communities_ManagementCompanyId_Status",
                table: "Communities");

            migrationBuilder.DropIndex(
                name: "IX_Communities_RestoredByUserId",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "RecurringPayments");

            migrationBuilder.DropColumn(
                name: "EnrolledByUserId",
                table: "RecurringPayments");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "ActivatedByUserId",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "ManagementCompanyId",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "RestoredAt",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "RestoredByUserId",
                table: "Communities");

            migrationBuilder.CreateIndex(
                name: "IX_Communities_CommunityName",
                table: "Communities",
                column: "CommunityName",
                unique: true);
        }
    }
}
