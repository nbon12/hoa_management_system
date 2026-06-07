using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OutboxDedupKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DedupKey",
                table: "OutboxMessages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_DedupKey",
                table: "OutboxMessages",
                column: "DedupKey",
                unique: true,
                filter: "\"DedupKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_DedupKey",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "DedupKey",
                table: "OutboxMessages");
        }
    }
}
