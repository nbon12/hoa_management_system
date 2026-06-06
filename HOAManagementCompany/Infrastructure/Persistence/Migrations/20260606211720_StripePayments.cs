using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HOAManagementCompany.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] // EF-generated migration
    public partial class StripePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentAuthorizationId",
                table: "RecurringPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VaultedPaymentMethodId",
                table: "RecurringPayments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AlertEmailOptIn",
                table: "Owners",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AlertPhone",
                table: "Owners",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AlertSmsOptIn",
                table: "Owners",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Owners",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundCode",
                table: "LedgerEntries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "LedgerEntries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "TransactionId",
                table: "LedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransactionId",
                table: "DraftEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlertConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ConsentText = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertConsents_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HoaPaymentConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CardFeeType = table.Column<string>(type: "text", nullable: false),
                    CardFeeValue = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    CardScope = table.Column<string>(type: "text", nullable: false),
                    SurchargingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AchFeeValue = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    AllocationOrderJson = table.Column<string>(type: "jsonb", nullable: false),
                    NsfFeeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NsfFeeAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    VariableNoticeLeadDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoaPaymentConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxMessages_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurringPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    MandateText = table.Column<string>(type: "text", nullable: false),
                    MandateVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AmountTermsSnapshot = table.Column<string>(type: "text", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AcceptedUserAgent = table.Column<string>(type: "text", nullable: true),
                    StripeMandateId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TerminatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAuthorizations_RecurringPayments_RecurringPaymentId",
                        column: x => x.RecurringPaymentId,
                        principalTable: "RecurringPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    StripePaymentIntentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StripeChargeId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    CumulativeRefundedAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    CardFunding = table.Column<string>(type: "text", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureMessage = table.Column<string>(type: "text", nullable: true),
                    ReturnCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StripeBalanceTransactionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ProcessorFeeAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    StripePayoutId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebhookEventInbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StripeEventId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookEventInbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaskedMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RenderModel = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipts_PaymentTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_PropertyId_Sequence",
                table: "LedgerEntries",
                columns: new[] { "PropertyId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_TransactionId",
                table: "LedgerEntries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftEntries_TransactionId",
                table: "DraftEntries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertConsents_OwnerId_Channel",
                table: "AlertConsents",
                columns: new[] { "OwnerId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_HoaPaymentConfigs_CommunityId",
                table: "HoaPaymentConfigs",
                column: "CommunityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_OwnerId",
                table: "OutboxMessages",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status",
                table: "OutboxMessages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAuthorizations_RecurringPaymentId",
                table: "PaymentAuthorizations",
                column: "RecurringPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_CreatedAt",
                table: "PaymentTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_IdempotencyKey",
                table: "PaymentTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_OwnerId",
                table: "PaymentTransactions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PropertyId",
                table: "PaymentTransactions",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Status",
                table: "PaymentTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_StripeChargeId",
                table: "PaymentTransactions",
                column: "StripeChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_StripePaymentIntentId",
                table: "PaymentTransactions",
                column: "StripePaymentIntentId",
                unique: true,
                filter: "\"StripePaymentIntentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_OwnerId",
                table: "Receipts",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_TransactionId",
                table: "Receipts",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEventInbox_Status",
                table: "WebhookEventInbox",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEventInbox_StripeEventId",
                table: "WebhookEventInbox",
                column: "StripeEventId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DraftEntries_PaymentTransactions_TransactionId",
                table: "DraftEntries",
                column: "TransactionId",
                principalTable: "PaymentTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_PaymentTransactions_TransactionId",
                table: "LedgerEntries",
                column: "TransactionId",
                principalTable: "PaymentTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DraftEntries_PaymentTransactions_TransactionId",
                table: "DraftEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_LedgerEntries_PaymentTransactions_TransactionId",
                table: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "AlertConsents");

            migrationBuilder.DropTable(
                name: "HoaPaymentConfigs");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "PaymentAuthorizations");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "WebhookEventInbox");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_PropertyId_Sequence",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_TransactionId",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_DraftEntries_TransactionId",
                table: "DraftEntries");

            migrationBuilder.DropColumn(
                name: "CurrentAuthorizationId",
                table: "RecurringPayments");

            migrationBuilder.DropColumn(
                name: "VaultedPaymentMethodId",
                table: "RecurringPayments");

            migrationBuilder.DropColumn(
                name: "AlertEmailOptIn",
                table: "Owners");

            migrationBuilder.DropColumn(
                name: "AlertPhone",
                table: "Owners");

            migrationBuilder.DropColumn(
                name: "AlertSmsOptIn",
                table: "Owners");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Owners");

            migrationBuilder.DropColumn(
                name: "FundCode",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "DraftEntries");
        }
    }
}
