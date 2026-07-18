# M5-02 Inventory Persistence and Admin Stock Slices Plan

## Outcome and boundaries

Persist the M5-01 Inventory aggregate in PostgreSQL and deliver authorized `ReceiveStock`, `AdjustStock`, current-availability and paged-movement-history Application slices. Every durable on-hand change must be paired with exactly one immutable movement in the same transaction, retries must not apply an operation twice, and competing PostgreSQL operations must serialize without overselling or losing updates. M5-02 does not add Product create/update, Admin Razor UI, cart, CheckoutAttempt/provider maintenance, multi-product checkout orchestration, pre-order capacity, dashboard queues or a background worker; those remain M5-03 onward.

## Locked design

- `InventoryItems` is one-to-one with Product through unique `ProductId`. It persists on-hand, authoritative held quantity, the aggregate audit watermark and one `long Version`; Product receives no stock column or mutator.
- `StockMovements` is append-only evidence. `StockReservations` keeps immutable identity/hold facts with one controlled `Active -> terminal` row update and is never deleted or repurposed. Composite ownership FKs bind their `(InventoryItemId, ProductId)` to the same Inventory row. Every `OnHandQuantity` change has exactly one movement; held-only Reserve/Release/Expire transitions are evidenced by the reservation lifecycle and do not invent stock movements. A nullable composite Movement -> Reservation FK preserves ownership, filtered-unique `StockMovement.ReservationId` permits at most one consume movement, and nullable Restrict `StockReservation.ConsumedMovementId -> StockMovement.Id` rejects nonexistent evidence; the reverse scalar cannot prove reciprocal ownership by itself, so the Task 2 session validates both directions under the Inventory lock and fails closed on mismatch. PostgreSQL uses `timestamptz` instant storage plus cross-column lifecycle/status/expiry checks; zero-offset input enforcement remains Domain/Application/Npgsql responsibility.
- `InventoryItem.Version` is an EF concurrency token, while the production mutation session also locks the target row with PostgreSQL `FOR UPDATE`. The row lock linearizes read/check/mutate/save and Domain expected-version rules classify a stale Admin view; database concurrency remains the final guard.
- A caller-stable non-empty `OperationId` is the movement ID and Admin mutation idempotency key. One materializing PostgreSQL `SELECT ... FOR UPDATE` statement atomically locks and loads the requested `(InventoryItemId, ProductId)`, then the session queries/re-queries OperationId before expected-version evaluation. The first successful operation creates the movement; an exact retry uses one provider-neutral intent matcher over both identities, operation type, quantity/delta, expected source/result version, trimmed reason/reference and authorized actor, returning typed unchanged success. Reuse with different intent, including reuse against another Inventory, is a distinct `Conflict` verification and returns `Inventory.OperationConflict`; exact movement with impossible owning Inventory state is `Inconsistent`, fails closed as a logged system failure and is never mislabeled as a normal conflict.
- Each mutation opens a fresh once-only context and transaction through an aggregate-specific `IInventoryMutationSession`; no circuit-scoped tracker or generic repository is introduced. Its minimal capabilities are: lock/load existing composite Inventory ownership, find movement after the lock, load the target reservation tracked after the Inventory lock, add a Domain-created reservation, add a returned movement, and add an `InventoryCreation` item+InitialMovement atomically for M5-03. Reservation lifecycle changes occur only through `InventoryItem.Release/Expire/Consume`; there is no arbitrary update-reservation API. It exposes no public Reserve Application slice, generic repository, generic batch API or replaying execution-strategy callback. Aggregate and evidence are saved and committed together. Definitely rolled-back failures before the commit attempt roll back and clear tracking.
- A commit acknowledgement failure is verified non-cancellably from a fresh context by exact immutable movement evidence plus the owning Inventory row. Exact movement and equal intended Inventory version/state is Committed; exact movement with a greater current Version is Superseded and returns fresh authoritative counters/version; missing Inventory, lower current Version, or equal Version with mismatched counters/audit/ownership is Inconsistent. Absent movement, unavailable or inconsistent evidence returns safe `Persistence.CommitOutcomeUnknown`, and the handler never re-executes the mutation callback. Cancellation before commit or a definitely rolled-back outcome leaves no effect; cancellation during `CommitAsync` follows indeterminate verification and is rethrown only after reconciliation, allowing durable committed evidence or safe unresolved outcome without claiming rollback.
- `ReceiveStock` accepts quantity `> 0`; `AdjustStock` accepts a nonzero signed delta. Both require Inventory and Product identity, operation ID, positive expected version, required bounded reason/reference and authorized actor. FluentValidation is authoritative and emits Thai field messages before the handler runs.
- All four slices require server-side `CanManageProducts`. Authorization runs before validation and before opening mutation sessions/readers. `NotFound`, `StaleVersion`, `InsufficientOnHand` and `OperationConflict` are typed Thai business Results and are not Error-logged. Held/active-sum mismatch, malformed persisted lifecycle, impossible ownership, and unavailable/inconsistent commit verification are system/invariant failures: fail closed, log once at the system boundary and never expose them as normal business validation.
- Availability is a fresh no-tracking SQL aggregation over every reservation row for the target Inventory. It compares the sum of every status-Active physical hold with persisted `HeldQuantity`, excludes exact/past expiry only from effective customer availability, and treats a mismatch as an invariant-breaking system failure rather than publishing unsafe numbers.
- Movement history is fresh no-tracking, deterministic (`OccurredAtUtc DESC`, then `Id DESC`), server-paged and scoped by Inventory/Product ownership. It exposes immutable audit evidence only; no edit/delete action exists.
- `AddInventoryFoundation` is a Code First migration generated from Infrastructure, reviewed as SQL and applied by existing startup `MigrateAsync`; no `EnsureCreated`, scheduler or new package is added.

## Task 1: RED persistence model and database constraints

**Files:**

- Create Inventory EF configurations under `src/ToyStore.Infrastructure/Persistence/Configurations/`
- Update `src/ToyStore.Infrastructure/Persistence/ApplicationDbContext.cs`
- Add focused configuration/architecture tests under `tests/ToyStore.UnitTests/`
- Add PostgreSQL tests under `tests/ToyStore.IntegrationTests/Persistence/`

- [x] Add DbSets and mappings for InventoryItem, StockMovement and StockReservation without Product stock persistence.
- [x] Prove keys, one Inventory per Product, composite ownership, required lengths, timestamptz columns, string enums, Version concurrency and delete restrictions.
- [x] Prove checks/indexes for counter bounds, movement type/evidence shape, InitialStock exactly at version 1, every non-initial movement above version 1, one initial movement per Inventory, one movement per resulting Inventory version, one consume movement per reservation, reservation expiry/status/terminal shape and operational status/expiry/history reads. Keep `CheckoutAttemptId` as an indexed scalar until M7 and prove cross-Inventory/Product/nonexistent/duplicate consumption evidence is rejected as far as the locked FKs permit.
- [x] After the mappings first turn green in model tests, generate `AddInventoryFoundation`; then RED/GREEN PostgreSQL round-trip Domain hydration and immutable evidence/lifecycle persistence, including exact expiry, terminal reservation, initial zero stock and EF consume insert-before-terminal-update ordering across the nullable FK cycle. Never use `EnsureCreated` to bypass this sequencing.

## Task 2: RED operation-scoped mutation persistence

**Files:**

- Create `src/ToyStore.Application/Inventory/IInventoryMutationSession.cs`
- Create Inventory mutation execution/evidence contracts beside the interface
- Create `src/ToyStore.Infrastructure/Persistence/InventoryMutationSession.cs`
- Update Infrastructure dependency registration
- Add Unit contract tests and PostgreSQL session tests

- [x] Open one fresh once-only context/transaction, atomically lock/materialize target Inventory by requested `(InventoryItemId, ProductId)` with one `FOR UPDATE` query, then find OperationId; expose only the exact creation, movement and reservation capabilities locked above. Loading a Consumed reservation validates reciprocal movement identity/type/ownership, `QuantityDelta == -Quantity`, terminal time/actor/reason/reference and a movement version not ahead of the locked Inventory; every non-Consumed status requires no linked consume evidence. Any mismatch is a system invariant failure.
- [x] Exact duplicate OperationId is unchanged success before stale-version evaluation; the shared intent matcher treats changed reason/reference/actor/source version as `Conflict`. Concurrent matching/conflicting retries cover the same and different target Inventory IDs, including the named global movement-PK race with exactly one durable effect and unchanged losing aggregate.
- [x] Aggregate mutation plus movement commit atomically; expected/business failure, pre-commit cancellation and injected mid-save/constraint failure leave both quantities and movement history unchanged and release the row lock. Atomic `InventoryCreation` + InitialMovement persistence is covered here for M5-03 composition.
- [x] Verify indeterminate commit from a fresh context using exact immutable movement plus owning Inventory evidence and never retry the mutation delegate; distinguish `Conflict` from invariant `Inconsistent`, and cover Committed, Superseded, absent, corrupted/inconsistent and unavailable outcomes. Commit-time cancellation verifies non-cancellably and rethrows after reconciliation without an unconditional rollback claim.
- [x] Two independent PostgreSQL operations racing from the same version serialize: at most one stale Admin mutation applies, two last-unit reservation attempts produce one hold, and Release-versus-Consume yields one terminal state with a linked movement only when Consume wins. Sequential current-version Reserve/Adjust interleavings prove typed insufficient precedence; a separate overlapping same-version Reserve-vs-Adjust race proves one success/one stale result, no deadlock/timeout, nonnegative counters, `Held <= OnHand` and correct movement/reservation counts. A direct stale-snapshot test also proves the EF Version concurrency token rolls back the losing movement.

## Task 3: RED ReceiveStock and AdjustStock vertical slices

**Files:**

- Create action folders under `src/ToyStore.Application/Inventory/ReceiveStock/` and `AdjustStock/`
- Add Inventory authorization request/errors/result mapping
- Add focused Application Unit and PostgreSQL handler tests

- [x] Define commands with InventoryItemId, ProductId, OperationId, expected version, quantity/delta, reason and reference; actor is injected only by AuthorizationBehavior.
- [x] Add authoritative FluentValidation with Thai field messages for IDs, quantities, expected version and Inventory field limits.
- [x] Handlers open one session, lock/re-read authoritative Inventory, classify the explicit business/system taxonomy above, call Domain with UTC TimeProvider and persist exactly one movement. Before unchanged exact-retry success, a shared provider-neutral guard rejects owning Inventory version below the movement; at equal version it requires resulting on-hand and audit watermark to match, while a greater version is a valid superseded state. Mismatch fails closed as a system invariant.
- [x] Unauthorized/forbidden requests return before validation/session/database; business failures return typed Thai UI-safe Results and do not become system logs.
- [x] PostgreSQL handler tests prove happy path, exact retry, corrupted exact-retry fail-closed behavior, conflicting retry, stale view, lower-than-held adjustment, rollback and receive/adjust races.

## Task 4: RED availability and movement-history queries

**Files:**

- Create `IInventoryReadStore` and query folders under `src/ToyStore.Application/Inventory/`
- Create `src/ToyStore.Infrastructure/Persistence/InventoryReadStore.cs`
- Add focused Unit and PostgreSQL query tests

- [x] Availability query requires matching InventoryItemId/ProductId, uses TimeProvider UTC and returns both identities, on-hand, physical held, reservable, effective active reserved, customer available, Version, UpdatedAtUtc and UpdatedBy.
- [x] SQL includes every status-Active reservation; tests cover unexpired, exact-expiry, past-due and terminal rows, and fail closed on persisted held/snapshot disagreement.
- [x] Movement query validates matching InventoryItemId/ProductId/page/page size, canonical-clamps pages, orders deterministically and returns type, delta, resulting quantity/version, reason, reference, actor, reservation link and occurred UTC without cross-Inventory leakage.
- [x] Both queries require `CanManageProducts`; authorization-before-validation/reader tests and cancellation propagation pass.

## Task 5: Migration, documentation, verification and review

- [x] Review the already-generated `AddInventoryFoundation` migration/snapshot, inspect the idempotent SQL for destructive operations, and apply it twice against a migrated Identity+Catalog PostgreSQL database.
- [x] Update `ARCHITECTURE.md`/`DOMAIN_RULES.md` for durable M5-02 schema/transaction/idempotency facts; `LOCAL_DEVELOPMENT.md` needs no setup or command change.
- [x] Run focused Inventory Unit/Integration tests, full Unit and Integration suites, format verification, warnings-as-errors build, vulnerability scan, Compose validation, forbidden-dependency checks and EF pending-model check.
- [x] Obtain independent plan, invariant/concurrency, persistence/migration and authorization/code-quality reviews; fix each finding with fresh RED/GREEN evidence.
- [x] Mark M5-02 complete only after root verification, record exact counts/evidence in `TASKS.md`, set Current Focus M5-03 and continue without pausing.
