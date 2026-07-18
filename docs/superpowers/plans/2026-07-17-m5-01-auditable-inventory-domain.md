# M5-01 Auditable Inventory Domain Plan

## Outcome and boundaries

Model In-stock inventory as a separate Domain aggregate with immutable stock-change evidence and reservation facts. M5-01 proves arithmetic, audit, expiry and transition invariants in memory. It does not add EF configuration, migration, Application handlers, Admin UI, Product stock setters, CheckoutAttempt, Stripe/provider state, warehouse/SKU/lot concepts or a generic repository. PostgreSQL row locking/expected-version enforcement, multi-item all-or-nothing reservation and competing-customer tests belong to M5-02/M7.

## Locked decisions

- `InventoryItem` has its own required identity and `ProductId`; `Product` receives no stock property or mutator.
- `InventoryItem` persists `CreatedAtUtc/CreatedBy` and the aggregate audit watermark `UpdatedAtUtc/UpdatedBy`. Creation initializes both; every real mutation requires UTC, permits an equal timestamp, rejects `changedAtUtc < UpdatedAtUtc`, and advances watermark plus Version together. Rejected/idempotent work leaves the full watermark unchanged while movement/reservation evidence retains its own audit facts.
- `OnHandQuantity` is changed only by initialization, receive, signed adjustment and reservation consumption. Every change returns exactly one immutable `StockMovement`; initial stock emits `InitialStock` evidence even when zero, and no other movement may have a zero delta.
- `StockMovementType` is limited to `InitialStock`, `Received`, `Adjusted` and `ReservationConsumed`. Evidence stores its own ID, inventory/product IDs, signed delta, resulting on-hand quantity, resulting Inventory version, required reason/reference/actor, UTC occurrence time and optional reservation ID.
- `StockReservation` stores immutable identity, Inventory/Product IDs, required scalar `CheckoutAttemptId`, positive quantity and UTC reserved/expiry facts. Its only mutable fields are lifecycle evidence for `Active -> Released | Expired | Consumed`; rows are never deleted or repurposed. InventoryItem is the sole concurrency aggregate and reservation rows do not carry a second optimistic version.
- At time `t`, effective active reservations are status `Active` with `t < ExpiresAtUtc`; exact expiry (`t == ExpiresAtUtc`) is expired for customer-facing availability. `StockReservation.IsEffectiveActiveAt(t)` owns that Domain fact. `InventoryAvailability` computes `EffectiveActiveReservedQuantity` and `AvailableQuantity` only from an explicitly complete same-Inventory reservation snapshot; M5-02 must calculate the production read model from every matching row in SQL, never an arbitrary partial enumerable.
- Provider uncertainty remains fail-closed: `InventoryItem` persists authoritative nonnegative `HeldQuantity` for every unresolved status-Active reservation, including past-due holds. `ReservableQuantity = OnHandQuantity - HeldQuantity`; Reserve adds the quantity and Release/Expire/Consume subtract it atomically with the target reservation transition. A new reservation therefore cannot reuse stock until synchronous maintenance verifies provider state and explicitly transitions the old reservation. Customer-facing `AvailableQuantity(t) = OnHandQuantity - EffectiveActiveReservedQuantity(t)` may temporarily exceed Reservable without enabling oversell.
- The Domain never calls the clock and never hard-codes the 30-minute payment window or 2-minute grace. Callers supply UTC instants and the authoritative `ExpiresAtUtc`; CheckoutAttempt/Stripe policy remains M7.
- Release/expire/consume are controlled through `InventoryItem`, not by a public free-standing reservation mutator, so reservation status and held counter/evidence cannot diverge. A same-terminal retry is a no-op only when terminal status plus the stored immutable idempotency reference (and consumed movement ID when applicable) match; the same status with different evidence is `ReservationEvidenceConflict`, and another terminal is `ReservationTransitionInvalid`.
- Successful counter/lifecycle mutations increment persisted `long Version` exactly once; creation starts at 1 and initial evidence records version 1. Rejected/no-op work changes nothing. Later persistence uses the version/atomic transaction seam; M5-01 does not claim database concurrency.
- Terminal-operation precedence is fixed: validate request identity/ownership/shape first; an already-same terminal transition with matching immutable evidence returns a typed unchanged result without checking a stale expected Inventory version, creating evidence or advancing Version; evidence conflict/another terminal fails; only a real `Active -> terminal` mutation checks expected Inventory version. Version exhaustion and all checked arithmetic/evidence construction are validated before any item/reservation mutation.
- Movement ID is the operation-evidence/idempotency seam and M5-02 adds database uniqueness. Creation returns an explicit `InventoryCreation(InventoryItem, InitialMovement)` result; mutation methods return required evidence/typed changed-or-unchanged results. Movement history is not accumulated as an unbounded aggregate collection. Later sessions load the target reservation and update Inventory held/on-hand counters, reservation and movement in one transaction.
- `InventoryLimits` bounds trimmed audit fields before persistence: actor 200 characters, reason 500 and reference/idempotency key 200. Blank/over-limit values are typed Domain failures so M5-02 cannot introduce database-only validation.

## Task 1: RED public shape, stable rules and immutable evidence

**Files:**

- Create `src/ToyStore.Domain/Inventory/InventoryRule.cs`
- Create `src/ToyStore.Domain/Inventory/InventoryRuleException.cs`
- Create Inventory enums/entities under `src/ToyStore.Domain/Inventory/`
- Create focused tests under `tests/ToyStore.UnitTests/Domain/Inventory/`

- [x] Define stable typed rules for identity, quantity/arithmetic, audit-field limits, movement, reservation/evidence conflict, transition and concurrency/version-exhaustion failures.
- [x] Public-shape tests prove no public invalid constructors/setters, no Product stock mutator, no EF/provider/clock dependency and no warehouse/SKU/lot scope.
- [x] `StockMovement` and reservation identity/quantity/expiry facts are immutable after construction; lifecycle setters are private.

## Task 2: RED initialization, receive and adjustment

- [x] Create accepts non-empty Inventory/Product/movement IDs, initial stock `>= 0`, UTC time and required reason/reference/actor; starts Version 1, initializes created/updated audit and returns one `InitialStock` movement including the zero boundary.
- [x] Receive requires quantity `> 0`, checked arithmetic, valid monotonic audit and expected version; it increments on-hand/version once and returns one `Received` movement.
- [x] Adjust requires a nonzero signed delta, checked arithmetic and a result `>= HeldQuantity`; it increments once and returns one `Adjusted` movement.
- [x] Negative/zero/overflow/stale/audit failures validate before mutation and leave a complete aggregate snapshot unchanged. Cross-mutation tests accept equal audit time, reject time earlier than the prior unrelated mutation and verify watermark/Version advance together.

## Task 3: RED reservation creation and availability

- [x] Reserve requires non-empty reservation/CheckoutAttempt IDs, positive quantity, UTC reserved/expiry times with expiry strictly later and valid expected version. Reservation-ID uniqueness is the M5-02/M7 database/session idempotency seam; Domain validates identity shape without pretending it has historical rows.
- [x] Exact `ReservableQuantity` succeeds; one more fails without mutation. Reserve atomically increments authoritative HeldQuantity and uses all unresolved Active holds for fail-closed safety, including past-due holds awaiting provider reconciliation.
- [x] `StockReservation.IsEffectiveActiveAt(nowUtc)` excludes exact/past expiry. `InventoryAvailability` rejects wrong Inventory/Product rows and duplicate reservation IDs, then computes effective reserved/Available only from a caller-declared complete snapshot; architecture tests require the M5-02 production query to use all matching SQL rows.
- [x] Reservation creation increments Inventory version once and returns an immutable Active reservation; rejected attempts do not change HeldQuantity or Version.

## Task 4: RED release, expiry and consumption transition table

- [x] Release permits an Active hold at any time after reservation creation, subtracts HeldQuantity, keeps on-hand unchanged and records terminal UTC reason/reference/actor evidence.
- [x] Expire is allowed only at `nowUtc >= ExpiresAtUtc`; it is an explicit persistence transition invoked only after Application/provider maintenance has made release safe.
- [x] Consume transitions an unresolved Active reservation once, subtracts its quantity from both HeldQuantity and on-hand, and returns exactly one negative `ReservationConsumed` movement linked to the reservation.
- [x] Consumption cannot make on-hand or conservative availability negative; validate all terminal and movement evidence before changing item/reservation.
- [x] Same-terminal retries with matching reference/movement evidence are no-op; same-status different evidence conflicts; other terminal transitions, wrong-item/product reservations and premature expiry are typed failures. Missing-by-ID is a typed M5-02/M7 session/handler failure because Domain receives the loaded target object; null remains a programmer error. Tests snapshot item, reservation, returned evidence and aggregate Version for every Domain branch.
- [x] Reconciliation arithmetic tests cover unexpired and past-due holds: unexpired Release increases both Available and Reservable by Q; unexpired Consume decreases on-hand plus effective/physical hold equally so both quantities stay unchanged; premature unexpired Expire fails with the full snapshot/version unchanged; past-due Consume decreases customer Available by Q while Reservable stays unchanged; past-due Release/Expire leaves Available unchanged and increases Reservable by Q; no branch makes a quantity negative.
- [x] Terminal tests lock request-shape/ownership -> matching same-terminal no-op -> evidence conflict/other-terminal failure -> expected Inventory-version check for real mutation, plus atomic aggregate version-exhaustion behavior.

## Task 5: Architecture, documentation, verification and review

- [x] Add architecture/document-alignment tests proving Inventory remains Domain-only, Product has no direct stock mutation, movement evidence is mandatory and PostgreSQL/all-or-nothing guarantees remain deferred.
- [x] Update `DOMAIN_RULES.md` only with the locked effective-availability versus fail-closed physical-hold distinction; update `ARCHITECTURE.md` only if the aggregate/evidence boundary needs a durable note.
- [x] Run focused Inventory tests, full Unit/relevant Integration, format, warnings-as-errors build, vulnerability and forbidden-dependency checks; confirm no migration/model delta.
- [x] Obtain independent domain-invariant and scope/concurrency-seam reviews; fix findings with RED/GREEN evidence.
- [x] Mark M5-01 complete only after fresh root verification, set Current Focus M5-02 and continue without pausing.
