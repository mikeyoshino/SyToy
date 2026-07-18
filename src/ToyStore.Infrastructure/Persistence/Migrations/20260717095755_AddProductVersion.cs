using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Products",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Audit_Chronology",
                table: "Products",
                sql: "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"PublishedAtUtc\" IS NULL OR \"PublishedAtUtc\" >= \"CreatedAtUtc\") AND (\"ArchivedAtUtc\" IS NULL OR \"ArchivedAtUtc\" >= \"PublishedAtUtc\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Lifecycle_Audit",
                table: "Products",
                sql: "(\"Status\" = 'Draft' AND \"PublishedAtUtc\" IS NULL AND \"PublishedBy\" IS NULL AND \"ArchivedAtUtc\" IS NULL AND \"ArchivedBy\" IS NULL) OR (\"Status\" = 'Published' AND \"PublishedAtUtc\" IS NOT NULL AND \"PublishedBy\" IS NOT NULL AND \"ArchivedAtUtc\" IS NULL AND \"ArchivedBy\" IS NULL) OR (\"Status\" = 'Archived' AND \"PublishedAtUtc\" IS NOT NULL AND \"PublishedBy\" IS NOT NULL AND \"ArchivedAtUtc\" IS NOT NULL AND \"ArchivedBy\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Version_Positive",
                table: "Products",
                sql: "\"Version\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Audit_Chronology",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Lifecycle_Audit",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Version_Positive",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Products");
        }
    }
}
