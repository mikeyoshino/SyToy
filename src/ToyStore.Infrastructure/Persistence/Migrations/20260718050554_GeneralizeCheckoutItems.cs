using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeCheckoutItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE TEMP TABLE \"__LegacyCheckoutAttempts\" ON COMMIT DROP AS SELECT * FROM \"CheckoutAttempts\";");
            migrationBuilder.Sql(
                "CREATE TEMP TABLE \"__LegacyOrders\" ON COMMIT DROP AS SELECT * FROM \"Orders\";");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckoutAttempts_Products_ProductId",
                table: "CheckoutAttempts");

            migrationBuilder.DropIndex(
                name: "IX_CheckoutAttempts_ProductId",
                table: "CheckoutAttempts");

            migrationBuilder.DropIndex(
                name: "IX_CheckoutAttempts_ReservationId",
                table: "CheckoutAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CheckoutAttempts_Amounts",
                table: "CheckoutAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CheckoutAttempts_Quantity_Positive",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "ItemBalanceAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemBrandName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemCategoryName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemDepositAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemDepositPolicy",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemDisplayName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemEnglishName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemFullPrice",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemLinePaidAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemPreOrderCloseAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemPrimaryImageUrl",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemProductSlug",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ItemUniverseName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Item_BalancePaymentDays",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Item_EstimatedArrivalMonth",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Item_EstimatedArrivalYear",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Item_ProductId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Item_Quantity",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BalanceAmount",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "BalancePaymentDays",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "BrandName",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "CapacityId",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "CategoryName",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "DepositPolicy",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "EnglishName",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "EstimatedArrivalMonth",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "EstimatedArrivalYear",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "PreOrderCloseAtUtc",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "PrimaryImageUrl",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "ProductSlug",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "UniverseName",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "FullPrice",
                table: "CheckoutAttempts");

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmount",
                table: "CheckoutAttempts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SaleType",
                table: "Orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SaleType",
                table: "CheckoutAttempts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CheckoutAttemptItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductSlug = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UniverseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrimaryImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LinePaymentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreOrderCloseAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EstimatedArrivalMonth = table.Column<int>(type: "integer", nullable: true),
                    EstimatedArrivalYear = table.Column<int>(type: "integer", nullable: true),
                    BalancePaymentDays = table.Column<int>(type: "integer", nullable: true),
                    DepositPolicy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CheckoutAttemptId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutAttemptItems", x => x.Id);
                    table.CheckConstraint("CK_CheckoutAttemptItems_Amounts", "\"UnitPrice\" > 0 AND \"DepositAmount\" >= 0 AND \"BalanceAmount\" >= 0 AND \"LinePaymentAmount\" > 0");
                    table.CheckConstraint("CK_CheckoutAttemptItems_Quantity_Positive", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_CheckoutAttemptItems_SaleShape", "(\"SaleType\" = 'InStock' AND \"DepositAmount\" = 0 AND \"BalanceAmount\" = 0 AND \"PreOrderCloseAtUtc\" IS NULL AND \"DepositPolicy\" IS NULL) OR (\"SaleType\" = 'PreOrder' AND \"DepositAmount\" > 0 AND \"BalanceAmount\" > 0 AND \"PreOrderCloseAtUtc\" IS NOT NULL AND \"DepositPolicy\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CheckoutAttemptItems_CheckoutAttempts_CheckoutAttemptId",
                        column: x => x.CheckoutAttemptId,
                        principalTable: "CheckoutAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckoutAttemptItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductSlug = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UniverseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrimaryImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    FullPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LinePaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreOrderCloseAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EstimatedArrivalMonth = table.Column<int>(type: "integer", nullable: true),
                    EstimatedArrivalYear = table.Column<int>(type: "integer", nullable: true),
                    BalancePaymentDays = table.Column<int>(type: "integer", nullable: true),
                    DepositPolicy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_Amounts", "\"FullPrice\" > 0 AND \"DepositAmount\" >= 0 AND \"BalanceAmount\" >= 0 AND \"LinePaidAmount\" > 0");
                    table.CheckConstraint("CK_OrderItems_Quantity_Positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "CheckoutAttemptItems" (
                    "Id", "CheckoutAttemptId", "ProductId", "ResourceId", "ReservationId",
                    "SaleType", "Quantity", "DisplayName", "EnglishName", "ProductSlug",
                    "CategoryName", "BrandName", "UniverseName", "PrimaryImageUrl",
                    "UnitPrice", "DepositAmount", "BalanceAmount", "LinePaymentAmount",
                    "PreOrderCloseAtUtc", "EstimatedArrivalMonth", "EstimatedArrivalYear",
                    "BalancePaymentDays", "DepositPolicy")
                SELECT
                    "ReservationId", "Id", "ProductId", "CapacityId", "ReservationId",
                    'PreOrder', "Quantity", "DisplayName", "EnglishName", "ProductSlug",
                    "CategoryName", "BrandName", "UniverseName", "PrimaryImageUrl",
                    "FullPrice", "DepositAmount", "BalanceAmount", "PaymentAmount",
                    "PreOrderCloseAtUtc", "EstimatedArrivalMonth", "EstimatedArrivalYear",
                    "BalancePaymentDays", "DepositPolicy"
                FROM "__LegacyCheckoutAttempts";

                INSERT INTO "OrderItems" (
                    "Id", "OrderId", "ProductId", "SaleType", "DisplayName", "EnglishName",
                    "ProductSlug", "CategoryName", "BrandName", "UniverseName", "PrimaryImageUrl",
                    "Quantity", "FullPrice", "DepositAmount", "BalanceAmount", "LinePaidAmount",
                    "PreOrderCloseAtUtc", "EstimatedArrivalMonth", "EstimatedArrivalYear",
                    "BalancePaymentDays", "DepositPolicy")
                SELECT
                    "Id", "Id", "Item_ProductId", 'PreOrder', "ItemDisplayName", "ItemEnglishName",
                    "ItemProductSlug", "ItemCategoryName", "ItemBrandName", "ItemUniverseName",
                    "ItemPrimaryImageUrl", "Item_Quantity", "ItemFullPrice", "ItemDepositAmount",
                    "ItemBalanceAmount", "ItemLinePaidAmount", "ItemPreOrderCloseAtUtc",
                    "Item_EstimatedArrivalMonth", "Item_EstimatedArrivalYear",
                    "Item_BalancePaymentDays", "ItemDepositPolicy"
                FROM "__LegacyOrders";

                UPDATE "CheckoutAttempts" SET "SaleType" = 'PreOrder', "ShippingAmount" = 0;
                UPDATE "Orders" SET "SaleType" = 'PreOrder';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CheckoutAttempts_PaymentAmount_Positive",
                table: "CheckoutAttempts",
                sql: "\"PaymentAmount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CheckoutAttempts_ShippingAmount_NonNegative",
                table: "CheckoutAttempts",
                sql: "\"ShippingAmount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAttemptItems_CheckoutAttemptId_ProductId",
                table: "CheckoutAttemptItems",
                columns: new[] { "CheckoutAttemptId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAttemptItems_ProductId",
                table: "CheckoutAttemptItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAttemptItems_ReservationId",
                table: "CheckoutAttemptItems",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId_ProductId",
                table: "OrderItems",
                columns: new[] { "OrderId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutAttemptItems");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CheckoutAttempts_PaymentAmount_Positive",
                table: "CheckoutAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CheckoutAttempts_ShippingAmount_NonNegative",
                table: "CheckoutAttempts");

            migrationBuilder.DropColumn(
                name: "SaleType",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SaleType",
                table: "CheckoutAttempts");

            migrationBuilder.RenameColumn(
                name: "ShippingAmount",
                table: "CheckoutAttempts",
                newName: "FullPrice");

            migrationBuilder.AddColumn<decimal>(
                name: "ItemBalanceAmount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ItemBrandName",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemCategoryName",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ItemDepositAmount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ItemDepositPolicy",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemDisplayName",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemEnglishName",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ItemFullPrice",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ItemLinePaidAmount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ItemPreOrderCloseAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "ItemPrimaryImageUrl",
                table: "Orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemProductSlug",
                table: "Orders",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemUniverseName",
                table: "Orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Item_BalancePaymentDays",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Item_EstimatedArrivalMonth",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Item_EstimatedArrivalYear",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "Item_ProductId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Item_Quantity",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceAmount",
                table: "CheckoutAttempts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BalancePaymentDays",
                table: "CheckoutAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BrandName",
                table: "CheckoutAttempts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CapacityId",
                table: "CheckoutAttempts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "CategoryName",
                table: "CheckoutAttempts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "CheckoutAttempts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DepositPolicy",
                table: "CheckoutAttempts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "CheckoutAttempts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EnglishName",
                table: "CheckoutAttempts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EstimatedArrivalMonth",
                table: "CheckoutAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedArrivalYear",
                table: "CheckoutAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PreOrderCloseAtUtc",
                table: "CheckoutAttempts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "PrimaryImageUrl",
                table: "CheckoutAttempts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "CheckoutAttempts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ProductSlug",
                table: "CheckoutAttempts",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "CheckoutAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "CheckoutAttempts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "UniverseName",
                table: "CheckoutAttempts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAttempts_ProductId",
                table: "CheckoutAttempts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAttempts_ReservationId",
                table: "CheckoutAttempts",
                column: "ReservationId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CheckoutAttempts_Amounts",
                table: "CheckoutAttempts",
                sql: "\"FullPrice\" > 0 AND \"DepositAmount\" > 0 AND \"BalanceAmount\" > 0 AND \"PaymentAmount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CheckoutAttempts_Quantity_Positive",
                table: "CheckoutAttempts",
                sql: "\"Quantity\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckoutAttempts_Products_ProductId",
                table: "CheckoutAttempts",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
#pragma warning restore CA1861
