using System;
#pragma warning disable CA1861
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPreOrderCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckoutAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapacityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductSlug = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UniverseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrimaryImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FullPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PreOrderCloseAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EstimatedArrivalMonth = table.Column<int>(type: "integer", nullable: false),
                    EstimatedArrivalYear = table.Column<int>(type: "integer", nullable: false),
                    BalancePaymentDays = table.Column<int>(type: "integer", nullable: false),
                    DepositPolicy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AddressLine = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SubDistrict = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    District = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Province = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderSessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutAttempts", x => x.Id);
                    table.CheckConstraint("CK_CheckoutAttempts_Amounts", "\"FullPrice\" > 0 AND \"DepositAmount\" > 0 AND \"BalanceAmount\" > 0 AND \"PaymentAmount\" > 0");
                    table.CheckConstraint("CK_CheckoutAttempts_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_CheckoutAttempts_Quantity_Positive", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_CheckoutAttempts_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_CheckoutAttempts_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CheckoutAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FulfillmentStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AddressLine = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SubDistrict = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    District = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Province = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Item_ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemEnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemProductSlug = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ItemCategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemBrandName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemUniverseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemPrimaryImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Item_Quantity = table.Column<int>(type: "integer", nullable: false),
                    ItemFullPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ItemDepositAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ItemBalanceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ItemLinePaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ItemPreOrderCloseAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Item_EstimatedArrivalMonth = table.Column<int>(type: "integer", nullable: false),
                    Item_EstimatedArrivalYear = table.Column<int>(type: "integer", nullable: false),
                    Item_BalancePaymentDays = table.Column<int>(type: "integer", nullable: false),
                    ItemDepositPolicy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckoutAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ProviderSessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProviderPaymentReference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAttempts_ReservationId",
                table: "CheckoutAttempts",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CheckoutAttempts_CustomerId_IdempotencyKey",
                table: "CheckoutAttempts",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CheckoutAttempts_ProviderSessionId",
                table: "CheckoutAttempts",
                column: "ProviderSessionId",
                unique: true,
                filter: "\"ProviderSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CheckoutAttemptId",
                table: "Orders",
                column: "CheckoutAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Number",
                table: "Orders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CheckoutAttemptId",
                table: "Payments",
                column: "CheckoutAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderEventId",
                table: "Payments",
                column: "ProviderEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderPaymentReference",
                table: "Payments",
                column: "ProviderPaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderSessionId",
                table: "Payments",
                column: "ProviderSessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutAttempts");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
#pragma warning restore CA1861
