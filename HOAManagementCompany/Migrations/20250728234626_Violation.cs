using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Migrations
{
    /// <inheritdoc />
    public partial class Violation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "Violations",
                newName: "OccurrenceDate");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ViolationTypes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CovenantText",
                table: "ViolationTypes",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Violations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Violations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Violations");

            migrationBuilder.RenameColumn(
                name: "OccurrenceDate",
                table: "Violations",
                newName: "Date");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ViolationTypes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CovenantText",
                table: "ViolationTypes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10000)",
                oldMaxLength: 10000);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Violations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

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
                onDelete: ReferentialAction.Cascade);
        }
    }
}
