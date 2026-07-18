# M4-02 Catalog Reference Entities Implementation Plan

**Goal:** Model the seeded Product categories, Brand, Universe, Character identity and Product–Character relation required by the no-variant catalog, with deterministic normalization, durable seed identities, media eligibility and auditable archive behavior ready for PostgreSQL constraints in M4-03.

**Architecture:** Production changes stay in `ToyStore.Domain/Catalog`; no EF, MediatR, FluentValidation, filesystem or UI code. Domain types validate local invariants and expose normalized values that M4-03 can persist under unique constraints. Expected duplicate-name/slug/reference collisions remain typed Application `BusinessError` results backed by database constraints; Domain exceptions protect only impossible local state.

**Approved boundaries:**

- Category is required and v1 contains exactly seeded `ArtToy` and `Gundam`; there is no Category Admin CRUD, hierarchy or approved Category slug. The Domain lookup is the minimal stable `Id + Code`; Thai presentation copy is mapped later in the query/UI layer.
- Brand has required DisplayName/EnglishName/generated Slug, optional single image while being prepared, and Active/Archived state. Active + media is required before a Product may be published with it; that cross-aggregate check belongs to the later Product handler.
- Universe has the same local lifecycle/media eligibility and seeded `Marvel`, `DC`, `Unknown`; Admin-created Universes are allowed later.
- Character belongs to one Universe, uses one required name, and is unique by `(UniverseId, NormalizedName)`. There is no Character management page; inline creation uses this identity later.
- A Product owns a read-only collection of unique Product–Character links. M4-02 adds Draft-only Add/Remove Character methods with atomic audit; M4-03 only maps this already-decided ownership and composite uniqueness, while M4-07 supplies search/inline-create use cases.
- Generated slug validation accepts only `^[a-z0-9]+(?:-[a-z0-9]+)*$` with absolute end; slug generation, deterministic collision suffix and concurrent allocation remain M4-03.
- Name normalization is persisted application data, not culture-sensitive database magic: Unicode Form KC, trim, collapse Unicode whitespace to one ASCII space, then invariant uppercase. The exact algorithm is contract-tested before unique indexes depend on it.
- All mutation instants are UTC offset zero, audit time is non-decreasing, actor IDs are nonblank and failed mutations leave state unchanged.
- Media references contain only nonblank durable metadata here. Signature/MIME/size, filesystem/path/public-URL safety, staging and cleanup remain M4-04.
- Later Product publish/select handlers must enforce the complete cross-aggregate contract: Category exists and is one of the approved seed IDs; Brand/Universe exist, are Active and have media; every Character exists and belongs to the Product Universe; archived references cannot be newly selected or used for publication. Archiving a referenced Brand/Universe never cascades, deletes or rewrites Product/Order history.
- Seed constants are flat deterministic scalar data suitable for later EF `HasData`: Category IDs `10000000-0000-0000-0000-000000000001/2`; Universe IDs `20000000-0000-0000-0000-000000000001/2/3`; audit instant `2026-01-01T00:00:00Z`; actor `system:catalog-seed`. The actor is a durable audit identifier, not an Identity foreign key. Seeds never use current time, `Guid.NewGuid()` or an Admin row.

## Domain shape

```text
Catalog/
├── CatalogNameNormalizer.cs
├── CatalogSlug.cs
├── CatalogMediaReference.cs
├── CatalogReferenceStatus.cs
├── CatalogReferenceRule.cs
├── CatalogReferenceRuleException.cs
├── CatalogSeedIds.cs
├── ProductCategory.cs
├── ProductCategorySeeds.cs
├── Brand.cs
├── Universe.cs
├── UniverseSeeds.cs
├── Character.cs
├── CharacterIdentity.cs
└── (Product-owned relation is `Products/ProductCharacter.cs`)
```

Brand and Universe use explicit private state/private setters and may gain private ORM constructors in M4-03; no public invalid constructor is added. Seed definitions are deterministic immutable definitions, not shared mutable aggregate singletons.

## Task 1: Drive deterministic normalization and slug values with micro TDD

**Files:**

- Create: `tests/ToyStore.UnitTests/Domain/Catalog/CatalogValueTests.cs`
- Create: `src/ToyStore.Domain/Catalog/CatalogNameNormalizer.cs`
- Create: `src/ToyStore.Domain/Catalog/CatalogSlug.cs`
- Create: `src/ToyStore.Domain/Catalog/CatalogMediaReference.cs`
- Create: `src/ToyStore.Domain/Catalog/CatalogReferenceStatus.cs`
- Create: `src/ToyStore.Domain/Catalog/CatalogReferenceRule.cs`
- Create: `src/ToyStore.Domain/Catalog/CatalogReferenceRuleException.cs`

- [x] Start with the smallest compiling skeleton, then record assertion RED/GREEN separately for required names, Form-KC equivalence, Unicode whitespace collapse, invariant case normalization and culture independence under Turkish culture.
- [x] Record separate RED/GREEN for generated slug: valid lowercase segments pass; uppercase, leading/trailing/double hyphens, spaces, newline/carriage-return/NUL and non-ASCII fail using an absolute-end rule.
- [x] Refactor Product's private regex validation to delegate to this same CatalogSlug grammar without changing Product's public factory signature; contract-test Product, Brand and Universe so generated-slug rules cannot diverge.
- [x] `CatalogMediaReference` requires nonblank storage key, relative URL and accessible alt text but performs no path/file/provider inspection.
- [x] Assert structural equality and stable hash behavior for CatalogSlug and CatalogMediaReference.
- [x] Every failure exposes a stable `CatalogReferenceRule`; no Thai UI string or expected duplicate error is embedded in Domain.

## Task 2: Define immutable seeded Product categories

**Files:**

- Create: `tests/ToyStore.UnitTests/Domain/Catalog/ProductCategoryTests.cs`
- Create: `src/ToyStore.Domain/Catalog/CatalogSeedIds.cs`
- Create: `src/ToyStore.Domain/Catalog/ProductCategory.cs`
- Create: `src/ToyStore.Domain/Catalog/ProductCategorySeeds.cs`

- [x] Through assertion RED/GREEN, require exactly two definitions with the approved literal IDs, codes `ArtToy`/`Gundam` and no display-name/slug/hierarchy/archive/mutation API.
- [x] Return a read-only fresh collection/immutable values so callers cannot change shared seed state.
- [x] Reject duplicate seed ID/code in a seed-set invariant test so future explicit data changes cannot silently corrupt migration seeds.

## Task 3: Drive Brand lifecycle, normalization, media and audit

**Files:**

- Create: `tests/ToyStore.UnitTests/Domain/Catalog/BrandTests.cs`
- Create: `src/ToyStore.Domain/Catalog/Brand.cs`

- [x] Factory requires nonempty ID, DisplayName, EnglishName, generated CatalogSlug, UTC creation instant and actor; it stores normalized display/English names and starts Active.
- [x] `CanBeUsedByPublishedProduct` is true only when Active with media; attach/replace image uses one atomic audit guard.
- [x] Details update is allowed while Active, recomputes both normalized names and updates slug atomically, but does not perform duplicate checks.
- [x] Archive records ArchivedAt/ArchivedBy and is terminal for M4-02 local mutation APIs; rejected Domain factories/mutations throw `CatalogReferenceRuleException` carrying a stable rule.
- [x] For every mutation, test equal timestamp accepted, backwards/non-UTC/blank actor rejected and failure leaves details/media/status/audit unchanged.

## Task 4: Drive Universe lifecycle and deterministic seeds

**Files:**

- Create: `tests/ToyStore.UnitTests/Domain/Catalog/UniverseTests.cs`
- Create: `src/ToyStore.Domain/Catalog/Universe.cs`
- Create: `src/ToyStore.Domain/Catalog/UniverseSeeds.cs`

- [x] Reuse the same local contract shape as Brand without introducing a shared base entity that hides invariants.
- [x] Require exactly `Marvel`, `DC`, `Unknown` seed definitions with the approved literal IDs, flat display/English/normalized-name/slug/status scalars, literal UTC audit timestamp and `system:catalog-seed` actor.
- [x] Seed Universes may begin without logo and therefore are not publish-eligible until media is attached later; Active alone is insufficient.
- [x] Test update/logo/archive audit atomicity and immutable seed-definition isolation through behavior RED/GREEN; definitions must not depend on clock, random GUID or Identity.

## Task 5: Drive Character scoped identity and Product–Character pairs

**Files:**

- Create: `tests/ToyStore.UnitTests/Domain/Catalog/CharacterTests.cs`
- Create: `src/ToyStore.Domain/Catalog/Character.cs`
- Create: `src/ToyStore.Domain/Catalog/CharacterIdentity.cs`
- Create: `src/ToyStore.Domain/Products/ProductCharacter.cs`
- Modify: `src/ToyStore.Domain/Products/Product.cs`

- [x] Character requires nonempty ID/UniverseId/name, stores NormalizedName and exposes immutable `CharacterIdentity(UniverseId, NormalizedName)`.
- [x] Identity equality treats Form-KC/whitespace/case variants inside one Universe as the same identity and the same normalized name in different Universes as distinct.
- [x] Assert structural equality and stable hash behavior for CharacterIdentity and ProductCharacter.
- [x] Character is creation-only in M4-02; no generic management/update API is added before an approved use case.
- [x] ProductCharacter has no public invalid constructor; Product creates/removes links through Draft-only methods, rejects empty/duplicate CharacterId, exposes a read-only collection and updates audit atomically.
- [x] Rejected Add/Remove (duplicate, missing link, invalid actor/non-UTC/backwards time or non-Draft status) leaves links and audit unchanged; cross-Universe existence/match stays in the later handler.

## Task 6: Architecture contracts and full review

**Files:**

- Create: `tests/ToyStore.UnitTests/Domain/Catalog/CatalogArchitectureTests.cs`
- Modify: `TASKS.md`
- Modify: this plan evidence only

- [x] Assert no Domain type references EF/ASP.NET/MediatR/FluentValidation and no `ProductVariant`, Category hierarchy/Admin service or filesystem validation is introduced.
- [x] Assert public APIs expose read-only seed collections and no public parameterless invalid entity constructor.
- [x] Assert Product owns its Character links; persistence/search layers do not own business mutation.
- [x] Run focused Catalog Domain tests, format, CI build, full Unit and Integration suites, NuGet vulnerability scan and Compose validation; confirm no migration/schema file changed.
- [x] Obtain independent spec review and fix every gap with RED/GREEN tests.
- [x] Obtain independent code-quality review and fix every Critical/Important finding with RED/GREEN tests.
- [x] Mark M4-02 complete only after fresh final verification; set Current Focus to M4-03 and Next Task to M4-04 with exact evidence recorded.

## Explicit non-goals

- No Category Admin CRUD, hierarchy or reorder.
- No database uniqueness, EF mapping, seeds migration or slug allocation query yet.
- No cross-aggregate Character existence/Universe-match query in Domain; Application handlers enforce it later.
- No Brand/Universe/Character Application commands, FluentValidation or UI.
- No media upload/storage/path validation.
- No Product publication handler or cross-aggregate eligibility decision.

## Completion evidence

Completed and independently approved on 2026-07-17.

- Micro-TDD RED: normalization failed 1/22 on Form-KC and Unicode-whitespace behavior before GREEN; Category seeds failed 1/4 on the missing approved seed before GREEN; Brand eligibility failed because Active without media was initially accepted before GREEN; Universe seeds failed 1/15 with two instead of three definitions before GREEN; Product duplicate Character link failed to throw before GREEN. The slug/media characterization assertions were already GREEN on their first compiling-skeleton run because they codified the inherited M4-01 absolute-end grammar and completed value skeleton; test-harness-only reflection/snapshot assertions found during RED were corrected without weakening business expectations.
- Focused GREEN: Catalog + Product Domain tests 160/160.
- Verification: restore clean; format clean; CI build succeeded with 0 warnings/errors; Unit 343/343; Integration 51/51; NuGet vulnerability scan clean; Compose configuration valid and PostgreSQL running.
- Scope audit: production changes are confined to `ToyStore.Domain/Catalog`, Product-owned character relation/audit, and Domain test visibility; no EF/Application/UI/filesystem implementation and no migration or model-snapshot change was made.
- Independent spec review APPROVED. Independent code-quality review APPROVED with no Critical/Important findings.
