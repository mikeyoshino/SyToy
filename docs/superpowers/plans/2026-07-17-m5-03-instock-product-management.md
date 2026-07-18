# M5-03 In-stock Product Management Plan

## Outcome and boundaries

Deliver authorized Application slices to create, update, publish and archive In-stock Products. A create atomically persists Product, its ordered images/characters, one InventoryItem and exactly one InitialStock StockMovement (including initial stock zero). Product edits never change stock. This milestone is backend-only: no Admin Razor, Storefront, cart, Pre-order fields/actions or capacity persistence.

## Locked design

- Product gains persisted positive `Version` as the concurrency/audit watermark. Create starts at 1 and each logical Update/Publish/Archive command increments exactly once regardless of how many scalar/image/character values change; stale, rejected and semantic no-op/whitespace-only work increments zero. A complete replacement is validated before any state is applied. Its lifecycle is `Draft -> Published -> Archived`; only Draft permits detail, In-stock price, images and character changes; Publish needs an image and valid authoritative references; Archive is terminal. No SaleType conversion, published edit, unpublish or Pre-order command is introduced.
- Create always uses `SaleType.InStock`, an immutable seeded ArtToy/Gundam category, an active Brand and an active Universe. Character IDs are distinct, must exist, and must belong to that Universe. Publish rechecks that Brand has image and Universe has logo. InitialStock is create-only, nonnegative; it creates Inventory version 1 plus one `InitialStock` evidence row in the same transaction.
- A narrow fresh once-only `IProductMutationSession` owns one PostgreSQL transaction and does not compose the M5-02 Inventory session. It locks Product namespace before a target product (then Brand, Universe and character reads), validates references and uniqueness, loads the full Product image/character aggregate before edits, allocates slug, and persists Product plus `InventoryCreation` atomically. A fresh verification reader checks exact product fields/version/status/audit, ordered image keys, characters and creation Inventory/movement evidence after an indeterminate commit; never replay the callback.
- Commands require server-side `CanManageProducts`, actor injection only through authorization, FluentValidation Thai messages and typed UI-safe business Results. Authorization runs before validation, staging or opening a session. Product duplicate/stale/not-found/reference/lifecycle errors are normal Results; malformed persisted aggregate/evidence or unavailable commit proof is a logged fail-closed system failure.
- Product uploads are staged as one batch when at least one new upload exists; zero-upload create/update still executes the database mutation without calling empty-batch storage. The browser supplies uploads and retained image IDs/order only—never storage keys, URLs or primary flags. Retained plus new images must be at most eight and one deterministic combined order determines the primary image (`SortOrder == 0`). New media commits immediately before database Save/Commit; on definite failure it is discarded/cleaned with non-cancelled compensation and cleanup-ledger fallback. Indeterminate/superseded compensation checks and records each key separately because only a subset may remain referenced. Old media is considered for deletion only after durable DB commit and an unreferenced fresh check per key; a delete/registry failure logs/records an orphan but still returns durable success. Archive retains media.
- ExpectedVersion protects concurrent update/publish/archive. It is not presented as an exact cross-request idempotency key; duplicate acknowledged client submissions result in typed stale/duplicate outcomes, never repeated inventory or media effects.

## Task 1: RED Product domain versioned editing and lifecycle

- [x] Add Version and one atomic complete-replacement Draft edit API to Product with strict In-stock-only shape.
- [x] RED/GREEN domain tests prove create version 1; one logical multi-field/image/character Update, Publish or Archive increments exactly once; stale/rejected/no-op increments zero; Draft->Published->Archived, forbidden published/pre-order transitions and no stock mutation.

## Task 2: RED product mutation persistence and migration

- [x] Add `IProductMutationSession` contracts, fresh once-only PostgreSQL session and product uniqueness/reference lock order.
- [x] Atomically persist Product/images/characters plus `InventoryCreation` and InitialStock movement; add Product Version positive check/default/backfill, EF concurrency token and PostgreSQL one-step version/concurrency/rollback tests.
- [x] Generate/review migration and idempotent SQL without destructive changes.

## Task 3: RED batch product media coordinator

- [x] Add reusable batch staging/commit/compensation coordination without modifying the existing single-reference catalog behavior, including a zero-upload database-only path.
- [x] Test combined retained/new count and deterministic order/primary, zero upload, staging failure, database rollback, indeterminate/superseded per-key partial-reference reconciliation, post-commit old-key deletion and per-key cleanup-ledger fallback.

## Task 4: RED Create and Update In-stock Product slices

- [x] Create commands, Thai FluentValidation, typed errors/results and authorized handlers.
- [x] Unit tests prove unauthorized/forbidden requests stop before FluentValidation, staging, clock and session; cancellation propagates. PostgreSQL tests cover product/reference/category/character validation (distinct/existing/same locked Universe), create/update racing Brand/Universe archive, duplicate races, atomic InitialStock (including zero), expected-version races, full image/character replacement and stock immutability after create.

## Task 5: RED Publish/Archive slices, final verification and review

- [x] Add authorized publish/archive commands with reference/media readiness and stale/lifecycle checks; test authorization ordering and publish/archive races with Brand/Universe archive under the fixed lock order.
- [x] Run focused/full Unit and PostgreSQL suites, format, CI build, vulnerability/Compose/forbidden-dependency/EF model checks and migration script twice.
- [x] Update architecture/domain/task evidence, obtain independent invariant/concurrency, persistence/media and authorization/code-quality reviews, then set Current Focus M5-04.
