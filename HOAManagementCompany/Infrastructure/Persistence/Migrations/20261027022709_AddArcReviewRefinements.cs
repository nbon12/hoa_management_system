using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArcReviewRefinements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LapseRuleCitation",
                table: "CommunityArcSettings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "the community's governing documents");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerCommentId",
                table: "ArchitecturalAttachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecisionFinalAt",
                table: "ArchitecturalApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EligibleVoterCount",
                table: "ArchitecturalApplications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArchitecturalOwnerComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchitecturalOwnerComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchitecturalOwnerComments_ArchitecturalApplications_Applic~",
                        column: x => x.ApplicationId,
                        principalTable: "ArchitecturalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchitecturalOwnerComments_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalAttachments_OwnerCommentId",
                table: "ArchitecturalAttachments",
                column: "OwnerCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalOwnerComments_ApplicationId",
                table: "ArchitecturalOwnerComments",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalOwnerComments_AuthorUserId",
                table: "ArchitecturalOwnerComments",
                column: "AuthorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArchitecturalAttachments_ArchitecturalOwnerComments_OwnerCo~",
                table: "ArchitecturalAttachments",
                column: "OwnerCommentId",
                principalTable: "ArchitecturalOwnerComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchitecturalAttachments_ArchitecturalOwnerComments_OwnerCo~",
                table: "ArchitecturalAttachments");

            migrationBuilder.DropTable(
                name: "ArchitecturalOwnerComments");

            migrationBuilder.DropIndex(
                name: "IX_ArchitecturalAttachments_OwnerCommentId",
                table: "ArchitecturalAttachments");

            migrationBuilder.DropColumn(
                name: "LapseRuleCitation",
                table: "CommunityArcSettings");

            migrationBuilder.DropColumn(
                name: "OwnerCommentId",
                table: "ArchitecturalAttachments");

            migrationBuilder.DropColumn(
                name: "DecisionFinalAt",
                table: "ArchitecturalApplications");

            migrationBuilder.DropColumn(
                name: "EligibleVoterCount",
                table: "ArchitecturalApplications");
        }
    }
}
