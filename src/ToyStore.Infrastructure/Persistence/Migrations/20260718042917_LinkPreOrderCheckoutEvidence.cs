using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkPreOrderCheckoutEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CheckoutAttempts_ProductId",
                table: "CheckoutAttempts",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckoutAttempts_Products_ProductId",
                table: "CheckoutAttempts",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_CheckoutAttempts_CheckoutAttemptId",
                table: "Orders",
                column: "CheckoutAttemptId",
                principalTable: "CheckoutAttempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_CheckoutAttempts_CheckoutAttemptId",
                table: "Payments",
                column: "CheckoutAttemptId",
                principalTable: "CheckoutAttempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckoutAttempts_Products_ProductId",
                table: "CheckoutAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_CheckoutAttempts_CheckoutAttemptId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_CheckoutAttempts_CheckoutAttemptId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_CheckoutAttempts_ProductId",
                table: "CheckoutAttempts");
        }
    }
}
