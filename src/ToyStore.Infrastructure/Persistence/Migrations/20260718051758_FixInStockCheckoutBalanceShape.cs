using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixInStockCheckoutBalanceShape : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CheckoutAttemptItems_SaleShape",
                table: "CheckoutAttemptItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CheckoutAttemptItems_SaleShape",
                table: "CheckoutAttemptItems",
                sql: "(\"SaleType\" = 'InStock' AND \"DepositAmount\" = 0 AND \"BalanceAmount\" = 0 AND \"PreOrderCloseAtUtc\" IS NULL AND \"DepositPolicy\" IS NULL) OR (\"SaleType\" = 'PreOrder' AND \"DepositAmount\" > 0 AND \"BalanceAmount\" > 0 AND \"PreOrderCloseAtUtc\" IS NOT NULL AND \"DepositPolicy\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CheckoutAttemptItems_SaleShape",
                table: "CheckoutAttemptItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CheckoutAttemptItems_SaleShape",
                table: "CheckoutAttemptItems",
                sql: "(\"SaleType\" = 'InStock' AND \"DepositAmount\" = 0 AND \"BalanceAmount\" = \"UnitPrice\" AND \"PreOrderCloseAtUtc\" IS NULL AND \"DepositPolicy\" IS NULL) OR (\"SaleType\" = 'PreOrder' AND \"DepositAmount\" > 0 AND \"BalanceAmount\" > 0 AND \"PreOrderCloseAtUtc\" IS NOT NULL AND \"DepositPolicy\" IS NOT NULL)");
        }
    }
}
