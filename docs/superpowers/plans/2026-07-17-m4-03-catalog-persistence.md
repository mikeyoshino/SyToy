# M4-03 Catalog Persistence, Uniqueness and Slug Allocation Plan

**Goal:** Persist the approved Product catalog aggregate and reference entities in PostgreSQL with explicit EF Core mappings, deterministic seeds, final database constraints/indexes and transaction-safe generated-slug allocation, delivered as a reviewed Code First migration applied by the existing startup migration flow.

**Architecture:** `ApplicationDbContext` remains the single Identity + commerce DbContext. Infrastructure owns EF configurations, migrations, Npgsql advisory-lock allocation and database-specific behavior. Application declares only the specialized slug-allocation boundary required by future create commands; Domain keeps deterministic base-slug generation/validation. No generic repository, second DbContext, background service or HTTP migration endpoint is introduced.

**Persistence decisions to prove before migration:**

- Tables: `Products`, `ProductImages`, `ProductCharacters`, `ProductCategories`, `Brands`, `Universes`, `Characters`; existing Identity tables remain unchanged.
- Product stores one required Category/Brand/Universe FK with `Restrict`, owned ordered images with Product cascade, and Product-owned Character links with Product cascade/Character restrict.
- Product, Brand and Universe generated slugs are lowercase Domain values with case-insensitive-equivalent unique protection through validated lowercase storage; normalized display/English names have unique indexes in their own entity type.
- Character uniqueness is `(UniverseId, NormalizedName)`; Category Code is unique; ProductImage has unique StorageKey and `(ProductId, SortOrder)`.
- Product offers share the Products table. Exactly one offer matches SaleType under a PostgreSQL check constraint. Money uses unbounded PostgreSQL `numeric` to preserve the Domain's no-silent-rounding contract until an explicit precision rule is approved.
- Pre-order balance remains derived from FullPrice−Deposit and is not independently persisted. Estimated arrival persists explicit month/year. `timestamptz` values round-trip with UTC offset zero.
- ProductImage persists both SortOrder and IsPrimary, with `IsPrimary = (SortOrder = 0)` check and a unique partial primary-image index per Product. Draft may have zero images; max eight remains an aggregate rule because it is cross-row.
- Product/Brand/Universe generated-slug columns use a database regex check matching the shared Domain absolute-end grammar; name normalized columns are stored, not recomputed by database collation.
- Product gains stored `NormalizedDisplayName`/`NormalizedEnglishName` derived through `CatalogNameNormalizer` at creation. No Product update API is added.
- EF materialization uses private parameterless constructors/backing fields added explicitly to Domain entities/value objects; no public invalid constructor or invariant-bypassing business API is exposed.
- Category/Universe seeds use the literal M4-02 GUID/scalar definitions. Seed audit actor is a string, not an Identity FK.
- Slug allocation scope is separate for Product, Brand and Universe. It generates a base from EnglishName, then returns base, `-2`, `-3`, … using the lowest free suffix while holding one PostgreSQL transaction advisory lock for the entire entity scope/table until the caller's transaction commits. A per-base lock is forbidden because concurrent bases `toy` and `toy-2` overlap. Calling without an active transaction fails; allocation plus entity insert must share that transaction.

## Task 1: RED EF model and layer contracts

**Files:**

- Create: `tests/ToyStore.UnitTests/Infrastructure/CatalogModelConfigurationTests.cs`
- Create: `tests/ToyStore.UnitTests/Architecture/CatalogPersistenceArchitectureTests.cs`
- Modify: `src/ToyStore.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: `src/ToyStore.Infrastructure/Persistence/Configurations/*.cs`

- [ ] Add compiling configuration skeletons, then assertion-RED one entity at a time for table names, keys, required/max-length columns, enum conversions, UTC timestamp column types, owned/backing-field mappings, FK delete behaviors, indexes and named check constraints.
- [ ] Assert the model contains no ProductVariant/SKU/category hierarchy and Infrastructure configuration types do not leak into Domain/Application.
- [ ] Assert `IApplicationDbContext` remains EF-free and no generic repository is added.
- [ ] `ApplicationDbContext.OnModelCreating` must call `base.OnModelCreating(modelBuilder)` before `ApplyConfigurationsFromAssembly`; model tests prove Identity mappings remain present.
- [ ] Add private EF constructors/backing setters only as each round-trip mapping requires them; architecture tests reject public parameterless entity/value constructors.

## Task 2: Map Product aggregate, offers, images and Character links

**Files:**

- Modify: `src/ToyStore.Domain/Products/Product.cs`
- Modify: `src/ToyStore.Domain/Products/ProductImage.cs`
- Modify: `src/ToyStore.Domain/Products/ProductCharacter.cs`
- Modify: `src/ToyStore.Domain/Products/InStockOffer.cs`
- Modify: `src/ToyStore.Domain/Products/PreOrderOffer.cs`
- Modify: `src/ToyStore.Domain/Products/Money.cs`
- Modify: `src/ToyStore.Domain/Products/EstimatedArrival.cs`
- Create: `src/ToyStore.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`
- Create: `src/ToyStore.Infrastructure/Persistence/Configurations/ProductImageConfiguration.cs`
- Create: `src/ToyStore.Infrastructure/Persistence/Configurations/ProductCharacterConfiguration.cs`

- [ ] Add Product normalized-name state at creation and regression-test Form-KC/whitespace/culture behavior without exposing an update API.
- [ ] Remove `readonly` only from Product's `_inStockOffer`/`_preOrderOffer` EF navigation backing fields, keep them private, configure field navigation access and reject any public setter/invalid construction. All other aggregate mutation remains through Domain methods.
- [ ] Map offer backing fields as optional owned/table-split values with Money converters and explicit scalar column names; ignore computed BalanceAmount. Prove model construction/metadata before generating the provisional migration. Task 5 must then prove tracked save/clear/reload and nested optional PreOrderOffer→EstimatedArrival materialization; if EF rejects the nesting, flatten month/year behind a private persistence seam without duplicating authoritative public state, then regenerate the provisional migration.
- [ ] Add persisted ProductImage IsPrimary state updated only by Product order normalization; database check/partial unique index mirrors the aggregate.
- [ ] Add explicit named checks for SaleType/offer exclusivity and local numeric/date/capacity/customer-limit rules so direct SQL cannot create an impossible Product row.
- [ ] Map image/link collection backing fields; ProductCharacter composite key prevents duplicate pairs.
- [ ] Use behavior RED/GREEN model-metadata/materialization-seam tests before continuing to reference entities; PostgreSQL round-trip follows the provisional migration in Task 5.

## Task 3: Map Category, Brand, Universe, Character and deterministic seeds

**Files:**

- Modify: Domain Catalog types only where private EF materialization seams are required
- Create: `src/ToyStore.Infrastructure/Persistence/Configurations/ProductCategoryConfiguration.cs`
- Create: `src/ToyStore.Infrastructure/Persistence/Configurations/BrandConfiguration.cs`
- Create: `src/ToyStore.Infrastructure/Persistence/Configurations/UniverseConfiguration.cs`
- Create: `src/ToyStore.Infrastructure/Persistence/Configurations/CharacterConfiguration.cs`

- [ ] Map Category as `Id + Code` only and seed exactly ArtToy/Gundam.
- [ ] Map CatalogSlug through one value converter and optional Brand image/Universe logo as owned/table-split CatalogMediaReference values.
- [ ] Seed exactly Marvel/DC/Unknown using literal scalar definitions, including Created/Updated audit values and null archive/media values; generated migration must not call runtime factories/current clock.
- [ ] Map Character identity scalars and unique `(UniverseId, NormalizedName)`; ignore computed CharacterIdentity.
- [ ] Add `Restrict` FKs so reference archive/delete never cascades into Product/Character history.

## Task 4: Generate the provisional Code First migration

**Files:**

- Create: `src/ToyStore.Infrastructure/Persistence/Migrations/*_AddCatalogFoundation.cs`
- Modify: `src/ToyStore.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] After model-build/metadata tests pass for all mapped types, generate provisional `AddCatalogFoundation` with Infrastructure project/Web startup project; never use `EnsureCreated`.
- [ ] Inspect provisional migration/designer/snapshot for the approved tables, owned columns, FK delete behavior, named checks/indexes and literal seeds before PostgreSQL integration tests use it.
- [ ] Regenerate this migration/snapshot whenever Tasks 5–6 reveal a model defect; do not stack corrective migrations while M4-03 is uncommitted.

## Task 5: PostgreSQL round-trip, constraint and seed integration tests

**Files:**

- Create: `tests/ToyStore.IntegrationTests/Persistence/CatalogPersistenceTests.cs`
- Create: `tests/ToyStore.IntegrationTests/Persistence/CatalogConstraintTests.cs`
- Modify: `tests/ToyStore.IntegrationTests/Persistence/StartupMigrationTests.cs`
- Modify: test fixture/reset support with a deterministic test-only catalog seed restorer

- [ ] Round-trip Draft In-stock and Pre-order Products with Money precision, Bangkok UTC close, ETA, images/primary order and Character links; materialized aggregate methods/read-only collections still work.
- [ ] The first Product round-trip must explicitly call tracked SaveChanges, clear the change tracker and reload, proving private offer/navigation seams and nested PreOrder EstimatedArrival materialize from the provisional migration.
- [ ] Round-trip Brand/Universe media, audit/archive and Character normalized identity.
- [ ] Respawn deletes all catalog rows while retaining migration history, so every reset must run a deterministic test-only catalog seed restorer after Identity roles. Restore exact ProductCategory/Universe definitions from the shared literal seed contracts, delete no non-test database, and test exact restoration/no duplicates across repeated resets. This is mandatory, not optional.
- [ ] Through direct PostgreSQL writes, prove each unique/check/FK constraint rejects normalized duplicates, bad slug, mixed/missing offers, invalid price/deposit/capacity/limit, inconsistent primary/order and invalid relation IDs.
- [ ] Update startup tests to expect two migrations and all catalog tables, while still proving startup idempotency/fail-fast behavior and no `EnsureCreated`. Test empty startup separately from an Identity-only database migrated explicitly to `InitialIdentity` before Web startup.

## Task 6: Transaction-safe deterministic slug generation/allocation

**Files:**

- Create: `src/ToyStore.Domain/Catalog/CatalogSlugGenerator.cs`
- Create: `tests/ToyStore.UnitTests/Domain/Catalog/CatalogSlugGeneratorTests.cs`
- Create: `src/ToyStore.Application/Catalog/Slugs/ICatalogSlugAllocator.cs`
- Create: `src/ToyStore.Infrastructure/Persistence/CatalogSlugAllocator.cs`
- Modify: `src/ToyStore.Infrastructure/DependencyInjection.cs`
- Create: `tests/ToyStore.IntegrationTests/Persistence/CatalogSlugAllocatorTests.cs`

- [ ] Micro-TDD base generation: Form-KC + invariant lowercase; ASCII letters/digits retained; each punctuation/whitespace run becomes one hyphen; leading/trailing separators removed; empty/non-English result fails with a stable Domain rule. Do not transliterate or allocate suffix in Domain.
- [ ] Application boundary exposes explicit Product/Brand/Universe allocation methods and returns CatalogSlug; it has no EF/Npgsql type.
- [ ] Infrastructure requires `Database.CurrentTransaction`, takes one transaction advisory lock keyed only by entity scope/table, reads all overlapping exact/numeric-suffix values and chooses the lowest free suffix deterministically.
- [ ] Integration tests prove base, gaps, punctuation collision, independent scopes, rollback reuse, concurrent same-base allocation, concurrent overlapping `toy` versus `toy-2`, and separate-scope parallel allocation without collision, unique-constraint failure or deadlock.
- [ ] Registration is scoped and does not introduce a generic repository/service bucket.

## Task 7: Regenerate migration and review the explicit delta SQL

**Files:**

- Regenerate: `src/ToyStore.Infrastructure/Persistence/Migrations/*_AddCatalogFoundation.cs`
- Modify: `src/ToyStore.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`
- Create: `artifacts/migrations/AddCatalogFoundation.sql`

- [ ] Remove/regenerate the single uncommitted migration with Infrastructure project/Web startup project after the final model; never hand-author snapshot/designer output unless correcting a verified EF generation defect.
- [ ] Generate the idempotent SQL artifact explicitly from `20260716183355_InitialIdentity` to `AddCatalogFoundation`, not the full chain. Review that this delta creates only approved catalog tables/constraints/indexes/seeds and migration-history operations; no `DROP`, `TRUNCATE`, Identity alteration, destructive column rewrite or runtime-dependent seed expression.
- [ ] Run migration on an empty PostgreSQL test database and an Identity-only database, then start Web twice to prove startup applies it once and remains healthy.
- [ ] Verify generated Down removes only catalog objects in reverse dependency order; document that production rollback still requires backup/forward-fix review.

## Task 8: Full verification and independent reviews

- [ ] Run focused model/unit/integration persistence and slug-allocation tests.
- [ ] Run format, CI build, full Unit and Integration suites, vulnerability scan and Compose validation.
- [ ] Inspect migration/snapshot diff and generated SQL directly; record table/index/constraint/seed inventory and destructive-scan result.
- [ ] Obtain independent spec review and fix every gap with RED/GREEN tests plus regenerated migration/SQL when model changes.
- [ ] Obtain independent code-quality/migration review and fix every Critical/Important finding.
- [ ] Mark M4-03 complete only after fresh verification; set Current Focus M4-04 and Next Task M4-05 with exact evidence.

## Explicit non-goals

- No Product/Brand/Universe/Character Application create/update handlers or FluentValidation.
- No admin/storefront UI.
- No media bytes/filesystem/staging implementation.
- No Inventory/StockMovement/Pre-order capacity movement tables yet.
- No cache, scheduler, worker or second database.

## Completion evidence

Completed and independently approved on 2026-07-17.

- Final migration: `20260716235231_AddCatalogFoundation`; explicit idempotent delta SQL contains 7 tables, 19 indexes, 16 named checks and 5 literal seed rows, with no destructive or Identity DDL.
- Focused verification: Unit 189/189 and PostgreSQL persistence/constraint/slug/startup 43/43.
- Full verification: Unit 360/360 and Integration 89/89; format clean; CI build 0 warnings/errors; vulnerability scan clean; Compose valid; EF reports no pending model changes.
- The delta SQL applied twice successfully to an Identity-only PostgreSQL database.
- Independent spec and code-quality/migration re-reviews approved the final implementation with no remaining findings.
