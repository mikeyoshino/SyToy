using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowPublishedPreOrderCapacityAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PreOrderCapacityMovements_Type_Evidence",
                table: "PreOrderCapacityMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PreOrderCapacityMovements_Type_Evidence",
                table: "PreOrderCapacityMovements",
                sql: "(\"Type\" = 'InitialCapacity' AND \"AvailableQuantityDelta\" = \"Quantity\" AND \"ReservationId\" IS NULL AND \"ResultingCapacityVersion\" = 1 AND \"ResultingRemainingQuantity\" = \"Quantity\" AND \"ResultingHeldQuantity\" = 0 AND \"ResultingCommittedQuantity\" = 0 AND \"ResultingRetiredQuantity\" = 0) OR (\"Type\" = 'Reserved' AND \"AvailableQuantityDelta\" = -\"Quantity\" AND \"ReservationId\" IS NOT NULL) OR (\"Type\" IN ('Released', 'Expired', 'CancellationReopened') AND \"AvailableQuantityDelta\" = \"Quantity\" AND \"ReservationId\" IS NOT NULL) OR (\"Type\" IN ('ReservationConsumed', 'CancellationRetired') AND \"AvailableQuantityDelta\" = 0 AND \"ReservationId\" IS NOT NULL) OR (\"Type\" = 'CapacityIncreased' AND \"AvailableQuantityDelta\" = \"Quantity\" AND \"ReservationId\" IS NULL) OR (\"Type\" = 'CapacityDecreased' AND \"AvailableQuantityDelta\" = -\"Quantity\" AND \"ReservationId\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PreOrderCapacityMovements_Type_Evidence",
                table: "PreOrderCapacityMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PreOrderCapacityMovements_Type_Evidence",
                table: "PreOrderCapacityMovements",
                sql: "(\"Type\" = 'InitialCapacity' AND \"AvailableQuantityDelta\" = \"Quantity\" AND \"ReservationId\" IS NULL AND \"ResultingCapacityVersion\" = 1 AND \"ResultingRemainingQuantity\" = \"Quantity\" AND \"ResultingHeldQuantity\" = 0 AND \"ResultingCommittedQuantity\" = 0 AND \"ResultingRetiredQuantity\" = 0) OR (\"Type\" = 'Reserved' AND \"AvailableQuantityDelta\" = -\"Quantity\" AND \"ReservationId\" IS NOT NULL) OR (\"Type\" IN ('Released', 'Expired', 'CancellationReopened') AND \"AvailableQuantityDelta\" = \"Quantity\" AND \"ReservationId\" IS NOT NULL) OR (\"Type\" IN ('ReservationConsumed', 'CancellationRetired') AND \"AvailableQuantityDelta\" = 0 AND \"ReservationId\" IS NOT NULL)");
        }
    }
}
