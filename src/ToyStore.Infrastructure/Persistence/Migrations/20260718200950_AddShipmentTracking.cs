using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ShippedAtUtc",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "OrderAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderAuditEvents_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Carrier = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TrackingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ShippedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shipments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Shipped_Timestamp",
                table: "Orders",
                sql: "(\"FulfillmentStatus\" = 'Shipped' AND \"ShippedAtUtc\" IS NOT NULL) OR (\"FulfillmentStatus\" <> 'Shipped' AND \"ShippedAtUtc\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Version_Positive",
                table: "Orders",
                sql: "\"Version\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAuditEvents_OperationId",
                table: "OrderAuditEvents",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderAuditEvents_OrderId",
                table: "OrderAuditEvents",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Carrier_TrackingNumber",
                table: "Shipments",
                columns: ["Carrier", "TrackingNumber"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OperationId",
                table: "Shipments",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderId",
                table: "Shipments",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderAuditEvents");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Shipped_Timestamp",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Version_Positive",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Orders");
        }
    }
}
