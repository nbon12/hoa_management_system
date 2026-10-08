using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArchitecturalReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "RecipientUserId",
                table: "OutboxMessages",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArchitecturalApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationNumber = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PreviousRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedByUserId = table.Column<string>(type: "text", nullable: true),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjectType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProjectTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DecisionRule = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LapseRule = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecisionOutcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DecisionWording = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DecisionSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DecisionReachedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConditionsOfApproval = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OwnerReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<string>(type: "text", nullable: true),
                    ReminderSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LapseProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchitecturalApplications", x => x.Id);
                    table.CheckConstraint("CK_ArchitecturalApplications_ConditionsOnlyWhenApproved", "\"ConditionsOfApproval\" IS NULL OR \"DecisionOutcome\" = 'Approved'");
                    table.CheckConstraint("CK_ArchitecturalApplications_Revision", "\"Revision\" >= 1");
                    table.CheckConstraint("CK_ArchitecturalApplications_RevisionLink", "(\"Revision\" = 1) = (\"PreviousRevisionId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ArchitecturalApplications_ArchitecturalApplications_Previou~",
                        column: x => x.PreviousRevisionId,
                        principalTable: "ArchitecturalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArchitecturalApplications_AspNetUsers_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ArchitecturalApplications_AspNetUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ArchitecturalApplications_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArchitecturalApplications_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityArcSettings",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewPeriodDays = table.Column<int>(type: "integer", nullable: false),
                    LapseRule = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DecisionRule = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReminderDays = table.Column<int>(type: "integer", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FormalDisapprovalStatement = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    NextApplicationNumber = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityArcSettings", x => x.CommunityId);
                    table.ForeignKey(
                        name: "FK_CommunityArcSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CommunityArcSettings_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchitecturalAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchitecturalAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchitecturalAttachments_ArchitecturalApplications_Applicat~",
                        column: x => x.ApplicationId,
                        principalTable: "ArchitecturalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchitecturalInfoRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchitecturalInfoRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchitecturalInfoRequests_ArchitecturalApplications_Applica~",
                        column: x => x.ApplicationId,
                        principalTable: "ArchitecturalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchitecturalInfoRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchitecturalVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterUserId = table.Column<string>(type: "text", nullable: false),
                    Choice = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CastAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchitecturalVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchitecturalVotes_ArchitecturalApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "ArchitecturalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchitecturalVotes_AspNetUsers_VoterUserId",
                        column: x => x.VoterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_RecipientUserId",
                table: "OutboxMessages",
                column: "RecipientUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OutboxMessages_SingleRecipient",
                table: "OutboxMessages",
                sql: "(\"OwnerId\" IS NULL) <> (\"RecipientUserId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplications_ClosedByUserId",
                table: "ArchitecturalApplications",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplications_CommunityId_ApplicationNumber_Rev~",
                table: "ArchitecturalApplications",
                columns: new[] { "CommunityId", "ApplicationNumber", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplications_CommunityId_Status_DueDate",
                table: "ArchitecturalApplications",
                columns: new[] { "CommunityId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplications_PreviousRevisionId",
                table: "ArchitecturalApplications",
                column: "PreviousRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplications_PropertyId",
                table: "ArchitecturalApplications",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplications_SubmittedByUserId",
                table: "ArchitecturalApplications",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalAttachments_ApplicationId",
                table: "ArchitecturalAttachments",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalInfoRequests_ApplicationId",
                table: "ArchitecturalInfoRequests",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalInfoRequests_RequestedByUserId",
                table: "ArchitecturalInfoRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalVotes_ApplicationId_VoterUserId",
                table: "ArchitecturalVotes",
                columns: new[] { "ApplicationId", "VoterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalVotes_VoterUserId",
                table: "ArchitecturalVotes",
                column: "VoterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityArcSettings_UpdatedByUserId",
                table: "CommunityArcSettings",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OutboxMessages_AspNetUsers_RecipientUserId",
                table: "OutboxMessages",
                column: "RecipientUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OutboxMessages_AspNetUsers_RecipientUserId",
                table: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "ArchitecturalAttachments");

            migrationBuilder.DropTable(
                name: "ArchitecturalInfoRequests");

            migrationBuilder.DropTable(
                name: "ArchitecturalVotes");

            migrationBuilder.DropTable(
                name: "CommunityArcSettings");

            migrationBuilder.DropTable(
                name: "ArchitecturalApplications");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_RecipientUserId",
                table: "OutboxMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OutboxMessages_SingleRecipient",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "OutboxMessages");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "OutboxMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
