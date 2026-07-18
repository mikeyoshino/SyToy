using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core generates inline metadata arrays for composite keys and indexes.

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OnHandQuantity = table.Column<int>(type: "integer", nullable: false),
                    HeldQuantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.UniqueConstraint("AK_InventoryItems_Id_ProductId", x => new { x.Id, x.ProductId });
                    table.CheckConstraint("CK_InventoryItems_Audit_Actors_NotBlank", "\"CreatedBy\" ~ '[^[:space:]]' AND \"UpdatedBy\" ~ '[^[:space:]]'");
                    table.CheckConstraint("CK_InventoryItems_Audit_Chronology", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_InventoryItems_HeldQuantity_Bounds", "\"HeldQuantity\" >= 0 AND \"HeldQuantity\" <= \"OnHandQuantity\"");
                    table.CheckConstraint("CK_InventoryItems_OnHandQuantity_NonNegative", "\"OnHandQuantity\" >= 0");
                    table.CheckConstraint("CK_InventoryItems_Version_Positive", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_InventoryItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QuantityDelta = table.Column<int>(type: "integer", nullable: false),
                    ResultingOnHandQuantity = table.Column<int>(type: "integer", nullable: false),
                    ResultingInventoryVersion = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovements_Evidence_NotBlank", "\"Reason\" ~ '[^[:space:]]' AND \"Reference\" ~ '[^[:space:]]' AND \"Actor\" ~ '[^[:space:]]'");
                    table.CheckConstraint("CK_StockMovements_Quantity_Evidence", "(\"Type\" = 'InitialStock' AND \"QuantityDelta\" >= 0 AND \"ReservationId\" IS NULL AND \"ResultingInventoryVersion\" = 1 AND \"QuantityDelta\" = \"ResultingOnHandQuantity\") OR (\"Type\" = 'Received' AND \"QuantityDelta\" > 0 AND \"ReservationId\" IS NULL) OR (\"Type\" = 'Adjusted' AND \"QuantityDelta\" <> 0 AND \"ReservationId\" IS NULL) OR (\"Type\" = 'ReservationConsumed' AND \"QuantityDelta\" < 0 AND \"ReservationId\" IS NOT NULL)");
                    table.CheckConstraint("CK_StockMovements_ResultingInventoryVersion_Positive", "\"ResultingInventoryVersion\" > 0");
                    table.CheckConstraint("CK_StockMovements_ResultingOnHandQuantity_NonNegative", "\"ResultingOnHandQuantity\" >= 0");
                    table.CheckConstraint("CK_StockMovements_Version_MatchesType", "(\"Type\" = 'InitialStock' AND \"ResultingInventoryVersion\" = 1) OR (\"Type\" <> 'InitialStock' AND \"ResultingInventoryVersion\" > 1)");
                    table.ForeignKey(
                        name: "FK_StockMovements_InventoryItems_InventoryItemId_ProductId",
                        columns: x => new { x.InventoryItemId, x.ProductId },
                        principalTable: "InventoryItems",
                        principalColumns: new[] { "Id", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckoutAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReserveReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReserveReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReservedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TerminalAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TerminalActor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TerminalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TerminalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConsumedMovementId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.Id);
                    table.UniqueConstraint("AK_StockReservations_Id_InventoryItemId_ProductId", x => new { x.Id, x.InventoryItemId, x.ProductId });
                    table.CheckConstraint("CK_StockReservations_Evidence_NotBlank", "\"ReserveReason\" ~ '[^[:space:]]' AND \"ReserveReference\" ~ '[^[:space:]]' AND \"ReservedBy\" ~ '[^[:space:]]' AND (\"TerminalActor\" IS NULL OR \"TerminalActor\" ~ '[^[:space:]]') AND (\"TerminalReason\" IS NULL OR \"TerminalReason\" ~ '[^[:space:]]') AND (\"TerminalReference\" IS NULL OR \"TerminalReference\" ~ '[^[:space:]]')");
                    table.CheckConstraint("CK_StockReservations_Expiry_AfterReserved", "\"ExpiresAtUtc\" > \"ReservedAtUtc\"");
                    table.CheckConstraint("CK_StockReservations_Lifecycle_Evidence", "(\"Status\" = 'Active' AND \"TerminalAtUtc\" IS NULL AND \"TerminalActor\" IS NULL AND \"TerminalReason\" IS NULL AND \"TerminalReference\" IS NULL AND \"ConsumedMovementId\" IS NULL) OR (\"Status\" IN ('Released', 'Expired') AND \"TerminalAtUtc\" IS NOT NULL AND \"TerminalActor\" IS NOT NULL AND \"TerminalReason\" IS NOT NULL AND \"TerminalReference\" IS NOT NULL AND \"ConsumedMovementId\" IS NULL) OR (\"Status\" = 'Consumed' AND \"TerminalAtUtc\" IS NOT NULL AND \"TerminalActor\" IS NOT NULL AND \"TerminalReason\" IS NOT NULL AND \"TerminalReference\" IS NOT NULL AND \"ConsumedMovementId\" IS NOT NULL)");
                    table.CheckConstraint("CK_StockReservations_Quantity_Positive", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_StockReservations_Terminal_Chronology", "\"TerminalAtUtc\" IS NULL OR (\"TerminalAtUtc\" >= \"ReservedAtUtc\" AND (\"Status\" <> 'Expired' OR \"TerminalAtUtc\" >= \"ExpiresAtUtc\"))");
                    table.ForeignKey(
                        name: "FK_StockReservations_InventoryItems_InventoryItemId_ProductId",
                        columns: x => new { x.InventoryItemId, x.ProductId },
                        principalTable: "InventoryItems",
                        principalColumns: new[] { "Id", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReservations_StockMovements_ConsumedMovementId",
                        column: x => x.ConsumedMovementId,
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_InventoryItems_ProductId",
                table: "InventoryItems",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryItemId_OccurredAtUtc_Id",
                table: "StockMovements",
                columns: new[] { "InventoryItemId", "OccurredAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryItemId_ProductId",
                table: "StockMovements",
                columns: new[] { "InventoryItemId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReservationId_InventoryItemId_ProductId",
                table: "StockMovements",
                columns: new[] { "ReservationId", "InventoryItemId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "UX_StockMovements_InventoryItemId_InitialStock",
                table: "StockMovements",
                column: "InventoryItemId",
                unique: true,
                filter: "\"Type\" = 'InitialStock'");

            migrationBuilder.CreateIndex(
                name: "UX_StockMovements_InventoryItemId_ResultingInventoryVersion",
                table: "StockMovements",
                columns: new[] { "InventoryItemId", "ResultingInventoryVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_StockMovements_ReservationId",
                table: "StockMovements",
                column: "ReservationId",
                unique: true,
                filter: "\"ReservationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_CheckoutAttemptId",
                table: "StockReservations",
                column: "CheckoutAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_InventoryItemId_ProductId",
                table: "StockReservations",
                columns: new[] { "InventoryItemId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_InventoryItemId_Status_ExpiresAtUtc",
                table: "StockReservations",
                columns: new[] { "InventoryItemId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_StockReservations_ConsumedMovementId",
                table: "StockReservations",
                column: "ConsumedMovementId",
                unique: true,
                filter: "\"ConsumedMovementId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_StockReservations_ReservationId_InventoryIte~",
                table: "StockMovements",
                columns: new[] { "ReservationId", "InventoryItemId", "ProductId" },
                principalTable: "StockReservations",
                principalColumns: new[] { "Id", "InventoryItemId", "ProductId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_InventoryItems_InventoryItemId_ProductId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockReservations_InventoryItems_InventoryItemId_ProductId",
                table: "StockReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_StockReservations_ReservationId_InventoryIte~",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "StockReservations");

            migrationBuilder.DropTable(
                name: "StockMovements");
        }
    }
}
#pragma warning restore CA1861
