using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // EF-generated migration uses array arguments required by the fluent API.

namespace ToyStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedEnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ImageStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImagePublicRelativeUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ImageAltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                    table.CheckConstraint("CK_Brands_Image_AllNullOrPresent", "(\"ImageStorageKey\" IS NULL AND \"ImagePublicRelativeUrl\" IS NULL AND \"ImageAltText\" IS NULL) OR (\"ImageStorageKey\" IS NOT NULL AND \"ImageStorageKey\" ~ '[^[:space:]]' AND \"ImagePublicRelativeUrl\" IS NOT NULL AND \"ImagePublicRelativeUrl\" ~ '[^[:space:]]' AND \"ImageAltText\" IS NOT NULL AND \"ImageAltText\" ~ '[^[:space:]]')");
                    table.CheckConstraint("CK_Brands_Slug_Format", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Universes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedEnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    LogoStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LogoPublicRelativeUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LogoAltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Universes", x => x.Id);
                    table.CheckConstraint("CK_Universes_Logo_AllNullOrPresent", "(\"LogoStorageKey\" IS NULL AND \"LogoPublicRelativeUrl\" IS NULL AND \"LogoAltText\" IS NULL) OR (\"LogoStorageKey\" IS NOT NULL AND \"LogoStorageKey\" ~ '[^[:space:]]' AND \"LogoPublicRelativeUrl\" IS NOT NULL AND \"LogoPublicRelativeUrl\" ~ '[^[:space:]]' AND \"LogoAltText\" IS NOT NULL AND \"LogoAltText\" ~ '[^[:space:]]')");
                    table.CheckConstraint("CK_Universes_Slug_Format", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Characters_Universes_UniverseId",
                        column: x => x.UniverseId,
                        principalTable: "Universes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedEnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ProductCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InStockPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    PreOrderFullPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    PreOrderDepositAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    PreOrderCloseAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PreOrderEstimatedArrivalMonth = table.Column<int>(type: "integer", nullable: true),
                    PreOrderEstimatedArrivalYear = table.Column<int>(type: "integer", nullable: true),
                    PreOrderTotalCapacity = table.Column<int>(type: "integer", nullable: true),
                    PreOrderMaxPerCustomer = table.Column<int>(type: "integer", nullable: true),
                    PreOrderBalancePaymentDays = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_InStock_Price", "\"InStockPrice\" IS NULL OR \"InStockPrice\" > 0");
                    table.CheckConstraint("CK_Products_Offer_Matches_SaleType", "(\"SaleType\" = 'InStock' AND \"InStockPrice\" IS NOT NULL AND \"PreOrderFullPrice\" IS NULL AND \"PreOrderDepositAmount\" IS NULL AND \"PreOrderCloseAtUtc\" IS NULL AND \"PreOrderEstimatedArrivalMonth\" IS NULL AND \"PreOrderEstimatedArrivalYear\" IS NULL AND \"PreOrderTotalCapacity\" IS NULL AND \"PreOrderMaxPerCustomer\" IS NULL AND \"PreOrderBalancePaymentDays\" IS NULL) OR (\"SaleType\" = 'PreOrder' AND \"InStockPrice\" IS NULL AND \"PreOrderFullPrice\" IS NOT NULL AND \"PreOrderDepositAmount\" IS NOT NULL AND \"PreOrderCloseAtUtc\" IS NOT NULL AND \"PreOrderEstimatedArrivalMonth\" IS NOT NULL AND \"PreOrderEstimatedArrivalYear\" IS NOT NULL AND \"PreOrderTotalCapacity\" IS NOT NULL AND \"PreOrderMaxPerCustomer\" IS NOT NULL AND \"PreOrderBalancePaymentDays\" IS NOT NULL)");
                    table.CheckConstraint("CK_Products_PreOrder_Amounts", "\"PreOrderFullPrice\" IS NULL OR (\"PreOrderFullPrice\" > 0 AND \"PreOrderDepositAmount\" > 0 AND \"PreOrderDepositAmount\" < \"PreOrderFullPrice\")");
                    table.CheckConstraint("CK_Products_PreOrder_BalancePaymentDays", "\"PreOrderBalancePaymentDays\" IS NULL OR \"PreOrderBalancePaymentDays\" > 0");
                    table.CheckConstraint("CK_Products_PreOrder_BangkokCloseTime", "\"PreOrderCloseAtUtc\" IS NULL OR (\"PreOrderCloseAtUtc\" AT TIME ZONE 'Asia/Bangkok')::time = TIME '23:59:59'");
                    table.CheckConstraint("CK_Products_PreOrder_Capacity", "\"PreOrderTotalCapacity\" IS NULL OR (\"PreOrderTotalCapacity\" > 0 AND \"PreOrderMaxPerCustomer\" > 0 AND \"PreOrderMaxPerCustomer\" <= \"PreOrderTotalCapacity\")");
                    table.CheckConstraint("CK_Products_PreOrder_CloseAfterCreated", "\"PreOrderCloseAtUtc\" IS NULL OR \"PreOrderCloseAtUtc\" > \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Products_PreOrder_EstimatedArrival", "\"PreOrderEstimatedArrivalMonth\" IS NULL OR (\"PreOrderEstimatedArrivalMonth\" BETWEEN 1 AND 12 AND \"PreOrderEstimatedArrivalYear\" BETWEEN 1 AND 9999)");
                    table.CheckConstraint("CK_Products_PreOrder_EtaNotBeforeCloseMonth", "\"PreOrderCloseAtUtc\" IS NULL OR (\"PreOrderEstimatedArrivalYear\", \"PreOrderEstimatedArrivalMonth\") >= (EXTRACT(YEAR FROM \"PreOrderCloseAtUtc\" AT TIME ZONE 'Asia/Bangkok'), EXTRACT(MONTH FROM \"PreOrderCloseAtUtc\" AT TIME ZONE 'Asia/Bangkok'))");
                    table.CheckConstraint("CK_Products_Slug_Format", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "FK_Products_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_ProductCategories_ProductCategoryId",
                        column: x => x.ProductCategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Universes_UniverseId",
                        column: x => x.UniverseId,
                        principalTable: "Universes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductCharacters",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCharacters", x => new { x.ProductId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_ProductCharacters_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductCharacters_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PublicRelativeUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.CheckConstraint("CK_ProductImages_PrimaryMatchesOrder", "\"IsPrimary\" = (\"SortOrder\" = 0)");
                    table.CheckConstraint("CK_ProductImages_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ProductCategories",
                columns: new[] { "Id", "Code" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "ArtToy" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Gundam" }
                });

            migrationBuilder.InsertData(
                table: "Universes",
                columns: new[] { "Id", "ArchivedAtUtc", "ArchivedBy", "CreatedAtUtc", "CreatedBy", "DisplayName", "EnglishName", "NormalizedDisplayName", "NormalizedEnglishName", "Slug", "Status", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:catalog-seed", "Marvel", "Marvel", "MARVEL", "MARVEL", "marvel", "Active", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:catalog-seed" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:catalog-seed", "DC", "DC", "DC", "DC", "dc", "Active", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:catalog-seed" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:catalog-seed", "Unknown", "Unknown", "UNKNOWN", "UNKNOWN", "unknown", "Active", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system:catalog-seed" }
                });

            migrationBuilder.CreateIndex(
                name: "UX_Brands_NormalizedDisplayName",
                table: "Brands",
                column: "NormalizedDisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Brands_NormalizedEnglishName",
                table: "Brands",
                column: "NormalizedEnglishName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Brands_Slug",
                table: "Brands",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Characters_UniverseId_NormalizedName",
                table: "Characters",
                columns: new[] { "UniverseId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProductCategories_Code",
                table: "ProductCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductCharacters_CharacterId",
                table: "ProductCharacters",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "UX_ProductImages_ProductId_Primary",
                table: "ProductImages",
                column: "ProductId",
                unique: true,
                filter: "\"IsPrimary\"");

            migrationBuilder.CreateIndex(
                name: "UX_ProductImages_ProductId_SortOrder",
                table: "ProductImages",
                columns: new[] { "ProductId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProductImages_StorageKey",
                table: "ProductImages",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandId",
                table: "Products",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductCategoryId",
                table: "Products",
                column: "ProductCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status_SaleType",
                table: "Products",
                columns: new[] { "Status", "SaleType" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_UniverseId",
                table: "Products",
                column: "UniverseId");

            migrationBuilder.CreateIndex(
                name: "UX_Products_NormalizedDisplayName",
                table: "Products",
                column: "NormalizedDisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Products_NormalizedEnglishName",
                table: "Products",
                column: "NormalizedEnglishName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Universes_NormalizedDisplayName",
                table: "Universes",
                column: "NormalizedDisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Universes_NormalizedEnglishName",
                table: "Universes",
                column: "NormalizedEnglishName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Universes_Slug",
                table: "Universes",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductCharacters");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "Universes");
        }
    }
}
