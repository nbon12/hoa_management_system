using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecurringVaultedMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SC-008: drop every deprecated masked card/bank column. These are dropped (not renamed)
            // so no masked PAN/routing string leaks into the new vaulted-method fields.
            migrationBuilder.DropColumn(name: "AccountNumberMasked", table: "RecurringPayments");
            migrationBuilder.DropColumn(name: "AccountType", table: "RecurringPayments");
            migrationBuilder.DropColumn(name: "BillingZip", table: "RecurringPayments");
            migrationBuilder.DropColumn(name: "CardExpiry", table: "RecurringPayments");
            migrationBuilder.DropColumn(name: "CardNumberMasked", table: "RecurringPayments");
            migrationBuilder.DropColumn(name: "CardholderName", table: "RecurringPayments");
            migrationBuilder.DropColumn(name: "RoutingNumberMasked", table: "RecurringPayments");

            migrationBuilder.AlterColumn<string>(
                name: "VaultedPaymentMethodId",
                table: "RecurringPayments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MethodFunding",
                table: "RecurringPayments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MethodBrand",
                table: "RecurringPayments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MethodLast4",
                table: "RecurringPayments",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MethodBrand", table: "RecurringPayments");
            migrationBuilder.DropColumn(name: "MethodFunding", table: "RecurringPayments");
            migrationBuilder.DropColumn(name: "MethodLast4", table: "RecurringPayments");

            migrationBuilder.AlterColumn<string>(
                name: "VaultedPaymentMethodId",
                table: "RecurringPayments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(name: "AccountNumberMasked", table: "RecurringPayments", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "AccountType", table: "RecurringPayments", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "BillingZip", table: "RecurringPayments", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "CardExpiry", table: "RecurringPayments", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "CardNumberMasked", table: "RecurringPayments", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "CardholderName", table: "RecurringPayments", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "RoutingNumberMasked", table: "RecurringPayments", type: "text", nullable: true);
        }
    }
}
