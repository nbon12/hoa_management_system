using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Migrations
{
    /// <inheritdoc />
    public partial class AddViolationTypeToViolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ViolationTypeId",
                table: "Violations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Violations_ViolationTypeId",
                table: "Violations",
                column: "ViolationTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Violations_ViolationTypes_ViolationTypeId",
                table: "Violations",
                column: "ViolationTypeId",
                principalTable: "ViolationTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Violations_ViolationTypes_ViolationTypeId",
                table: "Violations");

            migrationBuilder.DropIndex(
                name: "IX_Violations_ViolationTypeId",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "ViolationTypeId",
                table: "Violations");
        }
    }
}
