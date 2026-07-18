using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartOperationResultReplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResultData",
                table: "CartOperations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ResultingTotalQuantity",
                table: "CartOperations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE "CartOperations" AS operation
                SET "ResultingTotalQuantity" = COALESCE((
                    SELECT SUM(item."Quantity")
                    FROM "CartItems" AS item
                    WHERE item."CartId" = operation."CartId"), 0);

                UPDATE "CartOperations"
                SET "ResultData" = '{"RejectedItems":[],"ClampedItems":[]}'::jsonb
                WHERE "Type" = 'Merge' AND "ResultData" IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CartOperations_ResultData_ByType",
                table: "CartOperations",
                sql: "(\"Type\" = 'Merge' AND \"ResultData\" IS NOT NULL) OR (\"Type\" <> 'Merge' AND \"ResultData\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CartOperations_ResultingTotal_NonNegative",
                table: "CartOperations",
                sql: "\"ResultingTotalQuantity\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CartOperations_ResultData_ByType",
                table: "CartOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CartOperations_ResultingTotal_NonNegative",
                table: "CartOperations");

            migrationBuilder.DropColumn(
                name: "ResultData",
                table: "CartOperations");

            migrationBuilder.DropColumn(
                name: "ResultingTotalQuantity",
                table: "CartOperations");
        }
    }
}
