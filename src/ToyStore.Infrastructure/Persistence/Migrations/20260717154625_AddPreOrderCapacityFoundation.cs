using System;
#pragma warning disable CA1861
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace ToyStore.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260717154625_AddPreOrderCapacityFoundation")]
public class AddPreOrderCapacityFoundation : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("PreOrderCapacities", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> productId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> totalCapacity = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> heldQuantity = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> committedQuantity = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> retiredQuantity = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> closeAtUtc = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> createdAtUtc = table.Column<DateTimeOffset>("timestamp with time zone");
			int? maxLength = 200;
			OperationBuilder<AddColumnOperation> createdBy = table.Column<string>("character varying(200)", null, maxLength);
			OperationBuilder<AddColumnOperation> updatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone");
			maxLength = 200;
			return new
			{
				Id = id,
				ProductId = productId,
				TotalCapacity = totalCapacity,
				HeldQuantity = heldQuantity,
				CommittedQuantity = committedQuantity,
				RetiredQuantity = retiredQuantity,
				CloseAtUtc = closeAtUtc,
				CreatedAtUtc = createdAtUtc,
				CreatedBy = createdBy,
				UpdatedAtUtc = updatedAtUtc,
				UpdatedBy = table.Column<string>("character varying(200)", null, maxLength),
				Version = table.Column<long>("bigint")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_PreOrderCapacities", x => x.Id);
			table.UniqueConstraint("AK_PreOrderCapacities_Id_ProductId", x => new { x.Id, x.ProductId });
			table.CheckConstraint("CK_PreOrderCapacities_Audit_Actors_NotBlank", "\"CreatedBy\" ~ '[^[:space:]]' AND \"UpdatedBy\" ~ '[^[:space:]]'");
			table.CheckConstraint("CK_PreOrderCapacities_Audit_Chronology", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
			table.CheckConstraint("CK_PreOrderCapacities_CloseAfterCreated", "\"CloseAtUtc\" > \"CreatedAtUtc\"");
			table.CheckConstraint("CK_PreOrderCapacities_QuantityAccounting", "\"HeldQuantity\" >= 0 AND \"CommittedQuantity\" >= 0 AND \"RetiredQuantity\" >= 0 AND \"HeldQuantity\" + \"CommittedQuantity\" + \"RetiredQuantity\" <= \"TotalCapacity\"");
			table.CheckConstraint("CK_PreOrderCapacities_TotalCapacity_Positive", "\"TotalCapacity\" > 0");
			table.CheckConstraint("CK_PreOrderCapacities_Version_Positive", "\"Version\" > 0");
			table.ForeignKey("FK_PreOrderCapacities_Products_ProductId", x => x.ProductId, "Products", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("PreOrderCapacityReservations", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> capacityId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> productId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> checkoutAttemptId = table.Column<Guid>("uuid");
			int? maxLength = 450;
			OperationBuilder<AddColumnOperation> customerId = table.Column<string>("character varying(450)", null, maxLength);
			OperationBuilder<AddColumnOperation> quantity = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> reservedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> expiresAtUtc = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> reserveMovementId = table.Column<Guid>("uuid");
			maxLength = 300;
			OperationBuilder<AddColumnOperation> reserveReason = table.Column<string>("character varying(300)", null, maxLength);
			maxLength = 200;
			OperationBuilder<AddColumnOperation> reserveReference = table.Column<string>("character varying(200)", null, maxLength);
			maxLength = 200;
			OperationBuilder<AddColumnOperation> reservedBy = table.Column<string>("character varying(200)", null, maxLength);
			maxLength = 32;
			OperationBuilder<AddColumnOperation> status = table.Column<string>("character varying(32)", null, maxLength);
			OperationBuilder<AddColumnOperation> transitionAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength = 200;
			OperationBuilder<AddColumnOperation> transitionActor = table.Column<string>("character varying(200)", null, maxLength, rowVersion: false, null, nullable: true);
			maxLength = 300;
			OperationBuilder<AddColumnOperation> transitionReason = table.Column<string>("character varying(300)", null, maxLength, rowVersion: false, null, nullable: true);
			maxLength = 200;
			OperationBuilder<AddColumnOperation> transitionReference = table.Column<string>("character varying(200)", null, maxLength, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> transitionMovementId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> consumedMovementId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			maxLength = 32;
			OperationBuilder<AddColumnOperation> cancellationKind = table.Column<string>("character varying(32)", null, maxLength, rowVersion: false, null, nullable: true);
			maxLength = 32;
			return new
			{
				Id = id,
				CapacityId = capacityId,
				ProductId = productId,
				CheckoutAttemptId = checkoutAttemptId,
				CustomerId = customerId,
				Quantity = quantity,
				ReservedAtUtc = reservedAtUtc,
				ExpiresAtUtc = expiresAtUtc,
				ReserveMovementId = reserveMovementId,
				ReserveReason = reserveReason,
				ReserveReference = reserveReference,
				ReservedBy = reservedBy,
				Status = status,
				TransitionAtUtc = transitionAtUtc,
				TransitionActor = transitionActor,
				TransitionReason = transitionReason,
				TransitionReference = transitionReference,
				TransitionMovementId = transitionMovementId,
				ConsumedMovementId = consumedMovementId,
				CancellationKind = cancellationKind,
				DepositDisposition = table.Column<string>("character varying(32)", null, maxLength, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_PreOrderCapacityReservations", x => x.Id);
			table.UniqueConstraint("AK_PreOrderCapacityReservations_Id_CapacityId_ProductId", x => new { x.Id, x.CapacityId, x.ProductId });
			table.CheckConstraint("CK_PreOrderCapacityReservations_CancellationPolicy", "\"Status\" <> 'Cancelled' OR ((\"CancellationKind\" IN ('Customer', 'BalanceOverdue') AND \"DepositDisposition\" = 'Forfeited') OR (\"CancellationKind\" = 'AdminOrSupplier' AND \"DepositDisposition\" = 'RefundRequired'))");
			table.CheckConstraint("CK_PreOrderCapacityReservations_Evidence_NotBlank", "\"CustomerId\" ~ '[^[:space:]]' AND \"ReserveReason\" ~ '[^[:space:]]' AND \"ReserveReference\" ~ '[^[:space:]]' AND \"ReservedBy\" ~ '[^[:space:]]' AND (\"TransitionActor\" IS NULL OR \"TransitionActor\" ~ '[^[:space:]]') AND (\"TransitionReason\" IS NULL OR \"TransitionReason\" ~ '[^[:space:]]') AND (\"TransitionReference\" IS NULL OR \"TransitionReference\" ~ '[^[:space:]]')");
			table.CheckConstraint("CK_PreOrderCapacityReservations_Expiry_AfterReserved", "\"ExpiresAtUtc\" > \"ReservedAtUtc\"");
			table.CheckConstraint("CK_PreOrderCapacityReservations_Lifecycle_Evidence", "(\"Status\" = 'Active' AND \"TransitionAtUtc\" IS NULL AND \"TransitionActor\" IS NULL AND \"TransitionReason\" IS NULL AND \"TransitionReference\" IS NULL AND \"TransitionMovementId\" IS NULL AND \"ConsumedMovementId\" IS NULL AND \"CancellationKind\" IS NULL AND \"DepositDisposition\" IS NULL) OR (\"Status\" IN ('Released', 'Expired') AND \"TransitionAtUtc\" IS NOT NULL AND \"TransitionActor\" IS NOT NULL AND \"TransitionReason\" IS NOT NULL AND \"TransitionReference\" IS NOT NULL AND \"TransitionMovementId\" IS NOT NULL AND \"ConsumedMovementId\" IS NULL AND \"CancellationKind\" IS NULL AND \"DepositDisposition\" IS NULL) OR (\"Status\" = 'Consumed' AND \"TransitionAtUtc\" IS NOT NULL AND \"TransitionActor\" IS NOT NULL AND \"TransitionReason\" IS NOT NULL AND \"TransitionReference\" IS NOT NULL AND \"TransitionMovementId\" IS NOT NULL AND \"ConsumedMovementId\" = \"TransitionMovementId\" AND \"CancellationKind\" IS NULL AND \"DepositDisposition\" IS NULL) OR (\"Status\" = 'Cancelled' AND \"TransitionAtUtc\" IS NOT NULL AND \"TransitionActor\" IS NOT NULL AND \"TransitionReason\" IS NOT NULL AND \"TransitionReference\" IS NOT NULL AND \"TransitionMovementId\" IS NOT NULL AND \"ConsumedMovementId\" IS NOT NULL AND \"CancellationKind\" IS NOT NULL AND \"DepositDisposition\" IS NOT NULL)");
			table.CheckConstraint("CK_PreOrderCapacityReservations_Quantity_Positive", "\"Quantity\" > 0");
			table.CheckConstraint("CK_PreOrderCapacityReservations_Transition_Chronology", "\"TransitionAtUtc\" IS NULL OR (\"TransitionAtUtc\" >= \"ReservedAtUtc\" AND (\"Status\" <> 'Expired' OR \"TransitionAtUtc\" >= \"ExpiresAtUtc\"))");
			table.ForeignKey("FK_PreOrderCapacityReservations_PreOrderCapacities_CapacityId_~", x => new { x.CapacityId, x.ProductId }, "PreOrderCapacities", new string[2] { "Id", "ProductId" }, null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("PreOrderCapacityMovements", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> capacityId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> productId = table.Column<Guid>("uuid");
			int? maxLength = 32;
			OperationBuilder<AddColumnOperation> type = table.Column<string>("character varying(32)", null, maxLength);
			OperationBuilder<AddColumnOperation> quantity = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> availableQuantityDelta = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> resultingRemainingQuantity = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> resultingHeldQuantity = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> resultingCommittedQuantity = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> resultingRetiredQuantity = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> resultingCapacityVersion = table.Column<long>("bigint");
			maxLength = 300;
			OperationBuilder<AddColumnOperation> reason = table.Column<string>("character varying(300)", null, maxLength);
			maxLength = 200;
			OperationBuilder<AddColumnOperation> reference = table.Column<string>("character varying(200)", null, maxLength);
			maxLength = 200;
			return new
			{
				Id = id,
				CapacityId = capacityId,
				ProductId = productId,
				Type = type,
				Quantity = quantity,
				AvailableQuantityDelta = availableQuantityDelta,
				ResultingRemainingQuantity = resultingRemainingQuantity,
				ResultingHeldQuantity = resultingHeldQuantity,
				ResultingCommittedQuantity = resultingCommittedQuantity,
				ResultingRetiredQuantity = resultingRetiredQuantity,
				ResultingCapacityVersion = resultingCapacityVersion,
				Reason = reason,
				Reference = reference,
				Actor = table.Column<string>("character varying(200)", null, maxLength),
				OccurredAtUtc = table.Column<DateTimeOffset>("timestamp with time zone"),
				ReservationId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_PreOrderCapacityMovements", x => x.Id);
			table.CheckConstraint("CK_PreOrderCapacityMovements_Evidence_NotBlank", "\"Reason\" ~ '[^[:space:]]' AND \"Reference\" ~ '[^[:space:]]' AND \"Actor\" ~ '[^[:space:]]'");
			table.CheckConstraint("CK_PreOrderCapacityMovements_Quantity_Positive", "\"Quantity\" > 0");
			table.CheckConstraint("CK_PreOrderCapacityMovements_ResultingQuantities_NonNegative", "\"ResultingRemainingQuantity\" >= 0 AND \"ResultingHeldQuantity\" >= 0 AND \"ResultingCommittedQuantity\" >= 0 AND \"ResultingRetiredQuantity\" >= 0");
			table.CheckConstraint("CK_PreOrderCapacityMovements_ResultingVersion_Positive", "\"ResultingCapacityVersion\" > 0");
			table.CheckConstraint("CK_PreOrderCapacityMovements_Type_Evidence", "(\"Type\" = 'InitialCapacity' AND \"AvailableQuantityDelta\" = \"Quantity\" AND \"ReservationId\" IS NULL AND \"ResultingCapacityVersion\" = 1 AND \"ResultingRemainingQuantity\" = \"Quantity\" AND \"ResultingHeldQuantity\" = 0 AND \"ResultingCommittedQuantity\" = 0 AND \"ResultingRetiredQuantity\" = 0) OR (\"Type\" = 'Reserved' AND \"AvailableQuantityDelta\" = -\"Quantity\" AND \"ReservationId\" IS NOT NULL) OR (\"Type\" IN ('Released', 'Expired', 'CancellationReopened') AND \"AvailableQuantityDelta\" = \"Quantity\" AND \"ReservationId\" IS NOT NULL) OR (\"Type\" IN ('ReservationConsumed', 'CancellationRetired') AND \"AvailableQuantityDelta\" = 0 AND \"ReservationId\" IS NOT NULL)");
			table.ForeignKey("FK_PreOrderCapacityMovements_PreOrderCapacities_CapacityId_Pro~", x => new { x.CapacityId, x.ProductId }, "PreOrderCapacities", new string[2] { "Id", "ProductId" }, null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_PreOrderCapacityMovements_PreOrderCapacityReservations_Rese~", x => new { x.ReservationId, x.CapacityId, x.ProductId }, "PreOrderCapacityReservations", new string[3] { "Id", "CapacityId", "ProductId" }, null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateIndex("UX_PreOrderCapacities_ProductId", "PreOrderCapacities", "ProductId", null, unique: true);
		migrationBuilder.CreateIndex("IX_PreOrderCapacityMovements_CapacityId_OccurredAtUtc_Id", "PreOrderCapacityMovements", new string[3] { "CapacityId", "OccurredAtUtc", "Id" }, null, unique: false, null, new bool[3] { false, true, true });
		migrationBuilder.CreateIndex("IX_PreOrderCapacityMovements_CapacityId_ProductId", "PreOrderCapacityMovements", new string[2] { "CapacityId", "ProductId" });
		migrationBuilder.CreateIndex("IX_PreOrderCapacityMovements_ReservationId_CapacityId_ProductId", "PreOrderCapacityMovements", new string[3] { "ReservationId", "CapacityId", "ProductId" });
		migrationBuilder.CreateIndex("UX_PreOrderCapacityMovements_CapacityId_InitialCapacity", "PreOrderCapacityMovements", "CapacityId", null, unique: true, "\"Type\" = 'InitialCapacity'");
		migrationBuilder.CreateIndex("UX_PreOrderCapacityMovements_CapacityId_Version", "PreOrderCapacityMovements", new string[2] { "CapacityId", "ResultingCapacityVersion" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_PreOrderCapacityReservations_CapacityId_ProductId", "PreOrderCapacityReservations", new string[2] { "CapacityId", "ProductId" });
		migrationBuilder.CreateIndex("IX_PreOrderCapacityReservations_CapacityId_Status_ExpiresAtUtc", "PreOrderCapacityReservations", new string[3] { "CapacityId", "Status", "ExpiresAtUtc" });
		migrationBuilder.CreateIndex("IX_PreOrderCapacityReservations_ProductId_CustomerId_Status", "PreOrderCapacityReservations", new string[3] { "ProductId", "CustomerId", "Status" });
		migrationBuilder.CreateIndex("UX_PreOrderCapacityReservations_CheckoutAttemptId", "PreOrderCapacityReservations", "CheckoutAttemptId", null, unique: true);
		migrationBuilder.CreateIndex("UX_PreOrderCapacityReservations_ReserveMovementId", "PreOrderCapacityReservations", "ReserveMovementId", null, unique: true);
		migrationBuilder.CreateIndex("UX_PreOrderCapacityReservations_TransitionMovementId", "PreOrderCapacityReservations", "TransitionMovementId", null, unique: true, "\"TransitionMovementId\" IS NOT NULL");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("PreOrderCapacityMovements");
		migrationBuilder.DropTable("PreOrderCapacityReservations");
		migrationBuilder.DropTable("PreOrderCapacities");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.10").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("Id").HasColumnType("text");
			b.Property<string>("ConcurrencyStamp").IsConcurrencyToken().HasColumnType("text");
			b.Property<string>("Name").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("NormalizedName").HasMaxLength(256).HasColumnType("character varying(256)");
			b.HasKey("Id");
			b.HasIndex("NormalizedName").IsUnique().HasDatabaseName("RoleNameIndex");
			b.ToTable("AspNetRoles", (string?)null);
		});
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", delegate(EntityTypeBuilder b)
		{
			b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("integer");
			b.Property<int>("Id").UseIdentityByDefaultColumn();
			b.Property<string>("ClaimType").HasColumnType("text");
			b.Property<string>("ClaimValue").HasColumnType("text");
			b.Property<string>("RoleId").IsRequired().HasColumnType("text");
			b.HasKey("Id");
			b.HasIndex("RoleId");
			b.ToTable("AspNetRoleClaims", (string?)null);
		});
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", delegate(EntityTypeBuilder b)
		{
			b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("integer");
			b.Property<int>("Id").UseIdentityByDefaultColumn();
			b.Property<string>("ClaimType").HasColumnType("text");
			b.Property<string>("ClaimValue").HasColumnType("text");
			b.Property<string>("UserId").IsRequired().HasColumnType("text");
			b.HasKey("Id");
			b.HasIndex("UserId");
			b.ToTable("AspNetUserClaims", (string?)null);
		});
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("LoginProvider").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("ProviderKey").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("ProviderDisplayName").HasColumnType("text");
			b.Property<string>("UserId").IsRequired().HasColumnType("text");
			b.HasKey("LoginProvider", "ProviderKey");
			b.HasIndex("UserId");
			b.ToTable("AspNetUserLogins", (string?)null);
		});
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("UserId").HasColumnType("text");
			b.Property<string>("RoleId").HasColumnType("text");
			b.HasKey("UserId", "RoleId");
			b.HasIndex("RoleId");
			b.ToTable("AspNetUserRoles", (string?)null);
		});
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("UserId").HasColumnType("text");
			b.Property<string>("LoginProvider").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("Name").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("Value").HasColumnType("text");
			b.HasKey("UserId", "LoginProvider", "Name");
			b.ToTable("AspNetUserTokens", (string?)null);
		});
		modelBuilder.Entity("ToyStore.Domain.Carts.Cart", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("CustomerId").IsRequired().HasMaxLength(450)
				.HasColumnType("character varying(450)");
			b.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("CustomerId").IsUnique().HasDatabaseName("UX_Carts_CustomerId");
			b.ToTable("Carts", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_Carts_Audit_Chronology", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
				t.HasCheckConstraint("CK_Carts_CustomerId_NotBlank", "\"CustomerId\" ~ '[^[:space:]]'");
				t.HasCheckConstraint("CK_Carts_Id_NotEmpty", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
				t.HasCheckConstraint("CK_Carts_Version_Positive", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.Carts.CartItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("CartId").HasColumnType("uuid");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<int>("Quantity").HasColumnType("integer");
			b.HasKey("CartId", "ProductId");
			b.HasIndex("ProductId").HasDatabaseName("IX_CartItems_ProductId");
			b.ToTable("CartItems", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_CartItems_CartId_NotEmpty", "\"CartId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
				t.HasCheckConstraint("CK_CartItems_ProductId_NotEmpty", "\"ProductId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
				t.HasCheckConstraint("CK_CartItems_Quantity_Bounds", "\"Quantity\" BETWEEN 1 AND 99");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.Carts.CartOperation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CartId").HasColumnType("uuid");
			b.Property<string>("IntentFingerprint").IsRequired().HasMaxLength(64)
				.HasColumnType("character(64)")
				.IsFixedLength();
			b.Property<DateTimeOffset>("OccurredAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("ResultData").HasColumnType("jsonb");
			b.Property<long>("ResultingCartVersion").HasColumnType("bigint");
			b.Property<long>("ResultingTotalQuantity").HasColumnType("bigint");
			b.Property<string>("Type").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.HasKey("Id");
			b.HasIndex("CartId", "OccurredAtUtc").HasDatabaseName("IX_CartOperations_CartId_OccurredAtUtc");
			b.ToTable("CartOperations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_CartOperations_CartId_NotEmpty", "\"CartId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
				t.HasCheckConstraint("CK_CartOperations_Fingerprint", "\"IntentFingerprint\" ~ '^[0-9a-f]{64}$'");
				t.HasCheckConstraint("CK_CartOperations_Id_NotEmpty", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
				t.HasCheckConstraint("CK_CartOperations_ResultData_ByType", "(\"Type\" = 'Merge' AND \"ResultData\" IS NOT NULL) OR (\"Type\" <> 'Merge' AND \"ResultData\" IS NULL)");
				t.HasCheckConstraint("CK_CartOperations_ResultingTotal_NonNegative", "\"ResultingTotalQuantity\" >= 0");
				t.HasCheckConstraint("CK_CartOperations_ResultingVersion_Positive", "\"ResultingCartVersion\" > 0");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.Catalog.Brand", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset?>("ArchivedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("ArchivedBy").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("CreatedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("DisplayName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("EnglishName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("NormalizedDisplayName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("NormalizedEnglishName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Slug").IsRequired().HasMaxLength(240)
				.HasColumnType("character varying(240)");
			b.Property<string>("Status").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("UpdatedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<long>("Version").IsConcurrencyToken().ValueGeneratedOnAdd()
				.HasColumnType("bigint")
				.HasDefaultValue(1L);
			b.HasKey("Id");
			b.HasIndex("NormalizedDisplayName").IsUnique().HasDatabaseName("UX_Brands_NormalizedDisplayName");
			b.HasIndex("NormalizedEnglishName").IsUnique().HasDatabaseName("UX_Brands_NormalizedEnglishName");
			b.HasIndex("Slug").IsUnique().HasDatabaseName("UX_Brands_Slug");
			b.ToTable("Brands", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_Brands_Image_AllNullOrPresent", "(\"ImageStorageKey\" IS NULL AND \"ImagePublicRelativeUrl\" IS NULL AND \"ImageAltText\" IS NULL) OR (\"ImageStorageKey\" IS NOT NULL AND \"ImageStorageKey\" ~ '[^[:space:]]' AND \"ImagePublicRelativeUrl\" IS NOT NULL AND \"ImagePublicRelativeUrl\" ~ '[^[:space:]]' AND \"ImageAltText\" IS NOT NULL AND \"ImageAltText\" ~ '[^[:space:]]')");
				t.HasCheckConstraint("CK_Brands_Slug_Format", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.Catalog.Character", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("NormalizedName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<Guid>("UniverseId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("UniverseId", "NormalizedName").IsUnique().HasDatabaseName("UX_Characters_UniverseId_NormalizedName");
			b.ToTable("Characters", (string?)null);
		});
		modelBuilder.Entity("ToyStore.Domain.Catalog.ProductCategory", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Code").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.HasKey("Id");
			b.HasIndex("Code").IsUnique().HasDatabaseName("UX_ProductCategories_Code");
			b.ToTable("ProductCategories", (string?)null);
			b.HasData(new
			{
				Id = new Guid("10000000-0000-0000-0000-000000000001"),
				Code = "ArtToy"
			}, new
			{
				Id = new Guid("10000000-0000-0000-0000-000000000002"),
				Code = "Gundam"
			});
		});
		modelBuilder.Entity("ToyStore.Domain.Catalog.Universe", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset?>("ArchivedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("ArchivedBy").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("CreatedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("DisplayName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("EnglishName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("NormalizedDisplayName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("NormalizedEnglishName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Slug").IsRequired().HasMaxLength(240)
				.HasColumnType("character varying(240)");
			b.Property<string>("Status").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("UpdatedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<long>("Version").IsConcurrencyToken().ValueGeneratedOnAdd()
				.HasColumnType("bigint")
				.HasDefaultValue(1L);
			b.HasKey("Id");
			b.HasIndex("NormalizedDisplayName").IsUnique().HasDatabaseName("UX_Universes_NormalizedDisplayName");
			b.HasIndex("NormalizedEnglishName").IsUnique().HasDatabaseName("UX_Universes_NormalizedEnglishName");
			b.HasIndex("Slug").IsUnique().HasDatabaseName("UX_Universes_Slug");
			b.ToTable("Universes", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_Universes_Logo_AllNullOrPresent", "(\"LogoStorageKey\" IS NULL AND \"LogoPublicRelativeUrl\" IS NULL AND \"LogoAltText\" IS NULL) OR (\"LogoStorageKey\" IS NOT NULL AND \"LogoStorageKey\" ~ '[^[:space:]]' AND \"LogoPublicRelativeUrl\" IS NOT NULL AND \"LogoPublicRelativeUrl\" ~ '[^[:space:]]' AND \"LogoAltText\" IS NOT NULL AND \"LogoAltText\" ~ '[^[:space:]]')");
				t.HasCheckConstraint("CK_Universes_Slug_Format", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
			});
			b.HasData(new
			{
				Id = new Guid("20000000-0000-0000-0000-000000000001"),
				CreatedAtUtc = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
				CreatedBy = "system:catalog-seed",
				DisplayName = "Marvel",
				EnglishName = "Marvel",
				NormalizedDisplayName = "MARVEL",
				NormalizedEnglishName = "MARVEL",
				Slug = "marvel",
				Status = "Active",
				UpdatedAtUtc = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
				UpdatedBy = "system:catalog-seed",
				Version = 1L
			}, new
			{
				Id = new Guid("20000000-0000-0000-0000-000000000002"),
				CreatedAtUtc = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
				CreatedBy = "system:catalog-seed",
				DisplayName = "DC",
				EnglishName = "DC",
				NormalizedDisplayName = "DC",
				NormalizedEnglishName = "DC",
				Slug = "dc",
				Status = "Active",
				UpdatedAtUtc = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
				UpdatedBy = "system:catalog-seed",
				Version = 1L
			}, new
			{
				Id = new Guid("20000000-0000-0000-0000-000000000003"),
				CreatedAtUtc = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
				CreatedBy = "system:catalog-seed",
				DisplayName = "Unknown",
				EnglishName = "Unknown",
				NormalizedDisplayName = "UNKNOWN",
				NormalizedEnglishName = "UNKNOWN",
				Slug = "unknown",
				Status = "Active",
				UpdatedAtUtc = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
				UpdatedBy = "system:catalog-seed",
				Version = 1L
			});
		});
		modelBuilder.Entity("ToyStore.Domain.Inventory.InventoryItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("CreatedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<int>("HeldQuantity").HasColumnType("integer");
			b.Property<int>("OnHandQuantity").HasColumnType("integer");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("UpdatedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.HasKey("Id");
			b.HasAlternateKey("Id", "ProductId").HasName("AK_InventoryItems_Id_ProductId");
			b.HasIndex("ProductId").IsUnique().HasDatabaseName("UX_InventoryItems_ProductId");
			b.ToTable("InventoryItems", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_InventoryItems_Audit_Actors_NotBlank", "\"CreatedBy\" ~ '[^[:space:]]' AND \"UpdatedBy\" ~ '[^[:space:]]'");
				t.HasCheckConstraint("CK_InventoryItems_Audit_Chronology", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
				t.HasCheckConstraint("CK_InventoryItems_HeldQuantity_Bounds", "\"HeldQuantity\" >= 0 AND \"HeldQuantity\" <= \"OnHandQuantity\"");
				t.HasCheckConstraint("CK_InventoryItems_OnHandQuantity_NonNegative", "\"OnHandQuantity\" >= 0");
				t.HasCheckConstraint("CK_InventoryItems_Version_Positive", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.Inventory.StockMovement", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Actor").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<Guid>("InventoryItemId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("OccurredAtUtc").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<int>("QuantityDelta").HasColumnType("integer");
			b.Property<string>("Reason").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<string>("Reference").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<Guid?>("ReservationId").HasColumnType("uuid");
			b.Property<long>("ResultingInventoryVersion").HasColumnType("bigint");
			b.Property<int>("ResultingOnHandQuantity").HasColumnType("integer");
			b.Property<string>("Type").IsRequired().HasMaxLength(32)
				.HasColumnType("character varying(32)");
			b.HasKey("Id").HasName("PK_StockMovements");
			b.HasIndex("InventoryItemId").IsUnique().HasDatabaseName("UX_StockMovements_InventoryItemId_InitialStock")
				.HasFilter("\"Type\" = 'InitialStock'");
			b.HasIndex("ReservationId").IsUnique().HasDatabaseName("UX_StockMovements_ReservationId")
				.HasFilter("\"ReservationId\" IS NOT NULL");
			b.HasIndex("InventoryItemId", "ProductId");
			b.HasIndex("InventoryItemId", "ResultingInventoryVersion").IsUnique().HasDatabaseName("UX_StockMovements_InventoryItemId_ResultingInventoryVersion");
			b.HasIndex("InventoryItemId", "OccurredAtUtc", "Id").IsDescending(false, true, true).HasDatabaseName("IX_StockMovements_InventoryItemId_OccurredAtUtc_Id");
			b.HasIndex("ReservationId", "InventoryItemId", "ProductId");
			b.ToTable("StockMovements", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_StockMovements_Evidence_NotBlank", "\"Reason\" ~ '[^[:space:]]' AND \"Reference\" ~ '[^[:space:]]' AND \"Actor\" ~ '[^[:space:]]'");
				t.HasCheckConstraint("CK_StockMovements_Quantity_Evidence", "(\"Type\" = 'InitialStock' AND \"QuantityDelta\" >= 0 AND \"ReservationId\" IS NULL AND \"ResultingInventoryVersion\" = 1 AND \"QuantityDelta\" = \"ResultingOnHandQuantity\") OR (\"Type\" = 'Received' AND \"QuantityDelta\" > 0 AND \"ReservationId\" IS NULL) OR (\"Type\" = 'Adjusted' AND \"QuantityDelta\" <> 0 AND \"ReservationId\" IS NULL) OR (\"Type\" = 'ReservationConsumed' AND \"QuantityDelta\" < 0 AND \"ReservationId\" IS NOT NULL)");
				t.HasCheckConstraint("CK_StockMovements_ResultingInventoryVersion_Positive", "\"ResultingInventoryVersion\" > 0");
				t.HasCheckConstraint("CK_StockMovements_ResultingOnHandQuantity_NonNegative", "\"ResultingOnHandQuantity\" >= 0");
				t.HasCheckConstraint("CK_StockMovements_Version_MatchesType", "(\"Type\" = 'InitialStock' AND \"ResultingInventoryVersion\" = 1) OR (\"Type\" <> 'InitialStock' AND \"ResultingInventoryVersion\" > 1)");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.Inventory.StockReservation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CheckoutAttemptId").HasColumnType("uuid");
			b.Property<Guid?>("ConsumedMovementId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("ExpiresAtUtc").HasColumnType("timestamp with time zone");
			b.Property<Guid>("InventoryItemId").HasColumnType("uuid");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<int>("Quantity").HasColumnType("integer");
			b.Property<string>("ReserveReason").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<string>("ReserveReference").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTimeOffset>("ReservedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("ReservedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Status").IsRequired().HasMaxLength(32)
				.HasColumnType("character varying(32)");
			b.Property<string>("TerminalActor").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTimeOffset?>("TerminalAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("TerminalReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("TerminalReference").HasMaxLength(200).HasColumnType("character varying(200)");
			b.HasKey("Id");
			b.HasAlternateKey("Id", "InventoryItemId", "ProductId").HasName("AK_StockReservations_Id_InventoryItemId_ProductId");
			b.HasIndex("CheckoutAttemptId").HasDatabaseName("IX_StockReservations_CheckoutAttemptId");
			b.HasIndex("ConsumedMovementId").IsUnique().HasDatabaseName("UX_StockReservations_ConsumedMovementId")
				.HasFilter("\"ConsumedMovementId\" IS NOT NULL");
			b.HasIndex("InventoryItemId", "ProductId");
			b.HasIndex("InventoryItemId", "Status", "ExpiresAtUtc").HasDatabaseName("IX_StockReservations_InventoryItemId_Status_ExpiresAtUtc");
			b.ToTable("StockReservations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_StockReservations_Evidence_NotBlank", "\"ReserveReason\" ~ '[^[:space:]]' AND \"ReserveReference\" ~ '[^[:space:]]' AND \"ReservedBy\" ~ '[^[:space:]]' AND (\"TerminalActor\" IS NULL OR \"TerminalActor\" ~ '[^[:space:]]') AND (\"TerminalReason\" IS NULL OR \"TerminalReason\" ~ '[^[:space:]]') AND (\"TerminalReference\" IS NULL OR \"TerminalReference\" ~ '[^[:space:]]')");
				t.HasCheckConstraint("CK_StockReservations_Expiry_AfterReserved", "\"ExpiresAtUtc\" > \"ReservedAtUtc\"");
				t.HasCheckConstraint("CK_StockReservations_Lifecycle_Evidence", "(\"Status\" = 'Active' AND \"TerminalAtUtc\" IS NULL AND \"TerminalActor\" IS NULL AND \"TerminalReason\" IS NULL AND \"TerminalReference\" IS NULL AND \"ConsumedMovementId\" IS NULL) OR (\"Status\" IN ('Released', 'Expired') AND \"TerminalAtUtc\" IS NOT NULL AND \"TerminalActor\" IS NOT NULL AND \"TerminalReason\" IS NOT NULL AND \"TerminalReference\" IS NOT NULL AND \"ConsumedMovementId\" IS NULL) OR (\"Status\" = 'Consumed' AND \"TerminalAtUtc\" IS NOT NULL AND \"TerminalActor\" IS NOT NULL AND \"TerminalReason\" IS NOT NULL AND \"TerminalReference\" IS NOT NULL AND \"ConsumedMovementId\" IS NOT NULL)");
				t.HasCheckConstraint("CK_StockReservations_Quantity_Positive", "\"Quantity\" > 0");
				t.HasCheckConstraint("CK_StockReservations_Terminal_Chronology", "\"TerminalAtUtc\" IS NULL OR (\"TerminalAtUtc\" >= \"ReservedAtUtc\" AND (\"Status\" <> 'Expired' OR \"TerminalAtUtc\" >= \"ExpiresAtUtc\"))");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.PreOrders.PreOrderCapacity", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("CloseAtUtc").HasColumnType("timestamp with time zone");
			b.Property<int>("CommittedQuantity").HasColumnType("integer");
			b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("CreatedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<int>("HeldQuantity").HasColumnType("integer");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<int>("RetiredQuantity").HasColumnType("integer");
			b.Property<int>("TotalCapacity").HasColumnType("integer");
			b.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("UpdatedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.HasKey("Id");
			b.HasAlternateKey("Id", "ProductId").HasName("AK_PreOrderCapacities_Id_ProductId");
			b.HasIndex("ProductId").IsUnique().HasDatabaseName("UX_PreOrderCapacities_ProductId");
			b.ToTable("PreOrderCapacities", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_PreOrderCapacities_Audit_Actors_NotBlank", "\"CreatedBy\" ~ '[^[:space:]]' AND \"UpdatedBy\" ~ '[^[:space:]]'");
				t.HasCheckConstraint("CK_PreOrderCapacities_Audit_Chronology", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
				t.HasCheckConstraint("CK_PreOrderCapacities_CloseAfterCreated", "\"CloseAtUtc\" > \"CreatedAtUtc\"");
				t.HasCheckConstraint("CK_PreOrderCapacities_QuantityAccounting", "\"HeldQuantity\" >= 0 AND \"CommittedQuantity\" >= 0 AND \"RetiredQuantity\" >= 0 AND \"HeldQuantity\" + \"CommittedQuantity\" + \"RetiredQuantity\" <= \"TotalCapacity\"");
				t.HasCheckConstraint("CK_PreOrderCapacities_TotalCapacity_Positive", "\"TotalCapacity\" > 0");
				t.HasCheckConstraint("CK_PreOrderCapacities_Version_Positive", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.PreOrders.PreOrderCapacityMovement", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Actor").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<int>("AvailableQuantityDelta").HasColumnType("integer");
			b.Property<Guid>("CapacityId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("OccurredAtUtc").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<int>("Quantity").HasColumnType("integer");
			b.Property<string>("Reason").IsRequired().HasMaxLength(300)
				.HasColumnType("character varying(300)");
			b.Property<string>("Reference").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<Guid?>("ReservationId").HasColumnType("uuid");
			b.Property<long>("ResultingCapacityVersion").HasColumnType("bigint");
			b.Property<int>("ResultingCommittedQuantity").HasColumnType("integer");
			b.Property<int>("ResultingHeldQuantity").HasColumnType("integer");
			b.Property<int>("ResultingRemainingQuantity").HasColumnType("integer");
			b.Property<int>("ResultingRetiredQuantity").HasColumnType("integer");
			b.Property<string>("Type").IsRequired().HasMaxLength(32)
				.HasColumnType("character varying(32)");
			b.HasKey("Id");
			b.HasIndex("CapacityId").IsUnique().HasDatabaseName("UX_PreOrderCapacityMovements_CapacityId_InitialCapacity")
				.HasFilter("\"Type\" = 'InitialCapacity'");
			b.HasIndex("CapacityId", "ProductId");
			b.HasIndex("CapacityId", "ResultingCapacityVersion").IsUnique().HasDatabaseName("UX_PreOrderCapacityMovements_CapacityId_Version");
			b.HasIndex("CapacityId", "OccurredAtUtc", "Id").IsDescending(false, true, true).HasDatabaseName("IX_PreOrderCapacityMovements_CapacityId_OccurredAtUtc_Id");
			b.HasIndex("ReservationId", "CapacityId", "ProductId");
			b.ToTable("PreOrderCapacityMovements", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_PreOrderCapacityMovements_Evidence_NotBlank", "\"Reason\" ~ '[^[:space:]]' AND \"Reference\" ~ '[^[:space:]]' AND \"Actor\" ~ '[^[:space:]]'");
				t.HasCheckConstraint("CK_PreOrderCapacityMovements_Quantity_Positive", "\"Quantity\" > 0");
				t.HasCheckConstraint("CK_PreOrderCapacityMovements_ResultingQuantities_NonNegative", "\"ResultingRemainingQuantity\" >= 0 AND \"ResultingHeldQuantity\" >= 0 AND \"ResultingCommittedQuantity\" >= 0 AND \"ResultingRetiredQuantity\" >= 0");
				t.HasCheckConstraint("CK_PreOrderCapacityMovements_ResultingVersion_Positive", "\"ResultingCapacityVersion\" > 0");
				t.HasCheckConstraint("CK_PreOrderCapacityMovements_Type_Evidence", "(\"Type\" = 'InitialCapacity' AND \"AvailableQuantityDelta\" = \"Quantity\" AND \"ReservationId\" IS NULL AND \"ResultingCapacityVersion\" = 1 AND \"ResultingRemainingQuantity\" = \"Quantity\" AND \"ResultingHeldQuantity\" = 0 AND \"ResultingCommittedQuantity\" = 0 AND \"ResultingRetiredQuantity\" = 0) OR (\"Type\" = 'Reserved' AND \"AvailableQuantityDelta\" = -\"Quantity\" AND \"ReservationId\" IS NOT NULL) OR (\"Type\" IN ('Released', 'Expired', 'CancellationReopened') AND \"AvailableQuantityDelta\" = \"Quantity\" AND \"ReservationId\" IS NOT NULL) OR (\"Type\" IN ('ReservationConsumed', 'CancellationRetired') AND \"AvailableQuantityDelta\" = 0 AND \"ReservationId\" IS NOT NULL)");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.PreOrders.PreOrderCapacityReservation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("CancellationKind").HasMaxLength(32).HasColumnType("character varying(32)");
			b.Property<Guid>("CapacityId").HasColumnType("uuid");
			b.Property<Guid>("CheckoutAttemptId").HasColumnType("uuid");
			b.Property<Guid?>("ConsumedMovementId").HasColumnType("uuid");
			b.Property<string>("CustomerId").IsRequired().HasMaxLength(450)
				.HasColumnType("character varying(450)");
			b.Property<string>("DepositDisposition").HasMaxLength(32).HasColumnType("character varying(32)");
			b.Property<DateTimeOffset>("ExpiresAtUtc").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<int>("Quantity").HasColumnType("integer");
			b.Property<Guid>("ReserveMovementId").HasColumnType("uuid");
			b.Property<string>("ReserveReason").IsRequired().HasMaxLength(300)
				.HasColumnType("character varying(300)");
			b.Property<string>("ReserveReference").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTimeOffset>("ReservedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("ReservedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Status").IsRequired().HasMaxLength(32)
				.HasColumnType("character varying(32)");
			b.Property<string>("TransitionActor").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTimeOffset?>("TransitionAtUtc").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TransitionMovementId").HasColumnType("uuid");
			b.Property<string>("TransitionReason").HasMaxLength(300).HasColumnType("character varying(300)");
			b.Property<string>("TransitionReference").HasMaxLength(200).HasColumnType("character varying(200)");
			b.HasKey("Id");
			b.HasAlternateKey("Id", "CapacityId", "ProductId").HasName("AK_PreOrderCapacityReservations_Id_CapacityId_ProductId");
			b.HasIndex("CheckoutAttemptId").IsUnique().HasDatabaseName("UX_PreOrderCapacityReservations_CheckoutAttemptId");
			b.HasIndex("ReserveMovementId").IsUnique().HasDatabaseName("UX_PreOrderCapacityReservations_ReserveMovementId");
			b.HasIndex("TransitionMovementId").IsUnique().HasDatabaseName("UX_PreOrderCapacityReservations_TransitionMovementId")
				.HasFilter("\"TransitionMovementId\" IS NOT NULL");
			b.HasIndex("CapacityId", "ProductId");
			b.HasIndex("CapacityId", "Status", "ExpiresAtUtc").HasDatabaseName("IX_PreOrderCapacityReservations_CapacityId_Status_ExpiresAtUtc");
			b.HasIndex("ProductId", "CustomerId", "Status").HasDatabaseName("IX_PreOrderCapacityReservations_ProductId_CustomerId_Status");
			b.ToTable("PreOrderCapacityReservations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_PreOrderCapacityReservations_CancellationPolicy", "\"Status\" <> 'Cancelled' OR ((\"CancellationKind\" IN ('Customer', 'BalanceOverdue') AND \"DepositDisposition\" = 'Forfeited') OR (\"CancellationKind\" = 'AdminOrSupplier' AND \"DepositDisposition\" = 'RefundRequired'))");
				t.HasCheckConstraint("CK_PreOrderCapacityReservations_Evidence_NotBlank", "\"CustomerId\" ~ '[^[:space:]]' AND \"ReserveReason\" ~ '[^[:space:]]' AND \"ReserveReference\" ~ '[^[:space:]]' AND \"ReservedBy\" ~ '[^[:space:]]' AND (\"TransitionActor\" IS NULL OR \"TransitionActor\" ~ '[^[:space:]]') AND (\"TransitionReason\" IS NULL OR \"TransitionReason\" ~ '[^[:space:]]') AND (\"TransitionReference\" IS NULL OR \"TransitionReference\" ~ '[^[:space:]]')");
				t.HasCheckConstraint("CK_PreOrderCapacityReservations_Expiry_AfterReserved", "\"ExpiresAtUtc\" > \"ReservedAtUtc\"");
				t.HasCheckConstraint("CK_PreOrderCapacityReservations_Lifecycle_Evidence", "(\"Status\" = 'Active' AND \"TransitionAtUtc\" IS NULL AND \"TransitionActor\" IS NULL AND \"TransitionReason\" IS NULL AND \"TransitionReference\" IS NULL AND \"TransitionMovementId\" IS NULL AND \"ConsumedMovementId\" IS NULL AND \"CancellationKind\" IS NULL AND \"DepositDisposition\" IS NULL) OR (\"Status\" IN ('Released', 'Expired') AND \"TransitionAtUtc\" IS NOT NULL AND \"TransitionActor\" IS NOT NULL AND \"TransitionReason\" IS NOT NULL AND \"TransitionReference\" IS NOT NULL AND \"TransitionMovementId\" IS NOT NULL AND \"ConsumedMovementId\" IS NULL AND \"CancellationKind\" IS NULL AND \"DepositDisposition\" IS NULL) OR (\"Status\" = 'Consumed' AND \"TransitionAtUtc\" IS NOT NULL AND \"TransitionActor\" IS NOT NULL AND \"TransitionReason\" IS NOT NULL AND \"TransitionReference\" IS NOT NULL AND \"TransitionMovementId\" IS NOT NULL AND \"ConsumedMovementId\" = \"TransitionMovementId\" AND \"CancellationKind\" IS NULL AND \"DepositDisposition\" IS NULL) OR (\"Status\" = 'Cancelled' AND \"TransitionAtUtc\" IS NOT NULL AND \"TransitionActor\" IS NOT NULL AND \"TransitionReason\" IS NOT NULL AND \"TransitionReference\" IS NOT NULL AND \"TransitionMovementId\" IS NOT NULL AND \"ConsumedMovementId\" IS NOT NULL AND \"CancellationKind\" IS NOT NULL AND \"DepositDisposition\" IS NOT NULL)");
				t.HasCheckConstraint("CK_PreOrderCapacityReservations_Quantity_Positive", "\"Quantity\" > 0");
				t.HasCheckConstraint("CK_PreOrderCapacityReservations_Transition_Chronology", "\"TransitionAtUtc\" IS NULL OR (\"TransitionAtUtc\" >= \"ReservedAtUtc\" AND (\"Status\" <> 'Expired' OR \"TransitionAtUtc\" >= \"ExpiresAtUtc\"))");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.Products.Product", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset?>("ArchivedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("ArchivedBy").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<Guid>("BrandId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("CreatedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Description").IsRequired().HasColumnType("text");
			b.Property<string>("DisplayName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("EnglishName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("NormalizedDisplayName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("NormalizedEnglishName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<Guid>("ProductCategoryId").HasColumnType("uuid");
			b.Property<DateTimeOffset?>("PublishedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("PublishedBy").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<string>("SaleType").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<string>("Slug").IsRequired().HasMaxLength(240)
				.HasColumnType("character varying(240)");
			b.Property<string>("Status").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<Guid>("UniverseId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("UpdatedBy").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<long>("Version").IsConcurrencyToken().ValueGeneratedOnAdd()
				.HasColumnType("bigint")
				.HasDefaultValue(1L);
			b.HasKey("Id");
			b.HasIndex("BrandId");
			b.HasIndex("NormalizedDisplayName").IsUnique().HasDatabaseName("UX_Products_NormalizedDisplayName");
			b.HasIndex("NormalizedEnglishName").IsUnique().HasDatabaseName("UX_Products_NormalizedEnglishName");
			b.HasIndex("ProductCategoryId");
			b.HasIndex("Slug").IsUnique().HasDatabaseName("UX_Products_Slug");
			b.HasIndex("UniverseId");
			b.HasIndex("Status", "SaleType").HasDatabaseName("IX_Products_Status_SaleType");
			b.ToTable("Products", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_Products_Audit_Chronology", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"PublishedAtUtc\" IS NULL OR \"PublishedAtUtc\" >= \"CreatedAtUtc\") AND (\"ArchivedAtUtc\" IS NULL OR \"ArchivedAtUtc\" >= \"PublishedAtUtc\")");
				t.HasCheckConstraint("CK_Products_InStock_Price", "\"InStockPrice\" IS NULL OR \"InStockPrice\" > 0");
				t.HasCheckConstraint("CK_Products_Lifecycle_Audit", "(\"Status\" = 'Draft' AND \"PublishedAtUtc\" IS NULL AND \"PublishedBy\" IS NULL AND \"ArchivedAtUtc\" IS NULL AND \"ArchivedBy\" IS NULL) OR (\"Status\" = 'Published' AND \"PublishedAtUtc\" IS NOT NULL AND \"PublishedBy\" IS NOT NULL AND \"ArchivedAtUtc\" IS NULL AND \"ArchivedBy\" IS NULL) OR (\"Status\" = 'Archived' AND \"PublishedAtUtc\" IS NOT NULL AND \"PublishedBy\" IS NOT NULL AND \"ArchivedAtUtc\" IS NOT NULL AND \"ArchivedBy\" IS NOT NULL)");
				t.HasCheckConstraint("CK_Products_Offer_Matches_SaleType", "(\"SaleType\" = 'InStock' AND \"InStockPrice\" IS NOT NULL AND \"PreOrderFullPrice\" IS NULL AND \"PreOrderDepositAmount\" IS NULL AND \"PreOrderCloseAtUtc\" IS NULL AND \"PreOrderEstimatedArrivalMonth\" IS NULL AND \"PreOrderEstimatedArrivalYear\" IS NULL AND \"PreOrderTotalCapacity\" IS NULL AND \"PreOrderMaxPerCustomer\" IS NULL AND \"PreOrderBalancePaymentDays\" IS NULL) OR (\"SaleType\" = 'PreOrder' AND \"InStockPrice\" IS NULL AND \"PreOrderFullPrice\" IS NOT NULL AND \"PreOrderDepositAmount\" IS NOT NULL AND \"PreOrderCloseAtUtc\" IS NOT NULL AND \"PreOrderEstimatedArrivalMonth\" IS NOT NULL AND \"PreOrderEstimatedArrivalYear\" IS NOT NULL AND \"PreOrderTotalCapacity\" IS NOT NULL AND \"PreOrderMaxPerCustomer\" IS NOT NULL AND \"PreOrderBalancePaymentDays\" IS NOT NULL)");
				t.HasCheckConstraint("CK_Products_PreOrder_Amounts", "\"PreOrderFullPrice\" IS NULL OR (\"PreOrderFullPrice\" > 0 AND \"PreOrderDepositAmount\" > 0 AND \"PreOrderDepositAmount\" < \"PreOrderFullPrice\")");
				t.HasCheckConstraint("CK_Products_PreOrder_BalancePaymentDays", "\"PreOrderBalancePaymentDays\" IS NULL OR \"PreOrderBalancePaymentDays\" > 0");
				t.HasCheckConstraint("CK_Products_PreOrder_BangkokCloseTime", "\"PreOrderCloseAtUtc\" IS NULL OR (\"PreOrderCloseAtUtc\" AT TIME ZONE 'Asia/Bangkok')::time = TIME '23:59:59'");
				t.HasCheckConstraint("CK_Products_PreOrder_Capacity", "\"PreOrderTotalCapacity\" IS NULL OR (\"PreOrderTotalCapacity\" > 0 AND \"PreOrderMaxPerCustomer\" > 0 AND \"PreOrderMaxPerCustomer\" <= \"PreOrderTotalCapacity\")");
				t.HasCheckConstraint("CK_Products_PreOrder_CloseAfterCreated", "\"PreOrderCloseAtUtc\" IS NULL OR \"PreOrderCloseAtUtc\" > \"CreatedAtUtc\"");
				t.HasCheckConstraint("CK_Products_PreOrder_EstimatedArrival", "\"PreOrderEstimatedArrivalMonth\" IS NULL OR (\"PreOrderEstimatedArrivalMonth\" BETWEEN 1 AND 12 AND \"PreOrderEstimatedArrivalYear\" BETWEEN 1 AND 9999)");
				t.HasCheckConstraint("CK_Products_PreOrder_EtaNotBeforeCloseMonth", "\"PreOrderCloseAtUtc\" IS NULL OR (\"PreOrderEstimatedArrivalYear\", \"PreOrderEstimatedArrivalMonth\") >= (EXTRACT(YEAR FROM \"PreOrderCloseAtUtc\" AT TIME ZONE 'Asia/Bangkok'), EXTRACT(MONTH FROM \"PreOrderCloseAtUtc\" AT TIME ZONE 'Asia/Bangkok'))");
				t.HasCheckConstraint("CK_Products_Slug_Format", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
				t.HasCheckConstraint("CK_Products_Version_Positive", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("ToyStore.Domain.Products.ProductCharacter", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<Guid>("CharacterId").HasColumnType("uuid");
			b.HasKey("ProductId", "CharacterId");
			b.HasIndex("CharacterId");
			b.ToTable("ProductCharacters", (string?)null);
		});
		modelBuilder.Entity("ToyStore.Domain.Products.ProductImage", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AltText").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<bool>("IsPrimary").HasColumnType("boolean");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<string>("PublicRelativeUrl").IsRequired().HasMaxLength(1000)
				.HasColumnType("character varying(1000)");
			b.Property<int>("SortOrder").HasColumnType("integer");
			b.Property<string>("StorageKey").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.HasKey("Id");
			b.HasIndex("ProductId").IsUnique().HasDatabaseName("UX_ProductImages_ProductId_Primary")
				.HasFilter("\"IsPrimary\"");
			b.HasIndex("StorageKey").IsUnique().HasDatabaseName("UX_ProductImages_StorageKey");
			b.HasIndex("ProductId", "SortOrder").IsUnique().HasDatabaseName("UX_ProductImages_ProductId_SortOrder");
			b.ToTable("ProductImages", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_ProductImages_PrimaryMatchesOrder", "\"IsPrimary\" = (\"SortOrder\" = 0)");
				t.HasCheckConstraint("CK_ProductImages_SortOrder", "\"SortOrder\" >= 0");
			});
		});
		modelBuilder.Entity("ToyStore.Infrastructure.Identity.ApplicationUser", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("Id").HasColumnType("text");
			b.Property<int>("AccessFailedCount").HasColumnType("integer");
			b.Property<string>("ConcurrencyStamp").IsConcurrencyToken().HasColumnType("text");
			b.Property<string>("Email").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<bool>("EmailConfirmed").HasColumnType("boolean");
			b.Property<bool>("LockoutEnabled").HasColumnType("boolean");
			b.Property<DateTimeOffset?>("LockoutEnd").HasColumnType("timestamp with time zone");
			b.Property<bool>("MustChangePassword").HasColumnType("boolean");
			b.Property<string>("NormalizedEmail").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("NormalizedUserName").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("PasswordHash").HasColumnType("text");
			b.Property<string>("PhoneNumber").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<bool>("PhoneNumberConfirmed").HasColumnType("boolean");
			b.Property<string>("SecurityStamp").HasColumnType("text");
			b.Property<bool>("TwoFactorEnabled").HasColumnType("boolean");
			b.Property<string>("UserName").HasMaxLength(256).HasColumnType("character varying(256)");
			b.HasKey("Id");
			b.HasIndex("NormalizedEmail").HasDatabaseName("EmailIndex");
			b.HasIndex("NormalizedUserName").IsUnique().HasDatabaseName("UserNameIndex");
			b.ToTable("AspNetUsers", (string?)null);
		});
		modelBuilder.Entity("ToyStore.Infrastructure.Persistence.MediaCleanupEntry", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("AttemptCount").HasColumnType("integer");
			b.Property<Guid>("EntityId").HasColumnType("uuid");
			b.Property<string>("EntityType").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTimeOffset>("FirstObservedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("LastAttemptAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("Reason").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<DateTimeOffset?>("ResolvedAtUtc").HasColumnType("timestamp with time zone");
			b.Property<string>("StorageKey").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.HasKey("Id");
			b.HasIndex("StorageKey").IsUnique().HasDatabaseName("UX_MediaCleanupEntries_Unresolved_StorageKey")
				.HasFilter("\"ResolvedAtUtc\" IS NULL");
			b.ToTable("MediaCleanupEntries", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_MediaCleanupEntries_AttemptCount_Positive", "\"AttemptCount\" > 0");
			});
		});
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", delegate(EntityTypeBuilder b)
		{
			b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null).WithMany().HasForeignKey("RoleId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", delegate(EntityTypeBuilder b)
		{
			b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null).WithMany().HasForeignKey("RoleId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("ToyStore.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.Carts.Cart", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("CustomerId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.Carts.CartItem", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.Carts.Cart", null).WithMany("Items").HasForeignKey("CartId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("ToyStore.Domain.Products.Product", null).WithMany().HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.Carts.CartOperation", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.Carts.Cart", null).WithMany().HasForeignKey("CartId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.Catalog.Brand", delegate(EntityTypeBuilder b)
		{
			b.OwnsOne("ToyStore.Domain.Catalog.CatalogMediaReference", "Image", delegate(OwnedNavigationBuilder ownedNavigationBuilder)
			{
				ownedNavigationBuilder.Property<Guid>("BrandId").HasColumnType("uuid");
				ownedNavigationBuilder.Property<string>("AltText").IsRequired().HasMaxLength(500)
					.HasColumnType("character varying(500)")
					.HasColumnName("ImageAltText");
				ownedNavigationBuilder.Property<string>("PublicRelativeUrl").IsRequired().HasMaxLength(1000)
					.HasColumnType("character varying(1000)")
					.HasColumnName("ImagePublicRelativeUrl");
				ownedNavigationBuilder.Property<string>("StorageKey").IsRequired().HasMaxLength(500)
					.HasColumnType("character varying(500)")
					.HasColumnName("ImageStorageKey");
				ownedNavigationBuilder.HasKey("BrandId");
				ownedNavigationBuilder.ToTable("Brands");
				ownedNavigationBuilder.WithOwner().HasForeignKey("BrandId");
			});
			b.Navigation("Image");
		});
		modelBuilder.Entity("ToyStore.Domain.Catalog.Character", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.Catalog.Universe", null).WithMany().HasForeignKey("UniverseId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.Catalog.Universe", delegate(EntityTypeBuilder b)
		{
			b.OwnsOne("ToyStore.Domain.Catalog.CatalogMediaReference", "Logo", delegate(OwnedNavigationBuilder ownedNavigationBuilder)
			{
				ownedNavigationBuilder.Property<Guid>("UniverseId").HasColumnType("uuid");
				ownedNavigationBuilder.Property<string>("AltText").IsRequired().HasMaxLength(500)
					.HasColumnType("character varying(500)")
					.HasColumnName("LogoAltText");
				ownedNavigationBuilder.Property<string>("PublicRelativeUrl").IsRequired().HasMaxLength(1000)
					.HasColumnType("character varying(1000)")
					.HasColumnName("LogoPublicRelativeUrl");
				ownedNavigationBuilder.Property<string>("StorageKey").IsRequired().HasMaxLength(500)
					.HasColumnType("character varying(500)")
					.HasColumnName("LogoStorageKey");
				ownedNavigationBuilder.HasKey("UniverseId");
				ownedNavigationBuilder.ToTable("Universes");
				ownedNavigationBuilder.WithOwner().HasForeignKey("UniverseId");
			});
			b.Navigation("Logo");
		});
		modelBuilder.Entity("ToyStore.Domain.Inventory.InventoryItem", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.Products.Product", null).WithOne().HasForeignKey("ToyStore.Domain.Inventory.InventoryItem", "ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.Inventory.StockMovement", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.Inventory.InventoryItem", null).WithMany().HasForeignKey("InventoryItemId", "ProductId")
				.HasPrincipalKey("Id", "ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("ToyStore.Domain.Inventory.StockReservation", null).WithMany().HasForeignKey("ReservationId", "InventoryItemId", "ProductId")
				.HasPrincipalKey("Id", "InventoryItemId", "ProductId")
				.OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity("ToyStore.Domain.Inventory.StockReservation", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.Inventory.StockMovement", null).WithMany().HasForeignKey("ConsumedMovementId")
				.OnDelete(DeleteBehavior.Restrict)
				.HasConstraintName("FK_StockReservations_StockMovements_ConsumedMovementId");
			b.HasOne("ToyStore.Domain.Inventory.InventoryItem", null).WithMany().HasForeignKey("InventoryItemId", "ProductId")
				.HasPrincipalKey("Id", "ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.PreOrders.PreOrderCapacity", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.Products.Product", null).WithOne().HasForeignKey("ToyStore.Domain.PreOrders.PreOrderCapacity", "ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.PreOrders.PreOrderCapacityMovement", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.PreOrders.PreOrderCapacity", null).WithMany().HasForeignKey("CapacityId", "ProductId")
				.HasPrincipalKey("Id", "ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("ToyStore.Domain.PreOrders.PreOrderCapacityReservation", null).WithMany().HasForeignKey("ReservationId", "CapacityId", "ProductId")
				.HasPrincipalKey("Id", "CapacityId", "ProductId")
				.OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity("ToyStore.Domain.PreOrders.PreOrderCapacityReservation", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.PreOrders.PreOrderCapacity", null).WithMany().HasForeignKey("CapacityId", "ProductId")
				.HasPrincipalKey("Id", "ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.Products.Product", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.Catalog.Brand", null).WithMany().HasForeignKey("BrandId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("ToyStore.Domain.Catalog.ProductCategory", null).WithMany().HasForeignKey("ProductCategoryId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("ToyStore.Domain.Catalog.Universe", null).WithMany().HasForeignKey("UniverseId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.OwnsOne("ToyStore.Domain.Products.InStockOffer", "InStockOffer", delegate(OwnedNavigationBuilder ownedNavigationBuilder)
			{
				ownedNavigationBuilder.Property<Guid>("ProductId").HasColumnType("uuid");
				ownedNavigationBuilder.Property<decimal>("Price").HasColumnType("numeric").HasColumnName("InStockPrice");
				ownedNavigationBuilder.HasKey("ProductId");
				ownedNavigationBuilder.HasIndex("Price").HasDatabaseName("IX_Products_InStockPrice");
				ownedNavigationBuilder.ToTable("Products");
				ownedNavigationBuilder.WithOwner().HasForeignKey("ProductId");
			});
			b.OwnsOne("ToyStore.Domain.Products.PreOrderOffer", "PreOrderOffer", delegate(OwnedNavigationBuilder ownedNavigationBuilder)
			{
				ownedNavigationBuilder.Property<Guid>("ProductId").HasColumnType("uuid");
				ownedNavigationBuilder.Property<int>("BalancePaymentDays").HasColumnType("integer").HasColumnName("PreOrderBalancePaymentDays");
				ownedNavigationBuilder.Property<DateTimeOffset>("CloseAtUtc").HasColumnType("timestamp with time zone").HasColumnName("PreOrderCloseAtUtc");
				ownedNavigationBuilder.Property<decimal>("DepositAmount").HasColumnType("numeric").HasColumnName("PreOrderDepositAmount");
				ownedNavigationBuilder.Property<int>("EstimatedArrivalMonth").HasColumnType("integer").HasColumnName("PreOrderEstimatedArrivalMonth");
				ownedNavigationBuilder.Property<int>("EstimatedArrivalYear").HasColumnType("integer").HasColumnName("PreOrderEstimatedArrivalYear");
				ownedNavigationBuilder.Property<decimal>("FullPrice").HasColumnType("numeric").HasColumnName("PreOrderFullPrice");
				ownedNavigationBuilder.Property<int>("MaxPerCustomer").HasColumnType("integer").HasColumnName("PreOrderMaxPerCustomer");
				ownedNavigationBuilder.Property<int>("TotalCapacity").HasColumnType("integer").HasColumnName("PreOrderTotalCapacity");
				ownedNavigationBuilder.HasKey("ProductId");
				ownedNavigationBuilder.ToTable("Products");
				ownedNavigationBuilder.WithOwner().HasForeignKey("ProductId");
			});
			b.Navigation("InStockOffer");
			b.Navigation("PreOrderOffer");
		});
		modelBuilder.Entity("ToyStore.Domain.Products.ProductCharacter", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.Catalog.Character", null).WithMany().HasForeignKey("CharacterId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("ToyStore.Domain.Products.Product", null).WithMany("Characters").HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.Products.ProductImage", delegate(EntityTypeBuilder b)
		{
			b.HasOne("ToyStore.Domain.Products.Product", null).WithMany("Images").HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("ToyStore.Domain.Carts.Cart", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Items");
		});
		modelBuilder.Entity("ToyStore.Domain.Products.Product", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Characters");
			b.Navigation("Images");
		});
	}
}
#pragma warning restore CA1861
