# M4-01 Product Aggregate and Conditional Offers Implementation Plan

**Goal:** Implement the first catalog Domain aggregate with a no-variant Product model, exactly one In-stock or Pre-order offer, ordered images, auditable lifecycle timestamps and strict publish/archive invariants.

**Architecture:** Production changes stay entirely in `ToyStore.Domain`; no EF Core, MediatR, Blazor or provider dependency enters the aggregate. Application FluentValidation will later reject user input before handlers construct Domain values, while the Domain still throws one typed invariant exception if trusted application code attempts an impossible state. Persistence, slug collision allocation, cross-aggregate publication eligibility, reference-entity validation, inventory movements and Pre-order capacity movements remain in their explicitly later tasks.

**Approved decisions:**

- Product v1 has no variant and no SKU/variant stock model.
- `SaleType` is exactly `InStock` or `PreOrder`; `ProductStatus` is exactly `Draft`, `Published` or `Archived`.
- Product references one Category, Brand and Universe by ID; Character relations arrive in M4-02.
- Draft may have no image; Published requires at least one image; maximum is eight; order zero is the primary image.
- In-stock stores a positive THB price. `InitialStock` is not Product state and will become the initial audited Inventory movement in M5.
- Pre-order stores positive FullPrice, positive DepositAmount below FullPrice, computed BalanceAmount, selected Bangkok CloseDate converted to `23:59:59 Asia/Bangkok` as UTC, EstimatedArrival month/year, positive TotalCapacity, required MaxPerCustomer not above capacity, and positive BalancePaymentDays defaulting to seven.
- Capacity configuration belongs to the Product offer, but reserved/consumed/remaining capacity and its audit trail arrive in M6.
- All persisted instants accepted by the aggregate use UTC offset zero; actor IDs and required text are nonblank.
- M4-01 supports Draft assembly, ordered-image mutations, Publish and Archive. It intentionally does not define Product editing after Publish, offer replacement, SaleType switching or direct capacity editing; M5/M6 transaction slices must make those decisions with inventory/capacity audit requirements in scope.

**Tech stack:** .NET 10, C# Domain types, xUnit v3. No new package.

## Public Domain shape

```text
Products/
├── Product.cs
├── ProductImage.cs
├── ProductStatus.cs
├── SaleType.cs
├── Money.cs
├── InStockOffer.cs
├── PreOrderOffer.cs
├── EstimatedArrival.cs
├── ProductRule.cs
└── ProductRuleException.cs
```

`Product` exposes private setters/read-only image collection and factory methods for each SaleType. It keeps private nullable `_inStockOffer`/`_preOrderOffer` backing fields, a single authoritative `SaleType` discriminator and read-only typed accessors. Exactly one backing field is populated; M4-03 will map this shape and add a matching database check constraint. A private parameterless ORM constructor may be added when EF mapping is implemented, but no public invalid constructor is allowed. `ProductRuleException` carries a stable `ProductRule` value for invariant-breaking internal calls; later Application handlers map expected input/business failures to `Result<T>` and typed Thai errors rather than exposing the exception to UI.

## Task 1: Define RED contracts for money, offer types and Bangkok close time

**Files:**

- Create: `tests/ToyStore.UnitTests/Domain/Products/ProductOfferTests.cs`

- [x] Assert `SaleType` contains only `InStock` and `PreOrder`, and `ProductStatus` contains only `Draft`, `Published`, `Archived`.
- [x] Assert THB-only Money rejects negative values, preserves decimal input without silent rounding, allows zero for other future commerce values and exposes constant `Currency = "THB"`; offers separately reject zero price/full price, zero deposit and deposit equal to/above full price.
- [x] Assert `BalanceAmount = FullPrice - DepositAmount` and cannot be supplied independently.
- [x] Assert selected CloseDate becomes exactly `23:59:59 Asia/Bangkok` and is stored as UTC offset zero.
- [x] Assert CloseDate uses the exact instant rule `closeAtUtc > nowUtc`: selecting today before Bangkok close passes, equality and after-close fail, and a non-UTC `nowUtc` fails.
- [x] Assert EstimatedArrival is a valid month/year and cannot precede the selected Bangkok local close month; cover December→January year rollover so UTC calendar conversion cannot change the comparison.
- [x] Assert TotalCapacity > 0, MaxPerCustomer > 0 and `<= TotalCapacity`, and BalancePaymentDays > 0 with default 7.
- [x] Add the smallest compiling public skeleton first. Then drive one named behavior at a time through assertion RED/GREEN in this order: Money → InStockOffer → Bangkok conversion → PreOrderOffer. Record each meaningful assertion failure; do not batch a large compile-only RED.

## Task 2: Implement value objects and conditional offers

**Files:**

- Create: `src/ToyStore.Domain/Products/SaleType.cs`
- Create: `src/ToyStore.Domain/Products/ProductStatus.cs`
- Create: `src/ToyStore.Domain/Products/Money.cs`
- Create: `src/ToyStore.Domain/Products/EstimatedArrival.cs`
- Create: `src/ToyStore.Domain/Products/InStockOffer.cs`
- Create: `src/ToyStore.Domain/Products/PreOrderOffer.cs`
- Create: `src/ToyStore.Domain/Products/ProductRule.cs`
- Create: `src/ToyStore.Domain/Products/ProductRuleException.cs`

- [x] Implement immutable THB-only Money and EstimatedArrival values without framework/provider dependencies or a public currency input.
- [x] Centralize invariant failures through `ProductRuleException`; never place Thai UI copy in Domain exceptions.
- [x] Make `InStockOffer` and `PreOrderOffer` immutable and expose only computed balance/UTC close state.
- [x] Keep time conversion deterministic by accepting `nowUtc` and the selected `DateOnly`; use the IANA/ICU `Asia/Bangkok` zone, compare the selected local month/year before UTC conversion and reject non-UTC input.
- [x] Assert the stable `ProductRule` for every invariant group, and keep each named focused test GREEN before adding the next behavior.

## Task 3: Define RED Product identity, offer and audit contracts

**Files:**

- Create: `tests/ToyStore.UnitTests/Domain/Products/ProductTests.cs`

- [x] Assert factories reject empty IDs, blank DisplayName/EnglishName/Description/generated Slug/actor, invalid generated slug pattern, empty Category/Brand/Universe IDs and non-UTC or time-travelling audit instants.
- [x] Assert a new Product is Draft with exactly one matching private offer backing field, a matching discriminator and typed read-only accessors.
- [x] Do not add Product-detail update, offer-replacement or SaleType-switching APIs in M4-01; assert the public API remains limited to factories, Draft image assembly and lifecycle actions.
- [x] Assert invalid publish/archive transitions report stable `ProductRule` values; Published can only Archive through the M4-01 public mutation API.
- [x] Assert Publish records PublishedAt/PublishedBy and Archive records ArchivedAt/ArchivedBy while UpdatedAt/UpdatedBy are non-decreasing; equal timestamps in one transaction are valid and backwards timestamps fail.
- [x] Add Product creation, local publish and archive behaviors one at a time with assertion RED before implementation.

## Task 4: Implement Product aggregate lifecycle

**Files:**

- Create: `src/ToyStore.Domain/Products/Product.cs`

- [x] Use private state/private setters and factories `CreateInStock`/`CreatePreOrder`; no public parameterless constructor is allowed. A private ORM constructor is explicitly permitted in M4-03.
- [x] Validate generated slug with `^[a-z0-9]+(?:-[a-z0-9]+)*$`; generation and collision suffix allocation remain M4-03.
- [x] Implement local Publish/Archive methods with UTC, actor and non-decreasing-time guards; defer all general update/offer-change/SaleType-switch rules to M5/M6.
- [x] Define M4-01 Publish as local aggregate eligibility only: required Product fields/relation IDs, one valid offer and at least one image. M5 Application handlers must also verify referenced Category/Brand/Universe existence, active/archive state and required Brand/Universe media before calling Publish.
- [x] Keep effective `OutOfStock` and `PreOrderClosed` out of ProductStatus; those are derived from inventory/capacity/time later.
- [x] Run Product tests GREEN.

## Task 5: Define RED ordered-image contracts

**Files:**

- Create: `tests/ToyStore.UnitTests/Domain/Products/ProductImageTests.cs`

- [x] Assert image ID, opaque storage key, public relative URL and useful alt text are nonblank. Useful alt text follows the approved accessibility rule; later Application/UI may generate it from Product name.
- [x] Assert a Product accepts at most eight images and rejects duplicate image IDs/storage keys.
- [x] Assert insertion order produces contiguous zero-based SortOrder and exactly the first image is primary.
- [x] Assert reorder requires each current image ID exactly once and then recalculates SortOrder/primary.
- [x] Assert Draft removal recalculates order; image mutation is not exposed after Publish in M4-01.
- [x] Assert Publish fails without an image and succeeds after an image is present.
- [x] Every Add/Remove/Reorder call accepts deterministic `changedAtUtc` and actor, updates UpdatedAt/UpdatedBy, accepts equal time and rejects non-UTC/backwards time. Add each rule through assertion RED/GREEN rather than one batch.

## Task 6: Implement image entity and aggregate operations

**Files:**

- Create: `src/ToyStore.Domain/Products/ProductImage.cs`
- Modify: `src/ToyStore.Domain/Products/Product.cs`

- [x] Let Product own image creation/removal/reordering so callers cannot set SortOrder or primary independently; all three paths call one audit-touch guard.
- [x] Expose images through a read-only view and keep order normalization in one private method.
- [x] Validate only nonblank durable metadata and aggregate ordering/duplicate rules here. Filesystem canonicalization, decoded traversal, configured-root containment, safe public URL grammar, JPEG/PNG/WebP signature, MIME, 5 MB, staging/commit and orphan cleanup all belong to M4-04 storage.
- [x] Run all Product Domain tests GREEN.

## Task 7: Regression, review and task tracking

- [x] Run focused Domain Product tests.
- [x] Run `dotnet format ToyStore.sln --verify-no-changes`.
- [x] Run `dotnet build ToyStore.sln --no-restore -p:CI=true`.
- [x] Run full Unit and Integration suites.
- [x] Run NuGet vulnerability and Compose configuration checks; confirm no migration/schema file changed.
- [x] Obtain independent spec review against M4-01/TASKS/master commerce spec and fix every gap with RED/GREEN tests.
- [x] Obtain independent code-quality review and fix every Critical/Important finding with RED/GREEN tests.
- [x] Mark M4-01 `[x]` only after fresh final verification; set Current Focus to M4-02 and Next Task to M4-03. Record exact counts and review verdict in `TASKS.md` and below.

## Explicit non-goals

- No `ProductVariant`, SKU or variant stock.
- No EF configuration, migration, slug collision query or database uniqueness yet.
- No Brand/Universe/Character/Category entity implementation yet.
- No InventoryItem, StockMovement or capacity movement/reservation yet.
- No Application command/handler/FluentValidation or UI yet.
- No upload bytes, MIME/signature inspection, staging or filesystem work yet.

## Completion evidence

Completed and independently approved on 2026-07-17.

- Compiling Domain skeleton: CI build passed with 0 warnings/errors.
- Assertion RED/GREEN evidence: Money 1 failing assertion → 4/4 green; In-stock 1 failing assertion → 6/6 green; Bangkok conversion 1 failing assertion and exact-boundary group 3/3 failing assertions → 10/10 green; ETA 5 failing assertions → 16/16 green; Pre-order amount/capacity group 8 failing assertions → 25/25 green.
- Product RED/GREEN evidence: creation contracts 14 failing assertions → 14/14 green; initial lifecycle group 2 failing assertions → 19/19 green; image add/metadata/order group 8 failing assertions → 8/8 green; reorder/remove group 4 failing assertions → green; lifecycle audit group 6 failing assertions → green.
- Quality-review RED/GREEN: changing/multi-enumeration reorder 2 failing cases, Product creation at/after Pre-order close 2 failing cases and trailing-newline slug 1 failing case → targeted 12/12 green. Reorder now snapshots once, fully validates input and audit without mutation, then commits the prepared order; failed input preserves both image order and audit.
- Fresh implementation verification: focused Product Domain 83/83; Unit 266/266; Integration 51/51; format clean; CI build 0 warnings/errors; NuGet vulnerability scan clean; Compose config valid; no migration/schema file was created or modified by M4-01.
- Independent spec review APPROVED. Independent code-quality review APPROVED after the three Important findings were fixed and re-reviewed.
