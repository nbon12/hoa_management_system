using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Migrations
{
    /// <inheritdoc />
    public partial class MakeAuditColumnsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, make the columns nullable
            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "ViolationTypes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ViolationTypes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "Violations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Violations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            // Now clean up existing data by setting empty strings to null
            migrationBuilder.Sql("UPDATE \"Violations\" SET \"CreatedBy\" = NULL WHERE \"CreatedBy\" = ''");
            migrationBuilder.Sql("UPDATE \"Violations\" SET \"UpdatedBy\" = NULL WHERE \"UpdatedBy\" = ''");
            migrationBuilder.Sql("UPDATE \"ViolationTypes\" SET \"CreatedBy\" = NULL WHERE \"CreatedBy\" = ''");
            migrationBuilder.Sql("UPDATE \"ViolationTypes\" SET \"UpdatedBy\" = NULL WHERE \"UpdatedBy\" = ''");

            // Update seeded data
            migrationBuilder.UpdateData(
                table: "ViolationTypes",
                keyColumn: "Id",
                keyValue: new Guid("3f843e9d-3e26-4696-84d6-f20f2dc20b1f"),
                columns: new[] { "CreatedBy", "UpdatedBy" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "ViolationTypes",
                keyColumn: "Id",
                keyValue: new Guid("b5a56c9b-a14f-4f9b-afc1-82d00663aa01"),
                columns: new[] { "CreatedBy", "UpdatedBy" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore empty strings for CreatedBy and UpdatedBy
            migrationBuilder.Sql("UPDATE \"Violations\" SET \"CreatedBy\" = '' WHERE \"CreatedBy\" IS NULL");
            migrationBuilder.Sql("UPDATE \"Violations\" SET \"UpdatedBy\" = '' WHERE \"UpdatedBy\" IS NULL");
            migrationBuilder.Sql("UPDATE \"ViolationTypes\" SET \"CreatedBy\" = '' WHERE \"CreatedBy\" IS NULL");
            migrationBuilder.Sql("UPDATE \"ViolationTypes\" SET \"UpdatedBy\" = '' WHERE \"UpdatedBy\" IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "ViolationTypes",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ViolationTypes",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "Violations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Violations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "ViolationTypes",
                keyColumn: "Id",
                keyValue: new Guid("3f843e9d-3e26-4696-84d6-f20f2dc20b1f"),
                columns: new[] { "CreatedBy", "UpdatedBy" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "ViolationTypes",
                keyColumn: "Id",
                keyValue: new Guid("b5a56c9b-a14f-4f9b-afc1-82d00663aa01"),
                columns: new[] { "CreatedBy", "UpdatedBy" },
                values: new object[] { "", "" });
        }
    }
}
