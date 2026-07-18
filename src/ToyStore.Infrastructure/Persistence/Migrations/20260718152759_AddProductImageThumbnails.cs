using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPublicRelativeUrl",
                table: "ProductImages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailStorageKey",
                table: "ProductImages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProductImages_ThumbnailStorageKey",
                table: "ProductImages",
                column: "ThumbnailStorageKey",
                unique: true,
                filter: "\"ThumbnailStorageKey\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductImages_Thumbnail_AllNullOrPresent",
                table: "ProductImages",
                sql: "(\"ThumbnailStorageKey\" IS NULL AND \"ThumbnailPublicRelativeUrl\" IS NULL) OR (\"ThumbnailStorageKey\" IS NOT NULL AND \"ThumbnailPublicRelativeUrl\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ProductImages_ThumbnailStorageKey",
                table: "ProductImages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductImages_Thumbnail_AllNullOrPresent",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "ThumbnailPublicRelativeUrl",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "ThumbnailStorageKey",
                table: "ProductImages");
        }
    }
}
