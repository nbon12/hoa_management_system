using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResidentArcSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RespondedByUserId",
                table: "ArchitecturalInfoRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseMessage",
                table: "ArchitecturalInfoRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InfoRequestId",
                table: "ArchitecturalAttachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadedByUserId",
                table: "ArchitecturalAttachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcknowledgedAt",
                table: "ArchitecturalApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractorContact",
                table: "ArchitecturalApplications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractorName",
                table: "ArchitecturalApplications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedCompletionDate",
                table: "ArchitecturalApplications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedStartDate",
                table: "ArchitecturalApplications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WithdrawnAt",
                table: "ArchitecturalApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawnByUserId",
                table: "ArchitecturalApplications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArchitecturalApplicationDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    PreviousRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProjectTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PlannedStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedCompletionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContractorContact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    RemovedCarriedAttachmentIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchitecturalApplicationDrafts", x => x.Id);
                    table.CheckConstraint("CK_ArchitecturalApplicationDrafts_PlannedDates", "\"PlannedCompletionDate\" IS NULL OR \"PlannedStartDate\" IS NULL OR \"PlannedCompletionDate\" >= \"PlannedStartDate\"");
                    table.ForeignKey(
                        name: "FK_ArchitecturalApplicationDrafts_ArchitecturalApplications_Pr~",
                        column: x => x.PreviousRevisionId,
                        principalTable: "ArchitecturalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArchitecturalApplicationDrafts_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ArchitecturalApplicationDrafts_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArchitecturalApplicationDrafts_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchitecturalDraftAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchitecturalDraftAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchitecturalDraftAttachments_ArchitecturalApplicationDraft~",
                        column: x => x.DraftId,
                        principalTable: "ArchitecturalApplicationDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchitecturalDraftAttachments_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalInfoRequests_RespondedByUserId",
                table: "ArchitecturalInfoRequests",
                column: "RespondedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalAttachments_InfoRequestId",
                table: "ArchitecturalAttachments",
                column: "InfoRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalAttachments_UploadedByUserId",
                table: "ArchitecturalAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplications_WithdrawnByUserId",
                table: "ArchitecturalApplications",
                column: "WithdrawnByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplicationDrafts_CommunityId",
                table: "ArchitecturalApplicationDrafts",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplicationDrafts_CreatedByUserId",
                table: "ArchitecturalApplicationDrafts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplicationDrafts_PreviousRevisionId",
                table: "ArchitecturalApplicationDrafts",
                column: "PreviousRevisionId",
                unique: true,
                filter: "\"PreviousRevisionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalApplicationDrafts_PropertyId",
                table: "ArchitecturalApplicationDrafts",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalDraftAttachments_DraftId",
                table: "ArchitecturalDraftAttachments",
                column: "DraftId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalDraftAttachments_UploadedByUserId",
                table: "ArchitecturalDraftAttachments",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArchitecturalApplications_AspNetUsers_WithdrawnByUserId",
                table: "ArchitecturalApplications",
                column: "WithdrawnByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ArchitecturalAttachments_ArchitecturalInfoRequests_InfoRequ~",
                table: "ArchitecturalAttachments",
                column: "InfoRequestId",
                principalTable: "ArchitecturalInfoRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ArchitecturalAttachments_AspNetUsers_UploadedByUserId",
                table: "ArchitecturalAttachments",
                column: "UploadedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ArchitecturalInfoRequests_AspNetUsers_RespondedByUserId",
                table: "ArchitecturalInfoRequests",
                column: "RespondedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchitecturalApplications_AspNetUsers_WithdrawnByUserId",
                table: "ArchitecturalApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_ArchitecturalAttachments_ArchitecturalInfoRequests_InfoRequ~",
                table: "ArchitecturalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_ArchitecturalAttachments_AspNetUsers_UploadedByUserId",
                table: "ArchitecturalAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_ArchitecturalInfoRequests_AspNetUsers_RespondedByUserId",
                table: "ArchitecturalInfoRequests");

            migrationBuilder.DropTable(
                name: "ArchitecturalDraftAttachments");

            migrationBuilder.DropTable(
                name: "ArchitecturalApplicationDrafts");

            migrationBuilder.DropIndex(
                name: "IX_ArchitecturalInfoRequests_RespondedByUserId",
                table: "ArchitecturalInfoRequests");

            migrationBuilder.DropIndex(
                name: "IX_ArchitecturalAttachments_InfoRequestId",
                table: "ArchitecturalAttachments");

            migrationBuilder.DropIndex(
                name: "IX_ArchitecturalAttachments_UploadedByUserId",
                table: "ArchitecturalAttachments");

            migrationBuilder.DropIndex(
                name: "IX_ArchitecturalApplications_WithdrawnByUserId",
                table: "ArchitecturalApplications");

            migrationBuilder.DropColumn(
                name: "RespondedByUserId",
                table: "ArchitecturalInfoRequests");

            migrationBuilder.DropColumn(
                name: "ResponseMessage",
                table: "ArchitecturalInfoRequests");

            migrationBuilder.DropColumn(
                name: "InfoRequestId",
                table: "ArchitecturalAttachments");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "ArchitecturalAttachments");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                table: "ArchitecturalApplications");

            migrationBuilder.DropColumn(
                name: "ContractorContact",
                table: "ArchitecturalApplications");

            migrationBuilder.DropColumn(
                name: "ContractorName",
                table: "ArchitecturalApplications");

            migrationBuilder.DropColumn(
                name: "PlannedCompletionDate",
                table: "ArchitecturalApplications");

            migrationBuilder.DropColumn(
                name: "PlannedStartDate",
                table: "ArchitecturalApplications");

            migrationBuilder.DropColumn(
                name: "WithdrawnAt",
                table: "ArchitecturalApplications");

            migrationBuilder.DropColumn(
                name: "WithdrawnByUserId",
                table: "ArchitecturalApplications");
        }
    }
}
